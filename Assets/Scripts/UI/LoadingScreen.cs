using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 씬 전환 로딩 화면
///
/// [자동 설치]
/// PhotonManager.Awake()에서 자동 생성 + DontDestroyOnLoad
///
/// [사용]
/// LoadingScreen.Instance.Show()  — 로딩 시작 시
/// LoadingScreen.Instance.Hide()  — 로딩 완료 시
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    private GameObject  _panel;
    private TextMeshProUGUI _msgText;
    private TextMeshProUGUI _tipText;
    private RectTransform   _fillBar;

    private bool  _active;
    private float _dotTimer;
    private int   _dotCount;

    static readonly string[] _tips =
    {
        "쓰레기를 채굴해서 NPC에게 판매하세요!",
        "나무를 배치하면 날씨 이벤트 확률이 줄어들어요.",
        "채굴 미니게임은 화살표 키를 빠르게 입력하세요!",
        "골드를 모아 꾸미기 아이템을 구매할 수 있어요.",
        "배터리는 시간이 지나면 자동으로 회복됩니다.",
        "각 캐릭터마다 고유한 특성이 있어요. 잘 활용하세요!",
        "자신의 구역을 꾸며 팀 꾸미기 점수를 올리세요!",
    };

    // ─────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        _panel.SetActive(false);
    }

    void Update()
    {
        if (!_active) return;

        // 점 애니메이션 (로딩 중...)
        _dotTimer += Time.unscaledDeltaTime;
        if (_dotTimer >= 0.45f)
        {
            _dotTimer = 0f;
            _dotCount = (_dotCount + 1) % 4;
            if (_msgText != null)
                _msgText.text = "로딩 중" + new string('.', _dotCount);
        }

        // 프로그레스 바 사인파 애니메이션
        if (_fillBar != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 1.8f) + 1f) * 0.5f;
            _fillBar.sizeDelta = new Vector2(Mathf.Lerp(40f, 380f, t), _fillBar.sizeDelta.y);
        }
    }

    // ─────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────

    public void Show()
    {
        if (_panel == null) return;
        _active    = true;
        _dotCount  = 0;
        _dotTimer  = 0f;
        if (_msgText != null) _msgText.text = "로딩 중";
        if (_tipText != null)
            _tipText.text = "TIP  " + _tips[Random.Range(0, _tips.Length)];
        _panel.SetActive(true);
    }

    public void Hide()
    {
        StartCoroutine(HideRoutine());
    }

    IEnumerator HideRoutine()
    {
        // 씬 로드 직후 짧게 유지
        yield return new WaitForSecondsRealtime(0.6f);
        _active = false;
        if (_panel != null) _panel.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  UI 생성
    // ─────────────────────────────────────────

    void Build()
    {
        // 전용 Canvas (sortingOrder 최상위)
        var canvasGO = new GameObject("LoadingCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // 전체 어두운 배경
        MakeImage(canvasGO.transform, "BG",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.06f, 0.03f, 1f));

        // 상단 장식선
        MakeImage(canvasGO.transform, "TopLine",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -4f), new Vector2(0f, 0f),
            new Color(0.30f, 0.58f, 0.22f, 0.8f));

        // 하단 장식선
        MakeImage(canvasGO.transform, "BottomLine",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 4f),
            new Color(0.30f, 0.58f, 0.22f, 0.8f));

        // 게임 제목
        MakeTMP(canvasGO.transform, "Title", "Scrap & Sprout",
            new Vector2(0.5f, 0.58f), new Vector2(600f, 90f),
            40f, FontStyles.Bold, new Color(0.85f, 0.74f, 0.28f));

        // 부제목
        MakeTMP(canvasGO.transform, "Sub", "쓰레기를 줍고, 공간을 꾸미자!",
            new Vector2(0.5f, 0.50f), new Vector2(500f, 36f),
            16f, FontStyles.Normal, new Color(0.65f, 0.80f, 0.55f, 0.7f));

        // 로딩 텍스트
        _msgText = MakeTMP(canvasGO.transform, "Msg", "로딩 중",
            new Vector2(0.5f, 0.40f), new Vector2(300f, 40f),
            20f, FontStyles.Normal, new Color(0.87f, 0.84f, 0.77f));

        // 프로그레스 바 배경
        MakeImage(canvasGO.transform, "BarBG",
            new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f),
            new Vector2(-200f, -4f), new Vector2(200f, 4f),
            new Color(0.12f, 0.18f, 0.10f));

        // 프로그레스 바 채우기
        var fillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(canvasGO.transform, false);
        _fillBar = fillGO.GetComponent<RectTransform>();
        _fillBar.anchorMin = _fillBar.anchorMax = new Vector2(0.5f, 0.34f);
        _fillBar.pivot     = new Vector2(0f, 0.5f);
        _fillBar.anchoredPosition = new Vector2(-200f, 0f);
        _fillBar.sizeDelta = new Vector2(100f, 8f);
        fillGO.GetComponent<Image>().color = new Color(0.30f, 0.58f, 0.22f);
        fillGO.GetComponent<Image>().raycastTarget = false;

        // 팁 텍스트
        _tipText = MakeTMP(canvasGO.transform, "Tip", "",
            new Vector2(0.5f, 0.26f), new Vector2(700f, 50f),
            14f, FontStyles.Normal, new Color(0.65f, 0.72f, 0.58f, 0.8f));

        _panel = canvasGO;
    }

    // ─────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────

    static void MakeImage(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static TextMeshProUGUI MakeTMP(Transform parent, string name, string text,
        Vector2 anchorPos, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorPos;
        r.pivot     = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize;
        tmp.fontStyle = style; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }
}
