using UnityEngine;

public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.7f, 0.15f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Animation")]
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    [Header("Respawn")]
    [Tooltip("Pressed with Ctrl (Windows) or Cmd (macOS) held to reset the player to spawn.")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    private Rigidbody2D rb;
    private float moveInput;
    private bool jumpPressed;
    private bool isGrounded;
    private Vector2 spawnPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
        }

        if (Input.GetKeyDown(resetKey) && IsResetModifierHeld())
        {
            ResetToSpawn();
        }
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        Move();
        Jump();
        FlipVisual();
        UpdateAnimation();
    }

    private void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapBox(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );
    }

    private void Move()
    {
        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    private void Jump()
    {
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        jumpPressed = false;
    }

    private void FlipVisual()
    {
        if (bodySpriteRenderer == null)
        {
            return;
        }

        if (moveInput > 0.01f)
        {
            bodySpriteRenderer.flipX = false;
        }
        else if (moveInput < -0.01f)
        {
            bodySpriteRenderer.flipX = true;
        }
    }

    private void UpdateAnimation()
    {
        if (bodyAnimator == null)
        {
            return;
        }

        bodyAnimator.SetFloat("Speed", Mathf.Abs(moveInput));
        bodyAnimator.SetBool("IsGrounded", isGrounded);
        bodyAnimator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
    }

    private bool IsResetModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
    }

    private void ResetToSpawn()
    {
        // rb.position teleports cleanly without an interpolation smear.
        rb.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}