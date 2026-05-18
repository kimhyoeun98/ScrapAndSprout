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
/// - ApiManager.Login() 호출 (Spring 서버)
/// - 로그인 성공 시 PlayerPrefs 저장 및 LobbyScene 이동
/// 
/// [Fusion 2 통합]
/// - PhotonManager 초기화 확인
/// - user_name을 PlayerPrefs에 저장 (PhotonManager가 사용)
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
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log("[LoginUI] 초기화 시작");

        // ✅ PhotonManager 존재 확인
        if (PhotonManager.Instance != null)
            Debug.Log("[LoginUI] ✅ PhotonManager 확인됨");
        else
        {
            Debug.LogWarning("[LoginUI] ⚠️ PhotonManager가 없습니다!");
            Debug.LogWarning("[LoginUI] LoginScene Hierarchy에 PhotonManager 오브젝트를 추가하세요!");
        }

        // ✅ LoginScene BGM 시작
        AudioManager.Instance?.PlayBGMForScene("LobbyScene");

        Debug.Log("═══════════════════════════════════════════");

        // 버튼 이벤트 연결
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
            loginButton.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick()); // ✅ 추가
        }

        if (signInButton != null)
        {
            signInButton.interactable = false;
            signInButton.onClick.AddListener(OnSignInButtonClicked);
            signInButton.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick()); // ✅ 추가
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

        // Enter 키로 로그인
        if (passwordInput != null)
            passwordInput.onSubmit.AddListener((value) => OnLoginButtonClicked());
    }

    // ─────────────────────────────────────────
    //  로그인 버튼 클릭
    // ─────────────────────────────────────────

    void OnLoginButtonClicked()
    {
        Debug.Log("[LoginUI] 로그인 버튼 클릭");

        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("Insert ID!", Color.red);
            Debug.LogWarning("[LoginUI] ID가 비어있음");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus("Insert PW!", Color.red);
            Debug.LogWarning("[LoginUI] 비밀번호가 비어있음");
            return;
        }

        RequestLogin(username, password);
    }

    // ─────────────────────────────────────────
    //  Spring 서버에 로그인 요청
    // ─────────────────────────────────────────

    void RequestLogin(string username, string password)
    {
        Debug.Log("───────────────────────────────────────────");
        Debug.Log("[LoginUI] Spring 서버 로그인 요청 시작");

        if (ApiManager.Instance == null)
        {
            ShowStatus("Server connect error: ApiManager not found.", Color.red);
            Debug.LogError("[LoginUI] ❌ ApiManager.Instance가 null!");
            return;
        }

        SetUIInteractable(false);
        ShowStatus("Login...", Color.yellow);

        if (loadingIcon != null)
            loadingIcon.SetActive(true);

        LoginRequest request = new LoginRequest
        {
            user_name = username,
            password = password
        };

        Debug.Log($"[LoginUI] 요청 정보:");
        Debug.Log($"  - user_name: {username}");
        Debug.Log("───────────────────────────────────────────");

        ApiManager.Instance.Login(request, OnLoginSuccess, OnLoginFail);
    }

    // ─────────────────────────────────────────
    //  로그인 성공 콜백
    // ─────────────────────────────────────────

    void OnLoginSuccess(LoginResponse response)
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log("[LoginUI] ✅ Spring 서버 로그인 성공!");
        Debug.Log($"  - user_id: {response.user_id}");
        Debug.Log($"  - user_name: {response.user_name}");

        ShowStatus($"Welcome, {response.user_name}!", Color.green);

        if (loadingIcon != null)
            loadingIcon.SetActive(false);

        PlayerPrefs.SetString("user_name", response.user_name);
        PlayerPrefs.SetString("user_id", response.user_id);
        PlayerPrefs.Save();

        Debug.Log("[LoginUI] PlayerPrefs 저장 완료:");
        Debug.Log($"  - user_name: {response.user_name}");
        Debug.Log($"  - user_id: {response.user_id}");
        Debug.Log("═══════════════════════════════════════════");

        Invoke(nameof(GoToNextScene), 1f);
    }

    // ─────────────────────────────────────────
    //  로그인 실패 콜백
    // ─────────────────────────────────────────

    void OnLoginFail(string errorMessage)
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.LogWarning("[LoginUI] ❌ Spring 서버 로그인 실패!");
        Debug.LogWarning($"  - Error: {errorMessage}");
        Debug.Log("═══════════════════════════════════════════");

        ShowStatus("Failed Login! Check ID/PW.", Color.red);

        SetUIInteractable(true);

        if (loadingIcon != null)
            loadingIcon.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  LobbyScene으로 이동
    // ─────────────────────────────────────────

    void GoToNextScene()
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

    // ─────────────────────────────────────────
    //  회원가입 버튼 (미구현)
    // ─────────────────────────────────────────

    void OnSignInButtonClicked()
    {
        ShowStatus("회원가입 기능은 준비중입니다.", Color.yellow);
        Debug.Log("[LoginUI] Sign in 버튼 클릭 (미구현)");
    }

    // ─────────────────────────────────────────
    //  UI 헬퍼 함수들
    // ─────────────────────────────────────────

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

    public void TestLogout()
    {
        if (ApiManager.Instance != null)
        {
            ApiManager.Instance.Logout();
            ShowStatus("로그아웃되었습니다.", Color.white);
            SetUIInteractable(true);

            if (usernameInput != null) usernameInput.text = "";
            if (passwordInput != null) passwordInput.text = "";

            PlayerPrefs.DeleteKey("user_name");
            PlayerPrefs.DeleteKey("user_id");
            PlayerPrefs.DeleteKey("RoomCode");
            PlayerPrefs.DeleteKey("RoomMode");
            PlayerPrefs.Save();

            Debug.Log("[LoginUI] 로그아웃 완료 - PlayerPrefs 정리됨");
        }
    }
}