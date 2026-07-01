using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
	[Header("── HUD ──")]
	public TextMeshProUGUI goldText;

	public TextMeshProUGUI timerText;

	public TextMeshProUGUI decorScoreText;

	public TextMeshProUGUI statusMessageText;

	public TextMeshProUGUI weatherText;

	[Header("── 인벤토리 ──")]
	public GameObject inventoryPanel;

	public GameObject hotbar;

	[Header("── 안내 텍스트 ──")]
	public GameObject pickText;

	[Header("── 게임 종료 버튼 ──")]
	public Button gameExitButton;

	private bool _isInventoryOpen;

	private int _nearbyTrashCount;

	private GameObject _exitConfirmPanel;

	[Header("── 페이드 패널 ──")]
	[Tooltip("전체화면 검정 Image. Canvas 하위에 배치 후 연결")]
	public Image fadePanel;

	private Coroutine _fadeCoroutine;

	private Coroutine _statusCoroutine;

	private PlayerMovement _cachedLocalPlayer;

	private float _playerFindTimer;

	private Image _batteryWarningOverlay;

	private Coroutine _batteryPulseCoroutine;

	private bool _batteryWarningActive;

	public static UIManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			SceneManager.sceneLoaded += OnSceneLoaded;
			Debug.Log("[UIManager] 싱글톤 생성 + DontDestroyOnLoad + 씬 로드 감지");
		}
		else if (Instance != this)
		{
			Debug.Log("[UIManager] 중복 생성 제거");
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		Debug.Log("[UIManager] 씬 로드됨: " + scene.name);
		if (scene.name != "TrashZoneScene" && scene.name != "DecoScene" && scene.name != "TutorialScene")
		{
			Debug.Log("[UIManager] 비게임 씬(" + scene.name + ") 진입 — 잔여 HUD 캔버스 파괴");
			Instance = null;
			SceneManager.sceneLoaded -= OnSceneLoaded;
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else
		{
			StartCoroutine(ReinitializeUINextFrame());
		}
	}

	private void Start()
	{
		if (Instance != this)
		{
			Debug.Log("[UIManager] 중복 인스턴스의 Start() 무시");
		}
		else
		{
			InitializeUI();
		}
	}

	private IEnumerator ReinitializeUINextFrame()
	{
		yield return null;
		yield return null;
		yield return new WaitForSeconds(0.5f);
		if (this == null)
		{
			Debug.LogWarning("[UIManager] UIManager가 destroyed됨");
			yield break;
		}
		if (Instance != this)
		{
			Debug.LogWarning("[UIManager] Instance가 다름 (중복 제거됨)");
			yield break;
		}
		if (!base.gameObject.activeInHierarchy)
		{
			Debug.LogWarning("[UIManager] gameObject이 비활성화됨");
			yield break;
		}
		string text = SceneManager.GetActiveScene().name;
		Debug.Log("[UIManager] 현재 씬: " + text);
		Debug.Log(string.Format("[UIManager] 씬 이름 비교: '{0}' == 'TrashZoneScene' ? {1}", text, text == "TrashZoneScene"));
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (canvas != null)
		{
			Transform root = canvas.transform.root;
			Debug.Log("[UIManager] " + root.name + " DontDestroyOnLoad 설정");
		}
		bool flag = text == "TrashZoneScene";
		AutoConnectHUD();
		LeaderboardUI[] array = UnityEngine.Object.FindObjectsByType<LeaderboardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (LeaderboardUI leaderboardUI in array)
		{
			if (!(leaderboardUI == null))
			{
				leaderboardUI.gameObject.SetActive(value: true);
				Debug.Log("[UIManager] LeaderboardUI " + (flag ? "활성" : "비활성") + " 씬");
			}
		}
		MinimapUI[] array2 = UnityEngine.Object.FindObjectsByType<MinimapUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (MinimapUI minimapUI in array2)
		{
			if (!(minimapUI == null))
			{
				minimapUI.gameObject.SetActive(flag);
				Debug.Log("[UIManager] MinimapUI " + (flag ? "표시" : "숨김"));
			}
		}
		if (!flag && text == "DecoScene")
		{
			Debug.Log("[UIManager] DecoScene - UI 숨김 (배치 UI만 표시)");
		}
		Debug.Log("[UIManager] UI 재초기화 완료");
	}

	private void InitializeUI()
	{
		Debug.Log("[UIManager] InitializeUI() 시작");
		if (pickText != null)
		{
			pickText.SetActive(value: false);
		}
		if (hotbar != null)
		{
			hotbar.SetActive(value: true);
		}
		if (inventoryPanel != null)
		{
			inventoryPanel.SetActive(value: false);
		}
		AutoConnectHUD();
		AutoConnectGameExitButton();
		BuildExitConfirmPanel();
		BuildWeatherText();
		BuildBatteryUI();
		Debug.Log("[UIManager] UI 초기화 완료");
	}

	private void BuildBatteryUI()
	{
		GameObject gameObject = GameObject.Find("BatteryBar");
		if (gameObject == null)
		{
			Canvas component = GetComponent<Canvas>();
			if (component != null)
			{
				Transform transform = component.transform.Find("HUD");
				if (transform != null)
				{
					gameObject = transform.Find("BatteryBar")?.gameObject;
				}
			}
		}
		if (gameObject != null)
		{
			Debug.Log("[UIManager] BatteryBar 찾음: " + gameObject.name);
		}
		else
		{
			Debug.LogWarning("[UIManager] 씬에서 BatteryBar 오브젝트를 찾을 수 없습니다");
		}
	}

	private void AutoConnectHUD()
	{
		TextMeshProUGUI[] array = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (TextMeshProUGUI textMeshProUGUI in array)
		{
			switch (textMeshProUGUI.gameObject.name)
			{
			case "GoldText":
				if (goldText == null)
				{
					goldText = textMeshProUGUI;
				}
				break;
			case "TimerText":
				if (timerText == null)
				{
					timerText = textMeshProUGUI;
				}
				break;
			case "DecorScoreText":
				if (decorScoreText == null)
				{
					decorScoreText = textMeshProUGUI;
				}
				break;
			case "StatusMessageText":
				if (statusMessageText == null)
				{
					statusMessageText = textMeshProUGUI;
				}
				break;
			case "PickedText":
				if (pickText == null)
				{
					pickText = textMeshProUGUI.gameObject;
				}
				break;
			case "WeatherText":
				if (weatherText == null)
				{
					weatherText = textMeshProUGUI;
				}
				break;
			}
		}
		Debug.Log("[UIManager] pickText 자동 연결: " + ((pickText != null) ? pickText.name : "실패"));
		if (statusMessageText != null)
		{
			statusMessageText.gameObject.SetActive(value: true);
			statusMessageText.text = "";
			RectTransform rectTransform = statusMessageText.rectTransform;
			Vector2 sizeDelta = rectTransform.sizeDelta;
			if (sizeDelta.x < 700f)
			{
				sizeDelta.x = 700f;
				rectTransform.sizeDelta = sizeDelta;
			}
			statusMessageText.alignment = TextAlignmentOptions.Center;
			statusMessageText.enableWordWrapping = false;
		}
		Debug.Log($"[UIManager] HUD 자동 연결 — Gold:{goldText != null} Timer:{timerText != null} Score:{decorScoreText != null} Status:{statusMessageText != null}");
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.I) && !TrashZoneChat.IsTyping)
		{
			ToggleInventory();
		}
		CheckNearbyTrash();
	}

	private void BuildExitConfirmPanel()
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (!(canvas == null))
		{
			_exitConfirmPanel = new GameObject("ExitConfirmPanel", typeof(RectTransform), typeof(Image));
			_exitConfirmPanel.transform.SetParent(canvas.transform, worldPositionStays: false);
			RectTransform component = _exitConfirmPanel.GetComponent<RectTransform>();
			component.anchorMin = Vector2.zero;
			component.anchorMax = Vector2.one;
			Vector2 offsetMin = (component.offsetMax = Vector2.zero);
			component.offsetMin = offsetMin;
			_exitConfirmPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			GameObject gameObject = new GameObject("Box", typeof(RectTransform), typeof(Image));
			gameObject.transform.SetParent(_exitConfirmPanel.transform, worldPositionStays: false);
			RectTransform component2 = gameObject.GetComponent<RectTransform>();
			offsetMin = (component2.anchorMax = new Vector2(0.5f, 0.5f));
			component2.anchorMin = offsetMin;
			component2.sizeDelta = new Vector2(340f, 180f);
			gameObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);
			GameObject obj = new GameObject("Question", typeof(RectTransform), typeof(TextMeshProUGUI));
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component3 = obj.GetComponent<RectTransform>();
			component3.anchorMin = new Vector2(0f, 0.5f);
			component3.anchorMax = new Vector2(1f, 1f);
			component3.offsetMin = new Vector2(20f, 0f);
			component3.offsetMax = new Vector2(-20f, -10f);
			TextMeshProUGUI component4 = obj.GetComponent<TextMeshProUGUI>();
			component4.text = "게임을 나가시겠습니까?";
			component4.fontSize = 20f;
			component4.fontStyle = FontStyles.Bold;
			component4.alignment = TextAlignmentOptions.Center;
			component4.color = Color.white;
			GameObject gameObject2 = new GameObject("Buttons", typeof(RectTransform));
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component5 = gameObject2.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 0f);
			component5.anchorMax = new Vector2(1f, 0.5f);
			component5.offsetMin = new Vector2(20f, 16f);
			component5.offsetMax = new Vector2(-20f, -8f);
			HorizontalLayoutGroup horizontalLayoutGroup = gameObject2.AddComponent<HorizontalLayoutGroup>();
			horizontalLayoutGroup.spacing = 16f;
			horizontalLayoutGroup.childControlWidth = true;
			horizontalLayoutGroup.childControlHeight = true;
			horizontalLayoutGroup.childForceExpandWidth = true;
			horizontalLayoutGroup.childForceExpandHeight = true;
			CreateConfirmButton(gameObject2.transform, "로비로 나가기", new Color(0.7f, 0.2f, 0.2f, 1f), OnExitToLobby);
			CreateConfirmButton(gameObject2.transform, "계속 게임", new Color(0.2f, 0.55f, 0.2f, 1f), OnContinueGame);
			_exitConfirmPanel.SetActive(value: false);
		}
	}

	private void CreateConfirmButton(Transform parent, string label, Color color, UnityAction onClick)
	{
		GameObject gameObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.GetComponent<Image>().color = color;
		GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		Vector2 offsetMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = offsetMin;
		TextMeshProUGUI component2 = obj.GetComponent<TextMeshProUGUI>();
		component2.text = label;
		component2.fontSize = 16f;
		component2.fontStyle = FontStyles.Bold;
		component2.alignment = TextAlignmentOptions.Center;
		component2.color = Color.white;
		gameObject.GetComponent<Button>().onClick.AddListener(onClick);
	}

	private void ToggleExitConfirm()
	{
		if (!(_exitConfirmPanel == null))
		{
			bool flag = !_exitConfirmPanel.activeSelf;
			_exitConfirmPanel.SetActive(flag);
			Cursor.visible = flag;
			Cursor.lockState = (flag ? CursorLockMode.None : CursorLockMode.None);
		}
	}

	private void OnExitToLobby()
	{
		_exitConfirmPanel.SetActive(value: false);
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LeaveRoom();
		}
		else
		{
			SceneManager.LoadScene("LobbyScene");
		}
	}

	private void OnContinueGame()
	{
		_exitConfirmPanel?.SetActive(value: false);
	}

	private void AutoConnectGameExitButton()
	{
		if (gameExitButton != null)
		{
			WireExitButton();
			return;
		}
		Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault((Button b) => b.gameObject.name == "NextRoundButton");
		if (button != null)
		{
			gameExitButton = button;
			WireExitButton();
		}
	}

	private void WireExitButton()
	{
		gameExitButton.gameObject.SetActive(value: true);
		TextMeshProUGUI componentInChildren = gameExitButton.GetComponentInChildren<TextMeshProUGUI>();
		if (componentInChildren != null)
		{
			componentInChildren.text = "게임 종료";
		}
		gameExitButton.onClick.RemoveAllListeners();
		gameExitButton.onClick.AddListener(delegate
		{
			if (GameManager.Instance != null)
			{
				GameManager.Instance.GoToResultScene();
			}
		});
		Debug.Log("[UIManager] 게임 종료 버튼 연결 완료");
	}

	private void ToggleInventory()
	{
		_isInventoryOpen = !_isInventoryOpen;
		if (inventoryPanel != null)
		{
			inventoryPanel.SetActive(_isInventoryOpen);
		}
		if (hotbar != null)
		{
			hotbar.SetActive(!_isInventoryOpen);
		}
	}

	public void ForceCloseInventory()
	{
		_isInventoryOpen = false;
		if (inventoryPanel != null)
		{
			inventoryPanel.SetActive(value: false);
		}
		if (hotbar != null)
		{
			hotbar.SetActive(value: true);
		}
	}

	public void OnTrashEnter()
	{
		_nearbyTrashCount++;
		if (pickText != null)
		{
			pickText.SetActive(value: true);
		}
		Debug.Log($"[UIManager] OnTrashEnter — 범위 내 쓰레기: {_nearbyTrashCount}개");
	}

	public void OnTrashExit()
	{
		_nearbyTrashCount--;
		if (_nearbyTrashCount <= 0)
		{
			_nearbyTrashCount = 0;
			if (pickText != null)
			{
				pickText.SetActive(value: false);
			}
		}
		Debug.Log($"[UIManager] OnTrashExit — 범위 내 쓰레기: {_nearbyTrashCount}개");
	}

	public void OnTrashCollected()
	{
		_nearbyTrashCount = 0;
		if (pickText != null)
		{
			pickText.SetActive(value: false);
		}
	}

	public void FadeOut(float duration)
	{
		if (!(this == null))
		{
			if (_fadeCoroutine != null)
			{
				StopCoroutine(_fadeCoroutine);
			}
			_fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, duration));
		}
	}

	public void FadeIn(float duration)
	{
		if (!(this == null))
		{
			if (_fadeCoroutine != null)
			{
				StopCoroutine(_fadeCoroutine);
			}
			_fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, duration));
		}
	}

	private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
	{
		if (!(fadePanel == null))
		{
			fadePanel.gameObject.SetActive(value: true);
			float elapsed = 0f;
			Color c = fadePanel.color;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				c.a = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
				fadePanel.color = c;
				yield return null;
			}
			c.a = toAlpha;
			fadePanel.color = c;
			if (toAlpha <= 0f)
			{
				fadePanel.gameObject.SetActive(value: false);
			}
		}
	}

	public void ShowStatusMessage(string message, float duration = 2f)
	{
		if (string.IsNullOrEmpty(message))
		{
			return;
		}
		Debug.Log("[UIManager] " + message);
		if (this == null || statusMessageText == null)
		{
			return;
		}
		try
		{
			if (base.gameObject.activeInHierarchy)
			{
				if (_statusCoroutine != null)
				{
					StopCoroutine(_statusCoroutine);
				}
				_statusCoroutine = StartCoroutine(ShowStatusRoutine(message, duration));
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[UIManager] ShowStatusMessage 실패: " + ex.Message);
		}
	}

	private IEnumerator ShowStatusRoutine(string message, float duration)
	{
		statusMessageText.text = message;
		statusMessageText.gameObject.SetActive(value: true);
		yield return new WaitForSeconds(duration);
		statusMessageText.gameObject.SetActive(value: false);
		_statusCoroutine = null;
	}

	private void CheckNearbyTrash()
	{
		if (pickText == null)
		{
			return;
		}
		_playerFindTimer -= Time.deltaTime;
		if (_playerFindTimer <= 0f || _cachedLocalPlayer == null)
		{
			_playerFindTimer = 1f;
			_cachedLocalPlayer = null;
			PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
			foreach (PlayerMovement playerMovement in array)
			{
				if (playerMovement.HasInputAuthority)
				{
					_cachedLocalPlayer = playerMovement;
					break;
				}
			}
		}
		if (_cachedLocalPlayer == null)
		{
			pickText.SetActive(value: false);
			return;
		}
		Vector2 a = new Vector2(_cachedLocalPlayer.transform.position.x, _cachedLocalPlayer.transform.position.y);
		TrashPile[] array2 = UnityEngine.Object.FindObjectsByType<TrashPile>(FindObjectsSortMode.None);
		bool active = false;
		TrashPile[] array3 = array2;
		foreach (TrashPile trashPile in array3)
		{
			if (!(trashPile == null))
			{
				Vector2 b = new Vector2(trashPile.transform.position.x, trashPile.transform.position.y);
				if (Vector2.Distance(a, b) < 2.5f)
				{
					active = true;
					break;
				}
			}
		}
		pickText.SetActive(active);
	}

	public void UpdateGold(int gold)
	{
		if (goldText != null)
		{
			goldText.text = $"Gold: {gold:N0}G";
		}
	}

	public void UpdateTimer(float elapsedSeconds)
	{
		if (!(timerText == null))
		{
			int num = Mathf.FloorToInt(elapsedSeconds / 60f);
			int num2 = Mathf.FloorToInt(elapsedSeconds % 60f);
			timerText.text = $"{num:00}:{num2:00}";
			timerText.color = Color.white;
		}
	}

	public void UpdateDecorScore(int current, int goal)
	{
		if (decorScoreText != null)
		{
			decorScoreText.text = $"꾸미기: {current}pt";
		}
	}

	public void SetBatteryWarning(bool active)
	{
		if (_batteryWarningActive == active)
		{
			return;
		}
		_batteryWarningActive = active;
		if (_batteryWarningOverlay == null)
		{
			BuildBatteryWarningOverlay();
		}
		if (active)
		{
			_batteryWarningOverlay.gameObject.SetActive(value: true);
			if (_batteryPulseCoroutine != null)
			{
				StopCoroutine(_batteryPulseCoroutine);
			}
			_batteryPulseCoroutine = StartCoroutine(BatteryPulseRoutine());
			return;
		}
		if (_batteryPulseCoroutine != null)
		{
			StopCoroutine(_batteryPulseCoroutine);
		}
		_batteryPulseCoroutine = null;
		if (_batteryWarningOverlay != null)
		{
			_batteryWarningOverlay.gameObject.SetActive(value: false);
		}
	}

	private void BuildBatteryWarningOverlay()
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (!(canvas == null))
		{
			GameObject gameObject = new GameObject("BatteryWarningOverlay", typeof(RectTransform), typeof(Image));
			gameObject.transform.SetParent(canvas.transform, worldPositionStays: false);
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.anchorMin = Vector2.zero;
			component.anchorMax = Vector2.one;
			Vector2 offsetMin = (component.offsetMax = Vector2.zero);
			component.offsetMin = offsetMin;
			_batteryWarningOverlay = gameObject.GetComponent<Image>();
			_batteryWarningOverlay.color = new Color(1f, 0f, 0f, 0f);
			_batteryWarningOverlay.raycastTarget = false;
			gameObject.SetActive(value: false);
		}
	}

	private IEnumerator BatteryPulseRoutine()
	{
		while (true)
		{
			yield return OverlayFade(0.04f, 0.28f, 0.35f);
			yield return OverlayFade(0.28f, 0.04f, 0.35f);
		}
	}

	private IEnumerator OverlayFade(float from, float to, float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			if (_batteryWarningOverlay != null)
			{
				_batteryWarningOverlay.color = new Color(1f, 0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
			}
			yield return null;
		}
	}

	public void ShowMilestoneNotification(int milestone)
	{
		StartCoroutine(MilestoneRoutine(milestone));
	}

	private IEnumerator MilestoneRoutine(int milestone)
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (!(canvas == null))
		{
			GameObject bg = new GameObject("MilestoneBG", typeof(RectTransform), typeof(Image));
			bg.transform.SetParent(canvas.transform, worldPositionStays: false);
			RectTransform component = bg.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.anchoredPosition = new Vector2(0f, 90f);
			component.sizeDelta = new Vector2(400f, 72f);
			Image bgImg = bg.GetComponent<Image>();
			bgImg.color = new Color(0.05f, 0.07f, 0.04f, 0.94f);
			bgImg.raycastTarget = false;
			GameObject obj = new GameObject("Bar", typeof(RectTransform), typeof(Image));
			obj.transform.SetParent(bg.transform, worldPositionStays: false);
			RectTransform component2 = obj.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0f, 1f);
			component2.anchorMax = new Vector2(1f, 1f);
			component2.pivot = new Vector2(0.5f, 1f);
			component2.anchoredPosition = Vector2.zero;
			component2.sizeDelta = new Vector2(0f, 4f);
			obj.GetComponent<Image>().color = new Color(0.85f, 0.74f, 0.28f, 1f);
			obj.GetComponent<Image>().raycastTarget = false;
			GameObject gameObject = new GameObject("MilestoneText", typeof(RectTransform), typeof(TextMeshProUGUI));
			gameObject.transform.SetParent(bg.transform, worldPositionStays: false);
			RectTransform component3 = gameObject.GetComponent<RectTransform>();
			component3.anchorMin = Vector2.zero;
			component3.anchorMax = Vector2.one;
			component3.offsetMin = new Vector2(12f, 4f);
			component3.offsetMax = new Vector2(-12f, -4f);
			TextMeshProUGUI tmp = gameObject.GetComponent<TextMeshProUGUI>();
			tmp.text = $"팀 꾸미기 {milestone:N0}pt 달성!";
			tmp.fontSize = 24f;
			tmp.fontStyle = FontStyles.Bold;
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.color = new Color(0.85f, 0.74f, 0.28f);
			tmp.raycastTarget = false;
			float elapsed = 0f;
			while (elapsed < 0.2f)
			{
				elapsed += Time.deltaTime;
				float num = Mathf.Lerp(0.6f, 1f, elapsed / 0.2f);
				bg.transform.localScale = new Vector3(num, num, 1f);
				yield return null;
			}
			bg.transform.localScale = Vector3.one;
			yield return new WaitForSeconds(2.5f);
			elapsed = 0f;
			Color bgColor = bgImg.color;
			Color txtColor = tmp.color;
			while (elapsed < 0.5f)
			{
				elapsed += Time.deltaTime;
				float num2 = elapsed / 0.5f;
				bgImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * (1f - num2));
				tmp.color = new Color(txtColor.r, txtColor.g, txtColor.b, 1f - num2);
				yield return null;
			}
			UnityEngine.Object.Destroy(bg);
		}
	}

	public void UpdateWeather(WeatherManager.WeatherType weather)
	{
		if (weatherText == null)
		{
			BuildWeatherText();
		}
		if (!(weatherText == null))
		{
			switch (weather)
			{
			case WeatherManager.WeatherType.Clear:
				weatherText.text = "☀ 맑음";
				weatherText.color = Color.yellow;
				break;
			case WeatherManager.WeatherType.AcidRain:
				weatherText.text = "☂ 산성비";
				weatherText.color = new Color(0.4f, 1f, 0.4f);
				break;
			case WeatherManager.WeatherType.YellowDust:
				weatherText.text = "\ud83d\udca8 황사";
				weatherText.color = new Color(1f, 0.8f, 0.3f);
				break;
			}
		}
	}

	private void BuildWeatherText()
	{
		if (!(weatherText != null))
		{
			Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
			if (!(canvas == null))
			{
				GameObject gameObject = new GameObject("WeatherCard", typeof(RectTransform), typeof(Image));
				gameObject.transform.SetParent(canvas.transform, worldPositionStays: false);
				RectTransform component = gameObject.GetComponent<RectTransform>();
				component.anchorMin = new Vector2(0.5f, 1f);
				component.anchorMax = new Vector2(0.5f, 1f);
				component.pivot = new Vector2(0.5f, 1f);
				component.anchoredPosition = new Vector2(0f, -10f);
				component.sizeDelta = new Vector2(140f, 38f);
				gameObject.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.04f, 0.88f);
				gameObject.GetComponent<Image>().raycastTarget = false;
				GameObject obj = new GameObject("Bar", typeof(RectTransform), typeof(Image));
				obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
				RectTransform component2 = obj.GetComponent<RectTransform>();
				component2.anchorMin = new Vector2(0f, 1f);
				component2.anchorMax = new Vector2(1f, 1f);
				component2.pivot = new Vector2(0.5f, 1f);
				component2.anchoredPosition = Vector2.zero;
				component2.sizeDelta = new Vector2(0f, 3f);
				obj.GetComponent<Image>().color = new Color(0.3f, 0.58f, 0.22f, 1f);
				obj.GetComponent<Image>().raycastTarget = false;
				GameObject gameObject2 = new GameObject("WeatherText", typeof(RectTransform), typeof(TextMeshProUGUI));
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				RectTransform component3 = gameObject2.GetComponent<RectTransform>();
				component3.anchorMin = Vector2.zero;
				component3.anchorMax = Vector2.one;
				component3.offsetMin = new Vector2(6f, 2f);
				component3.offsetMax = new Vector2(-6f, -3f);
				weatherText = gameObject2.GetComponent<TextMeshProUGUI>();
				weatherText.fontSize = 15f;
				weatherText.fontStyle = FontStyles.Bold;
				weatherText.color = new Color(0.85f, 0.74f, 0.28f);
				weatherText.alignment = TextAlignmentOptions.Center;
				weatherText.raycastTarget = false;
			}
		}
	}

	public void ShowFloatingGold(int delta, Vector3 worldPos)
	{
		if (delta != 0)
		{
			StartCoroutine(FloatingGoldRoutine(delta, worldPos));
		}
	}

	private IEnumerator FloatingGoldRoutine(int delta, Vector3 worldPos)
	{
		Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		if (!(canvas == null))
		{
			GameObject go = new GameObject("FloatingGold", typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(canvas.transform, worldPositionStays: false);
			TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
			tmp.text = ((delta > 0) ? $"+{delta:N0}G" : $"{delta:N0}G");
			tmp.fontSize = 22f;
			tmp.fontStyle = FontStyles.Bold;
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.raycastTarget = false;
			Color textColor = (tmp.color = ((delta > 0) ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.4f, 0.4f)));
			RectTransform rect = go.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(140f, 40f);
			Camera main = Camera.main;
			Vector2 screenPoint = ((main != null) ? ((Vector2)main.WorldToScreenPoint(worldPos + Vector3.up * 1.5f)) : new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f));
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), screenPoint, (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : main, out var localPoint);
			rect.anchoredPosition = localPoint;
			float duration = 1.5f;
			float elapsed = 0f;
			Vector2 startPos = rect.anchoredPosition;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float num = elapsed / duration;
				tmp.color = new Color(textColor.r, textColor.g, textColor.b, 1f - num);
				rect.anchoredPosition = startPos + new Vector2(0f, 60f * num);
				yield return null;
			}
			UnityEngine.Object.Destroy(go);
		}
	}
}
