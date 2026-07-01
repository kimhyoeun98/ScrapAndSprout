using UnityEngine;

public class PlayerNameTag : MonoBehaviour
{
	[Header("── 태그 설정 ──")]
	public Vector3 offset = new Vector3(0f, 0.8f, -0.1f);

	public int textSize = 55;

	private static Font _koreanFont;

	private static Font KoreanFont
	{
		get
		{
			if (_koreanFont == null)
			{
				_koreanFont = Font.CreateDynamicFontFromOSFont(new string[5] { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Gulim", "Arial Unicode MS" }, 55);
			}
			return _koreanFont;
		}
	}

	public void Setup(bool hasInputAuthority)
	{
		string text = base.gameObject.name.ToLower();
		string text2 = (text.Contains("alpha") ? "알파" : (text.Contains("beta") ? "베타" : (text.Contains("gamma") ? "감마" : ((!text.Contains("delta")) ? "플레이어" : "델타"))));
		bool flag = GetComponent<AIBotController>() != null && !hasInputAuthority;
		bool flag2 = hasInputAuthority && !flag;
		string text3;
		Color color;
		if (flag)
		{
			text3 = "[BOT] " + text2;
			color = new Color(0.65f, 0.65f, 0.65f);
		}
		else if (flag2)
		{
			string text4 = ((ApiManager.Instance != null && !string.IsNullOrEmpty(ApiManager.Instance.userName)) ? ApiManager.Instance.userName : text2);
			text3 = "[나] " + text4;
			color = new Color(1f, 0.9f, 0.2f);
		}
		else
		{
			text3 = text2;
			color = Color.white;
		}
		GameObject obj = new GameObject("NameTag");
		obj.transform.SetParent(base.transform);
		obj.transform.localPosition = offset;
		obj.transform.localRotation = Quaternion.identity;
		obj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
		AddTextMesh(obj.transform, "Shadow", text3, new Color(0f, 0f, 0f, 0.55f), new Vector3(0.6f, -0.6f, 0.05f));
		AddTextMesh(obj.transform, "Label", text3, color, Vector3.zero);
	}

	private static void AddTextMesh(Transform parent, string goName, string text, Color color, Vector3 localPos)
	{
		GameObject gameObject = new GameObject(goName);
		gameObject.transform.SetParent(parent);
		gameObject.transform.localPosition = localPos;
		gameObject.transform.localScale = Vector3.one;
		TextMesh textMesh = gameObject.AddComponent<TextMesh>();
		Font koreanFont = KoreanFont;
		if (koreanFont != null)
		{
			textMesh.font = koreanFont;
			MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
			if (component != null)
			{
				component.sharedMaterial = koreanFont.material;
			}
		}
		textMesh.text = text;
		textMesh.color = color;
		textMesh.fontSize = 55;
		textMesh.fontStyle = FontStyle.Bold;
		textMesh.alignment = TextAlignment.Center;
		textMesh.anchor = TextAnchor.MiddleCenter;
	}
}
