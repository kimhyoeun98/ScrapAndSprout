using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 키보드 입력 받기 (WASD / 방향키)
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        // 1. 입력받은 방향 그대로 벡터 생성 (보정 없음)
        Vector2 direction = new Vector2(moveInput.x, moveInput.y);

        // 2. 이동 실행
        if (direction.magnitude > 0.1f)
        {
            // 입력 방향 그대로 속도 적용
            rb.linearVelocity = direction.normalized * moveSpeed;
        }
        else
        {
            // 입력 없으면 즉시 정지
            rb.linearVelocity = Vector2.zero;
        }
    }
}
    