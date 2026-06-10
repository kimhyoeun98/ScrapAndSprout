using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        // Inspector에 직접 연결 안 했으면: 떠도는 옛 ShopPanel을 모두 제거하고 새로 생성
        // (DecoScene 왕복 후 옛 패널의 버튼이 파괴된 NPCInteraction을 가리켜 클릭이 먹지 않는 문제 방지)
        if (shopPanel == null)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = canvas.transform.Find("ShopPanel");
                if (t != null) Destroy(t.gameObject);
            }
            BuildShopPanelMS();  // onClick이 이 NPCInteraction에 연결된 새 패널
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

    /// <summary>로컬 플레이어 TrashCollector 확보 (파괴/씬전환 후에도 재탐색)</summary>
    TrashCollector ResolveCollector()
    {
        // Unity의 == null 오버로드로 파괴된 객체도 null로 판정됨
        if (_playerCollector != null) return _playerCollector;

        foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (pm.HasInputAuthority)
            {
                _playerCollector = pm.GetComponent<TrashCollector>();
                break;
            }
        }
        return _playerCollector;
    }

    public void OnSellButtonClicked()
    {
        if (ResolveCollector() == null)
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

    public void OnBuyTreeButtonClicked()        => BuyDeco("나무");
    public void OnBuyBoxButtonClicked()         => BuyDeco("나무풍 상자");
    public void OnBuyChairButtonClicked()       => BuyDeco("나무풍 의자");
    public void OnBuyFenceButtonClicked()       => BuyDeco("나무풍 울타리");
    public void OnBuyVaseButtonClicked()        => BuyDeco("나무풍 꽃병");
    public void OnBuyTableButtonClicked()       => BuyDeco("나무풍 탁자");
    public void OnBuyFlowerFieldButtonClicked() => BuyDeco("나무풍 꽃밭");

    void BuyDeco(string itemName)
    {
        if (ResolveCollector() == null)
        {
            Debug.LogWarning($"[NPC] 플레이어를 찾을 수 없습니다! ({itemName})");
            return;
        }
        _playerCollector.BuyDecorationItem(itemName);
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
        shopInfoText.text = $"보유 골드:  {_playerCollector.gold:N0} G";
    }

    // ═══════════════════════════════════════════
    //  메이플스토리 스타일 상점 UI 빌더
    // ═══════════════════════════════════════════

    // ── 색상 팔레트 ──
    static readonly Color C_BG       = new Color(0.06f, 0.06f, 0.11f, 0.98f);
    static readonly Color C_HEADER   = new Color(0.03f, 0.03f, 0.07f, 1.00f);
    static readonly Color C_SLOT     = new Color(0.10f, 0.10f, 0.18f, 1.00f);
    static readonly Color C_SLOT_BD  = new Color(0.22f, 0.27f, 0.45f, 1.00f);
    static readonly Color C_TITLE    = new Color(0.92f, 0.88f, 0.76f, 1.00f);
    static readonly Color C_PRICE    = new Color(0.96f, 0.83f, 0.25f, 1.00f);
    static readonly Color C_GOLD_BAR = new Color(0.08f, 0.09f, 0.16f, 1.00f);
    static readonly Color C_BTN_BUY  = new Color(0.14f, 0.40f, 0.78f, 1.00f);
    static readonly Color C_BTN_SELL = new Color(0.65f, 0.18f, 0.14f, 1.00f);
    static readonly Color C_DIVIDER  = new Color(0.20f, 0.24f, 0.40f, 0.70f);

    // ── 아이템 데이터 ──
    struct ItemDef
    {
        public string name;
        public int    price;
        public Color  iconColor;
        public string iconChar;
        public System.Action onClick;
    }

    void BuildShopPanelMS()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // ── 아이템 목록 (나중에 여기에 추가만 하면 됨) ──
        ItemDef[] items =
        {
            new ItemDef { name="나무",   price=40,  iconColor=new Color(0.15f,0.60f,0.25f), iconChar="나", onClick=OnBuyTreeButtonClicked       },
            new ItemDef { name="나무풍 상자",   price=20,  iconColor=new Color(0.58f,0.38f,0.15f), iconChar="상", onClick=OnBuyBoxButtonClicked        },
            new ItemDef { name="나무풍 의자",   price=30,  iconColor=new Color(0.18f,0.46f,0.72f), iconChar="의", onClick=OnBuyChairButtonClicked      },
            new ItemDef { name="나무풍 울타리", price=50,  iconColor=new Color(0.40f,0.43f,0.47f), iconChar="울", onClick=OnBuyFenceButtonClicked      },
            new ItemDef { name="나무풍 꽃병",   price=60,  iconColor=new Color(0.55f,0.17f,0.70f), iconChar="꽃", onClick=OnBuyVaseButtonClicked       },
            new ItemDef { name="나무풍 탁자",   price=100, iconColor=new Color(0.37f,0.21f,0.09f), iconChar="탁", onClick=OnBuyTableButtonClicked      },
            new ItemDef { name="나무풍 꽃밭",   price=200, iconColor=new Color(0.88f,0.33f,0.56f), iconChar="밭", onClick=OnBuyFlowerFieldButtonClicked },
        };

        const float PANEL_W = 420f;
        const float PANEL_H = 520f;
        const float HEADER_H = 48f;
        const float GOLD_H   = 34f;
        const float BOTTOM_H = 52f;
        const float SCROLL_H = PANEL_H - HEADER_H - GOLD_H - 2f - BOTTOM_H;

        // ── 메인 패널 ──
        //var panel = MkImg(canvas.transform, "ShopPanel", C_BG)수정전;
        var panel = MkImg(canvas.GetComponent<RectTransform>(), "ShopPanel", C_BG);
        var pr    = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(-160f, 0f);
        pr.sizeDelta = new Vector2(PANEL_W, PANEL_H);
        shopPanel = panel.gameObject;

        // 외곽 테두리
        var bd = MkImg(pr, "Border", C_SLOT_BD);
        Stretch(bd, -1f); bd.transform.SetAsFirstSibling();

        // ── 헤더 ──
        var header = MkImg(pr, "Header", C_HEADER);
        TopStrip(header, HEADER_H);

        //MkTMP(header, "Title", "꾸미기 상점", 수정전
        //    new Vector2(0f,0f), new Vector2(1f,1f),
        //    new Vector2(16f,0f), new Vector2(-52f,0f),
        //    18f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);

        MkTMP(header.GetComponent<RectTransform>(), "Title", "꾸미기 상점",
        new Vector2(0f, 0f), new Vector2(1f, 1f),
        new Vector2(16f, 0f), new Vector2(-52f, 0f),
        18f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);

        // var closeBtn = MkButton(header, "X", new Color(0.55f,0.12f,0.10f), 수정전
        var closeBtn = MkButton(header.GetComponent<RectTransform>(), "X", new Color(0.55f, 0.12f, 0.10f),
        new Vector2(1f,0.5f), new Vector2(-6f,0f), new Vector2(36f,36f));
        closeBtn.onClick.AddListener(() => shopPanel.SetActive(false));

        // ── 골드 바 ──
        var goldBar = MkImg(pr, "GoldBar", C_GOLD_BAR);
        var gbr = goldBar.GetComponent<RectTransform>();
        gbr.anchorMin=new Vector2(0f,1f); gbr.anchorMax=new Vector2(1f,1f);
        gbr.pivot=new Vector2(0.5f,1f);
        gbr.anchoredPosition=new Vector2(0f,-HEADER_H);
        gbr.sizeDelta=new Vector2(0f,GOLD_H);

        //shopInfoText = MkTMP(goldBar, "ShopInfoText", "보유 골드:  0 G", 수정전
        //    Vector2.zero, Vector2.one,
        //    new Vector2(14f,0f), new Vector2(-14f,0f),
        //    14f, FontStyles.Bold, C_PRICE, TextAlignmentOptions.Left);

        shopInfoText = MkTMP(goldBar.GetComponent<RectTransform>(), "ShopInfoText", "보유 골드:  0 G",
        Vector2.zero, Vector2.one,
        new Vector2(14f, 0f), new Vector2(-14f, 0f),
        14f, FontStyles.Bold, C_PRICE, TextAlignmentOptions.Left);

        // ── 구분선 ──
        MkDivider(pr, -(HEADER_H + GOLD_H));

        // ── 스크롤 뷰 (아이템 목록) ──
        var scrollObj = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(pr, false);
        var scrollRt = scrollObj.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(0f, BOTTOM_H);
        scrollRt.offsetMax = new Vector2(0f, -(HEADER_H + GOLD_H + 2f));

        var scrollComp = scrollObj.GetComponent<ScrollRect>();
        scrollComp.horizontal = false;
        scrollComp.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // Viewport
        var vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        vp.transform.SetParent(scrollObj.transform, false);
        var vpRt = vp.GetComponent<RectTransform>();
        vpRt.anchorMin=Vector2.zero; vpRt.anchorMax=Vector2.one;
        vpRt.offsetMin=vpRt.offsetMax=Vector2.zero;
        vp.GetComponent<Image>().color = new Color(0f,0f,0f,0.01f);
        vp.GetComponent<Mask>().showMaskGraphic = false;
        scrollComp.viewport = vpRt;

        // Content (아이템이 쌓이는 곳)
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vp.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f,1f);
        contentRt.anchorMax = new Vector2(1f,1f);
        contentRt.pivot     = new Vector2(0.5f,1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;
        scrollComp.content = contentRt;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5f;
        vlg.padding = new RectOffset(8, 8, 6, 6);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 아이템 카드 생성 (스크롤 내부) ──
        foreach (var item in items)
            BuildItemCard(contentRt, item);

        // ── 구분선 ──
        MkDivider(pr, -( PANEL_H - BOTTOM_H));

        // ── 하단 판매 버튼 ──
        var sellBtn = MkButton(pr, "쓰레기 전부 판매", C_BTN_SELL,
            new Vector2(0.5f,0f), new Vector2(0f, 10f), new Vector2(220f,36f));
        sellBtn.onClick.AddListener(OnSellButtonClicked);

        panel.gameObject.SetActive(false);
    }

    // 카드 1개 (리스트 행 스타일, 너비 100% 자동)
    void BuildItemCard(RectTransform parent, ItemDef item)
    {
        const float CARD_H  = 72f;
        const float ICON_SZ = 52f;

        var card = MkImg(parent, $"Card_{item.name}", C_SLOT);
        var cr   = card.GetComponent<RectTransform>();
        cr.sizeDelta = new Vector2(0f, CARD_H);

        // 하단 구분선 (카드 구분)
        var sep = MkImg(cr, "Sep", new Color(0.18f,0.22f,0.36f,0.6f));
        var sr  = sep.GetComponent<RectTransform>();
        sr.anchorMin=new Vector2(0f,0f); sr.anchorMax=new Vector2(1f,0f);
        sr.pivot=new Vector2(0.5f,0f); sr.anchoredPosition=Vector2.zero;
        sr.sizeDelta=new Vector2(-8f,1f);

        // 아이콘
        var iconBG = MkImg(cr, "Icon", item.iconColor);
        var ibr    = iconBG.GetComponent<RectTransform>();
        ibr.anchorMin=ibr.anchorMax=new Vector2(0f,0.5f);
        ibr.pivot=new Vector2(0f,0.5f);
        ibr.anchoredPosition=new Vector2(10f,0f);
        ibr.sizeDelta=new Vector2(ICON_SZ,ICON_SZ);

        //MkTMP(iconBG, "T", item.iconChar수정전,
        MkTMP(iconBG.GetComponent<RectTransform>(), "T", item.iconChar,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            24f, FontStyles.Bold, new Color(1f,1f,1f,0.92f), TextAlignmentOptions.Center);

        // 이름 + 가격
        float tx = 10f + ICON_SZ + 10f;
        MkTMP(cr, "Name", item.name,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(tx,-6f), new Vector2(-80f,-26f),
            15f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);

        MkTMP(cr, "Price", $"{item.price:N0} G",
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(tx,-30f), new Vector2(-80f,-50f),
            13f, FontStyles.Normal, C_PRICE, TextAlignmentOptions.Left);

        // 구매 버튼
        var btn = MkButton(cr, "구매", C_BTN_BUY,
            new Vector2(1f,0.5f), new Vector2(-10f,0f), new Vector2(62f,32f));
        System.Action cb = item.onClick;
        btn.onClick.AddListener(() => cb?.Invoke());
    }

    // ─────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────

    static Image MkImg(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    static void Stretch(Image img, float offset)
    {
        var r = img.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(offset, offset);
        r.offsetMax = new Vector2(-offset, -offset);
    }

    static void TopStrip(Image img, float h)
    {
        var r = img.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f,1f); r.anchorMax = new Vector2(1f,1f);
        r.pivot = new Vector2(0.5f,1f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0f,h);
    }

    static void MkDivider(RectTransform parent, float y)
    {
        var img = MkImg(parent, "Div", C_DIVIDER);
        var r   = img.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f,1f); r.anchorMax = new Vector2(1f,1f);
        r.pivot = new Vector2(0.5f,1f);
        r.anchoredPosition = new Vector2(0f,y); r.sizeDelta = new Vector2(-16f,1f);
    }

    static TextMeshProUGUI MkTMP(RectTransform parent, string name, string text,
        Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax,
        float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin=aMin; r.anchorMax=aMax; r.offsetMin=offMin; r.offsetMax=offMax;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text=text; tmp.fontSize=size; tmp.fontStyle=style;
        tmp.color=color; tmp.alignment=align; tmp.raycastTarget=false;
        return tmp;
    }

    static Button MkButton(RectTransform parent, string label, Color bg,
        Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go  = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = pos; r.sizeDelta = size;
        go.GetComponent<Image>().color = bg;

        var txt = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var tr  = txt.GetComponent<RectTransform>();
        tr.anchorMin=Vector2.zero; tr.anchorMax=Vector2.one;
        tr.offsetMin=tr.offsetMax=Vector2.zero;
        var tmp = txt.GetComponent<TextMeshProUGUI>();
        tmp.text=label; tmp.fontSize=13f; tmp.fontStyle=FontStyles.Bold;
        tmp.alignment=TextAlignmentOptions.Center;
        tmp.color=new Color(0.95f,0.92f,0.85f);

        return go.GetComponent<Button>();
    }
}