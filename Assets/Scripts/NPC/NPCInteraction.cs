using Fusion;
using TMPro;
using UnityEngine;

public class NPCInteraction : NetworkBehaviour
{
    [Header("UI 연결 (Inspector에서 드래그)")]
    public GameObject interactionUI;
    public GameObject shopPanel;
    public TextMeshProUGUI shopInfoText;

    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;

    void Start()
    {
        // 비활성화된 오브젝트도 찾을 수 있도록 FindObjectsByType 활용
        if (shopPanel == null)
        {
            // 모든 Canvas 하위에서 이름으로 탐색
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = canvas.transform.Find("ShopPanel");
                if (t != null) { shopPanel = t.gameObject; break; }
            }
            Debug.Log($"[NPC] ShopPanel 자동 연결: {(shopPanel != null ? shopPanel.name : "실패")}");
        }

        if (shopInfoText == null && shopPanel != null)
        {
            var t = shopPanel.transform.Find("ShopInfoText");
            if (t != null) shopInfoText = t.GetComponent<TextMeshProUGUI>();
        }

        if (interactionUI == null)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = canvas.transform.Find("NPC_InteractionUI");
                if (t != null) { interactionUI = t.gameObject; break; }
            }
        }

        // 시작 시 패널 숨기기
        if (shopPanel != null) shopPanel.SetActive(false);
        if (interactionUI != null) interactionUI.SetActive(false);
    }

    void Update()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f);

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            // ✅ 비활성화된 오브젝트 무시!
            if (!hit.gameObject.activeInHierarchy) continue;

            var pm = hit.GetComponent<PlayerMovement>();
            if (pm == null || !pm.HasInputAuthority) continue;

            // ✅ TrashCollector도 HasInputAuthority 체크!
            var collector = hit.GetComponent<TrashCollector>();
            if (collector == null || !collector.HasInputAuthority) continue;

            if (!_isPlayerNearby)
            {
                if (interactionUI != null && (shopPanel == null || !shopPanel.activeSelf))
                    interactionUI.SetActive(true);
            }
            _isPlayerNearby = true;
            _playerCollector = collector;
            found = true;
            break;
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

    public void ToggleShop()
    {
        if (shopPanel == null)
        {
            Debug.LogError("[NPC] shopPanel이 연결되지 않았습니다!");
            return;
        }

        bool isOpening = !shopPanel.activeSelf;


        if (_playerCollector != null)
        {

        }


        shopPanel.SetActive(isOpening);

        if (isOpening)
        {
            if (interactionUI != null) interactionUI.SetActive(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;


            RefreshShopInfo();


            Debug.Log("[NPC] 상점 열림");
        }
        else
        {
            if (interactionUI != null) interactionUI.SetActive(true);
            Debug.Log("[NPC] 상점 닫힘");
        }
    }

    public void OnSellButtonClicked()
    {
        // 플레이어 재탐색 — HasInputAuthority 있는 PlayerMovement 찾기
        if (_playerCollector == null)
        {
            foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (pm.HasInputAuthority)
                {
                    _playerCollector = pm.GetComponent<TrashCollector>();
                    break;
                }
            }
        }

        if (_playerCollector == null)
        {
            Debug.LogWarning("[NPC] 플레이어를 찾을 수 없습니다!");
            return;
        }

        _playerCollector.SellAllTrash();
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }




    // ─────────────────────────────────────────
    //  꾸미기 아이템 구매 버튼 (기획 개편 반영)
    //  ShopPanel 각 버튼 OnClick()에 연결하세요.
    // ─────────────────────────────────────────

    public void OnBuyTreeButtonClicked()
    {
        if (_playerCollector == null)
        {
            foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (pm.HasInputAuthority)
                {
                    _playerCollector = pm.GetComponent<TrashCollector>();
                    break;
                }
            }
        }
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("나무");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyBoxButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("상자");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyChairButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("의자");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyFenceButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("울타리");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyVaseButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("꽃병");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyTableButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("탁자");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    public void OnBuyFlowerFieldButtonClicked()
    {
        if (_playerCollector == null) return;
        _playerCollector.BuyDecorationItem("꽃밭");
        Invoke(nameof(RefreshShopInfo), 0.5f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!other.gameObject.activeInHierarchy) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();

        if (interactionUI != null && (shopPanel == null || !shopPanel.activeSelf))
            interactionUI.SetActive(true);

        Debug.Log("[NPC] 플레이어 접근 감지 — F키로 상점을 열 수 있습니다.");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = false;
        _playerCollector = null;

        if (shopPanel != null) shopPanel.SetActive(false);
        if (interactionUI != null) interactionUI.SetActive(false);

        Debug.Log("[NPC] 플레이어 이탈 — 상점 닫힘");
    }

    void RefreshShopInfo()
    {
        if (shopInfoText == null || _playerCollector == null) return;

        // 꾸미기 점수와 보유 골드 표시
        shopInfoText.text =
            $"보유 골드: {_playerCollector.gold:N0}G\n" +
            $"캐릭터: {_playerCollector.characterType}\n";
    }
}