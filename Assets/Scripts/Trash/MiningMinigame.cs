using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(9999)]
public class MiningMinigame : MonoBehaviour
{
	[Header("── 미니게임 UI ──")]
	[Tooltip("미니게임 전체 패널 (시작 시 켜짐)")]
	public GameObject minigamePanel;

	[Tooltip("방향키 아이콘들이 들어갈 부모 오브젝트")]
	public Transform sequenceContainer;

	[Tooltip("입력 피드백 텍스트 (✓ / ✗)")]
	public TextMeshProUGUI inputFeedbackText;

	[Tooltip("남은 시간 텍스트")]
	public TextMeshProUGUI timerText;

	[Tooltip("시간 바 (선택)")]
	public Slider timerSlider;

	[Tooltip("채굴 진행 바 (미연결 시 자동 생성)")]
	public Slider progressSlider;

	[Tooltip("진행 카운터 텍스트 (미연결 시 자동 생성)")]
	public TextMeshProUGUI progressText;

	[Header("── 방향키 아이콘 ──")]
	[Tooltip("방향키 아이콘 프리팹 (Image 컴포넌트 포함)")]
	public GameObject arrowIconPrefab;

	[Tooltip("위쪽 화살표 스프라이트")]
	public Sprite arrowUp;

	[Tooltip("아래쪽 화살표 스프라이트")]
	public Sprite arrowDown;

	[Tooltip("왼쪽 화살표 스프라이트")]
	public Sprite arrowLeft;

	[Tooltip("오른쪽 화살표 스프라이트")]
	public Sprite arrowRight;

	[Header("── 색상 설정 ──")]
	[Tooltip("아직 입력 안 한 아이콘 색")]
	public Color pendingColor = Color.white;

	[Tooltip("현재 입력해야 할 아이콘 색 (강조)")]
	public Color currentColor = Color.yellow;

	[Tooltip("성공적으로 입력한 아이콘 색")]
	public Color successColor = Color.green;

	[Tooltip("틀렸을 때 아이콘 색")]
	public Color failColor = Color.red;

	[Header("── 시간 설정 ──")]
	[Tooltip("전체 제한 시간 (초). 입력 횟수에 비례해서 늘어남")]
	public float secondsPerInput = 2.5f;

	private List<KeyCode> _sequence = new List<KeyCode>();

	private int _currentIndex;

	private bool _isPlaying;

	private float _timeLeft;

	private Action<bool> _resultCallback;

	private List<GameObject> _iconObjects = new List<GameObject>();

	private static readonly KeyCode[] _arrowKeys = new KeyCode[4]
	{
		KeyCode.UpArrow,
		KeyCode.DownArrow,
		KeyCode.LeftArrow,
		KeyCode.RightArrow
	};

	private Coroutine _feedbackCoroutine;

	public static MiningMinigame Instance { get; private set; }

