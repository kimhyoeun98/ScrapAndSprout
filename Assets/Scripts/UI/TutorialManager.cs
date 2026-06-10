using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  인게임 튜토리얼 매니저 (TutorialManager.cs)
/// ═══════════════════════════════════════════════════════
///
/// [튜토리얼 흐름]
///   STEP 0 : 환영 메시지 (자동)
///   STEP 1 : 이동          (WASD)
///   STEP 2 : 쓰레기 수거   (E키)
///   STEP 3 : 인벤토리 확인 (I키 열기 → 닫기)
///   STEP 4 : 판매          (NPC 접근 → F키 → 판매버튼)
///   STEP 5 : 꾸미기 아이템 구매 (NPC 상점에서 나무/가구 구매)
///   STEP 6 : 꾸미기 배치   (세이프 존에서 F키 → 배치버튼)
///   STEP 7 : 배터리 구매   (NPC 상점에서 배터리 구매)
///   STEP 8 : 배터리 충전   (B키)
///   STEP 9 : 완료
///
/// [부착 위치] 씬의 빈 GameObject (TutorialManager)에 부착
/// ═══════════════════════════════════════════════════════
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────
    public static TutorialManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    //  튜토리얼 단계 정의
    // ─────────────────────────────────────────
    public enum TutorialStep
    {
        Welcome = 0,
        Move = 1,
        PickUpTrash = 2,
        Inventory = 3,
        SellTrash = 4,
        BuyDecoItem = 5,
        PlaceDecoItem = 6,
        Complete = 7,
    }

    // ─────────────────────────────────────────
    //  인스펙터 — UI 연결
    // ─────────────────────────────────────────
    [Header("튜토리얼 UI 패널")]
    public GameObject tutorialPanel;
    public TextMeshProUGUI tutorialText;
    public TextMeshProUGUI stepCounterText;
    public TextMeshProUGUI keyHintText;
    public Button skipButton;
    public Button nextButton;

    [Header("게임 오브젝트 연결")]
    public GameObject player;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────
    private TutorialStep _currentStep = TutorialStep.Welcome;
    private TrashCollector _trashCollector;
    private PlayerMovement _playerMovement;
    private int _prevDecoItemCount = 0; // 꾸미기 아이템 보유 수 스냅샷
    private int _prevTreeCount = 0;     // 배치된 나무 수 스냅샷

    private bool _trashPickedUp = false;
    private bool _inventoryOpened = false;
    private bool _decoItemPlaced = false;

    private bool _stepCompleted = false;
    private int _prevGold = -1;

    // ─────────────────────────────────────────
    //  안내 메시지 & 키 힌트
    // ─────────────────────────────────────────
    private readonly string[] _messages = new string[]
    {
        /* 0 Welcome       */ "Scrap & Sprout에 오신 것을 환영합니다!\n기본 조작법을 안내해 드릴게요.",
        /* 1 Move          */ "[WASD] 키로 캐릭터를 이동해 보세요!",
        /* 2 PickUpTrash   */ "쓰레기 더미에 가까이 다가가\n[E] 키를 눌러 채굴 미니게임을 시작하세요!",
        /* 3 Inventory     */ "[I] 키로 인벤토리를 열어보세요.\n수거한 아이템을 확인할 수 있어요.",
        /* 4 SellTrash     */ "NPC에게 다가가 [F] 키를 누른 뒤\n[판매] 버튼으로 쓰레기를 팔아 골드를 받으세요!",
        /* 5 BuyDecoItem   */ "골드로 꾸미기 아이템을 구매해 보세요!\nNPC 상점에서 나무, 상자 등을 살 수 있어요.",
        /* 6 PlaceDecoItem */ "텔레포터로 세이프 존에 이동한 뒤\n배치 포인트 근처에서 [F] 키를 눌러\n꾸미기 아이템을 배치해 보세요!",
        /* 7 Complete      */ "튜토리얼 완료!\n이제 공간을 꾸미고 점수를 쌓아보세요!",
    };

    private readonly string[] _keyHints = new string[]
    {
        /* 0 */ "",
        /* 1 */ "W  A  S  D",
        /* 2 */ "E",
        /* 3 */ "I",
        /* 4 */ "F",
        /* 5 */ "F",
        /* 6 */ "텔레포터 → F키",
        /* 7 */ "",
    };

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        if (PlayerPrefs.GetInt("TutorialComplete", 0) == 1)
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            _currentStep = TutorialStep.Complete;
            Debug.Log("[Tutorial] 이전 완료 기록 — 건너뜁니다.");
            return;
        }

        if (player != null)
        {
            _trashCollector = player.GetComponent<TrashCollector>();
            _playerMovement = player.GetComponent<PlayerMovement>();
        }

        if (skipButton != null) skipButton.onClick.AddListener(SkipTutorial);
        if (nextButton != null) nextButton.onClick.AddListener(AdvanceStep);

        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        ShowStep(_currentStep);
    }

    void Update()
    {
        switch (_currentStep)
        {
            case TutorialStep.Move:          CheckMoveStep(); break;
            case TutorialStep.PickUpTrash:   CheckPickUpStep(); break;
            case TutorialStep.Inventory:     CheckInventoryStep(); break;
            case TutorialStep.SellTrash:     CheckSellStep(); break;
            case TutorialStep.BuyDecoItem:   CheckBuyDecoItemStep(); break;
            case TutorialStep.PlaceDecoItem: CheckPlaceDecoItemStep(); break;
        }
    }

    // ─────────────────────────────────────────
    //  단계 표시 & 전환
    // ─────────────────────────────────────────
    void ShowStep(TutorialStep step)
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(true);

        int index = (int)step;

        if (tutorialText != null) tutorialText.text = _messages[index];
        if (stepCounterText != null)
        {
            int total = (int)TutorialStep.Complete;
            stepCounterText.text = (step == TutorialStep.Complete)
                ? "" : $"{index} / {total}";
        }
        if (keyHintText != null)
        {
            bool has = !string.IsNullOrEmpty(_keyHints[index]);
            keyHintText.gameObject.SetActive(has);
            if (has) keyHintText.text = _keyHints[index];
        }

        // CanvasGroup alpha 즉시 1로
        CanvasGroup cg = tutorialPanel?.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // Next 버튼: Welcome 단계 or 단계 완료 시 표시
        _stepCompleted = false;
        if (nextButton != null)
            nextButton.gameObject.SetActive(step == TutorialStep.Welcome);

        if (step == TutorialStep.Welcome)
            StartCoroutine(AutoNextAfterDelay(2.5f));

        if (step == TutorialStep.Complete)
            StartCoroutine(HidePanelAfterDelay(3.0f));

        // 스냅샷 (변화량 감지용)
        SnapshotState();

        Debug.Log($"[Tutorial] STEP {index}: {step}");
    }

    public void AdvanceStep()
    {
        if (_currentStep == TutorialStep.Complete) return;
        // 완료 대기 중이면 다음 단계로 진행
        if (_stepCompleted || _currentStep == TutorialStep.Welcome)
        {
            _stepCompleted = false;
            _currentStep++;
            ShowStep(_currentStep);
        }
    }

    // ─────────────────────────────────────────
    //  단계 성공 처리 (Next 버튼 활성화)
    // ─────────────────────────────────────────
    void StepSuccess(string logMsg)
    {
        if (_stepCompleted) return; // 중복 호출 방지
        _stepCompleted = true;
        Debug.Log($"[Tutorial] {logMsg} → Next 버튼을 눌러주세요!");

        // 안내 메시지를 "완료! Next를 눌러주세요"로 변경
        if (tutorialText != null)
            tutorialText.text = tutorialText.text + "\n[Next] 버튼을 눌러 계속하세요!";
        // Next 버튼 활성화
        if (nextButton != null)
            nextButton.gameObject.SetActive(true);

        // 키 힌트 숨김
        if (keyHintText != null)
            keyHintText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  상태 스냅샷
    // ─────────────────────────────────────────
    void SnapshotState()
    {
        if (_trashCollector == null) return;
        _prevGold = _trashCollector.gold;
        _prevDecoItemCount = CountDecoItems();
        _prevTreeCount = WeatherManager.Instance != null
            ? (int)(WeatherManager.Instance.badWeatherBaseChance - WeatherManager.Instance.BadWeatherChance)
            : 0;
    }

    static readonly string[] _decoItemNames = { "나무", "상자", "의자", "울타리", "꽃병", "탁자", "꽃밭" };

    int CountDecoItems()
    {
        if (_trashCollector == null) return 0;
        int total = 0;
        foreach (var name in _decoItemNames)
            if (_trashCollector.inventory.ContainsKey(name))
                total += _trashCollector.inventory[name];
        return total;
    }

    // ─────────────────────────────────────────
    //  각 단계 감지 함수
    // ─────────────────────────────────────────

    // STEP 1: WASD 이동
    void CheckMoveStep()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            StepSuccess("WASD 이동 감지");
        }
    }

    // STEP 2: 쓰레기 수거 (인벤토리 변화 백업 감지)
    void CheckPickUpStep()
    {
        if (_trashCollector == null) return;
        int total = 0;
        foreach (var kv in _trashCollector.inventory)
            if (kv.Key != "Seed" && kv.Key != "Battery") total += kv.Value;

        if (total > 0 && !_trashPickedUp)
        {
            _trashPickedUp = true;
            StepSuccess("쓰레기 수거 감지");
        }
    }

    // STEP 3: 인벤토리 열기 → 닫기
    void CheckInventoryStep()
    {
        if (UIManager.Instance == null || UIManager.Instance.inventoryPanel == null) return;

        bool isOpen = UIManager.Instance.inventoryPanel.activeSelf;

        if (!_inventoryOpened && isOpen)
        {
            _inventoryOpened = true;
            Debug.Log("[Tutorial] 인벤토리 열기 감지");
        }

        if (_inventoryOpened && !isOpen)
        {
            StepSuccess("인벤토리 닫기 감지");
        }
    }

    // STEP 4: 판매 완료 (골드 증가)
    void CheckSellStep()
    {
        if (_trashCollector == null) return;
        if (_trashCollector.gold > _prevGold && _prevGold >= 0)
            StepSuccess("판매 완료 감지");
    }

    // STEP 5: 꾸미기 아이템 구매 (인벤토리에 나무/상자 등 등장)
    void CheckBuyDecoItemStep()
    {
        if (_trashCollector == null) return;
        if (CountDecoItems() > _prevDecoItemCount)
            StepSuccess("꾸미기 아이템 구매 감지");
    }

    // STEP 6: 꾸미기 배치 완료 (WeatherManager 나무 수 증가 or 인벤토리 아이템 감소)
    void CheckPlaceDecoItemStep()
    {
        int currentTreeCount = WeatherManager.Instance != null
            ? (int)(WeatherManager.Instance.badWeatherBaseChance - WeatherManager.Instance.BadWeatherChance)
            : 0;

        int currentDecoCount = _trashCollector != null ? CountDecoItems() : 0;

        bool treePlaced  = currentTreeCount > _prevTreeCount;
        bool itemRemoved = currentDecoCount < _prevDecoItemCount;

        if ((treePlaced || itemRemoved) && !_decoItemPlaced)
        {
            _decoItemPlaced = true;
            StepSuccess("꾸미기 배치 감지");
        }
    }

    // ─────────────────────────────────────────
    //  외부 이벤트 수신
    // ─────────────────────────────────────────

    /// <summary>TrashItem.TryCollect() 완료 시 호출하세요.</summary>
    public void OnTrashPickedUp()
    {
        if (_currentStep == TutorialStep.PickUpTrash && !_trashPickedUp)
        {
            _trashPickedUp = true;
            StepSuccess("외부 이벤트 - 쓰레기 수거");
        }
    }

    /// <summary>DecorationPlacer.PlaceItem() 완료 시 호출 가능.</summary>
    public void OnDecoItemPlaced()
    {
        if (_currentStep == TutorialStep.PlaceDecoItem && !_decoItemPlaced)
        {
            _decoItemPlaced = true;
            StepSuccess("외부 이벤트 - 꾸미기 배치");
        }
    }

    // ─────────────────────────────────────────
    //  건너뛰기
    // ─────────────────────────────────────────
    public void SkipTutorial()
    {
        StopAllCoroutines();
        _currentStep = TutorialStep.Complete;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("TutorialComplete", 1);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] 건너뜀");
    }

    // ─────────────────────────────────────────
    //  유틸리티
    // ─────────────────────────────────────────
    IEnumerator AutoNextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_currentStep == TutorialStep.Welcome) AdvanceStep();
    }

    IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("TutorialComplete", 1);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] 완료 & 패널 숨김");
    }

    [ContextMenu("ResetTutorial")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("TutorialComplete");
        PlayerPrefs.Save();
        StopAllCoroutines();

        _currentStep = TutorialStep.Welcome;
        _trashPickedUp = false;
        _inventoryOpened = false;
        _decoItemPlaced = false;

        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        ShowStep(_currentStep);
        Debug.Log("[Tutorial] 초기화 완료");
    }

    public TutorialStep CurrentStep => _currentStep;
    public bool IsComplete => _currentStep == TutorialStep.Complete;
}