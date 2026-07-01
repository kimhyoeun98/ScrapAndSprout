using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SignUpUI : MonoBehaviour
{
	[Header("── Input Fields ──")]
	[Tooltip("아이디 입력창")]
	public TMP_InputField idInput;

	[Tooltip("닉네임 입력창")]
	public TMP_InputField nicknameInput;

	[Tooltip("이메일 입력창")]
	public TMP_InputField emailInput;

	[Tooltip("비밀번호 입력창")]
	public TMP_InputField passwordInput;

	[Tooltip("비밀번호 확인 입력창")]
	public TMP_InputField passwordConfirmInput;

	[Header("── Buttons ──")]
	[Tooltip("아이디 중복확인 버튼")]
	public Button checkIdButton;

	[Tooltip("가입하기 버튼")]
	public Button signUpButton;

	[Tooltip("뒤로가기 버튼")]
	public Button backButton;

	[Header("── Status Display ──")]
	[Tooltip("상태 메시지 텍스트 (StatusText)")]
	public TextMeshProUGUI statusText;

	[Header("── Panel 전환 ──")]
	[Tooltip("뒤로가기 시 다시 보여줄 LoginPanel")]
	public GameObject loginPanel;

	private bool _idChecked;

	private void Start()
	{
		if (signUpButton != null)
		{
			signUpButton.onClick.AddListener(OnSignUpButtonClicked);
		}
		if (backButton != null)
		{
			backButton.onClick.AddListener(OnBackButtonClicked);
		}
		if (checkIdButton != null)
		{
			checkIdButton.onClick.AddListener(OnCheckIdButtonClicked);
		}
		if (statusText != null)
		{
			statusText.text = "";
		}
	}

	private void OnSignUpButtonClicked()
	{
		Debug.Log("[SignUpUI] 가입하기 버튼 클릭");
		string text = idInput.text.Trim();
		string text2 = nicknameInput.text.Trim();
		string text3 = emailInput.text.Trim();
		string text4 = passwordInput.text;
		string text5 = passwordConfirmInput.text;
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text3) || string.IsNullOrEmpty(text4) || string.IsNullOrEmpty(text5))
		{
			ShowStatus("모든 항목을 입력해주세요.", Color.red);
			return;
		}
		if (string.IsNullOrEmpty(text2))
		{
			text2 = text;
		}
		if (text4 != text5)
		{
			ShowStatus("비밀번호가 일치하지 않습니다.", Color.red);
			return;
		}
		if (ApiManager.Instance == null)
		{
			ShowStatus("서버 연결 오류: ApiManager 없음", Color.red);
			Debug.LogError("[SignUpUI] ApiManager.Instance가 null!");
			return;
		}
		SignUpRequest request = new SignUpRequest
		{
			user_id = text,
			user_name = text2,
			email = text3,
			password = text4
		};
		ShowStatus("가입 처리 중...", Color.yellow);
		ApiManager.Instance.SignUp(request, OnSignUpSuccess, OnSignUpFail);
	}

	private void OnSignUpSuccess(SignUpResponse response)
	{
		Debug.Log("[SignUpUI] 가입 성공: " + response.message);
		ShowStatus("가입 완료! 로그인해주세요.", Color.green);
		Invoke("OnBackButtonClicked", 1f);
	}

	private void OnSignUpFail(string errorMessage)
	{
		Debug.LogWarning("[SignUpUI] 가입 실패: " + errorMessage);
		ShowStatus("가입 실패: 다시 시도해주세요.", Color.red);
	}

	private void OnCheckIdButtonClicked()
	{
		string text = idInput.text.Trim();
		if (string.IsNullOrEmpty(text))
		{
			ShowStatus("아이디를 입력해주세요.", Color.red);
			return;
		}
		if (ApiManager.Instance == null)
		{
			ShowStatus("서버 연결 오류: ApiManager 없음", Color.red);
			return;
		}
		ShowStatus("확인 중...", Color.yellow);
		ApiManager.Instance.CheckIdDuplicate(text, OnCheckIdResult, OnCheckIdFail);
	}

	private void OnCheckIdResult(CheckIdResponse response)
	{
		if (response.available)
		{
			_idChecked = true;
			ShowStatus("사용 가능한 아이디입니다.", Color.green);
		}
		else
		{
			_idChecked = false;
			ShowStatus("이미 사용 중인 아이디입니다.", Color.red);
		}
	}

	private void OnCheckIdFail(string errorMessage)
	{
		_idChecked = false;
		ShowStatus("중복확인 실패: 다시 시도해주세요.", Color.red);
		Debug.LogWarning("[SignUpUI] 중복확인 실패: " + errorMessage);
	}

	private void OnBackButtonClicked()
	{
		Debug.Log("[SignUpUI] 뒤로가기 클릭 → LoginPanel로 전환");
		base.gameObject.SetActive(value: false);
		if (loginPanel != null)
		{
			loginPanel.SetActive(value: true);
		}
	}

	private void ShowStatus(string message, Color color)
	{
		if (statusText == null)
		{
			Debug.LogWarning("[SignUpUI] StatusText가 연결되지 않았습니다!");
			return;
		}
		statusText.text = message;
		statusText.color = color;
	}
}
