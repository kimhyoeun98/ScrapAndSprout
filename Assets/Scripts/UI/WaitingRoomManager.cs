using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 대기방 전체 관리자
/// </summary>
public class WaitingRoomManager : NetworkBehaviour
{
    public static WaitingRoomManager Instance { get; private set; }

    [Header("── 슬롯 참조 ──")]
    public PlayerSlot[] playerSlots = new PlayerSlot[4];

    [Header("── 캐릭터 선택 패널 ──")]
    public GameObject characterSelectPanel;
    public Button alphaButton;
    public Button betaButton;
    public Button gammaButton;
    public Button deltaButton;
    public Button closeButton;

    [Header("── 방 정보 UI ──")]
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI roomCodeText;
    public Button exitButton;

    [Header("── 캐릭터 스프라이트 ──")]
    public Sprite alphaSprite;
    public Sprite betaSprite;
    public Sprite gammaSprite;
    public Sprite deltaSprite;

    [Header("── 게임 시작 ──")]
    public Button readyButton;
    public Button startGameButton;
    public Button addBotButton;

    private int _mySlotIndex = -1;
    private int _currentSelectingSlotIndex = -1;
    private bool _isSpawned = false;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[웨이팅룸] Instance 설정 완료");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        if (!_isSpawned || Object == null)
            return;

