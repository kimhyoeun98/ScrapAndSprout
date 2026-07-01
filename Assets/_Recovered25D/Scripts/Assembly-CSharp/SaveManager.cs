using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
	private const string FileName = "savegame.json";

	private const int SaveVersion = 1;

	[Tooltip("자동 저장 주기(초)")]
	public float autoSaveInterval = 30f;

	private float _saveTimer;

	public static SaveManager Instance { get; private set; }

	public static GameSaveData Pending { get; private set; }

	public static bool IsContinuing { get; private set; }

	public static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

	public static bool HasSave => File.Exists(SavePath);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Bootstrap()
	{
		if (!(Instance != null))
		{
			GameObject obj = new GameObject("SaveManager");
			Instance = obj.AddComponent<SaveManager>();
			UnityEngine.Object.DontDestroyOnLoad(obj);
			LoadFromDisk();
			Debug.Log("[SaveManager] 부트스트랩 완료 — 저장 경로: " + SavePath);
		}
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
	}

	private static void LoadFromDisk()
	{
		try
		{
			if (!File.Exists(SavePath))
			{
				Debug.Log("[SaveManager] 세이브 없음");
				return;
			}
			GameSaveData gameSaveData = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
			if (gameSaveData == null || gameSaveData.version != 1)
			{
				Debug.LogWarning($"[SaveManager] 세이브 버전 불일치/손상 — 무시 (file={gameSaveData?.version}, expect={1})");
				return;
			}
			Pending = gameSaveData;
			Debug.Log($"[SaveManager] 세이브 감지 — {gameSaveData.savedAtIso}, {gameSaveData.elapsedTime:F0}s, {gameSaveData.teamDecorScore}pt, 데코 {gameSaveData.decorations.Count}개");
		}
		catch (Exception ex)
		{
			Debug.LogError("[SaveManager] 세이브 로드 실패: " + ex.Message);
		}
	}

	public static bool BeginContinue()
	{
		if (Pending == null)
		{
			LoadFromDisk();
		}
		if (Pending == null)
		{
			Debug.LogWarning("[SaveManager] 이어할 세이브가 없습니다");
			return false;
		}
		IsContinuing = true;
		DecoInventoryBridge.ClearDecorations();
		if (Pending.decoRoomDecorations != null)
		{
			foreach (GameSaveData.DecoEntry decoRoomDecoration in Pending.decoRoomDecorations)
			{
				DecoInventoryBridge.AddDeco(decoRoomDecoration.itemName, new Vector3(decoRoomDecoration.x, decoRoomDecoration.y, decoRoomDecoration.z), decoRoomDecoration.flipped);
			}
		}
		Debug.Log($"[SaveManager] 이어하기 시작 — {Pending.elapsedTime:F0}s, {Pending.teamDecorScore}pt");
		return true;
	}

	public static void BeginNewGame()
	{
		IsContinuing = false;
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F9))
		{
			GameManager instance = GameManager.Instance;
			bool flag = instance != null && instance.Object != null && instance.Object.IsValid;
			Debug.Log("[SaveManager][F9] scene=" + SceneManager.GetActiveScene().name + ", " + $"gm={instance != null}, objValid={flag}, " + $"stateAuth={flag && instance.HasStateAuthority}, " + $"isGameplay={IsGameplayScene()}, isHost={IsHost()}");
			Save();
		}
		if (IsGameplayScene() && IsHost())
		{
			_saveTimer += Time.unscaledDeltaTime;
			if (_saveTimer >= autoSaveInterval)
			{
				_saveTimer = 0f;
				Save();
			}
		}
	}

	private void OnApplicationQuit()
	{
		if (IsGameplayScene() && IsHost())
		{
			Save();
		}
	}

	private void OnApplicationPause(bool paused)
	{
		if (paused && IsGameplayScene() && IsHost())
		{
			Save();
		}
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		Debug.Log($"[SaveManager] 씬 로드 감지: {scene.name} (mode={mode})");
		if (scene.name == "TrashZoneScene" || scene.name == "DecoScene")
		{
			StartCoroutine(InitialSaveRoutine());
		}
	}

	private IEnumerator InitialSaveRoutine()
	{
		float t = 0f;
		while (t < 8f && !IsHost())
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		yield return new WaitForSecondsRealtime(2f);
		Debug.Log($"[SaveManager] 첫 저장 시도 — isGameplay={IsGameplayScene()}, isHost={IsHost()} (대기 {t:F1}s)");
		if (IsGameplayScene() && IsHost())
		{
			Save();
			_saveTimer = 0f;
		}
		else
		{
			Debug.LogWarning("[SaveManager] 첫 저장 건너뜀 — 호스트/게임플레이 조건 불충족");
		}
	}

	public void Save()
	{
		try
		{
			GameSaveData gameSaveData = Capture();
			if (gameSaveData == null)
			{
				Debug.LogWarning("[SaveManager] 저장 건너뜀 — GameManager.Instance 없음(Capture null)");
				return;
			}
			string contents = JsonUtility.ToJson(gameSaveData, prettyPrint: true);
			File.WriteAllText(SavePath, contents);
			Debug.Log($"[SaveManager] 저장 완료 — {gameSaveData.elapsedTime:F0}s, {gameSaveData.teamDecorScore}pt, 데코 {gameSaveData.decorations.Count}개 → {SavePath}");
		}
		catch (Exception ex)
		{
			Debug.LogError("[SaveManager] 저장 실패: " + ex.Message);
		}
	}

	private GameSaveData Capture()
	{
		GameManager instance = GameManager.Instance;
		if (instance == null)
		{
			return null;
		}
		GameSaveData gameSaveData = new GameSaveData
		{
			version = 1,
			savedAtIso = DateTime.Now.ToString("s"),
			elapsedTime = instance.ElapsedTime,
			teamDecorScore = instance.SafeDecorScore
		};
		for (int i = 0; i < 4; i++)
		{
			gameSaveData.playerDecorScores[i] = GameManager.LocalPlayerScores[i];
		}
		gameSaveData.placedItems = instance.SnapshotPlacedItems();
		gameSaveData.decorations = instance.SnapshotDecorations();
		foreach (DecoInventoryBridge.PlacedDeco savedDecoration in DecoInventoryBridge.SavedDecorations)
		{
			gameSaveData.decoRoomDecorations.Add(new GameSaveData.DecoEntry
			{
				itemName = savedDecoration.itemName,
				x = savedDecoration.position.x,
				y = savedDecoration.position.y,
				z = savedDecoration.position.z,
				flipped = savedDecoration.flipped
			});
		}
		TrashCollector trashCollector = FindLocalCollector();
		if (trashCollector != null)
		{
			if (trashCollector.inventory != null)
			{
				foreach (KeyValuePair<string, int> item in trashCollector.inventory)
				{
					gameSaveData.inventory.Add(new GameSaveData.InvEntry
					{
						name = item.Key,
						count = item.Value
					});
				}
			}
			if (trashCollector.Object != null && trashCollector.Object.IsValid)
			{
				gameSaveData.gold = trashCollector.gold;
			}
		}
		PlayerMovement playerMovement = FindLocalPlayer();
		if (playerMovement != null)
		{
			gameSaveData.hasHostPlayer = true;
			if (playerMovement.Object != null && playerMovement.Object.IsValid)
			{
				gameSaveData.hostBattery = playerMovement.CurrentBattery;
			}
			Vector3 position = playerMovement.transform.position;
			gameSaveData.hostX = position.x;
			gameSaveData.hostY = position.y;
			gameSaveData.hostZ = position.z;
		}
		return gameSaveData;
	}

	public static void DeleteSave()
	{
		try
		{
			if (File.Exists(SavePath))
			{
				File.Delete(SavePath);
			}
			Pending = null;
			IsContinuing = false;
			Debug.Log("[SaveManager] 세이브 삭제");
		}
		catch (Exception ex)
		{
			Debug.LogError("[SaveManager] 세이브 삭제 실패: " + ex.Message);
		}
	}

	private static bool IsGameplayScene()
	{
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			string text = SceneManager.GetSceneAt(i).name;
			if (text == "TrashZoneScene" || text == "DecoScene")
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsHost()
	{
		GameManager instance = GameManager.Instance;
		if (instance != null && instance.Object != null && instance.Object.IsValid)
		{
			return instance.HasStateAuthority;
		}
		return false;
	}

	private static TrashCollector FindLocalCollector()
	{
		TrashCollector[] array = UnityEngine.Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
		foreach (TrashCollector trashCollector in array)
		{
			if (trashCollector.HasInputAuthority)
			{
				return trashCollector;
			}
		}
		return null;
	}

	private static PlayerMovement FindLocalPlayer()
	{
		PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		foreach (PlayerMovement playerMovement in array)
		{
			if (playerMovement.HasInputAuthority)
			{
				return playerMovement;
			}
		}
		return null;
	}
}
