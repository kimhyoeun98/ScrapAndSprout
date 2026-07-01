using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementUI : MonoBehaviour
{
	private static readonly Color C_DIM = new Color(0f, 0f, 0f, 0.6f);

	private static readonly Color C_BG = new Color(0.06f, 0.06f, 0.11f, 0.98f);

	private static readonly Color C_HEADER = new Color(0.03f, 0.03f, 0.07f, 1f);

	private static readonly Color C_BORDER = new Color(0.22f, 0.27f, 0.45f, 1f);

	private static readonly Color C_ROW = new Color(0.11f, 0.12f, 0.18f, 1f);

	private static readonly Color C_ROW_DONE = new Color(0.1f, 0.18f, 0.1f, 1f);

	private static readonly Color C_TITLE = new Color(0.92f, 0.88f, 0.76f, 1f);

	private static readonly Color C_DESC = new Color(0.7f, 0.72f, 0.78f, 1f);

	private static readonly Color C_GOLD = new Color(0.96f, 0.83f, 0.25f, 1f);

	private static readonly Color C_BAR_BG = new Color(0.05f, 0.05f, 0.09f, 1f);

	private static readonly Color C_BAR_FILL = new Color(0.3f, 0.62f, 0.3f, 1f);

	private static readonly Color C_DONE = new Color(0.45f, 0.9f, 0.45f, 1f);

	private GameObject _root;

	private RectTransform _listContent;

	private TextMeshProUGUI _summary;

	private readonly List<GameObject> _rows = new List<GameObject>();

	public static AchievementUI Instance { get; private set; }

	public bool IsOpen
	{
		get
		{
			if (_root != null)
			{
				return _root.activeSelf;
			}
			return false;
		}
	}

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else if (Instance != this)
		{
			Object.Destroy(this);
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public void Open()
	{
		if (_root == null)
		{
			Build();
		}
		if (!(_root == null))
		{
			_root.SetActive(value: true);
			_root.transform.SetAsLastSibling();
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			Rebuild();
			StartCoroutine(RefreshFromServer());
		}
	}

	public void Close()
	{
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
	}

	private IEnumerator RefreshFromServer()
	{
		if (AchievementManager.Instance != null)
		{
			yield return AchievementManager.Instance.LoadAchievements();
		}
		if (IsOpen)
		{
			Rebuild();
		}
	}

	private void Build()
	{
		Canvas canvas = Object.FindFirstObjectByType<Canvas>();
		if (canvas == null)
		{
			return;
		}
		RectTransform component = canvas.GetComponent<RectTransform>();
		Canvas[] array = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			Transform transform = array[i].transform.Find("AchievementUI");
			if (transform != null)
			{
				Object.Destroy(transform.gameObject);
			}
		}
		_root = new GameObject("AchievementUI", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(component, worldPositionStays: false);
		RectTransform component2 = _root.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		Vector2 offsetMin = (component2.offsetMax = Vector2.zero);
		component2.offsetMin = offsetMin;
		_root.GetComponent<Image>().color = C_DIM;
		Image image = MkImg(component2, "Box", C_BG);
		image.raycastTarget = true;
		RectTransform rectTransform = image.rectTransform;
		offsetMin = (rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform.anchorMin = offsetMin;
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.sizeDelta = new Vector2(560f, 640f);
		Image image2 = MkImg(rectTransform, "Border", C_BORDER);
		Stretch(image2, -2f);
		image2.transform.SetAsFirstSibling();
		RectTransform rectTransform2 = MkImg(rectTransform, "Header", C_HEADER).rectTransform;
		rectTransform2.anchorMin = new Vector2(0f, 1f);
		rectTransform2.anchorMax = new Vector2(1f, 1f);
		rectTransform2.pivot = new Vector2(0.5f, 1f);
		rectTransform2.anchoredPosition = Vector2.zero;
		rectTransform2.sizeDelta = new Vector2(0f, 56f);
		MkTMP(rectTransform2, "Title", "★ 업적", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-200f, 0f), 22f, FontStyles.Bold, C_TITLE, TextAlignmentOptions.Left);
		_summary = MkTMP(rectTransform2, "Summary", "", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-58f, 0f), 14f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Right);
		MkButton(rectTransform2, "✕", new Color(0.55f, 0.12f, 0.1f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(38f, 38f)).onClick.AddListener(Close);
		GameObject gameObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
		gameObject.transform.SetParent(rectTransform, worldPositionStays: false);
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.offsetMin = new Vector2(10f, 12f);
		component3.offsetMax = new Vector2(-10f, -64f);
		ScrollRect component4 = gameObject.GetComponent<ScrollRect>();
		component4.horizontal = false;
		component4.scrollSensitivity = 24f;
		GameObject gameObject2 = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component5 = gameObject2.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		offsetMin = (component5.offsetMax = Vector2.zero);
		component5.offsetMin = offsetMin;
		gameObject2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
		component4.viewport = component5;
		GameObject obj = new GameObject("Content", typeof(RectTransform));
		obj.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component6 = obj.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0f, 1f);
		component6.anchorMax = new Vector2(1f, 1f);
		component6.pivot = new Vector2(0.5f, 1f);
		component6.anchoredPosition = Vector2.zero;
		component6.sizeDelta = Vector2.zero;
		VerticalLayoutGroup verticalLayoutGroup = obj.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = 6f;
		verticalLayoutGroup.padding = new RectOffset(6, 6, 6, 6);
		verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		obj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		component4.content = component6;
		_listContent = component6;
		_root.SetActive(value: false);
	}

	private void Rebuild()
	{
		if (_listContent == null)
		{
			return;
		}
		foreach (GameObject row in _rows)
		{
			Object.Destroy(row);
		}
		_rows.Clear();
		AchievementManager instance = AchievementManager.Instance;
		List<AchievementData> list = ((instance != null) ? instance.GetCachedAchievements() : null);
		if (list == null || list.Count == 0)
		{
			if (_summary != null)
			{
				_summary.text = "";
			}
			_rows.Add(MkEmptyRow((instance == null) ? "업적 시스템을 찾을 수 없습니다." : "업적을 불러오는 중입니다... (서버 연결 확인)"));
			return;
		}
		int num = 0;
		foreach (AchievementData item in list)
		{
			if (item.isCompleted)
			{
				num++;
			}
		}
		if (_summary != null)
		{
			_summary.text = $"달성 {num}/{list.Count} ({Mathf.RoundToInt(100f * (float)num / (float)list.Count)}%)";
		}
		foreach (AchievementData item2 in list)
		{
			_rows.Add(BuildRow(item2));
		}
	}

	private GameObject BuildRow(AchievementData a)
	{
		Image image = MkImg(_listContent, "Ach_" + a.achievementType, a.isCompleted ? C_ROW_DONE : C_ROW);
		RectTransform rectTransform = image.rectTransform;
		rectTransform.sizeDelta = new Vector2(0f, 96f);
		LayoutElement layoutElement = image.gameObject.AddComponent<LayoutElement>();
		layoutElement.minHeight = 96f;
		layoutElement.preferredHeight = 96f;
		string text = ((!string.IsNullOrEmpty(a.achievementName)) ? a.achievementName : ((!string.IsNullOrEmpty(a.designation)) ? a.designation : a.achievementType));
		MkTMP(rectTransform, "Name", text, new Vector2(0f, 0.54f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-92f, -6f), 24f, FontStyles.Bold, a.isCompleted ? C_DONE : C_TITLE, TextAlignmentOptions.Left);
		MkTMP(rectTransform, "Desc", a.detail ?? "", new Vector2(0f, 0.3f), new Vector2(1f, 0.54f), new Vector2(16f, 0f), new Vector2(-92f, 0f), 16f, FontStyles.Normal, C_DESC, TextAlignmentOptions.Left);
		float x = (a.isCompleted ? 1f : ((a.targetValue <= 0) ? Mathf.Clamp01((float)a.progressPercent / 100f) : Mathf.Clamp01((float)a.currentProgress / (float)a.targetValue)));
		RectTransform rectTransform2 = MkImg(rectTransform, "BarBg", C_BAR_BG).rectTransform;
		rectTransform2.anchorMin = new Vector2(0f, 0f);
		rectTransform2.anchorMax = new Vector2(1f, 0f);
		rectTransform2.pivot = new Vector2(0.5f, 0f);
		rectTransform2.anchoredPosition = new Vector2(0f, 12f);
		rectTransform2.sizeDelta = new Vector2(-28f, 14f);
		RectTransform rectTransform3 = MkImg(rectTransform2, "Fill", a.isCompleted ? C_DONE : C_BAR_FILL).rectTransform;
		rectTransform3.anchorMin = new Vector2(0f, 0f);
		rectTransform3.anchorMax = new Vector2(x, 1f);
		Vector2 offsetMin = (rectTransform3.offsetMax = Vector2.zero);
		rectTransform3.offsetMin = offsetMin;
		string text2 = (a.isCompleted ? "달성 ✓" : ((a.targetValue > 0) ? $"{a.currentProgress} / {a.targetValue}" : $"{Mathf.RoundToInt((float)a.progressPercent)}%"));
		MkTMP(rectTransform, "Prog", text2, new Vector2(0.58f, 0.54f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-14f, -6f), 18f, FontStyles.Bold, a.isCompleted ? C_DONE : C_GOLD, TextAlignmentOptions.Right);
		return image.gameObject;
	}

	private GameObject MkEmptyRow(string msg)
	{
		Image image = MkImg(_listContent, "Empty", C_ROW);
		image.rectTransform.sizeDelta = new Vector2(0f, 64f);
		LayoutElement layoutElement = image.gameObject.AddComponent<LayoutElement>();
		layoutElement.minHeight = 64f;
		layoutElement.preferredHeight = 64f;
		MkTMP(image.rectTransform, "Msg", msg, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f), 15f, FontStyles.Normal, C_DESC, TextAlignmentOptions.Center);
		return image.gameObject;
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
		RectTransform rectTransform = img.rectTransform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(offset, offset);
		rectTransform.offsetMax = new Vector2(0f - offset, 0f - offset);
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
		component2.overflowMode = TextOverflowModes.Overflow;
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
		component3.fontSize = 16f;
		component3.fontStyle = FontStyles.Bold;
		component3.alignment = TextAlignmentOptions.Center;
		component3.color = new Color(0.95f, 0.92f, 0.85f);
		return gameObject.GetComponent<Button>();
	}
}
