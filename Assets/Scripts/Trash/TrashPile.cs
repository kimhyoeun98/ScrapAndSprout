using System;
using Fusion;
using UnityEngine;
using UnityEngine.Scripting;

public class TrashPile : NetworkBehaviour
{
	public enum PileSize
	{
		Small,
		Large
	}

	[Header("── 더미 설정 ──")]
	public PileSize pileSize;

	[Header("── 감지 범위 ──")]
	[Tooltip("플레이어 감지 범위 (0이면 Collider 크기 자동 사용)")]
	public float interactionRadius = 2.5f;

	[Tooltip("소형 더미 기본 입력 횟수")]
	public int smallBaseInputCount = 4;

	[Tooltip("대형 더미 기본 입력 횟수")]
	public int largeBaseInputCount = 6;

	[Tooltip("날씨 악화 시 추가 입력 횟수")]
	public int badWeatherExtraInputs = 2;

	[Header("── 드랍 아이템 프리팹 ──")]
	[Tooltip("드랍될 아이템 프리팹들 (TrashCollector가 인벤토리로 받음)")]
	public NetworkObject[] itemPrefabs;

	private static readonly float[] _baseDropRates = new float[7] { 35f, 30f, 20f, 10f, 3f, 1f, 0.5f };

	private static readonly float[] _badWeatherDropRates = new float[7] { 44.5f, 38.25f, 10f, 5f, 1.5f, 0.5f, 0.25f };

	private static readonly string[] _itemNames = new string[7] { "휴지", "바나나껍질", "음료캔", "디스크", "타이어", "드럼통", "컴퓨터" };

	public static readonly int[] ItemPrices = new int[7] { 7, 10, 15, 30, 100, 200, 350 };

	private bool _isPlayerNearby;

	private TrashCollector _playerCollector;

	private bool _isBeingMined;

	[Networked]
	private int NetworkedRequiredInputs { get; set; }

	private void Update()
	{
		if (_isBeingMined)
		{
			return;
		}
		DetectNearbyPlayer();
		if (_isPlayerNearby && _playerCollector != null && Input.GetKeyDown(KeyCode.E))
		{
			PlayerMovement component = _playerCollector.GetComponent<PlayerMovement>();
			if (component != null && !component.CanAct)
			{
				UIManager.Instance?.ShowStatusMessage("배터리 방전! 채굴 불가");
			}
			else
			{
				StartMining();
			}
		}
	}

