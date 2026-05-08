using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Fusion;

/// <summary>
/// 로비 UI 관리 스크립트
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("── Buttons ──")]
    public Button playButton;
    public Button joinInButton;
    public Button tutorialButton;
    public Button exitButton;

    [Header("── UI Elements ──")]
    public TextMeshProUGUI nicknameText;

    [Header("── 방 이름 입력 팝업 ──")]
    public GameObject roomNamePanel;
    public TextMeshProUGUI titleText;
    public TMP_InputField roomNameInput;
    public Button confirmButton;
    public Button cancelButton;

    private GameMode _currentMode;
    private bool _isConnecting = false;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
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
            roomNamePanel.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (joinInButton != null)
            joinInButton.onClick.AddListener(OnJoinInClicked);

        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(OnTutorialClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        UpdateNickname();

        Debug.Log("[LobbyUI] 로비 씬 로드 완료");
    }

    // ─────────────────────────────────────────
    //  닉네임 업데이트
    // ─────────────────────────────────────────

    void UpdateNickname()
    {
        if (nicknameText == null)
        {
            Debug.LogWarning("[LobbyUI] NicknameText가 연결되지 않았습니다!");
            return;
        }

        if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
        {
            nicknameText.text = ApiManager.Instance.userName;
            PlayerPrefs.SetString("user_name", ApiManager.Instance.userName);
            PlayerPrefs.Save();

            Debug.Log($"[LobbyUI] 닉네임 표시: {ApiManager.Instance.userName}");
        }
        else
        {
            nicknameText.text = "Guest";
            PlayerPrefs.SetString("user_name", "Guest");
            PlayerPrefs.Save();
            Debug.LogWarning("[LobbyUI] 로그인 정보 없음 → Guest 표시");
        }
    }

    // ─────────────────────────────────────────
    //  버튼 클릭 이벤트
    // ─────────────────────────────────────────

    void OnPlayClicked()
    {
        _currentMode = GameMode.Host;
        titleText.text = "방 생성";
        roomNameInput.text = "";
        roomNameInput.placeholder.GetComponent<TextMeshProUGUI>().text = "방 이름 입력";
        roomNamePanel.SetActive(true);

        Debug.Log("[LobbyUI] Play 버튼 클릭 - Host 모드");
    }

    void OnJoinInClicked()
    {
        _currentMode = GameMode.Client;
        titleText.text = "방 코드 입력";
        roomNameInput.text = "";
        roomNameInput.placeholder.GetComponent<TextMeshProUGUI>().text = "4자리 코드 입력";
        roomNamePanel.SetActive(true);

        Debug.Log("[LobbyUI] Join 버튼 클릭 - Client 모드");
    }

    /// <summary>
    /// ✅ 수정: RoomName 저장 추가
    /// </summary>
    async void OnConfirmClicked()
    {
        // ── 중복 클릭 방지 ──────────────────────
        if (_isConnecting)
        {
            Debug.LogWarning("[로비] 이미 접속 중입니다!");
            return;
        }

        roomNamePanel.SetActive(false);

        if (_currentMode == GameMode.Host)
        {
            // ═══════════════════════════════════════════
            //  Host 모드 — 방 생성
            // ═══════════════════════════════════════════

            // ✅ 방 이름 가져오기
            string roomName = roomNameInput.text.Trim();

            // 빈 값이면 기본 이름 사용
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = $"{ApiManager.Instance?.userName ?? "Guest"}의 방";
            }

            string roomCode = Random.Range(1000, 9999).ToString();

            // ✅ RoomName도 저장!
            PlayerPrefs.SetString("RoomName", roomName);
            PlayerPrefs.SetString("RoomCode", roomCode);
            PlayerPrefs.SetString("RoomMode", "Create");
            PlayerPrefs.Save();

            Debug.Log("═══════════════════════════════════════════");
            Debug.Log($"[로비] 방 생성 요청");
            Debug.Log($"  - 방 이름: {roomName}");  // ✅ 추가
            Debug.Log($"  - 방 코드: {roomCode}");
            Debug.Log($"  - 모드: Host");

            // ✅ PlayerPrefs 확인 로그
            Debug.Log($"[로비] PlayerPrefs 저장 확인:");
            Debug.Log($"  - RoomName: {PlayerPrefs.GetString("RoomName")}");
            Debug.Log($"  - RoomCode: {PlayerPrefs.GetString("RoomCode")}");

            if (PhotonManager.Instance == null)
            {
                Debug.LogError("[로비] ❌ PhotonManager.Instance가 null!");
                Debug.Log("═══════════════════════════════════════════");
                return;
            }

            _isConnecting = true;
            SetButtonsInteractable(false);

            Debug.Log("[로비] PhotonManager.StartHostWithRoom 호출 시작...");

            try
            {
                await PhotonManager.Instance.StartHostWithRoom(roomCode);

                Debug.Log("[로비] ✅ PhotonManager.StartHostWithRoom 완료");
                Debug.Log("[로비] → waitingRoomScene 전환은 PhotonManager가 처리");
                Debug.Log("═══════════════════════════════════════════");
            }
            catch (System.Exception e)
            {
                Debug.LogError("═══════════════════════════════════════════");
                Debug.LogError($"[로비] ❌ 방 생성 실패!");
                Debug.LogError($"  - 에러: {e.Message}");
                Debug.LogError("═══════════════════════════════════════════");

                _isConnecting = false;
                SetButtonsInteractable(true);
            }
        }
        else
        {
            // ═══════════════════════════════════════════
            //  Client 모드 — 방 참여
            // ═══════════════════════════════════════════

            if (string.IsNullOrWhiteSpace(roomNameInput.text))
            {
                Debug.LogWarning("[로비] 방 코드를 입력해주세요!");
                roomNamePanel.SetActive(true);
                return;
            }

            string code = roomNameInput.text.Trim();

            // ✅ Client는 RoomName 저장 안 함 (Host가 이미 저장함)
            PlayerPrefs.SetString("RoomCode", code);
            PlayerPrefs.SetString("RoomMode", "Join");
            PlayerPrefs.Save();

            Debug.Log("═══════════════════════════════════════════");
            Debug.Log($"[로비] 방 참여 요청");
            Debug.Log($"  - 방 코드: {code}");
            Debug.Log($"  - 모드: Client");

            if (PhotonManager.Instance == null)
            {
                Debug.LogError("[로비] ❌ PhotonManager.Instance가 null!");
                Debug.Log("═══════════════════════════════════════════");
                return;
            }

            _isConnecting = true;
            SetButtonsInteractable(false);

            Debug.Log("[로비] PhotonManager.StartClientWithRoom 호출 시작...");

            try
            {
                await PhotonManager.Instance.StartClientWithRoom(code);

                Debug.Log("[로비] ✅ PhotonManager.StartClientWithRoom 완료");
                Debug.Log("[로비] → waitingRoomScene 전환은 PhotonManager가 처리");
                Debug.Log("═══════════════════════════════════════════");
            }
            catch (System.Exception e)
            {
                Debug.LogError("═══════════════════════════════════════════");
                Debug.LogError($"[로비] ❌ 방 참여 실패!");
                Debug.LogError($"  - 에러: {e.Message}");
                Debug.LogError("═══════════════════════════════════════════");

                _isConnecting = false;
                SetButtonsInteractable(true);
            }
        }
    }

    void OnCancelClicked()
    {
        roomNamePanel.SetActive(false);
        roomNameInput.text = "";
        Debug.Log("[LobbyUI] 팝업 취소");
    }

    void OnTutorialClicked()
    {
        Debug.Log("[LobbyUI] Tutorial 버튼 클릭");
        LoadScene("TutorialScene");
    }

    void OnExitClicked()
    {
        Debug.Log("[LobbyUI] Exit 버튼 클릭 → 로그아웃");
        if (ApiManager.Instance != null)
            ApiManager.Instance.Logout();
        LoadScene("LoginScene");
    }

    // ─────────────────────────────────────────
    //  씬 전환 헬퍼
    // ─────────────────────────────────────────

    void LoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.Log($"[LobbyUI] {sceneName} 씬으로 이동");
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"[LobbyUI] {sceneName} 씬이 Build Settings에 없습니다!");
        }
    }

    void SetButtonsInteractable(bool interactable)
    {
        if (playButton != null)
            playButton.interactable = interactable;

        if (joinInButton != null)
            joinInButton.interactable = interactable;

        if (tutorialButton != null)
            tutorialButton.interactable = interactable;

        if (exitButton != null)
            exitButton.interactable = interactable;

        if (confirmButton != null)
            confirmButton.interactable = interactable;
    }

    void OnValidate()
    {
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
        if (roomNamePanel == null)
            Debug.LogWarning("[LobbyUI] RoomNamePanel이 연결되지 않았습니다!");
        if (roomNameInput == null)
            Debug.LogWarning("[LobbyUI] RoomNameInput이 연결되지 않았습니다!");
    }
}