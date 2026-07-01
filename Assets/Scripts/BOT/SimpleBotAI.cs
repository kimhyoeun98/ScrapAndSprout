using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SimpleBotAI : MonoBehaviour
{
	[Header("AI 설정")]
	[SerializeField]
	private float _detectionRadius = 10f;

	[SerializeField]
	private float _arrivedThreshold = 0.5f;

	[SerializeField]
	private float _collectTime = 1f;

	private NavMeshAgent _agent;

	private GameObject _targetTrash;

	private bool _isCollecting;

	private void Awake()
	{
		_agent = GetComponent<NavMeshAgent>();
		if (_agent == null)
		{
			Debug.LogError("[SimpleBotAI] NavMeshAgent 없음!");
		}
	}

	private void Start()
	{
		Debug.Log("[SimpleBotAI] " + base.gameObject.name + " 시작!");
		FindAndMoveToTrash();
	}

	private void Update()
	{
		if (_agent == null || _isCollecting)
		{
			return;
		}
		if (_targetTrash != null)
		{
			if (Vector3.Distance(base.transform.position, _targetTrash.transform.position) <= _arrivedThreshold)
			{
				StartCoroutine(CollectTrash());
			}
		}
		else
		{
			FindAndMoveToTrash();
		}
	}

	private void FindAndMoveToTrash()
	{
		Collider[] array = Physics.OverlapSphere(base.transform.position, _detectionRadius, LayerMask.GetMask("Trash"));
		GameObject gameObject = null;
		float num = float.MaxValue;
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			float num2 = Vector3.Distance(base.transform.position, collider.transform.position);
			if (num2 < num)
			{
				num = num2;
				gameObject = collider.gameObject;
			}
		}
		if (gameObject != null)
		{
			_targetTrash = gameObject;
			_agent.isStopped = false;
			_agent.SetDestination(_targetTrash.transform.position);
			Debug.Log("[SimpleBotAI] " + base.gameObject.name + " 쓰레기 발견 → " + _targetTrash.name);
			return;
		}
		Vector3 sourcePosition = base.transform.position + Random.insideUnitSphere * 5f;
		sourcePosition.y = base.transform.position.y;
		if (NavMesh.SamplePosition(sourcePosition, out var hit, 5f, -1))
		{
			_agent.isStopped = false;
			_agent.SetDestination(hit.position);
			Debug.Log("[SimpleBotAI] " + base.gameObject.name + " 랜덤 이동");
		}
	}

	private IEnumerator CollectTrash()
	{
		_isCollecting = true;
		_agent.isStopped = true;
		Debug.Log("[SimpleBotAI] " + base.gameObject.name + " 수거 시작!");
		yield return new WaitForSeconds(_collectTime);
		if (_targetTrash != null)
		{
			Debug.Log("[SimpleBotAI] " + base.gameObject.name + " 쓰레기 수거 완료! " + _targetTrash.name + " 제거!");
			Object.Destroy(_targetTrash);
		}
		_targetTrash = null;
		_isCollecting = false;
		yield return new WaitForSeconds(0.5f);
		FindAndMoveToTrash();
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(base.transform.position, _detectionRadius);
		if (_targetTrash != null)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawLine(base.transform.position, _targetTrash.transform.position);
			Gizmos.DrawSphere(_targetTrash.transform.position, 0.3f);
		}
	}
}
