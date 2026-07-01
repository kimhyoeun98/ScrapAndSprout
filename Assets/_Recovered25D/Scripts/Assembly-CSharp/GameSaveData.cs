using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
	[Serializable]
	public class InvEntry
	{
		public string name;

		public int count;
	}

	[Serializable]
	public class DecoEntry
	{
		public string itemName;

		public float x;

		public float y;

		public float z;

		public bool flipped;
	}

	public int version = 1;

	public string savedAtIso;

	public float elapsedTime;

	public int teamDecorScore;

	public int[] playerDecorScores = new int[4];

	public List<string> placedItems = new List<string>();

	public List<InvEntry> inventory = new List<InvEntry>();

	public int gold;

	public List<DecoEntry> decorations = new List<DecoEntry>();

	public List<DecoEntry> decoRoomDecorations = new List<DecoEntry>();

	public bool hasHostPlayer;

	public float hostBattery;

	public float hostX;

	public float hostY;

	public float hostZ;
}
