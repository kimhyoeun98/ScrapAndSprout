// ═══════════════════════════════════════════════════════════════
//  PhotonManager.cs
//  
//  역할: Photon Fusion 2 NetworkRunner 생명주기 전담 관리
//  
//  설계 원칙:
//  - LoginScene에서만 생성, DontDestroyOnLoad로 모든 씬에서 유지
//  - async/await 방식으로 안전한 비동기 처리
//  - NetworkRunner 중복 생성 방지 및 정리
//  - 슬롯 배정은 WaitingRoomManager가 전담
// ═══════════════════════════════════════════════════════════════
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static PhotonManager Instance { get; private set; }

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Awake()
    {
        // 싱글톤 패턴 + DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PhotonManager] 중복 인스턴스 감지 - 파괴: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("═══════════════════════════════════════════");
        Debug.Log("[PhotonManager] 초기화 완료");
        Debug.Log("  - DontDestroyOnLoad 설정됨");
        Debug.Log("  - 모든 씬에서 사용 가능");
        Debug.Log("═══════════════════════════════════════════");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[PhotonManager] 인스턴스 파괴됨");
        }
    }

    // ─────────────────────────────────────────
    //  Public API - LobbyUI에서 호출
    // ─────────────────────────────────────────

    /// <summary>
    /// Host 모드로 방 개설
    /// </summary>
    public async Task StartHostWithRoom(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            roomCode = UnityEngine.Random.Range(1000, 9999).ToString();
        }

        Debug.Log("───────────────────────────────────────────");
        Debug.Log($"[PhotonManager] Host 모드 시작 요청");
        Debug.Log($"  - 방 코드: {roomCode}");

        // ✅ PlayerPrefs 저장 (반드시 Save 호출!)
        PlayerPrefs.SetString("RoomCode", roomCode);
        PlayerPrefs.SetString("RoomMode", "Create");
        PlayerPrefs.Save();

        Debug.Log($"[PhotonManager] ✅ PlayerPrefs 저장 완료");
        Debug.Log($"  - RoomCode: {PlayerPrefs.GetString("RoomCode")}");
        Debug.Log($"  - RoomMode: {PlayerPrefs.GetString("RoomMode")}");

        await StartMultiplayerSession(roomCode, true);
    }

    /// <summary>
    /// Client 모드로 방 참여
    /// </summary>
    public async Task StartClientWithRoom(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogError("[PhotonManager] Client 참여 실패 - 방 코드가 비어있음!");
            return;
        }

        Debug.Log("───────────────────────────────────────────");
        Debug.Log($"[PhotonManager] Client 모드 시작 요청");
        Debug.Log($"  - 방 코드: {roomCode}");

        // ✅ PlayerPrefs 저장 (반드시 Save 호출!)
        PlayerPrefs.SetString("RoomCode", roomCode);
        PlayerPrefs.SetString("RoomMode", "Join");
        PlayerPrefs.Save();  // ✅ 필수!

        Debug.Log($"[PhotonManager] ✅ PlayerPrefs 저장 완료");
        Debug.Log($"  - RoomCode: {PlayerPrefs.GetString("RoomCode")}");
        Debug.Log($"  - RoomMode: {PlayerPrefs.GetString("RoomMode")}");

        await StartMultiplayerSession(roomCode, false);
    }

    // ─────────────────────────────────────────
    //  Core - Fusion 세션 시작 (async/await)
    // ─────────────────────────────────────────

    /// <summary>
    /// Photon Fusion 멀티플레이 세션 시작
    /// </summary>
    private async Task<bool> StartMultiplayerSession(string sessionName, bool isHost)
    {
        try
        {
            Debug.Log("═══════════════════════════════════════════");
            Debug.Log("[Fusion] 세션 시작 준비");
            Debug.Log($"  - Session: {sessionName}");
            Debug.Log($"  - Mode: {(isHost ? "Host" : "Client")}");

            // ── 1. 백그라운드 실행 허용 ────────────────
            Application.runInBackground = true;

            // ── 2. 기존 Runner 정리 ──────────────────
            await CleanupRunner();

            // ── 3. NetworkRunner 생성 ──────────────────
            _runner = gameObject.GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                Debug.Log("[Fusion] NetworkRunner 컴포넌트 생성");
            }

            if (_runner == null)
            {
                Debug.LogError("[Fusion] ❌ NetworkRunner 생성 실패!");
                return false;
            }

            _runner.ProvideInput = true;

            // ── 4. Physics 컴포넌트 추가 ──────────────
            var physics = gameObject.GetComponent<RunnerSimulatePhysics2D>();
            if (physics == null)
            {
                gameObject.AddComponent<RunnerSimulatePhysics2D>();
                Debug.Log("[Fusion] RunnerSimulatePhysics2D 추가");
            }

            // ── 5. NetworkSceneManager 생성 ──────────
            _sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
            if (_sceneManager == null)
            {
                _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
                Debug.Log("[Fusion] NetworkSceneManagerDefault 생성");
            }

            if (_sceneManager == null)
            {
                Debug.LogError("[Fusion] ❌ NetworkSceneManagerDefault 생성 실패!");
                return false;
            }

            // ── 6. 현재 씬 정보 가져오기 ──────────────
            Scene currentScene = SceneManager.GetActiveScene();
            int sceneIndex = currentScene.buildIndex;

            Debug.Log($"[Fusion] 현재 씬: {currentScene.name} (Index: {sceneIndex})");

            // ── 7. StartGameArgs 구성 ─────────────────
            GameMode mode = isHost ? GameMode.Host : GameMode.Client;

            var args = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(sceneIndex),
                SceneManager = _sceneManager,
                PlayerCount = 4,
            };

            //Debug.Log("[Fusion] StartGameArgs 구성 완료");
            //Debug.Log($"  - GameMode: {mode}");
            //Debug.Log($"  - SessionName: {sessionName}");
            //Debug.Log($"  - Scene: {sceneIndex}");
            //Debug.Log($"  - PlayerCount: 4");

            //// ── 8. StartGame 비동기 호출 ────────────────
            //Debug.Log("[Fusion] Runner.StartGame 호출 시작...");
            //Debug.LogError("━━━━━━━━━━━━━━━━━━━━━━━━━");
            //Debug.LogError("[로비 입장1");
            //Debug.LogError("━━━━━━━━━━━━━━━━━━━━━━━━━");
            //_runner.AddCallbacks(this);
            //Debug.Log("[Fusion] 콜백 등록 완료");

            var result = await _runner.StartGame(args);

            // ── 9. 결과 처리 ───────────────────────────
            if (result.Ok)
            {
                Debug.Log("═══════════════════════════════════════════");
                Debug.Log("[Fusion] ✅ 접속 성공!");
                Debug.Log($"  - Session: {sessionName}");
                Debug.Log($"  - Mode: {mode}");
                Debug.Log("═══════════════════════════════════════════");

                // ── 10. 씬 전환 (waitingRoomScene) ──────
                await LoadNetworkScene("waitingRoomScene");

                return true;
            }
            else
            {
                Debug.LogError("═══════════════════════════════════════════");
                Debug.LogError("[Fusion] ❌ 접속 실패!");
                Debug.LogError($"  - ShutdownReason: {result.ShutdownReason}");
                Debug.LogError($"  - ErrorMessage: {result.ErrorMessage}");
                Debug.LogError("═══════════════════════════════════════════");

                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("═══════════════════════════════════════════");
            Debug.LogError($"[Fusion] ❌ 예외 발생: {e.GetType().Name}");
            Debug.LogError($"  - Message: {e.Message}");
            Debug.LogError($"  - StackTrace:\n{e.StackTrace}");
            Debug.LogError("═══════════════════════════════════════════");

            return false;
        }
    }

    // ─────────────────────────────────────────
    //  Helper - Runner 생명주기 관리
    // ─────────────────────────────────────────

    /// <summary>
    /// 기존 NetworkRunner 안전하게 정리
    /// </summary>
    private async Task CleanupRunner()
    {
        if (_runner != null)
        {
            Debug.Log("[Fusion] 기존 Runner Shutdown 시작");

            try
            {
                await _runner.Shutdown();
                Debug.Log("[Fusion] Runner Shutdown 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Fusion] Shutdown 예외 (무시): {e.Message}");
            }

            if (_runner != null)
            {
                Destroy(_runner);
                _runner = null;
            }

            Debug.Log("[Fusion] 기존 Runner 제거 완료");
        }
    }

    /// <summary>
    /// NetworkSceneManager를 통한 안전한 씬 전환
    /// </summary>
    private async Task LoadNetworkScene(string sceneName)
    {
        if (_runner == null || _sceneManager == null)
        {
            Debug.LogError("[Fusion] Runner 또는 SceneManager가 null - 일반 씬 전환");
            SceneManager.LoadScene(sceneName);
            return;
        }

        Debug.Log($"[Fusion] 네트워크 씬 전환 시작: {sceneName}");

        // Build Settings에서 씬 인덱스 찾기
        int sceneIndex = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                sceneIndex = i;
                break;
            }
        }

        if (sceneIndex == -1)
        {
            Debug.LogError($"[Fusion] 씬을 찾을 수 없음: {sceneName}");
            return;
        }

        // Host 또는 Server만 씬 전환 명령 가능
        if (_runner.IsServer)
        {
            _runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            Debug.Log($"[Fusion] 씬 로드 명령 전송: {sceneName} (Index: {sceneIndex})");
        }
        else
        {
            Debug.Log($"[Fusion] Client는 Host의 씬 전환 대기 중...");
        }

        await Task.Yield();
    }

    // ─────────────────────────────────────────
    //  Public - 방 나가기
    // ─────────────────────────────────────────

    /// <summary>
    /// 방 나가기 - WaitingRoomManager에서 호출
    /// </summary>
    // public async void LeaveRoom()
    // 변경: public async Task LeaveRoom()

    public async Task LeaveRoom()  // ← void를 Task로
    {
        Debug.Log("[PhotonManager] 방 나가기 시작");

        if (_runner != null)
        {
            await CleanupRunner();
            Debug.Log("[PhotonManager] Runner 정리 완료");
        }

        SceneManager.LoadScene("LobbyScene");
        Debug.Log("[PhotonManager] LobbyScene으로 이동");
    }

    // ─────────────────────────────────────────
    //  Public - 씬 전환 (WaitingRoomManager 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// TrashZoneScene으로 전환
    /// </summary>
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

        // 추가된 코드
        int sceneIndex = GetSceneIndex("TrashZoneScene");
        if (sceneIndex == -1)
        {
            Debug.LogError("[Fusion] TrashZoneScene을 Build Settings에서 찾을 수 없음!");
            return;
        }

        _runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        Debug.Log($"[Fusion] TrashZoneScene 로드 명령 전송 (Index: {sceneIndex})");
    }

    // ─────────────────────────────────────────
    //  INetworkRunnerCallbacks
    // ─────────────────────────────────────────

    //public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    //{
    //    string currentScene = SceneManager.GetActiveScene().name;
    //    Debug.Log($"[Fusion] 플레이어 입장: {player.PlayerId} | 씬: {currentScene}");

    //    if (currentScene == "TrashZoneScene" && runner.IsServer)
    //    {
    //        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
    //        int index = Mathf.Clamp(runner.SessionInfo.PlayerCount - 1, 0, spawnPoints.Length - 1);

    //        Vector3 spawnPos = spawnPoints.Length > 0
    //            ? spawnPoints[index].transform.position
    //            : new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), 0f);

    //        runner.Spawn(
    //            Resources.Load<NetworkObject>("Player"),
    //            spawnPos,
    //            Quaternion.identity,
    //            player
    //        );

    //        Debug.Log($"[Fusion] 플레이어 스폰 완료: {player.PlayerId}");
    //    }
    //}
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[Fusion] 플레이어 입장: {player.PlayerId} | 씬: {currentScene}");

        // TrashZoneScene에서는 OnSceneLoadDone에서 일괄 스폰
        // OnPlayerJoined에서는 스폰 안 함 (중복 방지)
    }
    /// <summary>
    /// 추가
    /// 씬 이름으로 Build Index 찾기
    /// </summary>
    private int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
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
        var data = new NetworkInputData();
        data.direction = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
        data.jump = Input.GetKeyDown(KeyCode.UpArrow);
        data.interact = Input.GetKeyDown(KeyCode.E);
        data.teleport = Input.GetKeyDown(KeyCode.DownArrow);
        input.Set(data);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[Fusion] ✅ 서버 연결 성공");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[Fusion] ❌ 서버 연결 끊김: {reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[Fusion] ❌ 연결 실패: {reason}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Fusion] Shutdown: {shutdownReason}");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("[Fusion] 씬 로드 시작");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[Fusion] 씬 로드 완료");

        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[Fusion] 현재 씬: {currentScene}");

        if (currentScene == "TrashZoneScene" && runner.IsServer)
        {
            Debug.Log($"[Fusion] TrashZoneScene 진입 — 플레이어 스폰 + PCG 시작");
            Debug.Log($"[Fusion] 접속자 수: {runner.ActivePlayers.Count()}");

            // 플레이어 스폰
            foreach (var player in runner.ActivePlayers)
                SpawnPlayer(runner, player);

            // PCG + 봇은 딜레이 후 실행 (씬 오브젝트 초기화 대기)
            StartCoroutine(StartPCGAfterDelay());
        }
    }
    System.Collections.IEnumerator StartPCGAfterDelay()
    {
        yield return new UnityEngine.WaitForSeconds(0.5f);

        // 1. PCG 맵 생성
        var pcg = UnityEngine.Object.FindFirstObjectByType<PCGManager>();
        if (pcg != null)
        {
            Debug.Log("[Fusion] PCGManager.StartMapGeneration() 호출");
            pcg.StartMapGeneration();
        }
        else
        {
            Debug.LogError("[Fusion] PCGManager를 찾을 수 없음!");
        }

        // 2. 봇 스폰 (WaitingRoom에서 추가한 봇 수만큼)
        int botCount = PlayerPrefs.GetInt("BotCount", 0);
        Debug.Log($"[Fusion] 봇 스폰: {botCount}개");

        GameObject[] spawnPoints = UnityEngine.GameObject.FindGameObjectsWithTag("SpawnPoint");

        for (int i = 0; i < botCount; i++)
        {
            // 캐릭터 인덱스에 따라 프리팹 결정
            int charIndex = PlayerPrefs.GetInt($"BotCharacter_{i}", 1);
            string botPrefabName = charIndex switch
            {
                1 => "PlayerAlpha",
                2 => "PlayerBeta",
                3 => "PlayerGamma",
                4 => "PlayerDelta",
                _ => "PlayerAlpha"
            };

            NetworkObject botPrefab = Resources.Load<NetworkObject>(botPrefabName);
            if (botPrefab == null)
            {
                Debug.LogError($"[Fusion] 봇 프리팹 로드 실패: {botPrefabName}");
                continue;
            }

            // SpawnPoint i+1 번째 위치에 스폰 (0번은 플레이어)
            Vector3 botPos = spawnPoints.Length > i + 1
                ? spawnPoints[i + 1].transform.position
                : new Vector3(-15f + (i + 1) * 5f, -8f, -1.2f);
            botPos.z = -1.2f;

            var spawnedBot = _runner?.Spawn(botPrefab, botPos, UnityEngine.Quaternion.identity);
            if (spawnedBot != null)
            {
                Debug.Log($"[Fusion] 봇 {i + 1} 스폰 완료: {botPrefabName} at {botPos}");

                // AIBotController 동적 추가 (프리팹에 없어야 함)
                if (spawnedBot.GetComponent<AIBotController>() == null)
                    spawnedBot.gameObject.AddComponent<AIBotController>();
            }
        }

        // PlayerPrefs 초기화
        PlayerPrefs.SetInt("BotCount", 0);
        for (int i = 0; i < botCount; i++)
            PlayerPrefs.DeleteKey($"BotCharacter_{i}");
        PlayerPrefs.Save();
    }

    // ✅ 플레이어 스폰 로직 분리
    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] 플레이어 스폰 시도: {player.PlayerId}");

        // ✅ 선택한 캐릭터에 따라 프리팹 결정
        int characterIndex = PlayerPrefs.GetInt("SelectedCharacter", 1);

        string prefabName;
        switch (characterIndex)
        {
            case 1: prefabName = "PlayerAlpha"; break;
            case 2: prefabName = "PlayerBeta"; break;
            case 3: prefabName = "PlayerGamma"; break;
            case 4: prefabName = "PlayerDelta"; break;
            default:
                prefabName = "PlayerAlpha";
                Debug.LogWarning($"[Fusion] 알 수 없는 캐릭터 인덱스: {characterIndex}");
                break;
        }

        Debug.Log($"[Fusion] 로드할 프리팹: {prefabName}");

        // ✅ Resources에서 프리팹 로드
        NetworkObject playerPrefab = Resources.Load<NetworkObject>(prefabName);

        if (playerPrefab == null)
        {
            Debug.LogError($"[Fusion] ❌ {prefabName} 프리팹 로드 실패!");
            return;
        }

        // SpawnPoint 찾기 (기존 로직 유지)
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        int index = Mathf.Clamp(player.PlayerId, 0, spawnPoints.Length - 1);

        Vector3 spawnPos = spawnPoints.Length > 0
            ? spawnPoints[index].transform.position
            : new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), -0.5f);

        spawnPos.z = -1.2f;

        // ✅ 스폰!
        NetworkObject spawnedPlayer = runner.Spawn(
            playerPrefab,
            spawnPos,
            Quaternion.identity,
            player
        );

        if (spawnedPlayer != null)
        {
            Debug.Log($"[Fusion] ✅ {prefabName} 스폰 완료 (PlayerId: {player.PlayerId})");
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

    // 빈 구현
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}