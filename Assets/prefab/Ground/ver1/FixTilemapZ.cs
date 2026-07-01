using UnityEngine;
using UnityEngine.Tilemaps;

public class FixTilemapZ : MonoBehaviour
{
	[SerializeField]
	private Tilemap _tilemap;

	[ContextMenu("Fix All Tiles Z Position")]
	public void FixAllTilesZ()
	{
		if (_tilemap == null)
		{
			Debug.LogError("Tilemap이 할당되지 않았습니다!");
			return;
		}
		BoundsInt cellBounds = _tilemap.cellBounds;
		_tilemap.GetTilesBlock(cellBounds);
		int num = 0;
		for (int i = cellBounds.xMin; i < cellBounds.xMax; i++)
		{
			for (int j = cellBounds.yMin; j < cellBounds.yMax; j++)
			{
				for (int k = cellBounds.zMin; k < cellBounds.zMax; k++)
				{
					Vector3Int position = new Vector3Int(i, j, k);
					TileBase tile = _tilemap.GetTile(position);
					if (tile != null)
					{
						Vector3Int position2 = new Vector3Int(i, j, 0);
						_tilemap.SetTile(position, null);
						_tilemap.SetTile(position2, tile);
						num++;
					}
				}
			}
		}
		Debug.Log($"총 {num}개의 타일 Z 위치를 수정했습니다.");
	}
}
