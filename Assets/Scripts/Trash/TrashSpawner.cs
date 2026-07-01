using Fusion;
using UnityEngine;

public class TrashSpawner : NetworkBehaviour
{
	[Header("── 스폰할 쓰레기 프리팹 ──")]
	public NetworkObject[] trashPrefabs;

	[Header("── 스폰 위치 ──")]
	public Transform[] spawnPoints;

	[Header("── 스폰 설정 ──")]
	public float spawnInterval = 30f;

	public int maxTrashCount = 10;

	public float initialDelay = 5f;

	private float _spawnTimer;

	public override void Spawned()
	{
		if (base.HasStateAuthority)
		{
			_spawnTimer = 0f - initialDelay;
			Debug.Log("[스포너] Host 초기화 완료");
		}
	}

	public override void FixedUpdateNetwork()
	{
		if (base.HasStateAuthority)
		{
			_spawnTimer += base.Runner.DeltaTime;
			if (_spawnTimer >= spawnInterval)
			{
				_spawnTimer = 0f;
				TrySpawnTrash();
			}
		}
	}

	private void TrySpawnTrash()
	{
		if (trashPrefabs != null && trashPrefabs.Length != 0 && spawnPoints != null && spawnPoints.Length != 0)
		{
			int num = GameObject.FindGameObjectsWithTag("Trash").Length;
			if (num >= maxTrashCount)
			{
				Debug.Log($"[스포너] 최대 수 도달 ({num}/{maxTrashCount})");
				return;
			}
			int num2 = Random.Range(0, trashPrefabs.Length);
			int num3 = Random.Range(0, spawnPoints.Length);
			base.Runner.Spawn(trashPrefabs[num2], spawnPoints[num3].position, Quaternion.identity);
			Debug.Log("[스포너] 쓰레기 네트워크 스폰!");
		}
	}

	public void SetSpawnInterval(float newInterval)
	{
		spawnInterval = newInterval;
	}

}
