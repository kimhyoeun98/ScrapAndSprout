using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Fusion;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using UnityEngine.Tilemaps;

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

	public float smallPileYOffset;

	public float largePileYOffset = 1f;

	[Header("── NPC 프리팹 ──")]
	public GameObject npcPrefab;

	public Transform npcSpawnPoint;

	[HideInInspector]
	public GameObject[] trashPrefabs;

	[HideInInspector]
	public Tilemap targetTilemap;

	[HideInInspector]
	public Tilemap trashTilemap;

	[HideInInspector]
	public Tilemap safeTilemap;

	[HideInInspector]
	public Tilemap collisionTilemap;

	private PCGResponse _currentMapData;

	private bool _mapGenerated;

	private bool _spawnStarted;

	private List<Vector3> _spawnedPositions = new List<Vector3>();

	private float _respawnTimer;

	[Header("── 수량 / 재스폰 설정 ──")]
	[Tooltip("게임 시작 시 한 번에 깔아둘 쓰레기 더미 수")]
	public int initialSpawnCount = 12;

	[Tooltip("재스폰 주기(초)")]
	public float respawnInterval = 12f;

	[Tooltip("맵에 동시에 존재할 수 있는 최대 쓰레기 더미 수")]
	public int maxTrashCount = 20;

	[Tooltip("재스폰 1회당 최대 스폰 개수")]
	public int respawnBatch = 6;

	private string _receivedData = "";

	private int _receivedChunks;

	private int _totalChunks;

	private List<Vector3> _trashTileWorld;

	private Vector2[][] _walkablePaths;

	private bool _walkableResolved;

	private void Awake()
	{
		Debug.Log("[PCG] PCGManager 초기화 — 쓰레기 스폰 전용 모드");
	}

	public override void Spawned()
	{
		Debug.Log($"[PCG] Spawned — IsServer:{base.Runner?.IsServer}");
	}

	public void StartMapGeneration()
	{
		if (_spawnStarted)
		{
			Debug.LogWarning("[PCG] 이미 맵 생성 시작됨");
			return;
		}
		_spawnStarted = true;
		TrashSpawner trashSpawner = UnityEngine.Object.FindFirstObjectByType<TrashSpawner>();
		if (trashSpawner != null)
		{
			trashSpawner.gameObject.SetActive(value: false);
			Debug.Log("[PCG] TrashSpawner 비활성화 — PCGManager가 스폰 담당");
		}
		Debug.Log("[PCG] \ud83c\udfaf 맵 생성 시작 (OnSceneLoadDone 호출)");
		StartCoroutine(RequestMapFromServer());
	}

	private IEnumerator RequestMapFromServer()
	{
		Debug.Log("[PCG] \ud83c\udf10 FastAPI 요청: " + fastApiUrl + "/ai/pcg/map");
		string s = JsonConvert.SerializeObject(new PCGRequest
		{
			pollutionLevel = pollutionLevel,
			mapWidth = mapWidth,
			mapHeight = mapHeight
		});
		using UnityWebRequest www = new UnityWebRequest(fastApiUrl + "/ai/pcg/map", "POST");
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		www.uploadHandler = new UploadHandlerRaw(bytes);
		www.downloadHandler = new DownloadHandlerBuffer();
		www.SetRequestHeader("Content-Type", "application/json");
		yield return www.SendWebRequest();
		if (www.result == UnityWebRequest.Result.Success)
		{
			string text = www.downloadHandler.text;
			Debug.Log($"[PCG] ✅ 응답 {text.Length}자");
			try
			{
				PCGResponse pCGResponse = JsonConvert.DeserializeObject<PCGResponse>(text);
				Debug.Log($"[PCG] 파싱 완료 — 쓰레기: {pCGResponse.trashSpawnPoints.Count}개");
				_currentMapData = pCGResponse;
				_mapGenerated = true;
				RenderMap(pCGResponse);
				if (base.Runner != null)
				{
					RPC_NotifyMapReady();
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("[PCG] ❌ JSON 파싱 실패: " + ex.Message);
			}
		}
		else
		{
			Debug.LogError("[PCG] ❌ 요청 실패: " + www.error);
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_NotifyMapReady()
	{

		if (base.HasStateAuthority)
		{
			Debug.Log("[PCG] RPC_NotifyMapReady — Host는 이미 스폰 완료");
			return;
		}
		Debug.Log("[PCG] RPC_NotifyMapReady — 클라이언트: 청크 요청");
		RPC_RequestMapData();
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	private void RPC_RequestMapData()
	{

		if (!_mapGenerated || _currentMapData == null)
		{
			Debug.LogWarning("[PCG] 맵 아직 미생성 — 3초 후 재시도");
			StartCoroutine(WaitAndSendMapData());
		}
		else
		{
			Debug.Log("[PCG] 클라이언트 요청 수신 — 청크 전송 시작");
			SendMapDataInChunks();
		}
	}

	private IEnumerator WaitAndSendMapData()
	{
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

	private void SendMapDataInChunks()
	{
		string text = JsonConvert.SerializeObject(_currentMapData);
		int num = 400;
		int num2 = Mathf.CeilToInt((float)text.Length / (float)num);
		Debug.Log($"[PCG] 청크 전송: {num2}개 ({text.Length}자)");
		for (int i = 0; i < num2; i++)
		{
			int num3 = i * num;
			int length = Mathf.Min(num, text.Length - num3);
			RPC_ReceiveMapChunk(i, num2, text.Substring(num3, length));
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_ReceiveMapChunk(int chunkIndex, int totalChunks, string chunk)
	{

		if (base.HasStateAuthority)
		{
			return;
		}
		if (chunkIndex == 0)
		{
			_receivedData = "";
			_receivedChunks = 0;
			_totalChunks = totalChunks;
			Debug.Log($"[PCG] 청크 수신 시작 — 총 {totalChunks}개");
		}
		_receivedData += chunk;
		_receivedChunks++;
		if (_receivedChunks != _totalChunks)
		{
			return;
		}
		try
		{
			PCGResponse data = JsonConvert.DeserializeObject<PCGResponse>(_receivedData);
			Debug.Log("[PCG] ✅ 클라이언트 맵 수신 완료 — RenderMap 실행");
			RegisterSpawnPositionsOnly(data);
		}
		catch (Exception ex)
		{
			Debug.LogError("[PCG] ❌ 클라이언트 파싱 실패: " + ex.Message);
		}
	}

	private void RegisterSpawnPositionsOnly(PCGResponse data)
	{
		_spawnedPositions.Clear();
		float num = 3f;
		foreach (TrashPoint trashSpawnPoint in data.trashSpawnPoints)
		{
			float num2 = trashZoneWorldOffsetX + (float)trashSpawnPoint.x;
			if (num2 > -25f)
			{
				continue;
			}
			Vector3 vector = new Vector3(num2, groundY, -1f);
			bool flag = false;
			foreach (Vector3 spawnedPosition in _spawnedPositions)
			{
				if (Vector3.Distance(vector, spawnedPosition) < num)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				_spawnedPositions.Add(vector);
			}
		}
		Debug.Log($"[PCG] 클라이언트 위치 등록 완료 — {_spawnedPositions.Count}개");
	}

	private void RenderMap(PCGResponse data)
	{
		Debug.Log("[PCG] 쓰레기 더미 스폰 시작");
		if (trashPileSmall == null && trashPileLarge == null)
		{
			Debug.LogWarning("[PCG] TrashPile 프리팹 미연결!");
			return;
		}
		_spawnedPositions.Clear();
		List<Vector3> list = new List<Vector3>();
		float minDist = 3f;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = Mathf.Max(0, initialSpawnCount);
		for (int i = 0; i < num4; i++)
		{
			if (!TryGetTrashSpawnPos(list, minDist, out var pos))
			{
				Debug.LogWarning("[PCG] 빈 트래시 타일을 못 찾음 — 초기 스폰 중단");
				break;
			}
			list.Add(pos);
			bool flag = trashPileLarge != null && trashPileSmall != null && UnityEngine.Random.value < largePileChance;
			SpawnTrashPileAt(pos, flag);
			if (flag)
			{
				num3++;
			}
			else
			{
				num2++;
			}
			num++;
		}
		Debug.Log($"[PCG] ✅ {num}개 스폰 완료");
		if (npcPrefab != null)
		{
			Debug.Log("[PCG] NPC 스폰 시도 — npcPrefab:" + npcPrefab.name);
			SpawnNPC(data.npcPosition);
		}
		else
		{
			Debug.LogError("[PCG] npcPrefab이 null!");
		}
	}

	private void SpawnTrashPileFixed(TrashPoint point, bool isLarge)
	{
		NetworkObject networkObject = (isLarge ? trashPileLarge : (trashPileSmall ?? trashPileLarge));
		if (!(networkObject == null))
		{
			float x = trashZoneWorldOffsetX + (float)point.x;
			float num = (isLarge ? largePileYOffset : smallPileYOffset);
			Vector3 vector = new Vector3(x, groundY + num, -1f);
			if (base.Runner.Spawn(networkObject, vector, Quaternion.identity) != null)
			{
				_spawnedPositions.Add(new Vector3(x, groundY + num, -1f));
				Debug.Log(string.Format("[PCG] ✅ 최초 스폰 {0}: {1}", isLarge ? "대형" : "소형", vector));
			}
		}
	}

	public override void FixedUpdateNetwork()
	{
		if (base.Runner.IsServer && _mapGenerated)
		{
			_respawnTimer += base.Runner.DeltaTime;
			if (_respawnTimer >= respawnInterval)
			{
				_respawnTimer = 0f;
				TryRespawnTrash();
			}
		}
	}

	private void TryRespawnTrash()
	{
		int num = UnityEngine.Object.FindObjectsByType<TrashPile>(FindObjectsSortMode.None).Length;
		if (num >= maxTrashCount)
		{
			return;
		}
		TrashPile[] array = UnityEngine.Object.FindObjectsByType<TrashPile>(FindObjectsSortMode.None);
		List<Vector3> list = new List<Vector3>();
		TrashPile[] array2 = array;
		foreach (TrashPile trashPile in array2)
		{
			list.Add(trashPile.transform.position);
		}
		int num2 = Mathf.Min(respawnBatch, maxTrashCount - num);
		int num3 = 0;
		for (int j = 0; j < num2; j++)
		{
			if (!TryGetTrashSpawnPos(list, 4f, out var pos))
			{
				Debug.LogWarning("[PCG] 재스폰: 빈 트래시 타일 없음 — 중단");
				break;
			}
			list.Add(pos);
			bool flag = num3 < 2 && trashPileLarge != null && trashPileSmall != null && UnityEngine.Random.value < largePileChance;
			if (flag)
			{
				num3++;
			}
			SpawnTrashPileAt(pos, flag);
			Debug.Log($"[PCG] 재스폰 {j + 1}/{num2} at {pos}");
		}
	}

	private List<Vector3> GetTrashTilePositions()
	{
		if (_trashTileWorld != null)
		{
			return _trashTileWorld;
		}
		_trashTileWorld = new List<Vector3>();
		GameObject gameObject = GameObject.Find("Tilemap_trash");
		if (gameObject == null)
		{
			Debug.LogWarning("[PCG] Tilemap_trash 오브젝트를 못 찾음 — 타일 스폰 불가");
			return _trashTileWorld;
		}
		Tilemap component = gameObject.GetComponent<Tilemap>();
		if (component == null)
		{
			Debug.LogWarning("[PCG] Tilemap_trash에 Tilemap 컴포넌트 없음");
			return _trashTileWorld;
		}
		component.CompressBounds();
		foreach (Vector3Int item in component.cellBounds.allPositionsWithin)
		{
			if (component.HasTile(item))
			{
				_trashTileWorld.Add(component.GetCellCenterWorld(item));
			}
		}
		Debug.Log($"[PCG] Tilemap_trash 타일 {_trashTileWorld.Count}칸 수집");
		return _trashTileWorld;
	}

	private bool TryGetTrashSpawnPos(List<Vector3> occupied, float minDist, out Vector3 pos)
	{
		pos = default(Vector3);
		List<Vector3> trashTilePositions = GetTrashTilePositions();
		if (trashTilePositions.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < 60; i++)
		{
			Vector3 vector = trashTilePositions[UnityEngine.Random.Range(0, trashTilePositions.Count)];
			vector.z = -1f;
			if (!IsWalkable(vector))
			{
				continue;
			}
			bool flag = false;
			foreach (Vector3 item in occupied)
			{
				if (Vector3.Distance(vector, item) < minDist)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				pos = vector;
				return true;
			}
		}
		return false;
	}

	private void EnsureWalkable()
	{
		if (_walkableResolved)
		{
			return;
		}
		_walkableResolved = true;
		GameObject gameObject = GameObject.Find("MapBorder");
		if (gameObject == null)
		{
			Debug.LogWarning("[PCG] MapBorder 못 찾음 — 걷기영역 제한 없이 스폰");
			return;
		}
		PolygonCollider2D component = gameObject.GetComponent<PolygonCollider2D>();
		if (component == null)
		{
			Debug.LogWarning("[PCG] MapBorder에 PolygonCollider2D 없음");
			return;
		}
		_walkablePaths = new Vector2[component.pathCount][];
		for (int i = 0; i < component.pathCount; i++)
		{
			Vector2[] path = component.GetPath(i);
			Vector2[] array = new Vector2[path.Length];
			for (int j = 0; j < path.Length; j++)
			{
				array[j] = gameObject.transform.TransformPoint(path[j]);
			}
			_walkablePaths[i] = array;
		}
		Debug.Log($"[PCG] 걷기영역(MapBorder) 캐시 완료 — path {component.pathCount}개");
	}

	private bool IsWalkable(Vector2 pt)
	{
		EnsureWalkable();
		if (_walkablePaths == null)
		{
			return true;
		}
		bool flag = false;
		Vector2[][] walkablePaths = _walkablePaths;
		foreach (Vector2[] array in walkablePaths)
		{
			int num = 0;
			int num2 = array.Length - 1;
			while (num < array.Length)
			{
				if (array[num].y > pt.y != array[num2].y > pt.y && pt.x < (array[num2].x - array[num].x) * (pt.y - array[num].y) / (array[num2].y - array[num].y) + array[num].x)
				{
					flag = !flag;
				}
				num2 = num++;
			}
		}
		return flag;
	}

	private void SpawnTrashPileAt(Vector3 tilePos, bool isLarge)
	{
		NetworkObject networkObject = (isLarge ? trashPileLarge : (trashPileSmall ?? trashPileLarge));
		if (!(networkObject == null))
		{
			float num = (isLarge ? largePileYOffset : smallPileYOffset);
			Vector3 vector = new Vector3(tilePos.x, tilePos.y + num, -1f);
			if (base.Runner.Spawn(networkObject, vector, Quaternion.identity) != null)
			{
				_spawnedPositions.Add(new Vector3(tilePos.x, tilePos.y, -1f));
				Debug.Log(string.Format("[PCG] ✅ 트래시타일 스폰 {0}: {1}", isLarge ? "대형" : "소형", vector));
			}
		}
	}

	private void SpawnTrashPile(TrashPoint point)
	{
		bool flag = trashPileLarge != null && UnityEngine.Random.value < largePileChance;
		NetworkObject networkObject = (flag ? trashPileLarge : (trashPileSmall ?? trashPileLarge));
		if (networkObject == null)
		{
			Debug.LogError("[PCG] prefab null!");
			return;
		}
		float x = trashZoneWorldOffsetX + (float)point.x;
		float num = (flag ? largePileYOffset : smallPileYOffset);
		_ = groundY;
		Vector3 vector = new Vector3(x, -8f, -1f);
		if (base.Runner == null)
		{
			Debug.LogError("[PCG] Runner null!");
			return;
		}
		if (!base.Runner.IsServer)
		{
			Debug.LogWarning("[PCG] Client — 스폰 불가");
			return;
		}
		NetworkObject networkObject2 = base.Runner.Spawn(networkObject, vector, Quaternion.identity);
		if (networkObject2 != null)
		{
			_spawnedPositions.Add(new Vector3(x, groundY, -1f));
			Debug.Log($"[PCG] ✅ 성공: {networkObject2.name} ID:{networkObject2.Id} at {vector}");
		}
		else
		{
			Debug.LogError("[PCG] ❌ Spawn 반환값 null! prefab:" + networkObject.name);
		}
	}

	private void SpawnNPC(NPCPosition npc)
	{
		if (!(npcPrefab == null) && base.Runner.IsServer)
		{
			Vector3 vector = ((npcSpawnPoint != null) ? npcSpawnPoint.position : new Vector3(npc.x - mapWidth, groundY, -1f));
			NetworkObject networkObject = Resources.Load<NetworkObject>("npc");
			if (networkObject == null)
			{
				Debug.LogError("[PCG] Resources에서 npc 로드 실패!");
				return;
			}
			base.Runner.Spawn(networkObject, vector, Quaternion.identity);
			Debug.Log($"[PCG] NPC 네트워크 스폰 완료: {vector}");
		}
	}

	public void ChangeTileToGreen(Vector3Int cellPos)
	{
	}

	public bool IsPollutedTile(Vector3Int cellPos)
	{
		return false;
	}

	public bool IsGreenTile(Vector3Int cellPos)
	{
		return false;
	}

}
