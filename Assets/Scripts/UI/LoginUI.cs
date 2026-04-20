using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 로그인 UI 관리 스크립트
/// 
/// [부착 위치] LoginPanel 오브젝트
/// 
/// [역할]
/// - ID/PW 입력 받기
/// - ApiManager.Login() 호출
/// - 로그인 성공 시 다음 씬으로 이동
/// - 에러 메시지 표시
/// </summary>
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

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 버튼 이벤트 연결
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginButtonClicked);

        if (signInButton != null)
        {
            // Sign in 버튼은 일단 비활성화 (향후 회원가입 기능 구현 시 활성화)
            signInButton.interactable = false;
            signInButton.onClick.AddListener(OnSignInButtonClicked);
        }

        // 로딩 아이콘 숨김
        if (loadingIcon != null)
            loadingIcon.SetActive(false);

        // 상태 텍스트 초기화
        if (statusText != null)
            statusText.text = "";

        // 테스트용 기본값 자동 입력
        if (autoFillTestValues)
        {
            if (usernameInput != null)
                usernameInput.text = defaultUsername;

            if (passwordInput != null)
                passwordInput.text = defaultPassword;
        }

        // 이미 로그인되어 있는지 확인
        CheckAutoLogin();

        // Enter 키로 로그인 (편의 기능)
        if (passwordInput != null)
        {
            passwordInput.onSubmit.AddListener((value) => OnLoginButtonClicked());
        }
    }

    // ─────────────────────────────────────────
    //  자동 로그인 체크
    // ─────────────────────────────────────────

    /// <summary>
    /// 이미 로그인되어 있으면 바로 다음 씬으로 이동
    /// </summary>
    void CheckAutoLogin()
    {
        if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
        {
            Debug.Log("[LoginUI] 이미 로그인되어 있음 → 자동으로 다음 씬 이동");
            ShowStatus($"Welcome, {ApiManager.Instance.userName}!", Color.green);

            // 1초 후 자동 이동
            Invoke(nameof(GoToNextScene), 1f);
        }
    }

    // ─────────────────────────────────────────
    //  로그인 버튼 클릭
    // ─────────────────────────────────────────

    void OnLoginButtonClicked()
    {
        // 입력값 검증
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("Insert ID!", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus("Insert PW!", Color.red);
            return;
        }

        // 로그인 요청
        RequestLogin(username, password);
    }

    // ─────────────────────────────────────────
    //  서버에 로그인 요청
    // ─────────────────────────────────────────

    void RequestLogin(string username, string password)
    {
        // ApiManager 존재 확인
        if (ApiManager.Instance == null)
        {
            ShowStatus("Sever connect error: ApiManager not found.", Color.red);
            Debug.LogError("[LoginUI] ApiManager.Instance가 null입니다!");
            return;
        }

        // UI 비활성화 (중복 클릭 방지)
        SetUIInteractable(false);
        ShowStatus("Login...", Color.yellow);

        if (loadingIcon != null)
            loadingIcon.SetActive(true);

        // ApiManager를 통해 로그인 요청
        LoginRequest request = new LoginRequest
        {
            user_name = username,
            password = password
        };

        Debug.Log($"[LoginUI] 로그인 요청: {username}");

        ApiManager.Instance.Login(
            request,
            OnLoginSuccess,
            OnLoginFail
        );
    }

    // ─────────────────────────────────────────
    //  로그인 성공 콜백
    // ─────────────────────────────────────────

    void OnLoginSuccess(LoginResponse response)
    {
        Debug.Log($"[LoginUI] 로그인 성공! → {response.user_name} (ID: {response.user_id})");

        ShowStatus($"Welcome, {response.user_name}!", Color.green);

        if (loadingIcon != null)
            loadingIcon.SetActive(false);

        // 1초 후 다음 씬으로 이동
        Invoke(nameof(GoToNextScene), 1f);
    }

    // ─────────────────────────────────────────
    //  로그인 실패 콜백
    // ─────────────────────────────────────────

    void OnLoginFail(string errorMessage)
    {
        Debug.LogWarning($"[LoginUI] 로그인 실패: {errorMessage}");

        ShowStatus("Failed Login! Check ID/PW.", Color.red);

        // UI 재활성화
        SetUIInteractable(true);

        if (loadingIcon != null)
            loadingIcon.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  다음 씬으로 이동
    // ─────────────────────────────────────────

    void GoToNextScene()
    {
        // LobbyScene으로 이동
        if (Application.CanStreamedLevelBeLoaded("LobbyScene"))
        {
            Debug.Log("[LoginUI] LobbyScene으로 이동");
            SceneManager.LoadScene("LobbyScene");
        }
        else
        {
            Debug.LogWarning("[LoginUI] LobbyScene이 Build Settings에 없습니다. LoginPanel을 숨깁니다.");
            gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    //  회원가입 버튼 (미구현)
    // ─────────────────────────────────────────

    void OnSignInButtonClicked()
    {
        ShowStatus("회원가입 기능은 준비중입니다.", Color.yellow);
        Debug.Log("[LoginUI] Sign in 버튼 클릭 (미구현)");

        // 향후 회원가입 UI 또는 별도 씬으로 이동
    }

    // ─────────────────────────────────────────
    //  UI 헬퍼 함수들
    // ─────────────────────────────────────────

    /// <summary>
    /// 상태 메시지를 표시합니다.
    /// </summary>
    void ShowStatus(string message, Color color)
    {
        if (statusText == null)
        {
            Debug.LogWarning("[LoginUI] StatusText가 연결되지 않았습니다!");
            return;
        }

        statusText.text = message;
        statusText.color = color;
    }

    /// <summary>
    /// UI 요소들의 활성화 상태를 변경합니다.
    /// </summary>
    void SetUIInteractable(bool interactable)
    {
        if (usernameInput != null)
            usernameInput.interactable = interactable;

        if (passwordInput != null)
            passwordInput.interactable = interactable;

        if (loginButton != null)
            loginButton.interactable = interactable;

        if (signInButton != null && signInButton.interactable)
            signInButton.interactable = interactable;
    }

    // ─────────────────────────────────────────
    //  테스트/디버그 함수
    // ─────────────────────────────────────────

    /// <summary>
    /// [디버그] 로그아웃 테스트
    /// </summary>
    public void TestLogout()
    {
        if (ApiManager.Instance != null)
        {
            ApiManager.Instance.Logout();
            ShowStatus("로그아웃되었습니다.", Color.white);
            SetUIInteractable(true);

            if (usernameInput != null)
                usernameInput.text = "";
            if (passwordInput != null)
                passwordInput.text = "";
        }
    }
}