using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;

/// <summary>
/// 플레이어 이동 + 배터리 시스템 (Photon Fusion 2 버전)
/// 원본 구조 유지 + 네트워크 최소 변경
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  이동 설정
    // ─────────────────────────────────────────

    [Header("── 이동 설정 ──")]
    public float moveSpeed = 5f;
    public float lowBatterySpeedMultiplier = 0.5f;

    // ─────────────────────────────────────────
    //  배터리 설정
    // ─────────────────────────────────────────

    [Header("── 배터리 설정 ──")]
    [Range(0f, 100f)]
    public float batteryMax = 100f;
    public float drainIntervalSeconds = 10f;
    public float drainAmountPercent = 2f;
    public float chargeAmountPercent = 50f;

    // ─────────────────────────────────────────
    //  배터리 UI 연결
    // ─────────────────────────────────────────

    [Header("── 배터리 UI 연결 ──")]
    public TextMeshProUGUI batteryText;
    public Image batteryBarImage;
    public GameObject batteryWarningUI;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    // [Networked] = 모든 클라이언트에 자동 동기화
    // 배터리가 로컬 변수면 다른 사람 화면에서 항상 100%로 보임
    [Networked] public float CurrentBattery { get; set; }

    private bool _isDead => CurrentBattery <= 0f;
    private float _drainTimer = 0f;
    private Rigidbody2D _rb;
    private TrashCollector _trashCollector;

    public bool CanAct => CurrentBattery > 0f;

    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trashCollector = GetComponent<TrashCollector>();
    }

    /// <summary>
    /// Spawned: Photon이 오브젝트를 네트워크에 등록 완료했을 때 호출
    /// Start() 대신 쓰는 이유: Photon 정보는 Spawned() 이후에만 유효함
    /// </summary>
    public override void Spawned()
    {
        Debug.Log($"[Photon] 스폰 완료 - 내 캐릭터: {HasInputAuthority}");

        if (HasStateAuthority)
        {
            // Host만 배터리 초기값 설정 — [Networked]라서 모든 클라이언트에 자동 전파
            CurrentBattery = batteryMax;
        }

        UpdateBatteryUI();
        Debug.Log($"[배터리] 초기화 완료: {CurrentBattery}%");

        if (!HasInputAuthority) return;

        // 배터리 UI 자동 연결 — Prefab 스폰 시 Inspector 연결이 끊기므로 코드로 찾기
        var batteryParent = GameObject.Find("Battery");
        if (batteryParent != null)
        {
            batteryText = batteryParent.GetComponentInChildren<TextMeshProUGUI>();
            batteryBarImage = batteryParent.GetComponentInChildren<Image>();
            batteryWarningUI = batteryParent.transform.Find("BatteryWarning")?.gameObject;
            Debug.Log("[배터리] UI 자동 연결 완료!");
        }

        // 카메라 자동 연결 — Cinemachine Follow를 내 플레이어로 설정
        foreach (var mono in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            var prop = mono.GetType().GetProperty("Follow");
            if (prop != null && prop.PropertyType == typeof(Transform))
            {
                prop.SetValue(mono, this.transform);
                Debug.Log($"[카메라] {mono.GetType().Name} Follow 연결 성공!");
                break;
            }
        }
    }

    // ─────────────────────────────────────────
    //  입력 처리 — 배터리/아이템만 처리
    //  이동 입력은 PhotonManager.OnInput()에서 처리
    // ─────────────────────────────────────────

    void Update()
    {
        // 내 캐릭터가 아니면 입력 처리 안 함
        if (!HasInputAuthority) return;

        // 배터리 자동 소모 타이머
        _drainTimer += Time.deltaTime;
        if (_drainTimer >= drainIntervalSeconds)
        {
            _drainTimer = 0f;
            // 배터리 소모는 Host에게 RPC로 요청
            // [Networked] 변수는 Host만 수정 가능하므로 RPC 필요
            RPC_DrainBattery(drainAmountPercent);
        }

        // B키: 배터리 아이템 사용
        if (Input.GetKeyDown(KeyCode.B))
            TryUseBatteryItem();
    }

    // ─────────────────────────────────────────
    //  이동 처리
    //  GetInput이 알아서 권한 필터링을 해줌 — HasInputAuthority 조기 리턴 제거
    //  NetworkRigidbody2D가 다른 플레이어 위치 자동 동기화
    // ─────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            float currentSpeed = _isDead
                ? moveSpeed * lowBatterySpeedMultiplier
                : moveSpeed;

            if (data.direction.magnitude > 0.1f)
                _rb.linearVelocity = data.direction.normalized * currentSpeed;
            else
                _rb.linearVelocity = Vector2.zero;
        }
    }

    // ─────────────────────────────────────────
    //  배터리 소모/충전
    // ─────────────────────────────────────────

    public void DrainBattery(float amount)
    {
        if (CurrentBattery <= 0f) return;
        // HasStateAuthority = Host만 [Networked] 변수 수정 가능
        if (!HasStateAuthority) return;

        CurrentBattery = Mathf.Max(0f, CurrentBattery - amount);
        if (CurrentBattery <= 0f) OnBatteryDead();
        else if (CurrentBattery <= 30f)
            Debug.Log($"[배터리] 잔량 부족: {CurrentBattery:F0}%");
    }

    public void ChargeBattery(float amount)
    {
        if (!HasStateAuthority) return;

        bool wasDead = _isDead;
        CurrentBattery = Mathf.Min(batteryMax, CurrentBattery + amount);
        if (wasDead && !_isDead)
            UIManager.Instance?.ShowStatusMessage("배터리 충전! 정상 복구", 2f);
        Debug.Log($"[배터리] 충전 완료: {CurrentBattery:F0}%");
    }

    void TryUseBatteryItem()
    {
        if (_trashCollector == null) return;
        if (!_trashCollector.inventory.ContainsKey("Battery")
            || _trashCollector.inventory["Battery"] <= 0)
        {
            UIManager.Instance?.ShowStatusMessage("배터리가 없습니다!", 2f);
            return;
        }
        if (CurrentBattery >= batteryMax)
        {
            UIManager.Instance?.ShowStatusMessage("배터리가 이미 가득 찼습니다.", 1.5f);
            return;
        }

        // 인벤토리 차감은 로컬에서 처리
        _trashCollector.inventory["Battery"]--;
        if (_trashCollector.inventory["Battery"] <= 0)
            _trashCollector.inventory.Remove("Battery");
        _trashCollector.RefreshUI();

        // 배터리 충전은 Host에게 RPC로 요청
        // [Networked] 변수는 Host만 수정 가능하므로 RPC 필요
        RPC_UseBatteryItem();
    }

    // 배터리 아이템 사용 RPC — Host가 받아서 충전 처리
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_UseBatteryItem()
    {
        ChargeBattery(chargeAmountPercent);
    }

    // 배터리 소모 RPC — Host가 받아서 [Networked] 변수 차이
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DrainBattery(float amount)
    {
        DrainBattery(amount);
    }

    void OnBatteryDead()
    {
        UIManager.Instance?.ShowStatusMessage("배터리 방전! 도움을 요청하세요!", 3f);
    }

    // ─────────────────────────────────────────
    //  UI 갱신
    //  Render() = 매 프레임 실행 — [Networked] 값이 바뀌면 모든 클라이언트에 반영
    // ─────────────────────────────────────────

    public override void Render()
    {
        UpdateBatteryUI();
    }

    void UpdateBatteryUI()
    {
        float ratio = CurrentBattery / batteryMax;
        if (batteryText != null)
            batteryText.text = $" {CurrentBattery:F0}%";
        if (batteryBarImage != null)
        {
            batteryBarImage.fillAmount = ratio;
            if (ratio <= 0.3f) batteryBarImage.color = Color.red;
            else if (ratio <= 0.6f) batteryBarImage.color = Color.yellow;
            else batteryBarImage.color = Color.green;
        }
        if (batteryWarningUI != null)
            batteryWarningUI.SetActive(CurrentBattery <= 30f);
    }

    public void SetBatteryFromServer(float serverBattery)
    {
        if (!HasStateAuthority) return;
        CurrentBattery = Mathf.Clamp(serverBattery, 0f, batteryMax);
        Debug.Log($"[배터리] 서버에서 로드: {CurrentBattery}%");
    }
}