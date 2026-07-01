using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
	[Serializable]
	public class TutorialStep
	{
		[Tooltip("말풍선에 표시될 제목 (예: 이동하기)")]
		public string title;

		[TextArea(2, 5)]
		[Tooltip("본문 설명 텍스트")]
		public string body;

		[Tooltip("하이라이트할 UI 오브젝트 (없으면 비워두세요)")]
		public GameObject highlightTarget;

		[Tooltip("화살표 포인터 위치 (월드 오브젝트, 없으면 비워두세요)")]
		public Transform pointerTarget;

		[Tooltip("이 단계에서 플레이어 이동을 잠글지 여부")]
		public bool lockMovement;

		[Tooltip("자동으로 다음 단계로 넘어갈 조건 타입")]
		public AutoAdvanceType autoAdvance;

		[Tooltip("자동 진행이 None이 아닐 때 대기 시간 (초)")]
		public float autoDelay;
	}

	public enum AutoAdvanceType
	{
		None,
		Timer,
		OnMove,
		OnEKeyPress,
		OnFKeyPress,
		OnSceneChange,
		OnDecoItemPlaced
	}

	[Header("── 튜토리얼 단계 ──")]
	[Tooltip("비워두면 InitDefaultSteps()로 자동 생성됩니다")]
	public List<TutorialStep> steps = new List<TutorialStep>();

	[Header("── 튜토리얼 오프라인 세션 (플레이어/봇/쓰레기) ──")]
	[Tooltip("쓰레기 더미 X축 간격 (플레이어 위치 기준 좌우)")]
	public float trashSpawnOffsetX = 1.5f;

	[Tooltip("쓰레기 더미 Y축 보정값 (프리팹 피벗 차이로 인해 떠 보이면 음수로 조정)")]
	public float trashSpawnOffsetY = -9f;

	[Header("── UI 연결 ──")]
	[Tooltip("튜토리얼 전체 패널 루트")]
	public GameObject tutorialPanel;

	[Tooltip("말풍선 제목 텍스트")]
	public TextMeshProUGUI titleText;

	[Tooltip("말풍선 본문 텍스트")]
	public TextMeshProUGUI bodyText;

	[Tooltip("'다음' 버튼")]
	public Button nextButton;

	[Tooltip("'건너뛰기' 버튼")]
	public Button skipButton;

	[Tooltip("현재 단계 표시 텍스트 (예: 3 / 8)")]
	public TextMeshProUGUI stepCountText;

	[Tooltip("하이라이트용 반투명 오버레이 Image")]
	public Image highlightOverlay;

	[Tooltip("화살표 포인터 오브젝트 (애니메이션 포함)")]
	public RectTransform pointerArrow;

	[Tooltip("화살표가 가리키는 방향 (기본: 아래 ↓)")]
	public Vector2 pointerOffset = new Vector2(0f, -60f);

	private int _currentStep;

	private bool _isRunning;

	public static string AllowedPurchaseItem = "나무풍 상자";

	private Coroutine _autoCoroutine;

	private Vector3 _lastPlayerPos;

	private NetworkRunner _tutorialRunner;

	private Vector3 _tutorialBasePos;

	private TutorialInputProvider _tutorialInput;

	public static TutorialManager Instance { get; private set; }

	public static bool IsTutorialActive { get; private set; } = false;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private async void Start()
	{
		if (steps == null || steps.Count == 0)
		{
			InitDefaultSteps();
		}
		if (nextButton != null)
		{
			nextButton.onClick.AddListener(NextStep);
		}
		if (skipButton != null)
		{
			skipButton.onClick.AddListener(SkipTutorial);
		}
		SetPanelVisible(visible: false);
		if (!(await StartTutorialSession()))
		{
			Debug.LogWarning("[Tutorial] 세션 시작 실패 — 플레이어/봇 없이 진행됩니다.");
		}
		StartTutorial();
	}

	private void InitDefaultSteps()
	{
		steps = new List<TutorialStep>
		{
			new TutorialStep
			{
				title = "Scrap & Sprout에 오신 걸 환영해요!",
				body = "인류가 사라진 지구.\n로봇인 당신이 쓰레기를 줍고 나만의 공간을 꾸미는 게임이에요.\n먼저 기본 조작을 배워볼게요!",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.Timer,
				autoDelay = 1.5f
			},
			new TutorialStep
			{
				title = "이동하기",
				body = "방향키(↑ ↓ ← →)로 이동할 수 있어요.\n한번 움직여 보세요!",
				lockMovement = false,
				autoAdvance = AutoAdvanceType.OnMove
			},
			new TutorialStep
			{
				title = "쓰레기 더미 채굴",
				body = "노란색 쓰레기 더미에 가까이 다가가면\n'E키를 눌러 채굴' 안내가 떠요.\n\n【E키】를 누르면 채굴 미니게임이 시작돼요!",
				lockMovement = false,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "채굴 미니게임",
				body = "화면에 방향키 순서가 표시돼요.\n순서대로 정확하게 입력하면 성공!\n\n 성공 → 쓰레기 아이템 획득\n 실패 또는 시간 초과 → 배터리 -10%",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "배터리 시스템",
				body = "채굴할 때마다 배터리가 소모돼요.\n배터리는 시간이 지나면 충전이 되어요.",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "NPC에게 쓰레기 판매",
				body = "수거한 쓰레기는 세이프존 NPC에게 팔 수 있어요.\n\nNPC 앞에서 【F키】를 누르면 상점이 열려요.\n'전체 판매' 버튼으로 한 번에 팔 수 있답니다!",
				lockMovement = false,
				autoAdvance = AutoAdvanceType.OnFKeyPress
			},
			new TutorialStep
			{
				title = "날씨 시스템",
				body = "게임 중 날씨가 변해요.\n\n맑음 → 기본 난이도\n 산성비 → 채굴 미니게임 +2단계 어려워짐\n황사 → 이동 속도 저하\n\n나무를 많이 심을수록 날씨가 안정돼요!",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "텔레포터로 세이프 존 이동",
				body = "쓰레기 존과 세이프 존은 텔레포터로만 이동해요.\n\n텔레포터 앞에서 【T키】를 누르면 이동!\n\n골드가 어느 정도 모이면 세이프 존으로 가세요.",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "세이프 존 도착!",
				body = "여기는 세이프 존이에요.\nNPC에게 쓰레기를 팔고 골드를 모을 수 있어요.\n\n골드로 세이프존을 꾸밀 수 있어요!",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.OnSceneChange
			},
			new TutorialStep
			{
				title = " 꾸미기 아이템 구매",
				body = "상점에서 나무, 상자, 의자 등\n다양한 아이템을 살 수 있어요.\n\n나무풍 테마로 세트를 맞추면\n5% 보너스 점수가 추가돼요! \n\n아이템을 하나 구매해서 Q키를 눌러 배치해보세요!",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.OnDecoItemPlaced
			},
			new TutorialStep
			{
				title = "씨앗 심기",
				body = "인벤토리에 '나무'가 있을 때\n【Q키】를 눌러 씨앗 심기 모드로 전환!\n\n원하는 위치를 클릭하면 나무가 심어져요.\n나무가 많을수록 날씨가 좋아져요",
				lockMovement = true,
				autoAdvance = AutoAdvanceType.None
			},
			new TutorialStep
			{
				title = "튜토리얼 완료!",
				body = "기본 조작을 모두 배웠어요!\n\n핵심 루프:\n 채굴 → 이동 → 판매 → 꾸미기\n\n자, 이제 쓰레기를 줍고\n나만의 공간을 꾸며볼까요?",
				lockMovement = false,
				autoAdvance = AutoAdvanceType.None
			}
		};
	}

	public void StartTutorial()
	{
		_currentStep = 0;
		_isRunning = true;
		IsTutorialActive = true;
		SetPanelVisible(visible: true);
		ShowStep(_currentStep);
		Debug.Log("[Tutorial] 시작!");
	}

	public void NextStep()
	{
		if (_isRunning)
		{
			StopAutoCoroutine();
			_currentStep++;
			if (_currentStep >= steps.Count)
			{
				EndTutorial();
			}
			else
			{
				ShowStep(_currentStep);
			}
		}
	}

	public void SkipTutorial()
	{
		StopAutoCoroutine();
		EndTutorial();
		Debug.Log("[Tutorial] 건너뜀");
	}

	private void ShowStep(int index)
	{
		if (index < 0 || index >= steps.Count)
		{
			return;
		}
		TutorialStep tutorialStep = steps[index];
		if (titleText != null)
		{
			titleText.text = tutorialStep.title;
		}
		if (bodyText != null)
		{
			bodyText.text = tutorialStep.body;
		}
		if (stepCountText != null)
		{
			stepCountText.text = $"{index + 1} / {steps.Count}";
		}
		LockPlayerMovement(tutorialStep.lockMovement);
		UpdateHighlight(tutorialStep.highlightTarget);
		UpdatePointer(tutorialStep.pointerTarget);
		if (nextButton != null)
		{
			TextMeshProUGUI componentInChildren = nextButton.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.text = ((index == steps.Count - 1) ? "완료!" : "다음 >");
			}
		}
		SetupAutoAdvance(tutorialStep);
		Debug.Log($"[Tutorial] Step {index + 1}: {tutorialStep.title}");
	}

	private void SetupAutoAdvance(TutorialStep step)
	{
		StopAutoCoroutine();
		switch (step.autoAdvance)
		{
		case AutoAdvanceType.Timer:
			if (step.autoDelay > 0f)
			{
				_autoCoroutine = StartCoroutine(AutoAfterDelay(step.autoDelay));
			}
			break;
		case AutoAdvanceType.OnMove:
		{
			PlayerMovement playerMovement = FindLocalPlayer();
			if (playerMovement != null)
			{
				_lastPlayerPos = playerMovement.transform.position;
			}
			_autoCoroutine = StartCoroutine(WaitForMove());
			break;
		}
		case AutoAdvanceType.OnEKeyPress:
			_autoCoroutine = StartCoroutine(WaitForKey(KeyCode.E));
			break;
		case AutoAdvanceType.OnFKeyPress:
			_autoCoroutine = StartCoroutine(WaitForKey(KeyCode.F));
			break;
		case AutoAdvanceType.OnSceneChange:
			SceneManager.sceneLoaded += OnSceneLoaded;
			break;
		case AutoAdvanceType.None:
			break;
		}
	}

	private IEnumerator AutoAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);
		NextStep();
	}

	private IEnumerator WaitForMove()
	{
		PlayerMovement pm = FindLocalPlayer();
		if (!(pm == null))
		{
			while (pm != null && Vector3.Distance(pm.transform.position, _lastPlayerPos) < 0.3f)
			{
				yield return null;
			}
			if (!(pm == null))
			{
				NextStep();
			}
		}
	}

	private IEnumerator WaitForKey(KeyCode key)
	{
		yield return null;
		while (!Input.GetKeyDown(key))
		{
			yield return null;
		}
		NextStep();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		StartCoroutine(DelayedNextStep(0.5f));
	}

	private IEnumerator DelayedNextStep(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (tutorialPanel != null)
		{
			SetPanelVisible(visible: true);
		}
		NextStep();
	}

	private void StopAutoCoroutine()
	{
		if (_autoCoroutine != null)
		{
			StopCoroutine(_autoCoroutine);
			_autoCoroutine = null;
		}
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private async void EndTutorial()
	{
		_isRunning = false;
		IsTutorialActive = false;
		StopAutoCoroutine();
		LockPlayerMovement(lockMovement: false);
		SetPanelVisible(visible: false);
		UpdateHighlight(null);
		UpdatePointer(null);
		PlayerPrefs.SetInt("TutorialDone", 1);
		PlayerPrefs.Save();
		UIManager.Instance?.ShowStatusMessage("튜토리얼 완료! 즐겁게 플레이하세요 ", 3f);
		Debug.Log("[Tutorial] 완료!");
		await ShutdownTutorialSession();
		SceneManager.LoadScene("LobbyScene");
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void Update()
	{
		_tutorialInput?.PollKeys();
	}

	private async Task<bool> StartTutorialSession()
	{
		try
		{
			Application.runInBackground = true;
			GameObject runnerObj = new GameObject("TutorialNetworkRunner");
			_tutorialRunner = runnerObj.AddComponent<NetworkRunner>();
			_tutorialRunner.ProvideInput = true;
			runnerObj.AddComponent<RunnerSimulatePhysics2D>();
			StartGameArgs args = new StartGameArgs
			{
				GameMode = GameMode.Single,
				PlayerCount = 1
			};
			StartGameResult startGameResult = await _tutorialRunner.StartGame(args);
			if (!startGameResult.Ok)
			{
				Debug.LogError($"[Tutorial] Fusion 세션 시작 실패: {startGameResult.ShutdownReason} - {startGameResult.ErrorMessage}");
				UnityEngine.Object.Destroy(runnerObj);
				_tutorialRunner = null;
				return false;
			}
			Debug.Log("[Tutorial] 오프라인 세션 시작 성공 (GameMode.Single)");
			_tutorialInput = new TutorialInputProvider();
			_tutorialRunner.AddCallbacks(_tutorialInput);
			SpawnTutorialPlayerAndBot();
			SpawnTutorialTrash();
			SpawnTutorialNPC();
			EnsureInventoryUI();
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError("[Tutorial] Fusion 세션 예외: " + ex.Message + "\n" + ex.StackTrace);
			return false;
		}
	}

	private void SpawnTutorialPlayerAndBot()
	{
		if (_tutorialRunner == null)
		{
			return;
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("SpawnPoint");
		Vector3 vector = ((array.Length > 1) ? array[1].transform.position : new Vector3(3f, 0f, 0f));
		vector.z = -1.2f;
		_tutorialBasePos = vector;
		Vector3 vector2 = vector + new Vector3(-0.8f, 0f, 0f);
		NetworkObject networkObject = Resources.Load<NetworkObject>("PlayerAlpha");
		if (networkObject == null)
		{
			Debug.LogError("[Tutorial] PlayerAlpha 프리팹 로드 실패! (Resources 폴더 확인)");
		}
		else if (_tutorialRunner.Spawn(networkObject, vector, Quaternion.identity, _tutorialRunner.LocalPlayer) != null)
		{
			Debug.Log($"[Tutorial] 플레이어 스폰 완료 at {vector}");
		}
		NetworkObject networkObject2 = Resources.Load<NetworkObject>("PlayerBeta");
		if (networkObject2 == null)
		{
			Debug.LogError("[Tutorial] PlayerBeta 프리팹 로드 실패! (Resources 폴더 확인)");
			return;
		}
		NetworkObject networkObject3 = _tutorialRunner.Spawn(networkObject2, vector2, Quaternion.identity);
		if (networkObject3 != null)
		{
			Debug.Log($"[Tutorial] AI 봇 스폰 완료 at {vector2}");
			if (networkObject3.GetComponent<AIBotController>() == null)
			{
				networkObject3.gameObject.AddComponent<AIBotController>();
			}
		}
	}

	private void SpawnTutorialTrash()
	{
		if (_tutorialRunner == null)
		{
			return;
		}
		string[] obj = new string[4] { "TrashPile", "TrashPile_Small", "SmallTrashPile", "TrashPile_Large" };
		NetworkObject networkObject = null;
		string[] array = obj;
		foreach (string text in array)
		{
			networkObject = Resources.Load<NetworkObject>(text);
			if (networkObject != null)
			{
				Debug.Log("[Tutorial] TrashPile 프리팹 로드 성공: " + text);
				break;
			}
		}
		if (networkObject == null)
		{
			Debug.LogError("[Tutorial] TrashPile 프리팹 로드 실패! Resources 폴더의 정확한 이름을 candidateNames에 추가해주세요.");
			return;
		}
		Vector3[] array2 = new Vector3[4]
		{
			_tutorialBasePos + new Vector3(0f - trashSpawnOffsetX, trashSpawnOffsetY, 0f),
			_tutorialBasePos + new Vector3(trashSpawnOffsetX, trashSpawnOffsetY, 0f),
			_tutorialBasePos + new Vector3((0f - trashSpawnOffsetX) * 2f, trashSpawnOffsetY, 0f),
			_tutorialBasePos + new Vector3(trashSpawnOffsetX * 2f, trashSpawnOffsetY, 0f)
		};
		foreach (Vector3 vector in array2)
		{
			NetworkObject networkObject2 = _tutorialRunner.Spawn(networkObject, vector, Quaternion.identity);
			if (networkObject2 != null)
			{
				if (!networkObject2.gameObject.CompareTag("Trash"))
				{
					networkObject2.gameObject.tag = "Trash";
				}
				Debug.Log($"[Tutorial] 쓰레기 더미 스폰: {vector}");
			}
		}
	}

	private void SpawnTutorialNPC()
	{
		if (_tutorialRunner == null)
		{
			return;
		}
		GameObject gameObject = GameObject.Find("NPCSpawnPoint");
		Vector3 vector = ((gameObject != null) ? gameObject.transform.position : (_tutorialBasePos + new Vector3(trashSpawnOffsetX * 3f, trashSpawnOffsetY, 0f)));
		string[] array = new string[4] { "NPC", "TraderNPC", "NPC_Trader", "ShopNPC" };
		NetworkObject networkObject = null;
		string[] array2 = array;
		foreach (string text in array2)
		{
			networkObject = Resources.Load<NetworkObject>(text);
			if (networkObject != null)
			{
				Debug.Log("[Tutorial] NPC(NetworkObject) 프리팹 로드 성공: " + text);
				NetworkObject networkObject2 = _tutorialRunner.Spawn(networkObject, vector, Quaternion.identity);
				if (networkObject2 != null && !networkObject2.gameObject.CompareTag("NPC"))
				{
					networkObject2.gameObject.tag = "NPC";
				}
				Debug.Log($"[Tutorial] NPC 스폰: {vector}");
				return;
			}
		}
		array2 = array;
		foreach (string text2 in array2)
		{
			GameObject gameObject2 = Resources.Load<GameObject>(text2);
			if (gameObject2 != null)
			{
				Debug.Log("[Tutorial] NPC(GameObject) 프리팹 로드 성공: " + text2);
				GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject2, vector, Quaternion.identity);
				if (!gameObject3.CompareTag("NPC"))
				{
					gameObject3.tag = "NPC";
				}
				Debug.Log($"[Tutorial] NPC 스폰: {vector}");
				return;
			}
		}
		Debug.LogError("[Tutorial] NPC 프리팹 로드 실패! Resources 폴더의 정확한 이름을 candidateNames에 추가해주세요.");
	}

	private void EnsureInventoryUI()
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null)
		{
			canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
		}
		if (canvas == null)
		{
			Debug.LogError("[Tutorial] EnsureInventoryUI: Canvas를 찾을 수 없습니다.");
			return;
		}
		if (canvas.GetComponentInChildren<InventoryUI>() != null)
		{
			Debug.Log("[Tutorial] InventoryUI 이미 존재함");
			return;
		}
		canvas.gameObject.AddComponent<InventoryUI>();
		Debug.Log("[Tutorial] InventoryUI 추가 완료 (I키 인벤토리 사용 가능)");
	}

	private async Task ShutdownTutorialSession()
	{
		if (!(_tutorialRunner == null))
		{
			if (_tutorialRunner.IsRunning)
			{
				await _tutorialRunner.Shutdown();
			}
			if (_tutorialRunner != null)
			{
				UnityEngine.Object.Destroy(_tutorialRunner.gameObject);
			}
			_tutorialRunner = null;
		}
	}

	private void SetPanelVisible(bool visible)
	{
		if (tutorialPanel != null)
		{
			tutorialPanel.SetActive(visible);
		}
	}

	private void UpdateHighlight(GameObject target)
	{
		if (highlightOverlay == null)
		{
			return;
		}
		if (target == null)
		{
			highlightOverlay.gameObject.SetActive(value: false);
			return;
		}
		highlightOverlay.gameObject.SetActive(value: true);
		RectTransform component = target.GetComponent<RectTransform>();
		if (component != null)
		{
			highlightOverlay.rectTransform.position = component.position;
			highlightOverlay.rectTransform.sizeDelta = component.sizeDelta + new Vector2(20f, 20f);
		}
	}

	private void UpdatePointer(Transform target)
	{
		if (pointerArrow == null)
		{
			return;
		}
		if (target == null)
		{
			pointerArrow.gameObject.SetActive(value: false);
			return;
		}
		pointerArrow.gameObject.SetActive(value: true);
		Camera main = Camera.main;
		if (!(main == null))
		{
			Vector3 vector = main.WorldToScreenPoint(target.position);
			Canvas canvas = tutorialPanel?.GetComponentInParent<Canvas>();
			if (!(canvas == null))
			{
				RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), vector, (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : main, out var localPoint);
				pointerArrow.anchoredPosition = localPoint + pointerOffset;
			}
		}
	}

	private PlayerMovement FindLocalPlayer()
	{
		PlayerMovement[] array = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
		foreach (PlayerMovement playerMovement in array)
		{
			if (playerMovement.HasInputAuthority)
			{
				return playerMovement;
			}
		}
		return null;
	}

	private void LockPlayerMovement(bool lockMovement)
	{
		PlayerMovement playerMovement = FindLocalPlayer();
		if (playerMovement != null)
		{
			playerMovement.IsMovementLocked = lockMovement;
		}
	}

	public void JumpToStep(int index)
	{
		if (!_isRunning)
		{
			StartTutorial();
		}
		StopAutoCoroutine();
		_currentStep = Mathf.Clamp(index, 0, steps.Count - 1);
		ShowStep(_currentStep);
	}

	public void ResetAndRestart()
	{
		PlayerPrefs.DeleteKey("TutorialDone");
		StartTutorial();
	}

	public void OnDecoItemPlaced()
	{
		if (_isRunning && _currentStep >= 0 && _currentStep < steps.Count && steps[_currentStep].autoAdvance == AutoAdvanceType.OnDecoItemPlaced)
		{
			NextStep();
		}
	}
}
