using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
	public float refreshInterval = 0.5f;

	[Header("── UI 참조 ──")]
	[SerializeField]
	private GameObject panel;

	[SerializeField]
	private TextMeshProUGUI[] rowTexts = new TextMeshProUGUI[4];

	[SerializeField]
	private Image[] rowBGs = new Image[4];

	[SerializeField]
	private Button continueButton;

	[SerializeField]
	private Button exitButton;

	[SerializeField]
	private Button achievementButton;

	private float _timer;

	private bool _isOpen;

	private static readonly Color C_BODY = new Color(0.87f, 0.84f, 0.77f, 1f);

	private static readonly Color C_ROW_EVEN = new Color(0.1f, 0.14f, 0.08f, 0.7f);

	private static readonly Color C_ROW_ODD = new Color(0.07f, 0.1f, 0.05f, 0.4f);

	private static readonly Color C_ME_BG = new Color(0.28f, 0.24f, 0.04f, 0.7f);

	private static readonly Color C_ME_TEXT = new Color(1f, 0.88f, 0.2f, 1f);

	private static readonly string[] _charNames = new string[5] { "", "알파", "베타", "감마", "델타" };

	private void Start()
	{
		if (continueButton != null)
		{
			continueButton.onClick.AddListener(delegate
			{
				Toggle();
			});
		}
		if (exitButton != null)
		{
			exitButton.onClick.AddListener(OnExit);
		}
		if (panel != null)
		{
			panel.SetActive(value: false);
		}
		EnsureAchievementUI();
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape) && !TrashZoneChat.IsTyping)
		{
			if (AchievementUI.Instance != null && AchievementUI.Instance.IsOpen)
			{
				AchievementUI.Instance.Close();
			}
			else
			{
				Toggle();
			}
		}
		else if (_isOpen)
		{
			_timer -= Time.deltaTime;
			if (!(_timer > 0f))
			{
				_timer = refreshInterval;
				Refresh();
			}
		}
	}

	public void Toggle()
	{
		if (panel == null)
		{
			Debug.LogWarning("[LeaderboardUI.Toggle] panel이 NULL — Inspector에서 연결 필요");
			return;
		}
		_isOpen = !_isOpen;
		panel.SetActive(_isOpen);
		Cursor.visible = _isOpen;
		Cursor.lockState = CursorLockMode.None;
		if (_isOpen)
		{
			Refresh();
		}
	}

	private void EnsureAchievementUI()
	{
		if (AchievementUI.Instance == null && Object.FindFirstObjectByType<AchievementUI>() == null)
		{
			base.gameObject.AddComponent<AchievementUI>();
		}
		if (achievementButton != null)
		{
			achievementButton.onClick.RemoveListener(OpenAchievements);
			achievementButton.onClick.AddListener(OpenAchievements);
		}
		else
		{
			Debug.LogWarning("[LeaderboardUI] achievementButton 미연결 — Inspector에서 업적 버튼을 연결하세요.");
		}
	}

	private void OpenAchievements()
	{
		AchievementUI.Instance?.Open();
	}

	private void OnExit()
	{
		if (panel != null)
		{
			panel.SetActive(value: false);
		}
		_isOpen = false;
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LeaveRoom();
		}
		else
		{
			SceneManager.LoadScene("LobbyScene");
		}
	}

	private void Refresh()
	{
		if (PhotonManager.Instance == null)
		{
			return;
		}
		int num = -1;
		TrashCollector[] array = Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
		foreach (TrashCollector trashCollector in array)
		{
			if (trashCollector.HasInputAuthority)
			{
				num = (int)(trashCollector.characterType + 1);
				break;
			}
		}
		bool flag = GameManager.Instance != null && GameManager.Instance.Object != null && GameManager.Instance.Object.IsValid;
		List<(string, int, bool)> list = new List<(string, int, bool)>();
		for (int j = 0; j < 4; j++)
		{
			if (!PhotonManager.Instance.HasPlayerInSlot(j))
			{
				continue;
			}
			int playerCharacter = PhotonManager.Instance.GetPlayerCharacter(j);
			if (playerCharacter == 0)
			{
				continue;
			}
			string item = ((playerCharacter < _charNames.Length) ? _charNames[playerCharacter] : $"P{j + 1}");
			int num2 = 0;
			if (flag)
			{
				try
				{
					num2 = GameManager.Instance.PlayerDecorScores.Get(j);
				}
				catch
				{
					num2 = GameManager.LocalPlayerScores[j];
				}
			}
			else
			{
				num2 = GameManager.LocalPlayerScores[j];
			}
			list.Add((item, num2, playerCharacter == num));
		}
		list.Sort(((string name, int score, bool isMe) a, (string name, int score, bool isMe) b) => b.score.CompareTo(a.score));
		string[] array2 = new string[4] { "1위", "2위", "3위", "4위" };
		for (int num3 = 0; num3 < rowTexts.Length; num3++)
		{
			if (rowTexts[num3] == null)
			{
				continue;
			}
			if (num3 < list.Count)
			{
				(string, int, bool) tuple = list[num3];
				string item2 = tuple.Item1;
				int item3 = tuple.Item2;
				bool item4 = tuple.Item3;
				string arg = (item4 ? ("[나] " + item2) : item2);
				rowTexts[num3].text = $"{array2[num3]}   {arg}   {item3}pt";
				rowTexts[num3].color = (item4 ? C_ME_TEXT : C_BODY);
				if (rowBGs[num3] != null)
				{
					rowBGs[num3].color = (item4 ? C_ME_BG : ((num3 % 2 == 0) ? C_ROW_EVEN : C_ROW_ODD));
				}
			}
			else
			{
				rowTexts[num3].text = "";
				if (rowBGs[num3] != null)
				{
					rowBGs[num3].color = Color.clear;
				}
			}
		}
	}
}
