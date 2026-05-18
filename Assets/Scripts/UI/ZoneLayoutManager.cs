using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// TrashZone / SafeZone 구역 관리자
///
/// [역할]
/// 1. 두 구역의 월드 경계 정의
/// 2. 구역별 배경색 / 조명 전환 (페이드)
/// 3. UIManager에게 페이드 인/아웃 요청
/// 4. 카메라 Confiner 경계 갱신
///
/// [씬 레이아웃 기준]
///   TrashZone: x = -60 ~ 0
///   SafeZone:  x =  0 ~ 60
///   (타일 1칸 = 1 유닛, mapWidth=30 → 구역 폭 30유닛)
///
/// [싱글톤]
/// ZoneLayoutManager.Instance로 어디서든 접근
/// </summary>
public class ZoneLayoutManager : MonoBehaviour
{
    public static ZoneLayoutManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────
    //  구역 경계 설정
    // ─────────────────────────────────────────

    [Header("── 구역 경계 (World 좌표) ──")]
    [Tooltip("TrashZone 왼쪽 끝 X")]
    public float trashZoneLeft = -60f;

    [Tooltip("TrashZone 오른쪽 끝 X (= SafeZone 왼쪽 끝)")]
    public float zoneBorderX = 0f;

    [Tooltip("SafeZone 오른쪽 끝 X")]
    public float safeZoneRight = 60f;

    [Tooltip("바닥 Y 좌표")]
    public float groundY = -5f;

    [Tooltip("천장 Y 좌표")]
    public float ceilingY = 10f;

    // ─────────────────────────────────────────
    //  텔레포트 입구 위치 (Inspector에서 연결)
    // ─────────────────────────────────────────

    [Header("── 텔레포트 입구 위치 ──")]
    [Tooltip("TrashZone 텔레포트 도착 지점 (SafeZone→Trash 이동 시 스폰 위치)")]
    public Transform trashZoneEntrance;

    [Tooltip("SafeZone 텔레포트 도착 지점 (Trash→Safe 이동 시 스폰 위치)")]
    public Transform safeZoneEntrance;

    // ─────────────────────────────────────────
    //  구역별 조명 색상
    // ─────────────────────────────────────────

    [Header("── 구역별 조명 ──")]
    [Tooltip("TrashZone 글로벌 조명 색 (탁하고 어두운 느낌)")]
    public Color trashZoneLightColor = new Color(0.7f, 0.65f, 0.55f, 1f);

    [Tooltip("SafeZone 글로벌 조명 색 (밝고 따뜻한 느낌)")]
    public Color safeZoneLightColor = new Color(1f, 0.97f, 0.88f, 1f);

    [Tooltip("조명 전환 시간 (초)")]
    public float lightTransitionDuration = 0.8f;

    // ─────────────────────────────────────────
    //  카메라 Confiner 경계
    // ─────────────────────────────────────────

    [Header("── 카메라 경계 Collider ──")]
    [Tooltip("TrashZone 카메라 경계 PolygonCollider2D")]
    public PolygonCollider2D trashZoneConfiner;

    [Tooltip("SafeZone 카메라 경계 PolygonCollider2D")]
    public PolygonCollider2D safeZoneConfiner;

    // ─────────────────────────────────────────
    //  내부 참조
    // ─────────────────────────────────────────

    private Light2D _globalLight;
    private Coroutine _lightCoroutine;

    // 현재 어느 구역에 있는지
    public enum Zone { Trash, Safe }
    public Zone CurrentZone { get; private set; } = Zone.Trash;

    // ─────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────

    void Start()
    {
        // Global Light 2D 자동 탐색
        _globalLight = FindFirstObjectByType<Light2D>();

        // 시작 시 TrashZone 조명 적용
        ApplyZoneLight(Zone.Trash, instant: true);

        Debug.Log("[ZoneLayoutManager] 초기화 완료");
        Debug.Log($"  TrashZone: x={trashZoneLeft} ~ {zoneBorderX}");
        Debug.Log($"  SafeZone:  x={zoneBorderX} ~ {safeZoneRight}");
    }

