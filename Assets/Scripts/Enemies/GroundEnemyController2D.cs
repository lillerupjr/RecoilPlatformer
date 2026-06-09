using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth2D))]
public abstract class GroundEnemyController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimator2D animatorBridge;
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private Transform attackPoint;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3f;
    [SerializeField] private int initialFacingDirection = 1;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float loseTargetRange = 8f;
    [SerializeField] private LayerMask targetLayer;

    [Header("Ground Checks")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.35f, 0.12f);
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.12f, 0.55f);
    [SerializeField] private Vector2 ledgeCheckSize = new Vector2(0.35f, 0.12f);
    [SerializeField] private float minimumDirectionalCheckOffset = 0.05f;
    [SerializeField] private float turnCooldown = 0.12f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float attackActiveTime = 0.15f;
    [SerializeField] private float attackRecovery = 0.35f;

    [Header("Death")]
    [Tooltip("Seconds the corpse lingers before it despawns.")]
    [SerializeField] private float despawnDelay = 5f;

    private Rigidbody2D rb;
    private EnemyHealth2D health;
    private Transform target;
    private int facingDirection;
    private float wallCheckLocalX;
    private float ledgeCheckLocalX;
    private float attackPointLocalX;
    private float attackCooldownTimer;
    private float turnCooldownTimer;
    private float attackTimer;
    private bool attackHitApplied;
    private bool missingReceiverLoggedThisAttack;
    private EnemyState state;

    protected enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Dead
    }

    protected Rigidbody2D Rigidbody => rb;
    protected Transform CurrentTarget => target;
    protected int FacingDirection => facingDirection;
    protected bool IsDead => state == EnemyState.Dead;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth2D>();
        facingDirection = initialFacingDirection >= 0 ? 1 : -1;
        wallCheckLocalX = GetAbsoluteLocalX(wallCheck);
        ledgeCheckLocalX = GetAbsoluteLocalX(ledgeCheck);
        attackPointLocalX = GetAbsoluteLocalX(attackPoint);
        ApplyFacingDirection();
    }

    protected virtual void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<EnemyHealth2D>();
        }

        health.Damaged += HandleDamaged;
        health.Died += HandleDied;
    }

    protected virtual void OnDisable()
    {
        if (health == null)
        {
            return;
        }

        health.Damaged -= HandleDamaged;
        health.Died -= HandleDied;
    }

    protected virtual void FixedUpdate()
    {
        if (state == EnemyState.Dead)
        {
            StopHorizontalMovement();
            return;
        }

        attackCooldownTimer -= Time.fixedDeltaTime;
        turnCooldownTimer -= Time.fixedDeltaTime;

        if (state == EnemyState.Attack)
        {
            UpdateAttack();
            return;
        }

        AcquireOrReleaseTarget();

        if (target != null && IsTargetInAttackRange() && attackCooldownTimer <= 0f)
        {
            StartAttack();
            return;
        }

        if (target != null)
        {
            state = EnemyState.Chase;
            ChaseTarget();
        }
        else
        {
            state = EnemyState.Patrol;
            Patrol();
        }

        UpdateAnimatorSpeed();
    }

    protected virtual void Patrol()
    {
        if (!HasGroundBelow())
        {
            StopHorizontalMovement();
            return;
        }

        if (turnCooldownTimer <= 0f && ShouldTurnAround())
        {
            TurnAround();
            turnCooldownTimer = turnCooldown;
        }

        MoveHorizontally(patrolSpeed);
    }

    protected virtual void ChaseTarget()
    {
        float xDelta = target.position.x - transform.position.x;
        if (Mathf.Abs(xDelta) > 0.05f)
        {
            SetFacingDirection(xDelta > 0f ? 1 : -1);
        }

        if (!HasGroundBelow() || (HasUsableLedgeCheck() && !HasGroundAhead()))
        {
            StopHorizontalMovement();
            return;
        }

        MoveHorizontally(chaseSpeed);
    }

    protected virtual void StartAttack()
    {
        state = EnemyState.Attack;
        attackTimer = attackWindup + attackActiveTime + attackRecovery;
        attackHitApplied = false;
        missingReceiverLoggedThisAttack = false;
        StopHorizontalMovement();
        animatorBridge?.SetSpeed(0f);
        animatorBridge?.TriggerAttack();
        OnAttackStarted();
    }

    protected virtual void UpdateAttack()
    {
        StopHorizontalMovement();

        float elapsed = attackWindup + attackActiveTime + attackRecovery - attackTimer;
        if (!attackHitApplied && elapsed >= attackWindup)
        {
            ApplyAttackHit();
            attackHitApplied = true;
        }

        attackTimer -= Time.fixedDeltaTime;
        if (attackTimer > 0f)
        {
            return;
        }

        attackCooldownTimer = attackCooldown;
        state = target != null ? EnemyState.Chase : EnemyState.Patrol;
        OnAttackFinished();
    }

    protected virtual void ApplyAttackHit()
    {
        if (attackPoint == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRange,
            targetLayer
        );

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IPlayerDamageReceiver2D receiver = hit.GetComponentInParent<IPlayerDamageReceiver2D>();
            if (receiver == null)
            {
                LogMissingDamageReceiver(hit);
                continue;
            }

            EnemyHitInfo2D hitInfo = CreateHitInfo(hit);
            OnAttackHit(receiver, hitInfo);
            return;
        }
    }

    protected virtual EnemyHitInfo2D CreateHitInfo(Collider2D hit)
    {
        Vector2 point = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        Vector2 direction = new Vector2(facingDirection, 0f);
        return new EnemyHitInfo2D(attackDamage, gameObject, point, direction);
    }

    protected virtual void OnAttackHit(IPlayerDamageReceiver2D receiver, EnemyHitInfo2D hitInfo)
    {
        receiver.ReceiveEnemyHit(hitInfo);
    }

    protected virtual void OnAttackStarted()
    {
    }

    protected virtual void OnAttackFinished()
    {
    }

    protected virtual void HandleDamaged(DamageInfo2D damage)
    {
        animatorBridge?.TriggerHurt();
    }

    protected virtual void HandleDied(DamageInfo2D damage)
    {
        state = EnemyState.Dead;
        target = null;
        StopHorizontalMovement();
        animatorBridge?.SetSpeed(0f);
        animatorBridge?.SetDead(true);

        // Stop the corpse from blocking/pushing the player, but keep it resting on the ground
        // (only the player's layer is excluded, not Ground).
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.excludeLayers = col.excludeLayers.value | targetLayer.value;
        }

        Destroy(gameObject, despawnDelay);
    }

    private void AcquireOrReleaseTarget()
    {
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.position);
            if (distance <= loseTargetRange)
            {
                return;
            }

            target = null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, targetLayer);
        float bestSqrDistance = float.PositiveInfinity;
        Transform bestTarget = null;

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }

            float sqrDistance = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
            {
                continue;
            }

            bestSqrDistance = sqrDistance;
            bestTarget = hit.transform;
        }

        target = bestTarget;
    }

    private bool IsTargetInAttackRange()
    {
        if (target == null || attackPoint == null)
        {
            return false;
        }

        return Vector2.Distance(attackPoint.position, target.position) <= attackRange;
    }

    private bool ShouldTurnAround()
    {
        return (HasUsableWallCheck() && HasWallAhead())
            || (HasUsableLedgeCheck() && !HasGroundAhead());
    }

    private bool HasGroundBelow()
    {
        return groundCheck != null
            && Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer) != null;
    }

    private bool HasWallAhead()
    {
        return wallCheck != null
            && Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, groundLayer) != null;
    }

    private bool HasGroundAhead()
    {
        return ledgeCheck != null
            && Physics2D.OverlapBox(ledgeCheck.position, ledgeCheckSize, 0f, groundLayer) != null;
    }

    private bool HasUsableWallCheck()
    {
        return wallCheck != null && wallCheckLocalX > minimumDirectionalCheckOffset;
    }

    private bool HasUsableLedgeCheck()
    {
        return ledgeCheck != null && ledgeCheckLocalX > minimumDirectionalCheckOffset;
    }

    private void MoveHorizontally(float speed)
    {
        rb.linearVelocity = new Vector2(facingDirection * speed, rb.linearVelocity.y);
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        animatorBridge?.SetSpeed(0f);
    }

    private void TurnAround()
    {
        SetFacingDirection(-facingDirection);
    }

    private void SetFacingDirection(int direction)
    {
        if (direction == 0 || facingDirection == direction)
        {
            return;
        }

        facingDirection = direction > 0 ? 1 : -1;
        ApplyFacingDirection();
    }

    private void ApplyFacingDirection()
    {
        animatorBridge?.FaceDirection(facingDirection);
        MirrorLocalX(wallCheck, wallCheckLocalX);
        MirrorLocalX(ledgeCheck, ledgeCheckLocalX);
        MirrorLocalX(attackPoint, attackPointLocalX);
    }

    private void UpdateAnimatorSpeed()
    {
        animatorBridge?.SetSpeed(Mathf.Abs(rb.linearVelocity.x));
    }

    private void LogMissingDamageReceiver(Collider2D hit)
    {
        if (missingReceiverLoggedThisAttack)
        {
            return;
        }

        Debug.Log(
            $"{name} attack overlapped '{hit.name}', but it has no {nameof(IPlayerDamageReceiver2D)} yet.",
            this
        );
        missingReceiverLoggedThisAttack = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        DrawCheckGizmo(groundCheck, groundCheckSize, Color.green);
        DrawCheckGizmo(wallCheck, wallCheckSize, Color.cyan);
        DrawCheckGizmo(ledgeCheck, ledgeCheckSize, Color.magenta);
    }

    private static void DrawCheckGizmo(Transform check, Vector2 size, Color color)
    {
        if (check == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireCube(check.position, size);
    }

    private static float GetAbsoluteLocalX(Transform check)
    {
        return check == null ? 0f : Mathf.Abs(check.localPosition.x);
    }

    private void MirrorLocalX(Transform check, float absoluteLocalX)
    {
        if (check == null)
        {
            return;
        }

        Vector3 localPosition = check.localPosition;
        localPosition.x = absoluteLocalX * facingDirection;
        check.localPosition = localPosition;
    }
}
