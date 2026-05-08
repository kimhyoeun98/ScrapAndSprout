using UnityEngine;
using Fusion;

/// <summary>
/// 쓰레기 랜덤 스폰 시스템 (Photon Fusion 버전)
/// Host만 스폰하고 모든 클라이언트에 자동 동기화
/// </summary>
public class TrashSpawner : NetworkBehaviour
{
    [Header("── 스폰할 쓰레기 프리팹 ──")]
    public NetworkObject[] trashPrefabs; // NetworkObject로 변경!

    [Header("── 스폰 위치 ──")]
    public Transform[] spawnPoints;

    [Header("── 스폰 설정 ──")]
    public float spawnInterval = 30f;
    public int maxTrashCount = 10;
    public float initialDelay = 5f;

    private float _spawnTimer = 0f;

    public override void Spawned()
    {
        // Host만 타이머 초기화
        if (!HasStateAuthority) return;
        _spawnTimer = -initialDelay;
        Debug.Log("[스포너] Host 초기화 완료");
    }

    public override void FixedUpdateNetwork()
    {
        // Host만 스폰 처리
        if (!HasStateAuthority) return;

        _spawnTimer += Runner.DeltaTime;

        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;
            TrySpawnTrash();
        }
    }

    void TrySpawnTrash()
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int currentCount = GameObject.FindGameObjectsWithTag("Trash").Length;

        if (currentCount >= maxTrashCount)
        {
            Debug.Log($"[스포너] 최대 수 도달 ({currentCount}/{maxTrashCount})");
            return;
        }

        int randomPrefabIndex = Random.Range(0, trashPrefabs.Length);
        int randomPointIndex = Random.Range(0, spawnPoints.Length);

        // Host가 스폰 → 모든 클라이언트에 자동 동기화!
        Runner.Spawn(
            trashPrefabs[randomPrefabIndex],
            spawnPoints[randomPointIndex].position,
            Quaternion.identity
        );

        Debug.Log($"[스포너] 쓰레기 네트워크 스폰!");
    }

    public void SetSpawnInterval(float newInterval)
    {
        spawnInterval = newInterval;
    }
}
