using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class MapBorderWall : MonoBehaviour
{
	private void Awake()
	{
		PolygonCollider2D component = GetComponent<PolygonCollider2D>();
		if (component == null)
		{
			Debug.LogWarning("[MapBorderWall] PolygonCollider2D 없음");
			return;
		}
		int num = 0;
		for (int i = 0; i < component.pathCount; i++)
		{
			Vector2[] path = component.GetPath(i);
			if (path != null && path.Length >= 2)
			{
				List<Vector2> list = new List<Vector2>(path);
				list.Add(path[0]);
				base.gameObject.AddComponent<EdgeCollider2D>().points = list.ToArray();
				num++;
			}
		}
		component.enabled = false;
		Debug.Log($"[MapBorderWall] 경계 벽 생성 완료: Edge {num}개 (Polygon path {component.pathCount}개)");
	}
}
