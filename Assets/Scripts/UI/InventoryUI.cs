using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;

/// <summary>
/// 인벤토리 UI 자동 생성 및 관리
///
/// [사용법]
/// TrashZoneScene Canvas 오브젝트에 이 스크립트를 Add Component
/// Inspector에서 별도 연결 없이 자동으로 UI를 생성합니다.
///
/// [단축키]
/// I 키: 전체 인벤토리 패널 열기/닫기
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ── 핫바 설정 ──────────────────────────────
    [Header("핫바")]
    [Tooltip("한 번에 핫바에 표시할 최대 아이템 종류 수")]
    public int hotbarMaxSlots = 6;

    // ── 내부 참조 ──────────────────────────────
    private TrashCollector _collector;

    private GameObject _hotbarRoot;
    private GameObject _inventoryPanelRoot;
    private Transform _inventoryListParent;

    private readonly List<GameObject> _hotbarSlots  = new();
    private readonly List<GameObject> _inventoryRows = new();

    private Dictionary<string, int> _lastInventory = new();
    private bool _inventoryOpen = false;

    // 꾸미기 아이템 구분용
    private static readonly HashSet<string> _decoNames = new()
    {
        "나무", "나무풍 상자", "나무풍 의자", "나무풍 울타리", "나무풍 꽃병", "나무풍 탁자", "나무풍 꽃밭"
    };

    // ── 색상 ──────────────────────────────────
    private static readonly Color ColorHotbarBg    = new(0.1f, 0.1f, 0.1f, 0.75f);
    private static readonly Color ColorSlotBg      = new(0.2f, 0.2f, 0.2f, 0.9f);
    private static readonly Color ColorDecoSlotBg  = new(0.1f, 0.3f, 0.1f, 0.9f);
    private static readonly Color ColorPanelBg     = new(0.05f, 0.05f, 0.05f, 0.92f);
    private static readonly Color ColorSectionTitle = new(1f, 0.85f, 0.3f, 1f);

    // ═══════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════

    void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        // DontDestroyOnLoad 제외하고 현재 씬 Canvas만 탐색
        if (canvas == null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.gameObject.scene.name == sceneName)
                {
                    canvas = c;
                    break;
                }
            }
        }

        if (canvas == null) { Debug.LogError("[InventoryUI] Canvas를 찾을 수 없습니다!"); return; }

        BuildHotbar(canvas.transform);
        BuildInventoryPanel(canvas.transform);
    }

    void Update()
    {
        // 로컬 플레이어 탐색 (스폰 타이밍 지연 대응)
        if (_collector == null)
        {
            foreach (var tc in FindObjectsByType<TrashCollector>(FindObjectsSortMode.None))
            {
                if (tc.HasInputAuthority) { _collector = tc; break; }
            }
            return;
        }

        // I 키: 인벤토리 토글
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();

        // 인벤토리 변경 감지 → 핫바 갱신
        if (HasInventoryChanged())
        {
            _lastInventory = new Dictionary<string, int>(_collector.inventory);
            RefreshHotbar();
            if (_inventoryOpen) RefreshInventoryPanel();
        }
    }

    // ═══════════════════════════════════════════
    //  UI 생성 (Start에서 1회)
    // ═══════════════════════════════════════════

    void BuildHotbar(Transform canvasRoot)
    {
        // 루트 패널
        _hotbarRoot = CreatePanel(canvasRoot, "Hotbar",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),   // 하단 중앙 앵커
            new Vector2(0f, 10f),                            // 하단에서 10px 위
            new Vector2(520f, 72f));
        SetColor(_hotbarRoot, ColorHotbarBg);

        // 가로 레이아웃
        var hlg = _hotbarRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.padding = new RectOffset(8, 8, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth  = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
    }

    void BuildInventoryPanel(Transform canvasRoot)
    {
        // 어두운 배경 패널 (화면 중앙)
        _inventoryPanelRoot = CreatePanel(canvasRoot, "InventoryPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(340f, 440f));
        SetColor(_inventoryPanelRoot, ColorPanelBg);
        _inventoryPanelRoot.SetActive(false);

        var rect = _inventoryPanelRoot.GetComponent<RectTransform>();

        // 제목
        var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(_inventoryPanelRoot.transform, false);
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(10f, -50f);
        titleRect.offsetMax = new Vector2(-10f, -10f);
        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "인벤토리";
        titleTmp.fontSize = 20f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;

        // 닫기 버튼
        BuildCloseButton(_inventoryPanelRoot.transform);

        // 스크롤 뷰
        var scrollObj = new GameObject("Scroll", typeof(RectTransform));
        scrollObj.transform.SetParent(_inventoryPanelRoot.transform, false);
        var scrollRect2 = scrollObj.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0f, 0f);
        scrollRect2.anchorMax = new Vector2(1f, 1f);
        scrollRect2.offsetMin = new Vector2(10f, 10f);
        scrollRect2.offsetMax = new Vector2(-10f, -55f);

        var scrollComp = scrollObj.AddComponent<ScrollRect>();
        scrollComp.horizontal = false;

        // Viewport
        var vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
        vpObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vpObj.GetComponent<Mask>().showMaskGraphic = false;
        scrollComp.viewport = vpRect;

        // Content (아이템 목록이 여기에 추가됨)
        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        var contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        scrollComp.content = contentRect;
        _inventoryListParent = contentRect;

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 4, 4);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void BuildCloseButton(Transform parent)
    {
        var btnObj = new GameObject("CloseButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        var r = btnObj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(1f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot     = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -8f);
        r.sizeDelta = new Vector2(36f, 36f);
        btnObj.GetComponent<Image>().color = new Color(0.7f, 0.2f, 0.2f, 0.9f);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(btnObj.transform, false);
        var lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var lTmp = labelObj.GetComponent<TextMeshProUGUI>();
        lTmp.text = "✕";
        lTmp.fontSize = 18f;
        lTmp.alignment = TextAlignmentOptions.Center;
        lTmp.color = Color.white;

        btnObj.GetComponent<Button>().onClick.AddListener(() =>
        {
            _inventoryOpen = false;
            _inventoryPanelRoot.SetActive(false);
        });
    }

    // ═══════════════════════════════════════════
    //  핫바 갱신
    // ═══════════════════════════════════════════

    void RefreshHotbar()
    {
        foreach (var s in _hotbarSlots) Destroy(s);
        _hotbarSlots.Clear();

        if (_hotbarRoot == null || _collector == null) return;

        int count = 0;
        foreach (var kv in _collector.inventory)
        {
            if (kv.Value <= 0) continue;
            if (count >= hotbarMaxSlots) break;

            bool isDeco = _decoNames.Contains(kv.Key);
            var slot = BuildHotbarSlot(_hotbarRoot.transform, kv.Key, kv.Value, isDeco);
            _hotbarSlots.Add(slot);
            count++;
        }
    }

    GameObject BuildHotbarSlot(Transform parent, string itemName, int count, bool isDeco)
    {
        var slotObj = new GameObject($"HSlot_{itemName}",
            typeof(RectTransform), typeof(Image));
        slotObj.transform.SetParent(parent, false);
        var r = slotObj.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(78f, 58f);
        slotObj.GetComponent<Image>().color = isDeco ? ColorDecoSlotBg : ColorSlotBg;

        // 아이템 이름
        AddTMP(slotObj.transform, "ItemName", itemName,
            new Vector2(0f, 0.45f), new Vector2(1f, 1f), 11f, Color.white);

        // 수량 (우하단)
        AddTMP(slotObj.transform, "Count", $"x{count}",
            new Vector2(0f, 0f), new Vector2(1f, 0.5f), 13f,
            isDeco ? new Color(0.5f, 1f, 0.5f) : Color.yellow);

        return slotObj;
    }

    // ═══════════════════════════════════════════
    //  인벤토리 패널 갱신
    // ═══════════════════════════════════════════

    void ToggleInventory()
    {
        _inventoryOpen = !_inventoryOpen;
        _inventoryPanelRoot.SetActive(_inventoryOpen);
        if (_inventoryOpen) RefreshInventoryPanel();
    }

    /// <summary>외부(배치 모드 진입 등)에서 인벤토리를 강제로 닫을 때 호출</summary>
    public void CloseInventory()
    {
        _inventoryOpen = false;
        if (_inventoryPanelRoot != null) _inventoryPanelRoot.SetActive(false);
    }

    void RefreshInventoryPanel()
    {
        foreach (var r in _inventoryRows) Destroy(r);
        _inventoryRows.Clear();

        if (_inventoryListParent == null || _collector == null) return;

        // 일반 쓰레기 아이템
        bool hasTrash = false;
        foreach (var kv in _collector.inventory)
        {
            if (_decoNames.Contains(kv.Key) || kv.Value <= 0) continue;
            if (!hasTrash)
            {
                _inventoryRows.Add(BuildSectionTitle("── 쓰레기 아이템 ──"));
                hasTrash = true;
            }
            _inventoryRows.Add(BuildInventoryRow(kv.Key, kv.Value, false));
        }

        // 꾸미기 아이템
        bool hasDeco = false;
        foreach (var kv in _collector.inventory)
        {
            if (!_decoNames.Contains(kv.Key) || kv.Value <= 0) continue;
            if (!hasDeco)
            {
                _inventoryRows.Add(BuildSectionTitle("── 꾸미기 아이템 ──"));
                hasDeco = true;
            }
            _inventoryRows.Add(BuildInventoryRow(kv.Key, kv.Value, true));
        }

        if (!hasTrash && !hasDeco)
            _inventoryRows.Add(BuildEmptyRow());
    }

    GameObject BuildSectionTitle(string text)
    {
        var obj = new GameObject("Section", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(_inventoryListParent, false);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
        var tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = ColorSectionTitle;
        return obj;
    }

    GameObject BuildInventoryRow(string itemName, int count, bool isDeco)
    {
        var rowObj = new GameObject($"Row_{itemName}",
            typeof(RectTransform), typeof(Image));
        rowObj.transform.SetParent(_inventoryListParent, false);
        rowObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
        rowObj.GetComponent<Image>().color = isDeco
            ? new Color(0.1f, 0.25f, 0.1f, 0.8f)
            : new Color(0.18f, 0.18f, 0.18f, 0.8f);

        // 아이템 이름 (왼쪽)
        AddTMP(rowObj.transform, "Name", itemName,
            new Vector2(0f, 0f), new Vector2(0.7f, 1f), 14f, Color.white,
            TextAlignmentOptions.MidlineLeft, new RectOffset(8, 0, 0, 0));

        // 수량 (오른쪽)
        AddTMP(rowObj.transform, "Count", $"x{count}",
            new Vector2(0.7f, 0f), new Vector2(1f, 1f), 14f,
            isDeco ? new Color(0.4f, 1f, 0.4f) : Color.yellow,
            TextAlignmentOptions.MidlineRight, new RectOffset(0, 8, 0, 0));

        // 꾸미기 아이템: 클릭 시 배치 모드 진입
        if (isDeco)
        {
            var btn = rowObj.AddComponent<Button>();
            // 호버 색상 (약간 밝게)
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.5f, 0.2f, 1f);
            colors.pressedColor     = new Color(0.3f, 0.7f, 0.3f, 1f);
            btn.colors = colors;

            string captured = itemName;
            btn.onClick.AddListener(() =>
            {
                // 인벤토리 닫기 (InventoryUI 패널 + UIManager 패널 둘 다)
                _inventoryOpen = false;
                _inventoryPanelRoot.SetActive(false);
                UIManager.Instance?.ForceCloseInventory();

                // 배치 모드 시작
                DecoPlacementController.Instance?.StartPlacement(captured, _collector);
            });
        }

        return rowObj;
    }

    GameObject BuildEmptyRow()
    {
        var obj = new GameObject("Empty", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(_inventoryListParent, false);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
        var tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = "아이템 없음";
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.6f, 0.6f, 0.6f);
        return obj;
    }

    // ═══════════════════════════════════════════
    //  인벤토리 변경 감지
    // ═══════════════════════════════════════════

    bool HasInventoryChanged()
    {
        if (_collector == null) return false;
        if (_collector.inventory.Count != _lastInventory.Count) return true;
        foreach (var kv in _collector.inventory)
            if (!_lastInventory.TryGetValue(kv.Key, out int v) || v != kv.Value)
                return true;
        return false;
    }

    // ═══════════════════════════════════════════
    //  UI 헬퍼
    // ═══════════════════════════════════════════

    static GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = size;
        return obj;
    }

    static void SetColor(GameObject obj, Color color)
        => obj.GetComponent<Image>().color = color;

    static void AddTMP(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        RectOffset padding = null)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = padding != null ? new Vector2(padding.left, padding.bottom) : Vector2.zero;
        r.offsetMax = padding != null ? new Vector2(-padding.right, -padding.top) : Vector2.zero;
        var tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
    }
}
