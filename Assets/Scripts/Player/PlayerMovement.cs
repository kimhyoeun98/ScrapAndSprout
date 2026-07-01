using System;
using System.Collections;
using System.Reflection;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class PlayerMovement : NetworkBehaviour
{
	[Header("── 이동 설정 ──")]
	public float moveSpeed = 5f;

	public float lowBatterySpeedMultiplier = 0.5f;

	[Header("── 점프 설정 ──")]
	public float jumpForce = 10f;

	[Tooltip("바닥 감지 레이 발사 거리")]
	public float groundCheckDistance = 1f;

	[Tooltip("바닥 레이어 (Tilemap_Collision 오브젝트의 Layer)")]
	public LayerMask groundLayer;

	[Header("── 배터리 설정 ──")]
	[Range(0f, 100f)]
	public float batteryMax = 100f;

	public float chargeAmountPercent = 50f;

	[Header("── 배터리 UI 연결 ──")]
	public TextMeshProUGUI batteryText;

	public Image batteryBarImage;

	public GameObject batteryWarningUI;

	private Slider _batterySlider;

	private static bool _continuePlayerApplied;

	private Rigidbody2D _rb;

	private TrashCollector _trashCollector;

	private SpriteRenderer _spriteRenderer;

	private AIBotController _bot;

	private bool _botChecked;

	private bool _isGrounded;

	private bool _isReviving;

	[Networked]
	public float CurrentBattery { get; set; }

	[Networked]
	public NetworkBool FacingRight { get; set; }

	[Networked]
	public int MoveDir { get; set; }

	[Networked]
	public NetworkBool IsMoving { get; set; }

	private bool _isDead => CurrentBattery <= 0f;

	public bool CanAct => CurrentBattery > 0f;

	public bool IsMovementLocked { get; set; }

	private void Awake()
	{
		_rb = GetComponent<Rigidbody2D>();
		_trashCollector = GetComponent<TrashCollector>();
		_spriteRenderer = GetComponent<SpriteRenderer>();
	}

	public override void Spawned()
	{
		Debug.Log($"[Photon] 스폰 완료 - 내 캐릭터: {base.HasInputAuthority}");
		// 봇은 스폰 직후 AIBotController가 추가된다. PlayerMovement는 비활성화하지 않고,
		// 봇일 경우 FixedUpdateNetwork에서 AI가 계산한 이동을 대신 구동한다(네트워크 물리 경로 재사용).
		if (base.HasStateAuthority)
		{
			if (SaveManager.IsContinuing && base.HasInputAuthority && !_continuePlayerApplied && SaveManager.Pending != null && SaveManager.Pending.hasHostPlayer)
			{
				CurrentBattery = SaveManager.Pending.hostBattery;
			}
			else
			{
				CurrentBattery = batteryMax;
			}
			FacingRight = true;
			Debug.Log($"[배터리] 초기화: {CurrentBattery}%");
		}
		UpdateBatteryUI();
		CapsuleCollider2D component = GetComponent<CapsuleCollider2D>();
		if (component != null)
		{
			component.size = new Vector2(component.size.x, 1.5f);
		}
		if (SaveManager.IsContinuing && base.HasInputAuthority && !_continuePlayerApplied && SaveManager.Pending != null && SaveManager.Pending.hasHostPlayer)
		{
			_continuePlayerApplied = true;
			base.transform.position = new Vector3(SaveManager.Pending.hostX, SaveManager.Pending.hostY, -1.2f);
			Debug.Log($"[PlayerMovement] 이어하기 위치 복원 → {base.transform.position}");
		}
		else
		{
			base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y, -1.2f);
		}
		if (_rb != null)
		{
			_rb.gravityScale = 0f;
			_rb.constraints = RigidbodyConstraints2D.FreezeRotation;
		}
		PlayerNameTag playerNameTag = GetComponent<PlayerNameTag>();
		if (playerNameTag == null)
		{
			playerNameTag = base.gameObject.AddComponent<PlayerNameTag>();
		}
		playerNameTag.Setup(base.HasInputAuthority);
		if (!base.HasInputAuthority)
		{
			return;
		}
		GameObject gameObject = GameObject.Find("BatteryBar");
		if (gameObject == null)
		{
			Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
			if (canvas != null)
			{
				Transform transform = canvas.transform.Find("HUD");
				if (transform != null)
				{
					gameObject = transform.Find("BatteryBar")?.gameObject;
				}
			}
		}
		if (gameObject == null)
		{
			Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				Transform transform2 = array[i].transform.Find("BatteryBar");
				if (transform2 != null)
				{
					gameObject = transform2.gameObject;
					break;
				}
			}
		}
		if (gameObject != null)
		{
			_batterySlider = gameObject.GetComponent<Slider>();
			if (_batterySlider != null)
			{
				Debug.Log("[배터리] Slider 찾음");
			}
			Transform transform3 = gameObject.transform.Find("Fill Area");
			if (transform3 != null)
			{
				batteryBarImage = transform3.GetComponent<Image>();
				if (batteryBarImage != null)
				{
					Debug.Log("[배터리] Fill Area Image 찾음");
				}
			}
			Transform transform4 = gameObject.transform.Find("Background");
			if (transform4 != null)
			{
				transform4.GetComponent<Image>();
				Debug.Log("[배터리] Background Image 찾음");
			}
			Debug.Log($"[배터리] UI 연결 완료: Slider={_batterySlider != null}, FillImage={batteryBarImage != null}");
		}
		else
		{
			Debug.LogError("[배터리] BatteryBar GameObject를 찾을 수 없습니다");
		}
		MonoBehaviour[] array2 = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
		foreach (MonoBehaviour monoBehaviour in array2)
		{
			PropertyInfo property = monoBehaviour.GetType().GetProperty("Follow");
			if (property != null && property.PropertyType == typeof(Transform))
			{
				property.SetValue(monoBehaviour, base.transform);
				Debug.Log("[카메라] " + monoBehaviour.GetType().Name + " Follow 연결 성공!");
				break;
			}
		}
	}

	private void Update()
	{
		if (base.HasInputAuthority && !IsMovementLocked && Input.GetKeyDown(KeyCode.B) && !TrashZoneChat.IsTyping)
		{
			TryUseBatteryItem();
		}
	}

	public override void FixedUpdateNetwork()
	{
		if (base.HasStateAuthority && CurrentBattery < batteryMax)
		{
			float num = 0.5f * base.Runner.DeltaTime;
			CurrentBattery = Mathf.Min(batteryMax, CurrentBattery + num);
			if (base.HasInputAuthority && UnityEngine.Random.Range(0f, 1f) < 0.01f)
			{
				Debug.Log($"[배터리] 회복: {CurrentBattery:F1}% (+{num:F3})");
			}
		}
		if (!_botChecked)
		{
			_bot = GetComponent<AIBotController>();
			_botChecked = true;
		}
		// 봇: 입력이 없으므로 AIBotController가 계산한 이동을 상태 권한(호스트)에서 대신 구동한다.
		if (_bot != null)
		{
			if (base.HasStateAuthority)
			{
				DriveBot();
			}
			return;
		}
		if (!GetInput<NetworkInputData>(out var input))
		{
			return;
		}
		if (IsMovementLocked)
		{
			_rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
			return;
		}
		float num2 = (_isDead ? (moveSpeed * lowBatterySpeedMultiplier) : moveSpeed);
		Vector2 b = input.direction.normalized * num2;
		_rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, b, 0.5f);
		if (input.direction.x > 0.1f)
		{
			FacingRight = true;
		}
		else if (input.direction.x < -0.1f)
		{
			FacingRight = false;
		}
		Vector2 direction = input.direction;
		bool flag = direction.sqrMagnitude > 0.01f;
		IsMoving = flag;
		if (flag)
		{
			if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
			{
				MoveDir = ((!(direction.x >= 0f)) ? 1 : 3);
			}
			else
			{
				MoveDir = ((direction.y >= 0f) ? 2 : 0);
			}
		}
		if ((bool)input.teleport)
		{
			ZoneTeleporter[] array = UnityEngine.Object.FindObjectsByType<ZoneTeleporter>(FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				array[i].TryTeleportByNetwork(base.gameObject);
			}
		}
	}

	private void DriveBot()
	{
		if (IsMovementLocked)
		{
			_rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
			IsMoving = false;
			return;
		}
		Vector2 desired = _bot.DesiredVelocity;
		_rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, desired, 0.5f);
		bool moving = desired.sqrMagnitude > 0.01f;
		IsMoving = moving;
		if (desired.x > 0.1f)
		{
			FacingRight = true;
		}
		else if (desired.x < -0.1f)
		{
			FacingRight = false;
		}
		if (moving)
		{
			if (Mathf.Abs(desired.x) >= Mathf.Abs(desired.y))
			{
				MoveDir = ((!(desired.x >= 0f)) ? 1 : 3);
			}
			else
			{
				MoveDir = ((desired.y >= 0f) ? 2 : 0);
			}
		}
	}

	public override void Render()
	{
		UpdateBatteryUI();
		if (base.HasInputAuthority)
		{
			UIManager.Instance?.SetBatteryWarning(CurrentBattery > 0f && CurrentBattery <= 20f);
		}
	}

	public void DrainBattery(float amount)
	{
		if (!(CurrentBattery <= 0f) && base.HasStateAuthority)
		{
			CurrentBattery = Mathf.Max(0f, CurrentBattery - amount);
			if (CurrentBattery <= 0f)
			{
				OnBatteryDead();
			}
		}
	}

	public void ChargeBattery(float amount)
	{
		if (base.HasStateAuthority)
		{
			bool isDead = _isDead;
			CurrentBattery = Mathf.Min(batteryMax, CurrentBattery + amount);
			if (isDead && !_isDead)
			{
				UIManager.Instance?.ShowStatusMessage("배터리 충전! 정상 복구");
			}
		}
	}

	private void TryUseBatteryItem()
	{
		if (_trashCollector == null)
		{
			return;
		}
		if (!_trashCollector.inventory.ContainsKey("Battery") || _trashCollector.inventory["Battery"] <= 0)
		{
			UIManager.Instance?.ShowStatusMessage("배터리가 없습니다!");
			return;
		}
		if (CurrentBattery >= batteryMax)
		{
			UIManager.Instance?.ShowStatusMessage("배터리가 이미 가득 찼습니다.", 1.5f);
			return;
		}
		_trashCollector.inventory["Battery"]--;
		if (_trashCollector.inventory["Battery"] <= 0)
		{
			_trashCollector.inventory.Remove("Battery");
		}
		_trashCollector.RefreshUI();
		RPC_UseBatteryItem();
	}

	[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
	public void RPC_LockMovement()
	{

		IsMovementLocked = true;
	}

	[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
	public void RPC_UnlockMovement()
	{

		IsMovementLocked = false;
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	private void RPC_UseBatteryItem()
	{

		ChargeBattery(chargeAmountPercent);
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_Teleport(Vector3 targetPos)
	{

		targetPos.z = -1.2f;
		bool flag = false;
		MonoBehaviour[] components = GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in components)
		{
			if (monoBehaviour.GetType().Name == "NetworkRigidbody2D")
			{
				MethodInfo method = monoBehaviour.GetType().GetMethod("Teleport");
				if (method != null)
				{
					method.Invoke(monoBehaviour, new object[2] { targetPos, null });
					flag = true;
					Debug.Log($"[텔레포트] NetworkRigidbody2D.Teleport() → {targetPos}");
					break;
				}
			}
		}
		if (!flag)
		{
			Rigidbody2D component = GetComponent<Rigidbody2D>();
			if (component != null)
			{
				component.linearVelocity = Vector2.zero;
				component.position = new Vector2(targetPos.x, targetPos.y);
			}
			base.transform.position = targetPos;
			Debug.Log($"[텔레포트] 직접 이동 → {targetPos}");
		}
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_DrainBattery(float amount)
	{

		DrainBattery(amount);
	}

	private void OnBatteryDead()
	{
		UIManager.Instance?.ShowStatusMessage("배터리 방전! 30초 후 자동 부활", 3f);
		if (!_isReviving)
		{
			StartCoroutine(AutoReviveCoroutine());
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
	public void RPC_FadeOut(float duration)
	{

		UIManager.Instance?.FadeOut(duration);
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
	public void RPC_FadeIn(float duration)
	{

		UIManager.Instance?.FadeIn(duration);
	}

	private IEnumerator AutoReviveCoroutine()
	{
		_isReviving = true;
		yield return new WaitForSeconds(30f);
		if (base.HasStateAuthority)
		{
			ChargeBattery(30f);
		}
		_isReviving = false;
		UIManager.Instance?.ShowStatusMessage("자동 부활!");
	}

	public void SetBatteryFromServer(float serverBattery)
	{
		if (base.HasStateAuthority)
		{
			CurrentBattery = Mathf.Clamp(serverBattery, 0f, batteryMax);
		}
	}

	private void UpdateBatteryUI()
	{
		float num = CurrentBattery / batteryMax;
		Color color = ((num <= 0.2f) ? Color.red : ((num <= 0.5f) ? Color.yellow : Color.green));
		if (batteryText != null)
		{
			batteryText.text = $"⚡ {CurrentBattery:F0}%";
			batteryText.color = color;
		}
		if (_batterySlider != null)
		{
			_batterySlider.value = num;
			if (_batterySlider.fillRect != null)
			{
				Image component = _batterySlider.fillRect.GetComponent<Image>();
				if (component != null)
				{
					component.color = color;
				}
			}
		}
		if (batteryBarImage != null)
		{
			batteryBarImage.fillAmount = num;
			batteryBarImage.color = color;
		}
		if (batteryWarningUI != null)
		{
			batteryWarningUI.SetActive(CurrentBattery <= 30f);
		}
	}

	private void OnDrawGizmos()
	{
		CapsuleCollider2D component = GetComponent<CapsuleCollider2D>();
		float num = ((component != null) ? component.bounds.extents.y : 1f);
		Vector2 vector = new Vector2(base.transform.position.x, base.transform.position.y - num);
		Gizmos.color = (_isGrounded ? Color.green : Color.red);
		Gizmos.DrawLine(vector, vector + Vector2.down * groundCheckDistance);
	}

}
