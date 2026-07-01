using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DevBootstrapCallbacks : MonoBehaviour, INetworkRunnerCallbacks, IPublicFacingInterface
{
	private string _prefabName;

	public void Init(string prefabName)
	{
		_prefabName = prefabName;
	}

	public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
	{
		Debug.Log($"[DevBootstrap] 플레이어 입장: {player.PlayerId}");
		if (runner.IsServer)
		{
			SpawnPlayer(runner, player);
		}
	}

	private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
	{
		NetworkObject networkObject = Resources.Load<NetworkObject>(_prefabName);
		if (networkObject == null)
		{
			Debug.LogError("[DevBootstrap] ❌ 프리팹 '" + _prefabName + "'을 Resources에서 찾을 수 없음!");
			return;
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("SpawnPoint");
		int num = Mathf.Clamp(player.PlayerId, 0, Mathf.Max(0, array.Length - 1));
		Vector3 vector;
		if (array.Length != 0)
		{
			vector = array[num].transform.position;
		}
		else
		{
			vector = new Vector3(-15f, -3.5f, -1f);
			Debug.LogWarning("[DevBootstrap] SpawnPoint 없음 → 기본 위치 스폰");
		}
		vector.z = -1f;
		if (runner.Spawn(networkObject, vector, Quaternion.identity, player) != null)
		{
			Debug.Log($"[DevBootstrap] ✅ {_prefabName} 스폰 완료! 위치: {vector}");
		}
		else
		{
			Debug.LogError($"[DevBootstrap] ❌ 스폰 실패: {player.PlayerId}");
		}
	}

	public void OnInput(NetworkRunner runner, NetworkInput input)
	{
		input.Set(new NetworkInputData
		{
			direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
			interact = Input.GetKeyDown(KeyCode.E),
			teleport = Input.GetKeyDown(KeyCode.T)
		});
	}

	public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
	{
		Debug.Log($"[DevBootstrap] 플레이어 퇴장: {player.PlayerId}");
	}

	public void OnConnectedToServer(NetworkRunner runner)
	{
		Debug.Log("[DevBootstrap] 서버 연결 성공");
	}

	public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
	{
		Debug.LogWarning($"[DevBootstrap] 연결 끊김: {reason}");
	}

	public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
	{
		Debug.LogError($"[DevBootstrap] 연결 실패: {reason}");
	}

	public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
	{
		Debug.Log($"[DevBootstrap] Shutdown: {shutdownReason}");
	}

	public void OnSceneLoadDone(NetworkRunner runner)
	{
		Debug.Log("[DevBootstrap] 씬 로드 완료");
		if (SceneManager.GetActiveScene().name == "TrashZoneScene" && runner.IsServer)
		{
			PCGManager pCGManager = UnityEngine.Object.FindFirstObjectByType<PCGManager>();
			if (pCGManager != null)
			{
				Debug.Log("[DevBootstrap] PCGManager.StartMapGeneration() 호출");
				pCGManager.StartMapGeneration();
			}
			else
			{
				Debug.LogError("[DevBootstrap] PCGManager 없음!");
			}
		}
	}

	public void OnSceneLoadStart(NetworkRunner runner)
	{
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

	public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
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
