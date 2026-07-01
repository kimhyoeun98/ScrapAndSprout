using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class WaitingRoomManager : NetworkBehaviour
{
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

	private bool _isSpawned;

	private int _lastReadyCount = -1;

	private bool _slotRequested;

	public static WaitingRoomManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Debug.Log("[웨이팅룸] Instance 설정 완료");
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void Update()
	{
		if (_isSpawned && !(base.Object == null) && base.Object.HasStateAuthority && startGameButton != null)
		{
			bool interactable = CheckAllPlayersReady();
			startGameButton.interactable = interactable;
		}
	}

	public override void Spawned()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[WRM.Spawned] 시작");
		Debug.Log($"[WRM.Spawned] HasStateAuthority: {base.Object.HasStateAuthority}");
		Debug.Log($"[WRM.Spawned] IsServer: {base.Runner.IsServer}");
		Debug.Log($"[WRM.Spawned] LocalPlayer: {base.Runner.LocalPlayer}");
		Debug.Log($"[WRM.Spawned] _mySlotIndex: {_mySlotIndex}");
		Debug.Log("═══════════════════════════════════════════");
		if (_mySlotIndex >= 0)
		{
			_isSpawned = true;
			Debug.Log($"[WRM.Spawned] 이미 슬롯 배정됨({_mySlotIndex}번) - 전체 스킵");
			return;
		}
		_isSpawned = true;
		InitializeSlots();
		DisplayRoomInfo();
		if (characterSelectPanel != null)
		{
			characterSelectPanel.SetActive(value: false);
		}
		SetupButtons();
		string text = ((PhotonManager.Instance != null && !string.IsNullOrEmpty(PhotonManager.Instance.LocalPlayerName)) ? PhotonManager.Instance.LocalPlayerName : PlayerPrefs.GetString("user_name", "Guest"));
		if (base.Object.HasStateAuthority)
		{
			PhotonManager.Instance?.ResetBotCache();
			Debug.Log("[WRM.Spawned] Host 모드 - 슬롯 0번 배정 시작");
			AssignHostSlot(text);
			Debug.Log("[WRM.Spawned] Host 모드 - 슬롯 0번 배정 완료");
			if (startGameButton != null)
			{
				startGameButton.gameObject.SetActive(value: true);
				startGameButton.interactable = false;
			}
			if (addBotButton != null)
			{
				addBotButton.gameObject.SetActive(value: true);
			}
		}
		else
		{
			if (!_slotRequested)
			{
				_slotRequested = true;
				Debug.Log("[WRM.Spawned] Client 모드 - 슬롯 요청 (최초 1회)");
				RPC_RequestSlot(text);
			}
			else
			{
				Debug.Log("[WRM.Spawned] Client 모드 - 슬롯 요청 스킵");
			}
			if (startGameButton != null)
			{
				startGameButton.gameObject.SetActive(value: false);
			}
			if (addBotButton != null)
			{
				addBotButton.gameObject.SetActive(value: false);
			}
		}
		Debug.Log("═══════════════════════════════════════════");
	}

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		_isSpawned = false;
		_slotRequested = false;
		Debug.Log("[WRM.Despawned] 호출됨");
	}

	private void SetMySlotIndex(int index)
	{
		_mySlotIndex = index;
		Debug.Log($"[WRM.SetMySlotIndex] _mySlotIndex 설정됨: {index}");
	}

	private void InitializeSlots()
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

	private void DisplayRoomInfo()
	{
		string text = PlayerPrefs.GetString("RoomName", "");
		string text2 = PlayerPrefs.GetString("RoomCode", "????");
		Debug.Log("[웨이팅룸] PlayerPrefs 읽기:");
		Debug.Log("  - RoomName: " + text);
		Debug.Log("  - RoomCode: " + text2);
		if (roomNameText != null)
		{
			roomNameText.text = (string.IsNullOrEmpty(text) ? "방이름: (없음)" : ("방이름: " + text));
		}
		else
		{
			Debug.LogWarning("[웨이팅룸] ⚠\ufe0f roomNameText가 null!");
		}
		if (roomCodeText != null)
		{
			roomCodeText.text = "방 코드: " + text2;
		}
		else
		{
			Debug.LogWarning("[웨이팅룸] ⚠\ufe0f roomCodeText가 null!");
		}
	}

	private void AssignHostSlot(string myName)
	{
		Debug.Log("[웨이팅룸] Host 모드 - 슬롯 0번 자동 배정");
		SetMySlotIndex(0);
		if (playerSlots[0] != null)
		{
			playerSlots[0].OccupySlot(myName, base.Runner.LocalPlayer);
			playerSlots[0].SetAsMySlot();
			Debug.Log("[웨이팅룸] ✅ Host 슬롯 0번 배정 완료");
		}
	}

	private void SetupButtons()
	{
		if (alphaButton != null)
		{
			alphaButton.onClick.AddListener(delegate
			{
				OnCharacterSelected(1);
			});
		}
		if (betaButton != null)
		{
			betaButton.onClick.AddListener(delegate
			{
				OnCharacterSelected(2);
			});
		}
		if (gammaButton != null)
		{
			gammaButton.onClick.AddListener(delegate
			{
				OnCharacterSelected(3);
			});
		}
		if (deltaButton != null)
		{
			deltaButton.onClick.AddListener(delegate
			{
				OnCharacterSelected(4);
			});
		}
		if (closeButton != null)
		{
			closeButton.onClick.AddListener(CloseCharacterSelectPanel);
		}
		if (exitButton != null)
		{
			exitButton.onClick.AddListener(OnExitClicked);
		}
		if (readyButton != null)
		{
			readyButton.onClick.AddListener(OnReadyClicked);
		}
		if (startGameButton != null)
		{
			startGameButton.onClick.AddListener(OnStartGameClicked);
		}
		if (addBotButton != null)
		{
			addBotButton.onClick.AddListener(OnAddBotClicked);
		}
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_RequestSlot(string playerName, RpcInfo info = default(RpcInfo))
	{

		Debug.Log("[슬롯 요청] 수신 - 요청자: " + playerName);
		for (int i = 1; i < playerSlots.Length; i++)
		{
			if (playerSlots[i] != null && !playerSlots[i].IsOccupied)
			{
				playerSlots[i].OccupySlot(playerName, info.Source);
				RPC_NotifySlotAssigned(info.Source, i, playerName);
				Debug.Log($"[슬롯 배정] ✅ 슬롯 {i}번 배정 완료");
				return;
			}
		}
		Debug.LogWarning("[슬롯 배정] ❌ 빈 슬롯 없음!");
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_NotifySlotAssigned(PlayerRef player, int slotIndex, string playerName)
	{

		if (playerSlots[slotIndex] != null)
		{
			string text = (string.IsNullOrEmpty(playerName) ? $"Player {slotIndex + 1}" : playerName);
			playerSlots[slotIndex].playerNameText.text = text;
			Debug.Log($"[슬롯 알림] 슬롯 {slotIndex} 이름 갱신: {text}");
		}
		if (player == base.Runner.LocalPlayer)
		{
			Debug.Log($"[슬롯 알림] ✅ 내 슬롯 배정됨 - 슬롯 {slotIndex}번");
			SetMySlotIndex(slotIndex);
			if (playerSlots[slotIndex] != null)
			{
				playerSlots[slotIndex].SetAsMySlot();
			}
		}
	}

	public void OpenCharacterSelectPanel(int slotIndex)
	{
		Debug.Log("[WRM.OpenPanel] 호출됨!");
		Debug.Log($"[WRM.OpenPanel] requestedSlot: {slotIndex}");
		Debug.Log($"[WRM.OpenPanel] mySlot: {_mySlotIndex}");
		_currentSelectingSlotIndex = slotIndex;
		if (characterSelectPanel != null)
		{
			characterSelectPanel.SetActive(value: true);
			Debug.Log("[WRM.OpenPanel] 패널 활성화 완료");
		}
		else
		{
			Debug.LogError("[WRM.OpenPanel] ❌ characterSelectPanel이 null!");
		}
	}

	private void CloseCharacterSelectPanel()
	{
		if (characterSelectPanel != null)
		{
			characterSelectPanel.SetActive(value: false);
		}
		_currentSelectingSlotIndex = -1;
	}

	private void OnCharacterSelected(int characterIndex)
	{
		if (_currentSelectingSlotIndex < 0 || _currentSelectingSlotIndex >= playerSlots.Length)
		{
			Debug.LogWarning("[캐릭터 선택] ❌ 잘못된 슬롯 인덱스");
			return;
		}
		for (int i = 0; i < playerSlots.Length; i++)
		{
			if (i != _currentSelectingSlotIndex && playerSlots[i] != null && playerSlots[i].CharacterIndex == characterIndex)
			{
				Debug.LogWarning($"[캐릭터 선택] ❌ 캐릭터 {characterIndex}는 슬롯 {i}번이 이미 선택함");
				CloseCharacterSelectPanel();
				return;
			}
		}
		PlayerSlot playerSlot = playerSlots[_currentSelectingSlotIndex];
		if (playerSlot != null && (bool)playerSlot.IsOccupied)
		{
			playerSlot.RPC_SetCharacter(characterIndex);
			RPC_NotifyCharacterSelected(_currentSelectingSlotIndex, characterIndex);
			Debug.Log($"[캐릭터 선택] 슬롯:{_currentSelectingSlotIndex} → 캐릭터:{characterIndex}");
		}
		RefreshCharacterButtonStates();
		CloseCharacterSelectPanel();
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	private void RPC_NotifyCharacterSelected(int slotIndex, int characterIndex)
	{

		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.SetPlayerCharacter(slotIndex, characterIndex);
			Debug.Log($"[캐릭터 RPC] Host에 저장 완료 - 슬롯:{slotIndex} → 캐릭터:{characterIndex}");
			RPC_RefreshCharacterButtons();
		}
	}

	private void OnReadyClicked()
	{
		Debug.Log("═══════════════════════════════════════════");
		Debug.Log("[준비 완료] 버튼 클릭!");
		Debug.Log($"[준비 완료] mySlot: {_mySlotIndex}");
		if (_mySlotIndex < 0 || _mySlotIndex >= playerSlots.Length)
		{
			Debug.LogWarning("[준비 완료] ❌ 잘못된 슬롯 인덱스");
			Debug.Log("═══════════════════════════════════════════");
			return;
		}
		PlayerSlot playerSlot = playerSlots[_mySlotIndex];
		if (playerSlot == null)
		{
			Debug.LogWarning("[준비 완료] ❌ 내 슬롯이 null");
			Debug.Log("═══════════════════════════════════════════");
			return;
		}
		Debug.Log($"[준비 완료] IsOccupied: {playerSlot.IsOccupied}");
		Debug.Log($"[준비 완료] CharacterIndex: {playerSlot.CharacterIndex}");
		Debug.Log($"[준비 완료] IsReady (현재): {playerSlot.IsReady}");
		if (!playerSlot.IsOccupied)
		{
			Debug.LogWarning("[준비 완료] ❌ 내 슬롯이 점유되지 않음");
			Debug.Log("═══════════════════════════════════════════");
			return;
		}
		if (playerSlot.CharacterIndex == 0)
		{
			Debug.LogWarning("[준비 완료] ❌ 캐릭터를 먼저 선택하세요!");
			Debug.Log("═══════════════════════════════════════════");
			return;
		}
		bool flag = !playerSlot.IsReady;
		Debug.Log($"[준비 완료] 새로운 준비 상태: {flag}");
		Debug.Log("[준비 완료] RPC_SetReady 호출 시작");
		playerSlot.RPC_SetReady(flag);
		Debug.Log("[준비 완료] RPC_SetReady 호출 완료");
		if (readyButton != null)
		{
			TextMeshProUGUI componentInChildren = readyButton.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.text = (flag ? "준비 취소" : "준비 완료");
				Debug.Log("[준비 완료] 버튼 텍스트 변경: " + componentInChildren.text);
			}
			else
			{
				Debug.LogWarning("[준비 완료] ⚠\ufe0f buttonText null!");
			}
		}
		else
		{
			Debug.LogWarning("[준비 완료] ⚠\ufe0f readyButton null!");
		}
		Debug.Log("═══════════════════════════════════════════");
	}

	private void OnStartGameClicked()
	{
		if (!base.Object.HasStateAuthority)
		{
			Debug.LogWarning("[게임 시작] ❌ Host만 게임을 시작할 수 있습니다!");
			return;
		}
		if (!CheckAllPlayersReady())
		{
			Debug.LogWarning("[게임 시작] ❌ 모든 플레이어가 준비되지 않았습니다!");
			return;
		}
		Debug.Log("[게임 시작] ✅ TrashZoneScene으로 전환");
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LoadGameScene();
		}
		else
		{
			Debug.LogError("[게임 시작] ❌ PhotonManager.Instance가 null!");
		}
	}

	private void OnAddBotClicked()
	{
		if (!base.Object.HasStateAuthority)
		{
			Debug.LogWarning("[봇 추가] ❌ Host만 봇을 추가할 수 있습니다!");
			return;
		}
		for (int i = 1; i < playerSlots.Length; i++)
		{
			if (!(playerSlots[i] != null) || (bool)playerSlots[i].IsOccupied)
			{
				continue;
			}
			bool[] array = new bool[5];
			for (int j = 0; j < playerSlots.Length; j++)
			{
				if (playerSlots[j] != null && (bool)playerSlots[j].IsOccupied)
				{
					int characterIndex = playerSlots[j].CharacterIndex;
					if (characterIndex >= 1 && characterIndex <= 4)
					{
						array[characterIndex] = true;
					}
				}
			}
			int num = -1;
			for (int k = 1; k <= 4; k++)
			{
				if (!array[k])
				{
					num = k;
					break;
				}
			}
			if (num == -1)
			{
				Debug.LogWarning("[봇 추가] ❌ 선택 가능한 캐릭터가 없습니다!");
				return;
			}
			string text = $"Bot_{i}";
			playerSlots[i].OccupySlot(text, PlayerRef.None);
			playerSlots[i].RPC_SetCharacter(num);
			playerSlots[i].RPC_SetReady(ready: true);
			if (PhotonManager.Instance != null)
			{
				PhotonManager.Instance.SetPlayerCharacter(i, num);
			}
			int num2 = PlayerPrefs.GetInt("BotCount", 0);
			PlayerPrefs.SetInt("BotCount", num2 + 1);
			PlayerPrefs.SetInt($"BotCharacter_{num2}", num);
			PlayerPrefs.Save();
			Debug.Log($"[봇 추가] ✅ 슬롯 {i}번에 {text} (캐릭터:{num}) 추가 완료");
			return;
		}
		Debug.LogWarning("[봇 추가] ❌ 빈 슬롯이 없습니다!");
	}

	private bool CheckAllPlayersReady()
	{
		if (!_isSpawned)
		{
			return false;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < playerSlots.Length; i++)
		{
			if (!(playerSlots[i] == null) && !(playerSlots[i].Object == null) && playerSlots[i].Object.IsValid && (bool)playerSlots[i].IsOccupied)
			{
				num++;
				if ((bool)playerSlots[i].IsReady)
				{
					num2++;
				}
			}
		}
		bool flag = num >= 2 && num == num2;
		if (num2 != _lastReadyCount)
		{
			_lastReadyCount = num2;
			Debug.Log($"[준비 확인] 점유: {num}, 준비: {num2}, 결과: {flag}");
		}
		return flag;
	}

	public void RefreshCharacterButtonStates()
	{
		bool[] array = new bool[5];
		for (int i = 0; i < playerSlots.Length; i++)
		{
			if (playerSlots[i] != null && (bool)playerSlots[i].IsOccupied)
			{
				int characterIndex = playerSlots[i].CharacterIndex;
				if (characterIndex >= 1 && characterIndex <= 4)
				{
					array[characterIndex] = true;
				}
			}
		}
		if (alphaButton != null)
		{
			alphaButton.interactable = !array[1];
		}
		if (betaButton != null)
		{
			betaButton.interactable = !array[2];
		}
		if (gammaButton != null)
		{
			gammaButton.interactable = !array[3];
		}
		if (deltaButton != null)
		{
			deltaButton.interactable = !array[4];
		}
		Debug.Log($"[캐릭터 버튼] Alpha:{!array[1]} Beta:{!array[2]} Gamma:{!array[3]} Delta:{!array[4]}");
	}

	public void OnPlayerLeft(PlayerRef player)
	{
		Debug.Log($"[웨이팅룸] 플레이어 퇴장: {player.PlayerId}");
		int num = Mathf.Clamp(player.PlayerId - 1, 0, 3);
		if (num >= 0 && num < playerSlots.Length && playerSlots[num] != null)
		{
			playerSlots[num].RPC_ClearSlot();
		}
	}

	private void OnExitClicked()
	{
		Debug.Log("[웨이팅룸] 방 나가기 버튼 클릭");
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LeaveRoom();
			return;
		}
		Debug.LogError("[웨이팅룸] PhotonManager.Instance가 null!");
		SceneManager.LoadScene("LobbyScene");
	}

	public Sprite GetCharacterSpritePublic(int index)
	{
		return index switch
		{
			1 => alphaSprite, 
			2 => betaSprite, 
			3 => gammaSprite, 
			4 => deltaSprite, 
			_ => null, 
		};
	}

	public string GetCharacterNamePublic(int index)
	{
		return index switch
		{
			1 => "Alpha", 
			2 => "Beta", 
			3 => "Gamma", 
			4 => "Delta", 
			_ => "", 
		};
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_RefreshCharacterButtons()
	{

		RefreshCharacterButtonStates();
	}

	public bool IsCharacterPanelOpen()
	{
		if (characterSelectPanel != null)
		{
			return characterSelectPanel.activeSelf;
		}
		return false;
	}

}
