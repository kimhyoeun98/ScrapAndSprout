using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 채굴 미니게임 — 랜덤 방향키 순서대로 입력하는 미니게임
///
/// [비유: 메이플스토리 룬 해방]
/// 화면에 방향키 순서가 표시되고, 플레이어가 순서대로 입력합니다.
/// 모두 맞추면 성공, 틀리거나 시간이 초과되면 실패입니다.
///
/// [사용법]
/// MiningMinigame.Instance.StartMinigame(횟수, 결과콜백);
///
/// [씬 설정]
/// MinigamePanel 하위에:
///   - SequenceContainer: 방향키 아이콘들이 나열되는 부모 오브젝트
///   - InputFeedbackText: "✓" / "✗" 피드백 텍스트
///   - TimerText: 남은 시간 표시
///   - TimerSlider: 시간 바 (선택)
/// </summary>
[DefaultExecutionOrder(9999)]
public class MiningMinigame : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────

    public static MiningMinigame Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────
    //  UI 연결 (Inspector에서 드래그)
    // ─────────────────────────────────────────

    [Header("── 미니게임 UI ──")]
    [Tooltip("미니게임 전체 패널 (시작 시 켜짐)")]
    public GameObject minigamePanel;

    [Tooltip("방향키 아이콘들이 들어갈 부모 오브젝트")]
    public Transform sequenceContainer;

    [Tooltip("입력 피드백 텍스트 (✓ / ✗)")]
    public TextMeshProUGUI inputFeedbackText;

    [Tooltip("남은 시간 텍스트")]
    public TextMeshProUGUI timerText;

    [Tooltip("시간 바 (선택)")]
    public Slider timerSlider;

    [Tooltip("채굴 진행 바 (미연결 시 자동 생성)")]
    public Slider progressSlider;

    [Tooltip("진행 카운터 텍스트 (미연결 시 자동 생성)")]
    public TextMeshProUGUI progressText;

    // ─────────────────────────────────────────
    //  방향키 아이콘 프리팹 / 스프라이트
    // ─────────────────────────────────────────

    [Header("── 방향키 아이콘 ──")]
    [Tooltip("방향키 아이콘 프리팹 (Image 컴포넌트 포함)")]
    public GameObject arrowIconPrefab;

    [Tooltip("위쪽 화살표 스프라이트")]
    public Sprite arrowUp;

    [Tooltip("아래쪽 화살표 스프라이트")]
    public Sprite arrowDown;

    [Tooltip("왼쪽 화살표 스프라이트")]
    public Sprite arrowLeft;

    [Tooltip("오른쪽 화살표 스프라이트")]
    public Sprite arrowRight;

    // ─────────────────────────────────────────
    //  색상 설정
    // ─────────────────────────────────────────

    [Header("── 색상 설정 ──")]
    [Tooltip("아직 입력 안 한 아이콘 색")]
    public Color pendingColor = Color.white;

    [Tooltip("현재 입력해야 할 아이콘 색 (강조)")]
    public Color currentColor = Color.yellow;

    [Tooltip("성공적으로 입력한 아이콘 색")]
    public Color successColor = Color.green;

    [Tooltip("틀렸을 때 아이콘 색")]
    public Color failColor = Color.red;

    // ─────────────────────────────────────────
    //  게임 설정
    // ─────────────────────────────────────────

    [Header("── 시간 설정 ──")]
    [Tooltip("전체 제한 시간 (초). 입력 횟수에 비례해서 늘어남")]
    public float secondsPerInput = 2.5f;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    // 현재 미니게임에서 눌러야 할 방향키 시퀀스
    // 예: [Up, Right, Down, Left]
    private List<KeyCode> _sequence = new List<KeyCode>();

    // 지금 몇 번째 입력을 기다리고 있는지 (0부터 시작)
    private int _currentIndex = 0;

    private bool _isPlaying = false;
    public bool IsPlaying => _isPlaying; // ← 추가
    private float _timeLeft = 0f;

    // 성공/실패 결과를 TrashPile에 돌려줄 콜백
    // Action<bool>: bool = true(성공), false(실패)
    private Action<bool> _resultCallback;

    // 생성된 아이콘 오브젝트들 (나중에 지우기 위해 보관)
    private List<GameObject> _iconObjects = new List<GameObject>();

    // 방향키 목록 (랜덤 선택에 사용)
    private static readonly KeyCode[] _arrowKeys =
    {
        KeyCode.UpArrow,
        KeyCode.DownArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow
    };

    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    void Start()
    {
        // minigamePanel 찾기
        if (minigamePanel == null)
        {
            // Canvas에서 MinigamePanel 찾기
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var found = canvas.transform.Find("MinigamePanel");
                if (found != null) minigamePanel = found.gameObject;
            }
        }

        // 여전히 없으면 모든 Canvas에서 검색
        if (minigamePanel == null)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var found = canvas.transform.Find("MinigamePanel");
                if (found != null) { minigamePanel = found.gameObject; break; }
            }
        }

        // 미니게임 패널은 시작 시 숨기기
        if (minigamePanel != null)
            minigamePanel.SetActive(false);
        else
            Debug.LogWarning("[MiningMinigame] MinigamePanel을 찾을 수 없습니다!");
    }

    // ─────────────────────────────────────────
    //  미니게임 시작
    //
    //  [매개변수]
    //  requiredInputs: 눌러야 할 방향키 개수
    //  callback: 결과를 받을 함수 (true=성공, false=실패)
    // ─────────────────────────────────────────

    public void StartMinigame(int requiredInputs, Action<bool> callback)
    {
        if (_isPlaying)
        {
            Debug.LogWarning("[MiningMinigame] 이미 진행 중입니다!");
            return;
        }

        _resultCallback = callback;
        _currentIndex = 0;
        _isPlaying = true;

        // 시퀀스 랜덤 생성
        _sequence.Clear();
        for (int i = 0; i < requiredInputs; i++)
        {
            KeyCode randomKey = _arrowKeys[UnityEngine.Random.Range(0, _arrowKeys.Length)];
            _sequence.Add(randomKey);
        }

        // 타이머 설정: 입력 횟수에 비례해서 시간 부여
        _timeLeft = requiredInputs * secondsPerInput;

        // UI 초기화 및 패널 열기
        SetupUI();

        Debug.Log($"[MiningMinigame] 시작! 시퀀스: {string.Join(", ", _sequence)}, 제한시간: {_timeLeft}초");
    }

    // ─────────────────────────────────────────
    //  UI 초기 세팅 — 방향키 아이콘 배치
    // ─────────────────────────────────────────

    void SetupUI()
    {
        // 이전 아이콘들 삭제
        foreach (var icon in _iconObjects)
            if (icon != null) Destroy(icon);
        _iconObjects.Clear();

        // 시퀀스 길이만큼 아이콘 생성
        for (int i = 0; i < _sequence.Count; i++)
        {
            if (arrowIconPrefab == null || sequenceContainer == null) break;

            GameObject iconObj = Instantiate(arrowIconPrefab, sequenceContainer);
            Image img = iconObj.GetComponent<Image>();

            if (img != null)
            {
                // 방향에 맞는 스프라이트 설정
                img.sprite = GetArrowSprite(_sequence[i]);
                // 첫 번째 아이콘만 강조, 나머지는 흐리게
                img.color = (i == 0) ? currentColor : pendingColor;
            }

            _iconObjects.Add(iconObj);
        }

        // 피드백 초기화
        if (inputFeedbackText != null)
            inputFeedbackText.text = "";

        // 타이머 바 초기화
        if (timerSlider != null)
            timerSlider.value = 1f;

        // 패널 활성화
        if (minigamePanel != null)
            minigamePanel.SetActive(true);

        // 진행 바 초기화 (없으면 자동 생성)
        if (progressSlider == null && minigamePanel != null)
            BuildProgressBar();
        UpdateProgressUI();

        // 플레이어 이동 잠금
        LockPlayerMovement(true);
    }



    void LockPlayerMovement(bool lockMovement)
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var pm in players)
        {
            if (!pm.HasInputAuthority) continue;
            pm.IsMovementLocked = lockMovement;
            Debug.Log($"[MiningMinigame] 잠금 적용: {pm.name} → {lockMovement}");
            break;
        }
    }

    // ─────────────────────────────────────────
    //  매 프레임: 입력 감지 + 타이머
    // ─────────────────────────────────────────

    void Update()
    {
        if (!_isPlaying) return;

        // 타이머 감소
        _timeLeft -= Time.deltaTime;
        UpdateTimerUI();

        // 시간 초과 → 실패
        if (_timeLeft <= 0f)
        {
            EndMinigame(false);
            return;
        }

        // 방향키 입력 체크
        foreach (KeyCode key in _arrowKeys)
        {
            if (Input.GetKeyDown(key))
            {
                HandleInput(key);
                break; // 한 프레임에 하나만 처리
            }
        }
    }

    // ─────────────────────────────────────────
    //  입력 처리
    // ─────────────────────────────────────────

    void HandleInput(KeyCode pressedKey)
    {
        if (_currentIndex >= _sequence.Count) return;

        KeyCode expectedKey = _sequence[_currentIndex];

        if (pressedKey == expectedKey)
        {
            // ✓ 정답
            OnCorrectInput();
        }
        else
        {
            // ✗ 오답 → 즉시 실패
            OnWrongInput();
        }
    }

    void OnCorrectInput()
    {
        // 현재 아이콘 초록색으로 변경
        if (_currentIndex < _iconObjects.Count)
        {
            Image img = _iconObjects[_currentIndex].GetComponent<Image>();
            if (img != null) img.color = successColor;
        }

        _currentIndex++;
        UpdateProgressUI();

        // 다음 아이콘 강조
        if (_currentIndex < _iconObjects.Count)
        {
            Image nextImg = _iconObjects[_currentIndex].GetComponent<Image>();
            if (nextImg != null) nextImg.color = currentColor;
        }

        // 피드백
        ShowFeedback("✓", successColor);

        // 모두 입력했으면 성공
        if (_currentIndex >= _sequence.Count)
        {
            StartCoroutine(DelayedEnd(true, 0.3f));
        }

        Debug.Log($"[MiningMinigame] 정답! {_currentIndex}/{_sequence.Count}");
    }

    void OnWrongInput()
    {
        // 현재 아이콘 빨간색으로 변경
        if (_currentIndex < _iconObjects.Count)
        {
            Image img = _iconObjects[_currentIndex].GetComponent<Image>();
            if (img != null) img.color = failColor;
        }

        ShowFeedback("✗", failColor);

        Debug.Log("[MiningMinigame] 오답! 미니게임 실패");

        // 살짝 딜레이 후 종료 (실패 연출)
        StartCoroutine(DelayedEnd(false, 0.5f));
    }

    // ─────────────────────────────────────────
    //  미니게임 종료
    // ─────────────────────────────────────────

    IEnumerator DelayedEnd(bool success, float delay)
    {
        
        
        yield return new WaitForSeconds(delay);
        EndMinigame(success);
    }

    void EndMinigame(bool success)
    {
        _isPlaying = false;

        // 패널 닫기
        if (minigamePanel != null)
            minigamePanel.SetActive(false);

        // 이동 잠금 해제
        LockPlayerMovement(false);

        // 아이콘 정리
        foreach (var icon in _iconObjects)
            if (icon != null) Destroy(icon);
        _iconObjects.Clear();

        Debug.Log($"[MiningMinigame] 종료 — {(success ? "성공" : "실패")}");

        // TrashPile으로 결과 전달
        _resultCallback?.Invoke(success);
        _resultCallback = null;
    }

    /// <summary>
    /// 외부에서 강제 취소 (플레이어가 더미에서 벗어났을 때)
    /// </summary>
    public void CancelMinigame()
    {
        if (!_isPlaying) return;
        EndMinigame(false);
        UIManager.Instance?.ShowStatusMessage("채굴 취소됨", 1f);
    }

    // ─────────────────────────────────────────
    //  UI 헬퍼
    // ─────────────────────────────────────────

    void UpdateTimerUI()
    {
        float totalTime = _sequence.Count * secondsPerInput;
        float ratio = _timeLeft / totalTime;

        if (timerText != null)
            timerText.text = $"{_timeLeft:F1}s";

        if (timerSlider != null)
            timerSlider.value = ratio;
    }

    void UpdateProgressUI()
    {
        int total = _sequence.Count;
        float ratio = total > 0 ? (float)_currentIndex / total : 0f;

        if (progressSlider != null)
            progressSlider.value = ratio;

        if (progressText != null)
            progressText.text = $"{_currentIndex} / {total}";
    }

    void BuildProgressBar()
    {
        if (minigamePanel == null)
        {
            Debug.LogWarning("[MiningMinigame] minigamePanel이 없어서 진행 바를 생성할 수 없습니다!");
            return;
        }

        // minigamePanel 하위에 진행 바 자동 생성
        var barObj = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image), typeof(Slider));
        barObj.transform.SetParent(minigamePanel.transform, false);

        var barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 8f);
        barRect.sizeDelta = new Vector2(-20f, 16f);
        barObj.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(barObj.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(2f, 2f);
        faRect.offsetMax = new Vector2(-2f, -2f);

        // Fill
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f, 1f);

        var slider = barObj.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        progressSlider = slider;

        // 카운터 텍스트
        var textObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(minigamePanel.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.anchoredPosition = new Vector2(0f, 26f);
        textRect.sizeDelta = new Vector2(0f, 20f);
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "0 / 0";
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        progressText = tmp;
    }

    private Coroutine _feedbackCoroutine;

    void ShowFeedback(string text, Color color)
    {
        if (inputFeedbackText == null) return;
        inputFeedbackText.text = text;
        inputFeedbackText.color = color;

        // StopAllCoroutines 대신 피드백 코루틴만 개별 취소
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = StartCoroutine(ClearFeedbackAfter(0.4f));
    }
    IEnumerator ClearFeedbackAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (inputFeedbackText != null)
            inputFeedbackText.text = "";
    }

    Sprite GetArrowSprite(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.UpArrow: return arrowUp;
            case KeyCode.DownArrow: return arrowDown;
            case KeyCode.LeftArrow: return arrowLeft;
            case KeyCode.RightArrow: return arrowRight;
            default: return null;
        }
    }
}