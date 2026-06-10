using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// 업적 시스템 관리자 — 기획서 업적 조건으로 전면 교체
///
/// [변경 내용]
/// 기존: 쓰레기 수거 횟수, 나무 심기, 라운드 클리어
/// 변경: 기획서 12가지 조건
///   - 희귀 아이템: 컴퓨터 첫 획득
///   - 골드 누적: 1,000 / 5,000 / 10,000 골드
///   - 꾸미기 점수: 500 / 1,000 / 2,000 / 5,000점
///   - 미니게임 성공: 100 / 500 / 1,000회
///
/// [유지된 것]
/// 서버 통신 구조, 캐시, 이벤트 시스템 전부 그대로 재사용
/// </summary>
public class AchievementManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────

    public static AchievementManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // ─────────────────────────────────────────
    //  업적 타입 상수 (서버 DB 키와 일치해야 함)
    //
    //  [비유]
    //  이 문자열들이 서버 DB의 업적 테이블 primary key 역할을 합니다.
    //  서버팀과 반드시 동일한 값을 사용해야 해요.
    // ─────────────────────────────────────────

    public static class AchievementTypes
    {
        // 희귀 아이템
        public const string RARE_ITEM_COMPUTER = "RARE_ITEM_COMPUTER";  // 컴퓨터 첫 획득

        // 골드 누적
        public const string GOLD_1000  = "GOLD_1000";
        public const string GOLD_5000  = "GOLD_5000";
        public const string GOLD_10000 = "GOLD_10000";

        // 꾸미기 점수
        public const string DECOR_500  = "DECOR_500";
        public const string DECOR_1000 = "DECOR_1000";
        public const string DECOR_2000 = "DECOR_2000";
        public const string DECOR_5000 = "DECOR_5000";

        // 미니게임 성공 횟수
        public const string MINIGAME_100  = "MINIGAME_100";
        public const string MINIGAME_500  = "MINIGAME_500";
        public const string MINIGAME_1000 = "MINIGAME_1000";
    }

    // ─────────────────────────────────────────
    //  서버 설정
    // ─────────────────────────────────────────

    [Header("서버 설정")]
    public string serverUrl = "http://172.31.51.36:8080/api/achievements";

    // ─────────────────────────────────────────
    //  플레이어 정보
    // ─────────────────────────────────────────

    [Tooltip("로그인 시스템에서 자동으로 채워집니다")]
    public string currentPlayerId = "test";

    // ─────────────────────────────────────────
    //  로컬 카운터 (서버 요청 횟수 줄이기 위한 캐시)
    //
    //  [비유]
    //  매 미니게임 성공마다 서버에 요청하면 부담이 큽니다.
    //  로컬에서 먼저 세다가 이정표(100, 500, 1000)에 도달하면 서버에 보냅니다.
    // ─────────────────────────────────────────

    private int _localMinigameSuccessCount = 0;
    private int _localTotalGold            = 0;
    private int _localDecorScore           = 0;

    // 이미 달성해서 다시 체크할 필요 없는 업적들
    private HashSet<string> _alreadyUnlocked = new HashSet<string>();

    // ─────────────────────────────────────────
    //  업적 캐시 + 이벤트 (기존 그대로)
    // ─────────────────────────────────────────

    private List<AchievementData> _cachedAchievements = new List<AchievementData>();

    public delegate void AchievementUnlockedDelegate(AchievementData achievement);
    public event AchievementUnlockedDelegate OnAchievementUnlocked;

    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    void Start()
    {
        if (ApiManager.Instance != null && !string.IsNullOrEmpty(ApiManager.Instance.playerId))
            currentPlayerId = ApiManager.Instance.playerId;

        StartCoroutine(LoadAchievements());
        Debug.Log($"[AchievementManager] 업적 시스템 시작 — playerId: {currentPlayerId}");
    }

    // ─────────────────────────────────────────
    //  외부에서 호출하는 이벤트 함수들 (신규)
    // ─────────────────────────────────────────

    /// <summary>
    /// 희귀 아이템 첫 획득 시 호출
    /// TrashPile.cs에서 "컴퓨터" 드랍 시 호출합니다
    /// </summary>
    public void OnRareItemFirstObtained(string itemName)
    {
        if (itemName == "컴퓨터" && !_alreadyUnlocked.Contains(AchievementTypes.RARE_ITEM_COMPUTER))
        {
            Debug.Log("[업적] 컴퓨터 첫 획득!");
            StartCoroutine(UpdateAchievementProgress(AchievementTypes.RARE_ITEM_COMPUTER, 1));
        }
    }

    /// <summary>
    /// 골드 총량 갱신 시 호출
    /// TrashCollector.SellAllTrash() 성공 콜백에서 호출합니다
    /// </summary>
    public void OnGoldAccumulated(int currentGold)
    {
        _localTotalGold = Mathf.Max(_localTotalGold, currentGold);

        // 이정표별로 업적 갱신
        if (_localTotalGold >= 1000)
            TryUpdateOnce(AchievementTypes.GOLD_1000);
        if (_localTotalGold >= 5000)
            TryUpdateOnce(AchievementTypes.GOLD_5000);
        if (_localTotalGold >= 10000)
            TryUpdateOnce(AchievementTypes.GOLD_10000);
    }

    /// <summary>
    /// 꾸미기 점수 갱신 시 호출
    /// GameManager.OnDecorationPlaced()에서 호출합니다
    /// </summary>
    public void OnDecorScoreUpdated(int totalDecorScore)
    {
        _localDecorScore = totalDecorScore;

        if (_localDecorScore >= 500)
            TryUpdateOnce(AchievementTypes.DECOR_500);
        if (_localDecorScore >= 1000)
            TryUpdateOnce(AchievementTypes.DECOR_1000);
        if (_localDecorScore >= 2000)
            TryUpdateOnce(AchievementTypes.DECOR_2000);
        if (_localDecorScore >= 5000)
            TryUpdateOnce(AchievementTypes.DECOR_5000);
    }

    /// <summary>
    /// 미니게임 성공 시 호출
    /// MiningMinigame → TrashPile.OnMinigameResult(true)에서 호출합니다
    /// </summary>
    public void OnMinigameSuccess()
    {
        _localMinigameSuccessCount++;

        // 이정표에 딱 도달한 순간에만 서버 요청
        // (이미 달성했으면 TryUpdateOnce에서 무시됨)
        if (_localMinigameSuccessCount == 100)
            TryUpdateOnce(AchievementTypes.MINIGAME_100);
        else if (_localMinigameSuccessCount == 500)
            TryUpdateOnce(AchievementTypes.MINIGAME_500);
        else if (_localMinigameSuccessCount == 1000)
            TryUpdateOnce(AchievementTypes.MINIGAME_1000);
    }

    /// <summary>
    /// 라운드 클리어 시 호출 (기존 함수 시그니처 유지)
    /// </summary>
    public void OnRoundCleared(float remainingTime)
    {
        // 라운드 클리어 업적은 현재 기획서에 없으므로 로그만 남김
        Debug.Log($"[업적] 라운드 클리어 — 남은시간: {remainingTime}초");
    }

    // ─────────────────────────────────────────
    //  업적 한 번만 달성 (이미 달성하면 서버 호출 안 함)
    // ─────────────────────────────────────────

    void TryUpdateOnce(string achievementType)
    {
        // 이미 달성했으면 건너뜀
        if (_alreadyUnlocked.Contains(achievementType)) return;

        _alreadyUnlocked.Add(achievementType);
        StartCoroutine(UpdateAchievementProgress(achievementType, 1));
    }

    // ─────────────────────────────────────────
    //  서버 통신 (기존 구조 그대로 재사용)
    // ─────────────────────────────────────────

    public IEnumerator LoadAchievements()
    {
        string url = $"{serverUrl}?playerId={currentPlayerId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string wrappedJson = "{\"achievements\":" + request.downloadHandler.text + "}";
                AchievementListResponse response = JsonUtility.FromJson<AchievementListResponse>(wrappedJson);
                _cachedAchievements = response.achievements;

                // 이미 달성한 업적은 로컬 캐시에도 등록
                foreach (var ach in _cachedAchievements)
                    if (ach.isCompleted)
                        _alreadyUnlocked.Add(ach.achievementType);

                Debug.Log($"[AchievementManager] 업적 로드 완료: {_cachedAchievements.Count}개 (달성: {_alreadyUnlocked.Count}개)");
            }
            else
            {
                Debug.LogWarning($"[AchievementManager] 업적 로드 실패: {request.error}");
            }
        }
    }

    IEnumerator UpdateAchievementProgress(string achievementType, int progressAmount)
    {
        string url = $"{serverUrl}/update";

        AchievementUpdateRequest requestData = new AchievementUpdateRequest
        {
            playerId        = currentPlayerId,
            achievementType = achievementType,
            progressAmount  = progressAmount
        };

        string jsonData = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                AchievementUnlockResponse response = JsonUtility.FromJson<AchievementUnlockResponse>(json);

                if (response.unlockedAchievement != null)
                {
                    Debug.Log($"🏆 [업적 달성] {response.unlockedAchievement.achievementName}");

                    AudioManager.Instance?.PlayAchievement();
                    OnAchievementUnlocked?.Invoke(response.unlockedAchievement);
                    UpdateCache(response.unlockedAchievement);
                }
            }
            // 서버 꺼져 있어도 게임은 정상 진행
        }
    }

    // ─────────────────────────────────────────
    //  캐시 관리 + 조회 (기존 그대로)
    // ─────────────────────────────────────────

    void UpdateCache(AchievementData updatedAchievement)
    {
        int index = _cachedAchievements.FindIndex(
            a => a.achievementType == updatedAchievement.achievementType);
        if (index >= 0)
            _cachedAchievements[index] = updatedAchievement;
    }

    public List<AchievementData> GetCachedAchievements()  => new List<AchievementData>(_cachedAchievements);
    public AchievementData GetAchievement(string type)    => _cachedAchievements.Find(a => a.achievementType == type);
    public List<AchievementData> GetUnlockedAchievements() => _cachedAchievements.FindAll(a => a.isCompleted);
    public List<AchievementData> GetLockedAchievements()   => _cachedAchievements.FindAll(a => !a.isCompleted);

    public float GetCompletionRate()
    {
        if (_cachedAchievements.Count == 0) return 0f;
        return ((float)GetUnlockedAchievements().Count / _cachedAchievements.Count) * 100f;
    }
}
