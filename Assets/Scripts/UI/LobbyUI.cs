using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 UI 관리 스크립트
/// 
/// [부착 위치] LobbyPanel 오브젝트
/// 
/// [역할]
/// - Play/Join/Tutorial/Exit 버튼 관리
/// - 닉네임 표시
/// - 씬 전환
/// - (향후) Photon 서버 연결
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("── Buttons ──")]
    [Tooltip("방 생성 (Play) 버튼")]
    public Button playButton;
    
    [Tooltip("방 참여 (Join in) 버튼")]
    public Button joinInButton;
    
    [Tooltip("튜토리얼 버튼")]
    public Button tutorialButton;
    
    [Tooltip("종료 (Exit) 버튼")]
    public Button exitButton;
    
    [Header("── UI Elements ──")]
    [Tooltip("닉네임 표시 텍스트")]
    public TextMeshProUGUI nicknameText;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 버튼 이벤트 연결
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
        
        if (joinInButton != null)
            joinInButton.onClick.AddListener(OnJoinInClicked);
        
        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(OnTutorialClicked);
        
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);
        
        // 닉네임 표시
        UpdateNickname();
        
        // Photon 서버 연결 (향후 구현)
        // ConnectToPhoton();
        
        Debug.Log("[LobbyUI] 로비 씬 로드 완료");
    }

    // ─────────────────────────────────────────
    //  닉네임 업데이트
    // ─────────────────────────────────────────

    /// <summary>
    /// ApiManager에서 저장된 닉네임을 표시합니다.
    /// </summary>
    void UpdateNickname()
    {
        if (nicknameText == null)
        {
            Debug.LogWarning("[LobbyUI] NicknameText가 연결되지 않았습니다!");
            return;
        }
        
        // ApiManager가 있으면 로그인된 유저 이름 표시
        if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
        {
            nicknameText.text = ApiManager.Instance.userName;
            Debug.Log($"[LobbyUI] 닉네임 표시: {ApiManager.Instance.userName}");
        }
        else
        {
            nicknameText.text = "Guest";
            Debug.LogWarning("[LobbyUI] 로그인 정보 없음 → Guest 표시");
        }
    }

    // ─────────────────────────────────────────
    //  버튼 클릭 이벤트
    // ─────────────────────────────────────────

    /// <summary>
    /// Play 버튼 클릭 → 방 생성 모드로 대기방 씬 이동
    /// </summary>
    void OnPlayClicked()
    {
        Debug.Log("[LobbyUI] Play 버튼 클릭 → 방 생성 모드");
        
        // 방 생성 모드 저장
        PlayerPrefs.SetString("RoomMode", "Create");
        PlayerPrefs.Save();
        
        // WaitingRoomScene으로 이동 (향후 구현)
        LoadScene("WaitingRoomScene");
    }

    /// <summary>
    /// Join in 버튼 클릭 → 방 참여 모드로 대기방 씬 이동
    /// </summary>
    void OnJoinInClicked()
    {
        Debug.Log("[LobbyUI] Join in 버튼 클릭 → 방 참여 모드");
        
        // 방 참여 모드 저장
        PlayerPrefs.SetString("RoomMode", "Join");
        PlayerPrefs.Save();
        
        // WaitingRoomScene으로 이동 (향후 구현)
        LoadScene("WaitingRoomScene");
    }

    /// <summary>
    /// Tutorial 버튼 클릭 → 튜토리얼 씬 이동
    /// </summary>
    void OnTutorialClicked()
    {
        Debug.Log("[LobbyUI] Tutorial 버튼 클릭");
        
        // TutorialScene으로 이동 (향후 구현)
        LoadScene("TutorialScene");
    }

    /// <summary>
    /// Exit 버튼 클릭 → 로그아웃 후 로그인 씬으로 이동
    /// </summary>
    void OnExitClicked()
    {
        Debug.Log("[LobbyUI] Exit 버튼 클릭 → 로그아웃");
        
        // 로그아웃
        if (ApiManager.Instance != null)
        {
            ApiManager.Instance.Logout();
        }
        
        // 로그인 씬으로 이동
        LoadScene("LoginScene");
    }

    // ─────────────────────────────────────────
    //  씬 전환 헬퍼
    // ─────────────────────────────────────────

    /// <summary>
    /// 씬을 로드합니다. 없으면 에러 메시지 표시.
    /// </summary>
    void LoadScene(string sceneName)
    {
        // 씬이 Build Settings에 추가되어 있는지 확인
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.Log($"[LobbyUI] {sceneName} 씬으로 이동");
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"[LobbyUI] {sceneName} 씬이 Build Settings에 없습니다!");
            ShowMessage($"{sceneName} is not available yet.");
        }
    }

    /// <summary>
    /// 임시 메시지 표시 (향후 UI 팝업으로 대체)
    /// </summary>
    void ShowMessage(string message)
    {
        Debug.Log($"[LobbyUI] 메시지: {message}");
        
        // TODO: 실제 UI 팝업 구현
        // 지금은 Console에만 로그
    }

    // ─────────────────────────────────────────
    //  Photon 연결 (향후 구현)
    // ─────────────────────────────────────────

    /// <summary>
    /// Photon 서버에 연결합니다. (8주차 작업)
    /// </summary>
    void ConnectToPhoton()
    {
        // TODO: Photon PUN2 설치 후 구현
        /*
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[LobbyUI] Photon 서버 연결 시도...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.Log("[LobbyUI] Photon 이미 연결됨");
        }
        */
        
        Debug.Log("[LobbyUI] Photon 연결 기능은 8주차에 구현 예정");
    }

    // ─────────────────────────────────────────
    //  디버그/테스트 함수
    // ─────────────────────────────────────────

    /// <summary>
    /// [테스트] 버튼 동작 확인
    /// </summary>
    void OnValidate()
    {
        // Editor에서 버튼이 제대로 연결되었는지 확인
        if (playButton == null)
            Debug.LogWarning("[LobbyUI] PlayButton이 연결되지 않았습니다!");
        
        if (joinInButton == null)
            Debug.LogWarning("[LobbyUI] JoinInButton이 연결되지 않았습니다!");
        
        if (tutorialButton == null)
            Debug.LogWarning("[LobbyUI] TutorialButton이 연결되지 않았습니다!");
        
        if (exitButton == null)
            Debug.LogWarning("[LobbyUI] ExitButton이 연결되지 않았습니다!");
        
        if (nicknameText == null)
            Debug.LogWarning("[LobbyUI] NicknameText가 연결되지 않았습니다!");
    }
}