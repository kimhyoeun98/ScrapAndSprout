using System.Collections.Generic;
using UnityEngine;

public static class DecoCatalog
{
	public class Entry
	{
		public string key;

		public string set;

		public string typeKr;

		public string prefabPath;

		public int price;

		public int score;
	}

	public static readonly Entry[] Items;

	private static readonly Dictionary<string, Entry> _byKey;

	private static readonly Dictionary<string, string> _prefabRawToKey;

	public static IReadOnlyList<Entry> All => Items;

	private static int PriceOf(string t)
	{
		return t switch
		{
			"나무" => 40, 
			"상자" => 20, 
			"의자" => 30, 
			"울타리" => 50, 
			"꽃병" => 60, 
			"탁자" => 100, 
			"꽃밭" => 200, 
			_ => 50, 
		};
	}

	private static int ScoreOf(string t)
	{
		return t switch
		{
			"나무" => 40, 
			"상자" => 20, 
			"의자" => 40, 
			"울타리" => 50, 
			"꽃병" => 60, 
			"탁자" => 100, 
			"꽃밭" => 200, 
			_ => 20, 
		};
	}

	private static Entry E(string key, string set, string typeKr, string prefabBase)
	{
		return new Entry
		{
			key = key,
			set = set,
			typeKr = typeKr,
			prefabPath = "deco/" + prefabBase,
			price = PriceOf(typeKr),
			score = ScoreOf(typeKr)
		};
	}

	static DecoCatalog()
	{
		Items = new Entry[70]
		{
			E("나무풍 나무", "나무 세트", "나무", "deco1_WoodTree_0"),
			E("나무풍 상자", "나무 세트", "상자", "deco1_WoodenBox_0"),
			E("나무풍 의자", "나무 세트", "의자", "deco1_WoodChair_0"),
			E("나무풍 울타리", "나무 세트", "울타리", "deco1_WoodFence_0"),
			E("나무풍 꽃병", "나무 세트", "꽃병", "deco1_WoodVase_0"),
			E("나무풍 탁자", "나무 세트", "탁자", "deco1_WoodTable_0"),
			E("나무풍 꽃밭", "나무 세트", "꽃밭", "deco1_WoodFlowerField_0"),
			E("정원풍 나무", "정원 세트", "나무", "deco1_GardenTree_0"),
			E("정원풍 상자", "정원 세트", "상자", "deco1_GardenBox_0"),
			E("정원풍 의자", "정원 세트", "의자", "deco1_GardenChair_0"),
			E("정원풍 울타리", "정원 세트", "울타리", "deco1_GardenFence_0"),
			E("정원풍 꽃병", "정원 세트", "꽃병", "deco1_GardenVase_0"),
			E("정원풍 탁자", "정원 세트", "탁자", "deco1_GardenTable_0"),
			E("정원풍 꽃밭", "정원 세트", "꽃밭", "deco1_GardenFlowerField_0"),
			E("모던풍 나무", "모던 세트", "나무", "deco1_ModernTree_0"),
			E("모던풍 상자", "모던 세트", "상자", "deco1_ModernBox_0"),
			E("모던풍 의자", "모던 세트", "의자", "deco1_ModernChair_0"),
			E("모던풍 울타리", "모던 세트", "울타리", "deco1_ModernFence_0"),
			E("모던풍 꽃병", "모던 세트", "꽃병", "deco1_ModernVase_0"),
			E("모던풍 탁자", "모던 세트", "탁자", "deco1_ModernTable_0"),
			E("모던풍 꽃밭", "모던 세트", "꽃밭", "deco1_ModernFlowerField_0"),
			E("빈티지풍 나무", "빈티지 세트", "나무", "deco1_VintageTree_0"),
			E("빈티지풍 상자", "빈티지 세트", "상자", "deco1_VintageBox_0"),
			E("빈티지풍 의자", "빈티지 세트", "의자", "deco1_VintageChair_0"),
			E("빈티지풍 울타리", "빈티지 세트", "울타리", "deco1_VintageFence_0"),
			E("빈티지풍 꽃병", "빈티지 세트", "꽃병", "deco1_VintageVase_0"),
			E("빈티지풍 탁자", "빈티지 세트", "탁자", "deco1_VintageTable_0"),
			E("빈티지풍 꽃밭", "빈티지 세트", "꽃밭", "deco1_VintageFlowerField_0"),
			E("캐주얼풍 나무", "캐주얼 세트", "나무", "deco1_CasualTree_0"),
			E("캐주얼풍 상자", "캐주얼 세트", "상자", "deco1_CasualBox_0"),
			E("캐주얼풍 의자", "캐주얼 세트", "의자", "deco1_CasualChair_0"),
			E("캐주얼풍 울타리", "캐주얼 세트", "울타리", "deco1_CasualFence_0"),
			E("캐주얼풍 꽃병", "캐주얼 세트", "꽃병", "deco1_CasualVase_0"),
			E("캐주얼풍 탁자", "캐주얼 세트", "탁자", "deco1_CasualTable_0"),
			E("캐주얼풍 꽃밭", "캐주얼 세트", "꽃밭", "deco1_CasualFlowerField_0"),
			E("사이버펑크풍 나무", "사이버펑크 세트", "나무", "deco1_CyberpunkTree_0"),
			E("사이버펑크풍 상자", "사이버펑크 세트", "상자", "deco1_CyberpunkBox_0"),
			E("사이버펑크풍 의자", "사이버펑크 세트", "의자", "deco1_CyberpunkChair_0"),
			E("사이버펑크풍 울타리", "사이버펑크 세트", "울타리", "deco1_CyberpunkFence_0"),
			E("사이버펑크풍 꽃병", "사이버펑크 세트", "꽃병", "deco1_CyberpunkVase_0"),
			E("사이버펑크풍 탁자", "사이버펑크 세트", "탁자", "deco1_CyberpunkTable_0"),
			E("사이버펑크풍 꽃밭", "사이버펑크 세트", "꽃밭", "deco1_CyberpunkFlowerField_0"),
			E("큐티풍 나무", "큐티 세트", "나무", "deco1_CuteTree_0"),
			E("큐티풍 상자", "큐티 세트", "상자", "deco1_CuteBox_0"),
			E("큐티풍 의자", "큐티 세트", "의자", "deco1_CuteChair_0"),
			E("큐티풍 울타리", "큐티 세트", "울타리", "deco1_CuteFence_0"),
			E("큐티풍 꽃병", "큐티 세트", "꽃병", "deco1_CuteVase_0"),
			E("큐티풍 탁자", "큐티 세트", "탁자", "deco1_CuteTable_0"),
			E("큐티풍 꽃밭", "큐티 세트", "꽃밭", "deco1_CuteFlowerField_0"),
			E("보헤미안풍 나무", "보헤미안 세트", "나무", "deco1_BohemianTree_0"),
			E("보헤미안풍 상자", "보헤미안 세트", "상자", "deco1_BohemianBox_0"),
			E("보헤미안풍 의자", "보헤미안 세트", "의자", "deco1_BohemianChair_0"),
			E("보헤미안풍 울타리", "보헤미안 세트", "울타리", "deco1_BohemianFence_0"),
			E("보헤미안풍 꽃병", "보헤미안 세트", "꽃병", "deco1_BohemianVase_0"),
			E("보헤미안풍 탁자", "보헤미안 세트", "탁자", "deco1_BohemianTable_0"),
			E("보헤미안풍 꽃밭", "보헤미안 세트", "꽃밭", "deco1_BohemianFlowerField_0"),
			E("유럽풍 나무", "유럽 세트", "나무", "deco1_NordicTree_0"),
			E("유럽풍 상자", "유럽 세트", "상자", "deco1_NordicBox_0"),
			E("유럽풍 의자", "유럽 세트", "의자", "deco1_NordicChair_0"),
			E("유럽풍 울타리", "유럽 세트", "울타리", "deco1_NordicFence_0"),
			E("유럽풍 꽃병", "유럽 세트", "꽃병", "deco1_NordicVase_0"),
			E("유럽풍 탁자", "유럽 세트", "탁자", "deco1_NordicTable_0"),
			E("유럽풍 꽃밭", "유럽 세트", "꽃밭", "deco1_NordicFlowerField_0"),
			E("오션풍 나무", "오션 세트", "나무", "deco1_OceanTree_0"),
			E("오션풍 상자", "오션 세트", "상자", "deco1_OceanBox_0"),
			E("오션풍 의자", "오션 세트", "의자", "deco1_OceanChair_0"),
			E("오션풍 울타리", "오션 세트", "울타리", "deco1_OceanFence_0"),
			E("오션풍 꽃병", "오션 세트", "꽃병", "deco1_OceanVase_0"),
			E("오션풍 탁자", "오션 세트", "탁자", "deco1_OceanTable_0"),
			E("오션풍 꽃밭", "오션 세트", "꽃밭", "deco1_OceanFlowerField_0")
		};
		_byKey = new Dictionary<string, Entry>();
		_prefabRawToKey = new Dictionary<string, string>();
		Entry[] items = Items;
		foreach (Entry entry in items)
		{
			_byKey[entry.key] = entry;
			string key = entry.prefabPath.Substring("deco/".Length).Replace("_0", "");
			_prefabRawToKey[key] = entry.key;
		}
	}

