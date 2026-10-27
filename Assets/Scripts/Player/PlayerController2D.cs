using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Header("Speed Limits")]
    [Tooltip("Caps horizontal speed (units/s). Bounds how far a horizontal recoil dash can "
        + "accumulate. Walking is unaffected as long as this stays above moveSpeed. 0 = uncapped.")]
    [SerializeField] private float maxHorizontalSpeed = 18f;
    [Tooltip("Terminal fall speed (units/s). Caps how fast gravity can pull the player down. "
        + "Upward launches are bounded by the recoil/jump force itself (vertical recoil SETS "
        + "velocity rather than adding), so only the fall is capped here. 0 = uncapped.")]
    [SerializeField] private float maxFallSpeed = 20f;

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
    private float conveyorVelocity;
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
        ClampSpeed();
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
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );
        isGrounded = hits.Length > 0;

        // Sample the tile under the feet for a conveyor push (applied in Move). A small downward
        // offset lands the lookup in the surface tile's cell rather than the empty cell above it.
        conveyorVelocity = 0f;
        foreach (Collider2D hit in hits)
        {
            Tilemap tilemap = hit.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                tilemap = hit.GetComponentInParent<Tilemap>();
            }
            if (tilemap == null)
            {
                continue;
            }

            Vector3Int cell = tilemap.WorldToCell(groundCheck.position + Vector3.down * 0.1f);
            if (tilemap.GetTile(cell) is AnimatedTile2D tile && tile.PushSpeed != 0f)
            {
                conveyorVelocity = tile.PushSpeed;
                break;
            }
        }
    }

    private void Move()
    {
        // While recoil control is locked out, leave horizontal velocity alone so the
        // impulse carries instead of being stomped by the per-step velocity set.
        if (controlLockoutTimer > 0f)
        {
            return;
        }

        // moveInput * moveSpeed is the walk; conveyorVelocity is the belt drift under the feet
        // (0 when not on a conveyor or airborne). ClampSpeed bounds the total.
        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed + conveyorVelocity,
            rb.linearVelocity.y
        );
    }

    // Entry point for the shotgun recoil. Kept here so all velocity mutation lives in one place;
    // ShotgunRecoil2D computes the impulse and calls in. The impulse is decomposed per axis:
    //   - Horizontal is ADDITIVE momentum (a sideways shot carries / chains).
    //   - Vertical is a "double jump": it CANCELS current vertical velocity and SETS it to the
    //     recoil's vertical part, so every up-shot launches consistently and can't stack to
    //     infinity. A purely horizontal shot has impulse.y == 0 and never touches vertical
    //     (and vice-versa).
    public void ApplyRecoil(Vector2 impulse, float lockout)
    {
        Vector2 v = rb.linearVelocity;

        // Horizontal: add to existing momentum.
        v.x += impulse.x;

        // Vertical: cancel-and-set "double jump", using the recoil's actual vertical component. A
        // diagonal shot therefore gives a proportionally smaller vertical launch (~0.71x force) plus
        // its horizontal half (added above). A purely horizontal shot has impulse.y == 0 and never
        // touches vertical.
        if (Mathf.Abs(impulse.y) > 0.0001f)
        {
            v.y = impulse.y;
        }

        rb.linearVelocity = v;
        ClampSpeed();

        // Only yield horizontal control when the shot actually pushed sideways — a pure vertical
        // shot keeps full air control so the double jump feels responsive.
        if (Mathf.Abs(impulse.x) > 0.0001f)
        {
            controlLockoutTimer = Mathf.Max(controlLockoutTimer, lockout);
        }
    }

    // Per-axis speed limits. Horizontal is clamped symmetrically; vertical only caps the fall
    // (terminal velocity) — upward launches are already bounded by the recoil/jump force since
    // vertical recoil sets velocity rather than accumulating it.
    private void ClampSpeed()
    {
        Vector2 v = rb.linearVelocity;

        if (maxHorizontalSpeed > 0f)
        {
            v.x = Mathf.Clamp(v.x, -maxHorizontalSpeed, maxHorizontalSpeed);
        }

        if (maxFallSpeed > 0f && v.y < -maxFallSpeed)
        {
            v.y = -maxFallSpeed;
        }

        rb.linearVelocity = v;
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

    // Public so PlayerHealth2D can respawn the player on death (and the reset hotkey uses it).
    public void ResetToSpawn()
    {
        // Set BOTH the transform and the body so the teleport is visible THIS frame. rb.position
        // alone only applies on the next physics step — which never arrives while a hit-stop has
        // Time.timeScale at 0 — leaving transform.position stale and the death-plane check
        // re-firing every frame (player frozen under the map).
        transform.position = spawnPosition;
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