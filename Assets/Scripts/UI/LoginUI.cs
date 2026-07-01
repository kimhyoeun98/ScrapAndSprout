using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
	[Header("── Input Fields ──")]
	[Tooltip("사용자 ID 입력창 (UsernameInput)")]
	public TMP_InputField usernameInput;

	[Tooltip("비밀번호 입력창 (PasswordInput)")]
	public TMP_InputField passwordInput;

	[Header("── Buttons ──")]
	[Tooltip("로그인 버튼")]
	public Button loginButton;

	[Tooltip("회원가입 버튼 (선택사항)")]
	public Button signInButton;

	[Header("── Status Display ──")]
	[Tooltip("상태 메시지 텍스트 (StatusText)")]
	public TextMeshProUGUI statusText;

	[Tooltip("로딩 아이콘 (선택사항)")]
	public GameObject loadingIcon;

	[Header("── 테스트 설정 ──")]
	[Tooltip("개발 중 빠른 테스트용 기본 ID")]
	public string defaultUsername = "test";

	[Tooltip("개발 중 빠른 테스트용 기본 PW")]
	public string defaultPassword = "1234";

	[Tooltip("자동으로 기본값 입력")]
	public bool autoFillTestValues = true;

	[Header("── Panel 전환 ──")]
	public GameObject signUpPanel;

	private void Start()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[LoginUI] 초기화 시작");
		if (PhotonManager.Instance != null)
		{
			Debug.Log("[LoginUI] ✅ PhotonManager 확인됨");
		}
		else
		{
			Debug.LogWarning("[LoginUI] ⚠\ufe0f PhotonManager가 없습니다!");
			Debug.LogWarning("[LoginUI] LoginScene Hierarchy에 PhotonManager 오브젝트를 추가하세요!");
		}
		AudioManager.Instance?.PlayBGMForScene("LobbyScene");
		Debug.Log("═══════════════════════════════════════════");
		if (loginButton != null)
		{
			loginButton.onClick.AddListener(OnLoginButtonClicked);
			loginButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (signInButton != null)
		{
			signInButton.onClick.AddListener(OnSignInButtonClicked);
			signInButton.onClick.AddListener(delegate
			{
				AudioManager.Instance?.PlayButtonClick();
			});
		}
		if (loadingIcon != null)
		{
			loadingIcon.SetActive(value: false);
		}
		if (statusText != null)
		{
			statusText.text = "";
		}
		if (autoFillTestValues)
		{
			if (usernameInput != null)
			{
				usernameInput.text = defaultUsername;
			}
			if (passwordInput != null)
			{
				passwordInput.text = defaultPassword;
			}
		}
		if (passwordInput != null)
		{
			passwordInput.onSubmit.AddListener(delegate
			{
				OnLoginButtonClicked();
			});
		}
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Tab))
		{
			if (usernameInput != null && usernameInput.isFocused)
			{
				FocusField(passwordInput);
			}
			else if (passwordInput != null && passwordInput.isFocused)
			{
				FocusField(usernameInput);
			}
			else
			{
				FocusField(usernameInput);
			}
		}
	}

	private void FocusField(TMP_InputField field)
	{
		if (!(field == null) && field.interactable)
		{
			field.Select();
			field.ActivateInputField();
			field.caretPosition = field.text.Length;
		}
	}

	private void OnLoginButtonClicked()
	{
		Debug.Log("[LoginUI] 로그인 버튼 클릭");
		string text = usernameInput.text.Trim();
		string text2 = passwordInput.text;
		if (string.IsNullOrEmpty(text))
		{
			ShowStatus("Insert ID!", Color.red);
			Debug.LogWarning("[LoginUI] ID가 비어있음");
		}
		else if (string.IsNullOrEmpty(text2))
		{
			ShowStatus("Insert PW!", Color.red);
			Debug.LogWarning("[LoginUI] 비밀번호가 비어있음");
		}
		else
		{
			RequestLogin(text, text2);
		}
	}

	private void RequestLogin(string username, string password)
	{
		Debug.Log("───────────────────────────────────────────");
		Debug.Log("[LoginUI] Spring 서버 로그인 요청 시작");
		if (ApiManager.Instance == null)
		{
			ShowStatus("Server connect error: ApiManager not found.", Color.red);
			Debug.LogError("[LoginUI] ❌ ApiManager.Instance가 null!");
			return;
		}
		SetUIInteractable(interactable: false);
		ShowStatus("Login...", Color.yellow);
		if (loadingIcon != null)
		{
			loadingIcon.SetActive(value: true);
		}
		LoginRequest request = new LoginRequest
		{
			user_name = username,
			password = password
		};
		Debug.Log("[LoginUI] 요청 정보:");
		Debug.Log("  - user_name: " + username);
		Debug.Log("───────────────────────────────────────────");
		ApiManager.Instance.Login(request, OnLoginSuccess, OnLoginFail);
	}

	private void OnLoginSuccess(LoginResponse response)
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[LoginUI] ✅ Spring 서버 로그인 성공!");
		Debug.Log("  - user_id: " + response.user_id);
		Debug.Log("  - user_name: " + response.user_name);
		ShowStatus("Welcome, " + response.user_name + "!", Color.green);
		if (loadingIcon != null)
		{
			loadingIcon.SetActive(value: false);
		}
		PlayerPrefs.SetString("user_name", response.user_name);
		PlayerPrefs.SetString("user_id", response.user_id);
		PlayerPrefs.Save();
		PhotonManager.Instance?.SetLocalPlayerName(response.user_name);
		Debug.Log("[LoginUI] PlayerPrefs 저장 완료:");
		Debug.Log("  - user_name: " + response.user_name);
		Debug.Log("  - user_id: " + response.user_id);
		Debug.Log("═══════════════════════════════════════════");
		Invoke("GoToNextScene", 1f);
	}

	private void OnLoginFail(string errorMessage)
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.LogWarning("[LoginUI] ❌ Spring 서버 로그인 실패!");
		Debug.LogWarning("  - Error: " + errorMessage);
		Debug.Log("═══════════════════════════════════════════");
		ShowStatus("Failed Login! Check ID/PW.", Color.red);
		SetUIInteractable(interactable: true);
		if (loadingIcon != null)
		{
			loadingIcon.SetActive(value: false);
		}
	}

	private void GoToNextScene()
	{
		if (Application.CanStreamedLevelBeLoaded("LobbyScene"))
		{
			Debug.Log("───────────────────────────────────────────");
			Debug.Log("[LoginUI] LobbyScene으로 이동");
			Debug.Log("[LoginUI] → PhotonManager는 DontDestroyOnLoad로 유지됨");
			Debug.Log("[LoginUI] → LobbyScene에서 Photon 세션 시작 대기");
			Debug.Log("───────────────────────────────────────────");
			SceneManager.LoadScene("LobbyScene");
		}
		else
		{
			Debug.LogError("[LoginUI] ❌ LobbyScene이 Build Settings에 없습니다!");
			ShowStatus("Error: LobbyScene not found!", Color.red);
		}
	}

	private void OnSignInButtonClicked()
	{
		base.gameObject.SetActive(value: false);
		if (signUpPanel != null)
		{
			signUpPanel.SetActive(value: true);
		}
	}

	private void ShowStatus(string message, Color color)
	{
		if (statusText == null)
		{
			Debug.LogWarning("[LoginUI] StatusText가 연결되지 않았습니다!");
			return;
		}
		statusText.text = message;
		statusText.color = color;
	}

	private void SetUIInteractable(bool interactable)
	{
		if (usernameInput != null)
		{
			usernameInput.interactable = interactable;
		}
		if (passwordInput != null)
		{
			passwordInput.interactable = interactable;
		}
		if (loginButton != null)
		{
			loginButton.interactable = interactable;
		}
		if (signInButton != null && signInButton.interactable)
		{
			signInButton.interactable = interactable;
		}
	}

	public void TestLogout()
	{
		if (ApiManager.Instance != null)
		{
			ApiManager.Instance.Logout();
			ShowStatus("로그아웃되었습니다.", Color.white);
			SetUIInteractable(interactable: true);
			if (usernameInput != null)
			{
				usernameInput.text = "";
			}
			if (passwordInput != null)
			{
				passwordInput.text = "";
			}
			PlayerPrefs.DeleteKey("user_name");
			PlayerPrefs.DeleteKey("user_id");
			PlayerPrefs.DeleteKey("RoomCode");
			PlayerPrefs.DeleteKey("RoomMode");
			PlayerPrefs.Save();
			Debug.Log("[LoginUI] 로그아웃 완료 - PlayerPrefs 정리됨");
		}
	}
}
