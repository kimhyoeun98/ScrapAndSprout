using System;
using System.Collections.Generic;

[Serializable]
public class PCGResponse
{
	public int mapWidth;

	public int mapHeight;

	public float pollutionLevel;

	public string initialWeather;

	public List<TrashPoint> trashSpawnPoints;

	public List<PlantableArea> plantableAreas;

	public NPCPosition npcPosition;

	public List<ZoneData> zones;
}
