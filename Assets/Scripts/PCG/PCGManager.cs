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

    private List<Vector3> _spawnedPositions = new List<Vector3>();
    private float _respawnTimer = 0f;
    public float respawnInterval = 30f;
    public int maxTrashCount = 10;

    // ─────────────────────────────────────────
    //  청크 수신용 내부 상태
    // ─────────────────────────────────────────
    private string _receivedData = "";
    private int _receivedChunks = 0;
    private int _totalChunks = 0;

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

        // ✅ TrashSpawner가 씬에 있으면 비활성화 (중복 스폰 방지)
        var trashSpawner = FindFirstObjectByType<TrashSpawner>();
        if (trashSpawner != null)
        {
            trashSpawner.gameObject.SetActive(false);
            Debug.Log("[PCG] TrashSpawner 비활성화 — PCGManager가 스폰 담당");
        }

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

                    // ✅ Host는 스폰까지 직접 실행
                    RenderMap(mapData);

                    // ✅ 클라이언트에게 맵 데이터 전송 알림
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

    // ─────────────────────────────────────────
    //  RPC: 맵 준비 알림 → 클라이언트가 청크 요청
    // ─────────────────────────────────────────

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_NotifyMapReady()
    {
        // ✅ Host 자신은 이미 RenderMap() 완료했으므로 스킵
        if (HasStateAuthority)
        {
            Debug.Log("[PCG] RPC_NotifyMapReady — Host는 이미 스폰 완료");
            return;
        }

        Debug.Log("[PCG] RPC_NotifyMapReady — 클라이언트: 청크 요청");
        RPC_RequestMapData();
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
void RPC_RequestMapData()
{
    // ✅ 맵이 아직 생성 안 됐으면 코루틴으로 대기 후 재시도
    if (!_mapGenerated || _currentMapData == null)
    {
        Debug.LogWarning("[PCG] 맵 아직 미생성 — 3초 후 재시도");
        StartCoroutine(WaitAndSendMapData());
        return;
    }

    Debug.Log("[PCG] 클라이언트 요청 수신 — 청크 전송 시작");
    SendMapDataInChunks();
}

IEnumerator WaitAndSendMapData()
{
    // 최대 10초 대기 (0.5초 간격으로 체크)
    float waited = 0f;
    while (!_mapGenerated && waited < 10f)
    {
        yield return new WaitForSeconds(0.5f);
        waited += 0.5f;
    }

    if (_mapGenerated && _currentMapData != null)
    {
        Debug.Log("[PCG] 맵 생성 완료 확인 — 청크 전송");
        SendMapDataInChunks();
    }
    else
    {
        Debug.LogError("[PCG] ❌ 10초 대기 후에도 맵 미생성 — FastAPI 확인 필요");
    }
}

    void SendMapDataInChunks()
    {
        string json = JsonConvert.SerializeObject(_currentMapData);
        int chunkSize = 400;
        int totalChunks = Mathf.CeilToInt((float)json.Length / chunkSize);

        Debug.Log($"[PCG] 청크 전송: {totalChunks}개 ({json.Length}자)");

        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * chunkSize;
            int length = Mathf.Min(chunkSize, json.Length - start);
            RPC_ReceiveMapChunk(i, totalChunks, json.Substring(start, length));
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ReceiveMapChunk(int chunkIndex, int totalChunks, string chunk)
    {
        // ✅ Host는 이미 처리했으므로 스킵
        if (HasStateAuthority) return;

        if (chunkIndex == 0)
        {
            _receivedData = "";
            _receivedChunks = 0;
            _totalChunks = totalChunks;
            Debug.Log($"[PCG] 청크 수신 시작 — 총 {totalChunks}개");
        }

        _receivedData += chunk;
        _receivedChunks++;

        // ✅ 모든 청크 수신 완료 → 클라이언트도 RenderMap 실행
        if (_receivedChunks == _totalChunks)
        {
            try
            {
                var mapData = JsonConvert.DeserializeObject<PCGResponse>(_receivedData);
                Debug.Log($"[PCG] ✅ 클라이언트 맵 수신 완료 — RenderMap 실행");

                // ✅ 클라이언트는 스폰하지 않고 위치 목록만 저장
                // (Host의 Runner.Spawn이 NetworkObject로 자동 복제됨)
                RegisterSpawnPositionsOnly(mapData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PCG] ❌ 클라이언트 파싱 실패: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 클라이언트 전용: 스폰은 하지 않고 _spawnedPositions만 채움
    /// (재스폰 로직에서 위치 참조용)
    /// </summary>
    void RegisterSpawnPositionsOnly(PCGResponse data)
    {
        _spawnedPositions.Clear();
        float minDistance = 3f;

        foreach (var point in data.trashSpawnPoints)
        {
            float worldX = trashZoneWorldOffsetX + point.x;

            //if (worldX > -20f) continue;
            if (worldX > -25f) continue;

            Vector3 candidatePos = new Vector3(worldX, groundY, -1f);

            bool tooClose = false;
            foreach (var pos in _spawnedPositions)
            {
                if (Vector3.Distance(candidatePos, pos) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                _spawnedPositions.Add(candidatePos);
        }

        Debug.Log($"[PCG] 클라이언트 위치 등록 완료 — {_spawnedPositions.Count}개");
    }

  
    void RenderMap(PCGResponse data)
    {
        Debug.Log("[PCG] 쓰레기 더미 스폰 시작");

        if (trashPileSmall == null && trashPileLarge == null)
        {
            Debug.LogWarning("[PCG] TrashPile 프리팹 미연결!");
            return;
        }

        _spawnedPositions.Clear();

        List<Vector3> spawnedThisRound = new List<Vector3>();
        float minDistance = 3f;

        int count = 0;
        int smallCount = 0;
        int largeCount = 0;

        foreach (var point in data.trashSpawnPoints)
        {
            if (count >= 3) break;

            float worldX = trashZoneWorldOffsetX + point.x;
            float worldY = groundY;
            Vector3 candidatePos = new Vector3(worldX, worldY, -1f);

            if (worldX > -25f)
            {
                Debug.Log($"[PCG] 중앙 구역 스킵: {worldX}");
                continue;
            }

            bool tooClose = false;
            foreach (var pos in spawnedThisRound)
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

            spawnedThisRound.Add(candidatePos);

            bool spawnLarge = largeCount < 1 && smallCount >= 2 && trashPileLarge != null;
            if (spawnLarge)
            {
                SpawnTrashPileFixed(point, true);
                largeCount++;
            }
            else
            {
                SpawnTrashPileFixed(point, false);
                smallCount++;
            }

            count++;
        }

        Debug.Log($"[PCG] ✅ {count}개 스폰 완료");

        if (npcPrefab != null)
        {
            Debug.Log($"[PCG] NPC 스폰 시도 — npcPrefab:{npcPrefab.name}");
            SpawnNPC(data.npcPosition);
        }
        else
        {
            Debug.LogError("[PCG] npcPrefab이 null!");
        }

    }

    void SpawnTrashPileFixed(TrashPoint point, bool isLarge)
    {
        NetworkObject prefab = isLarge ? trashPileLarge : (trashPileSmall ?? trashPileLarge);
        if (prefab == null) return;

        float worldX = trashZoneWorldOffsetX + point.x;
        float yOffset = isLarge ? largePileYOffset : smallPileYOffset;  // ← 추가
        Vector3 pos = new Vector3(worldX, groundY + yOffset, -1f);      // ← 수정

        NetworkObject spawned = Runner.Spawn(prefab, pos, Quaternion.identity);
        if (spawned != null)
        {
            _spawnedPositions.Add(new Vector3(worldX, groundY + yOffset, -1f)); // ← 수정
            Debug.Log($"[PCG] ✅ 최초 스폰 {(isLarge ? "대형" : "소형")}: {pos}");
        }
    }
    // ─────────────────────────────────────────
    //  재스폰 (Host 전용)
    // ─────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;
        if (!_mapGenerated) return;

        _respawnTimer += Runner.DeltaTime;
        if (_respawnTimer >= respawnInterval)
        {
            _respawnTimer = 0f;
            TryRespawnTrash();
        }
    }

 

    void TryRespawnTrash()
    {
        int currentCount = FindObjectsByType<TrashPile>(FindObjectsSortMode.None).Length;
        if (currentCount >= maxTrashCount) return;

        var existingPiles = FindObjectsByType<TrashPile>(FindObjectsSortMode.None);
        List<float> occupiedX = new List<float>();
        foreach (var pile in existingPiles)
            occupiedX.Add(pile.transform.position.x);

        int spawnCount = Mathf.Min(4, maxTrashCount - currentCount);
        int largeCount = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            float randomX;
            int attempt = 0;
            do
            {
                randomX = Random.Range(-55f, -20f);
                attempt++;
            }
            while (occupiedX.Exists(ox => Mathf.Abs(randomX - ox) < 8f) && attempt < 20);

            occupiedX.Add(randomX);

            bool isLarge = largeCount < 2
                           && trashPileLarge != null
                           && trashPileSmall != null
                           && Random.value < largePileChance;

            if (isLarge) largeCount++;

            NetworkObject prefab = isLarge ? trashPileLarge : (trashPileSmall ?? trashPileLarge);
            float yOffset = isLarge ? largePileYOffset : smallPileYOffset;
            Vector3 spawnPos = new Vector3(randomX, groundY + yOffset, -1f);

            Runner.Spawn(prefab, spawnPos, Quaternion.identity);
            Debug.Log($"[PCG] 재스폰 {i + 1}/{spawnCount} — {prefab.name} at {spawnPos}");
        }
    }


    // ─────────────────────────────────────────
    //  개별 TrashPile 스폰 (Host 전용)
    // ─────────────────────────────────────────

    void SpawnTrashPile(TrashPoint point)
    {
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
        //Vector3 pos = new Vector3(worldX, worldY, -1f);
        Vector3 pos = new Vector3(worldX, -8f, -1f);

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
        {
            // ✅ Y는 groundY 고정으로 저장 (재스폰 기준 위치)
            //_spawnedPositions.Add(new Vector3(pos.x, groundY, -1f));
            _spawnedPositions.Add(new Vector3(worldX, groundY, -1f));
            Debug.Log($"[PCG] ✅ 성공: {spawned.name} ID:{spawned.Id} at {pos}");
        }
        else
        {
            Debug.LogError($"[PCG] ❌ Spawn 반환값 null! prefab:{prefab.name}");
        }
    }

    

    void SpawnNPC(NPCPosition npc)
    {
        if (npcPrefab == null || !Runner.IsServer) return;

        Vector3 pos = npcSpawnPoint != null
            ? npcSpawnPoint.position
            : new Vector3(npc.x - mapWidth, groundY, -1f);

        // NetworkObject 대신 GameObject 직접 로드해서 스폰
        var prefab = Resources.Load<NetworkObject>("npc");
        if (prefab == null)
        {
            Debug.LogError("[PCG] Resources에서 npc 로드 실패!");
            return;
        }

        Runner.Spawn(prefab, pos, Quaternion.identity);
        Debug.Log($"[PCG] NPC 네트워크 스폰 완료: {pos}");
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