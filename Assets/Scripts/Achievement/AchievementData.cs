using System;

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

	public float ProgressPercent => (float)progressPercent;

	public bool IsCompleted => isCompleted;
}
