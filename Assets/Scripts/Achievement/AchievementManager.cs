using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AchievementManager : MonoBehaviour
{
	public static class AchievementTypes
	{
		public const string RARE_ITEM_COMPUTER = "RARE_ITEM_COMPUTER";

		public const string GOLD_1000 = "GOLD_1000";

		public const string GOLD_5000 = "GOLD_5000";

		public const string GOLD_10000 = "GOLD_10000";

		public const string DECOR_500 = "DECOR_500";

		public const string DECOR_1000 = "DECOR_1000";

		public const string DECOR_2000 = "DECOR_2000";

		public const string DECOR_5000 = "DECOR_5000";

		public const string MINIGAME_100 = "MINIGAME_100";

		public const string MINIGAME_500 = "MINIGAME_500";

		public const string MINIGAME_1000 = "MINIGAME_1000";
	}

	public delegate void AchievementUnlockedDelegate(AchievementData achievement);

	[Header("서버 설정")]
	public string serverUrl = "http://172.31.51.36:8080/api/achievements";

	[Tooltip("로그인 시스템에서 자동으로 채워집니다")]
	public string currentPlayerId = "test";

	private int _localMinigameSuccessCount;

	private int _localTotalGold;

	private int _localDecorScore;

	private HashSet<string> _alreadyUnlocked = new HashSet<string>();

	private List<AchievementData> _cachedAchievements = new List<AchievementData>();

	public static AchievementManager Instance { get; private set; }

	public event AchievementUnlockedDelegate OnAchievementUnlocked;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		if (ApiManager.Instance != null && !string.IsNullOrEmpty(ApiManager.Instance.playerId))
		{
			currentPlayerId = ApiManager.Instance.playerId;
		}
		StartCoroutine(LoadAchievements());
		Debug.Log("[AchievementManager] 업적 시스템 시작 — playerId: " + currentPlayerId);
	}

	public void OnRareItemFirstObtained(string itemName)
	{
		if (itemName == "컴퓨터" && !_alreadyUnlocked.Contains("RARE_ITEM_COMPUTER"))
		{
			Debug.Log("[업적] 컴퓨터 첫 획득!");
			StartCoroutine(UpdateAchievementProgress("RARE_ITEM_COMPUTER", 1));
		}
	}

	public void OnGoldAccumulated(int currentGold)
	{
		_localTotalGold = Mathf.Max(_localTotalGold, currentGold);
		if (_localTotalGold >= 1000)
		{
			TryUpdateOnce("GOLD_1000");
		}
		if (_localTotalGold >= 5000)
		{
			TryUpdateOnce("GOLD_5000");
		}
		if (_localTotalGold >= 10000)
		{
			TryUpdateOnce("GOLD_10000");
		}
	}

	public void OnDecorScoreUpdated(int totalDecorScore)
	{
		_localDecorScore = totalDecorScore;
		if (_localDecorScore >= 500)
		{
			TryUpdateOnce("DECOR_500");
		}
		if (_localDecorScore >= 1000)
		{
			TryUpdateOnce("DECOR_1000");
		}
		if (_localDecorScore >= 2000)
		{
			TryUpdateOnce("DECOR_2000");
		}
		if (_localDecorScore >= 5000)
		{
			TryUpdateOnce("DECOR_5000");
		}
	}

	public void OnMinigameSuccess()
	{
		_localMinigameSuccessCount++;
		if (_localMinigameSuccessCount == 100)
		{
			TryUpdateOnce("MINIGAME_100");
		}
		else if (_localMinigameSuccessCount == 500)
		{
			TryUpdateOnce("MINIGAME_500");
		}
		else if (_localMinigameSuccessCount == 1000)
		{
			TryUpdateOnce("MINIGAME_1000");
		}
	}

	public void OnRoundCleared(float remainingTime)
	{
		Debug.Log($"[업적] 라운드 클리어 — 남은시간: {remainingTime}초");
	}

	private void TryUpdateOnce(string achievementType)
	{
		if (!_alreadyUnlocked.Contains(achievementType))
		{
			_alreadyUnlocked.Add(achievementType);
			StartCoroutine(UpdateAchievementProgress(achievementType, 1));
		}
	}

	public IEnumerator LoadAchievements()
	{
		string uri = serverUrl + "?playerId=" + currentPlayerId;
		using UnityWebRequest request = UnityWebRequest.Get(uri);
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			AchievementListResponse achievementListResponse = JsonUtility.FromJson<AchievementListResponse>("{\"achievements\":" + request.downloadHandler.text + "}");
			_cachedAchievements = achievementListResponse.achievements;
			foreach (AchievementData cachedAchievement in _cachedAchievements)
			{
				if (cachedAchievement.isCompleted)
				{
					_alreadyUnlocked.Add(cachedAchievement.achievementType);
				}
			}
			Debug.Log($"[AchievementManager] 업적 로드 완료: {_cachedAchievements.Count}개 (달성: {_alreadyUnlocked.Count}개)");
		}
		else
		{
			Debug.LogWarning("[AchievementManager] 업적 로드 실패: " + request.error);
		}
	}

	private IEnumerator UpdateAchievementProgress(string achievementType, int progressAmount)
	{
		string url = serverUrl + "/update";
		string s = JsonUtility.ToJson(new AchievementUpdateRequest
		{
			playerId = currentPlayerId,
			achievementType = achievementType,
			progressAmount = progressAmount
		});
		using UnityWebRequest request = new UnityWebRequest(url, "POST");
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		request.uploadHandler = new UploadHandlerRaw(bytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			AchievementUnlockResponse achievementUnlockResponse = JsonUtility.FromJson<AchievementUnlockResponse>(request.downloadHandler.text);
			if (achievementUnlockResponse.unlockedAchievement != null)
			{
				Debug.Log("\ud83c\udfc6 [업적 달성] " + achievementUnlockResponse.unlockedAchievement.achievementName);
				AudioManager.Instance?.PlayAchievement();
				this.OnAchievementUnlocked?.Invoke(achievementUnlockResponse.unlockedAchievement);
				UpdateCache(achievementUnlockResponse.unlockedAchievement);
			}
		}
	}

	private void UpdateCache(AchievementData updatedAchievement)
	{
		int num = _cachedAchievements.FindIndex((AchievementData a) => a.achievementType == updatedAchievement.achievementType);
		if (num >= 0)
		{
			_cachedAchievements[num] = updatedAchievement;
		}
	}

	public List<AchievementData> GetCachedAchievements()
	{
		return new List<AchievementData>(_cachedAchievements);
	}

	public AchievementData GetAchievement(string type)
	{
		return _cachedAchievements.Find((AchievementData a) => a.achievementType == type);
	}

	public List<AchievementData> GetUnlockedAchievements()
	{
		return _cachedAchievements.FindAll((AchievementData a) => a.isCompleted);
	}

	public List<AchievementData> GetLockedAchievements()
	{
		return _cachedAchievements.FindAll((AchievementData a) => !a.isCompleted);
	}

	public float GetCompletionRate()
	{
		if (_cachedAchievements.Count == 0)
		{
			return 0f;
		}
		return (float)GetUnlockedAchievements().Count / (float)_cachedAchievements.Count * 100f;
	}
}
