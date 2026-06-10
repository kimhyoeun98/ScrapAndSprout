using UnityEngine;

/// <summary>
/// 배치된 꾸미기 아이템에 자동으로 추가되는 컴포넌트.
/// 클릭하면 좌우 반전.
/// Collider2D가 있어야 OnMouseDown이 동작함.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlacedDecoItem : MonoBehaviour
{
    void OnMouseDown()
    {
        // 배치 모드 중에는 반전 클릭 무시
        if (DecoPlacementController.Instance != null && DecoPlacementController.Instance.IsPlacing)
            return;

        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }
}
