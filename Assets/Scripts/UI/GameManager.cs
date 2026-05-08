using UnityEngine;
using TMPro;
using System.Collections;
using Fusion;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 시작하자마자 전부 숨기기 — Spawned() 전에 미리 처리
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (roundClearPanel != null) roundClearPanel.SetActive(false);
        if (dayText != null) dayText.gameObject.SetActive(false);
        if (roundText != null) roundText.gameObject.SetActive(false);
    }
    

    [Header("── 시간 설정 ──")]
    public float secondsPerDay = 60f;
    public int totalDays = 3;

    [Header("── 할당량 설정 ──")]
    public int baseTrashGoal = 10;
    public int baseTreeGoal = 3;
    public int trashGoalIncrease = 5;
    public int treeGoalIncrease = 2;

    [Header("── HUD UI 연결 ──")]
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI trashGoalText;
    public TextMeshProUGUI treeGoalText;
    public TextMeshProUGUI roundText;

    [Header("── 패널 연결 ──")]
    public GameObject gameOverPanel;
    public GameObject roundClearPanel;
    public TextMeshProUGUI gameOverResultText;
    public TextMeshProUGUI roundClearResultText;

    // ⭐ Networked = 모든 클라이언트에 자동 동기화
    [Networked] private float _remainingTime { get; set; }
    [Networked] private int _collectedTrash { get; set; }
    [Networked] private int _plantedTrees { get; set; }
    [Networked] private bool _isGameRunning { get; set; }
    [Networked] private int _currentRound { get; set; }
    [Networked] private int _currentDay { get; set; }

    [Networked] private int _trashGoal { get; set; }
    [Networked] private int _treeGoal { get; set; }

    private Coroutine _dayTextCoroutine;
    private Coroutine _roundTextCoroutine;

    public override void Spawned()
    {
        // Host만 게임 시작
        if (!HasStateAuthority) return;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (roundClearPanel != null) roundClearPanel.SetActive(false);
        if (dayText != null) dayText.gameObject.SetActive(false);
        if (roundText != null) roundText.gameObject.SetActive(false);

        _currentRound = 1;
        StartRound(_currentRound);

        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    public override void FixedUpdateNetwork()
    {
        // Host만 타이머 감소
        if (!HasStateAuthority || !_isGameRunning) return;

        _remainingTime -= Runner.DeltaTime;

        float totalTime = secondsPerDay * totalDays;
        float elapsedTime = totalTime - _remainingTime;
        int newDay = Mathf.Min(Mathf.FloorToInt(elapsedTime / secondsPerDay) + 1, totalDays);

        if (newDay != _currentDay)
        {
            _currentDay = newDay;
            OnDayChanged(_currentDay);
        }

        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            OnTimeUp();
        }
    }

    private bool _gameOverShown = false;

    void Update()
    {
        if (Runner == null) return;
        if (_isGameRunning)
        {
            UpdateTimerUI();
            UpdateTrashGoalUI();
            UpdateTreeGoalUI();
        }
        if (!_isGameRunning && !_gameOverShown && _remainingTime <= 0f)
        {
            _gameOverShown = true;
            ShowGameOverPanel();
        }
    }

    void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            // 결과 텍스트 표시
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

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

    public void OnTrashCollected()
    {
        if (!_isGameRunning) return;
        _collectedTrash++;
        UpdateTrashGoalUI();
        CheckClearCondition();
    }

    public void OnTreePlanted()
    {
        if (!_isGameRunning) return;
        _plantedTrees++;
        UpdateTreeGoalUI();
        CheckClearCondition();
    }

    void CheckClearCondition()
    {
        if (_collectedTrash >= _trashGoal && _plantedTrees >= _treeGoal)
            OnRoundClear();
    }

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
                    $"Time Over!\n\nRound {_currentRound}\n\n" +
                    $"Trash [{trashStatus}] {_collectedTrash} / {_trashGoal}\n" +
                    $"Tree  [{treeStatus}] {_plantedTrees} / {_treeGoal}\n\nMission Failed...";
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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
                    $"Time Left: {remainingSeconds}s\n\nNext Round Goal\n" +
                    $"Trash {_trashGoal + trashGoalIncrease} + Tree {_treeGoal + treeGoalIncrease}";
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnNextRoundButtonClicked()
    {
        if (!HasStateAuthority) return;
        _currentRound++;
        StartRound(_currentRound);
        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    public void OnRestartButtonClicked()
    {
        if (!HasStateAuthority) return;
        _currentRound = 1;
        StartRound(_currentRound);
        if (_roundTextCoroutine != null) StopCoroutine(_roundTextCoroutine);
        _roundTextCoroutine = StartCoroutine(ShowRoundTransition(_currentRound));
    }

    private IEnumerator ShowRoundTransition(int round)
    {
        if (roundText == null) yield break;
        roundText.text = $"Round {round}";
        roundText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        roundText.gameObject.SetActive(false);
        _roundTextCoroutine = null;
    }

    private IEnumerator ShowDayTransition(int day)
    {
        if (dayText == null) yield break;
        dayText.text = $"Day {day}";
        dayText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        dayText.gameObject.SetActive(false);
        _dayTextCoroutine = null;
    }

    void UpdateAllUI() { UpdateTimerUI(); UpdateTrashGoalUI(); UpdateTreeGoalUI(); }

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

    // GameManager.cs 내부에 추가 (반드시 NetworkBehaviour를 상속받은 클래스여야 함)

    // RpcSources.All: 모든 클라이언트가 호출 가능
    // RpcTargets.StateAuthority: Host(서버)에서만 실행됨

    // 클라이언트가 쓰레기 판매 시 Host에게 카운트 증가 요청
    
    
    // [Networked] 변수는 Host만 수정 가능하므로 RPC 필요
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddCollectedTrash(int count)
    {
        for (int i = 0; i < count; i++)
            OnTrashCollected();
    }
}