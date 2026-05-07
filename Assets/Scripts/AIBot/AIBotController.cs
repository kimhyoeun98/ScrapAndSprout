using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI 봇 - FSM 기반 자동 플레이
/// 쓰레기 수거 + 나무 심기를 자동으로 수행합니다
/// </summary>
public class AIBotController : MonoBehaviour
{
    // ==================== FSM 상태 ====================
    public enum BotState
    {
        Idle,           // 대기 (다음 행동 결정)
        FindTrash,      // 쓰레기 찾기
        MoveToTrash,    // 쓰레기로 이동
        CollectTrash,   // 쓰레기 수거
        FindSeedSpot,   // 씨앗 심을 위치 찾기
        MoveToSpot,     // 위치로 이동
        PlantSeed,      // 씨앗 심기
        BuyItem,        // NPC에서 물건 구매
        SellTrash       // NPC에게 쓰레기 판매
    }

    private BotState _currentState = BotState.Idle;

    // ==================== 설정 ====================
    [Header("이동 설정")]
    [Tooltip("AI 봇 이동 속도")]
    public float moveSpeed = 3f;

    [Tooltip("목표 도착 판정 거리")]
    public float arriveDistance = 0.5f;

    [Header("행동 설정")]
    [Tooltip("쓰레기 수거 우선순위 (0~1)")]
    [Range(0f, 1f)]
    public float trashPriority = 0.6f;

    [Tooltip("나무 심기 우선순위 (0~1)")]
    [Range(0f, 1f)]
    public float plantPriority = 0.4f;

    [Tooltip("행동 간 대기 시간 (초)")]
    public float actionDelay = 0.5f;

    // ==================== 컴포넌트 참조 ====================
    private Rigidbody2D _rb;
    private TrashCollector _trashCollector;
    private SeedPlanter _seedPlanter;

    // ==================== 내부 상태 ====================
    private Vector3 _targetPosition;
    private GameObject _targetObject;
    private float _actionTimer = 0f;

    // ==================== Unity 생명주기 ====================

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trashCollector = GetComponent<TrashCollector>();
        _seedPlanter = GetComponent<SeedPlanter>();