	public static Entry Get(string key)
	{
		if (key == null || !_byKey.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool IsDeco(string key)
	{
		if (key != null)
		{
			return _byKey.ContainsKey(key);
		}
		return false;
	}

	public static int Price(string key)
	{
		return Get(key)?.price ?? 0;
	}

	public static int Score(string key)
	{
		return Get(key)?.score ?? 0;
	}

	public static string PrefabPath(string key)
	{
		return Get(key)?.prefabPath;
	}

	public static string KeyForPrefabRaw(string rawName)
	{
		if (rawName == null || !_prefabRawToKey.TryGetValue(rawName, out var value))
		{
			return null;
		}
		return value;
	}

	public static Color FallbackColor(string typeKr)
	{
		return typeKr switch
		{
			"나무" => new Color(0.13f, 0.55f, 0.13f), 
			"상자" => new Color(0.55f, 0.35f, 0.13f), 
			"의자" => new Color(0.7f, 0.45f, 0.2f), 
			"울타리" => new Color(0.6f, 0.6f, 0.6f), 
			"꽃병" => new Color(0.55f, 0.2f, 0.7f), 
			"탁자" => new Color(0.4f, 0.25f, 0.1f), 
			"꽃밭" => new Color(0.95f, 0.55f, 0.7f), 
			_ => Color.grey, 
		};
	}

	public static Vector2 FallbackScale(string typeKr)
	{
		return typeKr switch
		{
			"나무" => new Vector2(1.2f, 2f), 
			"상자" => new Vector2(1f, 1f), 
			"의자" => new Vector2(0.9f, 1.1f), 
			"울타리" => new Vector2(2f, 0.8f), 
			"꽃병" => new Vector2(0.7f, 1.2f), 
			"탁자" => new Vector2(1.5f, 0.9f), 
			"꽃밭" => new Vector2(2f, 1f), 
			_ => Vector2.one, 
		};
	}
}