	private void DetectNearbyPlayer()
	{
		Collider2D component = GetComponent<Collider2D>();
		if (component == null)
		{
			return;
		}
		float radius = ((interactionRadius > 0f) ? interactionRadius : (component.bounds.extents.magnitude * 2f));
		Collider2D[] array = Physics2D.OverlapCircleAll(base.transform.position, radius);
		bool flag = false;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			if (!collider2D.CompareTag("Player"))
			{
				continue;
			}
			PlayerMovement component2 = collider2D.GetComponent<PlayerMovement>();
			if (!(component2 == null) && component2.HasInputAuthority)
			{
				if (!_isPlayerNearby)
				{
					UIManager.Instance?.OnTrashEnter();
				}
				_isPlayerNearby = true;
				_playerCollector = collider2D.GetComponent<TrashCollector>();
				flag = true;
				break;
			}
		}
		if (!flag && _isPlayerNearby)
		{
			_isPlayerNearby = false;
			_playerCollector = null;
			UIManager.Instance?.OnTrashExit();
		}
	}

	private void StartMining()
	{
		_isBeingMined = true;
		PlayerMovement obj = _playerCollector?.GetComponent<PlayerMovement>();
		float num = ((WeatherManager.Instance != null) ? WeatherManager.Instance.GetBatteryMultiplier() : 1f);
		obj?.RPC_DrainBattery(5f * num);
		int requiredInputCount = GetRequiredInputCount();
		Debug.Log($"[TrashPile] 채굴 시작! 더미 크기: {pileSize}, 필요 입력: {requiredInputCount}회");
		if (MiningMinigame.Instance == null)
		{
			Debug.LogError("[TrashPile] MiningMinigame.Instance null — MiningMinigame 오브젝트가 활성화 상태인지 확인!");
			_isBeingMined = false;
		}
		else
		{
			MiningMinigame.Instance.StartMinigame(requiredInputCount, OnMinigameResult);
		}
	}

	private void OnMinigameResult(bool success)
	{
		_isBeingMined = false;
		if (success)
		{
			string text = RollDropItem();
			GiveItemToPlayer(text);
			if (IsPlayerBetaCharacter() && UnityEngine.Random.Range(0f, 100f) < 5f)
			{
				string text2 = RollDropItem();
				GiveItemToPlayer(text2);
				UIManager.Instance?.ShowStatusMessage("더블 드랍! " + text + " + " + text2);
			}
			else
			{
				UIManager.Instance?.ShowStatusMessage("채굴 성공! " + text + " 획득", 1.5f);
			}
			if (text == "컴퓨터")
			{
				AchievementManager.Instance?.OnRareItemFirstObtained("컴퓨터");
			}
			AchievementManager.Instance?.OnMinigameSuccess();
			UIManager.Instance?.OnTrashCollected();
			if (base.Object == null || !base.Object.IsValid)
			{
				Debug.LogWarning("[TrashPile] 결과 처리 시점에 Object 무효 — Despawn 건너뜀");
			}
			else if (base.HasStateAuthority)
			{
				Debug.Log("[TrashPile] Host — 바로 Despawn");
				base.Runner.Despawn(base.Object);
			}
			else
			{
				Debug.Log($"[TrashPile] Despawn 요청 — Object.IsValid:{base.Object.IsValid}");
				RPC_RequestDespawn();
				Debug.Log("[TrashPile] 채굴 완료 — Despawn 요청");
			}
		}
		else
		{
			PlayerMovement obj = _playerCollector?.GetComponent<PlayerMovement>();
			float num = ((WeatherManager.Instance != null) ? WeatherManager.Instance.GetBatteryMultiplier() : 1f);
			obj?.RPC_DrainBattery(10f * num);
			UIManager.Instance?.ShowStatusMessage("채굴 실패! 배터리 -10%", 1.5f);
			Debug.Log("[TrashPile] 채굴 실패 — 배터리 소모");
		}
	}

	private string RollDropItem()
	{
		float[] array = ((WeatherManager.Instance != null && !WeatherManager.Instance.IsClear()) ? _badWeatherDropRates : _baseDropRates);
		int num = ((pileSize == PileSize.Small) ? 4 : 7);
		bool flag = IsPlayerAlphaCharacter();
		float num2 = 0f;
		for (int i = 0; i < num; i++)
		{
			float num3 = array[i];
			if (flag && i >= 3)
			{
				num3 += 0.5f;
			}
			num2 += num3;
		}
		float num4 = UnityEngine.Random.Range(0f, num2);
		float num5 = 0f;
		for (int j = 0; j < num; j++)
		{
			float num6 = array[j];
			if (flag && j >= 3)
			{
				num6 += 0.5f;
			}
			num5 += num6;
			if (num4 <= num5)
			{
				return _itemNames[j];
			}
		}
		return _itemNames[0];
	}

	private void GiveItemToPlayer(string itemName)
	{
		if (!(_playerCollector == null))
		{
			_playerCollector.RPC_AddItem(itemName);
			Debug.Log("[TrashPile] 아이템 지급: " + itemName);
		}
	}

	private bool IsPlayerAlphaCharacter()
	{
		if (_playerCollector == null)
		{
			return false;
		}
		return _playerCollector.characterType == TrashCollector.CharacterType.Alpha;
	}

	private bool IsPlayerBetaCharacter()
	{
		if (_playerCollector == null)
		{
			return false;
		}
		return _playerCollector.characterType == TrashCollector.CharacterType.Beta;
	}

	private void OnDrawGizmos()
	{
		BoxCollider2D component = GetComponent<BoxCollider2D>();
		if (!(component == null))
		{
			Gizmos.color = (_isPlayerNearby ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 1f, 0f, 0.3f));
			Gizmos.DrawCube(base.transform.position + (Vector3)component.offset, component.size);
			Gizmos.color = (_isPlayerNearby ? Color.green : Color.yellow);
			Gizmos.DrawWireCube(base.transform.position + (Vector3)component.offset, component.size);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Player"))
		{
			PlayerMovement component = other.GetComponent<PlayerMovement>();
			if (!(component == null) && component.HasInputAuthority)
			{
				_isPlayerNearby = true;
				_playerCollector = other.GetComponent<TrashCollector>();
				UIManager.Instance?.OnTrashEnter();
			}
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}
		PlayerMovement component = other.GetComponent<PlayerMovement>();
		if (!(component == null) && component.HasInputAuthority)
		{
			_isPlayerNearby = false;
			_playerCollector = null;
			if (_isBeingMined)
			{
				_isBeingMined = false;
				MiningMinigame.Instance?.CancelMinigame();
			}
			UIManager.Instance?.OnTrashExit();
		}
	}

	public override void Spawned()
	{
		Debug.Log($"[TrashPile] Spawned — IsServer:{base.Runner.IsServer} / Position:{base.transform.position}");
		if (base.HasStateAuthority)
		{
			int num = ((pileSize == PileSize.Small) ? smallBaseInputCount : largeBaseInputCount);
			bool flag = WeatherManager.Instance != null && !WeatherManager.Instance.IsClear();
			NetworkedRequiredInputs = (flag ? (num + badWeatherExtraInputs) : num);
			Debug.Log($"[TrashPile] 입력 횟수 확정: {NetworkedRequiredInputs}회 (날씨악화:{flag})");
		}
	}

	private int GetRequiredInputCount()
	{
		int num = ((pileSize == PileSize.Small) ? smallBaseInputCount : largeBaseInputCount);
		if (!(WeatherManager.Instance != null) || WeatherManager.Instance.IsClear())
		{
			return num;
		}
		return num + badWeatherExtraInputs;
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	private void RPC_RequestDespawn()
	{

		if (base.Object == null || !base.Object.IsValid)
		{
			Debug.LogWarning("[TrashPile] RPC_RequestDespawn — Object 무효");
			return;
		}
		Debug.Log("[TrashPile] RPC_RequestDespawn — Despawn 실행");
		base.Runner.Despawn(base.Object);
	}

}
