using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;

        bool isWalkingUp = moveInput.y > 0.1f && Mathf.Abs(moveInput.y) >= Mathf.Abs(moveInput.x);
        bool isWalkingSide = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y);

        if (spriteRenderer != null && isWalkingSide)
        {
            spriteRenderer.flipX = moveInput.x < 0f;
        }

        if (animator != null)
        {
            animator.SetBool("IsWalkingUp", isWalkingUp);
            animator.SetBool("IsWalkingSide", isWalkingSide);
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }
}