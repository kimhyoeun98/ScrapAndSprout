using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ⚠️ 개발/테스트 전용 — 빌드 배포 전 비활성화 또는 삭제할 것
///
/// [역할]
/// TrashZoneScene에서 Play를 눌렀을 때 Photon 세션이 없어서 플레이어가
/// 스폰되지 않는 문제를 해결하는 테스트용 부트스트랩입니다.
///
/// [사용법]
/// 1. TrashZoneScene Hierarchy에 빈 GameObject 생성 → 이름: "DevBootstrap"
/// 2. 이 스크립트를 DevBootstrap에 Add Component
/// 3. Player Prefab Name: "PlayerAlpha" (또는 원하는 캐릭터)
/// 4. Play 버튼 누르면 자동으로 Photon Host 세션 시작 + 플레이어 스폰
/// </summary>
public class DevBootstrap : MonoBehaviour
{
    [Header("⚠️ 개발 전용 — 배포 전 비활성화")]
    [Tooltip("스폰할 플레이어 프리팹 이름 (Resources 폴더 안에 있어야 함)")]
    public string playerPrefabName = "PlayerAlpha";

    [Tooltip("테스트 세션 이름 (아무 문자열이나 OK)")]
    public string testSessionName = "DEV_TEST";

    private NetworkRunner _runner;

    IEnumerator Start()
    {
        // PhotonManager가 이미 Runner를 실행 중이면 스킵
        if (PhotonManager.Instance != null
            && PhotonManager.Instance.GetComponent<NetworkRunner>() != null)
        {
            Debug.Log("[DevBootstrap] PhotonManager Runner 이미 실행 중. 스킵.");
            Destroy(gameObject);
            yield break;
        }

        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("[DevBootstrap] 테스트 모드 시작!");
        Debug.Log($"  - 프리팹: {playerPrefabName}");
        Debug.Log($"  - 세션: {testSessionName}");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        yield return null;

        StartCoroutine(StartFusionHost());
    }

    IEnumerator StartFusionHost()
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        if (GetComponent<RunnerSimulatePhysics2D>() == null)
            gameObject.AddComponent<RunnerSimulatePhysics2D>();

        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var callbacks = gameObject.AddComponent<DevBootstrapCallbacks>();
        callbacks.Init(playerPrefabName);
        _runner.AddCallbacks(callbacks);

        var args = new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = testSessionName,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = sceneManager,
            PlayerCount = 4,
        };

        Debug.Log("[DevBootstrap] Runner.StartGame 호출 중...");

        var task = _runner.StartGame(args);
        while (!task.IsCompleted)
            yield return null;

        if (task.Result.Ok)
            Debug.Log("[DevBootstrap] ✅ Photon Host 연결 성공! 플레이어 스폰 대기 중...");
        else
            Debug.LogError($"[DevBootstrap] ❌ 연결 실패: {task.Result.ShutdownReason}");
    }
}

// ─────────────────────────────────────────────────────────────────
//  DevBootstrapCallbacks
// ─────────────────────────────────────────────────────────────────

public class DevBootstrapCallbacks : MonoBehaviour, INetworkRunnerCallbacks
{
    private string _prefabName;

    public void Init(string prefabName)
    {
        _prefabName = prefabName;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[DevBootstrap] 플레이어 입장: {player.PlayerId}");
        if (!runner.IsServer) return;
        SpawnPlayer(runner, player);
    }

    void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject prefab = Resources.Load<NetworkObject>(_prefabName);

        if (prefab == null)
        {
            Debug.LogError($"[DevBootstrap] ❌ 프리팹 '{_prefabName}'을 Resources에서 찾을 수 없음!");
            return;
        }

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        int index = Mathf.Clamp(player.PlayerId, 0, Mathf.Max(0, spawnPoints.Length - 1));

        Vector3 spawnPos;
        if (spawnPoints.Length > 0)
            spawnPos = spawnPoints[index].transform.position;
        else
        {
            spawnPos = new Vector3(-15f, -3.5f, -1f);
            Debug.LogWarning("[DevBootstrap] SpawnPoint 없음 → 기본 위치 스폰");
        }

        spawnPos.z = -1f;

        NetworkObject spawned = runner.Spawn(prefab, spawnPos, Quaternion.identity, player);

        if (spawned != null)
            Debug.Log($"[DevBootstrap] ✅ {_prefabName} 스폰 완료! 위치: {spawnPos}");
        else
            Debug.LogError($"[DevBootstrap] ❌ 스폰 실패: {player.PlayerId}");
    }

    // ── 입력 처리 ──
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // 좌우 이동 (Y는 중력이 담당)
        data.direction = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            0f
        );

        // ↑키: 점프
        data.jump = Input.GetKeyDown(KeyCode.UpArrow);

        // E키: 상호작용 (쓰레기 채굴, NPC)
        data.interact = Input.GetKeyDown(KeyCode.E);

        // ↓키: 텔레포트
        data.teleport = Input.GetKeyDown(KeyCode.DownArrow);

        input.Set(data);
    }

    // ── 나머지 필수 콜백 ──
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        => Debug.Log($"[DevBootstrap] 플레이어 퇴장: {player.PlayerId}");

    public void OnConnectedToServer(NetworkRunner runner)
        => Debug.Log("[DevBootstrap] 서버 연결 성공");

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        => Debug.LogWarning($"[DevBootstrap] 연결 끊김: {reason}");

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        => Debug.LogError($"[DevBootstrap] 연결 실패: {reason}");

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        => Debug.Log($"[DevBootstrap] Shutdown: {shutdownReason}");

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[DevBootstrap] 씬 로드 완료");

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (currentScene == "TrashZoneScene" && runner.IsServer)
        {
            // PCG 맵 생성 (씬 로드 완료 후)
            var pcg = GameObject.FindFirstObjectByType<PCGManager>();
            if (pcg != null)
            {
                Debug.Log("[DevBootstrap] PCGManager.StartMapGeneration() 호출");
                pcg.StartMapGeneration();
            }
            else
            {
                Debug.LogError("[DevBootstrap] PCGManager 없음!");
            }
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner) { }
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