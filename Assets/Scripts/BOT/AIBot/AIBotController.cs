using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;

/// <summary>
/// AI 봇 — 2D 사이드뷰 기준
///
/// [동작 방식]
/// - Host에서만 실행 (HasStateAuthority 체크)
/// - FSM: Idle → FindTrash → MoveToTrash → CollectTrash → SellTrash
/// - 좌우 이동만 (Y는 중력이 담당)
/// - TrashPile 태그로 쓰레기 더미 탐색
/// - NPC 태그로 판매 위치 탐색
/// </summary>
public class AIBotController : NetworkBehaviour
{
    public enum BotState
    {
        Idle,
        FindTrash,
        MoveToTrash,
        CollectTrash,
        MoveToTeleporter,
        WaitTeleport,
        SellTrash,
        PlaceDecoration    // 꾸미기 아이템 배치
    }

    private BotState _currentState = BotState.Idle;

    [Header("── 이동 설정 ──")]
    public float moveSpeed = 2.5f;
    public float arriveDistance = 1.5f;

    [Header("── 행동 설정 ──")]
    public float actionDelay = 1f;
    public float collectTime = 3f;

    [Header("── 텔레포터 설정 ──")]
    [Tooltip("TrashZone → SafeZone 텔레포터 위치 (X 좌표)")]
    public float teleporterToSafeX = -2f;
    [Tooltip("SafeZone → TrashZone 텔레포터 위치 (X 좌표)")]
    public float teleporterToTrashX = 2f;
    [Tooltip("SafeZone 경계 X (이 값보다 크면 SafeZone)")]
    public float safeZoneBoundaryX = 0f;

    // 컴포넌트
    private Rigidbody2D _rb;
    private TrashCollector _trashCollector;
    private SpriteRenderer _spriteRenderer;

    // 내부 상태
    private GameObject _targetObject;
    private float _actionTimer = 0f;
    private bool _isCollecting = false;
    private bool _inSafeZone = false;      // 현재 SafeZone에 있는지
    //private bool _waitingTeleport = false; // 텔레포트 대기 중
    private float _teleportTimer = 0f;    // 텔레포트 대기 타이머

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trashCollector = GetComponent<TrashCollector>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        Debug.Log("[AIBot] 스폰 완료 — AI 시작");

        _actionTimer = 3f;

        // 스폰 직후 15초간 텔레포터 무시
        var teleporters = FindObjectsByType<ZoneTeleporter>(FindObjectsSortMode.None);
        foreach (var teleporter in teleporters)
            teleporter.AddBotCooldown(gameObject);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (_actionTimer > 0f)
        {
            _actionTimer -= Runner.DeltaTime;
            return;
        }

