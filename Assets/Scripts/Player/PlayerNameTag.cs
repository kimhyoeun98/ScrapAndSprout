using UnityEngine;

/// <summary>
/// 플레이어 머리 위 이름 태그 (MonoBehaviour)
///
/// [설치 방식]
/// PlayerMovement.Spawned()에서 AddComponent 후 Setup() 직접 호출.
/// Fusion Spawned() 콜백에 의존하지 않아 멀티 환경에서 안전.
/// </summary>
public class PlayerNameTag : MonoBehaviour
{
    [Header("── 태그 설정 ──")]
    public Vector3 offset  = new Vector3(0f, 0.8f, -0.1f);
    public int     textSize = 55;

    // ─────────────────────────────────────────

    /// <summary>PlayerMovement.Spawned()에서 직접 호출</summary>
    public void Setup(bool hasInputAuthority)
    {
        string goName = gameObject.name.ToLower();
        string charName;
        if      (goName.Contains("alpha")) charName = "알파";
        else if (goName.Contains("beta"))  charName = "베타";
        else if (goName.Contains("gamma")) charName = "감마";
        else if (goName.Contains("delta")) charName = "델타";
        else                               charName = "플레이어";

        bool isBot = GetComponent<AIBotController>() != null && !hasInputAuthority;
        bool isMe  = hasInputAuthority && !isBot;

        string displayName;
        Color  labelColor;

        if (isBot)
        {
            displayName = $"[BOT] {charName}";
            labelColor  = new Color(0.65f, 0.65f, 0.65f);
        }
        else if (isMe)
        {
            string userName = (ApiManager.Instance != null
                               && !string.IsNullOrEmpty(ApiManager.Instance.userName))
                ? ApiManager.Instance.userName
                : charName;
            displayName = $"[나] {userName}";
            labelColor  = new Color(1f, 0.9f, 0.2f);
        }
        else
        {
            displayName = charName;
            labelColor  = Color.white;
        }

        var root = new GameObject("NameTag");
        root.transform.SetParent(transform);
        root.transform.localPosition = offset;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = new Vector3(0.05f, 0.05f, 0.05f);

        AddTextMesh(root.transform, "Shadow", displayName,
            new Color(0f, 0f, 0f, 0.55f), new Vector3(0.6f, -0.6f, 0.05f));

        AddTextMesh(root.transform, "Label", displayName,
            labelColor, Vector3.zero);
    }

    static void AddTextMesh(Transform parent, string goName,
        string text, Color color, Vector3 localPos)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one;

        var tm        = go.AddComponent<TextMesh>();
        tm.text       = text;
        tm.color      = color;
        tm.fontSize   = 55;
        tm.fontStyle  = FontStyle.Bold;
        tm.alignment  = TextAlignment.Center;
        tm.anchor     = TextAnchor.MiddleCenter;
    }
}
