using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 꾸미기 아이템 시각 오브젝트 생성 담당
///
/// [씬 설정]
/// SafeZone 안의 빈 GameObject에 Add Component.
/// Inspector에서 각 아이템 스프라이트를 연결하세요.
/// 연결하지 않으면 색상 박스로 대체됩니다.
/// </summary>
public class DecoItemSpawner : MonoBehaviour
{
    public static DecoItemSpawner Instance { get; private set; }

    [Header("── 아이템 스프라이트 (미연결 시 색상 박스 사용) ──")]
    public Sprite spriteTree;
    public Sprite spriteBox;
    public Sprite spriteChair;
    public Sprite spriteFence;
    public Sprite spriteVase;
    public Sprite spriteTable;
    public Sprite spriteFlowerField;

    // 아이템별 대체 색상
    private static readonly Dictionary<string, Color> _itemColors = new()
    {
        { "나무",    new Color(0.13f, 0.55f, 0.13f) },
        { "상자",    new Color(0.55f, 0.35f, 0.13f) },
        { "의자",    new Color(0.70f, 0.45f, 0.20f) },
        { "울타리",  new Color(0.60f, 0.60f, 0.60f) },
        { "꽃병",    new Color(0.55f, 0.20f, 0.70f) },
        { "탁자",    new Color(0.40f, 0.25f, 0.10f) },
        { "꽃밭",    new Color(0.95f, 0.55f, 0.70f) },
    };

    // 아이템별 크기 배율
    private static readonly Dictionary<string, Vector2> _itemScales = new()
    {
        { "나무",    new Vector2(1.2f, 2.0f) },
        { "상자",    new Vector2(1.0f, 1.0f) },
        { "의자",    new Vector2(0.9f, 1.1f) },
        { "울타리",  new Vector2(2.0f, 0.8f) },
        { "꽃병",    new Vector2(0.7f, 1.2f) },
        { "탁자",    new Vector2(1.5f, 0.9f) },
        { "꽃밭",    new Vector2(2.0f, 1.0f) },
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    /// <summary>
    /// 꾸미기 아이템 시각 오브젝트 생성
    /// GameManager.RPC_PlaceDecoItem에서 모든 클라이언트에서 호출됩니다.
    /// </summary>
    public void CreateDecoObject(string itemName, Vector3 position)
    {
        var go = new GameObject($"Placed_{itemName}");
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 2;

        // 스프라이트 적용
        Sprite sprite = GetSprite(itemName);
        if (sprite != null)
        {
            sr.sprite = sprite;
        }
        else
        {
            // 폴백: 흰 정사각형 + 아이템 색상
            sr.sprite = CreateWhiteSquareSprite();
            sr.color = _itemColors.TryGetValue(itemName, out var c) ? c : Color.grey;
        }

        // 크기 적용
        if (_itemScales.TryGetValue(itemName, out var scale))
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        // 아이템 이름 라벨 (TMP 없이 간단하게)
        AddNameLabel(go.transform, itemName);

        Debug.Log($"[DecoItemSpawner] '{itemName}' 배치 완료 at {position}");
    }

    // ─────────────────────────────────────────
    //  정적 헬퍼 — GameManager RPC에서 Instance 없을 때 사용
    // ─────────────────────────────────────────

    public static void CreateStatic(string itemName, Vector3 position)
    {
        if (Instance != null)
        {
            Instance.CreateDecoObject(itemName, position);
            return;
        }

        // Instance 없으면 최소한의 오브젝트 생성
        var go = new GameObject($"Placed_{itemName}");
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSquareSprite();
        sr.color = _itemColors.TryGetValue(itemName, out var c) ? c : Color.grey;
        sr.sortingOrder = 2;

        if (_itemScales.TryGetValue(itemName, out var scale))
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    // ─────────────────────────────────────────
    //  스프라이트 조회
    // ─────────────────────────────────────────

    Sprite GetSprite(string itemName) => itemName switch
    {
        "나무"   => spriteTree,
        "상자"   => spriteBox,
        "의자"   => spriteChair,
        "울타리" => spriteFence,
        "꽃병"   => spriteVase,
        "탁자"   => spriteTable,
        "꽃밭"   => spriteFlowerField,
        _        => null
    };

    // ─────────────────────────────────────────
    //  1x1 흰 스프라이트 (폴백용)
    // ─────────────────────────────────────────

    static Sprite CreateWhiteSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f);
    }

    // ─────────────────────────────────────────
    //  이름 라벨 (TextMesh — TMP 불필요)
    // ─────────────────────────────────────────

    static void AddNameLabel(Transform parent, string itemName)
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent);
        labelGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        labelGo.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = itemName;
        tm.fontSize = 24;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
    }
}