        Debug.Log("[AIBot] AI 봇 시작!");
    }

    void Update()
    {
        // 타이머 감소
        if (_actionTimer > 0f)
        {
            _actionTimer -= Time.deltaTime;
            return;
        }

        // FSM 실행
        RunStateMachine();
    }

    void FixedUpdate()
    {
        // 이동 처리 (MoveToTrash, MoveToSpot 상태에서만)
        if (_currentState == BotState.MoveToTrash || _currentState == BotState.MoveToSpot)
        {
            MoveToTarget();
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    // ==================== FSM 실행 ====================

    void RunStateMachine()
    {
        switch (_currentState)
        {
            case BotState.Idle:
                State_Idle();
                break;

            case BotState.FindTrash:
                State_FindTrash();
                break;

            case BotState.MoveToTrash:
                State_MoveToTrash();
                break;

            case BotState.CollectTrash:
                State_CollectTrash();
                break;

            case BotState.FindSeedSpot:
                State_FindSeedSpot();
                break;

            case BotState.MoveToSpot:
                State_MoveToSpot();
                break;

            case BotState.PlantSeed:
                State_PlantSeed();
                break;

            case BotState.BuyItem:
                State_BuyItem();
                break;

            case BotState.SellTrash:
                State_SellTrash();
                break;
        }
    }

    // ==================== 상태 구현 ====================

    /// <summary>
    /// Idle: 다음 행동 결정
    /// </summary>
    void State_Idle()
    {
        Debug.Log("[AIBot] State: Idle - 다음 행동 결정 중...");

        // 1. 인벤토리가 가득 차면 판매
        if (_trashCollector != null && _trashCollector.inventory.Count >= 15)
        {
            ChangeState(BotState.SellTrash);
            return;
        }

        // 2. 씨앗이 있으면 심기
        if (HasSeed())
        {
            float random = Random.value;
            if (random < plantPriority)
            {
                ChangeState(BotState.FindSeedSpot);
                return;
            }
        }

        // 3. 골드가 있고 씨앗이 없으면 구매
        if (!HasSeed() && _trashCollector != null && _trashCollector.gold >= 30)
        {
            ChangeState(BotState.BuyItem);
            return;
        }

        // 4. 기본 행동: 쓰레기 수거
        ChangeState(BotState.FindTrash);
    }

    /// <summary>
    /// FindTrash: 가장 가까운 쓰레기 찾기
    /// </summary>
    void State_FindTrash()
    {
        GameObject[] trashes = GameObject.FindGameObjectsWithTag("Trash");

        if (trashes.Length == 0)
        {
            Debug.Log("[AIBot] 쓰레기가 없습니다. Idle로 복귀.");
            ChangeState(BotState.Idle);
            return;
        }

        // 가장 가까운 쓰레기 찾기
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (GameObject trash in trashes)
        {
            float distance = Vector3.Distance(transform.position, trash.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = trash;
            }
        }

        if (nearest != null)
        {
            _targetObject = nearest;
            _targetPosition = nearest.transform.position;
            ChangeState(BotState.MoveToTrash);
            Debug.Log($"[AIBot] 쓰레기 발견! 거리: {minDistance:F2}");
        }
        else
        {
            ChangeState(BotState.Idle);
        }
    }

    /// <summary>
    /// MoveToTrash: 쓰레기로 이동
    /// </summary>
    void State_MoveToTrash()
    {
        // 목표가 사라졌으면 다시 찾기
        if (_targetObject == null)
        {
            ChangeState(BotState.FindTrash);
            return;
        }

        // 도착 확인
        float distance = Vector3.Distance(transform.position, _targetPosition);
        if (distance < arriveDistance)
        {
            ChangeState(BotState.CollectTrash);
        }
    }

    /// <summary>
    /// CollectTrash: 쓰레기 수거
    /// </summary>
    void State_CollectTrash()
    {
        Debug.Log("[AIBot] 쓰레기 수거 중...");

        // TODO: TrashItem의 OnPickup() 호출
        if (_targetObject != null)
        {
            TrashItem trashItem = _targetObject.GetComponent<TrashItem>();
            if (trashItem != null)
            {
                // trashItem.OnPickup(); // 실제 수거 로직
                Destroy(_targetObject); // 임시: 즉시 삭제
            }
        }

        _targetObject = null;
        _actionTimer = actionDelay;
        ChangeState(BotState.Idle);
    }

    /// <summary>
    /// FindSeedSpot: 씨앗 심을 위치 찾기
    /// </summary>
    void State_FindSeedSpot()
    {
        // TODO: 오염된 타일 찾기 (Tilemap 연동)
        // 임시: 랜덤 위치
        float randomX = Random.Range(-10f, 10f);
        float randomY = Random.Range(-10f, 10f);
        _targetPosition = new Vector3(randomX, randomY, 0f);

        Debug.Log($"[AIBot] 식재 위치 결정: {_targetPosition}");
        ChangeState(BotState.MoveToSpot);
    }

    /// <summary>
    /// MoveToSpot: 식재 위치로 이동
    /// </summary>
    void State_MoveToSpot()
    {
        float distance = Vector3.Distance(transform.position, _targetPosition);
        if (distance < arriveDistance)
        {
            ChangeState(BotState.PlantSeed);
        }
    }

    /// <summary>
    /// PlantSeed: 씨앗 심기
    /// </summary>
    void State_PlantSeed()
    {
        Debug.Log("[AIBot] 씨앗 심기 중...");

        // TODO: SeedPlanter의 TryPlantSeed() 호출
        if (_seedPlanter != null)
        {
            // _seedPlanter.PlantAt(_targetPosition);
        }

        _actionTimer = actionDelay;
        ChangeState(BotState.Idle);
    }

    /// <summary>
    /// BuyItem: NPC에서 물건 구매
    /// </summary>
    void State_BuyItem()
    {
        Debug.Log("[AIBot] NPC에서 씨앗 구매 중...");

        // TODO: NPC 위치 찾기 + 이동 + 구매
        if (_trashCollector != null)
        {
            _trashCollector.BuyItemFromButton("Seed");
        }

        _actionTimer = actionDelay;
        ChangeState(BotState.Idle);
    }

    /// <summary>
    /// SellTrash: NPC에게 쓰레기 판매
    /// </summary>
    void State_SellTrash()
    {
        Debug.Log("[AIBot] NPC에게 쓰레기 판매 중...");

        // TODO: NPC 위치 찾기 + 이동 + 판매
        if (_trashCollector != null)
        {
            _trashCollector.SellAllTrash();
        }

        _actionTimer = actionDelay;
        ChangeState(BotState.Idle);
    }

    // ==================== 이동 ====================

    void MoveToTarget()
    {
        Vector3 direction = (_targetPosition - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x, direction.y) * moveSpeed;
    }

    // ==================== 헬퍼 함수 ====================

    void ChangeState(BotState newState)
    {
        Debug.Log($"[AIBot] State: {_currentState} → {newState}");
        _currentState = newState;
    }

    bool HasSeed()
    {
        return _trashCollector != null
               && _trashCollector.inventory.ContainsKey("Seed")
               && _trashCollector.inventory["Seed"] > 0;
    }

    // ==================== 디버그 ====================

    void OnDrawGizmos()
    {
        // 목표 위치 표시
        if (_currentState == BotState.MoveToTrash || _currentState == BotState.MoveToSpot)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_targetPosition, 0.5f);
            Gizmos.DrawLine(transform.position, _targetPosition);
        }
    }
}