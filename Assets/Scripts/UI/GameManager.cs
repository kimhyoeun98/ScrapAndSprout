using UnityEngine;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // ─────────────────────────────────────────
    //  게임 설정
    // ─────────────────────────────────────────

    [Header("── 시간 설정 ──")]
    public float secondsPerDay = 60f;
    public int totalDays = 3;

    [Header("── 할당량 설정 ──")]
    public int baseTrashGoal = 10;
    public int baseTreeGoal = 3;
    public int trashGoalIncrease = 5;
    public int treeGoalIncrease = 2;

    // ─────────────────────────────────────────
    //  UI 연결
    // ─────────────────────────────────────────

    [Header("── HUD UI 연결 ──")]
    public TextMeshProUGUI dayText;        // Day 전환 알림 전용
    public TextMeshProUGUI timerText;      // "02:45"
    public TextMeshProUGUI trashGoalText;  // "Trash: 7 / 10"
    public TextMeshProUGUI treeGoalText;   // "Tree: 2 / 3"
    public TextMeshProUGUI roundText;      // Round 전환 알림 전용

    [Header("── 패널 연결 ──")]
    public GameObject gameOverPanel;
    public GameObject roundClearPanel;
    public TextMeshProUGUI gameOverResultText;
    public TextMeshProUGUI roundClearResultText;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private int _currentRound = 1;
    private int _currentDay = 1;
    private float _remainingTime = 0f;
    private bool _isGameRunning = false;

    private int _trashGoal = 0;
    private int _treeGoal = 0;
    private int _collectedTrash = 0;
    private int _plantedTrees = 0;

    // 코루틴 참조 (중복 실행 방지)
    private Coroutine _dayTextCoroutine;
    private Coroutine _roundTextCoroutine;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (roundClearPanel != null) roundClearPanel.SetActive(false);
        if (dayText != null) dayText.gameObject.SetActive(false);
        if (roundText != null) roundText.gameObject.SetActive(false);

        StartRound(_currentRound);

        // ⭐ 게임 시작 시 Round 1 표시
        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    void Update()
    {
        if (!_isGameRunning) return;

        _remainingTime -= Time.deltaTime;

        float totalTime = secondsPerDay * totalDays;
        float elapsedTime = totalTime - _remainingTime;
        int newDay = Mathf.Min(Mathf.FloorToInt(elapsedTime / secondsPerDay) + 1, totalDays);

        if (newDay != _currentDay)
        {
            _currentDay = newDay;
            OnDayChanged(_currentDay);
        }

        UpdateTimerUI();

        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            OnTimeUp();
        }
    }

    // ─────────────────────────────────────────
    //  라운드 시작
    // ─────────────────────────────────────────

    void StartRound(int round)
    {
        _trashGoal = baseTrashGoal + (round - 1) * trashGoalIncrease;
        _treeGoal = baseTreeGoal + (round - 1) * treeGoalIncrease;
        _collectedTrash = 0;
        _plantedTrees = 0;
        _currentDay = 1;
        _remainingTime = secondsPerDay * totalDays;
        _isGameRunning = true;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (roundClearPanel != null) roundClearPanel.SetActive(false);
        if (dayText != null) dayText.gameObject.SetActive(false);
        if (roundText != null) roundText.gameObject.SetActive(false);

        Debug.Log($"[Round {round} Start] Trash: {_trashGoal} | Tree: {_treeGoal}");

        UpdateAllUI();
    }

    // ─────────────────────────────────────────
    //  외부 호출 함수
    // ─────────────────────────────────────────

    public void OnTrashCollected()
    {
        if (!_isGameRunning) return;
        _collectedTrash++;
        UpdateTrashGoalUI();
        Debug.Log($"[Quota] Trash {_collectedTrash}/{_trashGoal} | Tree {_plantedTrees}/{_treeGoal}");
        CheckClearCondition();
    }

    public void OnTreePlanted()
    {
        if (!_isGameRunning) return;
        _plantedTrees++;
        UpdateTreeGoalUI();
        Debug.Log($"[Quota] Trash {_collectedTrash}/{_trashGoal} | Tree {_plantedTrees}/{_treeGoal}");
        CheckClearCondition();
    }

    // ─────────────────────────────────────────
    //  클리어 조건 체크
    // ─────────────────────────────────────────

    void CheckClearCondition()
    {
        if (_collectedTrash >= _trashGoal && _plantedTrees >= _treeGoal)
            OnRoundClear();
    }

    // ─────────────────────────────────────────
    //  이벤트 핸들러
    // ─────────────────────────────────────────

    void OnDayChanged(int day)
    {
        if (_dayTextCoroutine != null) StopCoroutine(_dayTextCoroutine);
        _dayTextCoroutine = StartCoroutine(ShowDayTransition(day));
    }

    void OnTimeUp()
    {
        _isGameRunning = false;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverResultText != null)
            {
                string trashStatus = _collectedTrash >= _trashGoal ? "clear" : "fail";
                string treeStatus = _plantedTrees >= _treeGoal ? "clear" : "fail";

                gameOverResultText.text =
                    $"Time Over!\n\n" +
                    $"Round {_currentRound}\n\n" +
                    $"Trash [{trashStatus}] {_collectedTrash} / {_trashGoal}\n" +
                    $"Tree  [{treeStatus}] {_plantedTrees} / {_treeGoal}\n\n" +
                    $"Mission Failed...";
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log($"[GameOver] Trash: {_collectedTrash}/{_trashGoal} | Tree: {_plantedTrees}/{_treeGoal}");
    }

    void OnRoundClear()
    {
        _isGameRunning = false;
        int remainingSeconds = Mathf.CeilToInt(_remainingTime);

        if (roundClearPanel != null)
        {
            roundClearPanel.SetActive(true);

            if (roundClearResultText != null)
            {
                roundClearResultText.text =
                    $"Round {_currentRound} Clear!\n\n" +
                    $"Trash: {_collectedTrash} / {_trashGoal}\n" +
                    $"Tree:  {_plantedTrees} / {_treeGoal}\n" +
                    $"Time Left: {remainingSeconds}s\n\n" +
                    $"Next Round Goal\n" +
                    $"Trash {_trashGoal + trashGoalIncrease} + " +
                    $"Tree {_treeGoal + treeGoalIncrease}";
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log($"[Round {_currentRound} Clear!] Remaining: {remainingSeconds}s");
    }

    // ─────────────────────────────────────────
    //  버튼 연결 함수
    // ─────────────────────────────────────────

    public void OnNextRoundButtonClicked()
    {
        _currentRound++;
        StartRound(_currentRound);

        // ⭐ 라운드 전환 알림 표시
        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    public void OnRestartButtonClicked()
    {
        _currentRound = 1;
        StartRound(_currentRound);

        // ⭐ 재시작 시 Round 1 표시
        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    // ─────────────────────────────────────────
    //  전환 연출 코루틴
    // ─────────────────────────────────────────

    /// <summary>
    /// 화면 중앙에 "Round 1" 을 2초간 표시 후 숨깁니다.
    /// </summary>
    private IEnumerator ShowRoundTransition(int round)
    {
        if (roundText == null)
        {
            Debug.LogWarning("[GameManager] roundText가 연결되지 않았습니다!");
            yield break;
        }

        roundText.text = $"Round {round}";
        roundText.gameObject.SetActive(true);
        Debug.Log($"[GameManager] Round {round} 표시 시작");

        yield return new WaitForSeconds(2f);

        roundText.gameObject.SetActive(false);
        _roundTextCoroutine = null;
    }

    /// <summary>
    /// 화면 중앙에 "Day 2" 를 2초간 표시 후 숨깁니다.
    /// </summary>
    private IEnumerator ShowDayTransition(int day)
    {
        if (dayText == null) yield break;

        dayText.text = $"Day {day}";
        dayText.gameObject.SetActive(true);

        yield return new WaitForSeconds(2f);

        dayText.gameObject.SetActive(false);
        _dayTextCoroutine = null;
    }

    // ─────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────

    void UpdateAllUI()
    {
        UpdateTimerUI();
        UpdateTrashGoalUI();
        UpdateTreeGoalUI();
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(_remainingTime / 60f);
        int seconds = Mathf.FloorToInt(_remainingTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = _remainingTime <= 30f ? Color.red : Color.white;
    }

    void UpdateTrashGoalUI()
    {
        if (trashGoalText != null)
            trashGoalText.text = $"Trash: {_collectedTrash} / {_trashGoal}";
    }

    void UpdateTreeGoalUI()
    {
        if (treeGoalText != null)
            treeGoalText.text = $"Tree: {_plantedTrees} / {_treeGoal}";
    }
}