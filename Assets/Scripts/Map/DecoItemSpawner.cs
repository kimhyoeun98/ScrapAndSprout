using UnityEngine;

public class DecoItemSpawner : MonoBehaviour
{
	[Header("── 아이템 스프라이트 (미연결 시 색상 박스 사용) ──")]
	public Sprite spriteTree;

	public Sprite spriteBox;

	public Sprite spriteChair;

	public Sprite spriteFence;

	public Sprite spriteVase;

	public Sprite spriteTable;

	public Sprite spriteFlowerField;

	public static DecoItemSpawner Instance { get; private set; }

	private static Color FallbackColor(string itemName)
	{
		DecoCatalog.Entry entry = DecoCatalog.Get(itemName);
		if (entry == null)
		{
			return Color.grey;
		}
		return DecoCatalog.FallbackColor(entry.typeKr);
	}

	private static Vector2 FallbackScale(string itemName)
	{
		DecoCatalog.Entry entry = DecoCatalog.Get(itemName);
		if (entry == null)
		{
			return Vector2.one;
		}
		return DecoCatalog.FallbackScale(entry.typeKr);
	}

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

	public void CreateDecoObject(string itemName, Vector3 position)
	{
		if (!TrySpawnPrefab(itemName, position))
		{
			GameObject obj = new GameObject("Placed_" + itemName);
			obj.transform.position = position;
			SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
			spriteRenderer.sortingLayerName = "Default";
			spriteRenderer.sortingOrder = 2;
			Sprite sprite = GetSprite(itemName);
			if (sprite != null)
			{
				spriteRenderer.sprite = sprite;
			}
			else
			{
				spriteRenderer.sprite = CreateWhiteSquareSprite();
				spriteRenderer.color = FallbackColor(itemName);
			}
			Vector2 vector = FallbackScale(itemName);
			obj.transform.localScale = new Vector3(vector.x, vector.y, 1f);
			Debug.Log($"[DecoItemSpawner] '{itemName}' 배치 완료 at {position}");
		}
	}

	public static void CreateStatic(string itemName, Vector3 position)
	{
		if (!TrySpawnPrefab(itemName, position))
		{
			if (Instance != null)
			{
				Instance.CreateDecoObject(itemName, position);
				return;
			}
			GameObject obj = new GameObject("Placed_" + itemName);
			obj.transform.position = position;
			SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = CreateWhiteSquareSprite();
			spriteRenderer.color = FallbackColor(itemName);
			spriteRenderer.sortingOrder = 2;
			Vector2 vector = FallbackScale(itemName);
			obj.transform.localScale = new Vector3(vector.x, vector.y, 1f);
		}
	}

	private static bool TrySpawnPrefab(string itemName, Vector3 position)
	{
		string text = DecoCatalog.PrefabPath(itemName);
		if (text == null)
		{
			return false;
		}
		GameObject gameObject = Resources.Load<GameObject>(text);
		if (gameObject == null)
		{
			return false;
		}
		Object.Instantiate(gameObject, position, Quaternion.identity).name = "Placed_" + itemName;
		Debug.Log($"[DecoItemSpawner] '{itemName}' 프리팹 배치 at {position}");
		return true;
	}

	private Sprite GetSprite(string itemName)
	{
		return itemName switch
		{
			"나무풍 나무" => spriteTree, 
			"나무풍 상자" => spriteBox, 
			"나무풍 의자" => spriteChair, 
			"나무풍 울타리" => spriteFence, 
			"나무풍 꽃병" => spriteVase, 
			"나무풍 탁자" => spriteTable, 
			"나무풍 꽃밭" => spriteFlowerField, 
			_ => null, 
		};
	}

	private static Sprite CreateWhiteSquareSprite()
	{
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, Color.white);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
	}
}
