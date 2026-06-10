using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 꾸미기 아이템 배치 시스템 (세이프 스테이지 전용)
///
/// [씬 설정]
/// SafeZone 안에 빈 GameObject를 만들고 이 스크립트를 Add Component.
/// placementPanel은 연결하지 않아도 됩니다 — Awake에서 자동 생성합니다.
///
/// [연결 흐름]
/// 플레이어 F키 → DecorationPlacer → 배치 UI (보유 아이템 버튼) → PlaceItem
/// </summary>
public class DecorationPlacer : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  설정
    // ─────────────────────────────────────────

    [Header("── 배치 설정 ──")]
    [Tooltip("배치 UI 패널 (비워두면 자동 생성)")]
    public GameObject placementPanel;

    [Tooltip("배치 가능 범위 반경")]
    public float interactionRadius = 5f;

    [Tooltip("배치 가능 위치들 (선택)")]
    public Transform[] decoPlacePoints;

    // ─────────────────────────────────────────
    //  꾸미기 아이템 목록 (자동 로드)
    // ─────────────────────────────────────────

    private static List<string> _decoItems = new();

    // 아이템별 기본 가격 (없으면 기본값 50G)
    private static readonly Dictionary<string, int> _decoPrices = new()
    {
        { "나무",    40 }, { "나무풍 상자",   20 }, { "나무풍 의자",   30 },
        { "나무풍 울타리",  50 }, { "나무풍 꽃병",   60 }, { "나무풍 탁자",   100 },
        { "나무풍 꽃밭",   200 }
    };

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;

    private readonly Dictionary<string, Button>          _itemButtons = new();
    private readonly Dictionary<string, TextMeshProUGUI> _countTexts  = new();
    private GameObject _guideUI;

    // ─────────────────────────────────────────
    //  Awake — placementPanel 자동 생성
    // ─────────────────────────────────────────

    // 구역명 텍스트 (패널 상단)
    private TMPro.TextMeshProUGUI _zoneNameText;
    // 그리드 스냅 단위
    private const float GRID = 0.5f;

    void Start()
    {
        Debug.Log("[배치] DecorationPlacer.Start() 호출됨!");

        // Resources/deco에서 모든 프리팹 자동 로드
        LoadAllDecoItems();

        // PlayerZoneManager 자동 생성
        if (PlayerZoneManager.Instance == null)
        {
            var go = new GameObject("PlayerZoneManager");
            go.AddComponent<PlayerZoneManager>();
        }

        if (placementPanel == null)
        {
            BuildPlacementPanel();
            Debug.Log("[배치] ✓ 배치 패널 자동 생성됨");
        }
        else
        {
            Debug.Log("[배치] ✓ 배치 패널이 이미 있음");
        }

        // 배치 패널은 처음엔 닫혀있음 (F키로 열 수 있음)
        if (placementPanel != null)
        {
            placementPanel.SetActive(false);
            Debug.Log("[배치] ✓ 배치 패널 준비 완료");
        }
        else
        {
            Debug.LogError("[배치] ✗ placementPanel이 null입니다!");
        }
    }

    void LoadAllDecoItems()
    {
        _decoItems.Clear();

        // Resources/deco에서 모든 프리팹 로드
        var prefabs = Resources.LoadAll<GameObject>("deco");
        Debug.Log($"[배치] 로드된 프리팹: {prefabs.Length}개");

        foreach (var prefab in prefabs)
        {
            string name = prefab.name.Replace("_0", ""); // "_0" 제거
            if (!_decoItems.Contains(name))
            {
                _decoItems.Add(name);
                Debug.Log($"[배치] ✓ 추가됨: {name}");
            }
        }

        Debug.Log($"[배치] 총 {_decoItems.Count}개의 아이템 로드됨");
    }

    void BuildPlacementPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 패널 루트
        var panel = new GameObject("DecoPlacementPanel",
            typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(300f, 400f);
        panelRect.anchoredPosition = new Vector2(200f, 0f); // HUD와 안 겹치게 우측
        panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.04f, 0.95f);

        // 상단 액센트 바
        var accentBar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        accentBar.transform.SetParent(panel.transform, false);
        var abRect = accentBar.GetComponent<RectTransform>();
        abRect.anchorMin = new Vector2(0f, 1f); abRect.anchorMax = new Vector2(1f, 1f);
        abRect.pivot = new Vector2(0.5f, 1f); abRect.anchoredPosition = Vector2.zero;
        abRect.sizeDelta = new Vector2(0f, 4f);
        accentBar.GetComponent<Image>().color = new Color(0.30f, 0.58f, 0.22f, 1f);

        // 제목
        AddLabel(panel.transform, "Title", "꾸미기 배치",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -44f), new Vector2(0f, -6f),
            17f, FontStyles.Bold, new Color(0.85f, 0.74f, 0.28f));

        // 구역 이름 표시 (로컬 플레이어 구역)
        var zoneObj = new GameObject("ZoneNameText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        zoneObj.transform.SetParent(panel.transform, false);
        var zr = zoneObj.GetComponent<RectTransform>();
        zr.anchorMin = new Vector2(0f, 1f); zr.anchorMax = new Vector2(1f, 1f);
        zr.offsetMin = new Vector2(0f, -64f); zr.offsetMax = new Vector2(0f, -44f);
        _zoneNameText = zoneObj.GetComponent<TMPro.TextMeshProUGUI>();
        _zoneNameText.fontSize = 11f;
        _zoneNameText.fontStyle = FontStyles.Normal;
        _zoneNameText.alignment = TextAlignmentOptions.Center;
        _zoneNameText.color = new Color(0.65f, 0.85f, 0.55f);
        _zoneNameText.raycastTarget = false;
        UpdateZoneNameText();

        // 닫기 버튼
        BuildCloseButton(panel.transform, panel);

        // 안내 텍스트
        AddLabel(panel.transform, "Guide", "[F] 키로 열기/닫기",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -82f), new Vector2(0f, -64f),
            10f, FontStyles.Normal, new Color(0.6f, 0.7f, 0.6f, 0.7f));

        // 스크롤 뷰 (아이템 버튼 목록)
        var scrollContent = BuildScrollArea(panel.transform);

        // 아이템별 버튼 생성
        foreach (var itemName in _decoItems)
            BuildItemButton(scrollContent, itemName);

        panel.SetActive(false);
        placementPanel = panel;

        // F키 가이드 UI (플레이어 접근 시 표시)
        BuildGuideUI(canvas.transform);
    }

    void BuildGuideUI(Transform canvasRoot)
    {
        _guideUI = new GameObject("DecoGuideUI",
            typeof(RectTransform), typeof(Image));
        _guideUI.transform.SetParent(canvasRoot, false);
        var r = _guideUI.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, 90f);
        r.sizeDelta = new Vector2(220f, 36f);
        _guideUI.GetComponent<Image>().color = new Color(0.1f, 0.4f, 0.1f, 0.85f);

        AddLabel(_guideUI.transform, "Text", "[F] 꾸미기 배치",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            14f, FontStyles.Bold, Color.white);

        _guideUI.SetActive(false);
    }

    Transform BuildScrollArea(Transform parent)
    {
        var scrollObj = new GameObject("Scroll", typeof(RectTransform));
        scrollObj.transform.SetParent(parent, false);
        var scrollRect2 = scrollObj.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0f, 0f);
        scrollRect2.anchorMax = new Vector2(1f, 1f);
        scrollRect2.offsetMin = new Vector2(8f, 8f);
        scrollRect2.offsetMax = new Vector2(-8f, -92f);

        var scrollComp = scrollObj.AddComponent<ScrollRect>();
        scrollComp.horizontal = false;

        var vpObj = new GameObject("Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
        vpObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vpObj.GetComponent<Mask>().showMaskGraphic = false;
        scrollComp.viewport = vpRect;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        var contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        scrollComp.content = contentRect;

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;

        contentObj.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        return contentRect;
    }

    void BuildItemButton(Transform parent, string itemName)
    {
        int score = _decoPrices.TryGetValue(itemName, out int s) ? s : 0;

        var rowObj = new GameObject($"Row_{itemName}",
            typeof(RectTransform), typeof(Image));
        rowObj.transform.SetParent(parent, false);
        rowObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 46f);
        rowObj.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.12f, 0.85f);

        // 아이템 이름 + 점수 (왼쪽)
        AddLabel(rowObj.transform, "Info", $"{itemName}  (+{score}pt)",
            new Vector2(0f, 0f), new Vector2(0.65f, 1f),
            new Vector2(8f, 0f), Vector2.zero,
            13f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.MidlineLeft);

        // 보유 수량 (중앙)
        var countObj = new GameObject("Count",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        countObj.transform.SetParent(rowObj.transform, false);
        var cRect = countObj.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.65f, 0f);
        cRect.anchorMax = new Vector2(0.8f, 1f);
        cRect.offsetMin = cRect.offsetMax = Vector2.zero;
        var cTmp = countObj.GetComponent<TextMeshProUGUI>();
        cTmp.text = "x0";
        cTmp.fontSize = 13f;
        cTmp.alignment = TextAlignmentOptions.Center;
        cTmp.color = new Color(0.5f, 1f, 0.5f);
        _countTexts[itemName] = cTmp;

        // 배치 버튼 (오른쪽)
        var btnObj = new GameObject("PlaceBtn",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(rowObj.transform, false);
        var bRect = btnObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.8f, 0.1f);
        bRect.anchorMax = new Vector2(1f, 0.9f);
        bRect.offsetMin = new Vector2(0f, 0f);
        bRect.offsetMax = new Vector2(-6f, 0f);
        btnObj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 0.9f);

        AddLabel(btnObj.transform, "Label", "배치",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            13f, FontStyles.Bold, Color.white);

        var btn = btnObj.GetComponent<Button>();
        string captured = itemName;
        btn.onClick.AddListener(() => PlaceItem(captured));
        _itemButtons[itemName] = btn;
    }

    void BuildCloseButton(Transform parent, GameObject panel)
    {
        var btnObj = new GameObject("CloseBtn",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        var r = btnObj.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-6f, -6f);
        r.sizeDelta = new Vector2(32f, 32f);
        btnObj.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 0.9f);

        AddLabel(btnObj.transform, "X", "✕",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            16f, FontStyles.Bold, Color.white);

        btnObj.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));
    }

    // ─────────────────────────────────────────
    //  Update — 플레이어 감지 + F키 입력
    // ─────────────────────────────────────────

    void Update()
    {
        DetectNearbyPlayer();

        // Q키: 배치 UI 토글 (플레이어 없이도 작동)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log("[배치] Q키 입력 감지됨!");

            // DecoScene에서는 플레이어가 없을 수 있음 → UI만 열기
            if (placementPanel != null)
            {
                TogglePlacementUI();
                Debug.Log("[배치] ✓ 배치 UI 토글됨 - Panel active: " + placementPanel.activeSelf);
            }
            else
            {
                Debug.LogError("[배치] ✗ placementPanel이 null입니다!");
            }
        }
    }

    // ─────────────────────────────────────────
    //  플레이어 근접 감지
    // ─────────────────────────────────────────

    void DetectNearbyPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            var pm = hit.GetComponent<PlayerMovement>();
            if (pm == null || !pm.HasInputAuthority) continue;

            if (!_isPlayerNearby && _guideUI != null)
                _guideUI.SetActive(true);

            _isPlayerNearby = true;
            _playerCollector = hit.GetComponent<TrashCollector>();
            found = true;
            break;
        }

        if (!found && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            ClosePlacementUI();
            if (_guideUI != null) _guideUI.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    //  배치 UI 열기/닫기
    // ─────────────────────────────────────────

    void TogglePlacementUI()
    {
        if (placementPanel == null) return;

        bool isOpening = !placementPanel.activeSelf;
        placementPanel.SetActive(isOpening);

        if (isOpening)
        {
            UpdateZoneNameText();
            RefreshItemButtons();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (_guideUI != null) _guideUI.SetActive(false);
        }
    }

    /// <summary>로컬 플레이어 구역 내 그리드 스냅 위치 계산</summary>
    Vector3 CalcPlacePosition()
    {
        Vector3 playerPos = _playerCollector.transform.position;

        // 플레이어 X를 그리드 스냅
        float snappedX = Mathf.Round(playerPos.x / GRID) * GRID;

        // PlayerZoneManager가 있으면 구역 범위로 클램프
        if (PlayerZoneManager.Instance != null)
        {
            int slot = PlayerZoneManager.Instance.GetLocalSlot();
            var (minX, maxX) = PlayerZoneManager.Instance.GetZoneBounds(slot);
            snappedX = Mathf.Clamp(snappedX, minX + 0.5f, maxX - 0.5f);
        }

        // Y도 그리드 스냅 (지면 근처)
        float snappedY = Mathf.Round(playerPos.y / GRID) * GRID;

        return new Vector3(snappedX, snappedY, -1f);
    }

    /// <summary>패널 상단 구역명 텍스트 갱신</summary>
    void UpdateZoneNameText()
    {
        if (_zoneNameText == null) return;
        if (PlayerZoneManager.Instance == null)
        {
            _zoneNameText.text = "";
            return;
        }
        int slot = PlayerZoneManager.Instance.GetLocalSlot();
        _zoneNameText.text = $"▶ {PlayerZoneManager.Instance.GetZoneName(slot)}";
    }

    void ClosePlacementUI()
    {
        if (placementPanel != null)
            placementPanel.SetActive(false);
    }

    // 보유 수량에 맞게 버튼 활성/비활성 갱신
    void RefreshItemButtons()
    {
        if (_playerCollector == null) return;

        foreach (var itemName in _decoItems)
        {
            int count = _playerCollector.inventory.TryGetValue(itemName, out int c) ? c : 0;

            if (_countTexts.TryGetValue(itemName, out var cTmp))
                cTmp.text = $"x{count}";

            if (_itemButtons.TryGetValue(itemName, out var btn))
            {
                btn.interactable = count > 0;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = count > 0
                        ? new Color(0.2f, 0.6f, 0.2f, 0.9f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            }
        }
    }

    // ─────────────────────────────────────────
    //  아이템 배치
    // ─────────────────────────────────────────

    public void PlaceItem(string itemName)
    {
        Debug.Log($"[배치] PlaceItem 호출: {itemName}");

        // DecoScene에서는 플레이어가 없음 → 무제한 배치 허용
        if (_playerCollector != null)
        {
            // TrashZoneScene: 인벤토리 확인
            if (!_playerCollector.inventory.ContainsKey(itemName) ||
                 _playerCollector.inventory[itemName] <= 0)
            {
                Debug.Log($"[배치] 아이템 부족: {itemName}");
                UIManager.Instance?.ShowStatusMessage($"{itemName}이(가) 없습니다!", 2f);
                return;
            }
        }

        int score = (_playerCollector != null) ? _playerCollector.GetDecorScore(itemName) : 10;
        Debug.Log($"[배치] {itemName} 배치 진행: {score}pt");

        // 플레이어가 있으면 인벤토리 업데이트
        if (_playerCollector != null)
        {
            _playerCollector.inventory[itemName]--;
            if (_playerCollector.inventory[itemName] <= 0)
                _playerCollector.inventory.Remove(itemName);
            _playerCollector.RefreshUI();
        }

        // 팀 전체 점수 + 플레이어별 기여도 기록
        GameManager.Instance?.RPC_AddDecorScore(score);
        var runner = FindFirstObjectByType<Fusion.NetworkRunner>();
        int slotIndex = runner != null ? runner.LocalPlayer.PlayerId - 1 : 0;
        GameManager.Instance?.RPC_AddPlayerDecorScore(slotIndex, score);

        // 나무 배치 → 날씨 이벤트 확률 -1%
        if (itemName == "나무")
        {
            WeatherManager.Instance?.AddTree();
            AudioManager.Instance?.PlaySeedPlant();
        }

        // 서버에 배치 기록 (비동기, 실패해도 게임 진행)
        if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
        {
            ApiManager.Instance.PlaceDecoration(
                new DecoPlaceRequest
                {
                    playerId = ApiManager.Instance.playerId,
                    itemType = itemName,
                    score = score
                },
                (res) => Debug.Log($"[DecorationPlacer] 서버 기록 완료 +{score}pt"),
                (err) => Debug.LogWarning($"[DecorationPlacer] 서버 기록 실패 (로컬 반영은 완료): {err}")
            );
        }

        // 구역 인식 + 그리드 스냅 배치
        Vector3 placePos = CalcPlacePosition();
        GameManager.Instance?.RPC_PlaceDecoItem(itemName, placePos);

        // 세트 시스템: 배치 아이템 추적 + 세트 완성 체크
        GameManager.Instance?.RPC_TrackPlacedItem(itemName);

        UIManager.Instance?.ShowStatusMessage($"{itemName} 배치! +{score}pt", 2f);
        TutorialManager.Instance?.OnDecoItemPlaced();
        Debug.Log($"[DecorationPlacer] {itemName} 배치 완료, 점수: {score}pt");

        ClosePlacementUI();
    }

    // ─────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────

    static void AddLabel(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        float fontSize, FontStyles style, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
        var tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
    }

    // ─────────────────────────────────────────
    //  기즈모 — 에디터에서 범위 시각화
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        if (decoPlacePoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (var point in decoPlacePoints)
        {
            if (point != null)
                Gizmos.DrawWireCube(point.position, Vector3.one * 0.5f);
        }
    }
}