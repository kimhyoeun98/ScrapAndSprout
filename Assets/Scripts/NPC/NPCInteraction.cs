using System;
using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCInteraction : NetworkBehaviour
{
	private struct ItemDef
	{
		public string name;

		public int price;

		public Color iconColor;

		public string iconChar;

		public Action onClick;
	}

	[Header("UI 연결 (Inspector에서 드래그)")]
	public GameObject interactionUI;

	public GameObject shopPanel;

	public TextMeshProUGUI shopInfoText;

	[Header("대사 (상점 열기 전 인사말)")]
	[Tooltip("대사창 좌상단에 표시할 이름")]
	public string npcName = "상인";

	[Tooltip("F로 한 줄씩 넘어가며, 마지막 줄에서 상점이 열립니다")]
	[TextArea(1, 3)]
	public string[] dialogueLines = new string[4] { "어서 오게, 꼬마로봇이여!", "그래, 쓰레기는 많이 가져왔나?", "내가 쓸 만한 물건들을 좀 가져왔는데.", "천천히 둘러보고 마음에 드는 걸 골라보게나." };

	[Tooltip("타자기 효과 속도 (글자당 초). 0이면 즉시 표시")]
	public float typeSpeed = 0.04f;

	private bool _isPlayerNearby;

	private TrashCollector _playerCollector;

	private GameObject _dialogueBox;

	private TextMeshProUGUI _dialogueNameText;

	private TextMeshProUGUI _dialogueBodyText;

	private TextMeshProUGUI _dialogueIndicator;

	private bool _dialogueActive;

	private bool _hasGreeted;

	private bool _isTyping;

	private int _dialogueIndex;

	private string _currentFullLine = "";

	private Coroutine _typeCoroutine;

	private static readonly Color C_BG = new Color(0.06f, 0.06f, 0.11f, 0.98f);

	private static readonly Color C_HEADER = new Color(0.03f, 0.03f, 0.07f, 1f);

	private static readonly Color C_SLOT = new Color(0.1f, 0.1f, 0.18f, 1f);

	private static readonly Color C_SLOT_BD = new Color(0.22f, 0.27f, 0.45f, 1f);

	private static readonly Color C_TITLE = new Color(0.92f, 0.88f, 0.76f, 1f);

	private static readonly Color C_PRICE = new Color(0.96f, 0.83f, 0.25f, 1f);

	private static readonly Color C_GOLD_BAR = new Color(0.08f, 0.09f, 0.16f, 1f);

	private static readonly Color C_BTN_BUY = new Color(0.14f, 0.4f, 0.78f, 1f);

	private static readonly Color C_BTN_SELL = new Color(0.65f, 0.18f, 0.14f, 1f);

	private static readonly Color C_DIVIDER = new Color(0.2f, 0.24f, 0.4f, 0.7f);

	private void Start()
	{
		Canvas[] array;
		if (shopPanel == null)
		{
			array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				Transform transform = array[i].transform.Find("ShopPanel");
				if (transform != null)
				{
					UnityEngine.Object.Destroy(transform.gameObject);
				}
			}
			BuildShopPanelMS();
		}
		if (shopInfoText == null && shopPanel != null)
		{
			Transform transform2 = shopPanel.transform.Find("ShopInfoText");
			if (transform2 != null)
			{
				shopInfoText = transform2.GetComponent<TextMeshProUGUI>();
			}
		}
		if (interactionUI == null)
		{
			array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				Transform transform3 = array[i].transform.Find("NPC_InteractionUI");
				if (transform3 != null)
				{
					interactionUI = transform3.gameObject;
					break;
				}
			}
		}
		array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			Transform transform4 = array[i].transform.Find("NPC_DialogueBox");
			if (transform4 != null)
			{
				UnityEngine.Object.Destroy(transform4.gameObject);
			}
		}
		BuildDialogueBox();
		if (shopPanel != null)
		{
			shopPanel.SetActive(value: false);
		}
		if (interactionUI != null)
		{
			interactionUI.SetActive(value: false);
		}
		if (_dialogueBox != null)
		{
			_dialogueBox.SetActive(value: false);
		}
	}

	private void Update()
	{
		Collider2D[] array = Physics2D.OverlapCircleAll(base.transform.position, 2f);
		bool flag = false;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			if (!collider2D.CompareTag("Player") || !collider2D.gameObject.activeInHierarchy)
			{
				continue;
			}
			PlayerMovement component = collider2D.GetComponent<PlayerMovement>();
			if (component == null || !component.HasInputAuthority)
			{
				continue;
			}
			TrashCollector component2 = collider2D.GetComponent<TrashCollector>();
			if (!(component2 == null) && component2.HasInputAuthority)
			{
				if (!_isPlayerNearby && interactionUI != null && !_dialogueActive && (shopPanel == null || !shopPanel.activeSelf))
				{
					interactionUI.SetActive(value: true);
				}
				_isPlayerNearby = true;
				_playerCollector = component2;
				flag = true;
				break;
			}
		}
		if (!flag && _isPlayerNearby)
		{
			_isPlayerNearby = false;
			_playerCollector = null;
			CloseDialogue();
			_hasGreeted = false;
			if (shopPanel != null)
			{
				shopPanel.SetActive(value: false);
			}
			if (interactionUI != null)
			{
				interactionUI.SetActive(value: false);
			}
		}
		if (!_isPlayerNearby)
		{
			return;
		}
		if (_dialogueActive)
		{
			if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
			{
				AdvanceDialogue();
			}
		}
		else if (Input.GetKeyDown(KeyCode.F))
		{
			if (!_hasGreeted)
			{
				StartDialogue();
			}
			else
			{
				ToggleShop();
			}
		}
	}

	public void ToggleShop()
	{
		if (shopPanel == null)
		{
			Debug.LogError("[NPC] shopPanel이 연결되지 않았습니다!");
			return;
		}
		bool flag = !shopPanel.activeSelf;
		_ = _playerCollector != null;
		shopPanel.SetActive(flag);
		if (flag)
		{
			if (interactionUI != null)
			{
				interactionUI.SetActive(value: false);
			}
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			RefreshShopInfo();
			Debug.Log("[NPC] 상점 열림");
		}
		else
		{
			if (interactionUI != null)
			{
				interactionUI.SetActive(value: true);
			}
			Debug.Log("[NPC] 상점 닫힘");
		}
	}

	private TrashCollector ResolveCollector()
	{
		if (_playerCollector != null)
		{
			return _playerCollector;
		}
		PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		foreach (PlayerMovement playerMovement in array)
		{
			if (playerMovement.HasInputAuthority)
			{
				_playerCollector = playerMovement.GetComponent<TrashCollector>();
				break;
			}
		}
		return _playerCollector;
	}

	public void OnSellButtonClicked()
	{
		if (ResolveCollector() == null)
		{
			Debug.LogWarning("[NPC] 플레이어를 찾을 수 없습니다!");
			return;
		}
		_playerCollector.SellAllTrash();
		Invoke("RefreshShopInfo", 0.5f);
	}

	public void OnBuyTreeButtonClicked()
	{
		BuyDeco("나무풍 나무");
	}

	public void OnBuyBoxButtonClicked()
	{
		BuyDeco("나무풍 상자");
	}

	public void OnBuyChairButtonClicked()
	{
		BuyDeco("나무풍 의자");
	}

	public void OnBuyFenceButtonClicked()
	{
		BuyDeco("나무풍 울타리");
	}

	public void OnBuyVaseButtonClicked()
	{
		BuyDeco("나무풍 꽃병");
	}

	public void OnBuyTableButtonClicked()
	{
		BuyDeco("나무풍 탁자");
	}

	public void OnBuyFlowerFieldButtonClicked()
	{
		BuyDeco("나무풍 꽃밭");
	}

	private void BuyDeco(string itemName)
	{
		if (TutorialManager.IsTutorialActive && itemName != TutorialManager.AllowedPurchaseItem)
		{
			UIManager.Instance?.ShowStatusMessage("튜토리얼 중에는 구매할 수 없습니다.");
			Debug.Log("[NPC] 튜토리얼 중 구매 차단: " + itemName);
		}
		else if (ResolveCollector() == null)
		{
			Debug.LogWarning("[NPC] 플레이어를 찾을 수 없습니다! (" + itemName + ")");
		}
		else
		{
			_playerCollector.BuyDecorationItem(itemName);
			Invoke("RefreshShopInfo", 0.5f);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!other.CompareTag("Player") || !other.gameObject.activeInHierarchy)
		{
			return;
		}
		PlayerMovement component = other.GetComponent<PlayerMovement>();
		if (!(component == null) && component.HasInputAuthority)
		{
			_isPlayerNearby = true;
			_playerCollector = other.GetComponent<TrashCollector>();
			if (interactionUI != null && (shopPanel == null || !shopPanel.activeSelf))
			{
				interactionUI.SetActive(value: true);
			}
			Debug.Log("[NPC] 플레이어 접근 감지 — F키로 상점을 열 수 있습니다.");
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}
		PlayerMovement component = other.GetComponent<PlayerMovement>();
		if (!(component == null) && component.HasInputAuthority)
		{
			_isPlayerNearby = false;
			_playerCollector = null;
			_hasGreeted = false;
			CloseDialogue();
			if (shopPanel != null)
			{
				shopPanel.SetActive(value: false);
			}
			if (interactionUI != null)
			{
				interactionUI.SetActive(value: false);
			}
			Debug.Log("[NPC] 플레이어 이탈 — 상점 닫힘");
		}
	}

	private void RefreshShopInfo()
	{
		if (!(shopInfoText == null) && !(_playerCollector == null))
		{
			shopInfoText.text = $"보유 골드:  {_playerCollector.gold:N0} G";
		}
	}

	private void StartDialogue()
	{
		if (dialogueLines == null || dialogueLines.Length == 0)
		{
			_hasGreeted = true;
			ToggleShop();
			return;
		}
		if (_dialogueBox == null)
		{
			BuildDialogueBox();
		}
		if (_dialogueBox == null)
		{
			ToggleShop();
			return;
		}
		_dialogueActive = true;
		_dialogueIndex = 0;
		if (interactionUI != null)
		{
			interactionUI.SetActive(value: false);
		}
		if (_dialogueNameText != null)
		{
			_dialogueNameText.text = npcName;
		}
		_dialogueBox.SetActive(value: true);
		_dialogueBox.transform.SetAsLastSibling();
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
		ShowLine(0);
	}

	public void AdvanceDialogue()
	{
		if (!_dialogueActive)
		{
			return;
		}
		if (_isTyping)
		{
			if (_typeCoroutine != null)
			{
				StopCoroutine(_typeCoroutine);
				_typeCoroutine = null;
			}
			if (_dialogueBodyText != null)
			{
				_dialogueBodyText.maxVisibleCharacters = _currentFullLine.Length;
			}
			_isTyping = false;
			if (_dialogueIndicator != null)
			{
				_dialogueIndicator.gameObject.SetActive(value: true);
			}
		}
		else
		{
			_dialogueIndex++;
			if (_dialogueIndex >= dialogueLines.Length)
			{
				EndDialogue();
			}
			else
			{
				ShowLine(_dialogueIndex);
			}
		}
	}

	private void ShowLine(int index)
	{
		_currentFullLine = dialogueLines[index];
		if (_typeCoroutine != null)
		{
			StopCoroutine(_typeCoroutine);
		}
		_typeCoroutine = StartCoroutine(TypeLine(_currentFullLine));
	}

	private IEnumerator TypeLine(string line)
	{
		_isTyping = true;
		if (_dialogueIndicator != null)
		{
			_dialogueIndicator.gameObject.SetActive(value: false);
		}
		if (_dialogueBodyText != null)
		{
			_dialogueBodyText.text = line;
			_dialogueBodyText.maxVisibleCharacters = 0;
			int total = line.Length;
			if (typeSpeed <= 0f)
			{
				_dialogueBodyText.maxVisibleCharacters = total;
			}
			else
			{
				for (int c = 1; c <= total; c++)
				{
					_dialogueBodyText.maxVisibleCharacters = c;
					yield return new WaitForSeconds(typeSpeed);
				}
			}
		}
		_isTyping = false;
		if (_dialogueIndicator != null)
		{
			_dialogueIndicator.gameObject.SetActive(value: true);
		}
		_typeCoroutine = null;
	}

	private void EndDialogue()
	{
		_hasGreeted = true;
		CloseDialogue();
		if (shopPanel != null && !shopPanel.activeSelf)
		{
			ToggleShop();
		}
	}

	private void CloseDialogue()
	{
		if (_typeCoroutine != null)
		{
			StopCoroutine(_typeCoroutine);
			_typeCoroutine = null;
		}
		_isTyping = false;
		_dialogueActive = false;
		if (_dialogueBox != null)
		{
			_dialogueBox.SetActive(value: false);
		}
	}

	private void BuildDialogueBox()
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (!(canvas == null))
		{
			RectTransform component = canvas.GetComponent<RectTransform>();
			GameObject gameObject = new GameObject("NPC_DialogueBox", typeof(RectTransform), typeof(Image), typeof(Button));
			gameObject.transform.SetParent(component, worldPositionStays: false);
			RectTransform component2 = gameObject.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			Vector2 offsetMin = (component2.offsetMax = Vector2.zero);
			component2.offsetMin = offsetMin;
			gameObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
			Button component3 = gameObject.GetComponent<Button>();
			component3.transition = Selectable.Transition.None;
			component3.onClick.AddListener(AdvanceDialogue);
			_dialogueBox = gameObject;
			RectTransform component4 = MkImg(component2, "Box", C_BG).GetComponent<RectTransform>();
			component4.anchorMin = new Vector2(0f, 0f);
			component4.anchorMax = new Vector2(1f, 0f);
			component4.pivot = new Vector2(0.5f, 0f);
			component4.sizeDelta = new Vector2(-80f, 168f);
			component4.anchoredPosition = new Vector2(0f, 40f);
			Image image = MkImg(component4, "Border", C_SLOT_BD);
			Stretch(image, -2f);
			image.transform.SetAsFirstSibling();
			RectTransform component5 = MkImg(component4, "NameBox", C_HEADER).GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 1f);
			component5.anchorMax = new Vector2(0f, 1f);
			component5.pivot = new Vector2(0f, 0f);
			component5.anchoredPosition = new Vector2(20f, -4f);
			component5.sizeDelta = new Vector2(170f, 38f);
			Image image2 = MkImg(component5, "NameBorder", C_SLOT_BD);
			Stretch(image2, -2f);
			image2.transform.SetAsFirstSibling();
			_dialogueNameText = MkTMP(component5, "NameText", npcName, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f), 17f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Center);
			_dialogueBodyText = MkTMP(component4, "BodyText", "", Vector2.zero, Vector2.one, new Vector2(30f, 22f), new Vector2(-30f, -34f), 21f, FontStyles.Normal, new Color(0.95f, 0.93f, 0.86f), TextAlignmentOptions.TopLeft);
			_dialogueIndicator = MkTMP(component4, "Indicator", "F", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-78f, 10f), new Vector2(-16f, 38f), 15f, FontStyles.Bold, C_PRICE, TextAlignmentOptions.MidlineRight);
			StartCoroutine(BlinkIndicator());
		}
	}

	private IEnumerator BlinkIndicator()
	{
		while (this != null && _dialogueIndicator != null)
		{
			if (_dialogueIndicator.gameObject.activeSelf)
			{
				float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
				Color color = _dialogueIndicator.color;
				color.a = a;
				_dialogueIndicator.color = color;
			}
			yield return null;
		}
	}

	private void BuildShopPanelMS()
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (canvas == null)
		{
			return;
		}
		Image image = MkImg(canvas.GetComponent<RectTransform>(), "ShopPanel", C_BG);
		RectTransform component = image.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = new Vector2(-160f, 0f);
		component.sizeDelta = new Vector2(420f, 520f);
		shopPanel = image.gameObject;
		Image image2 = MkImg(component, "Border", C_SLOT_BD);
		Stretch(image2, -1f);
		image2.transform.SetAsFirstSibling();
		Image image3 = MkImg(component, "Header", C_HEADER);
		TopStrip(image3, 48f);
		MkTMP(image3.GetComponent<RectTransform>(), "Title", "꾸미기 상점", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-52f, 0f), 18f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);
		MkButton(image3.GetComponent<RectTransform>(), "X", new Color(0.55f, 0.12f, 0.1f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(36f, 36f)).onClick.AddListener(delegate
		{
			shopPanel.SetActive(value: false);
		});
		Image image4 = MkImg(component, "GoldBar", C_GOLD_BAR);
		RectTransform component2 = image4.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.anchoredPosition = new Vector2(0f, -48f);
		component2.sizeDelta = new Vector2(0f, 34f);
		shopInfoText = MkTMP(image4.GetComponent<RectTransform>(), "ShopInfoText", "보유 골드:  0 G", Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f), 14f, FontStyles.Bold, C_PRICE, TextAlignmentOptions.Left);
		MkDivider(component, -82f);
		GameObject gameObject = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
		gameObject.transform.SetParent(component, worldPositionStays: false);
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.offsetMin = new Vector2(0f, 52f);
		component3.offsetMax = new Vector2(0f, -84f);
		ScrollRect component4 = gameObject.GetComponent<ScrollRect>();
		component4.horizontal = false;
		component4.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
		GameObject gameObject2 = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component5 = gameObject2.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		anchorMin = (component5.offsetMax = Vector2.zero);
		component5.offsetMin = anchorMin;
		gameObject2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
		gameObject2.GetComponent<Mask>().showMaskGraphic = false;
		component4.viewport = component5;
		GameObject obj = new GameObject("Content", typeof(RectTransform));
		obj.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component6 = obj.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0f, 1f);
		component6.anchorMax = new Vector2(1f, 1f);
		component6.pivot = new Vector2(0.5f, 1f);
		component6.anchoredPosition = Vector2.zero;
		component6.sizeDelta = Vector2.zero;
		component4.content = component6;
		VerticalLayoutGroup verticalLayoutGroup = obj.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = 5f;
		verticalLayoutGroup.padding = new RectOffset(8, 8, 6, 6);
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = false;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		obj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		string text = null;
		foreach (DecoCatalog.Entry item in DecoCatalog.All)
		{
			if (item.set != text)
			{
				text = item.set;
				BuildSetHeader(component6, text);
			}
			string key = item.key;
			BuildItemCard(component6, new ItemDef
			{
				name = item.key,
				price = item.price,
				iconColor = IconColorForType(item.typeKr),
				iconChar = IconCharForType(item.typeKr),
				onClick = delegate
				{
					BuyDeco(key);
				}
			});
		}
		MkDivider(component, -468f);
		MkButton(component, "쓰레기 전부 판매", C_BTN_SELL, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(220f, 36f)).onClick.AddListener(OnSellButtonClicked);
		image.gameObject.SetActive(value: false);
	}

	private void BuildSetHeader(RectTransform parent, string setName)
	{
		GameObject obj = new GameObject("Header_" + setName, typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(parent, worldPositionStays: false);
		obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
		TextMeshProUGUI component = obj.GetComponent<TextMeshProUGUI>();
		component.text = "— " + setName + " —";
		component.fontSize = 14f;
		component.fontStyle = FontStyles.Bold;
		component.color = C_PRICE;
		component.alignment = TextAlignmentOptions.Center;
		component.raycastTarget = false;
	}

	private static string IconCharForType(string t)
	{
		return t switch
		{
			"나무" => "나", 
			"상자" => "상", 
			"의자" => "의", 
			"울타리" => "울", 
			"꽃병" => "꽃", 
			"탁자" => "탁", 
			"꽃밭" => "밭", 
			_ => "?", 
		};
	}

	private static Color IconColorForType(string t)
	{
		return t switch
		{
			"나무" => new Color(0.15f, 0.6f, 0.25f), 
			"상자" => new Color(0.58f, 0.38f, 0.15f), 
			"의자" => new Color(0.18f, 0.46f, 0.72f), 
			"울타리" => new Color(0.4f, 0.43f, 0.47f), 
			"꽃병" => new Color(0.55f, 0.17f, 0.7f), 
			"탁자" => new Color(0.37f, 0.21f, 0.09f), 
			"꽃밭" => new Color(0.88f, 0.33f, 0.56f), 
			_ => new Color(0.4f, 0.4f, 0.4f), 
		};
	}

	private void BuildItemCard(RectTransform parent, ItemDef item)
	{
		RectTransform component = MkImg(parent, "Card_" + item.name, C_SLOT).GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(0f, 72f);
		RectTransform component2 = MkImg(component, "Sep", new Color(0.18f, 0.22f, 0.36f, 0.6f)).GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(1f, 0f);
		component2.pivot = new Vector2(0.5f, 0f);
		component2.anchoredPosition = Vector2.zero;
		component2.sizeDelta = new Vector2(-8f, 1f);
		Image image = MkImg(component, "Icon", item.iconColor);
		RectTransform component3 = image.GetComponent<RectTransform>();
		Vector2 anchorMin = (component3.anchorMax = new Vector2(0f, 0.5f));
		component3.anchorMin = anchorMin;
		component3.pivot = new Vector2(0f, 0.5f);
		component3.anchoredPosition = new Vector2(10f, 0f);
		component3.sizeDelta = new Vector2(52f, 52f);
		MkTMP(image.GetComponent<RectTransform>(), "T", item.iconChar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center);
		float x = 72f;
		MkTMP(component, "Name", item.name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(x, -6f), new Vector2(-80f, -26f), 15f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);
		MkTMP(component, "Price", $"{item.price:N0} G", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(x, -30f), new Vector2(-80f, -50f), 13f, FontStyles.Normal, C_PRICE, TextAlignmentOptions.Left);
		Button button = MkButton(component, "구매", C_BTN_BUY, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(62f, 32f));
		Action cb = item.onClick;
		button.onClick.AddListener(delegate
		{
			cb?.Invoke();
		});
	}

	private static Image MkImg(RectTransform parent, string name, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		Image component = obj.GetComponent<Image>();
		component.color = color;
		component.raycastTarget = false;
		return component;
	}

	private static void Stretch(Image img, float offset)
	{
		RectTransform component = img.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = new Vector2(offset, offset);
		component.offsetMax = new Vector2(0f - offset, 0f - offset);
	}

	private static void TopStrip(Image img, float h)
	{
		RectTransform component = img.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.anchoredPosition = Vector2.zero;
		component.sizeDelta = new Vector2(0f, h);
	}

	private static void MkDivider(RectTransform parent, float y)
	{
		RectTransform component = MkImg(parent, "Div", C_DIVIDER).GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.anchoredPosition = new Vector2(0f, y);
		component.sizeDelta = new Vector2(-16f, 1f);
	}

	private static TextMeshProUGUI MkTMP(RectTransform parent, string name, string text, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, float size, FontStyles style, Color color, TextAlignmentOptions align)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = aMin;
		component.anchorMax = aMax;
		component.offsetMin = offMin;
		component.offsetMax = offMax;
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = text;
		component2.fontSize = size;
		component2.fontStyle = style;
		component2.color = color;
		component2.alignment = align;
		component2.raycastTarget = false;
		return component2;
	}

	private static Button MkButton(RectTransform parent, string label, Color bg, Vector2 anchor, Vector2 pos, Vector2 size)
	{
		GameObject gameObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = anchor);
		component.anchorMin = anchorMin;
		component.pivot = anchor;
		component.anchoredPosition = pos;
		component.sizeDelta = size;
		gameObject.GetComponent<Image>().color = bg;
		GameObject obj = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = obj.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		anchorMin = (component2.offsetMax = Vector2.zero);
		component2.offsetMin = anchorMin;
		TextMeshProUGUI component3 = obj.GetComponent<TextMeshProUGUI>();
		component3.text = label;
		component3.fontSize = 13f;
		component3.fontStyle = FontStyles.Bold;
		component3.alignment = TextAlignmentOptions.Center;
		component3.color = new Color(0.95f, 0.92f, 0.85f);
		return gameObject.GetComponent<Button>();
	}

}
