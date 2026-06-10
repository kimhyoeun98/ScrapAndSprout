using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[UIManager] 싱글톤 생성 + DontDestroyOnLoad + 씬 로드 감지");
        }
        else if (Instance != this)
        {
            Debug.Log("[UIManager] 중복 생성 제거");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[UIManager] 씬 로드됨: {scene.name}");

        // UIManager는 "Canvas" GameObject에 함께 붙어 있어 DontDestroyOnLoad로
        // HUD 캔버스째 씬을 넘어 유지된다. 이 유지는 게임플레이 씬 사이
        // (TrashZoneScene ↔ DecoScene, DecoScene엔 자체 UIManager가 없음)에서만 필요하다.
        // 그 외 씬(로비/결과/대기실/로그인 등)으로 나가면 잔여 HUD 캔버스가
        // 그대로 겹쳐 보이므로, 게임플레이 씬이 아니면 자기 자신을 파괴한다.
        // 다음 게임 시작 시 TrashZoneScene이 Canvas+UIManager를 새로 생성한다.
        // (TutorialScene은 자체 UIManager를 가지므로 유지 목록에 포함)
        if (scene.name != "TrashZoneScene" && scene.name != "DecoScene" && scene.name != "TutorialScene")
        {
            Debug.Log($"[UIManager] 비게임 씬({scene.name}) 진입 — 잔여 HUD 캔버스 파괴");
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Destroy(gameObject);
            return;
        }

        StartCoroutine(ReinitializeUINextFrame());
    }

    // ─────────────────────────────────────────
    //  HUD 연결
    // ─────────────────────────────────────────
    [Header("── HUD ──")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI decorScoreText;
    public TextMeshProUGUI statusMessageText;
    public TextMeshProUGUI weatherText;

    [Header("── 인벤토리 ──")]
    public GameObject inventoryPanel;
    public GameObject hotbar;

    [Header("── 안내 텍스트 ──")]
    public GameObject pickText;

    [Header("── 게임 종료 버튼 ──")]
    public Button gameExitButton;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────
    private bool _isInventoryOpen = false;
    private int _nearbyTrashCount = 0;

    // ESC 확인 다이얼로그
    private GameObject _exitConfirmPanel;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        if (Instance != this)
        {
            Debug.Log("[UIManager] 중복 인스턴스의 Start() 무시");
            return;
        }
        InitializeUI();
    }

    IEnumerator ReinitializeUINextFrame()
    {
        yield return null; // Awake/Start 완료 대기
        yield return null; // ← 한 프레임 더 대기
        yield return new WaitForSeconds(0.5f); // ← 추가: Canvas 완전 초기화 대기

        if (this == null)
        {
            Debug.LogWarning("[UIManager] UIManager가 destroyed됨");
            yield break;
        }

        if (Instance != this)
        {
            Debug.LogWarning("[UIManager] Instance가 다름 (중복 제거됨)");
            yield break;
        }

        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[UIManager] gameObject이 비활성화됨");
            yield break;
        }

        // 현재 씬에 따라 UI 표시 제어 (파괴하지 말고 활성화만 제어)
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[UIManager] 현재 씬: {currentScene}");
        Debug.Log($"[UIManager] 씬 이름 비교: '{currentScene}' == 'TrashZoneScene' ? {currentScene == "TrashZoneScene"}");

        // 현재 Canvas 찾기
        Canvas currentCanvas = FindFirstObjectByType<Canvas>();
        if (currentCanvas != null)
        {
            // Canvas의 root parent를 DontDestroyOnLoad로 설정 (Canvas 자체는 root가 아닐 수 있음)
            Transform root = currentCanvas.transform.root;
            //DontDestroyOnLoad(root.gameObject);
            Debug.Log($"[UIManager] {root.name} DontDestroyOnLoad 설정");
        }

        bool isTrashZone = (currentScene == "TrashZoneScene");

        // HUD 텍스트 참조 재연결: TrashZone↔Deco 왕복이나 새 게임 시 이전 씬의
        // (파괴된) 텍스트 오브젝트를 가리키는 stale 참조를 현재 씬 것으로 갱신.
        // AutoConnectHUD는 필드가 null일 때만 재할당하는데, 파괴된 Unity 오브젝트는
        // == null 이 true이므로 stale 참조가 자동으로 새 오브젝트로 교체된다.
        AutoConnectHUD();

        // LeaderboardUI — 활성화 유지 (ESC Update 필요), TrashZoneScene에서만 표시
        var existingLeaderboards = FindObjectsByType<LeaderboardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var lb in existingLeaderboards)
        {
            if (lb == null) continue;
            lb.gameObject.SetActive(true);
            Debug.Log($"[UIManager] LeaderboardUI {(isTrashZone ? "활성" : "비활성")} 씬");
        }

        // MinimapUI — TrashZoneScene에서만 표시
        var existingMinimaps = FindObjectsByType<MinimapUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mm in existingMinimaps)
        {
            if (mm == null) continue;
            mm.gameObject.SetActive(isTrashZone);
            Debug.Log($"[UIManager] MinimapUI {(isTrashZone ? "표시" : "숨김")}");
        }

        if (!isTrashZone && currentScene == "DecoScene")
        {
            Debug.Log("[UIManager] DecoScene - UI 숨김 (배치 UI만 표시)");
        }

        Debug.Log("[UIManager] UI 재초기화 완료");
    }

    void InitializeUI()
    {
        Debug.Log("[UIManager] InitializeUI() 시작");

        if (pickText != null) pickText.SetActive(false);
        if (hotbar != null) hotbar.SetActive(true);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        // LeaderboardUI/MinimapUI는 ReinitializeUINextFrame()에서 관리
        AutoConnectHUD();
        AutoConnectGameExitButton();
        BuildExitConfirmPanel();

        BuildWeatherText();
        BuildBatteryUI();

        Debug.Log("[UIManager] UI 초기화 완료");
    }

    void BuildBatteryUI()
    {
        // 씬에 이미 배치된 BatteryBar 찾기
        var batteryBar = GameObject.Find("BatteryBar");
        if (batteryBar == null)
        {
            // 대체 경로: Canvas/HUD/BatteryBar
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                var hud = canvas.transform.Find("HUD");
                if (hud != null)
                    batteryBar = hud.Find("BatteryBar")?.gameObject;
            }
        }

        if (batteryBar != null)
        {
            Debug.Log("[UIManager] BatteryBar 찾음: " + batteryBar.name);
        }
        else
        {
            Debug.LogWarning("[UIManager] 씬에서 BatteryBar 오브젝트를 찾을 수 없습니다");
        }
    }

    void AutoConnectHUD()
    {
        var allTMP = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var tmp in allTMP)
        {
            switch (tmp.gameObject.name)
            {
                case "GoldText": if (goldText == null) goldText = tmp; break;
                case "TimerText": if (timerText == null) timerText = tmp; break;
                case "DecorScoreText": if (decorScoreText == null) decorScoreText = tmp; break;
                case "StatusMessageText": if (statusMessageText == null) statusMessageText = tmp; break;
                case "PickedText": if (pickText == null) pickText = tmp.gameObject; break;
                case "WeatherText": if (weatherText == null) weatherText = tmp; break;
            }
        }

        Debug.Log($"[UIManager] pickText 자동 연결: {(pickText != null ? pickText.name : "실패")}");
        // StatusMessageText는 텍스트만 비워서 숨기기 (비활성화 X)
        if (statusMessageText != null)
        {
            statusMessageText.gameObject.SetActive(true);
            statusMessageText.text = "";
        }

        Debug.Log($"[UIManager] HUD 자동 연결 — Gold:{goldText != null} Timer:{timerText != null} Score:{decorScoreText != null} Status:{statusMessageText != null}");
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();

        // ESC: LeaderboardUI가 처리함 (LeaderboardUI.Update() 참고)

        // 매 프레임 쓰레기 근처 여부 체크
        CheckNearbyTrash();
    }

    // ─────────────────────────────────────────
    //  ESC 확인 다이얼로그
    // ─────────────────────────────────────────

    void BuildExitConfirmPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 반투명 배경 오버레이
        _exitConfirmPanel = new GameObject("ExitConfirmPanel",
            typeof(RectTransform), typeof(Image));
        _exitConfirmPanel.transform.SetParent(canvas.transform, false);
        var panelRect = _exitConfirmPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        _exitConfirmPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

        // 다이얼로그 박스
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_exitConfirmPanel.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(340f, 180f);
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        // 질문 텍스트
        var textObj = new GameObject("Question", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(box.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 0f);
        textRect.offsetMax = new Vector2(-20f, -10f);
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "게임을 나가시겠습니까?";
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // 버튼 영역
        var btnRow = new GameObject("Buttons", typeof(RectTransform));
        btnRow.transform.SetParent(box.transform, false);
        var btnRowRect = btnRow.GetComponent<RectTransform>();
        btnRowRect.anchorMin = new Vector2(0f, 0f);
        btnRowRect.anchorMax = new Vector2(1f, 0.5f);
        btnRowRect.offsetMin = new Vector2(20f, 16f);
        btnRowRect.offsetMax = new Vector2(-20f, -8f);
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // 로비로 나가기 버튼
        CreateConfirmButton(btnRow.transform, "로비로 나가기",
            new Color(0.7f, 0.2f, 0.2f, 1f), OnExitToLobby);

        // 계속 게임 버튼
        CreateConfirmButton(btnRow.transform, "계속 게임",
            new Color(0.2f, 0.55f, 0.2f, 1f), OnContinueGame);

        _exitConfirmPanel.SetActive(false);
    }

    void CreateConfirmButton(Transform parent, string label, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        btnObj.GetComponent<Image>().color = color;

        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        var r = textObj.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        btnObj.GetComponent<Button>().onClick.AddListener(onClick);
    }

    void ToggleExitConfirm()
    {
        if (_exitConfirmPanel == null) return;
        bool open = !_exitConfirmPanel.activeSelf;
        _exitConfirmPanel.SetActive(open);

        // 다이얼로그 열리면 커서 표시
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.None;
    }

    void OnExitToLobby()
    {
        _exitConfirmPanel.SetActive(false);
        if (PhotonManager.Instance != null)
            _ = PhotonManager.Instance.LeaveRoom();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    void OnContinueGame()
    {
        _exitConfirmPanel?.SetActive(false);
    }

    void AutoConnectGameExitButton()
    {
        if (gameExitButton != null) { WireExitButton(); return; }

        // 비활성 포함 탐색
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var btn = allButtons.FirstOrDefault(b => b.gameObject.name == "NextRoundButton");
        if (btn != null)
        {
            gameExitButton = btn;
            WireExitButton();
        }
    }

    void WireExitButton()
    {
        gameExitButton.gameObject.SetActive(true);

        // 버튼 텍스트 "게임 종료"로 변경
        var tmp = gameExitButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = "게임 종료";

        gameExitButton.onClick.RemoveAllListeners();
        gameExitButton.onClick.AddListener(() =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.GoToResultScene();
        });
        Debug.Log("[UIManager] 게임 종료 버튼 연결 완료");
    }

    // ─────────────────────────────────────────
    //  인벤토리 토글
    // ─────────────────────────────────────────
    void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(_isInventoryOpen);
        if (hotbar != null) hotbar.SetActive(!_isInventoryOpen);
    }

    /// <summary>외부(배치 모드 진입 등)에서 인벤토리를 강제로 닫을 때 호출</summary>
    public void ForceCloseInventory()
    {
        _isInventoryOpen = false;
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (hotbar != null) hotbar.SetActive(true);
    }

    // ─────────────────────────────────────────
    //  쓰레기 줍기 텍스트 관리
    // ─────────────────────────────────────────
    public void OnTrashEnter()
    {
        _nearbyTrashCount++;
        if (pickText != null)
            pickText.SetActive(true);
        Debug.Log($"[UIManager] OnTrashEnter — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashExit()
    {
        _nearbyTrashCount--;
        if (_nearbyTrashCount <= 0)
        {
            _nearbyTrashCount = 0;
            if (pickText != null)
                pickText.SetActive(false);
        }
        Debug.Log($"[UIManager] OnTrashExit — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashCollected()
    {
        _nearbyTrashCount = 0;
        if (pickText != null)
            pickText.SetActive(false);
    }


    // ─────────────────────────────────────────
    //  페이드 인/아웃 (텔레포트 연출용)
    //
    //  [씬 설정]
    //  Canvas 하위에 Image 오브젝트 생성
    //  이름: FadePanel, Color: 검정(0,0,0,0), RaycastTarget: false
    //  Anchor: Stretch (전체 화면 덮기)
    //  Inspector에서 fadePanel 슬롯에 연결
    // ─────────────────────────────────────────

    [Header("── 페이드 패널 ──")]
    [Tooltip("전체화면 검정 Image. Canvas 하위에 배치 후 연결")]
    public Image fadePanel;

    private Coroutine _fadeCoroutine;

    public void FadeOut(float duration)
    {
        if (this == null) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, duration));
    }

    public void FadeIn(float duration)
    {
        if (this == null) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, duration));
    }

    IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
    {
        if (fadePanel == null) yield break;

        fadePanel.gameObject.SetActive(true);

        float elapsed = 0f;
        Color c = fadePanel.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            fadePanel.color = c;
            yield return null;
        }

        c.a = toAlpha;
        fadePanel.color = c;

        // 완전히 투명해지면 비활성화
        if (toAlpha <= 0f)
            fadePanel.gameObject.SetActive(false);
    }

    public void ShowStatusMessage(string message, float duration = 2f)
    {
        if (string.IsNullOrEmpty(message)) return;
        Debug.Log($"[UIManager] {message}");

        // UIManager가 destroyed되었는지 체크
        if (this == null || statusMessageText == null)
            return;

        try
        {
            if (gameObject.activeInHierarchy)
            {
                if (_statusCoroutine != null) StopCoroutine(_statusCoroutine);
                _statusCoroutine = StartCoroutine(ShowStatusRoutine(message, duration));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UIManager] ShowStatusMessage 실패: {e.Message}");
        }
    }

    private Coroutine _statusCoroutine;

    IEnumerator ShowStatusRoutine(string message, float duration)
    {
        statusMessageText.text = message;
        statusMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        statusMessageText.gameObject.SetActive(false);
        _statusCoroutine = null;
    }
    // CheckNearbyTrash 캐시
    private PlayerMovement _cachedLocalPlayer;
    private float          _playerFindTimer;

    /// <summary>
    /// 매 프레임 로컬 플레이어 기준으로 쓰레기 더미 근처 여부 직접 체크
    /// Fusion Despawn 시 OnTriggerExit 미발생 문제 방지
    /// </summary>
    void CheckNearbyTrash()
    {
        if (pickText == null) return;

        // 로컬 플레이어 캐시 (1초마다 갱신)
        _playerFindTimer -= Time.deltaTime;
        if (_playerFindTimer <= 0f || _cachedLocalPlayer == null)
        {
            _playerFindTimer = 1f;
            _cachedLocalPlayer = null;
            foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (pm.HasInputAuthority) { _cachedLocalPlayer = pm; break; }
            }
        }

        if (_cachedLocalPlayer == null)
        {
            pickText.SetActive(false);
            return;
        }

        // 2D 거리 사용 (TrashPile의 Physics2D.OverlapCircleAll과 동일 기준)
        Vector2 playerPos2D = new Vector2(
            _cachedLocalPlayer.transform.position.x,
            _cachedLocalPlayer.transform.position.y);

        var piles = FindObjectsByType<TrashPile>(FindObjectsSortMode.None);
        bool anyNearby = false;
        foreach (var pile in piles)
        {
            if (pile == null) continue;
            Vector2 pilePos2D = new Vector2(
                pile.transform.position.x,
                pile.transform.position.y);
            if (Vector2.Distance(playerPos2D, pilePos2D) < 2.5f)
            {
                anyNearby = true;
                break;
            }
        }

        pickText.SetActive(anyNearby);
    }
    // ─────────────────────────────────────────
    //  HUD 갱신 (외부에서 호출)
    // ─────────────────────────────────────────

    public void UpdateGold(int gold)
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold:N0}G";
    }

    public void UpdateTimer(float elapsedSeconds)
    {
        if (timerText == null) return;
        int min = Mathf.FloorToInt(elapsedSeconds / 60f);
        int sec = Mathf.FloorToInt(elapsedSeconds % 60f);
        timerText.text = $"{min:00}:{sec:00}";
        timerText.color = Color.white;
    }

    public void UpdateDecorScore(int current, int goal)
    {
        if (decorScoreText != null)
            decorScoreText.text = $"꾸미기: {current}pt";
    }

    // ─────────────────────────────────────────
    //  배터리 위험 경고 (화면 가장자리 빨간 깜빡임)
    // ─────────────────────────────────────────

    private Image _batteryWarningOverlay;
    private Coroutine _batteryPulseCoroutine;
    private bool _batteryWarningActive = false;

    public void SetBatteryWarning(bool active)
    {
        if (_batteryWarningActive == active) return;
        _batteryWarningActive = active;

        if (_batteryWarningOverlay == null)
            BuildBatteryWarningOverlay();

        if (active)
        {
            _batteryWarningOverlay.gameObject.SetActive(true);
            if (_batteryPulseCoroutine != null) StopCoroutine(_batteryPulseCoroutine);
            _batteryPulseCoroutine = StartCoroutine(BatteryPulseRoutine());
        }
        else
        {
            if (_batteryPulseCoroutine != null) StopCoroutine(_batteryPulseCoroutine);
            _batteryPulseCoroutine = null;
            if (_batteryWarningOverlay != null)
                _batteryWarningOverlay.gameObject.SetActive(false);
        }
    }

    void BuildBatteryWarningOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("BatteryWarningOverlay",
            typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        _batteryWarningOverlay = go.GetComponent<Image>();
        _batteryWarningOverlay.color = new Color(1f, 0f, 0f, 0f);
        _batteryWarningOverlay.raycastTarget = false;
        go.SetActive(false);
    }

    IEnumerator BatteryPulseRoutine()
    {
        while (true)
        {
            yield return OverlayFade(0.04f, 0.28f, 0.35f);
            yield return OverlayFade(0.28f, 0.04f, 0.35f);
        }
    }

    IEnumerator OverlayFade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_batteryWarningOverlay != null)
                _batteryWarningOverlay.color = new Color(
                    1f, 0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
    }

    // ─────────────────────────────────────────
    //  꾸미기 점수 마일스톤 알림
    // ─────────────────────────────────────────

    public void ShowMilestoneNotification(int milestone)
    {
        StartCoroutine(MilestoneRoutine(milestone));
    }

    IEnumerator MilestoneRoutine(int milestone)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) yield break;

        // 배경 패널
        var bg = new GameObject("MilestoneBG",
            typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot     = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(0f, 90f);
        bgRect.sizeDelta = new Vector2(400f, 72f);
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.05f, 0.07f, 0.04f, 0.94f);
        bgImg.raycastTarget = false;

        // 상단 골드 바
        var topBar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(bg.transform, false);
        var tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0f, 1f); tbRect.anchorMax = new Vector2(1f, 1f);
        tbRect.pivot = new Vector2(0.5f, 1f); tbRect.anchoredPosition = Vector2.zero;
        tbRect.sizeDelta = new Vector2(0f, 4f);
        topBar.GetComponent<Image>().color = new Color(0.85f, 0.74f, 0.28f, 1f);
        topBar.GetComponent<Image>().raycastTarget = false;

        // 텍스트
        var go = new GameObject("MilestoneText",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(bg.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 4f); rect.offsetMax = new Vector2(-12f, -4f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = $"팀 꾸미기 {milestone:N0}pt 달성!";
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.74f, 0.28f);
        tmp.raycastTarget = false;

        // 팝인
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(0.6f, 1f, elapsed / 0.2f);
            bg.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        bg.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(2.5f);

        // 페이드 아웃
        elapsed = 0f;
        Color bgColor = bgImg.color;
        Color txtColor = tmp.color;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.5f;
            bgImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * (1f - t));
            tmp.color   = new Color(txtColor.r, txtColor.g, txtColor.b, 1f - t);
            yield return null;
        }

        Destroy(bg);
    }

    // ─────────────────────────────────────────
    //  날씨 HUD
    // ─────────────────────────────────────────

    public void UpdateWeather(WeatherManager.WeatherType weather)
    {
        if (weatherText == null)
            BuildWeatherText();
        if (weatherText == null) return;
        switch (weather)
        {
            case WeatherManager.WeatherType.Clear:
                weatherText.text = "☀ 맑음";
                weatherText.color = Color.yellow;
                break;
            case WeatherManager.WeatherType.AcidRain:
                weatherText.text = "☂ 산성비";
                weatherText.color = new Color(0.4f, 1f, 0.4f);
                break;
            case WeatherManager.WeatherType.YellowDust:
                weatherText.text = "💨 황사";
                weatherText.color = new Color(1f, 0.8f, 0.3f);
                break;
        }
    }

    void BuildWeatherText()
    {
        // Inspector에서 이미 연결됐으면 새로 만들지 않음
        if (weatherText != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 배경 카드
        var card = new GameObject("WeatherCard",
            typeof(RectTransform), typeof(Image));
        card.transform.SetParent(canvas.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 1f);
        cardRect.anchorMax = new Vector2(0.5f, 1f);
        cardRect.pivot     = new Vector2(0.5f, 1f);
        cardRect.anchoredPosition = new Vector2(0f, -10f);
        cardRect.sizeDelta = new Vector2(140f, 38f);
        card.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.04f, 0.88f);
        card.GetComponent<Image>().raycastTarget = false;

        // 액센트 바
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(card.transform, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot     = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 3f);
        bar.GetComponent<Image>().color = new Color(0.30f, 0.58f, 0.22f, 1f);
        bar.GetComponent<Image>().raycastTarget = false;

        // 텍스트
        var go = new GameObject("WeatherText",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(card.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 2f);
        rect.offsetMax = new Vector2(-6f, -3f);

        weatherText = go.GetComponent<TextMeshProUGUI>();
        weatherText.fontSize  = 15f;
        weatherText.fontStyle = FontStyles.Bold;
        weatherText.color     = new Color(0.85f, 0.74f, 0.28f);
        weatherText.alignment = TextAlignmentOptions.Center;
        weatherText.raycastTarget = false;
    }

    // ─────────────────────────────────────────
    //  골드 플로팅 텍스트
    // ─────────────────────────────────────────

    public void ShowFloatingGold(int delta, Vector3 worldPos)
    {
        if (delta == 0) return;
        StartCoroutine(FloatingGoldRoutine(delta, worldPos));
    }

    IEnumerator FloatingGoldRoutine(int delta, Vector3 worldPos)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) yield break;

        var go = new GameObject("FloatingGold", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = delta > 0 ? $"+{delta:N0}G" : $"{delta:N0}G";
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        Color textColor = delta > 0
            ? new Color(1f, 0.9f, 0.2f)   // 획득: 노란색
            : new Color(1f, 0.4f, 0.4f);  // 소비: 붉은색
        tmp.color = textColor;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140f, 40f);

        // 월드 → 스크린 → 캔버스 로컬 좌표 변환
        Camera cam = Camera.main;
        Vector2 screenPos = cam != null
            ? (Vector2)cam.WorldToScreenPoint(worldPos + Vector3.up * 1.5f)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(), screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 localPos);
        rect.anchoredPosition = localPos;

        // 위로 이동 + 페이드 아웃
        float duration = 1.5f;
        float elapsed = 0f;
        Vector2 startPos = rect.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            tmp.color = new Color(textColor.r, textColor.g, textColor.b, 1f - t);
            rect.anchoredPosition = startPos + new Vector2(0f, 60f * t);
            yield return null;
        }

        Destroy(go);
    }
}