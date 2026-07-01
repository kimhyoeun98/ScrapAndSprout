using System.Collections.Generic;
using UnityEngine;

public static class DecoInventoryBridge
{
	public struct PlacedDeco
	{
		public string itemName;

		public Vector3 position;

		public bool flipped;
	}

	public static Dictionary<string, int> SavedInventory { get; private set; } = new Dictionary<string, int>();

	public static int SavedGold { get; private set; }

	public static List<PlacedDeco> SavedDecorations { get; private set; } = new List<PlacedDeco>();

	public static bool IsReturningFromDeco { get; private set; }

	public static Vector3 ReturnPosition { get; private set; }

	public static float SavedElapsedTime { get; private set; }

	public static int SavedDecorScore { get; private set; }

	public static bool HasData => SavedInventory.Count > 0;

	public static void SaveFrom(TrashCollector collector)
	{
		SavedInventory = new Dictionary<string, int>(collector.inventory);
		SavedGold = collector.gold;
	}

	public static void AddDeco(string itemName, Vector3 position, bool flipped)
	{
		SavedDecorations.Add(new PlacedDeco
		{
			itemName = itemName,
			position = position,
			flipped = flipped
		});
	}

	public static void SaveReturnState(Vector3 playerPos, float elapsedTime, int decorScore)
	{
		ReturnPosition = playerPos;
		SavedElapsedTime = elapsedTime;
		SavedDecorScore = decorScore;
	}

	public static void PrepareReturn(TrashCollector collector)
	{
		SaveFrom(collector);
		IsReturningFromDeco = true;
	}

	public static void MarkReturning()
	{
		IsReturningFromDeco = true;
	}

	public static void ClearReturnFlag()
	{
		IsReturningFromDeco = false;
	}

	public static void RestoreTo(TrashCollector collector)
	{
		if (SavedInventory.Count > 0)
		{
			collector.inventory = new Dictionary<string, int>(SavedInventory);
		}
		if (SavedGold > 0)
		{
			collector.RPC_SetGold(SavedGold);
		}
	}

	public static void ClearDecorations()
	{
		SavedDecorations.Clear();
	}
}
