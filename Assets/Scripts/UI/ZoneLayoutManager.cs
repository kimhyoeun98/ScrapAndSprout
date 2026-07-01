using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ZoneLayoutManager : MonoBehaviour
{
	public enum Zone
	{
		Trash,
		Safe
	}

	[Header("── 구역 경계 (World 좌표) ──")]
	[Tooltip("TrashZone 왼쪽 끝 X")]
	public float trashZoneLeft = -60f;

	[Tooltip("TrashZone 오른쪽 끝 X (= SafeZone 왼쪽 끝)")]
	public float zoneBorderX;

	[Tooltip("SafeZone 오른쪽 끝 X")]
	public float safeZoneRight = 60f;

	[Tooltip("바닥 Y 좌표")]
	public float groundY = -5f;

	[Tooltip("천장 Y 좌표")]
	public float ceilingY = 10f;

	[Header("── 텔레포트 입구 위치 ──")]
	[Tooltip("TrashZone 텔레포트 도착 지점 (SafeZone→Trash 이동 시 스폰 위치)")]
	public Transform trashZoneEntrance;

	[Tooltip("SafeZone 텔레포트 도착 지점 (Trash→Safe 이동 시 스폰 위치)")]
	public Transform safeZoneEntrance;

	[Header("── 구역별 조명 ──")]
	[Tooltip("TrashZone 글로벌 조명 색 (탁하고 어두운 느낌)")]
	public Color trashZoneLightColor = new Color(0.7f, 0.65f, 0.55f, 1f);

	[Tooltip("SafeZone 글로벌 조명 색 (밝고 따뜻한 느낌)")]
	public Color safeZoneLightColor = new Color(1f, 0.97f, 0.88f, 1f);

	[Tooltip("조명 전환 시간 (초)")]
	public float lightTransitionDuration = 0.8f;

	[Header("── 카메라 경계 Collider ──")]
	[Tooltip("TrashZone 카메라 경계 PolygonCollider2D")]
	public PolygonCollider2D trashZoneConfiner;

	[Tooltip("SafeZone 카메라 경계 PolygonCollider2D")]
	public PolygonCollider2D safeZoneConfiner;

	private Light2D _globalLight;

	private Coroutine _lightCoroutine;

	public static ZoneLayoutManager Instance { get; private set; }

	public Zone CurrentZone { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		_globalLight = Object.FindFirstObjectByType<Light2D>();
		ApplyZoneLight(Zone.Trash, instant: true);
		Debug.Log("[ZoneLayoutManager] 초기화 완료");
		Debug.Log($"  TrashZone: x={trashZoneLeft} ~ {zoneBorderX}");
		Debug.Log($"  SafeZone:  x={zoneBorderX} ~ {safeZoneRight}");
	}

	public void OnZoneChanged(Zone newZone)
	{
		if (CurrentZone != newZone)
		{
			CurrentZone = newZone;
			ApplyZoneLight(newZone, instant: false);
			UpdateCameraConfiner(newZone);
			Debug.Log($"[ZoneLayoutManager] 구역 전환 → {newZone}");
		}
	}

	private void ApplyZoneLight(Zone zone, bool instant)
	{
		if (_globalLight == null)
		{
			return;
		}
		Color color = ((zone == Zone.Trash) ? trashZoneLightColor : safeZoneLightColor);
		if (instant)
		{
			_globalLight.color = color;
			return;
		}
		if (_lightCoroutine != null)
		{
			StopCoroutine(_lightCoroutine);
		}
		_lightCoroutine = StartCoroutine(LerpLight(_globalLight.color, color));
	}

	private IEnumerator LerpLight(Color from, Color to)
	{
		float elapsed = 0f;
		while (elapsed < lightTransitionDuration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / lightTransitionDuration;
			_globalLight.color = Color.Lerp(from, to, t);
			yield return null;
		}
		_globalLight.color = to;
		_lightCoroutine = null;
	}

	private void UpdateCameraConfiner(Zone zone)
	{
		GameObject gameObject = GameObject.Find("CinemachineCamera");
		if (!(gameObject == null))
		{
			CinemachineConfiner2D component = gameObject.GetComponent<CinemachineConfiner2D>();
			if (!(component == null))
			{
				component.BoundingShape2D = ((zone == Zone.Trash) ? trashZoneConfiner : safeZoneConfiner);
				component.InvalidateBoundingShapeCache();
				Debug.Log($"[카메라] Confiner → {zone}Zone");
			}
		}
	}

	public Zone GetZoneAt(float worldX)
	{
		if (!(worldX < zoneBorderX))
		{
			return Zone.Safe;
		}
		return Zone.Trash;
	}

	public bool IsInTrashZone(Vector3 position)
	{
		return position.x < zoneBorderX;
	}

	public bool IsInSafeZone(Vector3 position)
	{
		return position.x >= zoneBorderX;
	}

	private void OnDrawGizmos()
	{
		float y = ceilingY - groundY;
		Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.15f);
		Gizmos.DrawCube(new Vector3((trashZoneLeft + zoneBorderX) / 2f, (groundY + ceilingY) / 2f, 0f), new Vector3(zoneBorderX - trashZoneLeft, y, 0.1f));
		Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.8f);
		Gizmos.DrawLine(new Vector3(trashZoneLeft, groundY, 0f), new Vector3(trashZoneLeft, ceilingY, 0f));
		Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.15f);
		Gizmos.DrawCube(new Vector3((zoneBorderX + safeZoneRight) / 2f, (groundY + ceilingY) / 2f, 0f), new Vector3(safeZoneRight - zoneBorderX, y, 0.1f));
		Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.8f);
		Gizmos.DrawLine(new Vector3(safeZoneRight, groundY, 0f), new Vector3(safeZoneRight, ceilingY, 0f));
		Gizmos.color = new Color(0.6f, 0.4f, 1f, 1f);
		Gizmos.DrawLine(new Vector3(zoneBorderX, groundY, 0f), new Vector3(zoneBorderX, ceilingY, 0f));
	}
}
