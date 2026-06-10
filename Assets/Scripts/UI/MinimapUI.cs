using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 좌측 상단 미니맵 — 플레이어/봇 위치 실시간 표시
/// 씬에 직접 배치된 UI 오브젝트를 Inspector에서 연결해서 사용
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("── 월드 X 범위 ──")]
    public float worldMinX = -150f;
    public float worldMaxX =  150f;

    [Header("── 진입 불가 구역 경계 ──")]
    public float boundaryLeftX  = -10f;   // 왼쪽 경계선 월드 X
    public float boundaryRightX =  10f;   // 오른쪽 경계선 월드 X

    [Header("── UI 참조 ──")]
    [SerializeField] private RectTransform mapArea;         // 도트가 찍히는 맵 영역
    [SerializeField] private RectTransform boundaryLeft;    // 왼쪽 경계선
    [SerializeField] private RectTransform boundaryRight;   // 오른쪽 경계선
    [SerializeField] private RectTransform boundaryZone;    // 경계 구간 배경 (선택)
    [SerializeField] private RectTransform safeZone;        // 세이프존 배경
    [SerializeField] private RectTransform trashLabel;      // 쓰레기존 텍스트
    [SerializeField] private RectTransform safeLabel;       // 세이프존 텍스트

    // ── 도트 색상 ──
    static readonly Color C_ME     = new Color(1.00f, 0.88f, 0.20f, 1.00f);
    static readonly Color C_PLAYER = new Color(0.88f, 0.85f, 0.78f, 1.00f);
    static readonly Color C_BOT    = new Color(0.52f, 0.52f, 0.52f, 1.00f);

    private readonly Dictionary<PlayerMovement, RectTransform> _dots = new();
    private float _timer;

    void Start()
    {
        ApplyBoundaryPositions();
    }

    // 경계선 및 존 영역을 월드 좌표 기준으로 자동 배치
    void ApplyBoundaryPositions()
    {
        float leftRatio  = Mathf.InverseLerp(worldMinX, worldMaxX, boundaryLeftX);
        float rightRatio = Mathf.InverseLerp(worldMinX, worldMaxX, boundaryRightX);

        // 왼쪽 경계선
        if (boundaryLeft != null)
        {
            boundaryLeft.anchorMin = new Vector2(leftRatio, 0f);
            boundaryLeft.anchorMax = new Vector2(leftRatio, 1f);
            boundaryLeft.offsetMin = boundaryLeft.offsetMax = Vector2.zero;
        }

        // 오른쪽 경계선
        if (boundaryRight != null)
        {
            boundaryRight.anchorMin = new Vector2(rightRatio, 0f);
            boundaryRight.anchorMax = new Vector2(rightRatio, 1f);
            boundaryRight.offsetMin = boundaryRight.offsetMax = Vector2.zero;
        }

        // 경계 구간 (두 경계선 사이, 진입 불가)
        if (boundaryZone != null)
        {
            boundaryZone.anchorMin = new Vector2(leftRatio,  0f);
            boundaryZone.anchorMax = new Vector2(rightRatio, 1f);
            boundaryZone.offsetMin = boundaryZone.offsetMax = Vector2.zero;
        }

        // 세이프존 (오른쪽 경계 ~ 맵 끝)
        if (safeZone != null)
        {
            safeZone.anchorMin = new Vector2(rightRatio, 0f);
            safeZone.anchorMax = new Vector2(1f,         1f);
            safeZone.offsetMin = safeZone.offsetMax = Vector2.zero;
        }

        // 쓰레기존 라벨 (왼쪽 ~ 왼쪽 경계)
        if (trashLabel != null)
        {
            trashLabel.anchorMin = new Vector2(0f,        0f);
            trashLabel.anchorMax = new Vector2(leftRatio, 1f);
            trashLabel.offsetMin = trashLabel.offsetMax = Vector2.zero;
        }

        // 세이프존 라벨 (오른쪽 경계 ~ 맵 끝)
        if (safeLabel != null)
        {
            safeLabel.anchorMin = new Vector2(rightRatio, 0f);
            safeLabel.anchorMax = new Vector2(1f,         1f);
            safeLabel.offsetMin = safeLabel.offsetMax = Vector2.zero;
        }
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.1f;
        RefreshDots();
    }

    void RefreshDots()
    {
        if (mapArea == null)
        {
            Debug.LogWarning("[MinimapUI] mapArea가 NULL — Inspector에서 MapArea 연결 필요");
            return;
        }

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        var alive   = new HashSet<PlayerMovement>(players);

        var toRemove = new List<PlayerMovement>();
        foreach (var kv in _dots)
        {
            if (!alive.Contains(kv.Key))
            {
                if (kv.Value) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var pm in toRemove) _dots.Remove(pm);

        foreach (var pm in players)
        {
            if (pm == null) continue;
            if (!_dots.TryGetValue(pm, out var dot) || dot == null)
                dot = CreateDot(pm);

            float nx = Mathf.Clamp01(Mathf.InverseLerp(worldMinX, worldMaxX, pm.transform.position.x));
            dot.anchorMin = dot.anchorMax = new Vector2(nx, 0.5f);
            dot.anchoredPosition = Vector2.zero;
        }
    }

    RectTransform CreateDot(PlayerMovement pm)
    {
        bool isBot = pm.GetComponent<AIBotController>() != null && !pm.HasInputAuthority;
        bool isMe  = pm.HasInputAuthority && !isBot;

        Color color = isBot ? C_BOT : isMe ? C_ME : C_PLAYER;
        float size  = isMe ? 12f : 9f;

        var go = new GameObject(isMe ? "MeDot" : isBot ? "BotDot" : "Dot",
            typeof(RectTransform), typeof(Image));
        go.transform.SetParent(mapArea, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        if (isMe)
        {
            var shadowGO = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGO.transform.SetParent(go.transform, false);
            var sr = shadowGO.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = Vector2.zero;
            sr.sizeDelta = new Vector2(size + 4f, size + 4f);
            var sImg = shadowGO.GetComponent<Image>();
            sImg.color = new Color(0f, 0f, 0f, 0.55f);
            sImg.raycastTarget = false;
            shadowGO.transform.SetAsFirstSibling();
        }

        _dots[pm] = rt;
        return rt;
    }
}
