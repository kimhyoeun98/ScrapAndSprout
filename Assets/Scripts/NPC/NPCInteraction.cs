using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// NPC 상호작용 시스템 (S-03: NPC 거래)
///
/// [이 클래스의 역할]
/// 플레이어가 NPC에게 다가가면 상점 UI를 열고,
/// 쓰레기 판매 / 씨앗·배터리 구매를 처리합니다.
///
/// [부착 위치] NPC 오브젝트에 부착하세요.
///
/// [Inspector 연결 필요 항목]
/// - interactionUI : "F키를 눌러 상점 열기" 안내 텍스트 오브젝트
/// - shopPanel     : 상점 팝업 패널 오브젝트
/// - shopInfoText  : 상점 안에 표시할 보유 골드/씨앗 안내 텍스트 (선택)
/// </summary>
public class NPCInteraction : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────

    [Header("UI 연결 (Inspector에서 드래그)")]
    [Tooltip("NPC 근처에서 보여줄 '[F] 상점 열기' 안내 UI")]
    public GameObject interactionUI;

    [Tooltip("상점 팝업 패널 (판매/구매 버튼 포함)")]
    public GameObject shopPanel;

    [Tooltip("상점 내 골드·아이템 정보 표시 텍스트 (없어도 동작함)")]
    public TextMeshProUGUI shopInfoText;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    /// <summary>플레이어가 NPC 근처에 있는지 여부</summary>
    private bool _isPlayerNearby = false;

    /// <summary>
    /// 상호작용 중인 플레이어의 TrashCollector 참조
    /// (골드, 인벤토리 정보를 읽기 위해 필요)
    /// </summary>
    private TrashCollector _playerCollector;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Update()
    {
        // RunnerSimulatePhysics2D 때문에 OnTrigger가 Client에서 안 불림
        // 직접 거리 체크로 대체
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f);

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            var pm = hit.GetComponent<PlayerMovement>();
            if (pm != null && pm.HasInputAuthority)
            {
                if (!_isPlayerNearby)
                {
                    if (interactionUI != null && (shopPanel == null || !shopPanel.activeSelf))
                        interactionUI.SetActive(true);
                }
                _isPlayerNearby = true;
                _playerCollector = hit.GetComponent<TrashCollector>();
                found = true;
                break;
            }
        }

        if (!found && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            if (shopPanel != null) shopPanel.SetActive(false);
            if (interactionUI != null) interactionUI.SetActive(false);
        }

        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.F))
            ToggleShop();
    }

    // ─────────────────────────────────────────
    // ─────────────────────────────────────────
    //  상점 열기/닫기
    // ─────────────────────────────────────────

    /// <summary>
    /// 상점을 열거나 닫습니다.
    /// 열릴 때: 현재 골드/아이템 정보를 UI에 갱신합니다.
    /// </summary>
    public void ToggleShop()
    {
        if (shopPanel == null)
        {
            Debug.LogError("[NPC] shopPanel이 연결되지 않았습니다!");
            return;
        }

        bool isOpening = !shopPanel.activeSelf;
        shopPanel.SetActive(isOpening);

        if (isOpening)
        {
            // 상점이 열릴 때: 안내 UI 숨기기 + 커서 표시 + 정보 갱신
            if (interactionUI != null) interactionUI.SetActive(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 상점 안에 현재 골드/씨앗 정보 표시
            RefreshShopInfo();

            Debug.Log("[NPC] 상점 열림");
        }
        else
        {
            // 상점이 닫힐 때: 안내 UI 다시 표시
            if (interactionUI != null) interactionUI.SetActive(true);
            Debug.Log("[NPC] 상점 닫힘");
        }
    }

    // ─────────────────────────────────────────
    //  버튼 연동 함수 (Inspector Button.OnClick에 등록)
    // ─────────────────────────────────────────

    /// <summary>
    /// [버튼 연결용] "쓰레기 판매" 버튼에 이 함수를 등록하세요.
    /// TrashCollector의 SellAllTrash()를 호출합니다.
    /// </summary>
    public void OnSellButtonClicked()
    {
        if (_playerCollector == null)
        {
            Debug.LogWarning("[NPC] 플레이어를 찾을 수 없습니다!");
            return;
        }

        // 판매 실행 (서버 연동은 TrashCollector 내부에서 처리)
        _playerCollector.SellAllTrash();

        // 판매 후 상점 정보 갱신
        Invoke(nameof(RefreshShopInfo), 0.5f); // 0.5초 뒤에 갱신 (서버 응답 대기)
    }

    /// <summary>
    /// [버튼 연결용] "씨앗 구매" 버튼에 이 함수를 등록하세요.
    /// </summary>
    public void OnBuySeedButtonClicked()
    {
        if (_playerCollector == null) return;

        _playerCollector.BuyItemFromButton("Seed");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    /// <summary>
    /// [버튼 연결용] "배터리 구매" 버튼에 이 함수를 등록하세요.
    /// </summary>
    public void OnBuyBatteryButtonClicked()
    {
        if (_playerCollector == null) return;

        _playerCollector.BuyItemFromButton("Battery");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    // ─────────────────────────────────────────
    //  충돌 감지 (Trigger)
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 NPC 범위에 들어오면 호출됩니다.
    /// Collider2D의 Is Trigger가 체크되어 있어야 합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 내 캐릭터(HasInputAuthority)만 저장 — 다른 플레이어가 덮어쓰는 문제 방지
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();

        if (interactionUI != null && (shopPanel == null || !shopPanel.activeSelf))
            interactionUI.SetActive(true);

        Debug.Log("[NPC] 플레이어 접근 감지 — F키로 상점을 열 수 있습니다.");
    }

    /// <summary>
    /// 플레이어가 NPC 범위에서 나가면 호출됩니다.
    /// 상점이 열려 있으면 자동으로 닫힙니다.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 내 캐릭터(HasInputAuthority)만 처리
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = false;
        _playerCollector = null;

        if (shopPanel != null) shopPanel.SetActive(false);
        if (interactionUI != null) interactionUI.SetActive(false);

        Debug.Log("[NPC] 플레이어 이탈 — 상점 닫힘");
    }

    // ─────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────

    /// <summary>
    /// 상점 UI에 현재 플레이어의 골드와 씨앗 수량을 표시합니다.
    /// shopInfoText가 없으면 건너뜁니다.
    /// </summary>
    void RefreshShopInfo()
    {
        if (shopInfoText == null || _playerCollector == null) return;

        int seedCount = _playerCollector.inventory.ContainsKey("Seed")
                           ? _playerCollector.inventory["Seed"] : 0;
        int batteryCount = _playerCollector.inventory.ContainsKey("Battery")
                           ? _playerCollector.inventory["Battery"] : 0;

        shopInfoText.text =
            $"💰 보유 골드: {_playerCollector.gold:N0}G\n" +
            $"🌱 씨앗: {seedCount}개 | 🔋 배터리: {batteryCount}개";
    }
}