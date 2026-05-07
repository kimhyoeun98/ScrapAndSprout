using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // ==================== 이동 설정 ====================
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    // ==================== 배터리 시스템 ====================
    [Header("배터리")]
    [Tooltip("현재 배터리 (0~100)")]
    [Range(0f, 100f)]
    public float currentBattery = 100f;

    [Tooltip("이동 시 배터리 소모량 (% per second)")]
    public float batteryDrainPerSecond = 0.5f;  // 초당 0.5%

    [Tooltip("배터리가 0이 되면 이동 불가")]
    public bool stopMovementWhenEmpty = true;

    // ==================== 속성 (기존 코드 호환용) ====================
    /// <summary>
    /// 현재 배터리 (대문자 속성 - 기존 코드 호환용)
    /// </summary>
    public float CurrentBattery
    {
        get { return currentBattery; }
        set { currentBattery = Mathf.Clamp(value, 0f, 100f); }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 키보드 입력 받기 (WASD / 방향키)
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        // 배터리 UI 업데이트
        UpdateBatteryUI();
    }

    void FixedUpdate()
    {
        // 1. 입력받은 방향 그대로 벡터 생성 (보정 없음)
        Vector2 direction = new Vector2(moveInput.x, moveInput.y);

        // 2. 배터리 체크
        bool canMove = !stopMovementWhenEmpty || currentBattery > 0f;

        // 3. 이동 실행
        if (direction.magnitude > 0.1f && canMove)
        {
            // 입력 방향 그대로 속도 적용
            rb.linearVelocity = direction.normalized * moveSpeed;

            // 배터리 소모 (날씨 배율 적용)
            DrainBattery();
        }
        else
        {
            // 입력 없으면 즉시 정지
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ==================== 배터리 관리 ====================

    /// <summary>
    /// 플레이어가 행동 가능한지 확인 (배터리 체크)
    /// </summary>
    public bool CanAct()
    {
        return currentBattery > 0f;
    }

    /// <summary>
    /// 이동 시 배터리를 소모합니다 (날씨 배율 적용)
    /// </summary>
    public void DrainBattery()
    {
        if (currentBattery <= 0f)
            return;

        // 날씨 배율 가져오기
        float weatherMultiplier = 1.0f;
        if (WeatherManager.Instance != null)
        {
            weatherMultiplier = WeatherManager.Instance.GetBatteryMultiplier();
        }

        // 배터리 소모 (고정 프레임: FixedUpdate는 0.02초마다 호출)
        float drainAmount = batteryDrainPerSecond * Time.fixedDeltaTime * weatherMultiplier;
        currentBattery -= drainAmount;

        // 0 이하 방지
        if (currentBattery < 0f)
            currentBattery = 0f;
    }

    /// <summary>
    /// 특정 양만큼 배터리를 소모합니다 (날씨 배율 적용)
    /// TrashItem, SeedPlanter 등에서 사용
    /// </summary>
    /// <param name="amount">기본 소모량</param>
    public void DrainBattery(float amount)
    {
        // 날씨 배율 적용
        float weatherMultiplier = 1.0f;
        if (WeatherManager.Instance != null)
        {
            weatherMultiplier = WeatherManager.Instance.GetBatteryMultiplier();
        }

        float totalDrain = amount * weatherMultiplier;
        currentBattery -= totalDrain;

        if (currentBattery < 0f)
            currentBattery = 0f;

        Debug.Log($"[PlayerMovement] 배터리 소모: -{totalDrain}% (현재: {currentBattery}%)");
    }

    /// <summary>
    /// 배터리 UI 업데이트
    /// </summary>
    void UpdateBatteryUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBatteryUI(currentBattery, 100f);
        }
    }

    /// <summary>
    /// 배터리 충전
    /// </summary>
    /// <param name="amount">충전량</param>
    public void ChargeBattery(float amount)
    {
        currentBattery += amount;
        if (currentBattery > 100f)
            currentBattery = 100f;

        Debug.Log($"[PlayerMovement] 배터리 충전: +{amount}% (현재: {currentBattery}%)");
    }

    /// <summary>
    /// 특정 행동 시 배터리 소모 (수거, 식재 등)
    /// </summary>
    /// <param name="amount">기본 소모량</param>
    public void ConsumeBatteryForAction(float amount)
    {
        // 날씨 배율 적용
        float weatherMultiplier = 1.0f;
        if (WeatherManager.Instance != null)
        {
            weatherMultiplier = WeatherManager.Instance.GetBatteryMultiplier();
        }

        float totalDrain = amount * weatherMultiplier;
        currentBattery -= totalDrain;

        if (currentBattery < 0f)
            currentBattery = 0f;

        Debug.Log($"[PlayerMovement] 행동 배터리 소모: -{totalDrain}% (현재: {currentBattery}%)");
    }
}