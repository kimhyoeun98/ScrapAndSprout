using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Fusion;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameManager] 싱글톤 생성 + DontDestroyOnLoad");
        }
        else { Destroy(gameObject); return; }
    }

    [Header("── HUD UI 연결 ──")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI decorScoreText;

    // 게임 전체 타이머: realtime 기반 static (씬 전환과 무관하게 계속 흐름)
    private static float _gameStartRealtime = -1f;
    public float ElapsedTime =>
        _gameStartRealtime < 0f ? 0f : Time.realtimeSinceStartup - _gameStartRealtime;

    /// <summary>새 게임 시작을 위해 타이머/점수 정적 상태 초기화 (방 나갈 때 호출)</summary>
    public static void ResetStaticState()
    {
        _gameStartRealtime = -1f;
        _lastKnownDecorScore = 0;
        for (int i = 0; i < 4; i++) LocalPlayerScores[i] = 0;
    }

    [Networked] public int DecorScore { get; set; }

    // Networked 초기화 전에도 안전하게 점수 조회 (DecoScene 전환 시 사용)
    public int SafeDecorScore
    {
        get
        {
            if (Object == null || !Object.IsValid) return _lastKnownDecorScore;
            _lastKnownDecorScore = DecorScore;
            return _lastKnownDecorScore;
        }
    }
    private static int _lastKnownDecorScore = 0;

    // 플레이어별 기여도 (슬롯 0~3, Host가 관리)
    [Networked, Capacity(4)]
    public NetworkArray<int> PlayerDecorScores => default;

    private static readonly int[] _milestones = { 100, 500, 1000, 2000, 5000 };
    private readonly HashSet<int> _notifiedMilestones = new HashSet<int>();

    // 배치된 아이템 추적 (세트 시스템용)
    private readonly HashSet<string> _placedItems = new HashSet<string>();
    private readonly HashSet<int> _completedSetNotifications = new HashSet<int>();

    // 로컬 점수 캐시 (NetworkBehaviour 무효화 시 LeaderboardUI 폴백용)
    public static int[] LocalPlayerScores = new int[4];

    // 결과 화면으로 전달할 정적 데이터
    public static int FinalTeamDecorScore;
    public static int[] FinalPlayerDecorScores = new int[4];
    public static string[] FinalPlayerNames = new string[4];

    public override void Spawned()
    {
        if (!HasStateAuthority) return;

        if (DecoInventoryBridge.IsReturningFromDeco)
        {
            // DecoScene 복귀: 점수 복원 (타이머는 static이라 자동 유지)
            DecorScore = DecoInventoryBridge.SavedDecorScore;
            for (int i = 0; i < 4; i++)
                PlayerDecorScores.Set(i, LocalPlayerScores[i]);
            Debug.Log($"[GameManager] DecoScene 복귀 — 점수 {DecorScore}pt 복원, 타이머 {ElapsedTime:F0}s");
        }
        else if (_gameStartRealtime < 0f)
        {
            // 최초 게임 시작 (한 번만)
            _gameStartRealtime = Time.realtimeSinceStartup;
            DecorScore = 0;
            for (int i = 0; i < 4; i++)
                PlayerDecorScores.Set(i, 0);
            for (int i = 0; i < 4; i++)
                LocalPlayerScores[i] = 0;
            Debug.Log("[GameManager] 게임 시작!");
        }
        // DecoScene 진입 등 그 외: 아무것도 초기화하지 않음 (타이머/점수 유지)
    }

    // 직전 프레임의 씬 이름 — 새 게임 진입(로비/대기실 → TrashZone)을 감지해
    // Deco 왕복과 구분하고, 방 나갈 때의 잔여 프레임으로 타이머가 잘못 시작되는 것을 막는다.
    private static string _lastSceneName = "";

    void Update()
    {
        string scene = SceneManager.GetActiveScene().name;

        // 비게임 씬(로비/대기실 등)에서 TrashZone으로 "새로" 진입했을 때만 타이머 시작.
        // Deco→TrashZone(왕복)은 제외해 진행 시간을 유지하고,
        // 방 나가는 순간 TrashZone에 한 프레임 더 머무는 잔여 호출로는 시작되지 않는다.
        bool freshEntry = scene == "TrashZoneScene"
                          && _lastSceneName != "TrashZoneScene"
                          && _lastSceneName != "DecoScene";
        _lastSceneName = scene;

        if (freshEntry && _gameStartRealtime < 0f)
        {
            _gameStartRealtime = Time.realtimeSinceStartup;
            Debug.Log("[GameManager] 타이머 시작!");
        }

        UpdateTimerUI();  // 항상 호출 (realtime 기반)

        if (Runner == null) return;  // 네트워크 필요한 것만 여기서
        UpdateDecorScoreUI();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddDecorScore(int amount)
    {
        if (!HasStateAuthority) return;
        int prev = DecorScore;
        DecorScore += amount;
        AchievementManager.Instance?.OnDecorScoreUpdated(DecorScore);
        CheckMilestone(prev, DecorScore);
        Debug.Log($"[GameManager] 꾸미기 +{amount}pt → 총 {DecorScore}pt");
    }

    void CheckMilestone(int prev, int current)
    {
        foreach (int m in _milestones)
        {
            if (prev < m && current >= m && _notifiedMilestones.Add(m))
                RPC_NotifyMilestone(m);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_NotifyMilestone(int milestone)
    {
        UIManager.Instance?.ShowMilestoneNotification(milestone);
    }

    // 플레이어별 기여 점수 추가 (slotIndex: PlayerId - 1)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddPlayerDecorScore(int slotIndex, int amount)
    {
        if (!HasStateAuthority || slotIndex < 0 || slotIndex >= 4) return;
        PlayerDecorScores.Set(slotIndex, PlayerDecorScores.Get(slotIndex) + amount);
        LocalPlayerScores[slotIndex] = PlayerDecorScores.Get(slotIndex);
    }

    // 모든 클라이언트에 꾸미기 오브젝트 생성 명령
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlaceDecoItem(string itemName, Vector3 position)
    {
        DecoItemSpawner.CreateStatic(itemName, position);
        Debug.Log($"[GameManager] RPC_PlaceDecoItem: {itemName} at {position}");
    }

    // Host에서 아이템 배치 추적 + 세트 체크
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TrackPlacedItem(string itemName)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[세트시스템] 아이템 배치 추적: {itemName}");
        _placedItems.Add(itemName);

        // 세트 완성 체크
        var completedSets = SetDefinitions.GetCompletedSets(_placedItems);

        foreach (var completedSet in completedSets)
        {
            int setIndex = -1;
            for (int i = 0; i < SetDefinitions.Sets.Length; i++)
            {
                if (SetDefinitions.Sets[i] == completedSet)
                {
                    setIndex = i;
                    break;
                }
            }

            if (setIndex >= 0 && _completedSetNotifications.Add(setIndex))
            {
                int setScore = CalcSetScore(completedSet);
                int bonusScore = Mathf.RoundToInt(setScore * 0.05f);

                DecorScore += bonusScore;
                RPC_NotifySetCompletion(completedSet.name, bonusScore);

                Debug.Log($"[GameManager] 세트 완성: {completedSet.name} → +{bonusScore}pt (보너스)");
            }
        }
    }

    int CalcSetScore(SetDefinitions.Set set)
    {
        int total = 0;
        foreach (var itemName in set.items)
        {
            total += itemName switch
            {
                "나무"   => 40,
                "나무풍 상자"   => 20,
                "나무풍 의자"   => 30,
                "나무풍 울타리" => 50,
                "나무풍 꽃병"   => 60,
                "나무풍 탁자"   => 100,
                "나무풍 꽃밭"   => 200,
                _ => 0
            };
        }
        return total;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_NotifySetCompletion(string setName, int bonus)
    {
        UIManager.Instance?.ShowStatusMessage($"★ {setName} 완성! +{bonus}pt", 3f);
    }

    // 결과 화면으로 이동 (플레이어가 게임 종료 시 호출)
    public void GoToResultScene()
    {
        FinalTeamDecorScore = DecorScore;
        for (int i = 0; i < 4; i++)
            FinalPlayerDecorScores[i] = PlayerDecorScores.Get(i);

        // 플레이어 이름은 PhotonManager에서 가져옴
        if (PhotonManager.Instance != null)
        {
            for (int i = 0; i < 4; i++)
            {
                string charName = PhotonManager.Instance.GetPlayerCharacter(i) switch
                {
                    1 => "알파",
                    2 => "베타",
                    3 => "감마",
                    4 => "델타",
                    _ => $"플레이어{i + 1}"
                };
                FinalPlayerNames[i] = charName;
            }
        }

        PlayerPrefs.SetInt("FinalDecorScore", FinalTeamDecorScore);
        PlayerPrefs.Save();
        SceneManager.LoadScene("ResultScene");
    }

    // Inspector OnClick 연결용 (NextRoundButton 기존 참조 호환)
    public void OnNextRoundButtonClicked() => GoToResultScene();

    // 하위 호환용
    public void OnTrashCollected() { }
    public void OnTreePlanted() { }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddCollectedTrash(int count) { }

    void UpdateTimerUI()
    {
        float elapsed = ElapsedTime;

        // 화면 표시는 현재 씬의 UIManager(매 게임 새로 생성·연결되어 참조가 신선함)에 위임.
        // GameManager는 DontDestroyOnLoad 싱글톤이라 두 번째 게임부터 자신의 timerText가
        // 파괴된 첫 게임의 오브젝트를 가리켜(null) 직접 갱신이 멈추는 문제를 우회한다.
        UIManager.Instance?.UpdateTimer(elapsed);

        // 직접 연결된 timerText가 살아 있으면 보조로 함께 갱신 (단일 게임 세션 호환)
        if (timerText != null)
        {
            int min = Mathf.FloorToInt(elapsed / 60f);
            int sec = Mathf.FloorToInt(elapsed % 60f);
            timerText.text = $"{min:00}:{sec:00}";
        }
    }

    void UpdateDecorScoreUI()
    {
        int score = SafeDecorScore;
        if (decorScoreText != null)
            decorScoreText.text = $"꾸미기: {score}pt";
        UIManager.Instance?.UpdateDecorScore(score, 0);
    }
}