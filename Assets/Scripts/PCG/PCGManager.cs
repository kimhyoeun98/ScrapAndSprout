using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Fusion;

public class PCGManager : NetworkBehaviour
{
    [Header("서버 설정")]
    public string fastApiUrl = "http://172.31.51.36:8000";

    [Header("맵 설정")]
    public int mapWidth = 60;
    public int mapHeight = 20;
    public float pollutionLevel = 70f;

    [Header("── 스폰 설정 ──")]
    public float trashZoneWorldOffsetX = -20f;
    public float groundY = -9f;

    [Header("── TrashPile 프리팹 ──")]
    public NetworkObject trashPileSmall;
    public NetworkObject trashPileLarge;
    [Range(0f, 1f)]
    public float largePileChance = 0.3f;
    public float smallPileYOffset = 0f;
    public float largePileYOffset = 1f;

    [Header("── NPC 프리팹 ──")]
    public GameObject npcPrefab;
    public Transform npcSpawnPoint;

    // 하위 호환용
    [HideInInspector] public GameObject[] trashPrefabs;
    [HideInInspector] public Tilemap targetTilemap;
    [HideInInspector] public Tilemap trashTilemap;
    [HideInInspector] public Tilemap safeTilemap;
    [HideInInspector] public Tilemap collisionTilemap;

    private PCGResponse _currentMapData;
    private bool _mapGenerated = false;
    private bool _spawnStarted = false;

    void Awake()
    {
        Debug.Log("[PCG] PCGManager 초기화 — 쓰레기 스폰 전용 모드");
    }

    public override void Spawned()
    {
        Debug.Log($"[PCG] Spawned — IsServer:{Runner?.IsServer}");
        // 맵 생성은 OnSceneLoadDone에서 PhotonManager가 호출
        // Spawned()에서는 하지 않음 (씬 로드 중 스폰 유실 방지)
    }

    /// <summary>
    /// PhotonManager.OnSceneLoadDone에서 호출
    /// 씬 로드 완전히 끝난 후 맵 생성 시작
    /// </summary>
    public void StartMapGeneration()
    {
        if (_spawnStarted)
        {
            Debug.LogWarning("[PCG] 이미 맵 생성 시작됨");
            return;
        }
        _spawnStarted = true;
        Debug.Log("[PCG] 🎯 맵 생성 시작 (OnSceneLoadDone 호출)");
        StartCoroutine(RequestMapFromServer());
    }

