using UnityEngine;

/// <summary>
/// 쓰레기 랜덤 스폰 시스템
///
/// [동작 방식]
/// - 일정 시간마다 스폰 포인트 중 랜덤 위치에 쓰레기를 생성합니다.
/// - 최대 쓰레기 수를 초과하면 스폰하지 않습니다.
/// - pollution_level 연동은 추후 서버에서 regen_rate를 받아 확장 예정
///
/// [부착 위치] 씬에 빈 오브젝트 "TrashSpawner"를 만들고 부착하세요.
/// </summary>
public class TrashSpawner : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────

    [Header("── 스폰할 쓰레기 프리팹 ──")]
    [Tooltip("랜덤으로 스폰될 쓰레기 프리팹 목록 (can_0, banana_0 등)")]
    public GameObject[] trashPrefabs;

    [Header("── 스폰 위치 ──")]
    [Tooltip("쓰레기가 생성될 수 있는 위치들 (빈 오브젝트로 지정)")]
    public Transform[] spawnPoints;

    [Header("── 스폰 설정 ──")]
    [Tooltip("몇 초마다 쓰레기를 생성할지 (기본 30초)")]
    public float spawnInterval = 30f;

    [Tooltip("씬에 존재할 수 있는 최대 쓰레기 수")]
    public int maxTrashCount = 10;

    [Tooltip("게임 시작 후 첫 스폰까지 대기 시간 (초)")]
    public float initialDelay = 5f;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    /// <summary>현재 씬에 존재하는 쓰레기 수 추적</summary>
    private int _currentTrashCount = 0;

    /// <summary>스폰 타이머</summary>
    private float _spawnTimer = 0f;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 필수 설정 확인
        if (trashPrefabs == null || trashPrefabs.Length == 0)
        {
            Debug.LogError("[스포너] trashPrefabs가 비어있습니다! Inspector에서 프리팹을 연결하세요.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[스포너] spawnPoints가 비어있습니다! Inspector에서 스폰 포인트를 연결하세요.");
            return;
        }

        // 씬에 이미 있는 쓰레기 수 초기화
        _currentTrashCount = GameObject.FindGameObjectsWithTag("Trash").Length;

        // 첫 스폰을 initialDelay 이후로 설정
        _spawnTimer = -initialDelay;

        Debug.Log($"[스포너] 초기화 완료 | 현재 쓰레기: {_currentTrashCount}개 | {spawnInterval}초마다 스폰");
    }

    void Update()
    {
        _spawnTimer += Time.deltaTime;

        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;
            TrySpawnTrash();
        }
    }

    // ─────────────────────────────────────────
    //  스폰 로직
    // ─────────────────────────────────────────

    /// <summary>
    /// 쓰레기 스폰을 시도합니다.
    /// 최대 수 초과 시 스폰하지 않습니다.
    /// </summary>
    void TrySpawnTrash()
    {
        // 현재 쓰레기 수 갱신 (수거로 줄었을 수 있음)
        _currentTrashCount = GameObject.FindGameObjectsWithTag("Trash").Length;

        // 최대 수 체크
        if (_currentTrashCount >= maxTrashCount)
        {
            Debug.Log($"[스포너] 최대 쓰레기 수 도달 ({_currentTrashCount}/{maxTrashCount}) — 스폰 건너뜀");
            return;
        }

        // 랜덤 프리팹 선택
        int randomPrefabIndex = Random.Range(0, trashPrefabs.Length);
        GameObject selectedPrefab = trashPrefabs[randomPrefabIndex];

        // 랜덤 스폰 포인트 선택
        int randomPointIndex = Random.Range(0, spawnPoints.Length);
        Transform selectedPoint = spawnPoints[randomPointIndex];

        // 스폰 실행
        GameObject spawnedTrash = Instantiate(
            selectedPrefab,
            selectedPoint.position,
            Quaternion.identity
        );

        _currentTrashCount++;

        Debug.Log($"[스포너] {selectedPrefab.name} 스폰! 위치: {selectedPoint.name} | 현재: {_currentTrashCount}/{maxTrashCount}");
    }

    // ─────────────────────────────────────────
    //  외부에서 호출 가능 (추후 서버 연동용)
    // ─────────────────────────────────────────

    /// <summary>
    /// 스폰 간격을 동적으로 변경합니다.
    /// 추후 서버에서 pollution_level 기반 regen_rate를 받아 호출 예정
    /// </summary>
    public void SetSpawnInterval(float newInterval)
    {
        spawnInterval = newInterval;
        Debug.Log($"[스포너] 스폰 간격 변경: {newInterval}초");
    }
}