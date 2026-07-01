using System.Collections.Generic;
using UnityEngine;

public class AIBotController : MonoBehaviour
{
	public enum BotState
	{
		Idle,
		FindTrash,
		MoveToTrash,
		CollectTrash,
		MoveToTeleporter,
		MoveToNPC,
		SellTrash,
		BuyItem
	}

	private BotState _currentState;

	[Header("이동 설정")]
	[Tooltip("AI 봇 이동 속도")]
	public float moveSpeed = 3f;

	[Tooltip("목표 도착 판정 거리")]
	public float arriveDistance = 1.2f;

	[Header("행동 설정")]
	[Tooltip("판매를 시작할 쓰레기 보유 개수")]
	public int sellThreshold = 10;

	[Tooltip("봇이 구매할 꾸미기 아이템 이름 (TrashCollector 가격표와 일치해야 함)")]
	public string buyItemName = "나무풍 나무";

	[Tooltip("쓰레기 채굴(채굴 미니게임 대체) 소요 시간")]
	public float miningDuration = 1.5f;

	[Tooltip("쓰레기가 없을 때 재탐색 대기 시간 (초)")]
	public float noTrashRetryDelay = 3f;

	[Tooltip("행동 후 대기 시간 (초)")]
	public float actionDelay = 0.5f;

	private bool _isMining;

	[Header("NPC 설정")]
	[Tooltip("NPC를 찾기 위한 태그")]
	public string npcTag = "NPC";

	private Rigidbody2D _rb;

	// AI가 계산한 목표 이동 속도. 실제 물리 이동은 PlayerMovement.FixedUpdateNetwork(네트워크 물리 틱)가 대신 구동한다.
	public Vector2 DesiredVelocity { get; private set; }

	private TrashCollector _trashCollector;

	private Vector3 _targetPosition;

	private GameObject _targetObject;

	private float _actionTimer;

	private bool _isTeleporting;

	private float _teleportCooldown;

	private float _spawnGraceTimer = 1f;

	private BotState _npcGoalState = BotState.SellTrash;

	private BotState _afterTeleportState = BotState.MoveToNPC;

	private static readonly HashSet<string> TrashItemNames = new HashSet<string> { "휴지", "바나나껍질", "음료캔", "디스크", "타이어", "드럼통", "컴퓨터" };

	private void Start()
	{
		_rb = GetComponent<Rigidbody2D>();
		_trashCollector = GetComponent<TrashCollector>();
		Debug.Log("[AIBot] AI 봇 시작!");
	}

	private void Update()
	{
		if (_spawnGraceTimer > 0f)
		{
			_spawnGraceTimer -= Time.deltaTime;
		}
		else if (_actionTimer > 0f)
		{
			_actionTimer -= Time.deltaTime;
		}
		else
		{
			RunStateMachine();
		}
	}

	private void FixedUpdate()
	{
		if (_spawnGraceTimer > 0f || _actionTimer > 0f)
		{
			DesiredVelocity = Vector2.zero;
		}
		else if (_currentState == BotState.MoveToTrash || _currentState == BotState.MoveToNPC || _currentState == BotState.MoveToTeleporter)
		{
			DesiredVelocity = ComputeMoveVelocity();
		}
		else
		{
			DesiredVelocity = Vector2.zero;
		}
	}

	private void RunStateMachine()
	{
		switch (_currentState)
		{
		case BotState.Idle:
			State_Idle();
			break;
		case BotState.FindTrash:
			State_FindTrash();
			break;
		case BotState.MoveToTrash:
			State_MoveToTrash();
			break;
		case BotState.CollectTrash:
			State_CollectTrash();
			break;
		case BotState.MoveToTeleporter:
			State_MoveToTeleporter();
			break;
		case BotState.MoveToNPC:
			State_MoveToNPC();
			break;
		case BotState.SellTrash:
			State_SellTrash();
			break;
		case BotState.BuyItem:
			State_BuyItem();
			break;
		}
	}

	private void State_Idle()
	{
		if (_trashCollector == null)
		{
			ChangeState(BotState.FindTrash);
		}
		else if (CountTrashItems() >= sellThreshold)
		{
			_npcGoalState = BotState.SellTrash;
			_afterTeleportState = BotState.MoveToNPC;
			_targetObject = null;
			ChangeState(BotState.MoveToTeleporter);
		}
		else if (CountTrashItems() == 0 && CanAfford(buyItemName))
		{
			_npcGoalState = BotState.BuyItem;
			_afterTeleportState = BotState.MoveToNPC;
			_targetObject = null;
			ChangeState(BotState.MoveToTeleporter);
		}
		else
		{
			ChangeState(BotState.FindTrash);
		}
	}