    IEnumerator RequestMapFromServer()
    {
        Debug.Log($"[PCG] 🌐 FastAPI 요청: {fastApiUrl}/ai/pcg/map");

        var request = new PCGRequest
        {
            pollutionLevel = pollutionLevel,
            mapWidth = mapWidth,
            mapHeight = mapHeight
        };

        string jsonBody = JsonConvert.SerializeObject(request);

        using (UnityWebRequest www = new UnityWebRequest($"{fastApiUrl}/ai/pcg/map", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"[PCG] ✅ 응답 {responseText.Length}자");

                try
                {
                    var mapData = JsonConvert.DeserializeObject<PCGResponse>(responseText);
                    Debug.Log($"[PCG] 파싱 완료 — 쓰레기: {mapData.trashSpawnPoints.Count}개");

                    _currentMapData = mapData;
                    _mapGenerated = true;

                    RenderMap(mapData);

                    if (Runner != null)
                        RPC_NotifyMapReady();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PCG] ❌ JSON 파싱 실패: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[PCG] ❌ 요청 실패: {www.error}");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_NotifyMapReady()
    {
        Debug.Log("[PCG] RPC_NotifyMapReady");
        if (!HasStateAuthority)
            RPC_RequestMapData();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestMapData()
    {
        if (_mapGenerated && _currentMapData != null)
            SendMapDataInChunks();
    }

    void SendMapDataInChunks()
    {
        string json = JsonConvert.SerializeObject(_currentMapData);
        int chunkSize = 400;
        int totalChunks = Mathf.CeilToInt((float)json.Length / chunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * chunkSize;
            int length = Mathf.Min(chunkSize, json.Length - start);
            RPC_ReceiveMapChunk(i, totalChunks, json.Substring(start, length));
        }
    }

    private string _receivedData = "";
    private int _receivedChunks = 0;
    private int _totalChunks = 0;

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ReceiveMapChunk(int chunkIndex, int totalChunks, string chunk)
    {
        if (HasStateAuthority) return;

        if (chunkIndex == 0) { _receivedData = ""; _receivedChunks = 0; _totalChunks = totalChunks; }

        _receivedData += chunk;
        _receivedChunks++;

        if (_receivedChunks == _totalChunks)
        {
            try
            {
                var mapData = JsonConvert.DeserializeObject<PCGResponse>(_receivedData);
                Debug.Log("[PCG] ✅ 클라이언트 맵 수신 완료");
                RenderMap(mapData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PCG] ❌ 클라이언트 파싱 실패: {e.Message}");
            }
        }
    }

    void RenderMap(PCGResponse data)
    {
        Debug.Log("[PCG] 쓰레기 더미 스폰 시작");

        if (trashPileSmall == null && trashPileLarge == null)
        {
            Debug.LogWarning("[PCG] TrashPile 프리팹 미연결!");
            return;
        }

        // 겹침 방지: 스폰된 위치 목록 관리
        List<Vector3> spawnedPositions = new List<Vector3>();
        float minDistance = 3f; // 더미 간 최소 거리

        int count = 0;
        foreach (var point in data.trashSpawnPoints)
        {
            float worldX = trashZoneWorldOffsetX + point.x;
            float worldY = groundY;
            Vector3 candidatePos = new Vector3(worldX, worldY, -1f);

            // 중앙 구역 (-20 ~ 20) 스킵 (벽/텔레포터 구역)
            if (worldX >= -20f && worldX <= 20f)
            {
                Debug.Log($"[PCG] 중앙 구역 스킵: {worldX}");
                continue;
            }

            // 기존 스폰 위치와 너무 가까우면 스킵
            bool tooClose = false;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(candidatePos, pos) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                Debug.Log($"[PCG] 겹침 방지 — 스킵: {candidatePos}");
                continue;
            }

            spawnedPositions.Add(candidatePos);
            SpawnTrashPile(point);
            count++;
        }
        Debug.Log($"[PCG] ✅ {count}개 스폰 완료");

        if (npcPrefab != null)
            SpawnNPC(data.npcPosition);
    }

    void SpawnTrashPile(TrashPoint point)
    {
        // 소형/대형 결정
        bool isLarge = trashPileLarge != null && Random.value < largePileChance;
        NetworkObject prefab = isLarge ? trashPileLarge : (trashPileSmall ?? trashPileLarge);

        if (prefab == null)
        {
            Debug.LogError("[PCG] prefab null!");
            return;
        }

        float worldX = trashZoneWorldOffsetX + point.x;
        float yOffset = isLarge ? largePileYOffset : smallPileYOffset;
        float worldY = groundY + yOffset;
        Vector3 pos = new Vector3(worldX, worldY, -1f);

        Debug.Log($"[PCG] Spawn 시도 — {prefab.name} at {pos}, IsServer:{Runner?.IsServer}");

        if (Runner == null)
        {
            Debug.LogError("[PCG] Runner null!");
            return;
        }

        if (!Runner.IsServer)
        {
            Debug.LogWarning("[PCG] Client — 스폰 불가");
            return;
        }

        NetworkObject spawned = Runner.Spawn(prefab, pos, Quaternion.identity);
        if (spawned != null)
            Debug.Log($"[PCG] ✅ 성공: {spawned.name} ID:{spawned.Id}");
        else
            Debug.LogError($"[PCG] ❌ Spawn 반환값 null! prefab:{prefab.name}");
    }

    void SpawnNPC(NPCPosition npc)
    {
        if (npcPrefab == null) return;

        Vector3 pos = npcSpawnPoint != null
            ? npcSpawnPoint.position
            : new Vector3(npc.x - mapWidth, groundY, -1f);

        Instantiate(npcPrefab, pos, Quaternion.identity);
        Debug.Log($"[PCG] NPC 스폰: {pos}");
    }

    // 하위 호환용 빈 구현
    public void ChangeTileToGreen(Vector3Int cellPos) { }
    public bool IsPollutedTile(Vector3Int cellPos) => false;
    public bool IsGreenTile(Vector3Int cellPos) => false;
}

[System.Serializable] public class PCGRequest { public float pollutionLevel; public int mapWidth; public int mapHeight; }
[System.Serializable] public class PCGResponse { public int mapWidth; public int mapHeight; public float pollutionLevel; public string initialWeather; public List<TrashPoint> trashSpawnPoints; public List<PlantableArea> plantableAreas; public NPCPosition npcPosition; public List<ZoneData> zones; }
[System.Serializable] public class ZoneData { public int x, y, width, height; public float pollutionLevel; public string role; }
[System.Serializable] public class TrashPoint { public int x, y; }
[System.Serializable] public class PlantableArea { public int x, y, width, height; }
[System.Serializable] public class NPCPosition { public int x, y; }