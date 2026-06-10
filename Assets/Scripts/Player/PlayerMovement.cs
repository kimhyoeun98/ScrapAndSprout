using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;

/// <summary>
/// 플레이어 이동 + 배터리 시스템 (Photon Fusion 2 버전)
/// 2D 사이드뷰 기준: 좌우 이동 + 점프(↑) + 중력
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [Header("── 이동 설정 ──")]
    public float moveSpeed = 5f;
    public float lowBatterySpeedMultiplier = 0.5f;

    [Header("── 점프 설정 ──")]
    public float jumpForce = 10f;
    [Tooltip("바닥 감지 레이 발사 거리")]
    public float groundCheckDistance = 1f;
    [Tooltip("바닥 레이어 (Tilemap_Collision 오브젝트의 Layer)")]
    public LayerMask groundLayer;

    [Header("── 배터리 설정 ──")]
    [Range(0f, 100f)]
    public float batteryMax = 100f;
    public float chargeAmountPercent = 50f;

    [Header("── 배터리 UI 연결 ──")]
    public TextMeshProUGUI batteryText;
    public Image batteryBarImage;
    public GameObject batteryWarningUI;
    private Slider _batterySlider;

    [Networked] public float CurrentBattery { get; set; }
    [Networked] public NetworkBool FacingRight { get; set; }

    private bool _isDead => CurrentBattery <= 0f;
    public bool CanAct => CurrentBattery > 0f;

    private Rigidbody2D _rb;
    private TrashCollector _trashCollector;
    private SpriteRenderer _spriteRenderer;
    private bool _isGrounded = false;
    private bool _isReviving = false;

    // 이동 잠금 (미니게임 중 이동 불가)
    //public bool IsMovementLocked { get; private set; } = false;
    //public void LockMovement() { IsMovementLocked = true; }
    //public void UnlockMovement() { IsMovementLocked = false; }
    public bool IsMovementLocked { get; set; } = false;
    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trashCollector = GetComponent<TrashCollector>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void Spawned()
    {
        Debug.Log($"[Photon] 스폰 완료 - 내 캐릭터: {HasInputAuthority}");

        // ── 봇이면 PlayerMovement 비활성화 ──
        // AIBotController가 있고 InputAuthority가 없으면 봇
        var bot = GetComponent<AIBotController>();
        if (bot != null && !HasInputAuthority)
        {
            Debug.Log("[PlayerMovement] 봇으로 감지 — PlayerMovement 비활성화");
            enabled = false;
            return;
        }

        if (HasStateAuthority)
        {
            CurrentBattery = batteryMax;
            FacingRight = true;
            Debug.Log($"[배터리] 초기화: {CurrentBattery}%");
        }

        UpdateBatteryUI();

        // CapsuleCollider2D 크기 강제 설정
        var cap = GetComponent<CapsuleCollider2D>();
        if (cap != null)
            cap.size = new Vector2(cap.size.x, 1.5f);

        // Z축 고정
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            -1.2f
        );

        // 이름 태그 — Spawned() 직접 Setup 호출로 멀티 안전 보장
        var tag = GetComponent<PlayerNameTag>();
        if (tag == null) tag = gameObject.AddComponent<PlayerNameTag>();
        tag.Setup(HasInputAuthority);

        if (!HasInputAuthority) return;

        // Battery UI 찾기 (Canvas/HUD/BatteryBar)
        var batteryBar = GameObject.Find("BatteryBar");
        if (batteryBar == null)
        {
            // 대체 경로: Canvas에서 찾기
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var hud = canvas.transform.Find("HUD");
                if (hud != null)
                    batteryBar = hud.Find("BatteryBar")?.gameObject;
            }
        }

        // 여전히 없으면 모든 Canvas 검색
        if (batteryBar == null)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var found = canvas.transform.Find("BatteryBar");
                if (found != null) { batteryBar = found.gameObject; break; }
            }
        }

        if (batteryBar != null)
        {
            // Slider 찾기
            _batterySlider = batteryBar.GetComponent<Slider>();
            if (_batterySlider != null)
            {
                Debug.Log($"[배터리] Slider 찾음");
            }

            // Fill Area에서 Image 찾기
            var fillArea = batteryBar.transform.Find("Fill Area");
            if (fillArea != null)
            {
                batteryBarImage = fillArea.GetComponent<Image>();
                if (batteryBarImage != null)
                    Debug.Log($"[배터리] Fill Area Image 찾음");
            }

            // Background Image 찾기
            var background = batteryBar.transform.Find("Background");
            if (background != null)
            {
                var bgImg = background.GetComponent<Image>();
                Debug.Log($"[배터리] Background Image 찾음");
            }

            Debug.Log($"[배터리] UI 연결 완료: Slider={_batterySlider != null}, FillImage={batteryBarImage != null}");
        }
        else
        {
            Debug.LogError("[배터리] BatteryBar GameObject를 찾을 수 없습니다");
        }

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

    void Update()
    {
        if (!HasInputAuthority) return;

        // 이동 잠금 중이면 입력 무시
        if (IsMovementLocked) return;

        // ── B키: 배터리 아이템 사용 ──
        if (Input.GetKeyDown(KeyCode.B))
            TryUseBatteryItem();
    }

    //public override void FixedUpdateNetwork()
    //{
    //    if (!GetInput(out NetworkInputData data)) return;

    //    // 이동 잠금 중이면 정지
    //    if (IsMovementLocked)
    //    {
    //        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    //        return;
    //    }

    public override void FixedUpdateNetwork()
    {
        // ── 배터리 자연 회복: StateAuthority(Host)에서 모든 플레이어에 적용 ──
        if (HasStateAuthority && CurrentBattery < batteryMax)
        {
            float recovered = 0.5f * Runner.DeltaTime;
            CurrentBattery = Mathf.Min(batteryMax, CurrentBattery + recovered);
            if (HasInputAuthority && Random.Range(0f, 1f) < 0.01f) // 1% 확률로 디버깅 로그
                Debug.Log($"[배터리] 회복: {CurrentBattery:F1}% (+{recovered:F3})");
        }

        if (!GetInput(out NetworkInputData data)) return;
        if (IsMovementLocked)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }
        

    // ── 바닥 감지 ──
    var col = GetComponent<CapsuleCollider2D>();
        float halfHeight = col != null ? col.bounds.extents.y : 1f;
        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y - halfHeight);
        _isGrounded = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance, groundLayer);

        float currentSpeed = _isDead
            ? moveSpeed * lowBatterySpeedMultiplier
            : moveSpeed;

        // ── 좌우 이동 ──
        float targetVelX = data.direction.x * currentSpeed;
        _rb.linearVelocity = new Vector2(
            Mathf.Lerp(_rb.linearVelocity.x, targetVelX, 0.5f),
            _rb.linearVelocity.y
        );

        // ── 점프 ── NetworkInput으로 처리 (클라이언트 동기화)
        if (data.jump && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            Debug.Log($"[Jump] isGrounded={_isGrounded} jumpForce={jumpForce}");
        }

        // ── 방향 전환 — [Networked]로 동기화 ──
        if (data.direction.x > 0.1f)
            FacingRight = true;
        else if (data.direction.x < -0.1f)
            FacingRight = false;

        // ── 텔레포트 입력 감지 ──
        if (data.teleport)
        {
            var teleporters = FindObjectsByType<ZoneTeleporter>(FindObjectsSortMode.None);
            foreach (var teleporter in teleporters)
                teleporter.TryTeleportByNetwork(gameObject);
        }

    }

    public override void Render()
    {
        // ── 방향 동기화 — 모든 클라이언트에서 실행 ──
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = !FacingRight;

        UpdateBatteryUI();

        // ── 배터리 위험 경고 (내 캐릭터만) ──
        if (HasInputAuthority)
            UIManager.Instance?.SetBatteryWarning(CurrentBattery > 0f && CurrentBattery <= 20f);
    }

    public void DrainBattery(float amount)
    {
        if (CurrentBattery <= 0f || !HasStateAuthority) return;
        CurrentBattery = Mathf.Max(0f, CurrentBattery - amount);
        if (CurrentBattery <= 0f) OnBatteryDead();
    }

    public void ChargeBattery(float amount)
    {
        if (!HasStateAuthority) return;
        bool wasDead = _isDead;
        CurrentBattery = Mathf.Min(batteryMax, CurrentBattery + amount);
        if (wasDead && !_isDead)
            UIManager.Instance?.ShowStatusMessage("배터리 충전! 정상 복구", 2f);
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

        _trashCollector.inventory["Battery"]--;
        if (_trashCollector.inventory["Battery"] <= 0)
            _trashCollector.inventory.Remove("Battery");
        _trashCollector.RefreshUI();
        RPC_UseBatteryItem();
    }

    /// <summary>
    /// 미니게임 이동 잠금 — StateAuthority에서 InputAuthority로 전달
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_LockMovement()
    {
        IsMovementLocked = true;
    }

    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_UnlockMovement()
    {
        IsMovementLocked = false;
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_UseBatteryItem() => ChargeBattery(chargeAmountPercent);

    /// <summary>
    /// 텔레포트 — Host에서 위치 변경 후 모든 클라이언트 동기화
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Teleport(Vector3 targetPos)
    {
        targetPos.z = -1.2f;

        // NetworkRigidbody2D.Teleport() — StateAuthority에서 호출해야 작동
        bool teleported = false;
        foreach (var comp in GetComponents<MonoBehaviour>())
        {
            if (comp.GetType().Name == "NetworkRigidbody2D")
            {
                var method = comp.GetType().GetMethod("Teleport");
                if (method != null)
                {
                    method.Invoke(comp, new object[] {
                        (Vector3?)targetPos,
                        null
                    });
                    teleported = true;
                    Debug.Log($"[텔레포트] NetworkRigidbody2D.Teleport() → {targetPos}");
                    break;
                }
            }
        }

        if (!teleported)
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = new Vector2(targetPos.x, targetPos.y);
            }
            transform.position = targetPos;
            Debug.Log($"[텔레포트] 직접 이동 → {targetPos}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DrainBattery(float amount) => DrainBattery(amount);

    void OnBatteryDead()
    {
        UIManager.Instance?.ShowStatusMessage("배터리 방전! 30초 후 자동 부활", 3f);
        if (!_isReviving)
            StartCoroutine(AutoReviveCoroutine());
    }
    /// <summary>
    /// 텔레포트 페이드 아웃 — InputAuthority 클라이언트 본인 화면에서만 실행
    /// StateAuthority(Host)가 호출 → 해당 플레이어 화면에서만 페이드 아웃
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_FadeOut(float duration)
    {
        UIManager.Instance?.FadeOut(duration);
    }

    /// <summary>
    /// 텔레포트 페이드 인 — InputAuthority 클라이언트 본인 화면에서만 실행
    /// StateAuthority(Host)가 호출 → 해당 플레이어 화면에서만 페이드 인
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_FadeIn(float duration)
    {
        UIManager.Instance?.FadeIn(duration);
    }
    IEnumerator AutoReviveCoroutine()
    {
        _isReviving = true;
        yield return new WaitForSeconds(30f);
        if (HasStateAuthority)
            ChargeBattery(30f); // 30% 회복으로 부활
        _isReviving = false;
        UIManager.Instance?.ShowStatusMessage("자동 부활!", 2f);
    }

    public void SetBatteryFromServer(float serverBattery)
    {
        if (!HasStateAuthority) return;
        CurrentBattery = Mathf.Clamp(serverBattery, 0f, batteryMax);
    }

    void UpdateBatteryUI()
    {
        float ratio = CurrentBattery / batteryMax;
        Color barColor = ratio <= 0.2f ? Color.red
                       : ratio <= 0.5f ? Color.yellow
                       : Color.green;

        // 텍스트 업데이트
        if (batteryText != null)
        {
            batteryText.text = $"⚡ {CurrentBattery:F0}%";
            batteryText.color = barColor;
        }

        // Slider 업데이트 (있으면)
        if (_batterySlider != null)
        {
            _batterySlider.value = ratio;
            if (_batterySlider.fillRect != null)
            {
                var img = _batterySlider.fillRect.GetComponent<Image>();
                if (img != null) img.color = barColor;
            }
        }

        // Image 업데이트 (있으면)
        if (batteryBarImage != null)
        {
            batteryBarImage.fillAmount = ratio;
            batteryBarImage.color = barColor;
        }

        // 경고 UI
        if (batteryWarningUI != null)
            batteryWarningUI.SetActive(CurrentBattery <= 30f);
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<CapsuleCollider2D>();
        float halfHeight = col != null ? col.bounds.extents.y : 1f;
        Vector2 origin = new Vector2(transform.position.x, transform.position.y - halfHeight);
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }
}