        RunFSM();
    }
    public void ForceIdle()
    {
        StopMovement();
        _actionTimer = 3f;
        ChangeState(BotState.Idle);
    }
    // 폴백: Runner 없거나 StateAuthority 없을 때 Update에서 실행
    void Update()
    {
        if (Runner != null && HasStateAuthority) return; // FixedUpdateNetwork가 처리

        if (_actionTimer > 0f)
        {
            _actionTimer -= Time.deltaTime;
            return;
        }

        RunFSM();
    }

    void RunFSM()
    {
        switch (_currentState)
        {
            case BotState.Idle:            State_Idle(); break;
            case BotState.FindTrash:       State_FindTrash(); break;
            case BotState.MoveToTrash:     State_MoveToTrash(); break;
            case BotState.CollectTrash:    State_CollectTrash(); break;
            case BotState.MoveToTeleporter: State_MoveToTeleporter(); break;
            case BotState.WaitTeleport:    State_WaitTeleport(); break;
            case BotState.SellTrash:       State_SellTrash(); break;
            case BotState.PlaceDecoration: State_PlaceDecoration(); break;
        }

        // 현재 위치로 구역 판단
        _inSafeZone = transform.position.x > safeZoneBoundaryX;
    }

    // ── FSM 상태들 ──────────────────────────────

    static readonly string[] _decoItems = { "나무", "상자", "의자", "울타리", "꽃병", "탁자", "꽃밭" };

    bool HasDecoItem()
    {
        if (_trashCollector == null) return false;
        foreach (var name in _decoItems)
            if (_trashCollector.inventory.ContainsKey(name) && _trashCollector.inventory[name] > 0)
                return true;
        return false;
    }

    bool HasTrashItem()
    {
        if (_trashCollector == null) return false;
        foreach (var kv in _trashCollector.inventory)
            if (!System.Array.Exists(_decoItems, d => d == kv.Key) && kv.Value > 0)
                return true;
        return false;
    }

    void State_Idle()
    {
        if (_inSafeZone)
        {
            // SafeZone: 꾸미기 아이템 있으면 배치, 쓰레기 있으면 판매, 없으면 TrashZone으로
            if (HasDecoItem())
                ChangeState(BotState.PlaceDecoration);
            else if (HasTrashItem())
                ChangeState(BotState.SellTrash);
            else
                ChangeState(BotState.MoveToTeleporter);
        }
        else
        {
            // TrashZone: 인벤 3개 이상이면 SafeZone으로, 아니면 채굴
            if (_trashCollector != null && _trashCollector.inventory.Count >= 3)
                ChangeState(BotState.MoveToTeleporter);
            else
                ChangeState(BotState.FindTrash);
        }
    }

    void State_FindTrash()
    {
        // TrashPile 태그로 가장 가까운 더미 탐색
        GameObject[] piles = GameObject.FindGameObjectsWithTag("Trash");
        if (piles.Length == 0)
        {
            _actionTimer = 2f; // 2초 대기 후 재탐색
            ChangeState(BotState.Idle);
            return;
        }

        _targetObject = FindNearest(piles);
        ChangeState(BotState.MoveToTrash);
    }

    void State_MoveToTrash()
    {
        if (_targetObject == null)
        {
            ChangeState(BotState.FindTrash);
            return;
        }

        float dist = Mathf.Abs(transform.position.x - _targetObject.transform.position.x);

        if (dist <= arriveDistance)
        {
            // 도착 → 채굴 시작
            StopMovement();
            ChangeState(BotState.CollectTrash);
            return;
        }

        // X축 이동
        MoveTowardTarget(_targetObject.transform.position);
    }

    void State_CollectTrash()
    {
        if (!_isCollecting)
        {
            _isCollecting = true;
            _actionTimer = collectTime; // collectTime 후 채굴 완료
            Debug.Log("[AIBot] 채굴 중...");
            return;
        }

        // 채굴 완료
        _isCollecting = false;

        if (_targetObject != null)
        {
            // 쓰레기 더미에서 아이템 드랍 시뮬레이션
            var pile = _targetObject.GetComponent<TrashPile>();
            if (pile != null && _trashCollector != null)
            {
                // 랜덤 아이템 인벤토리에 추가
                string[] items = { "휴지", "바나나껍질", "음료캔", "디스크" };
                string item = items[Random.Range(0, items.Length)];
                if (!_trashCollector.inventory.ContainsKey(item))
                    _trashCollector.inventory[item] = 0;
                _trashCollector.inventory[item]++;
                Debug.Log($"[AIBot] {item} 획득!");
            }

            _targetObject = null;
        }

        _actionTimer = actionDelay;
        ChangeState(BotState.Idle);
    }

    //void State_MoveToTeleporter()
    //{
    //    // 현재 구역에 따라 타야 할 텔레포터 위치 결정
    //    float targetX = _inSafeZone ? teleporterToTrashX : teleporterToSafeX;
    //    Vector3 targetPos = new Vector3(targetX, transform.position.y, transform.position.z);

    //    float dist = Mathf.Abs(transform.position.x - targetX);

    //    if (dist <= arriveDistance)
    //    {
    //        StopMovement();
    //        ChangeState(BotState.WaitTeleport);
    //        _teleportTimer = 0.5f; // 0.5초 후 ↓키 입력 시뮬레이션
    //        Debug.Log($"[AIBot] 텔레포터 도착 — 대기 중");
    //        return;
    //    }

    //    MoveTowardTarget(targetPos);
    //}

    void State_MoveToTeleporter()
    {
        float targetX = _inSafeZone ? teleporterToTrashX : teleporterToSafeX;
        Vector3 targetPos = new Vector3(targetX, transform.position.y, transform.position.z);

        float dist = Mathf.Abs(transform.position.x - targetX);

        if (dist <= 1f)
        {
            StopMovement();
            _actionTimer = 2f;
            ChangeState(BotState.Idle);
            return;
        }

        MoveTowardTarget(targetPos);
    }

    void State_WaitTeleport()
    {
        _teleportTimer -= Runner.DeltaTime;

        if (_teleportTimer <= 0f)
        {
            // ZoneTeleporter가 OnTriggerEnter로 봇을 감지하도록
            // 봇이 텔레포터 범위 안에 있으면 자동으로 처리됨
            // 여기선 상태만 Idle로 돌려서 다음 행동 결정
            Debug.Log("[AIBot] 텔레포트 완료 대기");
            _actionTimer = 2f; // 텔레포트 완료될 때까지 대기
            ChangeState(BotState.Idle);
        }
    }

    void State_SellTrash()
    {
        // NPC 위치로 이동
        GameObject npc = GameObject.FindGameObjectWithTag("NPC");
        if (npc == null)
        {
            _actionTimer = 2f;
            ChangeState(BotState.Idle);
            return;
        }

        float dist = Mathf.Abs(transform.position.x - npc.transform.position.x);

        if (dist <= arriveDistance * 2f)
        {
            // NPC 도착 → 판매
            StopMovement();
            if (_trashCollector != null)
                _trashCollector.SellAllTrash();

            Debug.Log("[AIBot] 판매 완료!");
            _actionTimer = actionDelay;
            ChangeState(BotState.Idle);
            return;
        }

        MoveTowardTarget(npc.transform.position);
    }

    void State_PlaceDecoration()
    {
        // DecorationPlacer 탐색
        var placer = FindObjectsByType<DecorationPlacer>(FindObjectsSortMode.None)
            .FirstOrDefault();

        if (placer == null)
        {
            _actionTimer = 2f;
            ChangeState(BotState.Idle);
            return;
        }

        float dist = Mathf.Abs(transform.position.x - placer.transform.position.x);

        if (dist <= arriveDistance * 1.5f)
        {
            StopMovement();
            // 보유한 꾸미기 아이템 첫 번째 것을 배치
            foreach (var name in _decoItems)
            {
                if (_trashCollector != null &&
                    _trashCollector.inventory.ContainsKey(name) &&
                    _trashCollector.inventory[name] > 0)
                {
                    placer.PlaceItem(name);
                    Debug.Log($"[AIBot] 꾸미기 배치: {name}");
                    break;
                }
            }
            _actionTimer = actionDelay;
            ChangeState(BotState.Idle);
            return;
        }

        MoveTowardTarget(placer.transform.position);
    }

    // ── 이동 헬퍼 ──────────────────────────────

    void MoveTowardTarget(Vector3 targetPos)
    {
        if (_rb == null) return;

        float dir = Mathf.Sign(targetPos.x - transform.position.x);
        _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);

        // 스프라이트 방향
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = dir > 0;
    }

    void StopMovement()
    {
        if (_rb != null)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    GameObject FindNearest(GameObject[] objects)
    {
        GameObject nearest = null;
        float minDist = float.MaxValue;
        foreach (var obj in objects)
        {
            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = obj; }
        }
        return nearest;
    }

    void ChangeState(BotState newState)
    {
        _currentState = newState;
    }

    void OnDrawGizmos()
    {
        if (_targetObject == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _targetObject.transform.position);
    }
}