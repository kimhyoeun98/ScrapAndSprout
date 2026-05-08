using System;
using System.Collections.Generic;

/// <summary>
/// 업적 데이터 (서버에서 받아옴)
/// </summary>
[Serializable]
public class AchievementData
{
    public string achievementType;
    public string achievementName;
    public string designation;
    public string detail;
    public int targetValue;
    public int currentProgress;
    public bool isCompleted;
    public int rewardGold;
    public int rewardExp;
    public double progressPercent;
    
    /// <summary>
    /// 진행도 퍼센트 (0~100)
    /// </summary>
    public float ProgressPercent
    {
        get { return (float)progressPercent; }
    }
    
    /// <summary>
    /// 달성 완료 여부
    /// </summary>
    public bool IsCompleted
    {
        get { return isCompleted; }
    }
}

/// <summary>
/// 업적 목록 응답
/// </summary>
[Serializable]
public class AchievementListResponse
{
    public List<AchievementData> achievements;
}

/// <summary>
/// 업적 진행도 업데이트 요청
/// </summary>
[Serializable]
public class AchievementUpdateRequest
{
    public string playerId;
    public string achievementType;
    public int progressAmount = 1;
}

/// <summary>
/// 업적 달성 응답
/// </summary>
[Serializable]
public class AchievementUnlockResponse
{
    public bool success;
    public string message;
    public AchievementData unlockedAchievement;  // 새로 달성한 업적
    public int totalRewardGold;
    public int totalRewardExp;
}

/// <summary>
/// 업적 타입 상수
/// </summary>
public static class AchievementTypes
{
    public const string COLLECT_TRASH_1 = "COLLECT_TRASH_1";
    public const string COLLECT_TRASH_10 = "COLLECT_TRASH_10";
    public const string COLLECT_TRASH_50 = "COLLECT_TRASH_50";
    
    public const string PLANT_TREE_1 = "PLANT_TREE_1";
    public const string PLANT_TREE_5 = "PLANT_TREE_5";
    public const string PLANT_TREE_20 = "PLANT_TREE_20";
    
    public const string CLEAR_ROUND_1 = "CLEAR_ROUND_1";
    public const string CLEAR_ROUND_5 = "CLEAR_ROUND_5";
    
    public const string COLLECT_IN_RAIN_5 = "COLLECT_IN_RAIN_5";
    public const string PLANT_IN_DUST_3 = "PLANT_IN_DUST_3";
}
