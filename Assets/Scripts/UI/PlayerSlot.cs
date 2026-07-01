using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class PlayerSlot : NetworkBehaviour
{
	[Header("UI Elements")]
	public Image characterIcon;

	public TextMeshProUGUI playerNameText;

	public TextMeshProUGUI readyStatusText;

	public Button selectCharacterButton;

	private int _prevCharacter = -1;

	private bool _prevReady;

	private bool _prevOccupied;

	private int _slotIndex = -1;

	private WaitingRoomManager _manager;

	private bool _isLocal;

	private bool _prevButtonState;

	private string _prevLoginId = "";

	[Networked]
	public NetworkString<_32> LoginId { get; set; }

	[Networked]
	public int CharacterIndex { get; set; }

	[Networked]
	public NetworkBool IsReady { get; set; }

	[Networked]
	public NetworkBool IsOccupied { get; set; }

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

	public override void Spawned()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[슬롯.Spawned] 호출됨!");
		Debug.Log($"[슬롯.Spawned] slot: {_slotIndex}");
		Debug.Log($"[슬롯.Spawned] HasStateAuthority: {base.HasStateAuthority}");
		Debug.Log($"[슬롯.Spawned] _isLocal: {_isLocal}");
		Debug.Log($"[슬롯.Spawned] IsOccupied: {IsOccupied}");
		Debug.Log("═══════════════════════════════════════════");
		if (_isLocal)
		{
			Debug.Log($"[슬롯.Spawned] ⚠\ufe0f slot: {_slotIndex} - 이미 내 슬롯 → 초기화 스킵");
			_prevCharacter = -1;
			_prevReady = false;
			_prevOccupied = true;
			return;
		}
		if (base.HasStateAuthority)
		{
			CharacterIndex = 0;
			IsReady = false;
			IsOccupied = false;
			LoginId = "";
			Debug.Log($"[슬롯.Spawned] slot: {_slotIndex} - Host 네트워크 변수 초기화 완료");
		}
		_prevCharacter = -1;
		_prevReady = false;
		_prevOccupied = true;
	}

	public override void Render()
	{
		if (_prevOccupied != (bool)IsOccupied)
		{
			_prevOccupied = IsOccupied;
			if (!IsOccupied)
			{
				if (!_isLocal)
				{
					ClearSlotAll();
				}
				else
				{
					ClearVisualOnly();
				}
			}
		}
		bool flag = (bool)IsOccupied && _isLocal;
		selectCharacterButton.interactable = flag;
		if (_prevButtonState != flag)
		{
			_prevButtonState = flag;
			Debug.Log("───────────────────────────────────────────");
			Debug.Log("[Slot.Render.Button] 버튼 상태 변경됨!");
			Debug.Log($"[Slot.Render.Button] slot: {_slotIndex}");
			Debug.Log($"[Slot.Render.Button] IsOccupied: {IsOccupied}");
			Debug.Log($"[Slot.Render.Button] _isLocal: {_isLocal}");
			Debug.Log($"[Slot.Render.Button] interactable: {selectCharacterButton?.interactable}");
			Debug.Log("───────────────────────────────────────────");
		}
		if (_prevCharacter != CharacterIndex)
		{
			_prevCharacter = CharacterIndex;
			if (CharacterIndex == 0)
			{
				if (characterIcon != null)
				{
					characterIcon.sprite = null;
					characterIcon.color = new Color(1f, 1f, 1f, 0f);
				}
			}
			else if (_manager != null && characterIcon != null)
			{
				Sprite characterSpritePublic = _manager.GetCharacterSpritePublic(CharacterIndex);
				if (characterSpritePublic != null)
				{
					characterIcon.sprite = characterSpritePublic;
					characterIcon.color = Color.white;
				}
			}
		}
		if (_prevReady != (bool)IsReady)
		{
			_prevReady = IsReady;
			UpdateReadyStatus(IsReady);
		}
		if (_prevLoginId != LoginId.ToString())
		{
			_prevLoginId = LoginId.ToString();
			Debug.Log($"★★★ 슬롯 {_slotIndex} LoginId 변경: [{LoginId}] _isLocal:{_isLocal} IsOccupied:{IsOccupied}");
			if (!_isLocal && (bool)IsOccupied && !string.IsNullOrEmpty(LoginId.ToString()))
			{
				playerNameText.text = LoginId.ToString();
			}
		}
	}

	public void SetAsMySlot()
	{
		_isLocal = true;
		selectCharacterButton.interactable = true;
		string text = LoginId.ToString();
		if (string.IsNullOrEmpty(text))
		{
			text = PlayerPrefs.GetString("user_name", "");
			if (string.IsNullOrEmpty(text))
			{
				text = $"Player {_slotIndex + 1}";
			}
		}
		playerNameText.text = text;
		_prevLoginId = text;
		Debug.Log($"[Slot.SetAsMySlot] 슬롯:{_slotIndex} 닉네임:{text}");
	}

	public void OccupySlot(string loginId, PlayerRef player)
	{
		Debug.Log("───────────────────────────────────────────");
		Debug.Log("[Slot.OccupySlot] 호출됨!");
		Debug.Log($"[Slot.OccupySlot] slot: {_slotIndex}");
		Debug.Log("[Slot.OccupySlot] loginId: " + loginId);
		Debug.Log($"[Slot.OccupySlot] HasStateAuthority: {base.HasStateAuthority}");
		if (!base.HasStateAuthority)
		{
			Debug.LogWarning("[Slot.OccupySlot] ❌ StateAuthority 없음 - 중단");
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

	private void ClearVisualOnly()
	{
		playerNameText.text = "대기 중...";
		readyStatusText.text = "";
		readyStatusText.color = Color.gray;
		selectCharacterButton.interactable = false;
		if (characterIcon != null)
		{
			characterIcon.sprite = null;
			characterIcon.color = new Color(1f, 1f, 1f, 0f);
		}
		Debug.Log($"[슬롯 {_slotIndex}] ClearVisualOnly 완료 (_isLocal 유지)");
	}

	private void ClearSlotAll()
	{
		playerNameText.text = "대기 중...";
		readyStatusText.text = "";
		readyStatusText.color = Color.gray;
		selectCharacterButton.interactable = false;
		_isLocal = false;
		if (characterIcon != null)
		{
			characterIcon.sprite = null;
			characterIcon.color = new Color(1f, 1f, 1f, 0f);
		}
		Debug.Log($"[슬롯 {_slotIndex}] ClearSlotAll 완료");
	}

	public void UpdateReadyStatus(bool ready)
	{
		readyStatusText.text = (ready ? "준비 완료!" : "대기 중...");
		readyStatusText.color = (ready ? Color.green : Color.gray);
	}

	public bool IsEmpty()
	{
		return !IsOccupied;
	}

	public bool GetIsLocal()
	{
		return _isLocal;
	}

	public int GetCharacterIndex()
	{
		return CharacterIndex;
	}

	public string GetLoginId()
	{
		return LoginId.ToString();
	}

	private void OnSelectCharacterClicked()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[Slot.Click] 버튼 클릭됨!");
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
			Debug.LogError("[Slot.Click] ❌ _manager가 null!");
		}
	}

}