        if (Object.HasStateAuthority && startGameButton != null)
        {
            bool allReady = CheckAllPlayersReady();
            startGameButton.interactable = allReady;
        }
    }

    // ─────────────────────────────────────────
    //  NetworkBehaviour 생명주기
    // ─────────────────────────────────────────

    public override void Spawned()
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"[WRM.Spawned] 시작");
        Debug.Log($"[WRM.Spawned] HasStateAuthority: {Object.HasStateAuthority}");
        Debug.Log($"[WRM.Spawned] IsServer: {Runner.IsServer}");
        Debug.Log($"[WRM.Spawned] LocalPlayer: {Runner.LocalPlayer}");
        Debug.Log("═══════════════════════════════════════════");

        // ✅ Spawned 플래그 설정
        _isSpawned = true;

        InitializeSlots();
        DisplayRoomInfo();

        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(false);
        }

        SetupButtons();

        string myName = PlayerPrefs.GetString("user_name", "Guest");

        if (Object.HasStateAuthority)
        {
            Debug.Log("[WRM.Spawned] Host 모드 - 슬롯 0번 배정 시작");
            AssignHostSlot(myName);
            Debug.Log("[WRM.Spawned] Host 모드 - 슬롯 0번 배정 완료");

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = false;
            }

            if (addBotButton != null)
            {
                addBotButton.gameObject.SetActive(true);
            }
        }
        else
        {
            Debug.Log("[WRM.Spawned] Client 모드 - 슬롯 요청");
            RPC_RequestSlot(myName);

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
            }

            if (addBotButton != null)
            {
                addBotButton.gameObject.SetActive(false);
            }
        }

        Debug.Log("═══════════════════════════════════════════");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _isSpawned = false;
        Debug.Log("[WRM.Despawned] 호출됨");
    }

    // ─────────────────────────────────────────
    //  초기화 메서드들
    // ─────────────────────────────────────────

    void SetMySlotIndex(int index)
    {
        _mySlotIndex = index;
        Debug.Log($"[WRM.SetMySlotIndex] _mySlotIndex 설정됨: {index}");
    }

    void InitializeSlots()
    {
        Debug.Log("[WRM.InitializeSlots] 시작");

        if (playerSlots == null || playerSlots.Length != 4)
        {
            Debug.LogError("[WRM.InitializeSlots] ❌ playerSlots 배열 오류!");
            return;
        }

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].Initialize(i, this);
                Debug.Log($"[WRM.InitializeSlots] PlayerSlot {i}번 Initialize 완료");
            }
            else
            {
                Debug.LogError($"[WRM.InitializeSlots] ❌ PlayerSlot {i}번 null!");
            }
        }

        Debug.Log("[WRM.InitializeSlots] 완료");
    }

    void DisplayRoomInfo()
    {
        string roomName = PlayerPrefs.GetString("RoomName", "");
        string roomCode = PlayerPrefs.GetString("RoomCode", "????");

        Debug.Log($"[웨이팅룸] PlayerPrefs 읽기:");
        Debug.Log($"  - RoomName: {roomName}");
        Debug.Log($"  - RoomCode: {roomCode}");

        if (roomNameText != null)
        {
            roomNameText.text = string.IsNullOrEmpty(roomName) ? "방이름: (없음)" : $"방이름: {roomName}";
        }
        else
        {
            Debug.LogWarning("[웨이팅룸] ⚠️ roomNameText가 null!");
        }

        if (roomCodeText != null)
        {
            roomCodeText.text = $"방 코드: {roomCode}";
        }
        else
        {
            Debug.LogWarning("[웨이팅룸] ⚠️ roomCodeText가 null!");
        }
    }

    void AssignHostSlot(string myName)
    {
        Debug.Log("[웨이팅룸] Host 모드 - 슬롯 0번 자동 배정");

        SetMySlotIndex(0);

        if (playerSlots[0] != null)
        {
            playerSlots[0].OccupySlot(myName, Runner.LocalPlayer);
            playerSlots[0].SetAsMySlot();
            Debug.Log("[웨이팅룸] ✅ Host 슬롯 0번 배정 완료");
        }
    }

    void SetupButtons()
    {
        if (alphaButton != null)
            alphaButton.onClick.AddListener(() => OnCharacterSelected(1));
        if (betaButton != null)
            betaButton.onClick.AddListener(() => OnCharacterSelected(2));
        if (gammaButton != null)
            gammaButton.onClick.AddListener(() => OnCharacterSelected(3));
        if (deltaButton != null)
            deltaButton.onClick.AddListener(() => OnCharacterSelected(4));

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseCharacterSelectPanel);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (addBotButton != null)
            addBotButton.onClick.AddListener(OnAddBotClicked);
    }

    // ─────────────────────────────────────────
    //  슬롯 요청/배정 (RPC)
    // ─────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSlot(string playerName, RpcInfo info = default)
    {
        Debug.Log($"[슬롯 요청] 수신 - 요청자: {playerName}");

        for (int i = 1; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null && !playerSlots[i].IsOccupied)
            {
                playerSlots[i].OccupySlot(playerName, info.Source);
                RPC_NotifySlotAssigned(info.Source, i);
                Debug.Log($"[슬롯 배정] ✅ 슬롯 {i}번 배정 완료");
                return;
            }
        }

        Debug.LogWarning("[슬롯 배정] ❌ 빈 슬롯 없음!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_NotifySlotAssigned(PlayerRef player, int slotIndex)
    {
        if (player == Runner.LocalPlayer)
        {
            Debug.Log($"[슬롯 알림] ✅ 내 슬롯 배정됨 - 슬롯 {slotIndex}번");

            SetMySlotIndex(slotIndex);

            if (playerSlots[slotIndex] != null)
            {
                playerSlots[slotIndex].SetAsMySlot();
            }
        }
    }

    // ─────────────────────────────────────────
    //  캐릭터 선택 UI
    // ─────────────────────────────────────────

    public void OpenCharacterSelectPanel(int slotIndex)
    {
        Debug.Log($"[WRM.OpenPanel] 호출됨!");
        Debug.Log($"[WRM.OpenPanel] requestedSlot: {slotIndex}");
        Debug.Log($"[WRM.OpenPanel] mySlot: {_mySlotIndex}");

        _currentSelectingSlotIndex = slotIndex;

        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(true);
            Debug.Log($"[WRM.OpenPanel] 패널 활성화 완료");
        }
        else
        {
            Debug.LogError($"[WRM.OpenPanel] ❌ characterSelectPanel이 null!");
        }
    }

    void CloseCharacterSelectPanel()
    {
        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(false);
        }

        _currentSelectingSlotIndex = -1;
    }

    void OnCharacterSelected(int characterIndex)
    {
        if (_currentSelectingSlotIndex < 0 || _currentSelectingSlotIndex >= playerSlots.Length)
        {
            Debug.LogWarning("[캐릭터 선택] ❌ 잘못된 슬롯 인덱스");
            return;
        }

        PlayerSlot slot = playerSlots[_currentSelectingSlotIndex];
        if (slot != null && slot.IsOccupied)
        {
            slot.RPC_SetCharacter(characterIndex);
        }

        CloseCharacterSelectPanel();
    }

    // ─────────────────────────────────────────
    //  준비 완료 / 게임 시작 / 봇 추가
    // ─────────────────────────────────────────

    void OnReadyClicked()
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"[준비 완료] 버튼 클릭!");
        Debug.Log($"[준비 완료] mySlot: {_mySlotIndex}");

        if (_mySlotIndex < 0 || _mySlotIndex >= playerSlots.Length)
        {
            Debug.LogWarning("[준비 완료] ❌ 잘못된 슬롯 인덱스");
            Debug.Log("═══════════════════════════════════════════");
            return;
        }

        PlayerSlot mySlot = playerSlots[_mySlotIndex];
        if (mySlot == null)
        {
            Debug.LogWarning("[준비 완료] ❌ 내 슬롯이 null");
            Debug.Log("═══════════════════════════════════════════");
            return;
        }

        Debug.Log($"[준비 완료] IsOccupied: {mySlot.IsOccupied}");
        Debug.Log($"[준비 완료] CharacterIndex: {mySlot.CharacterIndex}");
        Debug.Log($"[준비 완료] IsReady (현재): {mySlot.IsReady}");

        if (!mySlot.IsOccupied)
        {
            Debug.LogWarning("[준비 완료] ❌ 내 슬롯이 점유되지 않음");
            Debug.Log("═══════════════════════════════════════════");
            return;
        }

        if (mySlot.CharacterIndex == 0)
        {
            Debug.LogWarning("[준비 완료] ❌ 캐릭터를 먼저 선택하세요!");
            Debug.Log("═══════════════════════════════════════════");
            return;
        }

        bool newReadyState = !mySlot.IsReady;

        Debug.Log($"[준비 완료] 새로운 준비 상태: {newReadyState}");
        Debug.Log($"[준비 완료] RPC_SetReady 호출 시작");

        mySlot.RPC_SetReady(newReadyState);

        Debug.Log($"[준비 완료] RPC_SetReady 호출 완료");

        if (readyButton != null)
        {
            TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = newReadyState ? "준비 취소" : "준비 완료";
                Debug.Log($"[준비 완료] 버튼 텍스트 변경: {buttonText.text}");
            }
            else
            {
                Debug.LogWarning("[준비 완료] ⚠️ buttonText null!");
            }
        }
        else
        {
            Debug.LogWarning("[준비 완료] ⚠️ readyButton null!");
        }

        Debug.Log("═══════════════════════════════════════════");
    }

    void OnStartGameClicked()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[게임 시작] ❌ Host만 게임을 시작할 수 있습니다!");
            return;
        }

        if (!CheckAllPlayersReady())
        {
            Debug.LogWarning("[게임 시작] ❌ 모든 플레이어가 준비되지 않았습니다!");
            return;
        }

        Debug.Log("[게임 시작] ✅ MainGame 씬으로 전환");

        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.LoadGameScene();
        }
        else
        {
            Debug.LogError("[게임 시작] ❌ PhotonManager.Instance가 null!");
        }
    }

    void OnAddBotClicked()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[봇 추가] ❌ Host만 봇을 추가할 수 있습니다!");
            return;
        }

        for (int i = 1; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null && !playerSlots[i].IsOccupied)
            {
                string botName = $"Bot_{i}";
                playerSlots[i].OccupySlot(botName, PlayerRef.None);
                playerSlots[i].RPC_SetCharacter(Random.Range(1, 5));
                playerSlots[i].RPC_SetReady(true);

                Debug.Log($"[봇 추가] ✅ 슬롯 {i}번에 {botName} 추가 완료");
                return;
            }
        }

        Debug.LogWarning("[봇 추가] ❌ 빈 슬롯이 없습니다!");
    }

    bool CheckAllPlayersReady()
    {
        if (!_isSpawned)
            return false;

        int occupiedCount = 0;
        int readyCount = 0;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null)
                continue;

            if (playerSlots[i].Object == null || !playerSlots[i].Object.IsValid)
                continue;

            if (playerSlots[i].IsOccupied)
            {
                occupiedCount++;
                if (playerSlots[i].IsReady)
                {
                    readyCount++;
                }
            }
        }

        bool result = occupiedCount >= 2 && occupiedCount == readyCount;

        Debug.Log($"[준비 확인] 점유: {occupiedCount}, 준비: {readyCount}, 결과: {result}");

        return result;
    }

    // ─────────────────────────────────────────
    //  플레이어 퇴장
    // ─────────────────────────────────────────

    public void OnPlayerLeft(PlayerRef player)
    {
        Debug.Log($"[웨이팅룸] 플레이어 퇴장: {player.PlayerId}");

        int slotIndex = Mathf.Clamp(player.PlayerId - 1, 0, 3);

        if (slotIndex >= 0 && slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
        {
            playerSlots[slotIndex].RPC_ClearSlot();
        }
    }

    void OnExitClicked()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.LeaveRoom();
        }
    }

    // ─────────────────────────────────────────
    //  PlayerSlot 헬퍼 메서드
    // ─────────────────────────────────────────

    public Sprite GetCharacterSpritePublic(int index)
    {
        switch (index)
        {
            case 1: return alphaSprite;
            case 2: return betaSprite;
            case 3: return gammaSprite;
            case 4: return deltaSprite;
            default: return null;
        }
    }

    public string GetCharacterNamePublic(int index)
    {
        switch (index)
        {
            case 1: return "Alpha";
            case 2: return "Beta";
            case 3: return "Gamma";
            case 4: return "Delta";
            default: return "";
        }
    }

    public bool IsCharacterPanelOpen()
    {
        return characterSelectPanel != null && characterSelectPanel.activeSelf;
    }

    public void RefreshCharacterButtonStates()
    {
        // TODO: 캐릭터 중복 선택 방지 기능
    }
}