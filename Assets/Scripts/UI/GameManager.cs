using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

public class GameManager : NetworkBehaviour
{
	[Header("── HUD UI 연결 ──")]
	public TextMeshProUGUI timerText;

	public TextMeshProUGUI decorScoreText;

	private static float _gameStartRealtime = -1f;

	private static int _lastKnownDecorScore = 0;

	private static readonly int[] _milestones = new int[5] { 100, 500, 1000, 2000, 5000 };

	private readonly HashSet<int> _notifiedMilestones = new HashSet<int>();

	private readonly HashSet<string> _placedItems = new HashSet<string>();

	private readonly HashSet<int> _completedSetNotifications = new HashSet<int>();

	private readonly List<GameSaveData.DecoEntry> _placedDecoLog = new List<GameSaveData.DecoEntry>();

	private static bool _continueApplied = false;

	public static int[] LocalPlayerScores = new int[4];

	public static int FinalTeamDecorScore;

	public static int[] FinalPlayerDecorScores = new int[4];

	public static string[] FinalPlayerNames = new string[4];

	private static string _lastSceneName = "";

	public static GameManager Instance { get; private set; }

	public float ElapsedTime
	{
		get
		{
			if (!(_gameStartRealtime < 0f))
			{
				return Time.realtimeSinceStartup - _gameStartRealtime;
			}
			return 0f;
		}
	}

	[Networked]
	public int DecorScore { get; set; }

	public int SafeDecorScore
	{
		get
		{
			if (base.Object == null || !base.Object.IsValid)
			{
				return _lastKnownDecorScore;
			}
			_lastKnownDecorScore = DecorScore;
			return _lastKnownDecorScore;
		}
	}

	[Networked]
	[Capacity(4)]
	public NetworkArray<int> PlayerDecorScores { get; }

