using UnityEngine;

public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Jump Feel")]
    [Tooltip("Grace window (s) to still jump after walking off a ledge.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Window (s) a jump pressed just before landing stays queued.")]
    [SerializeField] private float jumpBufferTime = 0.1f;
    [Tooltip("Fraction of upward velocity kept when Jump is released early (short hop).")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Extra gravity factor applied while falling so the descent feels weighty.")]
    [SerializeField] private float fallMultiplier = 2.5f;

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
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool jumpReleased;
    private float controlLockoutTimer;
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
            jumpBufferCounter = jumpBufferTime;
        }

        if (Input.GetButtonUp("Jump"))
        {
            jumpReleased = true;
        }

        if (Input.GetKeyDown(resetKey) && IsResetModifierHeld())
        {
            ResetToSpawn();
        }
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        UpdateJumpTimers();
        Move();
        Jump();
        FlipVisual();
        UpdateAnimation();
    }

    private void UpdateJumpTimers()
    {
        coyoteTimeCounter = isGrounded
            ? coyoteTime
            : coyoteTimeCounter - Time.fixedDeltaTime;

        // Refreshed to jumpBufferTime in Update whenever Jump is pressed.
        jumpBufferCounter -= Time.fixedDeltaTime;

        // Counts down the window during which recoil momentum overrides Move().
        controlLockoutTimer -= Time.fixedDeltaTime;
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
        // While recoil control is locked out, leave horizontal velocity alone so the
        // impulse carries instead of being stomped by the per-step velocity set.
        if (controlLockoutTimer > 0f)
        {
            return;
        }

        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    // Entry point for the shotgun recoil. Kept here so all velocity mutation lives in one
    // place; ShotgunRecoil2D computes the impulse and calls in.
    public void ApplyRecoil(Vector2 impulse, float lockout, float maxSpeed)
    {
        rb.AddForce(impulse, ForceMode2D.Impulse);

        if (maxSpeed > 0f && rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        controlLockoutTimer = Mathf.Max(controlLockoutTimer, lockout);
    }

    private void Jump()
    {
        // Fire when a buffered press meets a valid (grounded or coyote) window.
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;   // consume the buffered press
            coyoteTimeCounter = 0f;   // prevent a second jump within the grace window
        }

        // Variable height: releasing Jump while still rising cuts the climb short.
        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                rb.linearVelocity.y * jumpCutMultiplier
            );
        }

        jumpReleased = false;

        // Fall multiplier: apply heavier gravity on the way down.
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up
                * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    // True when the body sprite is currently facing left. Holds the last facing while idle.
    // ShotgunRecoil2D reads this to mirror the arm with the body.
    public bool FacingLeft => bodySpriteRenderer != null && bodySpriteRenderer.flipX;

    public enum LocomotionState { Idle, Run, Jump }

    // Current locomotion, derived from the same signals that drive the animator. ShotgunRecoil2D
    // reads this to shift the arm anchor per animation type (run shoulder sits forward + down).
    public LocomotionState Locomotion =>
        !isGrounded ? LocomotionState.Jump
        : Mathf.Abs(moveInput) > 0.01f ? LocomotionState.Run
        : LocomotionState.Idle;

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