    // ─────────────────────────────────────────
    //  구역 전환 (텔레포트 시 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 구역을 이동했을 때 호출.
    /// ZoneTeleporter가 위치 이동 후 이 함수를 호출하세요.
    /// </summary>
    public void OnZoneChanged(Zone newZone)
    {
        if (CurrentZone == newZone) return;
        CurrentZone = newZone;

        // 조명 전환
        ApplyZoneLight(newZone, instant: false);

        // 카메라 Confiner 전환 (Cinemachine 연동)
        UpdateCameraConfiner(newZone);

        Debug.Log($"[ZoneLayoutManager] 구역 전환 → {newZone}");
    }

    // ─────────────────────────────────────────
    //  조명 전환
    // ─────────────────────────────────────────

    void ApplyZoneLight(Zone zone, bool instant)
    {
        if (_globalLight == null) return;

        Color targetColor = (zone == Zone.Trash) ? trashZoneLightColor : safeZoneLightColor;

        if (instant)
        {
            _globalLight.color = targetColor;
            return;
        }

        if (_lightCoroutine != null)
            StopCoroutine(_lightCoroutine);

        _lightCoroutine = StartCoroutine(LerpLight(_globalLight.color, targetColor));
    }

    IEnumerator LerpLight(Color from, Color to)
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

    // ─────────────────────────────────────────
    //  카메라 Confiner 전환
    //
    //  [비유]
    //  카메라가 볼 수 있는 "창문 크기"를 구역마다 바꿔줍니다.
    //  TrashZone에 있을 때 카메라가 SafeZone을 보여주면 이상하니까요.
    // ─────────────────────────────────────────

    void UpdateCameraConfiner(Zone zone)
    {
        // Cinemachine Confiner2D를 찾아서 BoundingShape2D를 교체
        // CinemachineConfiner2D 컴포넌트를 직접 참조하면 더 좋지만
        // Cinemachine 버전에 따라 네임스페이스가 다를 수 있어 reflection으로 처리
        var confinerObj = GameObject.Find("CinemachineCamera");
        if (confinerObj == null) return;

        var confiner = confinerObj.GetComponent<Unity.Cinemachine.CinemachineConfiner2D>();
        if (confiner == null) return;

        confiner.BoundingShape2D = (zone == Zone.Trash)
            ? trashZoneConfiner
            : safeZoneConfiner;

        // 경계 캐시 재계산
        confiner.InvalidateBoundingShapeCache();

        Debug.Log($"[카메라] Confiner → {zone}Zone");
    }

    // ─────────────────────────────────────────
    //  편의 함수 — 현재 플레이어 위치로 구역 판별
    // ─────────────────────────────────────────

    public Zone GetZoneAt(float worldX)
    {
        return worldX < zoneBorderX ? Zone.Trash : Zone.Safe;
    }

    public bool IsInTrashZone(Vector3 position) => position.x < zoneBorderX;
    public bool IsInSafeZone(Vector3 position) => position.x >= zoneBorderX;

    // ─────────────────────────────────────────
    //  에디터 기즈모 — 구역 경계 시각화
    // ─────────────────────────────────────────

    void OnDrawGizmos()
    {
        float height = ceilingY - groundY;

        // TrashZone (빨간색)
        Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.15f);
        Gizmos.DrawCube(
            new Vector3((trashZoneLeft + zoneBorderX) / 2f, (groundY + ceilingY) / 2f, 0),
            new Vector3(zoneBorderX - trashZoneLeft, height, 0.1f)
        );
        Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawLine(
            new Vector3(trashZoneLeft, groundY, 0),
            new Vector3(trashZoneLeft, ceilingY, 0)
        );

        // SafeZone (초록색)
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.15f);
        Gizmos.DrawCube(
            new Vector3((zoneBorderX + safeZoneRight) / 2f, (groundY + ceilingY) / 2f, 0),
            new Vector3(safeZoneRight - zoneBorderX, height, 0.1f)
        );
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.8f);
        Gizmos.DrawLine(
            new Vector3(safeZoneRight, groundY, 0),
            new Vector3(safeZoneRight, ceilingY, 0)
        );

        // 경계선 (보라색)
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 1f);
        Gizmos.DrawLine(
            new Vector3(zoneBorderX, groundY, 0),
            new Vector3(zoneBorderX, ceilingY, 0)
        );
    }
}