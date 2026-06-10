using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 개별 슬롿 관리
/// </summary>
public class PlayerSlot : NetworkBehaviour
{
    [Header("UI Elements")]
    public Image characterIcon;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI readyStatusText;
    public Button selectCharacterButton;

    // ─────────────────────────────────────────
    //  [Networked] 변수
    // ─────────────────────────────────────────

    [Networked] public NetworkString<_32> LoginId { get; set; }
    [Networked] public int CharacterIndex { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsOccupied { get; set; }

    // ─────────────────────────────────────────
    //  로컬 변수
    // ─────────────────────────────────────────

    private int _prevCharacter = -1;
    private bool _prevReady = false;
    private bool _prevOccupied = false;

    private int _slotIndex = -1;
    private WaitingRoomManager _manager;
    private bool _isLocal = false;

    // ✅ 버튼 상태 추적용 (로그 중복 방지)
    private bool _prevButtonState = false;
    private string _prevLoginId = "";
    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    public void Initialize(int index, WaitingRoomManager manager)
    {
        _slotIndex = index;
        _manager = manager;

        if (selectCharacterButton != null)
        {
            selectCharacterButton.onClick.RemoveAllListeners();
            selectCharacterButton.onClick.AddListener(OnSelectCharacterClicked);
        }

        Debug.Log($"[슬롯 {_slotIndex}] Initialize 완료");
    }

    // ─────────────────────────────────────────
    //  Spawned
    // ─────────────────────────────────────────

    public override void Spawned()
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"[슬롯.Spawned] 호출됨!");
        Debug.Log($"[슬롯.Spawned] slot: {_slotIndex}");
        Debug.Log($"[슬롯.Spawned] HasStateAuthority: {HasStateAuthority}");
        Debug.Log($"[슬롯.Spawned] _isLocal: {_isLocal}");
        Debug.Log($"[슬롯.Spawned] IsOccupied: {IsOccupied}");
        Debug.Log("═══════════════════════════════════════════");

        // ✅ 이미 내 슬롯이면 초기화 스킵!
        if (_isLocal)
        {
            Debug.Log($"[슬롯.Spawned] ⚠️ slot: {_slotIndex} - 이미 내 슬롯 → 초기화 스킵");

            // 강제 갱신 플래그만 설정
            _prevCharacter = -1;
            _prevReady = false;
            _prevOccupied = true;
            return;
        }

        // Host만 네트워크 변수 초기화
        if (HasStateAuthority)
        {
            CharacterIndex = 0;
            IsReady = false;
            IsOccupied = false;
            LoginId = "";

            Debug.Log($"[슬롯.Spawned] slot: {_slotIndex} - Host 네트워크 변수 초기화 완료");
        }

