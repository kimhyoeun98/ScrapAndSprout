using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DecorationPlacer : MonoBehaviour
{
	[Header("── 배치 설정 ──")]
	[Tooltip("배치 UI 패널 (비워두면 자동 생성)")]
	public GameObject placementPanel;

	[Tooltip("배치 가능 범위 반경")]
	public float interactionRadius = 5f;

	[Tooltip("배치 가능 위치들 (선택)")]
	public Transform[] decoPlacePoints;

	private static List<string> _decoItems = new List<string>();

	private bool _isPlayerNearby;

	private TrashCollector _playerCollector;

	private readonly Dictionary<string, Button> _itemButtons = new Dictionary<string, Button>();

	private readonly Dictionary<string, TextMeshProUGUI> _countTexts = new Dictionary<string, TextMeshProUGUI>();

	private readonly Dictionary<string, GameObject> _itemRows = new Dictionary<string, GameObject>();

	private GameObject _guideUI;

	private GameObject _emptyLabel;

	private TextMeshProUGUI _zoneNameText;

	private const float GRID = 0.5f;

	private Canvas _decoCanvas;

	private void Start()
	{
		Debug.Log("[배치] DecorationPlacer.Start() 호출됨!");
		LoadAllDecoItems();
		if (PlayerZoneManager.Instance == null)
		{
			new GameObject("PlayerZoneManager").AddComponent<PlayerZoneManager>();
		}
		if (placementPanel == null)
		{
			BuildPlacementPanel();
			Debug.Log("[배치] ✓ 배치 패널 자동 생성됨");
		}
		else
		{
			Debug.Log("[배치] ✓ 배치 패널이 이미 있음");
		}
		if (placementPanel != null)
		{
			placementPanel.SetActive(value: false);
			Debug.Log("[배치] ✓ 배치 패널 준비 완료");
		}
		else
		{
			Debug.LogError("[배치] ✗ placementPanel이 null입니다!");
		}
	}

	private void LoadAllDecoItems()
	{
		_decoItems.Clear();
		GameObject[] array = Resources.LoadAll<GameObject>("deco");
		Debug.Log($"[배치] 로드된 프리팹: {array.Length}개");
		GameObject[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].name.Replace("_0", "");
			string text2 = DecoCatalog.KeyForPrefabRaw(text);
			if (text2 == null)
			{
				Debug.Log("[배치] 매핑 없는 프리팹 건너뜀: " + text);
			}
			else if (!_decoItems.Contains(text2))
			{
				_decoItems.Add(text2);
				Debug.Log("[배치] ✓ 추가됨: " + text2);
			}
		}
		Debug.Log($"[배치] 총 {_decoItems.Count}개의 아이템 로드됨");
	}

	private Canvas GetOrCreateOverlayCanvas()
	{
		if (_decoCanvas != null)
		{
			return _decoCanvas;
		}
		GameObject obj = new GameObject("DecoPlacementCanvas");
		Canvas canvas = obj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 500;
		CanvasScaler canvasScaler = obj.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		obj.AddComponent<GraphicRaycaster>();
		_decoCanvas = canvas;
		return _decoCanvas;
	}

	private void BuildPlacementPanel()
	{
		Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
		if (orCreateOverlayCanvas == null)
		{
			return;
		}
		GameObject gameObject = new GameObject("DecoPlacementPanel", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(orCreateOverlayCanvas.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(300f, 400f);
		component.anchoredPosition = new Vector2(200f, 0f);
		gameObject.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.04f, 0.95f);
		GameObject obj = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = obj.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.anchoredPosition = Vector2.zero;
		component2.sizeDelta = new Vector2(0f, 4f);
		obj.GetComponent<Image>().color = new Color(0.3f, 0.58f, 0.22f, 1f);
		AddLabel(gameObject.transform, "Title", "꾸미기 배치", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(0f, -6f), 17f, FontStyles.Bold, new Color(0.85f, 0.74f, 0.28f));
		GameObject gameObject2 = new GameObject("ZoneNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 1f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.offsetMin = new Vector2(0f, -64f);
		component3.offsetMax = new Vector2(0f, -44f);
		_zoneNameText = gameObject2.GetComponent<TextMeshProUGUI>();
		_zoneNameText.fontSize = 11f;
		_zoneNameText.fontStyle = FontStyles.Normal;
		_zoneNameText.alignment = TextAlignmentOptions.Center;
		_zoneNameText.color = new Color(0.65f, 0.85f, 0.55f);
		_zoneNameText.raycastTarget = false;
		UpdateZoneNameText();
		BuildCloseButton(gameObject.transform, gameObject);
		AddLabel(gameObject.transform, "Guide", "[F] 키로 열기/닫기", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -82f), new Vector2(0f, -64f), 10f, FontStyles.Normal, new Color(0.6f, 0.7f, 0.6f, 0.7f));
		Transform parent = BuildScrollArea(gameObject.transform);
		foreach (string decoItem in _decoItems)
		{
			BuildItemButton(parent, decoItem);
		}
		BuildEmptyLabel(parent);
		gameObject.SetActive(value: false);
		placementPanel = gameObject;
		BuildGuideUI(orCreateOverlayCanvas.transform);
	}

	private void BuildGuideUI(Transform canvasRoot)
	{
		_guideUI = new GameObject("DecoGuideUI", typeof(RectTransform), typeof(Image));
		_guideUI.transform.SetParent(canvasRoot, worldPositionStays: false);
		RectTransform component = _guideUI.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0f);
		component.anchorMax = new Vector2(0.5f, 0f);
		component.anchoredPosition = new Vector2(0f, 90f);
		component.sizeDelta = new Vector2(220f, 36f);
		_guideUI.GetComponent<Image>().color = new Color(0.1f, 0.4f, 0.1f, 0.85f);
		AddLabel(_guideUI.transform, "Text", "[F] 꾸미기 배치", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 14f, FontStyles.Bold, Color.white);
		_guideUI.SetActive(value: false);
	}

	private Transform BuildScrollArea(Transform parent)
	{
		GameObject gameObject = new GameObject("Scroll", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(1f, 1f);
		component.offsetMin = new Vector2(8f, 8f);
		component.offsetMax = new Vector2(-8f, -92f);
		ScrollRect scrollRect = gameObject.AddComponent<ScrollRect>();
		scrollRect.horizontal = false;
		GameObject gameObject2 = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		Vector2 offsetMin = (component2.offsetMax = Vector2.zero);
		component2.offsetMin = offsetMin;
		gameObject2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
		gameObject2.GetComponent<Mask>().showMaskGraphic = false;
		scrollRect.viewport = component2;
		GameObject obj = new GameObject("Content", typeof(RectTransform));
		obj.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component3 = obj.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 1f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.pivot = new Vector2(0.5f, 1f);
		component3.sizeDelta = Vector2.zero;
		scrollRect.content = component3;
		VerticalLayoutGroup verticalLayoutGroup = obj.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = 6f;
		verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		verticalLayoutGroup.childControlHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		obj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return component3;
	}

	private void BuildItemButton(Transform parent, string itemName)
	{
		int num = DecoCatalog.Price(itemName);
		GameObject gameObject = new GameObject("Row_" + itemName, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 46f);
		gameObject.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.12f, 0.85f);
		AddLabel(gameObject.transform, "Info", $"{itemName}  (+{num}pt)", new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(8f, 0f), Vector2.zero, 13f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
		GameObject obj = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.65f, 0f);
		component.anchorMax = new Vector2(0.8f, 1f);
		Vector2 offsetMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = offsetMin;
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = "x0";
		component2.fontSize = 13f;
		component2.alignment = TextAlignmentOptions.Center;
		component2.color = new Color(0.5f, 1f, 0.5f);
		_countTexts[itemName] = component2;
		GameObject obj2 = new GameObject("PlaceBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		obj2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component3 = obj2.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.8f, 0.1f);
		component3.anchorMax = new Vector2(1f, 0.9f);
		component3.offsetMin = new Vector2(0f, 0f);
		component3.offsetMax = new Vector2(-6f, 0f);
		obj2.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 0.9f);
		AddLabel(obj2.transform, "Label", "배치", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13f, FontStyles.Bold, Color.white);
		Button component4 = obj2.GetComponent<Button>();
		string captured = itemName;
		component4.onClick.AddListener(delegate
		{
			StartGhostPlacement(captured);
		});
		_itemButtons[itemName] = component4;
		_itemRows[itemName] = gameObject;
	}

	private void BuildEmptyLabel(Transform parent)
	{
		GameObject gameObject = new GameObject("EmptyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
		TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
		component.text = "보유한 꾸미기 아이템이 없습니다";
		component.fontSize = 12f;
		component.alignment = TextAlignmentOptions.Center;
		component.color = new Color(0.6f, 0.6f, 0.6f);
		gameObject.SetActive(value: false);
		_emptyLabel = gameObject;
	}

	private void StartGhostPlacement(string itemName)
	{
		Debug.Log("[배치] 고스트 배치 시작 요청: " + itemName);
		TrashCollector trashCollector = ResolveCollector();
		if (trashCollector != null && (!trashCollector.inventory.ContainsKey(itemName) || trashCollector.inventory[itemName] <= 0))
		{
			UIManager.Instance?.ShowStatusMessage(itemName + "이(가) 없습니다!");
			return;
		}
		DecoPlacementController decoPlacementController = DecoPlacementController.Instance;
		if (decoPlacementController == null)
		{
			decoPlacementController = new GameObject("DecoPlacementController").AddComponent<DecoPlacementController>();
		}
		ClosePlacementUI();
		decoPlacementController.StartPlacement(itemName, trashCollector);
	}

	private void BuildCloseButton(Transform parent, GameObject panel)
	{
		GameObject obj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(1f, 1f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(1f, 1f);
		component.anchoredPosition = new Vector2(-6f, -6f);
		component.sizeDelta = new Vector2(32f, 32f);
		obj.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
		AddLabel(obj.transform, "X", "✕", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16f, FontStyles.Bold, Color.white);
		obj.GetComponent<Button>().onClick.AddListener(delegate
		{
			panel.SetActive(value: false);
		});
	}

	private void Update()
	{
		DetectNearbyPlayer();
		if (Input.GetKeyDown(KeyCode.Q))
		{
			Debug.Log("[배치] Q키 입력 감지됨!");
			if (placementPanel != null)
			{
				TogglePlacementUI();
				Debug.Log("[배치] ✓ 배치 UI 토글됨 - Panel active: " + placementPanel.activeSelf);
			}
			else
			{
				Debug.LogError("[배치] ✗ placementPanel이 null입니다!");
			}
		}
	}

	private void DetectNearbyPlayer()
	{
		Collider2D[] array = Physics2D.OverlapCircleAll(base.transform.position, interactionRadius);
		bool flag = false;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			if (!collider2D.CompareTag("Player"))
			{
				continue;
			}
			PlayerMovement component = collider2D.GetComponent<PlayerMovement>();
			if (!(component == null) && component.HasInputAuthority)
			{
				if (!_isPlayerNearby && _guideUI != null)
				{
					_guideUI.SetActive(value: true);
				}
				_isPlayerNearby = true;
				_playerCollector = collider2D.GetComponent<TrashCollector>();
				flag = true;
				break;
			}
		}
		if (!flag && _isPlayerNearby)
		{
			_isPlayerNearby = false;
			_playerCollector = null;
			ClosePlacementUI();
			if (_guideUI != null)
			{
				_guideUI.SetActive(value: false);
			}
		}
	}

	private void TogglePlacementUI()
	{
		if (placementPanel == null)
		{
			return;
		}
		bool flag = !placementPanel.activeSelf;
		placementPanel.SetActive(flag);
		if (flag)
		{
			UpdateZoneNameText();
			RefreshItemButtons();
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			if (_guideUI != null)
			{
				_guideUI.SetActive(value: false);
			}
		}
	}

	private Vector3 CalcPlacePosition()
	{
		Vector3 vector = ((_playerCollector != null) ? _playerCollector.transform.position : ((!(Camera.main != null)) ? Vector3.zero : Camera.main.transform.position));
		float num = Mathf.Round(vector.x / 0.5f) * 0.5f;
		if (PlayerZoneManager.Instance != null)
		{
			int localSlot = PlayerZoneManager.Instance.GetLocalSlot();
			(float min, float max) zoneBounds = PlayerZoneManager.Instance.GetZoneBounds(localSlot);
			float item = zoneBounds.min;
			float item2 = zoneBounds.max;
			num = Mathf.Clamp(num, item + 0.5f, item2 - 0.5f);
		}
		float y = Mathf.Round(vector.y / 0.5f) * 0.5f;
		return new Vector3(num, y, -1f);
	}

	private void UpdateZoneNameText()
	{
		if (!(_zoneNameText == null))
		{
			if (PlayerZoneManager.Instance == null)
			{
				_zoneNameText.text = "";
				return;
			}
			int localSlot = PlayerZoneManager.Instance.GetLocalSlot();
			_zoneNameText.text = "▶ " + PlayerZoneManager.Instance.GetZoneName(localSlot);
		}
	}

	private void ClosePlacementUI()
	{
		if (placementPanel != null)
		{
			placementPanel.SetActive(value: false);
		}
	}

	private TrashCollector ResolveCollector()
	{
		if (_playerCollector != null)
		{
			return _playerCollector;
		}
		TrashCollector[] array = Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
		foreach (TrashCollector trashCollector in array)
		{
			if (trashCollector.HasInputAuthority)
			{
				return trashCollector;
			}
		}
		return null;
	}

	private void RefreshItemButtons()
	{
		TrashCollector trashCollector = ResolveCollector();
		int num = 0;
		foreach (string decoItem in _decoItems)
		{
			int value;
			int num2 = ((trashCollector != null && trashCollector.inventory.TryGetValue(decoItem, out value)) ? value : 0);
			bool flag = trashCollector == null || num2 > 0;
			if (flag)
			{
				num++;
			}
			if (_itemRows.TryGetValue(decoItem, out var value2) && value2 != null)
			{
				value2.SetActive(flag);
			}
			if (_countTexts.TryGetValue(decoItem, out var value3))
			{
				value3.text = ((trashCollector == null) ? "" : $"x{num2}");
			}
			if (_itemButtons.TryGetValue(decoItem, out var value4))
			{
				value4.interactable = flag;
			}
		}
		if (_emptyLabel != null)
		{
			_emptyLabel.SetActive(trashCollector != null && num == 0);
		}
	}

	public void PlaceItem(string itemName)
	{
		Debug.Log("[배치] PlaceItem 호출: " + itemName);
		if (_playerCollector != null && (!_playerCollector.inventory.ContainsKey(itemName) || _playerCollector.inventory[itemName] <= 0))
		{
			Debug.Log("[배치] 아이템 부족: " + itemName);
			UIManager.Instance?.ShowStatusMessage(itemName + "이(가) 없습니다!");
			return;
		}
		int score = ((_playerCollector != null) ? _playerCollector.GetDecorScore(itemName) : 10);
		Debug.Log($"[배치] {itemName} 배치 진행: {score}pt");
		if (_playerCollector != null)
		{
			_playerCollector.inventory[itemName]--;
			if (_playerCollector.inventory[itemName] <= 0)
			{
				_playerCollector.inventory.Remove(itemName);
			}
			_playerCollector.RefreshUI();
		}
		NetworkRunner networkRunner = Object.FindFirstObjectByType<NetworkRunner>();
		int slotIndex = ((networkRunner != null) ? (networkRunner.LocalPlayer.PlayerId - 1) : 0);
		if (itemName == "나무풍 나무")
		{
			WeatherManager.Instance?.AddTree();
			AudioManager.Instance?.PlaySeedPlant();
		}
		if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
		{
			ApiManager.Instance.PlaceDecoration(new DecoPlaceRequest
			{
				playerId = ApiManager.Instance.playerId,
				itemType = itemName,
				score = score
			}, delegate
			{
				Debug.Log($"[DecorationPlacer] 서버 기록 완료 +{score}pt");
			}, delegate(string err)
			{
				Debug.LogWarning("[DecorationPlacer] 서버 기록 실패 (로컬 반영은 완료): " + err);
			});
		}
		Vector3 position = CalcPlacePosition();
		if (GameManager.Instance != null && GameManager.Instance.Object != null && GameManager.Instance.Object.IsValid)
		{
			GameManager.Instance.RPC_AddDecorScore(score);
			GameManager.Instance.RPC_AddPlayerDecorScore(slotIndex, score);
			GameManager.Instance.RPC_PlaceDecoItem(itemName, position);
			GameManager.Instance.RPC_TrackPlacedItem(itemName);
		}
		else
		{
			DecoItemSpawner.CreateStatic(itemName, position);
			Debug.LogWarning("[배치] GameManager 무효 — 로컬 스폰으로 폴백");
		}
		UIManager.Instance?.ShowStatusMessage($"{itemName} 배치! +{score}pt");
		TutorialManager.Instance?.OnDecoItemPlaced();
		Debug.Log($"[DecorationPlacer] {itemName} 배치 완료, 점수: {score}pt");
		ClosePlacementUI();
	}

	private static void AddLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.offsetMin = offsetMin;
		component.offsetMax = offsetMax;
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = text;
		component2.fontSize = fontSize;
		component2.fontStyle = style;
		component2.alignment = align;
		component2.color = color;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(base.transform.position, interactionRadius);
		if (decoPlacePoints == null)
		{
			return;
		}
		Gizmos.color = Color.cyan;
		Transform[] array = decoPlacePoints;
		foreach (Transform transform in array)
		{
			if (transform != null)
			{
				Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
			}
		}
	}
}
