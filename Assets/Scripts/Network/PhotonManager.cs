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
    private Dictionary<int, int> _playerCharacterMap = new Dictionary<int, int>();

    // 봇 정보 보존 (DecoScene 왕복 후에도 봇 재스폰용)
    private static int _savedBotCount = -1;
    private static int[] _savedBotChars = new int[8];

    // ✅ 추가
    public string LocalPlayerName { get; private set; } = "";

    public void SetLocalPlayerName(string name)
    {
        LocalPlayerName = name;
        Debug.Log($"[PhotonManager] LocalPlayerName 설정: {name}");
    }

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

        // 로딩 화면 자동 생성
        if (FindFirstObjectByType<LoadingScreen>() == null)
        {
            var lsGO = new GameObject("LoadingScreen");
            lsGO.AddComponent<LoadingScreen>();
        }

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

            // ── 3. NetworkRunner 취득 또는 생성 ──────
            _runner = gameObject.GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                Debug.Log("[Fusion] NetworkRunner 컴포넌트 신규 추가");
            }
            else
            {
                // 이미 컴포넌트가 있으면 재사용 → 중복 AddComponent 방지
                Debug.Log("[Fusion] 기존 NetworkRunner 컴포넌트 재사용");
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

            // ── 8. StartGame 호출 (타임아웃 30초) ──────
            var startGameTask = _runner.StartGame(args);
            var timeoutTask = Task.Delay(30000);

            var completedTask = await Task.WhenAny(startGameTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Debug.LogError("═══════════════════════════════════════════");
                Debug.LogError("[Fusion] ❌ 30초 타임아웃 - Photon 연결 실패!");
                Debug.LogError("═══════════════════════════════════════════");

                await CleanupRunner();
                SceneManager.LoadScene("LobbyScene");
                return false;
            }

            var result = await startGameTask;

            // ── 9. 결과 처리 ───────────────────────────
            if (result.Ok)
            {
                Debug.Log("═══════════════════════════════════════════");
                Debug.Log($"[Fusion] ✅ 접속 성공! Session: {sessionName}, Mode: {mode}");
                Debug.Log("═══════════════════════════════════════════");

                await LoadNetworkScene("waitingRoomScene");
                return true;
            }
            else
            {
                Debug.LogError($"[Fusion] ❌ 접속 실패 - {result.ShutdownReason}: {result.ErrorMessage}");

                // ✅ 수정: 실패 시 Runner 정리 → 다음 시도 때 충돌 방지
                await CleanupRunner();
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Fusion] ❌ 예외: {e.GetType().Name} - {e.Message}\n{e.StackTrace}");

            // ✅ 수정: 예외 시에도 Runner 정리
            await CleanupRunner();
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
        if (_runner == null)
        {
            Debug.Log("[Fusion] CleanupRunner - 정리할 Runner 없음 (skip)");
            return;
        }

        Debug.Log($"[Fusion] CleanupRunner 시작 - IsRunning: {_runner.IsRunning}");

        // ✅ 수정: IsRunning 체크 추가
        // 이미 꺼진 Runner에 Shutdown 호출하면 예외 발생하므로 상태 먼저 확인
        if (_runner.IsRunning)
        {
            try
            {
                await _runner.Shutdown();
                Debug.Log("[Fusion] ✅ Runner.Shutdown() 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Fusion] Shutdown 예외 (무시): {e.Message}");
            }
        }
        else
        {
            Debug.Log("[Fusion] Runner가 이미 정지 상태 - Shutdown 생략");
        }

        // ✅ 수정: Destroy(_runner) 제거 → _runner = null 만 남김
        // Destroy()를 쓰면 같은 gameObject에 붙은 컴포넌트가 파괴되어
        // 다음 방 생성 시 AddComponent 충돌 발생 → 방 재생성 불가 원인
        _runner = null;
        Debug.Log("[Fusion] ✅ CleanupRunner 완료 (_runner = null)");
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
    //  Public - 캐릭터 선택 데이터 관리
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어 캐릭터 선택값 저장
    /// WaitingRoomManager에서 캐릭터 선택 시 호출
    /// </summary>
    public void SetPlayerCharacter(int slotIndex, int characterIndex)
    {
        _playerCharacterMap[slotIndex] = characterIndex;
        Debug.Log($"[PhotonManager] 캐릭터 저장 - 슬롯:{slotIndex} → 캐릭터:{characterIndex}");
        Debug.Log($"[PhotonManager] 현재 저장된 캐릭터 맵: {string.Join(", ", _playerCharacterMap)}");
    }

    /// <summary>
    /// 플레이어 캐릭터 선택값 조회
    /// SpawnPlayer()에서 호출
    /// </summary>
    public int GetPlayerCharacter(int slotIndex)
    {
        if (_playerCharacterMap.TryGetValue(slotIndex, out int charIndex))
        {
            Debug.Log($"[PhotonManager] 캐릭터 조회 - 슬롯:{slotIndex} → 캐릭터:{charIndex}");
            return charIndex;
        }

        Debug.LogWarning($"[PhotonManager] 슬롯:{slotIndex} 캐릭터 없음 → 기본값 Alpha(1)");
        return 1;
    }

    /// <summary>
    /// 방 나가거나 게임 종료 시 데이터 초기화
    /// </summary>
    public void ClearPlayerCharacterMap()
    {
        _playerCharacterMap.Clear();
        _savedBotCount = -1;        // 봇 정보 초기화 (다음 게임을 위해)
        GameManager.ResetStaticState();  // 타이머/점수 초기화
        DecoInventoryBridge.ClearDecorations();  // 꾸미기 배치 초기화
        Debug.Log("[PhotonManager] 캐릭터 맵 초기화 완료");
    }

    /// <summary>WaitingRoom 진입 시 봇 카운트 캐시 초기화 (새 게임 준비)</summary>
    public void ResetBotCache()
    {
        _savedBotCount = -1;
        PlayerPrefs.SetInt("BotCount", 0);
        PlayerPrefs.Save();
        Debug.Log("[PhotonManager] 봇 캐시 초기화");
    }
    /// <summary>
    /// 해당 슬롯에 실제 플레이어가 존재하는지 확인
    /// 슬롯에 플레이어가 없으면 GetPlayerCharacter()를 호출하지 않아
    /// 불필요한 경고 로그가 찍히는 것을 방지한다
    /// LeaderboardUI의 Refresh()에서 호출
    /// </summary>
    public bool HasPlayerInSlot(int slotIndex)
    {
        return _playerCharacterMap.ContainsKey(slotIndex);
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
        Debug.Log($"[PhotonManager] 현재 Runner 상태: {(_runner != null ? $"IsRunning={_runner.IsRunning}" : "null")}");

        // ✅ 추가: 방 나갈 때 캐릭터 데이터 초기화
        ClearPlayerCharacterMap();

        await CleanupRunner();
        await Task.Delay(200);

        Debug.Log("[PhotonManager] ✅ LeaveRoom 완료 - LobbyScene으로 이동");
        SceneManager.LoadScene("LobbyScene");
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

    /// <summary>
    /// DecoScene으로 전환 (Host만 호출 가능, 모든 클라이언트 동시 이동)
    /// </summary>
    public void LoadDecoScene()
    {
        // 인벤토리 항상 저장 (Host/Client 모두)
        foreach (var tc in FindObjectsByType<TrashCollector>(FindObjectsSortMode.None))
        {
            if (tc.HasInputAuthority)
            {
                DecoInventoryBridge.SaveFrom(tc);
                break;
            }
        }

        if (_runner == null || !_runner.IsServer)
        {
            // Client는 Fusion 씬 전환 권한 없음 → 로컬 로드 폴백
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

    // ─────────────────────────────────────────
    //  INetworkRunnerCallbacks
    // ─────────────────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[Fusion] 플레이어 입장: {player.PlayerId} | 씬: {currentScene}");

        // TrashZoneScene에서는 OnSceneLoadDone에서 일괄 스폰
        // OnPlayerJoined에서는 스폰 안 함 (중복 방지)
    }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            // ✅ 수정: 빈 구현 → LeaveRoom 호출
            // Host가 나가면 세션이 유지되지 않으므로 남은 클라이언트를 로비로 복귀
            Debug.LogWarning("[Fusion] ⚠️ Host 연결 끊김 감지 - 로비로 복귀");
            _ = LeaveRoom();
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

        // 미니게임 중이면 입력 차단
        if (MiningMinigame.Instance != null && MiningMinigame.Instance.IsPlaying)
        {
            input.Set(data); // 빈 입력
            return;
        }

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

            // ✅ 수정: 로그만 있던 것 → 씬 확인 후 강제 복귀 추가
            // 로비/로그인 씬에서 끊기는 건 정상 흐름이므로 제외
            // 게임 중 끊기면 _runner null 처리 후 LobbyScene으로 강제 이동
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != "LobbyScene" && currentScene != "LoginScene")
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

        // ✅ 수정: Host가 나가면 클라이언트에게 Shutdown 이벤트가 발생함
        // GameMode.Host 모드에서는 OnHostMigration이 아닌 OnShutdown으로 통보됨
        // 로비/로그인이 아닌 씬에서 Shutdown되면 로비로 강제 복귀
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "LobbyScene" && currentScene != "LoginScene")
        {
            // DisconnectedFromHost = 호스트가 나갔을 때 클라에게 오는 이유
            // GameClosed, Ok 등 다양한 이유가 올 수 있으므로 씬 기준으로 판단
            Debug.LogWarning($"[Fusion] 씬 [{currentScene}] 에서 Shutdown({shutdownReason}) 감지 → 로비 복귀");

            _runner = null;
            SceneManager.LoadScene("LobbyScene");
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("[Fusion] 씬 로드 시작");
        LoadingScreen.Instance?.Show(); // ← 추가
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[Fusion] 씬 로드 완료");
        LoadingScreen.Instance?.Hide();

        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[Fusion] 현재 씬: {currentScene}");

        if (currentScene == "TrashZoneScene" && runner.IsServer)
        {
            Debug.Log($"[Fusion] TrashZoneScene 진입 — 플레이어 스폰 + PCG 시작");
            Debug.Log($"[Fusion] 접속자 수: {runner.ActivePlayers.Count()}");

            foreach (var player in runner.ActivePlayers)
                SpawnPlayer(runner, player);

            StartCoroutine(StartPCGAfterDelay());
        }
        else if (currentScene == "DecoScene" && runner.IsServer)
        {
            Debug.Log($"[Fusion] DecoScene 진입 — 플레이어 스폰 (숨김 처리)");
            foreach (var player in runner.ActivePlayers)
                SpawnPlayer(runner, player);
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
        // 최초 진입 시 PlayerPrefs에서 읽어 static에 보존, 이후(DecoScene 왕복)엔 static 사용
        if (_savedBotCount < 0)
        {
            _savedBotCount = PlayerPrefs.GetInt("BotCount", 0);
            for (int i = 0; i < _savedBotCount && i < _savedBotChars.Length; i++)
                _savedBotChars[i] = PlayerPrefs.GetInt($"BotCharacter_{i}", 1);
        }
        int botCount = _savedBotCount;
        Debug.Log($"[Fusion] 봇 스폰: {botCount}개");

        GameObject[] spawnPoints = UnityEngine.GameObject.FindGameObjectsWithTag("SpawnPoint");

        for (int i = 0; i < botCount; i++)
        {
            // 캐릭터 인덱스에 따라 프리팹 결정
            int charIndex = (i < _savedBotChars.Length) ? _savedBotChars[i] : 1;
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

                IgnoreCollisionWithOtherPlayers(spawnedBot.gameObject);
            }
        }

        // PlayerPrefs는 그대로 두고 static(_savedBotCount)으로 봇 정보 유지
        // (DecoScene 왕복 후에도 봇 재스폰 가능)
    }

    // ✅ 플레이어 스폰 로직 분리
    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] 플레이어 스폰 시도: {player.PlayerId}");

        // ✅ 수정: PhotonManager의 Dictionary에서 캐릭터 읽기
        // PlayerId는 1부터 시작, 슬롯은 0부터 시작
        int slotIndex = Mathf.Clamp(player.PlayerId - 1, 0, 3);
        int characterIndex = GetPlayerCharacter(slotIndex);

        Debug.Log($"[Fusion] PlayerId:{player.PlayerId} → 슬롯:{slotIndex} → 캐릭터:{characterIndex}");

        string prefabName = characterIndex switch
        {
            1 => "PlayerAlpha",
            2 => "PlayerBeta",
            3 => "PlayerGamma",
            4 => "PlayerDelta",
            _ => "PlayerAlpha"
        };

        Debug.Log($"[Fusion] 로드할 프리팹: {prefabName}");

        NetworkObject playerPrefab = Resources.Load<NetworkObject>(prefabName);
        if (playerPrefab == null)
        {
            Debug.LogError($"[Fusion] ❌ {prefabName} 프리팹 로드 실패!");
            return;
        }

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        int index = Mathf.Clamp(player.PlayerId, 0, spawnPoints.Length - 1);

        Vector3 spawnPos = spawnPoints.Length > 0
            ? spawnPoints[index].transform.position
            : new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), -0.5f);
        spawnPos.z = -1.2f;

        NetworkObject spawnedPlayer = runner.Spawn(
            playerPrefab,
            spawnPos,
            Quaternion.identity,
            player
        );

        
        if (spawnedPlayer != null)
        {
            Debug.Log($"[Fusion] ✅ {prefabName} 스폰 완료 (PlayerId:{player.PlayerId})");
            IgnoreCollisionWithOtherPlayers(spawnedPlayer.gameObject); 
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
    /// <summary>
    /// 스폰된 플레이어/봇이 다른 플레이어/봇과 충돌하지 않도록 설정
    /// 바닥, 벽 등 환경 오브젝트와의 충돌은 유지
    /// </summary>
    private void IgnoreCollisionWithOtherPlayers(GameObject newPlayer)
    {
        var newCol = newPlayer.GetComponent<Collider2D>();
        if (newCol == null) return;

        // 씬에 있는 모든 PlayerMovement 오브젝트 찾기
        var allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var pm in allPlayers)
        {
            if (pm.gameObject == newPlayer) continue; // 자기 자신 제외
            var otherCol = pm.GetComponent<Collider2D>();
            if (otherCol == null) continue;
            Physics2D.IgnoreCollision(newCol, otherCol, true);
        }
    }

    // 빈 구현
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
   
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}