using UnityEngine;
using TMPro;

/// <summary>
/// 쓰레기 아이템 개별 스크립트 (E키 수거 방식)
///
/// [이 스크립트의 역할]
/// 각 쓰레기 오브젝트에 부착합니다.
/// 플레이어가 범위에 들어오면 "[E] 줍기" 안내를 표시하고,
/// E키를 누르면 수거합니다.
///
/// [부착 위치] can_0, can_1, banana_0 등 쓰레기 오브젝트 각각에 부착
/// </summary>
public class TrashItem : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────

    [Header("안내 UI 연결")]
    [Tooltip("'[E] 줍기' 안내 텍스트. 없으면 상태 메시지로 대체됩니다.")]
    public GameObject pickupGuideUI; // 각 쓰레기마다 자식 UI 또는 공용 UI

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    /// <summary>플레이어가 범위 안에 있는지</summary>
    private bool _isPlayerNearby = false;

    /// <summary>수거 대상 플레이어의 TrashCollector</summary>
    private TrashCollector _playerCollector;

    /// <summary>이미 수거됐는지 (중복 수거 방지)</summary>
    private bool _isCollected = false;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 안내 UI 초기 비활성화
        if (pickupGuideUI != null)
            pickupGuideUI.SetActive(false);
    }

    void Update()
    {
        // 플레이어가 범위 안에 있고 E키를 누르면 수거
        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            TryCollect();
        }
    }

    // ─────────────────────────────────────────
    //  수거 로직
    // ─────────────────────────────────────────

    /// <summary>
    /// E키 입력 시 수거를 시도합니다.
    /// </summary>
    void TryCollect()
    {
        // 중복 수거 방지
        if (_isCollected) return;

        // PlayerCollector 없으면 수거 불가
        if (_playerCollector == null)
        {
            Debug.LogWarning("[쓰레기] TrashCollector를 찾을 수 없습니다!");
            return;
        }

        // 배터리 방전 시 수거 불가
        var pm = _playerCollector.GetComponent<PlayerMovement>();
        if (pm != null && !pm.CanAct())
        {
            Debug.Log("[쓰레기] 배터리 방전 상태에서는 수거할 수 없습니다!");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowStatusMessage("배터리 방전! 수거 불가", 2f);
            return;
        }

        // ── 수거 실행 ──
        _isCollected = true;

        // 아이템 이름 추출 (Clone 제거)
        string itemName = gameObject.name.Replace("(Clone)", "").Trim();

        // 인벤토리에 추가
        if (_playerCollector.inventory.ContainsKey(itemName))
            _playerCollector.inventory[itemName]++;
        else
            _playerCollector.inventory.Add(itemName, 1);

        // UI 갱신
        _playerCollector.RefreshUI();

        // 배터리 소모
        pm?.DrainBattery(5f);

        // 상태 메시지
        if (UIManager.Instance != null)
            UIManager.Instance.ShowStatusMessage($"{itemName} 수거! (보유: {_playerCollector.inventory[itemName]}개)", 1.5f);

        Debug.Log($"[수거] {itemName} 수거 완료!");

        // 안내 UI 숨기기
        if (pickupGuideUI != null)
            pickupGuideUI.SetActive(false);

        // 오브젝트 제거
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    //  Trigger 충돌 감지
    // ─────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();

        // 안내 UI 표시
        if (pickupGuideUI != null)
            pickupGuideUI.SetActive(true);
        else if (UIManager.Instance != null)
            UIManager.Instance.ShowStatusMessage("[E] 줍기", 999f); // 범위 벗어날 때까지 유지

        Debug.Log($"[쓰레기] 플레이어 접근 — E키로 수거 가능");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _isPlayerNearby = false;
        _playerCollector = null;

        // 안내 UI 숨기기
        if (pickupGuideUI != null)
            pickupGuideUI.SetActive(false);
        else if (UIManager.Instance != null)
            UIManager.Instance.ShowStatusMessage("", 0f);
    }
}