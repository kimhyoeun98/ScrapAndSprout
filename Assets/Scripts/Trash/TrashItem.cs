using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Scripting;

public class TrashItem : NetworkBehaviour
{
	private bool _isPlayerNearby;

	private TrashCollector _playerCollector;

	private bool _isCollected;

	private void Start()
	{
		StartCoroutine(CheckPlayerAfterSpawn());
	}

	private void Update()
	{
		if (_isPlayerNearby && _playerCollector != null && Input.GetKeyDown(KeyCode.E))
		{
			PlayerMovement component = _playerCollector.GetComponent<PlayerMovement>();
			if (component != null && component.HasInputAuthority)
			{
				TryCollect();
			}
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
				Debug.Log("[TrashItem] 플레이어 진입: " + base.gameObject.name);
			}
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (other.CompareTag("Player"))
		{
			PlayerMovement component = other.GetComponent<PlayerMovement>();
			if (!(component == null) && component.HasInputAuthority)
			{
				_isPlayerNearby = false;
				_playerCollector = null;
				UIManager.Instance?.OnTrashExit();
				Debug.Log("[TrashItem] 플레이어 이탈: " + base.gameObject.name);
			}
		}
	}

	private IEnumerator CheckPlayerAfterSpawn()
	{
		yield return null;
		Collider2D component = GetComponent<Collider2D>();
		if (component == null)
		{
			yield break;
		}
		float x = component.bounds.extents.x;
		Collider2D[] array = Physics2D.OverlapCircleAll(base.transform.position, x);
		foreach (Collider2D collider2D in array)
		{
			if (collider2D.CompareTag("Player"))
			{
				PlayerMovement component2 = collider2D.GetComponent<PlayerMovement>();
				if (!(component2 == null) && component2.HasInputAuthority)
				{
					_isPlayerNearby = true;
					_playerCollector = collider2D.GetComponent<TrashCollector>();
					UIManager.Instance?.OnTrashEnter();
					Debug.Log("[TrashItem] 스폰 시 플레이어 감지됨");
					break;
				}
			}
		}
	}

	private void TryCollect()
	{
		if (!_isCollected && !(_playerCollector == null))
		{
			PlayerMovement component = _playerCollector.GetComponent<PlayerMovement>();
			if (component != null && !component.CanAct)
			{
				UIManager.Instance?.ShowStatusMessage("배터리 방전! 수거 불가");
				return;
			}
			_isCollected = true;
			string text = base.gameObject.name.Replace("(Clone)", "").Trim();
			_playerCollector.RPC_AddItem(text);
			component?.RPC_DrainBattery(5f);
			UIManager.Instance?.OnTrashCollected();
			UIManager.Instance?.ShowStatusMessage(text + " 수거!", 1.5f);
			AudioManager.Instance?.PlayTrashCollect();
			Debug.Log("[수거] " + text + " 수거 완료!");
			RPC_NotifyTrashCollected();
		}
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	private void RPC_NotifyTrashCollected()
	{

		GameManager.Instance?.OnTrashCollected();
		base.Runner.Despawn(base.Object);
	}

}
