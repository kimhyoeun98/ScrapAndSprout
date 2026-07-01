using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneTeleporter : MonoBehaviour
{
	[Header("── 텔레포트 설정 ──")]
	public Transform destination;

	public float cooldown = 3f;

	public float fadeDuration = 0.3f;

	[Header("── 씬 전환 ──")]
	[Tooltip("체크하면 씬 전환, 미체크하면 위치 이동")]
	public bool teleportToScene;

	[Tooltip("이동할 씬 이름")]
	public string sceneName = "DecoScene";

	[Header("── 안내 UI ──")]
	public GameObject guideUI;

	private GameObject _playerInRange;

	private bool _isTeleporting;

	private bool _downKeyBuffered;

	private HashSet<GameObject> _cooldownPlayers = new HashSet<GameObject>();

	private void Start()
	{
		if (base.gameObject.name.Contains("DecoZone") || base.gameObject.name.Contains("Deco"))
		{
			teleportToScene = true;
			sceneName = "DecoScene";
			Debug.Log("[ZoneTeleporter] " + base.gameObject.name + " → DecoScene으로 자동 설정");
		}
		if (destination == null && !teleportToScene)
		{
			Debug.LogWarning("[ZoneTeleporter] destination 미연결!");
		}
		if (guideUI != null)
		{
			guideUI.SetActive(value: false);
		}
	}

	private void Update()
	{
		if (_playerInRange == null || _isTeleporting || _cooldownPlayers.Contains(_playerInRange) || (destination == null && !teleportToScene))
		{
			return;
		}
		PlayerMovement component = _playerInRange.GetComponent<PlayerMovement>();
		if (!(component == null) && component.HasInputAuthority)
		{
			UIManager.Instance?.ShowStatusMessage("T키를 눌러 텔레포트하세요", 0.5f);
			if (Input.GetKeyDown(KeyCode.T))
			{
				Debug.Log("[텔레포터] T키 감지 → 텔레포트 시작");
				StartCoroutine(TeleportRoutine(_playerInRange));
			}
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}
		PlayerMovement component = other.GetComponent<PlayerMovement>();
		if (!(component == null) && !(other.GetComponent<AIBotController>() != null) && !_cooldownPlayers.Contains(other.gameObject))
		{
			_playerInRange = other.gameObject;
			_downKeyBuffered = false;
			if (component.HasInputAuthority && guideUI != null)
			{
				guideUI.SetActive(value: true);
			}
			Debug.Log("[텔레포터] 플레이어 진입 — T키로 이동");
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (other.CompareTag("Player") && !(other.GetComponent<PlayerMovement>() == null) && !(other.gameObject != _playerInRange) && !_isTeleporting)
		{
			_playerInRange = null;
			_downKeyBuffered = false;
			if (guideUI != null)
			{
				guideUI.SetActive(value: false);
			}
		}
	}

	private IEnumerator BotTeleportRoutine(GameObject bot)
	{
		if (destination == null)
		{
			yield break;
		}
		_cooldownPlayers.Add(bot);
		AIBotController component = bot.GetComponent<AIBotController>();
		if (component != null)
		{
			component.ForceIdle();
		}
		yield return new WaitForSeconds(0.3f);
		Vector3 position = destination.position;
		position.z = -1.2f;
		bool flag = false;
		MonoBehaviour[] components = bot.GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in components)
		{
			if (monoBehaviour.GetType().Name == "NetworkRigidbody2D")
			{
				MethodInfo method = monoBehaviour.GetType().GetMethod("Teleport");
				if (method != null)
				{
					method.Invoke(monoBehaviour, new object[2] { position, null });
					flag = true;
					Debug.Log($"[텔레포터] 봇 NetworkRigidbody2D.Teleport() → {position}");
					break;
				}
			}
		}
		if (!flag)
		{
			Rigidbody2D component2 = bot.GetComponent<Rigidbody2D>();
			if (component2 != null)
			{
				component2.linearVelocity = Vector2.zero;
				component2.position = new Vector2(position.x, position.y);
			}
			bot.transform.position = position;
			Debug.Log($"[텔레포터] 봇 직접 이동 → {position}");
		}
		yield return new WaitForSeconds(cooldown);
		_cooldownPlayers.Remove(bot);
	}

	private IEnumerator TeleportRoutine(GameObject player)
	{
		_isTeleporting = true;
		_cooldownPlayers.Add(player);
		_downKeyBuffered = false;
		PlayerMovement pm = player.GetComponent<PlayerMovement>();
		if (fadeDuration > 0f)
		{
			if (pm != null)
			{
				pm.RPC_FadeOut(fadeDuration);
			}
			yield return new WaitForSeconds(fadeDuration);
		}
		if (teleportToScene)
		{
			Debug.Log("[텔레포트] " + sceneName + "로 이동");
			if (sceneName == "DecoScene" && PhotonManager.Instance != null)
			{
				float elapsedTime = ((GameManager.Instance != null) ? GameManager.Instance.ElapsedTime : 0f);
				int decorScore = ((GameManager.Instance != null) ? GameManager.Instance.SafeDecorScore : 0);
				DecoInventoryBridge.SaveReturnState(player.transform.position, elapsedTime, decorScore);
				PhotonManager.Instance.LoadDecoScene();
			}
			else
			{
				SceneManager.LoadScene(sceneName);
			}
			yield break;
		}
		Vector3 position = destination.position;
		position.z = -1.2f;
		if (pm != null)
		{
			pm.RPC_Teleport(position);
			Debug.Log($"[텔레포트] RPC_Teleport → {position}");
		}
		yield return new WaitForSeconds(0.3f);
		if (fadeDuration > 0f)
		{
			if (pm != null)
			{
				pm.RPC_FadeIn(fadeDuration);
			}
			yield return new WaitForSeconds(fadeDuration);
		}
		_playerInRange = null;
		if (guideUI != null)
		{
			guideUI.SetActive(value: false);
		}
		_isTeleporting = false;
		_downKeyBuffered = false;
		yield return new WaitForSeconds(cooldown);
		_cooldownPlayers.Remove(player);
	}

	private void OnDrawGizmos()
	{
		if (!(destination == null))
		{
			Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.8f);
			Gizmos.DrawLine(base.transform.position, destination.position);
			Gizmos.DrawWireSphere(destination.position, 0.5f);
			BoxCollider2D component = GetComponent<BoxCollider2D>();
			if (component != null)
			{
				Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.3f);
				Gizmos.DrawCube(base.transform.position + (Vector3)component.offset, component.size);
			}
		}
	}

	public void AddBotCooldown(GameObject bot)
	{
		StartCoroutine(BotInitialCooldown(bot));
	}

	private IEnumerator BotInitialCooldown(GameObject bot)
	{
		_cooldownPlayers.Add(bot);
		yield return new WaitForSeconds(15f);
		_cooldownPlayers.Remove(bot);
		Debug.Log("[텔레포터] 봇 초기 쿨다운 해제");
	}

	public void RequestBotTeleport(GameObject bot)
	{
		StartCoroutine(BotTeleportRoutine(bot));
	}

	public void TryTeleportByNetwork(GameObject player)
	{
		if (!_isTeleporting && !_cooldownPlayers.Contains(player) && !(destination == null) && !(_playerInRange != player))
		{
			Debug.Log("[Portal] TryTeleportByNetwork - player: " + player.name);
			StartCoroutine(TeleportRoutine(player));
		}
	}
}
