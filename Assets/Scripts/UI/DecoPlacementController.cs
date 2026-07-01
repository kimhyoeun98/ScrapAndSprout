using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class DecoPlacementController : MonoBehaviour
{
	private const float GRID = 0.5f;

	private string _selectedItem;

	private GameObject _ghost;

	private bool _isPlacing;

	private bool _isFlipped;

	private bool _skipNextClick;

	private TrashCollector _collector;

	private int _localScore;

	private Grid _grid;

	private Tilemap _safeTilemap;

	private bool _safeTilemapSearched;

	private bool _placementValid = true;

	public static DecoPlacementController Instance { get; private set; }

	public bool IsPlacing => _isPlacing;

	private static int PlaceScore(string key)
	{
		if (!DecoCatalog.IsDeco(key))
		{
			return 10;
		}
		return Mathf.RoundToInt((float)DecoCatalog.Score(key) / 2f);
	}

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		if (SceneManager.GetActiveScene().name == "DecoScene")
		{
			BuildDecoSceneUI();
			RestoreSavedDecorations();
		}
	}

	public void StartPlacement(string itemName, TrashCollector collector)
	{
		if (_isPlacing)
		{
			CancelPlacement();
		}
		string text = DecoCatalog.PrefabPath(itemName);
		if (text == null)
		{
			Debug.LogWarning("[DecoPlacement] 프리팹 경로 없음: " + itemName);
			return;
		}
		GameObject gameObject = Resources.Load<GameObject>(text);
		if (gameObject == null)
		{
			Debug.LogWarning("[DecoPlacement] 프리팹 로드 실패: " + text);
			return;
		}
		_selectedItem = itemName;
		_collector = collector;
		_isPlacing = true;
		_isFlipped = false;
		_skipNextClick = true;
		_placementValid = true;
		_safeTilemapSearched = false;
		InventoryUI[] array = Object.FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].CloseInventory();
		}
		UIManager.Instance?.ForceCloseInventory();
		_ghost = Object.Instantiate(gameObject);
		_ghost.name = "DecoGhost";
		Collider2D[] componentsInChildren = _ghost.GetComponentsInChildren<Collider2D>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		SpriteRenderer[] componentsInChildren2 = _ghost.GetComponentsInChildren<SpriteRenderer>();
		foreach (SpriteRenderer spriteRenderer in componentsInChildren2)
		{
			spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0.5f);
		}
		UIManager.Instance?.ShowStatusMessage(itemName + " 선택됨 — 클릭으로 배치 / R: 반전 / ESC: 취소", 3f);
	}

	private void Update()
	{
		if (_isPlacing && !(_ghost == null))
		{
			UpdateGhostPosition();
			if (Input.GetKeyDown(KeyCode.R))
			{
				ToggleFlip();
			}
			if (_skipNextClick)
			{
				_skipNextClick = false;
			}
			else if ((!(EventSystem.current != null) || !EventSystem.current.IsPointerOverGameObject()) && Input.GetMouseButtonDown(0))
			{
				PlaceItem();
			}
			else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
			{
				CancelPlacement();
			}
		}
	}

	private void UpdateGhostPosition()
	{
		if (!(Camera.main == null))
		{
			Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			world.z = 0f;
			Vector3 vector = SnapToGrid(world);
			_ghost.transform.position = vector;
			_placementValid = IsInsideSafeZone(vector);
			SpriteRenderer[] componentsInChildren = _ghost.GetComponentsInChildren<SpriteRenderer>();
			foreach (SpriteRenderer spriteRenderer in componentsInChildren)
			{
				spriteRenderer.color = (_placementValid ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0.3f, 0.3f, 0.5f));
			}
		}
	}

	private Tilemap GetSafeTilemap()
	{
		if (_safeTilemap == null && !_safeTilemapSearched)
		{
			_safeTilemapSearched = true;
			GameObject gameObject = GameObject.Find("Tilemap_safe");
			if (gameObject != null)
			{
				_safeTilemap = gameObject.GetComponent<Tilemap>();
			}
			if (_safeTilemap == null)
			{
				Debug.LogWarning("[DecoPlacement] Tilemap_safe를 찾을 수 없습니다 — 배치 범위 제한 없음");
			}
		}
		return _safeTilemap;
	}

	private bool IsInsideSafeZone(Vector3 world)
	{
		Tilemap safeTilemap = GetSafeTilemap();
		if (safeTilemap == null || _grid == null)
		{
			return true;
		}
		Vector3Int position = _grid.WorldToCell(world);
		position.z = 0;
		return safeTilemap.HasTile(position);
	}

	private Vector3 SnapToGrid(Vector3 world)
	{
		if (_grid == null)
		{
			_grid = Object.FindFirstObjectByType<Grid>();
		}
		if (_grid != null)
		{
			Vector3Int position = _grid.WorldToCell(world);
			Vector3 cellCenterWorld = _grid.GetCellCenterWorld(position);
			cellCenterWorld.z = -1f;
			return cellCenterWorld;
		}
		float x = Mathf.Round(world.x / 0.5f) * 0.5f;
		float y = Mathf.Round(world.y / 0.5f) * 0.5f;
		return new Vector3(x, y, -1f);
	}

	private void ToggleFlip()
	{
		_isFlipped = !_isFlipped;
		Vector3 localScale = _ghost.transform.localScale;
		localScale.x *= -1f;
		_ghost.transform.localScale = localScale;
	}

	private void PlaceItem()
	{
		string text = DecoCatalog.PrefabPath(_selectedItem);
		if (text == null)
		{
			CancelPlacement();
			return;
		}
		if (!_placementValid)
		{
			UIManager.Instance?.ShowStatusMessage("안전 구역(Tilemap_safe) 안에서만 배치할 수 있습니다.");
			return;
		}
		if (_collector != null)
		{
			if (!_collector.inventory.TryGetValue(_selectedItem, out var value) || value <= 0)
			{
				UIManager.Instance?.ShowStatusMessage(_selectedItem + "이(가) 없습니다!");
				CancelPlacement();
				return;
			}
			_collector.inventory[_selectedItem]--;
			if (_collector.inventory[_selectedItem] <= 0)
			{
				_collector.inventory.Remove(_selectedItem);
			}
			_collector.RefreshUI();
		}
		Vector3 position = _ghost.transform.position;
		bool isFlipped = _isFlipped;
		DecoInventoryBridge.AddDeco(_selectedItem, position, isFlipped);
		int num = PlaceScore(_selectedItem);
		_localScore += num;
		UpdateScoreUI();
		NetworkRunner networkRunner = Object.FindFirstObjectByType<NetworkRunner>();
		int num2 = ((networkRunner != null) ? (networkRunner.LocalPlayer.PlayerId - 1) : 0);
		if (num2 >= 0 && num2 < 4)
		{
			GameManager.LocalPlayerScores[num2] += num;
		}
		bool flag = false;
		if (GameManager.Instance != null && GameManager.Instance.Object != null)
		{
			try
			{
				GameManager.Instance.RPC_AddDecorScore(num);
				GameManager.Instance.RPC_AddPlayerDecorScore(num2, num);
				GameManager.Instance.RPC_PlaceDecoItem(_selectedItem, position);
				GameManager.Instance.RPC_TrackPlacedItem(_selectedItem);
				flag = true;
				Debug.Log("[DecoPlacement] RPC_PlaceDecoItem 전송(전원 동기화) → " + _selectedItem);
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning("[DecoPlacement] GameManager RPC 호출 실패 — 로컬로만 배치됩니다 (다른 플레이어에게 보이지 않음): " + ex);
			}
		}
		else
		{
			Debug.LogWarning($"[DecoPlacement] GameManager 네트워크 오브젝트가 준비되지 않음 (Instance={GameManager.Instance != null}, Object={GameManager.Instance?.Object != null}) — 로컬로만 배치됩니다: " + _selectedItem);
		}
		if (!flag)
		{
			SpawnDecoObject(_selectedItem, text, position, isFlipped);
		}
		if (_selectedItem == "나무풍 나무")
		{
			WeatherManager.Instance?.AddTree();
		}
		TutorialManager.Instance?.OnDecoItemPlaced();
		UIManager.Instance?.ShowStatusMessage($"{_selectedItem} 배치 완료! +{num}pt");
		Object.Destroy(_ghost);
		_ghost = null;
		_isPlacing = false;
	}

	private void SpawnDecoObject(string itemName, string resourcePath, Vector3 pos, bool flipped)
	{
		GameObject gameObject = Resources.Load<GameObject>(resourcePath);
		if (!(gameObject == null))
		{
			GameObject gameObject2 = Object.Instantiate(gameObject, pos, Quaternion.identity);
			gameObject2.name = itemName;
			if (flipped)
			{
				Vector3 localScale = gameObject2.transform.localScale;
				localScale.x *= -1f;
				gameObject2.transform.localScale = localScale;
			}
			gameObject2.AddComponent<PlacedDecoItem>();
		}
	}

	private void RestoreSavedDecorations()
	{
		foreach (DecoInventoryBridge.PlacedDeco savedDecoration in DecoInventoryBridge.SavedDecorations)
		{
			string text = DecoCatalog.PrefabPath(savedDecoration.itemName);
			if (text != null)
			{
				SpawnDecoObject(savedDecoration.itemName, text, savedDecoration.position, savedDecoration.flipped);
			}
		}
		Debug.Log($"[DecoPlacement] 저장된 꾸미기 {DecoInventoryBridge.SavedDecorations.Count}개 복원");
	}

	private void CancelPlacement()
	{
		if (_ghost != null)
		{
			Object.Destroy(_ghost);
		}
		_ghost = null;
		_isPlacing = false;
	}

	private void BuildDecoSceneUI()
	{
		Canvas canvas = null;
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
		if (!(canvas == null))
		{
			GameObject gameObject = new GameObject("ReturnBtn", typeof(RectTransform), typeof(Image), typeof(Button));
			gameObject.transform.SetParent(canvas.transform, worldPositionStays: false);
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0f, 0f);
			component.anchorMax = new Vector2(0f, 0f);
			component.pivot = new Vector2(0f, 0f);
			component.anchoredPosition = new Vector2(10f, 10f);
			component.sizeDelta = new Vector2(180f, 50f);
			gameObject.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.2f, 0.95f);
			GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component2 = obj.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			Vector2 offsetMin = (component2.offsetMax = Vector2.zero);
			component2.offsetMin = offsetMin;
			TextMeshProUGUI component3 = obj.GetComponent<TextMeshProUGUI>();
			component3.text = "쓰레기존으로 돌아가기";
			component3.fontSize = 13f;
			component3.fontStyle = FontStyles.Bold;
			component3.alignment = TextAlignmentOptions.Center;
			component3.color = Color.white;
			gameObject.GetComponent<Button>().onClick.AddListener(ReturnToTrashZone);
		}
	}

	private void UpdateScoreUI()
	{
		UIManager.Instance?.UpdateDecorScore(_localScore, 0);
	}

	private void ReturnToTrashZone()
	{
		TrashCollector trashCollector = _collector;
		if (trashCollector == null)
		{
			TrashCollector[] array = Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
			foreach (TrashCollector trashCollector2 in array)
			{
				if (trashCollector2.HasInputAuthority)
				{
					trashCollector = trashCollector2;
					break;
				}
			}
		}
		if (trashCollector != null)
		{
			DecoInventoryBridge.PrepareReturn(trashCollector);
		}
		else
		{
			DecoInventoryBridge.MarkReturning();
		}
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LoadGameScene();
		}
		else
		{
			SceneManager.LoadScene("TrashZoneScene");
		}
	}
}
