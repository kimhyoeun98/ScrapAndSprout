using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 봇 AI - 쓰레기 진짜로 줍기
/// </summary>
public class SimpleBotAI : MonoBehaviour
{
    // ────────────────────────────────────────
    //  설정
    // ────────────────────────────────────────
    [Header("AI 설정")]
    [SerializeField] private float _detectionRadius = 10f;   // 쓰레기 감지 반경
    [SerializeField] private float _arrivedThreshold = 0.5f; // 도달 판정 거리
    [SerializeField] private float _collectTime = 1.0f;      // 줍는 시간

    // ────────────────────────────────────────
    //  내부 변수
    // ────────────────────────────────────────
    private NavMeshAgent _agent;
    private GameObject _targetTrash;
    private bool _isCollecting = false;

    // ────────────────────────────────────────
    //  Unity 생명주기
    // ────────────────────────────────────────
    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        if (_agent == null)
        {
            Debug.LogError("[SimpleBotAI] NavMeshAgent 없음!");
        }
    }

    void Start()
    {
        Debug.Log($"[SimpleBotAI] {gameObject.name} 시작!");
        FindAndMoveToTrash();
    }

    void Update()
    {
        if (_agent == null || _isCollecting) return;

        // 쓰레기로 이동 중
        if (_targetTrash != null)
        {
            float dist = Vector3.Distance(transform.position, _targetTrash.transform.position);

            if (dist <= _arrivedThreshold)
            {
                // 도착! 줍기 시작
                StartCoroutine(CollectTrash());
            }
        }
        else
        {
            // 타겟 없으면 다시 찾기
            FindAndMoveToTrash();
        }
    }

    // ────────────────────────────────────────
    //  쓰레기 찾기
    // ────────────────────────────────────────
    void FindAndMoveToTrash()
    {
        // 범위 내 쓰레기 찾기
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _detectionRadius,
            LayerMask.GetMask("Trash")
        );

        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = hit.gameObject;
            }
        }

        if (nearest != null)
        {
            // 쓰레기 발견! 이동 시작
            _targetTrash = nearest;
            _agent.isStopped = false;
            _agent.SetDestination(_targetTrash.transform.position);

            Debug.Log($"[SimpleBotAI] {gameObject.name} 쓰레기 발견 → {_targetTrash.name}");
        }
        else
        {
            // 쓰레기 없으면 랜덤 이동
            Vector3 randomPos = transform.position + Random.insideUnitSphere * 5f;
            randomPos.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, 5f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
                Debug.Log($"[SimpleBotAI] {gameObject.name} 랜덤 이동");
            }
        }
    }

    // ────────────────────────────────────────
    //  쓰레기 수거 (진짜로!)
    // ────────────────────────────────────────
    IEnumerator CollectTrash()
    {
        _isCollecting = true;
        _agent.isStopped = true;

        Debug.Log($"[SimpleBotAI] {gameObject.name} 수거 시작!");

        // 줍는 애니메이션 (있으면)
        // GetComponent<Animator>()?.SetTrigger("Collect");

        // 줍는 시간 대기
        yield return new WaitForSeconds(_collectTime);

        // ✅ 쓰레기 진짜로 제거!
        if (_targetTrash != null)
        {
            Debug.Log($"[SimpleBotAI] {gameObject.name} 쓰레기 수거 완료! {_targetTrash.name} 제거!");
            Destroy(_targetTrash);
        }

        _targetTrash = null;
        _isCollecting = false;

        // 다음 쓰레기 찾기
        yield return new WaitForSeconds(0.5f);
        FindAndMoveToTrash();
    }

    // ────────────────────────────────────────
    //  디버그 시각화
    // ────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // 감지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        // 타겟 쓰레기
        if (_targetTrash != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _targetTrash.transform.position);
            Gizmos.DrawSphere(_targetTrash.transform.position, 0.3f);
        }
    }
}