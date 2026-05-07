using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// 서버 기반 업적 시스템 관리자
/// Spring 서버와 통신하여 업적을 관리합니다
/// </summary>
public class AchievementManager : MonoBehaviour
{
    // ==================== 싱글톤 ====================
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

    // ==================== 서버 설정 ====================
    [Header("서버 설정")]
    [Tooltip("Spring 서버 URL")]
    public string serverUrl = "http://localhost:8080/api/achievements"; // 임시값
    
    // ==================== 사용자 정보 ====================
    [Tooltip("플레이어 ID (로그인 시스템에서 가져올 예정)")]
    public string currentPlayerId = "test";  // TODO: 로그인 시스템 연동
    
    // ==================== 업적 캐시 ====================
    private List<AchievementData> _cachedAchievements = new List<AchievementData>();
    
    // ==================== 이벤트 ====================
    public delegate void AchievementUnlockedDelegate(AchievementData achievement);
    public event AchievementUnlockedDelegate OnAchievementUnlocked;
    
    // ==================== Unity 생명주기 ====================
    
    void Start()
    {
        // 게임 시작 시 업적 목록 로드
        StartCoroutine(LoadAchievements());
        
        Debug.Log("[AchievementManager] 서버 기반 업적 시스템 시작");
    }
    
    // ==================== 업적 로드 ====================
    
    /// <summary>
    /// 서버에서 업적 목록 로드
    /// </summary>
    public IEnumerator LoadAchievements()
    {
        string url = $"{serverUrl}?playerId={currentPlayerId}";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                
                // JSON 배열을 리스트로 변환
                string wrappedJson = "{\"achievements\":" + json + "}";
                AchievementListResponse response = JsonUtility.FromJson<AchievementListResponse>(wrappedJson);
                
                _cachedAchievements = response.achievements;
                
                Debug.Log($"[AchievementManager] 업적 로드 완료: {_cachedAchievements.Count}개");
            }
            else
            {
                Debug.LogWarning($"[AchievementManager] 업적 로드 실패: {request.error}");
            }
        }
    }
    
    // ==================== 업적 진행도 업데이트 ====================
    
    /// <summary>
    /// 쓰레기 수거 시 호출
    /// </summary>
    public void OnTrashCollected()
    {
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.COLLECT_TRASH_1, 1));
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.COLLECT_TRASH_10, 1));
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.COLLECT_TRASH_50, 1));
        
        // 날씨별 업적 체크
        if (WeatherManager.Instance != null && WeatherManager.Instance.IsAcidRain())
        {
            StartCoroutine(UpdateAchievementProgress(AchievementTypes.COLLECT_IN_RAIN_5, 1));
        }
    }
    
    /// <summary>
    /// 나무 심기 시 호출
    /// </summary>
    public void OnTreePlanted()
    {
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.PLANT_TREE_1, 1));
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.PLANT_TREE_5, 1));
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.PLANT_TREE_20, 1));
        
        // 날씨별 업적 체크
        if (WeatherManager.Instance != null && WeatherManager.Instance.IsYellowDust())
        {
            StartCoroutine(UpdateAchievementProgress(AchievementTypes.PLANT_IN_DUST_3, 1));
        }
    }
    
    /// <summary>
    /// 라운드 클리어 시 호출
    /// </summary>
    public void OnRoundCleared(float remainingTime)
    {
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.CLEAR_ROUND_1, 1));
        StartCoroutine(UpdateAchievementProgress(AchievementTypes.CLEAR_ROUND_5, 1));
    }
    
    // ==================== 서버 통신 ====================
    
    /// <summary>
    /// 서버에 업적 진행도 업데이트 요청
    /// </summary>
    IEnumerator UpdateAchievementProgress(string achievementType, int progressAmount)
    {
        string url = $"{serverUrl}/update";
        
        // 요청 데이터 생성
        AchievementUpdateRequest requestData = new AchievementUpdateRequest
        {
            playerId = currentPlayerId,
            achievementType = achievementType,
            progressAmount = progressAmount
        };
        
        string jsonData = JsonUtility.ToJson(requestData);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                AchievementUnlockResponse response = JsonUtility.FromJson<AchievementUnlockResponse>(json);
                
                // 새로 달성한 업적이 있으면 이벤트 발생
                if (response.unlockedAchievement != null)
                {
                    Debug.Log($"🏆 [업적 달성] {response.unlockedAchievement.achievementName}");
                    
                    // 보상 로그
                    if (response.totalRewardGold > 0)
                    {
                        Debug.Log($"💰 보상 골드: +{response.totalRewardGold}");
                    }
                    
                    if (response.totalRewardExp > 0)
                    {
                        Debug.Log($"⭐ 보상 경험치: +{response.totalRewardExp}");
                    }
                    
                    // UI 알림
                    OnAchievementUnlocked?.Invoke(response.unlockedAchievement);
                    
                    // 캐시 업데이트
                    UpdateCache(response.unlockedAchievement);
                }
            }
            else
            {
                // 서버 꺼져있어도 게임은 정상 작동
                // Debug.LogWarning($"[AchievementManager] 업적 업데이트 실패: {request.error}");
            }
        }
    }
    
    // ==================== 캐시 관리 ====================
    
    /// <summary>
    /// 캐시된 업적 정보 업데이트
    /// </summary>
    void UpdateCache(AchievementData updatedAchievement)
    {
        int index = _cachedAchievements.FindIndex(a => a.achievementType == updatedAchievement.achievementType);
        if (index >= 0)
        {
            _cachedAchievements[index] = updatedAchievement;
        }
    }
    
    // ==================== 업적 조회 ====================
    
    /// <summary>
    /// 캐시된 업적 목록 반환
    /// </summary>
    public List<AchievementData> GetCachedAchievements()
    {
        return new List<AchievementData>(_cachedAchievements);
    }
    
    /// <summary>
    /// 특정 업적 찾기
    /// </summary>
    public AchievementData GetAchievement(string achievementType)
    {
        return _cachedAchievements.Find(a => a.achievementType == achievementType);
    }
    
    /// <summary>
    /// 달성한 업적 목록
    /// </summary>
    public List<AchievementData> GetUnlockedAchievements()
    {
        return _cachedAchievements.FindAll(a => a.isCompleted);
    }
    
    /// <summary>
    /// 미달성 업적 목록
    /// </summary>
    public List<AchievementData> GetLockedAchievements()
    {
        return _cachedAchievements.FindAll(a => !a.isCompleted);
    }
    
    /// <summary>
    /// 달성률 (0~100)
    /// </summary>
    public float GetCompletionRate()
    {
        if (_cachedAchievements.Count == 0)
            return 0f;
        
        int unlockedCount = GetUnlockedAchievements().Count;
        return ((float)unlockedCount / _cachedAchievements.Count) * 100f;
    }
}
