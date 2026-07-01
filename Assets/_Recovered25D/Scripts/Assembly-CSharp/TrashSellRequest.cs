using System;

[Serializable]
public class TrashSellRequest
{
	public string playerId;

	public string[] itemNames;

	public int[] itemCounts;

	public string characterType;
}