	private void State_FindTrash()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Trash");
		if (array.Length == 0)
		{
			Debug.Log("[AIBot] 쓰레기가 없습니다. Idle로 복귀.");
			_actionTimer = noTrashRetryDelay;
			ChangeState(BotState.Idle);
			return;
		}
		GameObject gameObject = null;
		float num = float.MaxValue;
		GameObject[] array2 = array;
		foreach (GameObject gameObject2 in array2)
		{
			float num2 = Vector3.Distance(base.transform.position, gameObject2.transform.position);
			if (num2 < num)
			{
				num = num2;
				gameObject = gameObject2;
			}
		}
		if (gameObject != null)
		{
			_targetObject = gameObject;
			_targetPosition = gameObject.transform.position;
			ChangeState(BotState.MoveToTrash);
			Debug.Log($"[AIBot] 쓰레기 발견! 거리: {num:F2}");
		}
		else
		{
			ChangeState(BotState.Idle);
		}
	}

	private void State_MoveToTrash()
	{
		if (_targetObject == null)
		{
			Debug.Log("[AIBot] 쓰레기 수집 완료 (TrashCollector 트리거)");
			_actionTimer = actionDelay;
			ChangeState(BotState.Idle);
			return;
		}
		_targetPosition = _targetObject.transform.position;
		if (Vector3.Distance(base.transform.position, _targetPosition) < arriveDistance)
		{
			ChangeState(BotState.CollectTrash);
		}
	}

	private void State_CollectTrash()
	{
		if (!_isMining)
		{
			_isMining = true;
			_actionTimer = miningDuration;
			Debug.Log("[AIBot] 채굴 중...");
			return;
		}
		_isMining = false;
		if (_targetObject != null && _trashCollector != null)
		{
			string text = ResolveTrashName(_targetObject);
			_trashCollector.RPC_AddItem(text);
			Debug.Log("[AIBot] 쓰레기 수집: " + text);
			Object.Destroy(_targetObject);
		}
		_targetObject = null;
		_actionTimer = actionDelay;
		ChangeState(BotState.Idle);
	}

	private void State_MoveToNPC()
	{
		if (_targetObject == null)
		{
			GameObject[] array = GameObject.FindGameObjectsWithTag(npcTag);
			if (array.Length == 0)
			{
				Debug.LogWarning("[AIBot] '" + npcTag + "' 태그를 가진 NPC를 찾을 수 없습니다. Idle로 복귀.");
				_actionTimer = actionDelay;
				ChangeState(BotState.Idle);
				return;
			}
			GameObject gameObject = null;
			float num = float.MaxValue;
			GameObject[] array2 = array;
			foreach (GameObject gameObject2 in array2)
			{
				float num2 = Vector3.Distance(base.transform.position, gameObject2.transform.position);
				if (num2 < num)
				{
					num = num2;
					gameObject = gameObject2;
				}
			}
			_targetObject = gameObject;
			_targetPosition = gameObject.transform.position;
			Debug.Log($"[AIBot] NPC 발견! 거리: {num:F2}, 목표: {_npcGoalState}");
		}
		_targetPosition = _targetObject.transform.position;
		if (Vector3.Distance(base.transform.position, _targetPosition) < arriveDistance)
		{
			_targetObject = null;
			ChangeState(_npcGoalState);
		}
	}

	private void State_MoveToTeleporter()
	{
		if (_teleportCooldown > 0f)
		{
			_teleportCooldown -= Time.deltaTime;
			return;
		}
		if (_targetObject == null)
		{
			ZoneTeleporter[] array = Object.FindObjectsByType<ZoneTeleporter>(FindObjectsSortMode.None);
			if (array.Length == 0)
			{
				Debug.LogWarning("[AIBot] ZoneTeleporter를 찾을 수 없습니다. Idle로 복귀.");
				_actionTimer = actionDelay;
				ChangeState(BotState.Idle);
				return;
			}
			ZoneTeleporter zoneTeleporter = null;
			float num = float.MaxValue;
			ZoneTeleporter[] array2 = array;
			foreach (ZoneTeleporter zoneTeleporter2 in array2)
			{
				if (!zoneTeleporter2.teleportToScene)
				{
					float num2 = Vector3.Distance(base.transform.position, zoneTeleporter2.transform.position);
					if (num2 < num)
					{
						num = num2;
						zoneTeleporter = zoneTeleporter2;
					}
				}
			}
			if (zoneTeleporter == null)
			{
				Debug.LogWarning("[AIBot] 적절한 ZoneTeleporter를 찾을 수 없습니다. Idle로 복귀.");
				_actionTimer = actionDelay;
				ChangeState(BotState.Idle);
				return;
			}
			_targetObject = zoneTeleporter.gameObject;
			Debug.Log($"[AIBot] 텔레포터로 이동: {zoneTeleporter.gameObject.name}, 목표상태: {_afterTeleportState}");
		}
		_targetPosition = _targetObject.transform.position;
		Vector2 a = new Vector2(base.transform.position.x, base.transform.position.y);
		Vector2 b = new Vector2(_targetPosition.x, _targetPosition.y);
		if (Vector2.Distance(a, b) < arriveDistance)
		{
			Debug.Log($"[AIBot] 텔레포터 도착 — 텔레포트 대기 후 {_afterTeleportState}로 전환");
			_isTeleporting = true;
			ZoneTeleporter component = _targetObject.GetComponent<ZoneTeleporter>();
			_targetObject = null;
			_targetPosition = base.transform.position;
			_actionTimer = 1f;
			_teleportCooldown = 10f;
			ChangeState(_afterTeleportState);
			if (component != null)
			{
				component.RequestBotTeleport(base.gameObject);
			}
		}
	}

	private void State_SellTrash()
	{
		Debug.Log("[AIBot] 쓰레기 판매 중...");
		if (_trashCollector != null)
		{
			_trashCollector.SellAllTrash();
		}
		_afterTeleportState = BotState.Idle;
		_targetObject = null;
		_actionTimer = actionDelay;
		ChangeState(BotState.MoveToTeleporter);
	}

	private void State_BuyItem()
	{
		Debug.Log("[AIBot] 꾸미기 아이템 구매 중: " + buyItemName);
		if (_trashCollector != null)
		{
			_trashCollector.BuyDecorationItem(buyItemName);
		}
		_afterTeleportState = BotState.Idle;
		_targetObject = null;
		_actionTimer = actionDelay;
		ChangeState(BotState.MoveToTeleporter);
	}

	private Vector2 ComputeMoveVelocity()
	{
		Vector3 normalized = (_targetPosition - base.transform.position).normalized;
		return new Vector2(normalized.x, normalized.y) * moveSpeed;
	}

	private void ChangeState(BotState newState)
	{
		Debug.Log($"[AIBot] State: {_currentState} → {newState}");
		_currentState = newState;
	}

	public void ForceIdle()
	{
		DesiredVelocity = Vector2.zero;
		if (_rb != null)
		{
			_rb.linearVelocity = Vector2.zero;
		}
		if (!_isTeleporting && !(_actionTimer > 0f))
		{
			_targetObject = null;
			ChangeState(BotState.Idle);
		}
	}

	public void SetTeleporting(bool value)
	{
		_isTeleporting = value;
	}

	private int CountTrashItems()
	{
		if (_trashCollector == null)
		{
			return 0;
		}
		int num = 0;
		foreach (KeyValuePair<string, int> item in _trashCollector.inventory)
		{
			if (TrashItemNames.Contains(item.Key))
			{
				num += item.Value;
			}
		}
		return num;
	}

	private bool CanAfford(string itemName)
	{
		if (_trashCollector == null)
		{
			return false;
		}
		int buyPrice = GetBuyPrice(itemName);
		if (buyPrice > 0)
		{
			return _trashCollector.gold >= buyPrice;
		}
		return false;
	}

	private int GetBuyPrice(string itemName)
	{
		if (_trashCollector == null)
		{
			return 0;
		}
		return itemName switch
		{
			"나무풍 나무" => _trashCollector.treePrice, 
			"나무풍 상자" => _trashCollector.boxPrice, 
			"나무풍 의자" => _trashCollector.chairPrice, 
			"나무풍 울타리" => _trashCollector.fencePrice, 
			"나무풍 꽃병" => _trashCollector.vasePrice, 
			"나무풍 탁자" => _trashCollector.tablePrice, 
			"나무풍 꽃밭" => _trashCollector.flowerFieldPrice, 
			_ => 0, 
		};
	}

	private string ResolveTrashName(GameObject obj)
	{
		string text = obj.name.Replace("(Clone)", "").Trim();
		if (!TrashItemNames.Contains(text))
		{
			return "휴지";
		}
		return text;
	}

	private void OnDrawGizmos()
	{
		if (_currentState == BotState.MoveToTrash || _currentState == BotState.MoveToNPC || _currentState == BotState.MoveToTeleporter)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(_targetPosition, 0.5f);
			Gizmos.DrawLine(base.transform.position, _targetPosition);
		}
	}
}
