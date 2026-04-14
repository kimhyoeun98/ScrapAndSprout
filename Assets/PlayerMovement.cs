using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 플레이어 이동 + 배터리 시스템
///
/// [배터리 규칙]
/// - 시간이 지나면 자동으로 소모됩니다 (기본 2%/10초)
/// - 배터리 0% → 이동속도 절반, 수거/식재 불가
/// - 인벤토리의 배터리 아이템으로 충전 가능
/// - (추후) 다른 플레이어가 근처에서 배터리 아이템 사용 시 충전 가능
///
/// [부착 위치] Player 오브젝트에 부착하세요.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  이동 설정
    // ─────────────────────────────────────────

    [Header("── 이동 설정 ──")]
    [Tooltip("기본 이동 속도")]
    public float moveSpeed = 5f;

    [Tooltip("배터리 방전 시 이동 속도 배율 (0.5 = 절반)")]
    public float lowBatterySpeedMultiplier = 0.5f;

    // ─────────────────────────────────────────
    //  배터리 설정
    // ─────────────────────────────────────────

    [Header("── 배터리 설정 ──")]
    [Tooltip("시작 배터리 (0~100)")]
    [Range(0f, 100f)]
    public float batteryMax = 100f;

    [Tooltip("N초마다 배터리가 자동 소모됩니다")]
    public float drainIntervalSeconds = 10f;

    [Tooltip("한 번 소모될 때 줄어드는 배터리량 (%)")]
    public float drainAmountPercent = 2f;

    [Tooltip("배터리 아이템 1개로 충전되는 양 (%)")]
    public float chargeAmountPercent = 50f;

    // ─────────────────────────────────────────
    //  배터리 UI 연결
    // ─────────────────────────────────────────

    [Header("── 배터리 UI 연결 ──")]
    [Tooltip("배터리 % 수치 텍스트 (예: '75%')")]
    public TextMeshProUGUI batteryText;

    [Tooltip("배터리 게이지 바 (Image - Filled 타입)")]
    public Image batteryBarImage;

    [Tooltip("배터리 경고 아이콘/패널 (30% 이하 시 표시)")]
    public GameObject batteryWarningUI;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    /// <summary>현재 배터리 잔량 (0~100)</summary>
    private float _currentBattery;

    /// <summary>배터리 방전 상태 여부</summary>
    private bool _isDead => _currentBattery <= 0f;

    /// <summary>자동 소모 타이머</summary>
    private float _drainTimer = 0f;

    // 컴포넌트 참조
    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private TrashCollector _trashCollector; // 배터리 아이템 인벤토리 확인용

    // ─────────────────────────────────────────
    //  외부에서 읽을 수 있는 프로퍼티
    // ─────────────────────────────────────────

    /// <summary>현재 배터리 (0~100). TrashCollector 등 외부에서 참조 가능</summary>
    public float CurrentBattery => _currentBattery;

    /// <summary>배터리가 있어서 정상 행동 가능한지 여부</summary>
    public bool CanAct => _currentBattery > 0f;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trashCollector = GetComponent<TrashCollector>();
        _currentBattery = batteryMax;

        // 서버에서 배터리 값 받아왔다면 덮어쓸 수 있도록
        // (ApiManager 초기화 후 LoadPlayerDataFromServer에서 호출 예정)

        UpdateBatteryUI();
        Debug.Log($"[배터리] 초기화 완료: {_currentBattery}%");
    }

    void Update()
    {
        // ── WASD 입력 ──
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        // ── 배터리 자동 소모 타이머 ──
        _drainTimer += Time.deltaTime;
        if (_drainTimer >= drainIntervalSeconds)
        {
            _drainTimer = 0f;
            DrainBattery(drainAmountPercent);
        }

        // ── B키: 배터리 아이템 사용 (인벤토리에서 꺼내 충전) ──
        if (Input.GetKeyDown(KeyCode.B))
        {
            TryUseBatteryItem();
        }
    }

    void FixedUpdate()
    {
        // ── 이동 속도 결정 ──
        // 배터리 방전 시: 절반 속도
        // 정상 시: 기본 속도
        float currentSpeed = _isDead
            ? moveSpeed * lowBatterySpeedMultiplier
            : moveSpeed;

        Vector2 direction = new Vector2(_moveInput.x, _moveInput.y);

        if (direction.magnitude > 0.1f)
            _rb.linearVelocity = direction.normalized * currentSpeed;
        else
            _rb.linearVelocity = Vector2.zero;
    }

    // ─────────────────────────────────────────
    //  배터리 소모/충전 함수
    // ─────────────────────────────────────────

    /// <summary>
    /// 배터리를 amount만큼 소모합니다.
    /// 외부(TrashCollector, SeedPlanter)에서 호출 가능합니다.
    ///
    /// [사용 예시]
    /// GetComponent&lt;PlayerMovement&gt;().DrainBattery(5f); // 5% 소모
    /// </summary>
    public void DrainBattery(float amount)
    {
        if (_currentBattery <= 0f) return; // 이미 방전 상태면 무시

        _currentBattery = Mathf.Max(0f, _currentBattery - amount);
        UpdateBatteryUI();

        // 방전 직후 한 번만 경고 출력
        if (_currentBattery <= 0f)
        {
            OnBatteryDead();
        }
        else if (_currentBattery <= 30f)
        {
            Debug.Log($"[배터리] 잔량 부족: {_currentBattery:F0}%");
        }
    }

    /// <summary>
    /// 배터리를 amount만큼 충전합니다.
    /// 다른 플레이어가 충전해줄 때도 이 함수를 호출하면 됩니다.
    /// </summary>
    public void ChargeBattery(float amount)
    {
        bool wasDead = _isDead;

        _currentBattery = Mathf.Min(batteryMax, _currentBattery + amount);
        UpdateBatteryUI();

        // 방전 상태에서 회복된 경우
        if (wasDead && !_isDead)
        {
            Debug.Log($"[배터리]  방전 해제! 이동속도 복구");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowStatusMessage(" 배터리 충전! 정상 복구", 2f);
        }

        Debug.Log($"[배터리] 충전 완료: {_currentBattery:F0}%");
    }

    // ─────────────────────────────────────────
    //  배터리 아이템 사용
    // ─────────────────────────────────────────

    /// <summary>
    /// B키를 누르면 인벤토리의 배터리 아이템을 1개 꺼내 충전합니다.
    ///
    /// [협력 확장 포인트]
    /// 추후 Photon RPC로 다른 플레이어의 ChargeBattery()를 호출하면
    /// "다른 플레이어 충전" 기능이 됩니다.
    /// </summary>
    void TryUseBatteryItem()
    {
        if (_trashCollector == null)
        {
            Debug.LogWarning("[배터리] TrashCollector를 찾을 수 없습니다!");
            return;
        }

        // 인벤토리에 배터리 있는지 확인
        if (!_trashCollector.inventory.ContainsKey("Battery")
            || _trashCollector.inventory["Battery"] <= 0)
        {
            Debug.Log("[배터리] 배터리 아이템이 없습니다. NPC에서 구매하세요.");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowStatusMessage("배터리가 없습니다! NPC에서 구매하세요.", 2f);
            return;
        }

        // 이미 가득 찼으면 사용 안 함
        if (_currentBattery >= batteryMax)
        {
            Debug.Log("[배터리] 이미 가득 찼습니다!");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowStatusMessage("배터리가 이미 가득 찼습니다.", 1.5f);
            return;
        }

        // 배터리 아이템 1개 소모 + 충전
        _trashCollector.inventory["Battery"]--;
        if (_trashCollector.inventory["Battery"] <= 0)
            _trashCollector.inventory.Remove("Battery");

        _trashCollector.RefreshUI();
        ChargeBattery(chargeAmountPercent);

        Debug.Log($"[배터리] 아이템 사용! +{chargeAmountPercent}% 충전 | 현재: {_currentBattery:F0}%");
    }

    // ─────────────────────────────────────────
    //  배터리 방전 이벤트
    // ─────────────────────────────────────────

    /// <summary>
    /// 배터리가 0%가 됐을 때 한 번 호출됩니다.
    /// </summary>
    void OnBatteryDead()
    {
        Debug.Log("[배터리] 방전! 이동속도 절반, 행동 불가");

        if (UIManager.Instance != null)
            UIManager.Instance.ShowStatusMessage(" 배터리 방전! 이동속도 감소\n다른 플레이어에게 도움을 요청하세요!", 3f);
    }

    // ─────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────

    /// <summary>
    /// 배터리 UI 전체를 갱신합니다.
    /// </summary>
    void UpdateBatteryUI()
    {
        float ratio = _currentBattery / batteryMax; // 0.0 ~ 1.0

        // 텍스트: "75%"
        if (batteryText != null)
            batteryText.text = $" {_currentBattery:F0}%";

        // 게이지 바: fillAmount로 자연스럽게 줄어듦
        if (batteryBarImage != null)
            batteryBarImage.fillAmount = ratio;

        // 색상: 30% 이하 빨간색, 60% 이하 노란색, 이상 초록색
        if (batteryBarImage != null)
        {
            if (ratio <= 0.3f) batteryBarImage.color = Color.red;
            else if (ratio <= 0.6f) batteryBarImage.color = Color.yellow;
            else batteryBarImage.color = Color.green;
        }

        // 경고 아이콘: 30% 이하 시 표시
        if (batteryWarningUI != null)
            batteryWarningUI.SetActive(_currentBattery <= 30f);
    }

    // ─────────────────────────────────────────
    //  외부 초기화용 (서버에서 배터리 값 받을 때)
    // ─────────────────────────────────────────

    /// <summary>
    /// 서버에서 받은 배터리 초기값을 설정합니다.
    /// LoadPlayerDataFromServer() 성공 콜백에서 호출하세요.
    /// </summary>
    public void SetBatteryFromServer(float serverBattery)
    {
        _currentBattery = Mathf.Clamp(serverBattery, 0f, batteryMax);
        UpdateBatteryUI();
        Debug.Log($"[배터리] 서버에서 로드: {_currentBattery}%");
    }
}