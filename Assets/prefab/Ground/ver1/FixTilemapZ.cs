using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tilemap의 모든 타일 Z 위치를 0으로 초기화
/// 사용 후 삭제해도 됨
/// </summary>
public class FixTilemapZ : MonoBehaviour
{
    [SerializeField] private Tilemap _tilemap;

    [ContextMenu("Fix All Tiles Z Position")]
    public void FixAllTilesZ()
    {
        if (_tilemap == null)
        {
            Debug.LogError("Tilemap이 할당되지 않았습니다!");
            return;
        }

        // Tilemap의 모든 타일 위치 가져오기
        BoundsInt bounds = _tilemap.cellBounds;
        TileBase[] allTiles = _tilemap.GetTilesBlock(bounds);

        int fixedCount = 0;

        // 모든 셀 순회
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int z = bounds.zMin; z < bounds.zMax; z++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, z);
                    TileBase tile = _tilemap.GetTile(cellPosition);

                    if (tile != null)
                    {
                        // Z=0인 위치로 타일 이동
                        Vector3Int newPosition = new Vector3Int(x, y, 0);
                        
                        // 기존 타일 제거
                        _tilemap.SetTile(cellPosition, null);
                        
                        // 새 위치에 타일 배치
                        _tilemap.SetTile(newPosition, tile);
                        
                        fixedCount++;
                    }
                }
            }
        }

        Debug.Log($"총 {fixedCount}개의 타일 Z 위치를 수정했습니다.");
    }
}