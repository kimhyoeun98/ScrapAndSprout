using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Fusion.Addons.Physics;

/// <summary>
/// Scrap &amp; Sprout — 인게임 튜토리얼 시스템
///
/// [전체 흐름]
/// TrashZone 입장 → 이동/채굴/판매/텔레포터 안내
/// → DecoScene 입장 → 꾸미기/세이프존 안내
///
/// [씬 설정]
/// Canvas 하위에 "TutorialPanel" 오브젝트를 만들고
/// 이 스크립트를 아무 빈 오브젝트에 붙인 뒤 Inspector에서 연결하세요.
///
/// [사용법]
/// TutorialManager.Instance.StartTutorial();  // 게임 시작 시 자동 호출
/// TutorialManager.Instance.NextStep();       // 다음 버튼 OnClick에 연결
/// TutorialManager.Instance.SkipTutorial();   // 건너뛰기 버튼 OnClick에 연결
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────

    public static TutorialManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────
    //  튜토리얼 단계 정의
    // ─────────────────────────────────────────

    [System.Serializable]
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
        public bool lockMovement = false;

        [Tooltip("자동으로 다음 단계로 넘어갈 조건 타입")]
        public AutoAdvanceType autoAdvance = AutoAdvanceType.None;

        [Tooltip("자동 진행이 None이 아닐 때 대기 시간 (초)")]
        public float autoDelay = 0f;
    }

    public enum AutoAdvanceType
    {
        None,           // 플레이어가 직접 '다음' 눌러야 함
        Timer,          // autoDelay초 후 자동 진행
        OnMove,         // 플레이어가 움직이면 진행
        OnEKeyPress,    // E키 누르면 진행
        OnFKeyPress,    // F키(상점) 누르면 진행
        OnSceneChange,  // 씬이 바뀌면 자동 진행
        OnDecoItemPlaced, // 꾸미기 아이템을 배치하면 자동 진행
    }

    // ─────────────────────────────────────────
    //  단계 목록 (Inspector에서 편집하거나 코드로 자동 생성)
    // ─────────────────────────────────────────

    [Header("── 튜토리얼 단계 ──")]
    [Tooltip("비워두면 InitDefaultSteps()로 자동 생성됩니다")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    [Header("── 튜토리얼 오프라인 세션 (플레이어/봇/쓰레기) ──")]
    [Tooltip("쓰레기 더미 X축 간격 (플레이어 위치 기준 좌우)")]
    public float trashSpawnOffsetX = 1.5f;
    [Tooltip("쓰레기 더미 Y축 보정값 (프리팹 피벗 차이로 인해 떠 보이면 음수로 조정)")]
    public float trashSpawnOffsetY = -9f;

    // ─────────────────────────────────────────
    //  UI 연결
    // ─────────────────────────────────────────

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

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private int _currentStep = 0;
    private bool _isRunning = false;

    // ─────────────────────────────────────────
    //  튜토리얼 중 구매 제한 (NPCInteraction.BuyDeco에서 참조)
    // ─────────────────────────────────────────

    /// <summary>튜토리얼이 진행 중인지 (NPCInteraction에서 구매 제한 체크용)</summary>
    public static bool IsTutorialActive { get; private set; } = false;

    /// <summary>튜토리얼 중 구매를 허용할 꾸미기 아이템 이름</summary>
    public static string AllowedPurchaseItem = "나무풍 상자";
    private Coroutine _autoCoroutine;
    private Vector3 _lastPlayerPos;

    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    async void Start()
    {
        // Inspector에 단계가 없으면 기본 단계 자동 생성
        if (steps == null || steps.Count == 0)
            InitDefaultSteps();

        // 버튼 이벤트 연결
        if (nextButton != null) nextButton.onClick.AddListener(NextStep);
        if (skipButton != null) skipButton.onClick.AddListener(SkipTutorial);

        // 패널 숨김
        SetPanelVisible(false);

        // 플레이어 1명 + AI 봇 1명을 오프라인(Single 모드)으로 스폰
        // (PhotonManager는 사용하지 않고, 이 씬 전용 NetworkRunner를 직접 띄운다)
        bool ok = await StartTutorialSession();
        if (!ok)
            Debug.LogWarning("[Tutorial] 세션 시작 실패 — 플레이어/봇 없이 진행됩니다.");

        // TutorialScene은 로비에서 "튜토리얼 보기" 버튼을 눌러야 들어오는
        // 전용 씬이므로, 진입 즉시 자동으로 튜토리얼을 시작한다.
        StartTutorial();
    }

    // ─────────────────────────────────────────
    //  기본 단계 자동 생성
    //  (Inspector에서 TutorialStep을 직접 설정하면 이 메서드는 무시됩니다)
    // ─────────────────────────────────────────

    void InitDefaultSteps()
    {
        steps = new List<TutorialStep>
        {
            // ── TrashZone ──────────────────────────────
            new TutorialStep
            {
                title    = "🌍 Scrap & Sprout에 오신 걸 환영해요!",
                body     = "인류가 사라진 지구.\n로봇인 당신이 쓰레기를 줍고 나만의 공간을 꾸미는 게임이에요.\n먼저 기본 조작을 배워볼게요!",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.Timer,
                autoDelay    = 1.5f,
            },
            new TutorialStep
            {
                title    = "🚶 이동하기",
                body     = "방향키(↑ ↓ ← →)로 이동할 수 있어요.\n한번 움직여 보세요!",
                lockMovement = false,
                autoAdvance  = AutoAdvanceType.OnMove,
            },
            new TutorialStep
            {
                title    = "⛏ 쓰레기 더미 채굴",
                body     = "노란색 쓰레기 더미에 가까이 다가가면\n'E키를 눌러 채굴' 안내가 떠요.\n\n【E키】를 누르면 채굴 미니게임이 시작돼요!",
                lockMovement = false,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "🎮 채굴 미니게임",
                body     = "화면에 방향키 순서가 표시돼요.\n순서대로 정확하게 입력하면 성공!\n\n✓ 성공 → 쓰레기 아이템 획득\n✗ 실패 또는 시간 초과 → 배터리 -10%",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "🔋 배터리 시스템",
                body     = "채굴할 때마다 배터리가 소모돼요.\n배터리가 0%가 되면 30초간 기절!\n\n팀원이 기절한 동료를 업고 이동할 수 있어요.",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "💰 NPC에게 쓰레기 판매",
                body     = "수거한 쓰레기는 세이프존 NPC에게 팔 수 있어요.\n\nNPC 앞에서 【F키】를 누르면 상점이 열려요.\n'전체 판매' 버튼으로 한 번에 팔 수 있답니다!",
                lockMovement = false,
                autoAdvance  = AutoAdvanceType.OnFKeyPress,
            },
            new TutorialStep
            {
                title    = "🌦 날씨 시스템",
                body     = "게임 중 날씨가 변해요.\n\n☀ 맑음 → 기본 난이도\n🌧 산성비 → 채굴 미니게임 +2단계 어려워짐\n🌫 황사 → 이동 속도 저하\n\n나무를 많이 심을수록 날씨가 안정돼요!",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "🌀 텔레포터로 세이프 존 이동",
                body     = "쓰레기 존과 세이프 존은 텔레포터로만 이동해요.\n\n텔레포터 앞에서 【↓ 방향키】를 누르면 이동!\n\n골드가 어느 정도 모이면 세이프 존으로 가세요.",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            // ── SafeZone ─────────────────────────────
            new TutorialStep
            {
                title    = "🏠 세이프 존 도착!",
                body     = "여기는 세이프 존이에요.\nNPC에게 쓰레기를 팔고 골드를 모을 수 있어요.\n\n골드가 생기면 꾸미기 존으로 이동해봐요!",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.OnSceneChange,
            },
            // ── DecoScene ────────────────────────────
            new TutorialStep
            {
                title    = "🏡 꾸미기 존 도착!",
                body     = "여기가 꾸미기 존이에요.\n모은 골드로 아이템을 구매하고\n원하는 곳에 배치해 팀 점수를 올려보세요!",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "🛒 꾸미기 아이템 구매",
                body     = "상점에서 나무, 상자, 의자 등\n다양한 아이템을 살 수 있어요.\n\n나무풍 테마로 세트를 맞추면\n5% 보너스 점수가 추가돼요! ★\n\n아이템을 하나 구매해서 배치해보세요!",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.OnDecoItemPlaced,
            },
            new TutorialStep
            {
                title    = "🌱 씨앗 심기",
                body     = "인벤토리에 '나무'가 있을 때\n【Q키】를 눌러 씨앗 심기 모드로 전환!\n\n원하는 위치를 클릭하면 나무가 심어져요.\n나무가 많을수록 날씨가 좋아져요 🌳",
                lockMovement = true,
                autoAdvance  = AutoAdvanceType.None,
            },
            new TutorialStep
            {
                title    = "✅ 튜토리얼 완료!",
                body     = "기본 조작을 모두 배웠어요!\n\n핵심 루프:\n⛏ 채굴 → 💰 판매 → 🌀 이동 → 🏡 꾸미기\n\n자, 이제 쓰레기를 줍고\n나만의 공간을 꾸며볼까요?",
                lockMovement = false,
                autoAdvance  = AutoAdvanceType.None,
            },
        };
    }

    // ─────────────────────────────────────────
    //  외부 호출 API
    // ─────────────────────────────────────────

    /// <summary>튜토리얼을 처음부터 시작합니다.</summary>
    public void StartTutorial()
    {
        _currentStep = 0;
        _isRunning = true;
        IsTutorialActive = true;
        SetPanelVisible(true);
        ShowStep(_currentStep);
        Debug.Log("[Tutorial] 시작!");
    }

    /// <summary>'다음' 버튼 또는 조건 달성 시 다음 단계로 이동합니다.</summary>
    public void NextStep()
    {
        if (!_isRunning) return;
        StopAutoCoroutine();

        _currentStep++;

        if (_currentStep >= steps.Count)
        {
            EndTutorial();
            return;
        }

        ShowStep(_currentStep);
    }

    /// <summary>튜토리얼을 건너뜁니다.</summary>
    public void SkipTutorial()
    {
        StopAutoCoroutine();
        EndTutorial();
        Debug.Log("[Tutorial] 건너뜀");
    }

    // ─────────────────────────────────────────
    //  단계 표시
    // ─────────────────────────────────────────

    void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Count) return;

        TutorialStep step = steps[index];

        // 텍스트 업데이트
        if (titleText != null) titleText.text = step.title;
        if (bodyText != null) bodyText.text = step.body;
        if (stepCountText != null)
            stepCountText.text = $"{index + 1} / {steps.Count}";

        // 이동 잠금
        LockPlayerMovement(step.lockMovement);

        // 하이라이트 오버레이
        UpdateHighlight(step.highlightTarget);

        // 화살표 포인터
        UpdatePointer(step.pointerTarget);

        // 다음 버튼 텍스트 (마지막 단계는 '완료')
        if (nextButton != null)
        {
            var label = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = (index == steps.Count - 1) ? "완료!" : "다음 >";
        }

        // 자동 진행 설정
        SetupAutoAdvance(step);

        Debug.Log($"[Tutorial] Step {index + 1}: {step.title}");
    }

    // ─────────────────────────────────────────
    //  자동 진행 (AutoAdvanceType)
    // ─────────────────────────────────────────

    void SetupAutoAdvance(TutorialStep step)
    {
        StopAutoCoroutine();

        switch (step.autoAdvance)
        {
            case AutoAdvanceType.Timer:
                if (step.autoDelay > 0f)
                    _autoCoroutine = StartCoroutine(AutoAfterDelay(step.autoDelay));
                break;

            case AutoAdvanceType.OnMove:
                // 현재 플레이어 위치 기록 → Update에서 감지
                var pm = FindLocalPlayer();
                if (pm != null) _lastPlayerPos = pm.transform.position;
                _autoCoroutine = StartCoroutine(WaitForMove());
                break;

            case AutoAdvanceType.OnEKeyPress:
                _autoCoroutine = StartCoroutine(WaitForKey(KeyCode.E));
                break;

            case AutoAdvanceType.OnFKeyPress:
                _autoCoroutine = StartCoroutine(WaitForKey(KeyCode.F));
                break;

            case AutoAdvanceType.OnSceneChange:
                // OnSceneLoaded에서 처리 (씬 전환 감지)
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                break;

            case AutoAdvanceType.None:
            default:
                break;
        }
    }

    IEnumerator AutoAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        NextStep();
    }

    IEnumerator WaitForMove()
    {
        var pm = FindLocalPlayer();
        if (pm == null) yield break;

        while (pm != null && Vector3.Distance(pm.transform.position, _lastPlayerPos) < 0.3f)
            yield return null;

        // 대기 중 플레이어 오브젝트가 파괴된 경우 (씬 전환/튜토리얼 종료 등) 진행하지 않음
        if (pm == null) yield break;

        NextStep();
    }

    IEnumerator WaitForKey(KeyCode key)
    {
        // 현재 프레임 입력 무시 (즉시 진행 방지)
        yield return null;
        while (!Input.GetKeyDown(key))
            yield return null;
        NextStep();
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                       UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        // 새 씬에서도 패널이 살아있는지 확인 후 진행
        StartCoroutine(DelayedNextStep(0.5f));
    }

    IEnumerator DelayedNextStep(float delay)
    {
        yield return new WaitForSeconds(delay);
        // DontDestroyOnLoad 패널 재확인
        if (tutorialPanel != null) SetPanelVisible(true);
        NextStep();
    }

    void StopAutoCoroutine()
    {
        if (_autoCoroutine != null)
        {
            StopCoroutine(_autoCoroutine);
            _autoCoroutine = null;
        }
        // 씬 이벤트 정리
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ─────────────────────────────────────────
    //  튜토리얼 종료
    // ─────────────────────────────────────────

    async void EndTutorial()
    {
        _isRunning = false;
        IsTutorialActive = false;
        StopAutoCoroutine();
        LockPlayerMovement(false);
        SetPanelVisible(false);
        UpdateHighlight(null);
        UpdatePointer(null);

        // 완료 기록 저장 → 다음 실행 시 튜토리얼 건너뜀
        PlayerPrefs.SetInt("TutorialDone", 1);
        PlayerPrefs.Save();

        UIManager.Instance?.ShowStatusMessage("튜토리얼 완료! 즐겁게 플레이하세요 🌱", 3f);
        Debug.Log("[Tutorial] 완료!");

        // 튜토리얼용 오프라인 세션(플레이어+봇) 정리
        await ShutdownTutorialSession();

        // 로비로 복귀 (씬 이름은 실제 빌드 세팅에 맞게 수정)
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");

        // 다음에 다시 TutorialScene에 들어왔을 때 새 인스턴스가
        // 정상 생성되도록 현재 싱글톤을 파괴
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    //  튜토리얼 전용 오프라인 세션 (PhotonManager 미사용)
    //
    //  TutorialScene 안에서만 쓰는 임시 NetworkRunner를 직접 생성해
    //  GameMode.Single로 시작 → 플레이어 1명 + AI 봇 1명을 스폰한다.
    //  로비의 PhotonManager 싱글톤과는 완전히 별개로 동작한다.
    // ─────────────────────────────────────────

    private NetworkRunner _tutorialRunner;
    private Vector3 _tutorialBasePos;
    private TutorialInputProvider _tutorialInput;

    void Update()
    {
        // 점프/수거/텔레포트 키 입력을 매 프레임 버퍼링 (FixedUpdateNetwork와의 빈도 차이로 인한 입력 누락 방지)
        _tutorialInput?.PollKeys();
    }

    private async Task<bool> StartTutorialSession()
    {
        try
        {
            Application.runInBackground = true;

            var runnerObj = new GameObject("TutorialNetworkRunner");
            _tutorialRunner = runnerObj.AddComponent<NetworkRunner>();
            _tutorialRunner.ProvideInput = true;
            runnerObj.AddComponent<RunnerSimulatePhysics2D>();

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Single,
                PlayerCount = 1,
            };

            var result = await _tutorialRunner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Tutorial] Fusion 세션 시작 실패: {result.ShutdownReason} - {result.ErrorMessage}");
                Destroy(runnerObj);
                _tutorialRunner = null;
                return false;
            }

            Debug.Log("[Tutorial] ✅ 오프라인 세션 시작 성공 (GameMode.Single)");

            // 이동/상호작용 입력을 위해 자체 INetworkRunnerCallbacks 등록
            _tutorialInput = new TutorialInputProvider();
            _tutorialRunner.AddCallbacks(_tutorialInput);

            SpawnTutorialPlayerAndBot();
            SpawnTutorialTrash();
            SpawnTutorialNPC();
            EnsureInventoryUI();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Tutorial] Fusion 세션 예외: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// SpawnPoint 태그 오브젝트 2개를 사용해
    /// 0번 = 플레이어(입력 권한 O), 1번 = AI 봇을 스폰한다.
    /// SpawnPoint가 없으면 임시 좌표를 사용한다.
    /// </summary>
    private void SpawnTutorialPlayerAndBot()
    {
        if (_tutorialRunner == null) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        // 봇과 플레이어 스폰 위치를 서로 바꿈 (1번 = 플레이어, 0번 = 봇)
        Vector3 playerPos = spawnPoints.Length > 1
            ? spawnPoints[1].transform.position
            : new Vector3(3f, 0f, 0f);
        playerPos.z = -1.2f;
        _tutorialBasePos = playerPos; // 트래시 배치 기준점으로 사용

        // 봇 스폰 위치를 SpawnPoint[0](포탈과 겹침) 대신
        // 플레이어 바로 옆 상대 위치로 배치 → 포탈을 막지 않도록 함
        Vector3 botPos = playerPos + new Vector3(-0.8f, 0f, 0f);

        // ── 플레이어(알파) — 입력 권한 부여 ──
        var playerPrefab = Resources.Load<NetworkObject>("PlayerAlpha");
        if (playerPrefab == null)
        {
            Debug.LogError("[Tutorial] ❌ PlayerAlpha 프리팹 로드 실패! (Resources 폴더 확인)");
        }
        else
        {
            var spawnedPlayer = _tutorialRunner.Spawn(playerPrefab, playerPos, Quaternion.identity, _tutorialRunner.LocalPlayer);
            if (spawnedPlayer != null)
                Debug.Log($"[Tutorial] ✅ 플레이어 스폰 완료 at {playerPos}");
        }

        // ── AI 봇(베타) — 입력 권한 없음 → AIBotController가 동작 ──
        var botPrefab = Resources.Load<NetworkObject>("PlayerBeta");
        if (botPrefab == null)
        {
            Debug.LogError("[Tutorial] ❌ PlayerBeta 프리팹 로드 실패! (Resources 폴더 확인)");
            return;
        }

        var spawnedBot = _tutorialRunner.Spawn(botPrefab, botPos, Quaternion.identity);
        if (spawnedBot != null)
        {
            Debug.Log($"[Tutorial] ✅ AI 봇 스폰 완료 at {botPos}");
            if (spawnedBot.GetComponent<AIBotController>() == null)
                spawnedBot.gameObject.AddComponent<AIBotController>();
        }
    }

    /// <summary>
    /// 튜토리얼 진행에 필요한 고정 쓰레기 더미 2개를 생성한다.
    /// TrashPile은 NetworkBehaviour(NetworkObject)이므로 runner.Spawn() 사용.
    /// 프리팹은 Resources 폴더 안에 있어야 한다. (정확한 이름은 PCGManager.cs 확인)
    /// </summary>
    private void SpawnTutorialTrash()
    {
        if (_tutorialRunner == null) return;

        // TODO: 실제 프로젝트에서 PCG가 사용하는 쓰레기 더미 프리팹 이름으로 수정
        string[] candidateNames = { "TrashPile", "TrashPile_Small", "SmallTrashPile", "TrashPile_Large" };

        NetworkObject trashPrefab = null;
        foreach (var name in candidateNames)
        {
            trashPrefab = Resources.Load<NetworkObject>(name);
            if (trashPrefab != null)
            {
                Debug.Log($"[Tutorial] TrashPile 프리팹 로드 성공: {name}");
                break;
            }
        }

        if (trashPrefab == null)
        {
            Debug.LogError("[Tutorial] ❌ TrashPile 프리팹 로드 실패! Resources 폴더의 정확한 이름을 candidateNames에 추가해주세요.");
            return;
        }

        Vector3[] positions =
        {
            _tutorialBasePos + new Vector3(-trashSpawnOffsetX, trashSpawnOffsetY, 0f),
            _tutorialBasePos + new Vector3( trashSpawnOffsetX, trashSpawnOffsetY, 0f),
            _tutorialBasePos + new Vector3(-trashSpawnOffsetX * 2f, trashSpawnOffsetY, 0f),
            _tutorialBasePos + new Vector3( trashSpawnOffsetX * 2f, trashSpawnOffsetY, 0f),
        };

        foreach (var pos in positions)
        {
            var spawned = _tutorialRunner.Spawn(trashPrefab, pos, Quaternion.identity);
            if (spawned != null)
            {
                if (!spawned.gameObject.CompareTag("Trash"))
                    spawned.gameObject.tag = "Trash";

                Debug.Log($"[Tutorial] ✅ 쓰레기 더미 스폰: {pos}");
            }
        }
    }

    /// <summary>
    /// TutorialScene의 "NPCSpawnPoint" 태그 위치에 NPC를 스폰한다.
    /// (AIBotController의 SellTrash 단계가 "NPC" 태그 오브젝트를 찾으므로 필요)
    /// 정확한 프리팹 이름은 GameManager/PCGManager가 NPC를 스폰하는 코드를 참고해서
    /// candidateNames에 맞는 이름으로 추가해주세요.
    /// </summary>
    private void SpawnTutorialNPC()
    {
        if (_tutorialRunner == null) return;

        // "NPCSpawnPoint"는 태그가 아니라 오브젝트 이름이므로 이름으로 검색
        GameObject spawnPointObj = GameObject.Find("NPCSpawnPoint");
        Vector3 npcPos = spawnPointObj != null
            ? spawnPointObj.transform.position
            : _tutorialBasePos + new Vector3(trashSpawnOffsetX * 3f, trashSpawnOffsetY, 0f);

        // TODO: 실제 프로젝트에서 사용하는 NPC 프리팹 이름으로 수정
        string[] candidateNames = { "NPC", "TraderNPC", "NPC_Trader", "ShopNPC" };

        // NPC가 NetworkObject(NetworkBehaviour)인 경우
        NetworkObject npcNetPrefab = null;
        foreach (var name in candidateNames)
        {
            npcNetPrefab = Resources.Load<NetworkObject>(name);
            if (npcNetPrefab != null)
            {
                Debug.Log($"[Tutorial] NPC(NetworkObject) 프리팹 로드 성공: {name}");
                var spawned = _tutorialRunner.Spawn(npcNetPrefab, npcPos, Quaternion.identity);
                if (spawned != null && !spawned.gameObject.CompareTag("NPC"))
                    spawned.gameObject.tag = "NPC";
                Debug.Log($"[Tutorial] ✅ NPC 스폰: {npcPos}");
                return;
            }
        }

        // NetworkObject가 아닌 일반 프리팹인 경우 (Instantiate)
        foreach (var name in candidateNames)
        {
            var npcPrefab = Resources.Load<GameObject>(name);
            if (npcPrefab != null)
            {
                Debug.Log($"[Tutorial] NPC(GameObject) 프리팹 로드 성공: {name}");
                var npc = Instantiate(npcPrefab, npcPos, Quaternion.identity);
                if (!npc.CompareTag("NPC"))
                    npc.tag = "NPC";
                Debug.Log($"[Tutorial] ✅ NPC 스폰: {npcPos}");
                return;
            }
        }

        Debug.LogError("[Tutorial] ❌ NPC 프리팹 로드 실패! Resources 폴더의 정확한 이름을 candidateNames에 추가해주세요.");
    }

    /// <summary>
    /// TutorialScene의 Canvas에 InventoryUI 컴포넌트가 없으면 추가한다.
    /// (다른 게임 씬과 달리 TutorialScene Canvas에는 기본적으로 없음 → I키 인벤토리 안 열리던 문제)
    /// </summary>
    private void EnsureInventoryUI()
    {
        // TutorialManager(및 그 부모 Canvas)는 DontDestroyOnLoad라서
        // gameObject.scene.name이 "DontDestroyOnLoad"가 됨 → 씬 이름 비교 대신
        // 이 TutorialManager가 속한 Canvas를 직접 사용한다.
        Canvas targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogError("[Tutorial] ❌ EnsureInventoryUI: Canvas를 찾을 수 없습니다.");
            return;
        }

        if (targetCanvas.GetComponentInChildren<InventoryUI>() != null)
        {
            Debug.Log("[Tutorial] InventoryUI 이미 존재함");
            return;
        }

        targetCanvas.gameObject.AddComponent<InventoryUI>();
        Debug.Log("[Tutorial] ✅ InventoryUI 추가 완료 (I키 인벤토리 사용 가능)");
    }

    /// <summary>이 씬에서 만든 임시 NetworkRunner를 종료하고 정리한다.</summary>
    private async Task ShutdownTutorialSession()
    {
        if (_tutorialRunner == null) return;

        if (_tutorialRunner.IsRunning)
            await _tutorialRunner.Shutdown();

        if (_tutorialRunner != null)
            Destroy(_tutorialRunner.gameObject);

        _tutorialRunner = null;
    }

    // ─────────────────────────────────────────
    //  UI 헬퍼
    // ─────────────────────────────────────────

    void SetPanelVisible(bool visible)
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(visible);
    }

    /// <summary>
    /// 하이라이트 오버레이: 지정 오브젝트 주변만 밝게, 나머지 어둡게.
    /// highlightTarget이 null이면 오버레이 숨김.
    /// </summary>
    void UpdateHighlight(GameObject target)
    {
        if (highlightOverlay == null) return;

        if (target == null)
        {
            highlightOverlay.gameObject.SetActive(false);
            return;
        }

        highlightOverlay.gameObject.SetActive(true);

        // RectTransform 기반 위치 동기화 (UI 오브젝트일 때)
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            highlightOverlay.rectTransform.position = targetRect.position;
            highlightOverlay.rectTransform.sizeDelta = targetRect.sizeDelta + new Vector2(20f, 20f);
        }
    }

    /// <summary>
    /// 화살표 포인터를 월드 오브젝트 위에 표시합니다.
    /// </summary>
    void UpdatePointer(Transform target)
    {
        if (pointerArrow == null) return;

        if (target == null)
        {
            pointerArrow.gameObject.SetActive(false);
            return;
        }

        pointerArrow.gameObject.SetActive(true);

        // 월드 → 스크린 → UI 좌표 변환
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(target.position);
        Canvas canvas = tutorialPanel?.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 localPoint
        );

        pointerArrow.anchoredPosition = localPoint + pointerOffset;
    }

    /// <summary>내 캐릭터(HasInputAuthority)를 찾아 반환합니다.</summary>
    PlayerMovement FindLocalPlayer()
    {
        foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            if (pm.HasInputAuthority) return pm;
        return null;
    }

    void LockPlayerMovement(bool lockMovement)
    {
        var pm = FindLocalPlayer();
        if (pm != null) pm.IsMovementLocked = lockMovement;
    }

    // ─────────────────────────────────────────
    //  외부에서 특정 단계로 점프 (선택 사항)
    //  예: NPC가 "채굴을 배워볼까요?" 대화 후 호출
    // ─────────────────────────────────────────

    /// <summary>특정 인덱스 단계로 즉시 이동합니다.</summary>
    public void JumpToStep(int index)
    {
        if (!_isRunning) StartTutorial();
        StopAutoCoroutine();
        _currentStep = Mathf.Clamp(index, 0, steps.Count - 1);
        ShowStep(_currentStep);
    }

    // ─────────────────────────────────────────
    //  튜토리얼 재시작 (설정에서 '튜토리얼 다시 보기' 버튼)
    // ─────────────────────────────────────────

    /// <summary>저장된 완료 기록을 지우고 튜토리얼을 처음부터 다시 시작합니다.</summary>
    public void ResetAndRestart()
    {
        PlayerPrefs.DeleteKey("TutorialDone");
        StartTutorial();
    }

    // ─────────────────────────────────────────
    //  꾸미기 아이템 배치 콜백 (DecorationPlacer에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 꾸미기 아이템을 배치했을 때 호출됩니다.
    /// 현재 단계의 autoAdvance가 OnDecoItemPlaced일 때만 다음 단계로 진행합니다.
    /// </summary>
    public void OnDecoItemPlaced()
    {
        if (!_isRunning) return;
        if (_currentStep < 0 || _currentStep >= steps.Count) return;

        if (steps[_currentStep].autoAdvance == AutoAdvanceType.OnDecoItemPlaced)
            NextStep();
    }
}

