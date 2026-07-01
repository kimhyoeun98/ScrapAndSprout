using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks, IPublicFacingInterface
{
	private NetworkRunner _runner;

	private NetworkSceneManagerDefault _sceneManager;

	private Dictionary<int, int> _playerCharacterMap = new Dictionary<int, int>();

	private static int _savedBotCount = -1;

	private static int[] _savedBotChars = new int[8];

	public static PhotonManager Instance { get; private set; }

	public string LocalPlayerName { get; private set; } = "";

	public void SetLocalPlayerName(string name)
	{
		LocalPlayerName = name;
		Debug.Log("[PhotonManager] LocalPlayerName 설정: " + name);
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("[PhotonManager] 중복 인스턴스 감지 - 파괴: " + base.gameObject.name);
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		if (UnityEngine.Object.FindFirstObjectByType<LoadingScreen>() == null)
		{
			new GameObject("LoadingScreen").AddComponent<LoadingScreen>();
		}
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[PhotonManager] 초기화 완료");
		Debug.Log("  - DontDestroyOnLoad 설정됨");
		Debug.Log("  - 모든 씬에서 사용 가능");
		Debug.Log("═══════════════════════════════════════════");
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
			Debug.Log("[PhotonManager] 인스턴스 파괴됨");
		}
	}

	public async Task StartHostWithRoom(string roomCode)
	{
		if (string.IsNullOrEmpty(roomCode))
		{
			roomCode = UnityEngine.Random.Range(1000, 9999).ToString();
		}
		Debug.Log("───────────────────────────────────────────");
		Debug.Log("[PhotonManager] Host 모드 시작 요청");
		Debug.Log("  - 방 코드: " + roomCode);
		PlayerPrefs.SetString("RoomCode", roomCode);
		PlayerPrefs.SetString("RoomMode", "Create");
		PlayerPrefs.Save();
		Debug.Log("[PhotonManager] ✅ PlayerPrefs 저장 완료");
		Debug.Log("  - RoomCode: " + PlayerPrefs.GetString("RoomCode"));
		Debug.Log("  - RoomMode: " + PlayerPrefs.GetString("RoomMode"));
		await StartMultiplayerSession(roomCode, isHost: true);
	}

	public async Task StartClientWithRoom(string roomCode)
	{
		if (string.IsNullOrEmpty(roomCode))
		{
			Debug.LogError("[PhotonManager] Client 참여 실패 - 방 코드가 비어있음!");
			return;
		}
		Debug.Log("───────────────────────────────────────────");
		Debug.Log("[PhotonManager] Client 모드 시작 요청");
		Debug.Log("  - 방 코드: " + roomCode);
		PlayerPrefs.SetString("RoomCode", roomCode);
		PlayerPrefs.SetString("RoomMode", "Join");
		PlayerPrefs.Save();
		Debug.Log("[PhotonManager] ✅ PlayerPrefs 저장 완료");
		Debug.Log("  - RoomCode: " + PlayerPrefs.GetString("RoomCode"));
		Debug.Log("  - RoomMode: " + PlayerPrefs.GetString("RoomMode"));
		await StartMultiplayerSession(roomCode, isHost: false);
	}

	private async Task<bool> StartMultiplayerSession(string sessionName, bool isHost)
	{
		bool result = default(bool);
		object obj;
		int num;
		try
		{
			Debug.Log("═══════════════════════════════════════════");
			Debug.Log("[Fusion] 세션 시작 준비");
			Debug.Log("  - Session: " + sessionName);
			Debug.Log("  - Mode: " + (isHost ? "Host" : "Client"));
			Application.runInBackground = true;
			await CleanupRunner();
			_runner = base.gameObject.GetComponent<NetworkRunner>();
			if (_runner == null)
			{
				_runner = base.gameObject.AddComponent<NetworkRunner>();
				Debug.Log("[Fusion] NetworkRunner 컴포넌트 신규 추가");
			}
			else
			{
				Debug.Log("[Fusion] 기존 NetworkRunner 컴포넌트 재사용");
			}
			if (_runner == null)
			{
				Debug.LogError("[Fusion] ❌ NetworkRunner 생성 실패!");
				result = false;
				return result;
			}
			_runner.ProvideInput = true;
			if (base.gameObject.GetComponent<RunnerSimulatePhysics2D>() == null)
			{
				base.gameObject.AddComponent<RunnerSimulatePhysics2D>();
				Debug.Log("[Fusion] RunnerSimulatePhysics2D 추가");
			}
			_sceneManager = base.gameObject.GetComponent<NetworkSceneManagerDefault>();
			if (_sceneManager == null)
			{
				_sceneManager = base.gameObject.AddComponent<NetworkSceneManagerDefault>();
				Debug.Log("[Fusion] NetworkSceneManagerDefault 생성");
			}
			if (_sceneManager == null)
			{
				Debug.LogError("[Fusion] ❌ NetworkSceneManagerDefault 생성 실패!");
				result = false;
				return result;
			}
			Scene activeScene = SceneManager.GetActiveScene();
			int buildIndex = activeScene.buildIndex;
			Debug.Log($"[Fusion] 현재 씬: {activeScene.name} (Index: {buildIndex})");
			GameMode mode = (isHost ? GameMode.Host : GameMode.Client);
			StartGameArgs args = new StartGameArgs
			{
				GameMode = mode,
				SessionName = sessionName,
				Scene = SceneRef.FromIndex(buildIndex),
				SceneManager = _sceneManager,
				PlayerCount = 4
			};
			Task<StartGameResult> startGameTask = _runner.StartGame(args);
			Task timeoutTask = Task.Delay(30000);
			if (await Task.WhenAny(startGameTask, timeoutTask) == timeoutTask)
			{
				Debug.LogError("═══════════════════════════════════════════");
				Debug.LogError("[Fusion] ❌ 30초 타임아웃 - Photon 연결 실패!");
				Debug.LogError("═══════════════════════════════════════════");
				await CleanupRunner();
				SceneManager.LoadScene("LobbyScene");
				result = false;
				return result;
			}
			StartGameResult startGameResult = await startGameTask;
			if (startGameResult.Ok)
			{
				Debug.Log("═══════════════════════════════════════════");
				Debug.Log($"[Fusion] ✅ 접속 성공! Session: {sessionName}, Mode: {mode}");
				Debug.Log("═══════════════════════════════════════════");
				await LoadNetworkScene("waitingRoomScene");
				result = true;
				return result;
			}
			Debug.LogError($"[Fusion] ❌ 접속 실패 - {startGameResult.ShutdownReason}: {startGameResult.ErrorMessage}");
			await CleanupRunner();
			result = false;
			return result;
		}
		catch (Exception ex)
		{
			obj = ex;
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		Exception ex2 = (Exception)obj;
		Debug.LogError("[Fusion] ❌ 예외: " + ex2.GetType().Name + " - " + ex2.Message + "\n" + ex2.StackTrace);
		await CleanupRunner();
		return false;
	}

	private async Task CleanupRunner()
	{
		if (_runner == null)
		{
			Debug.Log("[Fusion] CleanupRunner - 정리할 Runner 없음 (skip)");
			return;
		}
		Debug.Log($"[Fusion] CleanupRunner 시작 - IsRunning: {_runner.IsRunning}");
		if (_runner.IsRunning)
		{
			try
			{
				await _runner.Shutdown();
				Debug.Log("[Fusion] ✅ Runner.Shutdown() 완료");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Fusion] Shutdown 예외 (무시): " + ex.Message);
			}
		}
		else
		{
			Debug.Log("[Fusion] Runner가 이미 정지 상태 - Shutdown 생략");
		}
		_runner = null;
		Debug.Log("[Fusion] ✅ CleanupRunner 완료 (_runner = null)");
	}

	private async Task LoadNetworkScene(string sceneName)
	{
		if (_runner == null || _sceneManager == null)
		{
			Debug.LogError("[Fusion] Runner 또는 SceneManager가 null - 일반 씬 전환");
			SceneManager.LoadScene(sceneName);
			return;
		}
		Debug.Log("[Fusion] 네트워크 씬 전환 시작: " + sceneName);
		int num = -1;
		for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
		{
			if (Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == sceneName)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			Debug.LogError("[Fusion] 씬을 찾을 수 없음: " + sceneName);
			return;
		}
		if (_runner.IsServer)
		{
			_runner.LoadScene(SceneRef.FromIndex(num));
			Debug.Log($"[Fusion] 씬 로드 명령 전송: {sceneName} (Index: {num})");
		}
		else
		{
			Debug.Log("[Fusion] Client는 Host의 씬 전환 대기 중...");
		}
		await Task.Yield();
	}

	public void SetPlayerCharacter(int slotIndex, int characterIndex)
	{
		_playerCharacterMap[slotIndex] = characterIndex;
		Debug.Log($"[PhotonManager] 캐릭터 저장 - 슬롯:{slotIndex} → 캐릭터:{characterIndex}");
		Debug.Log("[PhotonManager] 현재 저장된 캐릭터 맵: " + string.Join(", ", _playerCharacterMap));
	}

	public int GetPlayerCharacter(int slotIndex)
	{
		if (_playerCharacterMap.TryGetValue(slotIndex, out var value))
		{
			Debug.Log($"[PhotonManager] 캐릭터 조회 - 슬롯:{slotIndex} → 캐릭터:{value}");
			return value;
		}
		Debug.LogWarning($"[PhotonManager] 슬롯:{slotIndex} 캐릭터 없음 → 기본값 Alpha(1)");
		return 1;
	}

	public void ClearPlayerCharacterMap()
	{
		_playerCharacterMap.Clear();
		_savedBotCount = -1;
		GameManager.ResetStaticState();
		DecoInventoryBridge.ClearDecorations();
		Debug.Log("[PhotonManager] 캐릭터 맵 초기화 완료");
	}

	public void ResetBotCache()
	{
		_savedBotCount = -1;
		PlayerPrefs.SetInt("BotCount", 0);
		PlayerPrefs.Save();
		Debug.Log("[PhotonManager] 봇 캐시 초기화");
	}

	public bool HasPlayerInSlot(int slotIndex)
	{
		return _playerCharacterMap.ContainsKey(slotIndex);
	}

	public async Task LeaveRoom()
	{
		Debug.Log("[PhotonManager] 방 나가기 시작");
		Debug.Log("[PhotonManager] 현재 Runner 상태: " + ((_runner != null) ? $"IsRunning={_runner.IsRunning}" : "null"));
		ClearPlayerCharacterMap();
		await CleanupRunner();
		await Task.Delay(200);
		Debug.Log("[PhotonManager] ✅ LeaveRoom 완료 - LobbyScene으로 이동");
		SceneManager.LoadScene("LobbyScene");
	}

	public void LoadGameScene()
	{
		if (_runner == null)
		{
			Debug.LogError("[Fusion] Runner가 null - 씬 전환 불가");
			return;
		}
		if (!_runner.IsServer)
		{
			Debug.LogWarning("[Fusion] Server가 아님 - 씬 전환 권한 없음");
			return;
		}
		Debug.Log("[Fusion] TrashZoneScene 전환 시작");
		LoadingScreen.Instance?.Show();
		int sceneIndex = GetSceneIndex("TrashZoneScene");
		if (sceneIndex == -1)
		{
			Debug.LogError("[Fusion] TrashZoneScene을 Build Settings에서 찾을 수 없음!");
			return;
		}
		_runner.LoadScene(SceneRef.FromIndex(sceneIndex));
		Debug.Log($"[Fusion] TrashZoneScene 로드 명령 전송 (Index: {sceneIndex})");
	}

	public void LoadDecoScene()
	{
		TrashCollector[] array = UnityEngine.Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
		foreach (TrashCollector trashCollector in array)
		{
			if (trashCollector.HasInputAuthority)
			{
				DecoInventoryBridge.SaveFrom(trashCollector);
				break;
			}
		}
		if (_runner == null || !_runner.IsServer)
		{
			Debug.LogWarning("[Fusion] DecoScene: Client 로컬 로드");
			SceneManager.LoadScene("DecoScene");
			return;
		}
		int sceneIndex = GetSceneIndex("DecoScene");
		if (sceneIndex == -1)
		{
			Debug.LogError("[Fusion] DecoScene을 Build Settings에서 찾을 수 없음!");
			return;
		}
		LoadingScreen.Instance?.Show();
		_runner.LoadScene(SceneRef.FromIndex(sceneIndex));
		Debug.Log($"[Fusion] DecoScene 로드 명령 전송 (Index: {sceneIndex})");
	}

	public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
	{
		string arg = SceneManager.GetActiveScene().name;
		Debug.Log($"[Fusion] 플레이어 입장: {player.PlayerId} | 씬: {arg}");
		if (runner.IsServer && player != runner.LocalPlayer && arg == "TrashZoneScene")
		{
			GameManager.Instance?.SendDecorationsTo(player);
		}
	}

	public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
	{
		Debug.LogWarning("[Fusion] ⚠\ufe0f Host 연결 끊김 감지 - 로비로 복귀");
		LeaveRoom();
	}

	private int GetSceneIndex(string sceneName)
	{
		for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
		{
			if (Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == sceneName)
			{
				return i;
			}
		}
		return -1;
	}

	public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
	{
		Debug.Log($"[Fusion] 플레이어 퇴장: {player.PlayerId}");
		if (WaitingRoomManager.Instance != null)
		{
			WaitingRoomManager.Instance.OnPlayerLeft(player);
		}
	}

	public void OnInput(NetworkRunner runner, NetworkInput input)
	{
		NetworkInputData value = default(NetworkInputData);
		if (TrashZoneChat.IsTyping)
		{
			input.Set(value);
			return;
		}
		if (MiningMinigame.Instance != null && MiningMinigame.Instance.IsPlaying)
		{
			input.Set(value);
			return;
		}
		value.direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
		value.interact = Input.GetKeyDown(KeyCode.E);
		value.teleport = Input.GetKeyDown(KeyCode.T);
		input.Set(value);
	}

	public void OnConnectedToServer(NetworkRunner runner)
	{
		Debug.Log("[Fusion] ✅ 서버 연결 성공");
	}

	public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
	{
		Debug.LogWarning($"[Fusion] ❌ 서버 연결 끊김: {reason}");
		string text = SceneManager.GetActiveScene().name;
		if (text != "LobbyScene" && text != "LoginScene")
		{
			Debug.LogWarning("[Fusion] 게임 중 연결 끊김 - 로비로 강제 복귀");
			PlayerPrefs.SetString("DisconnectReason", reason.ToString());
			PlayerPrefs.Save();
			_runner = null;
			SceneManager.LoadScene("LobbyScene");
		}
	}

	public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
	{
		Debug.LogError($"[Fusion] ❌ 연결 실패: {reason}");
	}

	public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
	{
		Debug.Log($"[Fusion] Shutdown: {shutdownReason}");
		string text = SceneManager.GetActiveScene().name;
		if (text != "LobbyScene" && text != "LoginScene")
		{
			Debug.LogWarning($"[Fusion] 씬 [{text}] 에서 Shutdown({shutdownReason}) 감지 → 로비 복귀");
			_runner = null;
			SceneManager.LoadScene("LobbyScene");
		}
	}

	public void OnSceneLoadStart(NetworkRunner runner)
	{
		Debug.Log("[Fusion] 씬 로드 시작");
		LoadingScreen.Instance?.Show();
	}

	public void OnSceneLoadDone(NetworkRunner runner)
	{
		Debug.Log("[Fusion] 씬 로드 완료");
		LoadingScreen.Instance?.Hide();
		string text = SceneManager.GetActiveScene().name;
		Debug.Log("[Fusion] 현재 씬: " + text);
		if (text == "TrashZoneScene" && runner.IsServer)
		{
			Debug.Log("[Fusion] TrashZoneScene 진입 — 플레이어 스폰 + PCG 시작");
			Debug.Log($"[Fusion] 접속자 수: {runner.ActivePlayers.Count()}");
			foreach (PlayerRef activePlayer in runner.ActivePlayers)
			{
				SpawnPlayer(runner, activePlayer);
			}
			StartCoroutine(StartPCGAfterDelay());
		}
		else
		{
			if (!(text == "DecoScene") || !runner.IsServer)
			{
				return;
			}
			Debug.Log("[Fusion] DecoScene 진입 — 플레이어 스폰 (숨김 처리)");
			foreach (PlayerRef activePlayer2 in runner.ActivePlayers)
			{
				SpawnPlayer(runner, activePlayer2);
			}
		}
	}

	private IEnumerator StartPCGAfterDelay()
	{
		yield return new WaitForSeconds(0.5f);
		PCGManager pCGManager = UnityEngine.Object.FindFirstObjectByType<PCGManager>();
		if (pCGManager != null)
		{
			Debug.Log("[Fusion] PCGManager.StartMapGeneration() 호출");
			pCGManager.StartMapGeneration();
		}
		else
		{
			Debug.LogError("[Fusion] PCGManager를 찾을 수 없음!");
		}
		if (_savedBotCount < 0)
		{
			_savedBotCount = PlayerPrefs.GetInt("BotCount", 0);
			for (int i = 0; i < _savedBotCount && i < _savedBotChars.Length; i++)
			{
				_savedBotChars[i] = PlayerPrefs.GetInt($"BotCharacter_{i}", 1);
			}
		}
		int savedBotCount = _savedBotCount;
		Debug.Log($"[Fusion] 봇 스폰: {savedBotCount}개");
		GameObject[] array = GameObject.FindGameObjectsWithTag("SpawnPoint");
		for (int j = 0; j < savedBotCount; j++)
		{
			string text = ((j >= _savedBotChars.Length) ? 1 : _savedBotChars[j]) switch
			{
				1 => "PlayerAlpha", 
				2 => "PlayerBeta", 
				3 => "PlayerGamma", 
				4 => "PlayerDelta", 
				_ => "PlayerAlpha", 
			};
			NetworkObject networkObject = Resources.Load<NetworkObject>(text);
			if (networkObject == null)
			{
				Debug.LogError("[Fusion] 봇 프리팹 로드 실패: " + text);
				continue;
			}
			Vector3 vector = ((array.Length > j + 1) ? array[j + 1].transform.position : new Vector3(-15f + (float)(j + 1) * 5f, -8f, -1.2f));
			vector.z = -1.2f;
			NetworkObject networkObject2 = _runner?.Spawn(networkObject, vector, Quaternion.identity);
			if (networkObject2 != null)
			{
				Debug.Log($"[Fusion] 봇 {j + 1} 스폰 완료: {text} at {vector}");
				if (networkObject2.GetComponent<AIBotController>() == null)
				{
					networkObject2.gameObject.AddComponent<AIBotController>();
				}
				IgnoreCollisionWithOtherPlayers(networkObject2.gameObject);
			}
		}
	}

	private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
	{
		Debug.Log($"[Fusion] 플레이어 스폰 시도: {player.PlayerId}");
		int num = Mathf.Clamp(player.PlayerId - 1, 0, 3);
		int playerCharacter = GetPlayerCharacter(num);
		Debug.Log($"[Fusion] PlayerId:{player.PlayerId} → 슬롯:{num} → 캐릭터:{playerCharacter}");
		string text = playerCharacter switch
		{
			1 => "PlayerAlpha", 
			2 => "PlayerBeta", 
			3 => "PlayerGamma", 
			4 => "PlayerDelta", 
			_ => "PlayerAlpha", 
		};
		Debug.Log("[Fusion] 로드할 프리팹: " + text);
		NetworkObject networkObject = Resources.Load<NetworkObject>(text);
		if (networkObject == null)
		{
			Debug.LogError("[Fusion] ❌ " + text + " 프리팹 로드 실패!");
			return;
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("SpawnPoint");
		int num2 = Mathf.Clamp(player.PlayerId, 0, array.Length - 1);
		Vector3 value = ((array.Length != 0) ? array[num2].transform.position : new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), -0.5f));
		value.z = -1.2f;
		NetworkObject networkObject2 = runner.Spawn(networkObject, value, Quaternion.identity, player);
		if (networkObject2 != null)
		{
			Debug.Log($"[Fusion] ✅ {text} 스폰 완료 (PlayerId:{player.PlayerId})");
			IgnoreCollisionWithOtherPlayers(networkObject2.gameObject);
		}
		else
		{
			Debug.LogError($"[Fusion] ❌ 플레이어 스폰 실패: {player.PlayerId}");
		}
	}

	public PlayerRef GetLocalPlayer()
	{
		if (_runner == null)
		{
			Debug.LogWarning("[Fusion] GetLocalPlayer - Runner가 null!");
			return PlayerRef.None;
		}
		return _runner.LocalPlayer;
	}

	private void IgnoreCollisionWithOtherPlayers(GameObject newPlayer)
	{
		Collider2D component = newPlayer.GetComponent<Collider2D>();
		if (component == null)
		{
			return;
		}
		PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		foreach (PlayerMovement playerMovement in array)
		{
			if (!(playerMovement.gameObject == newPlayer))
			{
				Collider2D component2 = playerMovement.GetComponent<Collider2D>();
				if (!(component2 == null))
				{
					Physics2D.IgnoreCollision(component, component2, ignore: true);
				}
			}
		}
	}

	public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
	{
	}

	public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
	{
	}

	public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
	{
	}

	public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
	{
	}

	public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
	{
	}

	public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
	{
	}

	public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
	{
	}

	public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
	{
	}

	public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
	{
	}
}
