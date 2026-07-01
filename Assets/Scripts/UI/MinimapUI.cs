using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class MinimapUI : MonoBehaviour
{
	[Header("── 월드 X 범위 ──")]
	public float worldMinX = -150f;

	public float worldMaxX = 150f;

	[Header("── 월드 Y 범위 (쓰레기 위치 표시용) ──")]
	[Tooltip("맵 세로 최소/최대 월드 Y. 쓰레기 점이 위아래로 몰리면 이 값을 조정")]
	public float worldMinY = -12f;

	public float worldMaxY = 8f;

	[Tooltip("켜면 시작 시 MapBorder(걷기 영역) 크기에 맞춰 worldMin/Max를 자동 보정")]
	public bool autoFitToMapBorder = true;

	[Header("── 진입 불가 구역 경계 ──")]
	public float boundaryLeftX = -10f;

	public float boundaryRightX = 10f;

	[Header("── UI 참조 ──")]
	[SerializeField]
	private RectTransform mapArea;

	[SerializeField]
	private RectTransform boundaryLeft;

	[SerializeField]
	private RectTransform boundaryRight;

	[SerializeField]
	private RectTransform boundaryZone;

	[SerializeField]
	private RectTransform safeZone;

	[SerializeField]
	private RectTransform trashLabel;

	[SerializeField]
	private RectTransform safeLabel;

	private static readonly Color C_ME = new Color(1f, 0.88f, 0.2f, 1f);

	private static readonly Color C_PLAYER = new Color(0.88f, 0.85f, 0.78f, 1f);

	private static readonly Color C_BOT = new Color(0.52f, 0.52f, 0.52f, 1f);

	private static readonly Color C_TRASH = new Color(0.3f, 0.8f, 0.45f, 1f);

	private readonly Dictionary<PlayerMovement, RectTransform> _dots = new Dictionary<PlayerMovement, RectTransform>();

	private readonly Dictionary<TrashPile, RectTransform> _trashDots = new Dictionary<TrashPile, RectTransform>();

	private float _timer;

	private void Start()
	{
		if (autoFitToMapBorder)
		{
			FitBoundsToMapBorder();
			FitZoneBoundary();
		}
		ApplyBoundaryPositions();
	}

	private void FitZoneBoundary()
	{
		float num = TilemapEdgeWorldX("Tilemap_safe", wantMax: false);
		float num2 = TilemapEdgeWorldX("Tilemap_trash", wantMax: true);
		if (!float.IsNaN(num) && !float.IsNaN(num2))
		{
			Debug.Log($"[MinimapUI] 구역 경계 자동 맞춤 — 세이프左:{num:F1} 쓰레기右:{num2:F1} → 경계:{boundaryRightX = (boundaryLeftX = (num + num2) * 0.5f):F1}");
		}
	}

	private float TilemapEdgeWorldX(string objName, bool wantMax)
	{
		GameObject gameObject = GameObject.Find(objName);
		if (gameObject == null)
		{
			return float.NaN;
		}
		Tilemap component = gameObject.GetComponent<Tilemap>();
		if (component == null)
		{
			return float.NaN;
		}
		component.CompressBounds();
		BoundsInt cellBounds = component.cellBounds;
		Vector3Int[] obj = new Vector3Int[4]
		{
			new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0),
			new Vector3Int(cellBounds.xMin, cellBounds.yMax, 0),
			new Vector3Int(cellBounds.xMax, cellBounds.yMin, 0),
			new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0)
		};
		float num = (wantMax ? float.MinValue : float.MaxValue);
		Vector3Int[] array = obj;
		foreach (Vector3Int position in array)
		{
			float x = component.GetCellCenterWorld(position).x;
			num = (wantMax ? Mathf.Max(num, x) : Mathf.Min(num, x));
		}
		return num;
	}

	private void FitBoundsToMapBorder()
	{
		GameObject gameObject = GameObject.Find("MapBorder");
		if (gameObject == null)
		{
			Debug.LogWarning("[MinimapUI] MapBorder 못 찾음 — 수동 worldMin/Max 사용");
			return;
		}
		PolygonCollider2D component = gameObject.GetComponent<PolygonCollider2D>();
		if (component == null)
		{
			Debug.LogWarning("[MinimapUI] MapBorder에 PolygonCollider2D 없음");
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		for (int i = 0; i < component.pathCount; i++)
		{
			Vector2[] path = component.GetPath(i);
			foreach (Vector2 vector in path)
			{
				Vector2 vector2 = gameObject.transform.TransformPoint(vector);
				if (vector2.x < num)
				{
					num = vector2.x;
				}
				if (vector2.x > num2)
				{
					num2 = vector2.x;
				}
				if (vector2.y < num3)
				{
					num3 = vector2.y;
				}
				if (vector2.y > num4)
				{
					num4 = vector2.y;
				}
			}
		}
		if (num < num2 && num3 < num4)
		{
			worldMinX = num;
			worldMaxX = num2;
			worldMinY = num3;
			worldMaxY = num4;
			Debug.Log($"[MinimapUI] 맵 경계 자동 맞춤 — X[{num:F1},{num2:F1}] Y[{num3:F1},{num4:F1}]");
		}
	}

	private void ApplyBoundaryPositions()
	{
		float x = Mathf.InverseLerp(worldMinX, worldMaxX, boundaryLeftX);
		float x2 = Mathf.InverseLerp(worldMinX, worldMaxX, boundaryRightX);
		if (boundaryLeft != null)
		{
			boundaryLeft.anchorMin = new Vector2(x, 0f);
			boundaryLeft.anchorMax = new Vector2(x, 1f);
			RectTransform rectTransform = boundaryLeft;
			Vector2 offsetMin = (boundaryLeft.offsetMax = Vector2.zero);
			rectTransform.offsetMin = offsetMin;
		}
		if (boundaryRight != null)
		{
			boundaryRight.anchorMin = new Vector2(x2, 0f);
			boundaryRight.anchorMax = new Vector2(x2, 1f);
			RectTransform rectTransform2 = boundaryRight;
			Vector2 offsetMin = (boundaryRight.offsetMax = Vector2.zero);
			rectTransform2.offsetMin = offsetMin;
		}
		if (boundaryZone != null)
		{
			boundaryZone.anchorMin = new Vector2(x, 0f);
			boundaryZone.anchorMax = new Vector2(x2, 1f);
			RectTransform rectTransform3 = boundaryZone;
			Vector2 offsetMin = (boundaryZone.offsetMax = Vector2.zero);
			rectTransform3.offsetMin = offsetMin;
		}
		if (safeZone != null)
		{
			safeZone.anchorMin = new Vector2(x2, 0f);
			safeZone.anchorMax = new Vector2(1f, 1f);
			RectTransform rectTransform4 = safeZone;
			Vector2 offsetMin = (safeZone.offsetMax = Vector2.zero);
			rectTransform4.offsetMin = offsetMin;
		}
		if (trashLabel != null)
		{
			trashLabel.anchorMin = new Vector2(0f, 0f);
			trashLabel.anchorMax = new Vector2(x, 1f);
			RectTransform rectTransform5 = trashLabel;
			Vector2 offsetMin = (trashLabel.offsetMax = Vector2.zero);
			rectTransform5.offsetMin = offsetMin;
		}
		if (safeLabel != null)
		{
			safeLabel.anchorMin = new Vector2(x2, 0f);
			safeLabel.anchorMax = new Vector2(1f, 1f);
			RectTransform rectTransform6 = safeLabel;
			Vector2 offsetMin = (safeLabel.offsetMax = Vector2.zero);
			rectTransform6.offsetMin = offsetMin;
		}
	}

	private void Update()
	{
		_timer -= Time.deltaTime;
		if (!(_timer > 0f))
		{
			_timer = 0.1f;
			RefreshDots();
		}
	}

	private void RefreshDots()
	{
		if (mapArea == null)
		{
			Debug.LogWarning("[MinimapUI] mapArea가 NULL — Inspector에서 MapArea 연결 필요");
			return;
		}
		PlayerMovement[] array = Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		HashSet<PlayerMovement> hashSet = new HashSet<PlayerMovement>(array);
		List<PlayerMovement> list = new List<PlayerMovement>();
		foreach (KeyValuePair<PlayerMovement, RectTransform> dot in _dots)
		{
			if (!hashSet.Contains(dot.Key))
			{
				if ((bool)dot.Value)
				{
					Object.Destroy(dot.Value.gameObject);
				}
				list.Add(dot.Key);
			}
		}
		foreach (PlayerMovement item in list)
		{
			_dots.Remove(item);
		}
		PlayerMovement[] array2 = array;
		foreach (PlayerMovement playerMovement in array2)
		{
			if (!(playerMovement == null))
			{
				if (!_dots.TryGetValue(playerMovement, out var value) || value == null)
				{
					value = CreateDot(playerMovement);
				}
				Vector3 position = playerMovement.transform.position;
				float x = Mathf.Clamp01(Mathf.InverseLerp(worldMinX, worldMaxX, position.x));
				float y = Mathf.Clamp01(Mathf.InverseLerp(worldMinY, worldMaxY, position.y));
				RectTransform rectTransform = value;
				Vector2 anchorMin = (value.anchorMax = new Vector2(x, y));
				rectTransform.anchorMin = anchorMin;
				value.anchoredPosition = Vector2.zero;
			}
		}
		RefreshTrashDots();
	}

	private void RefreshTrashDots()
	{
		if (mapArea == null)
		{
			return;
		}
		TrashPile[] array = Object.FindObjectsByType<TrashPile>(FindObjectsSortMode.None);
		HashSet<TrashPile> hashSet = new HashSet<TrashPile>(array);
		List<TrashPile> list = new List<TrashPile>();
		foreach (KeyValuePair<TrashPile, RectTransform> trashDot in _trashDots)
		{
			if (!hashSet.Contains(trashDot.Key))
			{
				if ((bool)trashDot.Value)
				{
					Object.Destroy(trashDot.Value.gameObject);
				}
				list.Add(trashDot.Key);
			}
		}
		foreach (TrashPile item in list)
		{
			_trashDots.Remove(item);
		}
		TrashPile[] array2 = array;
		foreach (TrashPile trashPile in array2)
		{
			if (!(trashPile == null))
			{
				if (!_trashDots.TryGetValue(trashPile, out var value) || value == null)
				{
					value = CreateTrashDot();
				}
				Vector3 position = trashPile.transform.position;
				float x = Mathf.Clamp01(Mathf.InverseLerp(worldMinX, worldMaxX, position.x));
				float y = Mathf.Clamp01(Mathf.InverseLerp(worldMinY, worldMaxY, position.y));
				RectTransform rectTransform = value;
				Vector2 anchorMin = (value.anchorMax = new Vector2(x, y));
				rectTransform.anchorMin = anchorMin;
				value.anchoredPosition = Vector2.zero;
				_trashDots[trashPile] = value;
			}
		}
	}

	private RectTransform CreateTrashDot()
	{
		GameObject obj = new GameObject("TrashDot", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(mapArea, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(7f, 7f);
		component.pivot = new Vector2(0.5f, 0.5f);
		Image component2 = obj.GetComponent<Image>();
		component2.color = C_TRASH;
		component2.raycastTarget = false;
		return component;
	}

	private RectTransform CreateDot(PlayerMovement pm)
	{
		bool flag = pm.GetComponent<AIBotController>() != null && !pm.HasInputAuthority;
		bool flag2 = pm.HasInputAuthority && !flag;
		Color color = (flag ? C_BOT : (flag2 ? C_ME : C_PLAYER));
		float num = (flag2 ? 12f : 9f);
		GameObject gameObject = new GameObject(flag2 ? "MeDot" : (flag ? "BotDot" : "Dot"), typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(mapArea, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(num, num);
		component.pivot = new Vector2(0.5f, 0.5f);
		Image component2 = gameObject.GetComponent<Image>();
		component2.color = color;
		component2.raycastTarget = false;
		if (flag2)
		{
			GameObject obj = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component3 = obj.GetComponent<RectTransform>();
			Vector2 anchorMin = (component3.anchorMax = new Vector2(0.5f, 0.5f));
			component3.anchorMin = anchorMin;
			component3.pivot = new Vector2(0.5f, 0.5f);
			component3.anchoredPosition = Vector2.zero;
			component3.sizeDelta = new Vector2(num + 4f, num + 4f);
			Image component4 = obj.GetComponent<Image>();
			component4.color = new Color(0f, 0f, 0f, 0.55f);
			component4.raycastTarget = false;
			obj.transform.SetAsFirstSibling();
		}
		_dots[pm] = component;
		return component;
	}
}