	public bool IsPlaying => _isPlaying;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		if (minigamePanel == null)
		{
			Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
			if (canvas != null)
			{
				Transform transform = canvas.transform.Find("MinigamePanel");
				if (transform != null)
				{
					minigamePanel = transform.gameObject;
				}
			}
		}
		if (minigamePanel == null)
		{
			Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				Transform transform2 = array[i].transform.Find("MinigamePanel");
				if (transform2 != null)
				{
					minigamePanel = transform2.gameObject;
					break;
				}
			}
		}
		if (minigamePanel != null)
		{
			minigamePanel.SetActive(value: false);
		}
		else
		{
			Debug.LogWarning("[MiningMinigame] MinigamePanel을 찾을 수 없습니다!");
		}
	}

	public void StartMinigame(int requiredInputs, Action<bool> callback)
	{
		if (_isPlaying)
		{
			Debug.LogWarning("[MiningMinigame] 이미 진행 중입니다!");
			return;
		}
		_resultCallback = callback;
		_currentIndex = 0;
		_isPlaying = true;
		_sequence.Clear();
		for (int i = 0; i < requiredInputs; i++)
		{
			KeyCode item = _arrowKeys[UnityEngine.Random.Range(0, _arrowKeys.Length)];
			_sequence.Add(item);
		}
		_timeLeft = (float)requiredInputs * secondsPerInput;
		SetupUI();
		Debug.Log(string.Format("[MiningMinigame] 시작! 시퀀스: {0}, 제한시간: {1}초", string.Join(", ", _sequence), _timeLeft));
	}

	private void SetupUI()
	{
		foreach (GameObject iconObject in _iconObjects)
		{
			if (iconObject != null)
			{
				UnityEngine.Object.Destroy(iconObject);
			}
		}
		_iconObjects.Clear();
		for (int i = 0; i < _sequence.Count; i++)
		{
			if (arrowIconPrefab == null)
			{
				break;
			}
			if (sequenceContainer == null)
			{
				break;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(arrowIconPrefab, sequenceContainer);
			Image component = gameObject.GetComponent<Image>();
			if (component != null)
			{
				component.sprite = GetArrowSprite(_sequence[i]);
				component.color = ((i == 0) ? currentColor : pendingColor);
			}
			_iconObjects.Add(gameObject);
		}
		if (inputFeedbackText != null)
		{
			inputFeedbackText.text = "";
		}
		if (timerSlider != null)
		{
			timerSlider.value = 1f;
		}
		if (minigamePanel != null)
		{
			minigamePanel.SetActive(value: true);
		}
		if (progressSlider == null && minigamePanel != null)
		{
			BuildProgressBar();
		}
		UpdateProgressUI();
		LockPlayerMovement(lockMovement: true);
	}

	private void LockPlayerMovement(bool lockMovement)
	{
		PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		foreach (PlayerMovement playerMovement in array)
		{
			if (playerMovement.HasInputAuthority)
			{
				playerMovement.IsMovementLocked = lockMovement;
				Debug.Log($"[MiningMinigame] 잠금 적용: {playerMovement.name} → {lockMovement}");
				break;
			}
		}
	}

	private void Update()
	{
		if (!_isPlaying)
		{
			return;
		}
		_timeLeft -= Time.deltaTime;
		UpdateTimerUI();
		if (_timeLeft <= 0f)
		{
			EndMinigame(success: false);
			return;
		}
		KeyCode[] arrowKeys = _arrowKeys;
		foreach (KeyCode keyCode in arrowKeys)
		{
			if (Input.GetKeyDown(keyCode))
			{
				HandleInput(keyCode);
				break;
			}
		}
	}

	private void HandleInput(KeyCode pressedKey)
	{
		if (_currentIndex < _sequence.Count)
		{
			KeyCode keyCode = _sequence[_currentIndex];
			if (pressedKey == keyCode)
			{
				OnCorrectInput();
			}
			else
			{
				OnWrongInput();
			}
		}
	}

	private void OnCorrectInput()
	{
		if (_currentIndex < _iconObjects.Count)
		{
			Image component = _iconObjects[_currentIndex].GetComponent<Image>();
			if (component != null)
			{
				component.color = successColor;
			}
		}
		_currentIndex++;
		UpdateProgressUI();
		if (_currentIndex < _iconObjects.Count)
		{
			Image component2 = _iconObjects[_currentIndex].GetComponent<Image>();
			if (component2 != null)
			{
				component2.color = currentColor;
			}
		}
		ShowFeedback("✓", successColor);
		if (_currentIndex >= _sequence.Count)
		{
			StartCoroutine(DelayedEnd(success: true, 0.3f));
		}
		Debug.Log($"[MiningMinigame] 정답! {_currentIndex}/{_sequence.Count}");
	}

	private void OnWrongInput()
	{
		if (_currentIndex < _iconObjects.Count)
		{
			Image component = _iconObjects[_currentIndex].GetComponent<Image>();
			if (component != null)
			{
				component.color = failColor;
			}
		}
		ShowFeedback("✗", failColor);
		Debug.Log("[MiningMinigame] 오답! 미니게임 실패");
		StartCoroutine(DelayedEnd(success: false, 0.5f));
	}

	private IEnumerator DelayedEnd(bool success, float delay)
	{
		yield return new WaitForSeconds(delay);
		EndMinigame(success);
	}

	private void EndMinigame(bool success)
	{
		_isPlaying = false;
		if (minigamePanel != null)
		{
			minigamePanel.SetActive(value: false);
		}
		LockPlayerMovement(lockMovement: false);
		foreach (GameObject iconObject in _iconObjects)
		{
			if (iconObject != null)
			{
				UnityEngine.Object.Destroy(iconObject);
			}
		}
		_iconObjects.Clear();
		Debug.Log("[MiningMinigame] 종료 — " + (success ? "성공" : "실패"));
		_resultCallback?.Invoke(success);
		_resultCallback = null;
	}

	public void CancelMinigame()
	{
		if (_isPlaying)
		{
			EndMinigame(success: false);
			UIManager.Instance?.ShowStatusMessage("채굴 취소됨", 1f);
		}
	}

	private void UpdateTimerUI()
	{
		float num = (float)_sequence.Count * secondsPerInput;
		float value = _timeLeft / num;
		if (timerText != null)
		{
			timerText.text = $"{_timeLeft:F1}s";
		}
		if (timerSlider != null)
		{
			timerSlider.value = value;
		}
	}

	private void UpdateProgressUI()
	{
		int count = _sequence.Count;
		float value = ((count > 0) ? ((float)_currentIndex / (float)count) : 0f);
		if (progressSlider != null)
		{
			progressSlider.value = value;
		}
		if (progressText != null)
		{
			progressText.text = $"{_currentIndex} / {count}";
		}
	}

	private void BuildProgressBar()
	{
		if (minigamePanel == null)
		{
			Debug.LogWarning("[MiningMinigame] minigamePanel이 없어서 진행 바를 생성할 수 없습니다!");
			return;
		}
		GameObject gameObject = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image), typeof(Slider));
		gameObject.transform.SetParent(minigamePanel.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(1f, 0f);
		component.pivot = new Vector2(0.5f, 0f);
		component.anchoredPosition = new Vector2(0f, 8f);
		component.sizeDelta = new Vector2(-20f, 16f);
		gameObject.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
		GameObject gameObject2 = new GameObject("Fill Area", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = new Vector2(2f, 2f);
		component2.offsetMax = new Vector2(-2f, -2f);
		GameObject obj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component3 = obj.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = new Vector2(1f, 1f);
		component3.sizeDelta = Vector2.zero;
		obj.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f, 1f);
		Slider component4 = gameObject.GetComponent<Slider>();
		component4.fillRect = component3;
		component4.direction = Slider.Direction.LeftToRight;
		component4.minValue = 0f;
		component4.maxValue = 1f;
		component4.value = 0f;
		component4.interactable = false;
		progressSlider = component4;
		GameObject obj2 = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
		obj2.transform.SetParent(minigamePanel.transform, worldPositionStays: false);
		RectTransform component5 = obj2.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0f, 0f);
		component5.anchorMax = new Vector2(1f, 0f);
		component5.pivot = new Vector2(0.5f, 0f);
		component5.anchoredPosition = new Vector2(0f, 26f);
		component5.sizeDelta = new Vector2(0f, 20f);
		TextMeshProUGUI component6 = obj2.GetComponent<TextMeshProUGUI>();
		component6.text = "0 / 0";
		component6.fontSize = 14f;
		component6.fontStyle = FontStyles.Bold;
		component6.alignment = TextAlignmentOptions.Center;
		component6.color = Color.white;
		progressText = component6;
	}

	private void ShowFeedback(string text, Color color)
	{
		if (!(inputFeedbackText == null))
		{
			inputFeedbackText.text = text;
			inputFeedbackText.color = color;
			if (_feedbackCoroutine != null)
			{
				StopCoroutine(_feedbackCoroutine);
			}
			_feedbackCoroutine = StartCoroutine(ClearFeedbackAfter(0.4f));
		}
	}

	private IEnumerator ClearFeedbackAfter(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (inputFeedbackText != null)
		{
			inputFeedbackText.text = "";
		}
	}

	private Sprite GetArrowSprite(KeyCode key)
	{
		return key switch
		{
			KeyCode.UpArrow => arrowUp, 
			KeyCode.DownArrow => arrowDown, 
			KeyCode.LeftArrow => arrowLeft, 
			KeyCode.RightArrow => arrowRight, 
			_ => null, 
		};
	}
}
