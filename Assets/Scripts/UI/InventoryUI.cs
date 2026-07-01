using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
	[Header("핫바")]
	[Tooltip("한 번에 핫바에 표시할 최대 아이템 종류 수")]
	public int hotbarMaxSlots = 6;

	private TrashCollector _collector;

	private GameObject _hotbarRoot;

	private GameObject _inventoryPanelRoot;

	private Transform _inventoryListParent;

	private readonly List<GameObject> _hotbarSlots = new List<GameObject>();

	private readonly List<GameObject> _inventoryRows = new List<GameObject>();

	private Dictionary<string, int> _lastInventory = new Dictionary<string, int>();

	private bool _inventoryOpen;

	private static readonly Color ColorHotbarBg = new Color(0.1f, 0.1f, 0.1f, 0.75f);

	private static readonly Color ColorSlotBg = new Color(0.2f, 0.2f, 0.2f, 0.9f);

	private static readonly Color ColorDecoSlotBg = new Color(0.1f, 0.3f, 0.1f, 0.9f);

	private static readonly Color ColorPanelBg = new Color(0.05f, 0.05f, 0.05f, 0.92f);

	private static readonly Color ColorSectionTitle = new Color(1f, 0.85f, 0.3f, 1f);

	private void Start()
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null)
		{
			string text = SceneManager.GetActiveScene().name;
			Canvas[] array = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			foreach (Canvas canvas2 in array)
			{
				if (canvas2.gameObject.scene.name == text)
				{
					canvas = canvas2;
					break;
				}
			}
		}
		if (canvas == null)
		{
			Debug.LogError("[InventoryUI] Canvas를 찾을 수 없습니다!");
			return;
		}
		BuildHotbar(canvas.transform);
		BuildInventoryPanel(canvas.transform);
	}

	private void Update()
	{
		if (_collector == null)
		{
			TrashCollector[] array = Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
			foreach (TrashCollector trashCollector in array)
			{
				if (trashCollector.HasInputAuthority)
				{
					_collector = trashCollector;
					break;
				}
			}
			return;
		}
		if (Input.GetKeyDown(KeyCode.I) && !TrashZoneChat.IsTyping)
		{
			ToggleInventory();
		}
		if (HasInventoryChanged())
		{
			_lastInventory = new Dictionary<string, int>(_collector.inventory);
			RefreshHotbar();
			if (_inventoryOpen)
			{
				RefreshInventoryPanel();
			}
		}
	}

	private void BuildHotbar(Transform canvasRoot)
	{
		_hotbarRoot = CreatePanel(canvasRoot, "Hotbar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(544f, 88f));
		SetColor(_hotbarRoot, ColorHotbarBg);
		HorizontalLayoutGroup horizontalLayoutGroup = _hotbarRoot.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.spacing = 6f;
		horizontalLayoutGroup.padding = new RectOffset(8, 8, 6, 6);
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		horizontalLayoutGroup.childControlWidth = false;
		horizontalLayoutGroup.childControlHeight = false;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
	}

	private void BuildInventoryPanel(Transform canvasRoot)
	{
		_inventoryPanelRoot = CreatePanel(canvasRoot, "InventoryPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 440f));
		SetColor(_inventoryPanelRoot, ColorPanelBg);
		_inventoryPanelRoot.SetActive(value: false);
		_inventoryPanelRoot.GetComponent<RectTransform>();
		GameObject obj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(_inventoryPanelRoot.transform, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.offsetMin = new Vector2(10f, -50f);
		component.offsetMax = new Vector2(-10f, -10f);
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = "인벤토리";
		component2.fontSize = 20f;
		component2.fontStyle = FontStyles.Bold;
		component2.alignment = TextAlignmentOptions.Center;
		component2.color = Color.white;
		BuildCloseButton(_inventoryPanelRoot.transform);
		GameObject gameObject = new GameObject("Scroll", typeof(RectTransform));
		gameObject.transform.SetParent(_inventoryPanelRoot.transform, worldPositionStays: false);
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.offsetMin = new Vector2(10f, 10f);
		component3.offsetMax = new Vector2(-10f, -55f);
		ScrollRect scrollRect = gameObject.AddComponent<ScrollRect>();
		scrollRect.horizontal = false;
		GameObject gameObject2 = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		Vector2 offsetMin = (component4.offsetMax = Vector2.zero);
		component4.offsetMin = offsetMin;
		gameObject2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
		gameObject2.GetComponent<Mask>().showMaskGraphic = false;
		scrollRect.viewport = component4;
		GameObject obj2 = new GameObject("Content", typeof(RectTransform));
		obj2.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component5 = obj2.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0f, 1f);
		component5.anchorMax = new Vector2(1f, 1f);
		component5.pivot = new Vector2(0.5f, 1f);
		component5.offsetMin = Vector2.zero;
		component5.offsetMax = Vector2.zero;
		component5.sizeDelta = new Vector2(0f, 0f);
		scrollRect.content = component5;
		_inventoryListParent = component5;
		VerticalLayoutGroup verticalLayoutGroup = obj2.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.padding = new RectOffset(6, 6, 4, 4);
		verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
		verticalLayoutGroup.childControlHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		obj2.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
	}

	private void BuildCloseButton(Transform parent)
	{
		GameObject gameObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 1f);
		component.anchoredPosition = new Vector2(-8f, -8f);
		component.sizeDelta = new Vector2(36f, 36f);
		gameObject.GetComponent<Image>().color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
		GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = obj.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		Vector2 offsetMin = (component2.offsetMax = Vector2.zero);
		component2.offsetMin = offsetMin;
		TextMeshProUGUI component3 = obj.GetComponent<TextMeshProUGUI>();
		component3.text = "✕";
		component3.fontSize = 18f;
		component3.alignment = TextAlignmentOptions.Center;
		component3.color = Color.white;
		gameObject.GetComponent<Button>().onClick.AddListener(delegate
		{
			_inventoryOpen = false;
			_inventoryPanelRoot.SetActive(value: false);
		});
	}

	private void RefreshHotbar()
	{
		foreach (GameObject hotbarSlot in _hotbarSlots)
		{
			Object.Destroy(hotbarSlot);
		}
		_hotbarSlots.Clear();
		if (_hotbarRoot == null || _collector == null)
		{
			return;
		}
		int num = 0;
		foreach (KeyValuePair<string, int> item2 in _collector.inventory)
		{
			if (item2.Value > 0)
			{
				if (num >= hotbarMaxSlots)
				{
					break;
				}
				bool isDeco = DecoCatalog.IsDeco(item2.Key);
				GameObject item = BuildHotbarSlot(_hotbarRoot.transform, item2.Key, item2.Value, isDeco);
				_hotbarSlots.Add(item);
				num++;
			}
		}
	}

	private GameObject BuildHotbarSlot(Transform parent, string itemName, int count, bool isDeco)
	{
		GameObject gameObject = new GameObject("HSlot_" + itemName, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 74f);
		gameObject.GetComponent<Image>().color = (isDeco ? ColorDecoSlotBg : ColorSlotBg);
		AddTMP(gameObject.transform, "ItemName", itemName, new Vector2(0f, 0.76f), new Vector2(1f, 1f), 10f, Color.white);
		GameObject gameObject2 = new GameObject("Icon", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.14f, 0.28f);
		component.anchorMax = new Vector2(0.86f, 0.76f);
		Vector2 offsetMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = offsetMin;
		Image component2 = gameObject2.GetComponent<Image>();
		component2.preserveAspect = true;
		component2.raycastTarget = false;
		Sprite sprite = ((_collector != null) ? _collector.GetSpriteByName(itemName) : null);
		if (sprite != null)
		{
			component2.sprite = sprite;
		}
		else
		{
			gameObject2.SetActive(value: false);
		}
		AddTMP(gameObject.transform, "Count", $"x{count}", new Vector2(0f, 0f), new Vector2(1f, 0.27f), 13f, isDeco ? new Color(0.5f, 1f, 0.5f) : Color.yellow);
		return gameObject;
	}

	private void ToggleInventory()
	{
		_inventoryOpen = !_inventoryOpen;
		_inventoryPanelRoot.SetActive(_inventoryOpen);
		if (_inventoryOpen)
		{
			RefreshInventoryPanel();
		}
	}

	public void CloseInventory()
	{
		_inventoryOpen = false;
		if (_inventoryPanelRoot != null)
		{
			_inventoryPanelRoot.SetActive(value: false);
		}
	}

	private void RefreshInventoryPanel()
	{
		foreach (GameObject inventoryRow in _inventoryRows)
		{
			Object.Destroy(inventoryRow);
		}
		_inventoryRows.Clear();
		if (_inventoryListParent == null || _collector == null)
		{
			return;
		}
		bool flag = false;
		foreach (KeyValuePair<string, int> item in _collector.inventory)
		{
			if (!DecoCatalog.IsDeco(item.Key) && item.Value > 0)
			{
				if (!flag)
				{
					_inventoryRows.Add(BuildSectionTitle("── 쓰레기 아이템 ──"));
					flag = true;
				}
				_inventoryRows.Add(BuildInventoryRow(item.Key, item.Value, isDeco: false));
			}
		}
		bool flag2 = false;
		foreach (KeyValuePair<string, int> item2 in _collector.inventory)
		{
			if (DecoCatalog.IsDeco(item2.Key) && item2.Value > 0)
			{
				if (!flag2)
				{
					_inventoryRows.Add(BuildSectionTitle("── 꾸미기 아이템 ──"));
					flag2 = true;
				}
				_inventoryRows.Add(BuildInventoryRow(item2.Key, item2.Value, isDeco: true));
			}
		}
		if (!flag && !flag2)
		{
			_inventoryRows.Add(BuildEmptyRow());
		}
	}

	private GameObject BuildSectionTitle(string text)
	{
		GameObject obj = new GameObject("Section", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(_inventoryListParent, worldPositionStays: false);
		obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
		TextMeshProUGUI component = obj.GetComponent<TextMeshProUGUI>();
		component.text = text;
		component.fontSize = 12f;
		component.fontStyle = FontStyles.Bold;
		component.alignment = TextAlignmentOptions.Center;
		component.color = ColorSectionTitle;
		return obj;
	}

	private GameObject BuildInventoryRow(string itemName, int count, bool isDeco)
	{
		GameObject gameObject = new GameObject("Row_" + itemName, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(_inventoryListParent, worldPositionStays: false);
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
		gameObject.GetComponent<Image>().color = (isDeco ? new Color(0.1f, 0.25f, 0.1f, 0.8f) : new Color(0.18f, 0.18f, 0.18f, 0.8f));
		AddTMP(gameObject.transform, "Name", itemName, new Vector2(0f, 0f), new Vector2(0.7f, 1f), 14f, Color.white, TextAlignmentOptions.MidlineLeft, new RectOffset(8, 0, 0, 0));
		AddTMP(gameObject.transform, "Count", $"x{count}", new Vector2(0.7f, 0f), new Vector2(1f, 1f), 14f, isDeco ? new Color(0.4f, 1f, 0.4f) : Color.yellow, TextAlignmentOptions.MidlineRight, new RectOffset(0, 8, 0, 0));
		if (isDeco)
		{
			Button button = gameObject.AddComponent<Button>();
			ColorBlock colors = button.colors;
			colors.highlightedColor = new Color(0.2f, 0.5f, 0.2f, 1f);
			colors.pressedColor = new Color(0.3f, 0.7f, 0.3f, 1f);
			button.colors = colors;
			string captured = itemName;
			button.onClick.AddListener(delegate
			{
				_inventoryOpen = false;
				_inventoryPanelRoot.SetActive(value: false);
				UIManager.Instance?.ForceCloseInventory();
				DecoPlacementController.Instance?.StartPlacement(captured, _collector);
			});
		}
		return gameObject;
	}

	private GameObject BuildEmptyRow()
	{
		GameObject obj = new GameObject("Empty", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(_inventoryListParent, worldPositionStays: false);
		obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
		TextMeshProUGUI component = obj.GetComponent<TextMeshProUGUI>();
		component.text = "아이템 없음";
		component.fontSize = 14f;
		component.alignment = TextAlignmentOptions.Center;
		component.color = new Color(0.6f, 0.6f, 0.6f);
		return obj;
	}

	private bool HasInventoryChanged()
	{
		if (_collector == null)
		{
			return false;
		}
		if (_collector.inventory.Count != _lastInventory.Count)
		{
			return true;
		}
		foreach (KeyValuePair<string, int> item in _collector.inventory)
		{
			if (!_lastInventory.TryGetValue(item.Key, out var value) || value != item.Value)
			{
				return true;
			}
		}
		return false;
	}

	private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.pivot = new Vector2(0.5f, 0f);
		component.anchoredPosition = anchoredPos;
		component.sizeDelta = size;
		return obj;
	}

	private static void SetColor(GameObject obj, Color color)
	{
		obj.GetComponent<Image>().color = color;
	}

	private static void AddTMP(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center, RectOffset padding = null)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.offsetMin = ((padding != null) ? new Vector2(padding.left, padding.bottom) : Vector2.zero);
		component.offsetMax = ((padding != null) ? new Vector2(-padding.right, -padding.top) : Vector2.zero);
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = text;
		component2.fontSize = fontSize;
		component2.alignment = alignment;
		component2.color = color;
	}
}
