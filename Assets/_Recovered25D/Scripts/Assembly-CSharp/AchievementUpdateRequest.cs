using System;

[Serializable]
public class AchievementUpdateRequest
{
	public string playerId;

	public string achievementType;

	public int progressAmount = 1;
}
