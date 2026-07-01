using System.Collections;
using System.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DevBootstrap : MonoBehaviour
{
	[Header("⚠\ufe0f 개발 전용 — 배포 전 비활성화")]
	[Tooltip("스폰할 플레이어 프리팹 이름 (Resources 폴더 안에 있어야 함)")]
	public string playerPrefabName = "PlayerAlpha";

	[Tooltip("테스트 세션 이름 (아무 문자열이나 OK)")]
	public string testSessionName = "DEV_TEST";

	private NetworkRunner _runner;

	private IEnumerator Start()
	{
		if (PhotonManager.Instance != null && PhotonManager.Instance.GetComponent<NetworkRunner>() != null)
		{
			Debug.Log("[DevBootstrap] PhotonManager Runner 이미 실행 중. 스킵.");
			Object.Destroy(base.gameObject);
			yield break;
		}
		Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		Debug.Log("[DevBootstrap] 테스트 모드 시작!");
		Debug.Log("  - 프리팹: " + playerPrefabName);
		Debug.Log("  - 세션: " + testSessionName);
		Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		yield return null;
		StartCoroutine(StartFusionHost());
	}

	private IEnumerator StartFusionHost()
	{
		_runner = base.gameObject.AddComponent<NetworkRunner>();
		_runner.ProvideInput = true;
		if (GetComponent<RunnerSimulatePhysics2D>() == null)
		{
			base.gameObject.AddComponent<RunnerSimulatePhysics2D>();
		}
		NetworkSceneManagerDefault sceneManager = base.gameObject.AddComponent<NetworkSceneManagerDefault>();
		DevBootstrapCallbacks devBootstrapCallbacks = base.gameObject.AddComponent<DevBootstrapCallbacks>();
		devBootstrapCallbacks.Init(playerPrefabName);
		_runner.AddCallbacks(devBootstrapCallbacks);
		StartGameArgs args = new StartGameArgs
		{
			GameMode = GameMode.Host,
			SessionName = testSessionName,
			Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
			SceneManager = sceneManager,
			PlayerCount = 4
		};
		Debug.Log("[DevBootstrap] Runner.StartGame 호출 중...");
		Task<StartGameResult> task = _runner.StartGame(args);
		while (!task.IsCompleted)
		{
			yield return null;
		}
		if (task.Result.Ok)
		{
			Debug.Log("[DevBootstrap] ✅ Photon Host 연결 성공! 플레이어 스폰 대기 중...");
		}
		else
		{
			Debug.LogError($"[DevBootstrap] ❌ 연결 실패: {task.Result.ShutdownReason}");
		}
	}
}
