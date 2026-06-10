using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class ResultManager : MonoBehaviour
{
    [Header("MVP UI")]
    [SerializeField] private TMP_Text _mvpNameText;
    [SerializeField] private TMP_Text _mvpScoreText;
    [SerializeField] private Image _mvpCrown;

    [Header("Player Stats - 4명")]
    [SerializeField] private PlayerStatUI[] _playerStatUIs;

    [Header("Buttons")]
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _lobbyButton;

    [System.Serializable]
    public class PlayerStatUI
    {
        public GameObject container;
        public TMP_Text nameText;
        public TMP_Text trashText;
        public TMP_Text seedText;
        public TMP_Text tradeText;
        public TMP_Text scoreText;
        public Image crownIcon;
    }

    [System.Serializable]
    private class PlayerResult
    {
        public string playerName;
        public int trashCollected;
        public int seedsPlanted;   // 꾸미기 기여 점수로 재사용
        public int npcTrades;
        public int batteryCharges;
        public int totalScore;

        public void CalculateScore()
        {
            // seedsPlanted = 꾸미기 기여 점수 (이미 최종 점수)
            totalScore = seedsPlanted > 0
                ? seedsPlanted
                : (trashCollected * 10) + (npcTrades * 5) + (batteryCharges * 3);
        }
    }

    private void Start()
    {
        // ✅ Result BGM 재생
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGMForScene("ResultScene");
        }

        // 버튼 이벤트 등록
        if (_retryButton != null)
            _retryButton.onClick.AddListener(OnRetryClicked);

        if (_lobbyButton != null)
            _lobbyButton.onClick.AddListener(OnLobbyClicked);

        // 결과 데이터 가져오기 및 표시
        FetchAndDisplayResults();
    }

    private void FetchAndDisplayResults()
    {
        List<PlayerResult> results = BuildResultsFromGameData();
        foreach (var r in results) r.CalculateScore();
        DisplayResults(results);
    }

    private List<PlayerResult> BuildResultsFromGameData()
    {
        var list = new List<PlayerResult>();

        int teamScore  = GameManager.FinalTeamDecorScore > 0
            ? GameManager.FinalTeamDecorScore
            : PlayerPrefs.GetInt("FinalDecorScore", 0);

        string[] names  = GameManager.FinalPlayerNames;
        int[]    scores = GameManager.FinalPlayerDecorScores;

        bool hasRealData = teamScore > 0;

        for (int i = 0; i < 4; i++)
        {
            string name = (names != null && i < names.Length && !string.IsNullOrEmpty(names[i]))
                ? names[i]
                : (i < 3 ? $"플레이어{i + 1}" : "AI봇");

            int contrib = (scores != null && i < scores.Length) ? scores[i] : 0;

            list.Add(new PlayerResult
            {
                playerName    = name,
                trashCollected = 0,
                seedsPlanted   = contrib,   // 꾸미기 기여 점수를 seedsPlanted 슬롯으로 재사용
                npcTrades      = 0,
                batteryCharges = 0
            });
        }

        // 실제 데이터가 없으면 팀 점수를 균등 배분해 표시
        if (!hasRealData || scores == null || scores.Length == 0)
        {
            int perPlayer = teamScore / 4;
            for (int i = 0; i < list.Count; i++)
                list[i].seedsPlanted = perPlayer;
        }

        return list;
    }

    private void DisplayResults(List<PlayerResult> results)
    {
        PlayerResult mvp = results.OrderByDescending(p => p.totalScore).First();

        if (_mvpNameText != null)
            _mvpNameText.text = $"🏆 MVP: {mvp.playerName}";

        if (_mvpScoreText != null)
            _mvpScoreText.text = $"{mvp.totalScore}점";

        for (int i = 0; i < results.Count && i < _playerStatUIs.Length; i++)
        {
            var ui = _playerStatUIs[i];
            var result = results[i];

            if (ui.container != null)
                ui.container.SetActive(true);

            ui.nameText.text = result.playerName;
            ui.trashText.text = result.trashCollected > 0 ? $"수거: {result.trashCollected}개" : "";
            ui.seedText.text = $"꾸미기: {result.seedsPlanted}pt";
            ui.tradeText.text = result.npcTrades > 0 ? $"거래: {result.npcTrades}회" : "";
            ui.scoreText.text = $"총점: {result.totalScore}pt";

            bool isMVP = result.playerName == mvp.playerName;
            if (ui.crownIcon != null)
                ui.crownIcon.gameObject.SetActive(isMVP);

            if (isMVP)
            {
                ui.nameText.color = Color.yellow;
                ui.nameText.text = "[MVP] " + result.playerName;
            }
            else
            {
                ui.nameText.color = Color.white;
            }
        }

        for (int i = results.Count; i < _playerStatUIs.Length; i++)
        {
            if (_playerStatUIs[i].container != null)
                _playerStatUIs[i].container.SetActive(false);
        }
    }

    private void OnRetryClicked()
    {
        // ✅ 버튼 클릭 사운드
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        Debug.Log("다시 하기");
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnLobbyClicked()
    {
        // ✅ 버튼 클릭 사운드
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        Debug.Log("로비로 이동");
        SceneManager.LoadScene("LobbyScene");
    }
}