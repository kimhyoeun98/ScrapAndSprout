using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class TrashCollector : NetworkBehaviour
{
	public enum CharacterType
	{
		Alpha,
		Beta,
		Gamma,
		Delta
	}

	[Serializable]
	public struct ItemData
	{
		public string itemName;

		public Sprite itemSprite;
	}

	[Header("── 캐릭터 특성 ──")]
	[Tooltip("캐릭터 선택 UI에서 선택된 캐릭터 타입")]
	public CharacterType characterType;

	public Dictionary<string, int> inventory = new Dictionary<string, int>();

	[Header("아이템 데이터 설정")]
	public List<ItemData> itemDatabase;

	[Header("UI 연결 (직접 드래그)")]
	public TextMeshProUGUI goldText;

	public TextMeshProUGUI[] hotbarTexts;

	public Image[] hotbarIcons;

	public TextMeshProUGUI[] inventoryTexts;

	public Image[] inventoryIcons;

	[Header("꾸미기 아이템 구매 가격")]
	[Tooltip("나무 구매 가격 (골드)")]
	public int treePrice = 40;

	[Tooltip("상자 구매 가격 (골드)")]
	public int boxPrice = 20;

	[Tooltip("의자 구매 가격 (골드)")]
	public int chairPrice = 30;

	[Tooltip("울타리 구매 가격 (골드)")]
	public int fencePrice = 50;

	[Tooltip("꽃병 구매 가격 (골드)")]
	public int vasePrice = 60;

	[Tooltip("탁자 구매 가격 (골드)")]
	public int tablePrice = 100;

	[Tooltip("꽃밭 구매 가격 (골드)")]
	public int flowerFieldPrice = 200;

	[Header("상태 메시지 UI (선택)")]
	public TextMeshProUGUI statusMessageText;

	private static readonly Dictionary<string, string> _spriteAlias = new Dictionary<string, string>
	{
		{ "휴지", "pootisue" },
		{ "바나나껍질", "banana" },
		{ "음료캔", "can" },
		{ "디스크", "disk" },
		{ "타이어", "tire" },
		{ "드럼통", "drumcan" },
		{ "컴퓨터", "computer" }
	};

	private readonly Dictionary<string, Sprite> _decoSpriteCache = new Dictionary<string, Sprite>();

	[Networked]
	public int gold { get; set; }

	public override void Spawned()
	{
		string text = base.gameObject.name.ToLower();
		if (text.Contains("alpha"))
		{
			characterType = CharacterType.Alpha;
		}
		else if (text.Contains("beta"))
		{
			characterType = CharacterType.Beta;
		}
		else if (text.Contains("gamma"))
		{
			characterType = CharacterType.Gamma;
		}
		else if (text.Contains("delta"))
		{
			characterType = CharacterType.Delta;
		}
		Debug.Log($"[TrashCollector] 캐릭터 타입: {characterType}");
		if (base.HasInputAuthority)
		{
			FindAndConnectUI();
		}
		ClearAllUI();
		UpdateGoldUI();
		if (!base.HasInputAuthority)
		{
			return;
		}
		string text2 = SceneManager.GetActiveScene().name;
		if (text2 == "DecoScene")
		{
			DecoInventoryBridge.RestoreTo(this);
			SpriteRenderer[] componentsInChildren = GetComponentsInChildren<SpriteRenderer>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			PlayerMovement component = GetComponent<PlayerMovement>();
			if (component != null)
			{
				component.IsMovementLocked = true;
			}
			PlayerNameTag componentInChildren = GetComponentInChildren<PlayerNameTag>();
			if (componentInChildren != null)
			{
				componentInChildren.gameObject.SetActive(value: false);
			}
			Debug.Log("[TrashCollector] DecoScene — 인벤토리 복원, 플레이어 숨김");
		}
		else if (text2 == "TrashZoneScene" && DecoInventoryBridge.IsReturningFromDeco)
		{
			DecoInventoryBridge.RestoreTo(this);
			StartCoroutine(TeleportToReturnPos());
			DecoInventoryBridge.ClearReturnFlag();
			Debug.Log("[TrashCollector] DecoScene 복귀 — 인벤토리·위치 복원");
		}
		else if (text2 == "TrashZoneScene" && SaveManager.IsContinuing && SaveManager.Pending != null && !DecoInventoryBridge.IsReturningFromDeco)
		{
			inventory = new Dictionary<string, int>();
			if (SaveManager.Pending.inventory != null)
			{
				foreach (GameSaveData.InvEntry item in SaveManager.Pending.inventory)
				{
					inventory[item.name] = item.count;
				}
			}
			RPC_SetGold(SaveManager.Pending.gold);
			RefreshUI();
			Debug.Log($"[TrashCollector] 이어하기 — 인벤토리 {inventory.Count}종, 골드 {SaveManager.Pending.gold} 복원");
		}
		else
		{
			LoadPlayerDataFromServer();
			Debug.Log("[TrashCollector] 초기화 완료");
		}
	}

	public override void Render()
	{
		if (base.HasInputAuthority)
		{
			UpdateGoldUI();
		}
	}

	private void LoadPlayerDataFromServer()
	{
		if (ApiManager.Instance == null)
		{
			Debug.LogWarning("[초기화] ApiManager 없음 — 골드 0으로 시작");
			return;
		}
		if (!ApiManager.Instance.IsLoggedIn)
		{
			Debug.LogWarning("[초기화] 로그인 상태가 아닙니다!");
			return;
		}
		ApiManager.Instance.GetPlayerInfo(delegate(PlayerInfoResponse response)
		{
			RPC_SetGold(response.GOLD);
			Debug.Log($"[초기화] 골드 로드 완료: {response.GOLD}G");
		}, delegate(string error)
		{
			Debug.LogWarning("[초기화] 서버 연결 실패, 골드 0으로 시작: " + error);
			RPC_SetGold(0);
		});
	}

	public void SellAllTrash()
	{
		if (inventory.Count == 0)
		{
			ShowStatus("팔 아이템이 없습니다.");
			return;
		}
		List<string> names = new List<string>();
		List<int> list = new List<int>();
		foreach (KeyValuePair<string, int> item in inventory)
		{
			if (!DecoCatalog.IsDeco(item.Key))
			{
				names.Add(item.Key);
				list.Add(item.Value);
			}
		}
		if (names.Count == 0)
		{
			ShowStatus("판매할 쓰레기가 없습니다.");
			return;
		}
		ShowStatus("서버에 판매 요청 중...");
		TrashSellRequest request = new TrashSellRequest
		{
			playerId = ApiManager.Instance.playerId,
			itemNames = names.ToArray(),
			itemCounts = list.ToArray(),
			characterType = characterType.ToString()
		};
		ApiManager.Instance.SellTrash(request, delegate(TradeResponse response)
		{
			int delta = response.gold - gold;
			RPC_SetGold(response.gold);
			foreach (string item2 in names)
			{
				int num = inventory[item2];
				for (int i = 0; i < num; i++)
				{
					GameManager.Instance?.OnTrashCollected();
				}
				inventory.Remove(item2);
				RPC_SetItemCount(item2, 0);
			}
			RefreshUI();
			AchievementManager.Instance?.OnGoldAccumulated(response.gold);
			UIManager.Instance?.ShowFloatingGold(delta, base.transform.position);
			ShowStatus($"판매 완료! 현재 골드: {gold:N0}");
		}, delegate(string error)
		{
			Debug.LogWarning("[판매] 서버 연결 실패, 로컬 처리: " + error);
			int num = 0;
			foreach (string item3 in names)
			{
				float num2 = ((characterType == CharacterType.Delta) ? 1.05f : 1f);
				int localItemPrice = GetLocalItemPrice(item3);
				num += Mathf.RoundToInt((float)(inventory[item3] * localItemPrice) * num2);
				inventory.Remove(item3);
				RPC_SetItemCount(item3, 0);
			}
			RPC_SetGold(gold + num);
			RefreshUI();
			UIManager.Instance?.ShowFloatingGold(num, base.transform.position);
			ShowStatus($"(오프라인) 판매 완료 +{num}G");
		});
	}

	private int GetLocalItemPrice(string itemName)
	{
		int num = Array.IndexOf(new string[7] { "휴지", "바나나껍질", "음료캔", "디스크", "타이어", "드럼통", "컴퓨터" }, itemName);
		if (num >= 0 && num < TrashPile.ItemPrices.Length)
		{
			return TrashPile.ItemPrices[num];
		}
		return 0;
	}

	public void BuyDecorationItem(string itemName)
	{
		int price = GetDecorationPrice(itemName);
		if (price <= 0)
		{
			Debug.LogWarning("[구매] 알 수 없는 아이템: " + itemName);
			return;
		}
		ShowStatus("서버에 구매 요청 중...");
		BuyRequest request = new BuyRequest
		{
			playerId = ApiManager.Instance.playerId,
			itemName = itemName,
			quantity = 1,
			characterType = characterType.ToString()
		};
		ApiManager.Instance.BuyItem(request, delegate(TradeResponse response)
		{
			if (response.success)
			{
				int delta = response.gold - gold;
				RPC_SetGold(response.gold);
				if (inventory.ContainsKey(itemName))
				{
					inventory[itemName]++;
				}
				else
				{
					inventory.Add(itemName, 1);
				}
				RPC_SetItemCount(itemName, inventory[itemName]);
				RefreshUI();
				UIManager.Instance?.ShowFloatingGold(delta, base.transform.position);
				ShowStatus($"{itemName} 구매 완료! 남은 골드: {gold:N0}");
				Debug.Log($"[구매 성공] {itemName} | 서버 골드: {response.gold}");
			}
			else
			{
				ShowStatus(response.message);
			}
		}, delegate(string error)
		{
			Debug.LogWarning("[구매] 서버 연결 실패, 로컬 처리: " + error);
			if (gold < price)
			{
				ShowStatus($"골드 부족! (필요: {price}G, 보유: {gold}G)");
			}
			else
			{
				RPC_SetGold(gold - price);
				if (inventory.ContainsKey(itemName))
				{
					inventory[itemName]++;
				}
				else
				{
					inventory.Add(itemName, 1);
				}
				RPC_SetItemCount(itemName, inventory[itemName]);
				RefreshUI();
				UIManager.Instance?.ShowFloatingGold(-price, base.transform.position);
				ShowStatus("(오프라인) " + itemName + " 구매 완료!");
			}
		});
	}

	private IEnumerator TeleportToReturnPos()
	{
		yield return null;
		PlayerMovement component = GetComponent<PlayerMovement>();
		if (component != null)
		{
			component.RPC_Teleport(DecoInventoryBridge.ReturnPosition);
		}
	}

	private int GetDecorationPrice(string itemName)
	{
		return DecoCatalog.Price(itemName);
	}

	public int GetDecorScore(string itemName)
	{
		if (!DecoCatalog.IsDeco(itemName))
		{
			return 0;
		}
		float num = DecoCatalog.Score(itemName);
		if (characterType == CharacterType.Gamma)
		{
			num *= 1.01f;
		}
		return Mathf.RoundToInt(num);
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_SetGold(int newGold)
	{

		gold = newGold;
		Debug.Log($"[골드] 동기화: {gold}");
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_AddItem(string itemName)
	{

		if (base.HasStateAuthority)
		{
			if (inventory.ContainsKey(itemName))
			{
				inventory[itemName]++;
			}
			else
			{
				inventory.Add(itemName, 1);
			}
			Debug.Log($"[인벤토리] {itemName} 추가! 현재: {inventory[itemName]}개");
			RPC_SyncItem(itemName, inventory[itemName]);
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	public void RPC_SyncItem(string itemName, int newCount)
	{
		if (!base.HasStateAuthority)
		{
			if (newCount <= 0)
			{
				inventory.Remove(itemName);
			}
			else
			{
				inventory[itemName] = newCount;
			}
		}
		Debug.Log($"[인벤토리] UI 갱신: {itemName} = {newCount}");
		RefreshUI();
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_SetItemCount(string itemName, int newCount)
	{
		if (base.HasStateAuthority)
		{
			if (newCount <= 0)
			{
				inventory.Remove(itemName);
			}
			else
			{
				inventory[itemName] = newCount;
			}
			RPC_SyncItem(itemName, newCount);
		}
	}

	public void RefreshUI()
	{
		ClearAllUI();
		UpdateAllUI();
	}

	private void UpdateGoldUI()
	{
		if (goldText != null)
		{
			goldText.text = $"Gold: {gold:N0}";
		}
	}

	private void UpdateAllUI()
	{
		int num = 0;
		foreach (KeyValuePair<string, int> item in inventory)
		{
			Sprite spriteByName = GetSpriteByName(item.Key);
			if (num < hotbarTexts.Length && hotbarTexts[num] != null)
			{
				hotbarTexts[num].text = item.Value.ToString();
			}
			if (spriteByName != null && num < hotbarIcons.Length && hotbarIcons[num] != null)
			{
				hotbarIcons[num].sprite = spriteByName;
				hotbarIcons[num].gameObject.SetActive(value: true);
			}
			if (num < inventoryTexts.Length && inventoryTexts[num] != null)
			{
				inventoryTexts[num].text = item.Value.ToString();
			}
			if (spriteByName != null && num < inventoryIcons.Length && inventoryIcons[num] != null)
			{
				inventoryIcons[num].sprite = spriteByName;
				inventoryIcons[num].gameObject.SetActive(value: true);
			}
			num++;
		}
	}

	private void FindAndConnectUI()
	{
		Debug.Log("[TrashCollector] UI 자동 연결 시작...");
		GameObject gameObject = GameObject.Find("GoldText");
		if (gameObject != null)
		{
			goldText = gameObject.GetComponent<TextMeshProUGUI>();
		}
		hotbarTexts = new TextMeshProUGUI[10];
		hotbarIcons = new Image[10];
		int num = 0;
		for (int i = 0; i < 10; i++)
		{
			GameObject gameObject2 = GameObject.Find((i == 0) ? "Slot" : $"Slot ({i})");
			if (gameObject2 != null)
			{
				Transform transform = gameObject2.transform.Find("CountText");
				Transform transform2 = gameObject2.transform.Find("ItemIcon");
				if (transform != null)
				{
					hotbarTexts[i] = transform.GetComponent<TextMeshProUGUI>();
					num++;
				}
				if (transform2 != null)
				{
					hotbarIcons[i] = transform2.GetComponent<Image>();
				}
			}
		}
		Debug.Log($"[TrashCollector] ✅ Hotbar 연결: {num}개");
		inventoryTexts = new TextMeshProUGUI[24];
		inventoryIcons = new Image[24];
		GameObject gameObject3 = GameObject.Find("UIManager");
		if (gameObject3 == null)
		{
			UIManager[] array = UnityEngine.Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			if (array.Length != 0)
			{
				gameObject3 = array[0].gameObject;
			}
		}
		if (!(gameObject3 != null))
		{
			return;
		}
		Transform transform3 = gameObject3.transform.Find("InventoryPanel");
		if (!(transform3 != null))
		{
			return;
		}
		Transform transform4 = transform3.Find("Grid");
		if (!(transform4 != null))
		{
			return;
		}
		int num2 = 0;
		for (int j = 0; j < 24; j++)
		{
			string n = ((j == 0) ? "BagSlot" : $"BagSlot ({j})");
			Transform transform5 = transform4.Find(n);
			if (transform5 != null)
			{
				Transform transform6 = transform5.Find("InvenCountText");
				Transform transform7 = transform5.Find("InvenIcon");
				if (transform6 != null)
				{
					inventoryTexts[j] = transform6.GetComponent<TextMeshProUGUI>();
					num2++;
				}
				if (transform7 != null)
				{
					inventoryIcons[j] = transform7.GetComponent<Image>();
				}
			}
		}
		Debug.Log($"[TrashCollector] ✅ Inventory 연결: {num2}개");
	}

	private void ClearAllUI()
	{
		TextMeshProUGUI[] array = hotbarTexts;
		foreach (TextMeshProUGUI textMeshProUGUI in array)
		{
			if ((bool)textMeshProUGUI)
			{
				textMeshProUGUI.text = "";
			}
		}
		Image[] array2 = hotbarIcons;
		foreach (Image image in array2)
		{
			if ((bool)image)
			{
				image.gameObject.SetActive(value: false);
			}
		}
		array = inventoryTexts;
		foreach (TextMeshProUGUI textMeshProUGUI2 in array)
		{
			if ((bool)textMeshProUGUI2)
			{
				textMeshProUGUI2.text = "";
			}
		}
		array2 = inventoryIcons;
		foreach (Image image2 in array2)
		{
			if ((bool)image2)
			{
				image2.gameObject.SetActive(value: false);
			}
		}
	}

	public Sprite GetSpriteByName(string name)
	{
		if (itemDatabase != null)
		{
			foreach (ItemData item in itemDatabase)
			{
				if (item.itemName == name)
				{
					return item.itemSprite;
				}
			}
			if (_spriteAlias.TryGetValue(name, out var value))
			{
				foreach (ItemData item2 in itemDatabase)
				{
					if (item2.itemName == value)
					{
						return item2.itemSprite;
					}
				}
			}
		}
		return GetDecoSprite(name);
	}

	private Sprite GetDecoSprite(string name)
	{
		if (_decoSpriteCache.TryGetValue(name, out var value))
		{
			return value;
		}
		Sprite sprite = null;
		string text = DecoCatalog.PrefabPath(name);
		if (text != null)
		{
			GameObject gameObject = Resources.Load<GameObject>(text);
			if (gameObject != null)
			{
				SpriteRenderer componentInChildren = gameObject.GetComponentInChildren<SpriteRenderer>();
				if (componentInChildren != null)
				{
					sprite = componentInChildren.sprite;
				}
			}
		}
		_decoSpriteCache[name] = sprite;
		return sprite;
	}

	private void ShowStatus(string message)
	{
		Debug.Log("[상태] " + message);
		if (statusMessageText != null)
		{
			statusMessageText.text = message;
		}
	}

}
