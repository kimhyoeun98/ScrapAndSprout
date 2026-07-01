using System;

[Serializable]
public class AchievementUnlockResponse
{
	public bool success;

	public string message;

	public AchievementData unlockedAchievement;

	public int totalRewardGold;

	public int totalRewardExp;
}