	[Networked]
	public int CollectedTrash { get; set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			Debug.Log("[GameManager] 싱글톤 생성");
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public static void ResetStaticState()
	{
		_gameStartRealtime = -1f;
		_lastKnownDecorScore = 0;
		for (int i = 0; i < 4; i++)
		{
			LocalPlayerScores[i] = 0;
		}
		_continueApplied = false;
	}

	public override void Spawned()
	{
		if (!base.HasStateAuthority)
		{
			return;
		}
		if (SaveManager.IsContinuing && !_continueApplied && !DecoInventoryBridge.IsReturningFromDeco)
		{
			_continueApplied = true;
			ApplyContinueSave(SaveManager.Pending);
		}
		else if (DecoInventoryBridge.IsReturningFromDeco)
		{
			DecorScore = DecoInventoryBridge.SavedDecorScore;
			for (int i = 0; i < 4; i++)
			{
				PlayerDecorScores.Set(i, LocalPlayerScores[i]);
			}
			Debug.Log($"[GameManager] DecoScene 복귀 — 점수 {DecorScore}pt 복원, 타이머 {ElapsedTime:F0}s");
		}
		else if (_gameStartRealtime < 0f)
		{
			_gameStartRealtime = Time.realtimeSinceStartup;
			DecorScore = 0;
			for (int j = 0; j < 4; j++)
			{
				PlayerDecorScores.Set(j, 0);
			}
			for (int k = 0; k < 4; k++)
			{
				LocalPlayerScores[k] = 0;
			}
			Debug.Log("[GameManager] 게임 시작!");
		}
	}

	private void Update()
	{
		string text = SceneManager.GetActiveScene().name;
		bool num = text == "TrashZoneScene" && _lastSceneName != "TrashZoneScene" && _lastSceneName != "DecoScene";
		_lastSceneName = text;
		if (num && _gameStartRealtime < 0f)
		{
			_gameStartRealtime = Time.realtimeSinceStartup;
			Debug.Log("[GameManager] 타이머 시작!");
		}
		UpdateTimerUI();
		if (!(base.Runner == null))
		{
			UpdateDecorScoreUI();
		}
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_AddDecorScore(int amount)
	{

		if (base.HasStateAuthority)
		{
			int decorScore = DecorScore;
			DecorScore += amount;
			AchievementManager.Instance?.OnDecorScoreUpdated(DecorScore);
			CheckMilestone(decorScore, DecorScore);
			Debug.Log($"[GameManager] 꾸미기 +{amount}pt → 총 {DecorScore}pt");
		}
	}

	private void CheckMilestone(int prev, int current)
	{
		int[] milestones = _milestones;
		foreach (int num in milestones)
		{
			if (prev < num && current >= num && _notifiedMilestones.Add(num))
			{
				RPC_NotifyMilestone(num);
			}
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_NotifyMilestone(int milestone)
	{

		UIManager.Instance?.ShowMilestoneNotification(milestone);
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_AddPlayerDecorScore(int slotIndex, int amount)
	{

		if (base.HasStateAuthority && slotIndex >= 0 && slotIndex < 4)
		{
			PlayerDecorScores.Set(slotIndex, PlayerDecorScores.Get(slotIndex) + amount);
			LocalPlayerScores[slotIndex] = PlayerDecorScores.Get(slotIndex);
		}
	}

	[Rpc(RpcSources.All, RpcTargets.All)]
	public void RPC_PlaceDecoItem(string itemName, Vector3 position)
	{

		DecoItemSpawner.CreateStatic(itemName, position);
		Debug.Log($"[GameManager] RPC_PlaceDecoItem: {itemName} at {position}");
		if (base.HasStateAuthority && SceneManager.GetActiveScene().name == "TrashZoneScene")
		{
			_placedDecoLog.Add(new GameSaveData.DecoEntry
			{
				itemName = itemName,
				x = position.x,
				y = position.y,
				z = position.z,
				flipped = false
			});
		}
	}

	public void SendDecorationsTo(PlayerRef target)
	{
		if (_placedDecoLog.Count == 0)
		{
			return;
		}
		foreach (GameSaveData.DecoEntry entry in _placedDecoLog)
		{
			RPC_ReplayDecoItemTo(target, entry.itemName, new Vector3(entry.x, entry.y, entry.z));
		}
		Debug.Log($"[GameManager] 늦게 참가한 플레이어({target})에게 꾸미기 {_placedDecoLog.Count}개 재전송");
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_ReplayDecoItemTo(PlayerRef target, string itemName, Vector3 position)
	{
		if (base.Runner.LocalPlayer != target)
		{
			return;
		}
		DecoItemSpawner.CreateStatic(itemName, position);
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_TrackPlacedItem(string itemName)
	{

		if (!base.HasStateAuthority)
		{
			return;
		}
		Debug.Log("[세트시스템] 아이템 배치 추적: " + itemName);
		_placedItems.Add(itemName);
		foreach (SetDefinitions.Set completedSet in SetDefinitions.GetCompletedSets(_placedItems))
		{
			int num3 = -1;
			for (int i = 0; i < SetDefinitions.Sets.Length; i++)
			{
				if (SetDefinitions.Sets[i] == completedSet)
				{
					num3 = i;
					break;
				}
			}
			if (num3 >= 0 && _completedSetNotifications.Add(num3))
			{
				int num4 = Mathf.RoundToInt((float)CalcSetScore(completedSet) * 0.05f);
				DecorScore += num4;
				RPC_NotifySetCompletion(completedSet.name, num4);
				Debug.Log($"[GameManager] 세트 완성: {completedSet.name} → +{num4}pt (보너스)");
			}
		}
	}

	private int CalcSetScore(SetDefinitions.Set set)
	{
		int num = 0;
		foreach (string item in set.items)
		{
			num += DecoCatalog.Price(item);
		}
		return num;
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_NotifySetCompletion(string setName, int bonus)
	{

		UIManager.Instance?.ShowStatusMessage($"★ {setName} 완성! +{bonus}pt", 3f);
	}

	private void ApplyContinueSave(GameSaveData data)
	{
		if (data == null)
		{
			return;
		}
		SetElapsedTime(data.elapsedTime);
		DecorScore = data.teamDecorScore;
		for (int i = 0; i < 4; i++)
		{
			int num = ((data.playerDecorScores != null && i < data.playerDecorScores.Length) ? data.playerDecorScores[i] : 0);
			PlayerDecorScores.Set(i, num);
			LocalPlayerScores[i] = num;
		}
		if (data.placedItems != null)
		{
			foreach (string placedItem in data.placedItems)
			{
				_placedItems.Add(placedItem);
			}
		}
		StartCoroutine(ReplayDecorationsRoutine(data));
		Debug.Log($"[GameManager] 이어하기 복원 — {data.elapsedTime:F0}s, {data.teamDecorScore}pt, 데코 {((data.decorations != null) ? data.decorations.Count : 0)}개");
	}

	private IEnumerator ReplayDecorationsRoutine(GameSaveData data)
	{
		if (data == null || data.decorations == null)
		{
			yield break;
		}
		float t = 0f;
		while (DecoItemSpawner.Instance == null && t < 3f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		foreach (GameSaveData.DecoEntry decoration in data.decorations)
		{
			RPC_PlaceDecoItem(decoration.itemName, new Vector3(decoration.x, decoration.y, decoration.z));
		}
		Debug.Log($"[GameManager] 이어하기 데코 {data.decorations.Count}개 재생성");
	}

	public void SetElapsedTime(float seconds)
	{
		_gameStartRealtime = Time.realtimeSinceStartup - Mathf.Max(0f, seconds);
	}

	public List<string> SnapshotPlacedItems()
	{
		return new List<string>(_placedItems);
	}

	public List<GameSaveData.DecoEntry> SnapshotDecorations()
	{
		return new List<GameSaveData.DecoEntry>(_placedDecoLog);
	}

	public void GoToResultScene()
	{
		SaveManager.DeleteSave();
		FinalTeamDecorScore = DecorScore;
		for (int i = 0; i < 4; i++)
		{
			FinalPlayerDecorScores[i] = PlayerDecorScores.Get(i);
		}
		if (PhotonManager.Instance != null)
		{
			for (int j = 0; j < 4; j++)
			{
				string text = PhotonManager.Instance.GetPlayerCharacter(j) switch
				{
					1 => "알파", 
					2 => "베타", 
					3 => "감마", 
					4 => "델타", 
					_ => $"플레이어{j + 1}", 
				};
				FinalPlayerNames[j] = text;
			}
		}
		PlayerPrefs.SetInt("FinalDecorScore", FinalTeamDecorScore);
		PlayerPrefs.Save();
		SceneManager.LoadScene("ResultScene");
	}

	public void OnNextRoundButtonClicked()
	{
		GoToResultScene();
	}

	public void OnTrashCollected()
	{
	}

	public void OnTreePlanted()
	{
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_AddCollectedTrash(int count)
	{
		if (base.HasStateAuthority)
		{
			CollectedTrash += count;
			Debug.Log($"[GameManager] 쓰레기 수집 +{count} → 총 {CollectedTrash}개");
		}
	}

	private void UpdateTimerUI()
	{
		float elapsedTime = ElapsedTime;
		UIManager.Instance?.UpdateTimer(elapsedTime);
		if (timerText != null)
		{
			int num = Mathf.FloorToInt(elapsedTime / 60f);
			int num2 = Mathf.FloorToInt(elapsedTime % 60f);
			timerText.text = $"{num:00}:{num2:00}";
		}
	}

	private void UpdateDecorScoreUI()
	{
		int safeDecorScore = SafeDecorScore;
		if (decorScoreText != null)
		{
			decorScoreText.text = $"꾸미기: {safeDecorScore}pt";
		}
		UIManager.Instance?.UpdateDecorScore(safeDecorScore, 0);
	}

}
