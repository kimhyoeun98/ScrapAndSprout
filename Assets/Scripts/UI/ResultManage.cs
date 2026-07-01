using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultManager : MonoBehaviour
{
	[Serializable]
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

	[Serializable]
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
			totalScore = ((seedsPlanted > 0) ? seedsPlanted : (trashCollected * 10 + npcTrades * 5 + batteryCharges * 3));
		}
	}

	[Header("MVP UI")]
	[SerializeField]
	private TMP_Text _mvpNameText;

	[SerializeField]
	private TMP_Text _mvpScoreText;

	[SerializeField]
	private Image _mvpCrown;

	[Header("Player Stats - 4명")]
	[SerializeField]
	private PlayerStatUI[] _playerStatUIs;

	[Header("Buttons")]
	[SerializeField]
	private Button _retryButton;

	[SerializeField]
	private Button _lobbyButton;

	private void Start()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlayBGMForScene("ResultScene");
		}
		if (_retryButton != null)
		{
			_retryButton.onClick.AddListener(OnRetryClicked);
		}
		if (_lobbyButton != null)
		{
			_lobbyButton.onClick.AddListener(OnLobbyClicked);
		}
		FetchAndDisplayResults();
	}

	private void FetchAndDisplayResults()
	{
		List<PlayerResult> list = BuildResultsFromGameData();
		foreach (PlayerResult item in list)
		{
			item.CalculateScore();
		}
		DisplayResults(list);
	}

	private List<PlayerResult> BuildResultsFromGameData()
	{
		List<PlayerResult> list = new List<PlayerResult>();
		int num = ((GameManager.FinalTeamDecorScore > 0) ? GameManager.FinalTeamDecorScore : PlayerPrefs.GetInt("FinalDecorScore", 0));
		string[] finalPlayerNames = GameManager.FinalPlayerNames;
		int[] finalPlayerDecorScores = GameManager.FinalPlayerDecorScores;
		bool flag = num > 0;
		for (int i = 0; i < 4; i++)
		{
			string playerName = ((finalPlayerNames != null && i < finalPlayerNames.Length && !string.IsNullOrEmpty(finalPlayerNames[i])) ? finalPlayerNames[i] : ((i < 3) ? $"플레이어{i + 1}" : "AI봇"));
			int seedsPlanted = ((finalPlayerDecorScores != null && i < finalPlayerDecorScores.Length) ? finalPlayerDecorScores[i] : 0);
			list.Add(new PlayerResult
			{
				playerName = playerName,
				trashCollected = 0,
				seedsPlanted = seedsPlanted,
				npcTrades = 0,
				batteryCharges = 0
			});
		}
		if (!flag || finalPlayerDecorScores == null || finalPlayerDecorScores.Length == 0)
		{
			int seedsPlanted2 = num / 4;
			for (int j = 0; j < list.Count; j++)
			{
				list[j].seedsPlanted = seedsPlanted2;
			}
		}
		return list;
	}

	private void DisplayResults(List<PlayerResult> results)
	{
		PlayerResult playerResult = results.OrderByDescending((PlayerResult p) => p.totalScore).First();
		if (_mvpNameText != null)
		{
			_mvpNameText.text = "\ud83c\udfc6 MVP: " + playerResult.playerName;
		}
		if (_mvpScoreText != null)
		{
			_mvpScoreText.text = $"{playerResult.totalScore}점";
		}
		for (int num = 0; num < results.Count && num < _playerStatUIs.Length; num++)
		{
			PlayerStatUI playerStatUI = _playerStatUIs[num];
			PlayerResult playerResult2 = results[num];
			if (playerStatUI.container != null)
			{
				playerStatUI.container.SetActive(value: true);
			}
			playerStatUI.nameText.text = playerResult2.playerName;
			playerStatUI.trashText.text = ((playerResult2.trashCollected > 0) ? $"수거: {playerResult2.trashCollected}개" : "");
			playerStatUI.seedText.text = $"꾸미기: {playerResult2.seedsPlanted}pt";
			playerStatUI.tradeText.text = ((playerResult2.npcTrades > 0) ? $"거래: {playerResult2.npcTrades}회" : "");
			playerStatUI.scoreText.text = $"총점: {playerResult2.totalScore}pt";
			bool flag = playerResult2.playerName == playerResult.playerName;
			if (playerStatUI.crownIcon != null)
			{
				playerStatUI.crownIcon.gameObject.SetActive(flag);
			}
			if (flag)
			{
				playerStatUI.nameText.color = Color.yellow;
				playerStatUI.nameText.text = "[MVP] " + playerResult2.playerName;
			}
			else
			{
				playerStatUI.nameText.color = Color.white;
			}
		}
		for (int num2 = results.Count; num2 < _playerStatUIs.Length; num2++)
		{
			if (_playerStatUIs[num2].container != null)
			{
				_playerStatUIs[num2].container.SetActive(value: false);
			}
		}
	}

	private void OnRetryClicked()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlayButtonClick();
		}
		Debug.Log("다시 하기");
		SceneManager.LoadScene("LobbyScene");
	}

	private void OnLobbyClicked()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlayButtonClick();
		}
		Debug.Log("로비로 이동");
		SceneManager.LoadScene("LobbyScene");
	}
}
