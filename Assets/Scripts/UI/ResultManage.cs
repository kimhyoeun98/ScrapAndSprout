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
        public int seedsPlanted;
        public int npcTrades;
        public int batteryCharges;
        public int totalScore;

        public void CalculateScore()
        {
            totalScore = (trashCollected * 10) +
                        (seedsPlanted * 20) +
                        (npcTrades * 5) +
                        (batteryCharges * 3);
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
        List<PlayerResult> results = CreateDummyResults();

        foreach (var result in results)
        {
            result.CalculateScore();
        }

        DisplayResults(results);
    }

    private List<PlayerResult> CreateDummyResults()
    {
        return new List<PlayerResult>
        {
            new PlayerResult
            {
                playerName = "효은",
                trashCollected = 35,
                seedsPlanted = 12,
                npcTrades = 8,
                batteryCharges = 5
            },
            new PlayerResult
            {
                playerName = "재엽",
                trashCollected = 28,
                seedsPlanted = 15,
                npcTrades = 6,
                batteryCharges = 4
            },
            new PlayerResult
            {
                playerName = "민제",
                trashCollected = 42,
                seedsPlanted = 10,
                npcTrades = 7,
                batteryCharges = 6
            },
            new PlayerResult
            {
                playerName = "AI봇",
                trashCollected = 20,
                seedsPlanted = 8,
                npcTrades = 4,
                batteryCharges = 3
            }
        };
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
            ui.trashText.text = $"쓰레기: {result.trashCollected}개";
            ui.seedText.text = $"씨앗: {result.seedsPlanted}개";
            ui.tradeText.text = $"거래: {result.npcTrades}회";
            ui.scoreText.text = $"총점: {result.totalScore}";

            bool isMVP = result.playerName == mvp.playerName;
            if (ui.crownIcon != null)
                ui.crownIcon.gameObject.SetActive(isMVP);

            if (isMVP)
            {
                ui.nameText.color = Color.yellow;
                ui.nameText.text = "b " + result.playerName;
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