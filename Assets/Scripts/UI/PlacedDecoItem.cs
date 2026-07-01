using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlacedDecoItem : MonoBehaviour
{
	private void OnMouseDown()
	{
		if (!(DecoPlacementController.Instance != null) || !DecoPlacementController.Instance.IsPlacing)
		{
			Vector3 localScale = base.transform.localScale;
			localScale.x *= -1f;
			base.transform.localScale = localScale;
		}
	}
}
