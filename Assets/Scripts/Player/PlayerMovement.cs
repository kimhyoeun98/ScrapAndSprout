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
    public float drainIntervalSeconds = 10f;
    public float drainAmountPercent = 2f;
    public float chargeAmountPercent = 50f;

    [Header("── 배터리 UI 연결 ──")]
    public TextMeshProUGUI batteryText;
    public Image batteryBarImage;
    public GameObject batteryWarningUI;

    [Networked] public float CurrentBattery { get; set; }

    private bool _isDead => CurrentBattery <= 0f;
    public bool CanAct => CurrentBattery > 0f;

    private Rigidbody2D _rb;
    private TrashCollector _trashCollector;
    private SpriteRenderer _spriteRenderer;
    private float _drainTimer = 0f;
    private bool _isGrounded = false;

    // 이동 잠금 (미니게임 중 이동 불가)
    public bool IsMovementLocked { get; private set; } = false;

    public void LockMovement() { IsMovementLocked = true; }
    public void UnlockMovement() { IsMovementLocked = false; }

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

        if (!HasInputAuthority) return;

        var batteryParent = GameObject.Find("Battery");
        if (batteryParent != null)
        {
            batteryText = batteryParent.GetComponentInChildren<TextMeshProUGUI>();
            batteryBarImage = batteryParent.GetComponentInChildren<Image>();
            batteryWarningUI = batteryParent.transform.Find("BatteryWarning")?.gameObject;
            Debug.Log("[배터리] UI 자동 연결 완료!");
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

        // ── 바닥 감지 ──
        var col = GetComponent<CapsuleCollider2D>();
        float halfHeight = col != null ? col.bounds.extents.y : 1f;
        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y - halfHeight);
        _isGrounded = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance, groundLayer);

        // ── 점프 ──
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            Debug.Log($"[점프 시도] isGrounded={_isGrounded}, groundLayer={groundLayer.value}");
            if (_isGrounded)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
                Debug.Log("[점프] 실행!");
            }
        }

        // ── 배터리 소모 타이머 ──
        _drainTimer += Time.deltaTime;
        if (_drainTimer >= drainIntervalSeconds)
        {
            _drainTimer = 0f;
            RPC_DrainBattery(drainAmountPercent);
        }

        // ── B키: 배터리 아이템 사용 ──
        if (Input.GetKeyDown(KeyCode.B))
            TryUseBatteryItem();
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData data)) return;

        // 이동 잠금 중이면 정지
        if (IsMovementLocked)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        float currentSpeed = _isDead
            ? moveSpeed * lowBatterySpeedMultiplier
            : moveSpeed;

        // 좌우 이동만 — Y는 중력이 담당
        float targetVelX = data.direction.x * currentSpeed;
        _rb.linearVelocity = new Vector2(
            Mathf.Lerp(_rb.linearVelocity.x, targetVelX, 0.5f),
            _rb.linearVelocity.y
        );

        // 이동 방향에 따라 스프라이트 좌우 반전
        if (_spriteRenderer != null)
        {
            if (data.direction.x > 0.1f)
                _spriteRenderer.flipX = false; // 오른쪽
            else if (data.direction.x < -0.1f)
                _spriteRenderer.flipX = true;  // 왼쪽
        }
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
        UIManager.Instance?.ShowStatusMessage("배터리 방전! 도움을 요청하세요!", 3f);
    }

    public void SetBatteryFromServer(float serverBattery)
    {
        if (!HasStateAuthority) return;
        CurrentBattery = Mathf.Clamp(serverBattery, 0f, batteryMax);
    }

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
            batteryBarImage.color = ratio <= 0.3f ? Color.red
                                  : ratio <= 0.6f ? Color.yellow
                                  : Color.green;
        }
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