        // 강제 갱신 플래그
        _prevCharacter = -1;
        _prevReady = false;
        _prevOccupied = true;
    }

    // ─────────────────────────────────────────
    //  Render
    // ─────────────────────────────────────────

    public override void Render()
    {
        // ── 1. 슬롯 점유 상태 변경 감지 ────
        if (_prevOccupied != (bool)IsOccupied)
        {
            _prevOccupied = IsOccupied;

            if (!IsOccupied)
            {
                // ✅ 내 슬롯이 아닐 때만 ClearSlotAll
                if (!_isLocal)
                {
                    ClearSlotAll();
                }
                else
                {
                    // 내 슬롯은 비주얼만 초기화
                    ClearVisualOnly();
                }
            }
        }

        // ── 2. 버튼 상태 매 프레임 강제 갱신 ────
        bool newButtonState = IsOccupied && _isLocal;
        selectCharacterButton.interactable = newButtonState;

        // ✅ 로그 추가 (버튼 상태 변경 시에만)
        if (_prevButtonState != newButtonState)
        {
            _prevButtonState = newButtonState;
            Debug.Log("───────────────────────────────────────────");
            Debug.Log($"[Slot.Render.Button] 버튼 상태 변경됨!");
            Debug.Log($"[Slot.Render.Button] slot: {_slotIndex}");
            Debug.Log($"[Slot.Render.Button] IsOccupied: {IsOccupied}");
            Debug.Log($"[Slot.Render.Button] _isLocal: {_isLocal}");
            Debug.Log($"[Slot.Render.Button] interactable: {selectCharacterButton?.interactable}");
            Debug.Log("───────────────────────────────────────────");
        }

        // ── 3. 캐릭터 이미지 변경 감지 ────
        if (_prevCharacter != CharacterIndex)
        {
            _prevCharacter = CharacterIndex;

            if (CharacterIndex == 0)
            {
                if (characterIcon != null)
                {
                    characterIcon.sprite = null;
                    characterIcon.color = new Color(1, 1, 1, 0);
                }
            }
            else
            {
                if (_manager != null && characterIcon != null)
                {
                    Sprite sprite = _manager.GetCharacterSpritePublic(CharacterIndex);

                    if (sprite != null)
                    {
                        characterIcon.sprite = sprite;
                        characterIcon.color = Color.white;
                    }
                }
            }
        }

        // ── 4. 준비 상태 텍스트 변경 감지 ────
        if (_prevReady != (bool)IsReady)
        {
            _prevReady = IsReady;
            UpdateReadyStatus(IsReady);
        }


    

        if (_prevLoginId != LoginId.ToString())
        {
            _prevLoginId = LoginId.ToString();

            // ✅ 어느 슬롯이 어떤 값으로 갱신하는지 확인
            Debug.Log($"★★★ 슬롯 {_slotIndex} LoginId 변경: [{LoginId}] _isLocal:{_isLocal} IsOccupied:{IsOccupied}");

            if (!_isLocal && IsOccupied && !string.IsNullOrEmpty(LoginId.ToString()))
            {
                playerNameText.text = LoginId.ToString();
            }
        }
    }

   

    public void SetAsMySlot()
    {
        _isLocal = true;
        selectCharacterButton.interactable = true;

        // ✅ LoginId가 이미 동기화됐으면 그걸 사용
        // 아직 동기화 안됐으면 PlayerPrefs에서 읽기
        string myName = LoginId.ToString();
        if (string.IsNullOrEmpty(myName))
        {
            myName = PlayerPrefs.GetString("user_name", "");
            if (string.IsNullOrEmpty(myName))
                myName = $"Player {_slotIndex + 1}";
        }

        playerNameText.text = myName;
        _prevLoginId = myName; // ✅ Render()가 덮어쓰지 않도록 동기화

        Debug.Log($"[Slot.SetAsMySlot] 슬롯:{_slotIndex} 닉네임:{myName}");
    }

    /// <summary>
    /// ✅ 직접 슬롯 점유 (Host용, RPC 아님)
    /// </summary>
    public void OccupySlot(string loginId, PlayerRef player)
    {
        // ✅ 로그 먼저 출력
        Debug.Log("───────────────────────────────────────────");
        Debug.Log($"[Slot.OccupySlot] 호출됨!");
        Debug.Log($"[Slot.OccupySlot] slot: {_slotIndex}");
        Debug.Log($"[Slot.OccupySlot] loginId: {loginId}");
        Debug.Log($"[Slot.OccupySlot] HasStateAuthority: {HasStateAuthority}");

        if (!HasStateAuthority)
        {
            Debug.LogWarning($"[Slot.OccupySlot] ❌ StateAuthority 없음 - 중단");
            Debug.Log("───────────────────────────────────────────");
            return;
        }

        Debug.Log($"[Slot.OccupySlot] IsOccupied (변경 전): {IsOccupied}");
        Debug.Log($"[Slot.OccupySlot] _isLocal: {_isLocal}");

        LoginId = loginId;
        IsOccupied = true;
        CharacterIndex = 0;
        IsReady = false;

        Debug.Log($"[Slot.OccupySlot] IsOccupied (변경 후): {IsOccupied}");
        Debug.Log($"[Slot.OccupySlot] buttonNull: {selectCharacterButton == null}");
        Debug.Log($"[Slot.OccupySlot] interactable: {selectCharacterButton?.interactable}");
        Debug.Log("───────────────────────────────────────────");
    }

    // ─────────────────────────────────────────
    //  RPC 함수들
    // ─────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetCharacter(int characterIndex)
    {
        Debug.Log($"[슬롯 {_slotIndex}] RPC_SetCharacter - 캐릭터 {characterIndex}");
        CharacterIndex = characterIndex;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool ready)
    {
        IsReady = ready;
        Debug.Log($"[슬롯 {_slotIndex}] 준비 상태 변경 → {ready}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ClearSlot()
    {
        LoginId = "";
        IsOccupied = false;
        CharacterIndex = 0;
        IsReady = false;

        Debug.Log($"[슬롯 {_slotIndex}] RPC_ClearSlot - 슬롯 비워짐");
    }

    // ─────────────────────────────────────────
    //  UI 함수들
    // ─────────────────────────────────────────

    /// <summary>
    /// ✅ 비주얼만 초기화 (_isLocal 유지)
    /// </summary>
    void ClearVisualOnly()
    {
        playerNameText.text = "대기 중...";
        readyStatusText.text = "";
        readyStatusText.color = Color.gray;
        selectCharacterButton.interactable = false;

        if (characterIcon != null)
        {
            characterIcon.sprite = null;
            characterIcon.color = new Color(1, 1, 1, 0);
        }

        Debug.Log($"[슬롯 {_slotIndex}] ClearVisualOnly 완료 (_isLocal 유지)");
    }

    /// <summary>
    /// ✅ 전체 초기화 (_isLocal 포함)
    /// </summary>
    void ClearSlotAll()
    {
        playerNameText.text = "대기 중...";
        readyStatusText.text = "";
        readyStatusText.color = Color.gray;
        selectCharacterButton.interactable = false;
        _isLocal = false;

        if (characterIcon != null)
        {
            characterIcon.sprite = null;
            characterIcon.color = new Color(1, 1, 1, 0);
        }

        Debug.Log($"[슬롯 {_slotIndex}] ClearSlotAll 완료");
    }

    public void UpdateReadyStatus(bool ready)
    {
        readyStatusText.text = ready ? "준비 완료!" : "대기 중...";
        readyStatusText.color = ready ? Color.green : Color.gray;
    }

    // ─────────────────────────────────────────
    //  Getter
    // ─────────────────────────────────────────

    public bool IsEmpty() => !IsOccupied;
    public bool GetIsLocal() => _isLocal;
    public int GetCharacterIndex() => CharacterIndex;
    public string GetLoginId() => LoginId.ToString();

    void OnSelectCharacterClicked()
    {
        // ✅ 로그 먼저 출력
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"[Slot.Click] 버튼 클릭됨!");
        Debug.Log($"[Slot.Click] slot: {_slotIndex}");
        Debug.Log($"[Slot.Click] _managerNull: {_manager == null}");
        Debug.Log($"[Slot.Click] _isLocal: {_isLocal}");
        Debug.Log($"[Slot.Click] IsOccupied: {IsOccupied}");
        Debug.Log("═══════════════════════════════════════════");

        if (_manager != null)
        {
            _manager.OpenCharacterSelectPanel(_slotIndex);
        }
        else
        {
            Debug.LogError($"[Slot.Click] ❌ _manager가 null!");
        }
    }
}