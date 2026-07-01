using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
	private GameObject _panel;

	private TextMeshProUGUI _msgText;

	private TextMeshProUGUI _tipText;

	private RectTransform _fillBar;

	private bool _active;

	private float _dotTimer;

	private int _dotCount;

	private static readonly string[] _tips = new string[7] { "쓰레기를 채굴해서 NPC에게 판매하세요!", "나무를 배치하면 날씨 이벤트 확률이 줄어들어요.", "채굴 미니게임은 화살표 키를 빠르게 입력하세요!", "골드를 모아 꾸미기 아이템을 구매할 수 있어요.", "배터리는 시간이 지나면 자동으로 회복됩니다.", "각 캐릭터마다 고유한 특성이 있어요. 잘 활용하세요!", "자신의 구역을 꾸며 팀 꾸미기 점수를 올리세요!" };

	public static LoadingScreen Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		Build();
		_panel.SetActive(value: false);
	}

	private void Update()
	{
		if (!_active)
		{
			return;
		}
		_dotTimer += Time.unscaledDeltaTime;
		if (_dotTimer >= 0.45f)
		{
			_dotTimer = 0f;
			_dotCount = (_dotCount + 1) % 4;
			if (_msgText != null)
			{
				_msgText.text = "로딩 중" + new string('.', _dotCount);
			}
		}
		if (_fillBar != null)
		{
			float t = (Mathf.Sin(Time.unscaledTime * 1.8f) + 1f) * 0.5f;
			_fillBar.sizeDelta = new Vector2(Mathf.Lerp(40f, 380f, t), _fillBar.sizeDelta.y);
		}
	}

	public void Show()
	{
		if (!(_panel == null))
		{
			_active = true;
			_dotCount = 0;
			_dotTimer = 0f;
			if (_msgText != null)
			{
				_msgText.text = "로딩 중";
			}
			if (_tipText != null)
			{
				_tipText.text = "TIP  " + _tips[Random.Range(0, _tips.Length)];
			}
			_panel.SetActive(value: true);
		}
	}

	public void Hide()
	{
		StartCoroutine(HideRoutine());
	}

	private IEnumerator HideRoutine()
	{
		yield return new WaitForSecondsRealtime(0.6f);
		_active = false;
		if (_panel != null)
		{
			_panel.SetActive(value: false);
		}
	}

	private void Build()
	{
		GameObject gameObject = new GameObject("LoadingCanvas");
		gameObject.transform.SetParent(base.transform);
		Canvas canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 999;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		gameObject.AddComponent<GraphicRaycaster>();
		MakeImage(gameObject.transform, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.03f, 1f));
		MakeImage(gameObject.transform, "TopLine", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(0f, 0f), new Color(0.3f, 0.58f, 0.22f, 0.8f));
		MakeImage(gameObject.transform, "BottomLine", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Color(0.3f, 0.58f, 0.22f, 0.8f));
		MakeTMP(gameObject.transform, "Title", "Scrap & Sprout", new Vector2(0.5f, 0.58f), new Vector2(600f, 90f), 40f, FontStyles.Bold, new Color(0.85f, 0.74f, 0.28f));
		MakeTMP(gameObject.transform, "Sub", "쓰레기를 줍고, 공간을 꾸미자!", new Vector2(0.5f, 0.5f), new Vector2(500f, 36f), 16f, FontStyles.Normal, new Color(0.65f, 0.8f, 0.55f, 0.7f));
		_msgText = MakeTMP(gameObject.transform, "Msg", "로딩 중", new Vector2(0.5f, 0.4f), new Vector2(300f, 40f), 20f, FontStyles.Normal, new Color(0.87f, 0.84f, 0.77f));
		MakeImage(gameObject.transform, "BarBG", new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), new Vector2(-200f, -4f), new Vector2(200f, 4f), new Color(0.12f, 0.18f, 0.1f));
		GameObject gameObject2 = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		_fillBar = gameObject2.GetComponent<RectTransform>();
		RectTransform fillBar = _fillBar;
		Vector2 anchorMin = (_fillBar.anchorMax = new Vector2(0.5f, 0.34f));
		fillBar.anchorMin = anchorMin;
		_fillBar.pivot = new Vector2(0f, 0.5f);
		_fillBar.anchoredPosition = new Vector2(-200f, 0f);
		_fillBar.sizeDelta = new Vector2(100f, 8f);
		gameObject2.GetComponent<Image>().color = new Color(0.3f, 0.58f, 0.22f);
		gameObject2.GetComponent<Image>().raycastTarget = false;
		_tipText = MakeTMP(gameObject.transform, "Tip", "", new Vector2(0.5f, 0.26f), new Vector2(700f, 50f), 14f, FontStyles.Normal, new Color(0.65f, 0.72f, 0.58f, 0.8f));
		_panel = gameObject;
	}

	private static void MakeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.offsetMin = offsetMin;
		component.offsetMax = offsetMax;
		Image component2 = obj.GetComponent<Image>();
		component2.color = color;
		component2.raycastTarget = false;
	}

	private static TextMeshProUGUI MakeTMP(Transform parent, string name, string text, Vector2 anchorPos, Vector2 size, float fontSize, FontStyles style, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = anchorPos);
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = Vector2.zero;
		component.sizeDelta = size;
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = text;
		component2.fontSize = fontSize;
		component2.fontStyle = style;
		component2.color = color;
		component2.alignment = TextAlignmentOptions.Center;
		component2.raycastTarget = false;
		return component2;
	}
}