// ─────────────────────────────────────────
//  튜토리얼 전용 입력 제공자
//
//  TutorialManager가 만든 _tutorialRunner에 등록되어
//  PlayerMovement.GetInput(out NetworkInputData)에 값을 채워준다.
//  PhotonManager.OnInput()과 동일한 키 매핑을 사용하지만
//  PhotonManager 인스턴스/상태와는 완전히 독립적으로 동작한다.
// ─────────────────────────────────────────
public class TutorialInputProvider : INetworkRunnerCallbacks
{
    // Update()와 FixedUpdateNetwork() 빈도 차이로 인해
    // GetKeyDown 한 프레임짜리 입력이 씹히지 않도록 "눌림 대기" 플래그로 버퍼링한다.
    private bool _jumpPressed;
    private bool _interactPressed;
    private bool _teleportPressed;

    /// <summary>TutorialManager.Update()에서 매 프레임 호출 — 키 입력을 버퍼에 누적</summary>
    public void PollKeys()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) _jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.E)) _interactPressed = true;
        if (Input.GetKeyDown(KeyCode.DownArrow)) _teleportPressed = true;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
        data.direction = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);

        // 버퍼에 쌓인 입력을 소비(읽고 리셋) — 한 번만 전달되도록
        data.jump = _jumpPressed;
        data.interact = _interactPressed;
        data.teleport = _teleportPressed;

        _jumpPressed = false;
        _interactPressed = false;
        _teleportPressed = false;

        input.Set(data);
    }

    // ── 아래는 튜토리얼(GameMode.Single)에서 사용하지 않는 콜백들 (빈 구현) ──
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}