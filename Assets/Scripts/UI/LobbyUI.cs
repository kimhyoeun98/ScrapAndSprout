using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
	[Header("── Buttons ──")]
	public Button playButton;

	public Button joinInButton;

	public Button tutorialButton;

	public Button exitButton;

	[Tooltip("이어하기 — 세이브가 있을 때만 표시되며, 호스트로 이어서 시작")]
	public Button continueButton;

	[Header("── UI Elements ──")]
	public TextMeshProUGUI nicknameText;

	[Header("── 방 이름 입력 팝업 ──")]
	public GameObject roomNamePanel;

	public TextMeshProUGUI titleText;

	public TMP_InputField roomNameInput;

	public Button confirmButton;

	public Button cancelButton;

	private GameMode _currentMode;

	private bool _isConnecting;

	private void Start()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[LobbyUI] 초기화 시작");
		if (PhotonManager.Instance != null)
		{
			Debug.Log("[LobbyUI] ✅ PhotonManager 확인됨");
		}
		else
		{
			Debug.LogError("[LobbyUI] ❌ PhotonManager가 없습니다!");
		}
		Debug.Log("═══════════════════════════════════════════");
		if (roomNamePanel != null)
		{
			roomNamePanel.SetActive(value: false);
		}
		if (playButton != null)
		{
			playButton.onClick.AddListener(OnPlayClicked);
			playButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (joinInButton != null)
		{
			joinInButton.onClick.AddListener(OnJoinInClicked);
			joinInButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (tutorialButton != null)
		{
			tutorialButton.onClick.AddListener(OnTutorialClicked);
			tutorialButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (exitButton != null)
		{
			exitButton.onClick.AddListener(OnExitClicked);
			exitButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (continueButton != null)
		{
			continueButton.onClick.AddListener(OnContinueClicked);
			continueButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
			continueButton.gameObject.SetActive(SaveManager.HasSave);
		}
		if (confirmButton != null)
		{
			confirmButton.onClick.AddListener(OnConfirmClicked);
			confirmButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (cancelButton != null)
		{
			cancelButton.onClick.AddListener(OnCancelClicked);
			cancelButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		UpdateNickname();
		AudioManager.Instance?.PlayBGMForScene("LobbyScene");
		Debug.Log("[LobbyUI] 로비 씬 로드 완료");
	}

	private void UpdateNickname()
	{
		if (nicknameText == null)
		{
			Debug.LogWarning("[LobbyUI] NicknameText가 연결되지 않았습니다!");
		}
		else if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
		{
			nicknameText.text = ApiManager.Instance.userName;
			PlayerPrefs.SetString("user_name", ApiManager.Instance.userName);
			PlayerPrefs.Save();
			Debug.Log("[LobbyUI] 닉네임 표시: " + ApiManager.Instance.userName);
		}
		else
		{
			nicknameText.text = "Guest";
			PlayerPrefs.SetString("user_name", "Guest");
			PlayerPrefs.Save();
			Debug.LogWarning("[LobbyUI] 로그인 정보 없음 → Guest 표시");
		}
	}

	private void OnPlayClicked()
	{
		SaveManager.BeginNewGame();
		_currentMode = GameMode.Host;
		titleText.text = "방 생성";
		roomNameInput.text = "";
		roomNameInput.placeholder.GetComponent<TextMeshProUGUI>().text = "방 이름 입력";
		roomNamePanel.SetActive(value: true);
		Debug.Log("[LobbyUI] Play 버튼 클릭 - Host 모드");
	}

	private void OnContinueClicked()
	{
		if (!SaveManager.BeginContinue())
		{
			Debug.LogWarning("[LobbyUI] 이어할 세이브가 없습니다");
			if (continueButton != null)
			{
				continueButton.gameObject.SetActive(value: false);
			}
			return;
		}
		_currentMode = GameMode.Host;
		if (roomNameInput != null)
		{
			roomNameInput.text = "";
		}
		Debug.Log("[LobbyUI] 이어하기 클릭 - Host로 복원 시작");
		OnConfirmClicked();
	}

	private void OnJoinInClicked()
	{
		_currentMode = GameMode.Client;
		titleText.text = "방 코드 입력";
		roomNameInput.text = "";
		roomNameInput.placeholder.GetComponent<TextMeshProUGUI>().text = "4자리 코드 입력";
		roomNamePanel.SetActive(value: true);
		Debug.Log("[LobbyUI] Join 버튼 클릭 - Client 모드");
	}

	private async void OnConfirmClicked()
	{
		if (_isConnecting)
		{
			Debug.LogWarning("[로비] 이미 접속 중입니다!");
			return;
		}
		roomNamePanel.SetActive(value: false);
		if (_currentMode == GameMode.Host)
		{
			string text = roomNameInput.text.Trim();
			if (string.IsNullOrEmpty(text))
			{
				text = (ApiManager.Instance?.userName ?? "Guest") + "의 방";
			}
			string text2 = UnityEngine.Random.Range(1000, 9999).ToString();
			PlayerPrefs.SetString("RoomName", text);
			PlayerPrefs.SetString("RoomCode", text2);
			PlayerPrefs.SetString("RoomMode", "Create");
			PlayerPrefs.Save();
			Debug.Log("═══════════════════════════════════════════");
			Debug.Log("[로비] 방 생성 요청");
			Debug.Log("  - 방 이름: " + text);
			Debug.Log("  - 방 코드: " + text2);
			Debug.Log("  - 모드: Host");
			Debug.Log("[로비] PlayerPrefs 저장 확인:");
			Debug.Log("  - RoomName: " + PlayerPrefs.GetString("RoomName"));
			Debug.Log("  - RoomCode: " + PlayerPrefs.GetString("RoomCode"));
			if (PhotonManager.Instance == null)
			{
				Debug.LogError("[로비] ❌ PhotonManager.Instance가 null!");
				Debug.Log("═══════════════════════════════════════════");
				return;
			}
			_isConnecting = true;
			SetButtonsInteractable(interactable: false);
			Debug.Log("[로비] PhotonManager.StartHostWithRoom 호출 시작...");
			try
			{
				await PhotonManager.Instance.StartHostWithRoom(text2);
				Debug.Log("[로비] ✅ PhotonManager.StartHostWithRoom 완료");
				Debug.Log("[로비] → waitingRoomScene 전환은 PhotonManager가 처리");
				Debug.Log("═══════════════════════════════════════════");
				return;
			}
			catch (Exception ex)
			{
				Debug.LogError("═══════════════════════════════════════════");
				Debug.LogError("[로비] ❌ 방 생성 실패!");
				Debug.LogError("  - 에러: " + ex.Message);
				Debug.LogError("═══════════════════════════════════════════");
				_isConnecting = false;
				SetButtonsInteractable(interactable: true);
				return;
			}
		}
		if (string.IsNullOrWhiteSpace(roomNameInput.text))
		{
			Debug.LogWarning("[로비] 방 코드를 입력해주세요!");
			roomNamePanel.SetActive(value: true);
			return;
		}
		string text3 = roomNameInput.text.Trim();
		PlayerPrefs.SetString("RoomCode", text3);
		PlayerPrefs.SetString("RoomMode", "Join");
		PlayerPrefs.Save();
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[로비] 방 참여 요청");
		Debug.Log("  - 방 코드: " + text3);
		Debug.Log("  - 모드: Client");
		if (PhotonManager.Instance == null)
		{
			Debug.LogError("[로비] ❌ PhotonManager.Instance가 null!");
			Debug.Log("═══════════════════════════════════════════");
			return;
		}
		_isConnecting = true;
		SetButtonsInteractable(interactable: false);
		Debug.Log("[로비] PhotonManager.StartClientWithRoom 호출 시작...");
		try
		{
			await PhotonManager.Instance.StartClientWithRoom(text3);
			Debug.Log("[로비] ✅ PhotonManager.StartClientWithRoom 완료");
			Debug.Log("[로비] → waitingRoomScene 전환은 PhotonManager가 처리");
			Debug.Log("═══════════════════════════════════════════");
		}
		catch (Exception ex2)
		{
			Debug.LogError("═══════════════════════════════════════════");
			Debug.LogError("[로비] ❌ 방 참여 실패!");
			Debug.LogError("  - 에러: " + ex2.Message);
			Debug.LogError("═══════════════════════════════════════════");
			_isConnecting = false;
			SetButtonsInteractable(interactable: true);
		}
	}

	private void OnCancelClicked()
	{
		roomNamePanel.SetActive(value: false);
		roomNameInput.text = "";
		Debug.Log("[LobbyUI] 팝업 취소");
	}

	private void OnTutorialClicked()
	{
		Debug.Log("[LobbyUI] Tutorial 버튼 클릭");
		LoadScene("TutorialScene");
	}

	private void OnExitClicked()
	{
		Debug.Log("[LobbyUI] Exit 버튼 클릭 → 로그아웃");
		if (ApiManager.Instance != null)
		{
			ApiManager.Instance.Logout();
		}
		LoadScene("LoginScene");
	}

	private void LoadScene(string sceneName)
	{
		if (Application.CanStreamedLevelBeLoaded(sceneName))
		{
			Debug.Log("[LobbyUI] " + sceneName + " 씬으로 이동");
			SceneManager.LoadScene(sceneName);
		}
		else
		{
			Debug.LogWarning("[LobbyUI] " + sceneName + " 씬이 Build Settings에 없습니다!");
		}
	}

	private void SetButtonsInteractable(bool interactable)
	{
		if (playButton != null)
		{
			playButton.interactable = interactable;
		}
		if (joinInButton != null)
		{
			joinInButton.interactable = interactable;
		}
		if (tutorialButton != null)
		{
			tutorialButton.interactable = interactable;
		}
		if (exitButton != null)
		{
			exitButton.interactable = interactable;
		}
		if (confirmButton != null)
		{
			confirmButton.interactable = interactable;
		}
		if (continueButton != null)
		{
			continueButton.interactable = interactable;
		}
	}

	private void OnValidate()
	{
		if (playButton == null)
		{
			Debug.LogWarning("[LobbyUI] PlayButton이 연결되지 않았습니다!");
		}
		if (joinInButton == null)
		{
			Debug.LogWarning("[LobbyUI] JoinInButton이 연결되지 않았습니다!");
		}
		if (tutorialButton == null)
		{
			Debug.LogWarning("[LobbyUI] TutorialButton이 연결되지 않았습니다!");
		}
		if (exitButton == null)
		{
			Debug.LogWarning("[LobbyUI] ExitButton이 연결되지 않았습니다!");
		}
		if (nicknameText == null)
		{
			Debug.LogWarning("[LobbyUI] NicknameText가 연결되지 않았습니다!");
		}
		if (roomNamePanel == null)
		{
			Debug.LogWarning("[LobbyUI] RoomNamePanel이 연결되지 않았습니다!");
		}
		if (roomNameInput == null)
		{
			Debug.LogWarning("[LobbyUI] RoomNameInput이 연결되지 않았습니다!");
		}
	}
}
