using System.Collections.Generic;

public static class SetDefinitions
{
	public class Set
	{
		public string name;

		public HashSet<string> items;
	}

	public static readonly Set[] Sets = new Set[10]
	{
		new Set
		{
			name = "나무 세트",
			items = new HashSet<string> { "나무풍 나무", "나무풍 상자", "나무풍 의자", "나무풍 울타리", "나무풍 꽃병", "나무풍 탁자", "나무풍 꽃밭" }
		},
		new Set
		{
			name = "정원 세트",
			items = new HashSet<string> { "정원풍 나무", "정원풍 상자", "정원풍 의자", "정원풍 울타리", "정원풍 꽃병", "정원풍 탁자", "정원풍 꽃밭" }
		},
		new Set
		{
			name = "모던 세트",
			items = new HashSet<string> { "모던풍 나무", "모던풍 상자", "모던풍 의자", "모던풍 울타리", "모던풍 꽃병", "모던풍 탁자", "모던풍 꽃밭" }
		},
		new Set
		{
			name = "빈티지 세트",
			items = new HashSet<string> { "빈티지풍 나무", "빈티지풍 상자", "빈티지풍 의자", "빈티지풍 울타리", "빈티지풍 꽃병", "빈티지풍 탁자", "빈티지풍 꽃밭" }
		},
		new Set
		{
			name = "캐주얼 세트",
			items = new HashSet<string> { "캐주얼풍 나무", "캐주얼풍 상자", "캐주얼풍 의자", "캐주얼풍 울타리", "캐주얼풍 꽃병", "캐주얼풍 탁자", "캐주얼풍 꽃밭" }
		},
		new Set
		{
			name = "사이버펑크 세트",
			items = new HashSet<string> { "사이버펑크풍 나무", "사이버펑크풍 상자", "사이버펑크풍 의자", "사이버펑크풍 울타리", "사이버펑크풍 꽃병", "사이버펑크풍 탁자", "사이버펑크풍 꽃밭" }
		},
		new Set
		{
			name = "큐티 세트",
			items = new HashSet<string> { "큐티풍 나무", "큐티풍 상자", "큐티풍 의자", "큐티풍 울타리", "큐티풍 꽃병", "큐티풍 탁자", "큐티풍 꽃밭" }
		},
		new Set
		{
			name = "보헤미안 세트",
			items = new HashSet<string> { "보헤미안풍 나무", "보헤미안풍 상자", "보헤미안풍 의자", "보헤미안풍 울타리", "보헤미안풍 꽃병", "보헤미안풍 탁자", "보헤미안풍 꽃밭" }
		},
		new Set
		{
			name = "유럽 세트",
			items = new HashSet<string> { "유럽풍 나무", "유럽풍 상자", "유럽풍 의자", "유럽풍 울타리", "유럽풍 꽃병", "유럽풍 탁자", "유럽풍 꽃밭" }
		},
		new Set
		{
			name = "오션 세트",
			items = new HashSet<string> { "오션풍 나무", "오션풍 상자", "오션풍 의자", "오션풍 울타리", "오션풍 꽃병", "오션풍 탁자", "오션풍 꽃밭" }
		}
	};

	public static List<Set> GetCompletedSets(HashSet<string> placedItems)
	{
		List<Set> list = new List<Set>();
		Set[] sets = Sets;
		foreach (Set set in sets)
		{
			if (placedItems.IsSupersetOf(set.items))
			{
				list.Add(set);
			}
		}
		return list;
	}

	public static float GetSetBonusMultiplier(int completedSetCount)
	{
		float num = 1f;
		for (int i = 0; i < completedSetCount; i++)
		{
			num *= 1.05f;
		}
		return num;
	}
}
