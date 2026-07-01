using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterWalkAnimator : MonoBehaviour
{
	[Tooltip("이 속도(월드/초) 이상이면 '걷는 중'으로 판정 — PlayerMovement 없을 때(봇) 폴백용")]
	public float minMoveSpeed = 0.1f;

	[Tooltip("이 캐릭터의 좌/우 스프라이트가 반대로 그려진 경우 체크 (예: 델타는 'right' 프레임이 왼쪽을 봄)")]
	public bool invertX;

	private const int DIR_DOWN = 0;

	private const int DIR_LEFT = 1;

	private const int DIR_UP = 2;

	private const int DIR_RIGHT = 3;

	private static readonly int DirHash = Animator.StringToHash("Dir");

	private Animator _animator;

	private PlayerMovement _pm;

	private AIBotController _bot;

	private Rigidbody2D _rb;

	private SpriteRenderer _sr;

	private int _lastDir;

	private Vector3 _lastPos;

	private void Awake()
	{
		_animator = GetComponent<Animator>();
		_pm = GetComponent<PlayerMovement>();
		_bot = GetComponent<AIBotController>();
		_rb = GetComponent<Rigidbody2D>();
		_sr = GetComponent<SpriteRenderer>();
		_lastPos = base.transform.position;
		if (_animator != null)
		{
			_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}
	}

	private void Update()
	{
		if (_animator == null)
		{
			return;
		}
		GetMotion(out var dir, out var moving);
		_lastDir = dir;
		// 좌/우 이동은 'right' 상태(3)를 재생하고 flipX로 좌향을 만든다.
		// 상/하(down/up)는 절대 뒤집지 않는다 (앞/뒤 스프라이트가 비대칭이므로).
		int num = (dir == DIR_LEFT ? DIR_RIGHT : dir);
		bool flip = false;
		if (dir == DIR_LEFT || dir == DIR_RIGHT)
		{
			// 일반 캐릭터: 왼쪽 이동 시 뒤집음. invertX(델타): 'right' 아트가 왼쪽을 보므로 반대로.
			flip = (invertX ? (dir == DIR_RIGHT) : (dir == DIR_LEFT));
		}
		if (_sr != null)
		{
			_sr.flipX = flip;
		}
		_animator.SetInteger(DirHash, num);
		_animator.speed = 1f;
		string stateName = StateName(num);
		AnimatorStateInfo currentAnimatorStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
		if (moving)
		{
			if (!currentAnimatorStateInfo.IsName(stateName))
			{
				_animator.Play(stateName, 0, 0f);
			}
		}
		else
		{
			_animator.Play(stateName, 0, 0f);
		}
	}

	private static string StateName(int dir)
	{
		return dir switch
		{
			1 => "left",
			2 => "up",
			3 => "right",
			_ => "down",
		};
	}

	private void GetMotion(out int dir, out bool moving)
	{
		// 봇은 스폰 직후 AIBotController가 추가되므로 지연 조회한다.
		if (_bot == null)
		{
			_bot = GetComponent<AIBotController>();
		}
		// 실제 플레이어(입력/네트워크로 IsMoving·MoveDir이 채워짐)만 PlayerMovement 값을 사용.
		// 봇은 PlayerMovement가 스폰 시점에 비활성화되지 못해 값이 갱신되지 않으므로 이동량으로 판정.
		if (_bot == null && _pm != null && _pm.enabled)
		{
			moving = _pm.IsMoving;
			dir = _pm.MoveDir;
			_lastPos = base.transform.position;
			return;
		}
		Vector3 pos = base.transform.position;
		Vector2 v = Vector2.zero;
		if (Time.deltaTime > 0f)
		{
			v = (pos - _lastPos) / Time.deltaTime;
		}
		_lastPos = pos;
		// 물리 속도가 있으면 우선 사용, 없으면 위치 변화량으로 대체 (호스트/클라이언트 모두 대응).
		if (_rb != null && _rb.linearVelocity.sqrMagnitude > v.sqrMagnitude)
		{
			v = _rb.linearVelocity;
		}
		moving = v.sqrMagnitude > minMoveSpeed * minMoveSpeed;
		dir = (moving ? DirFromVec(v) : _lastDir);
	}

	private static int DirFromVec(Vector2 v)
	{
		if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
		{
			if (!(v.x >= 0f))
			{
				return 1;
			}
			return 3;
		}
		if (!(v.y >= 0f))
		{
			return 0;
		}
		return 2;
	}
}
