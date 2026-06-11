using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth2D))]
public class RoboDogController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimator2D animatorBridge;
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
    [SerializeField] private LayerMask targetLayer;

    [Header("Ground Checks")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.45f, 0.18f);
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.18f, 0.75f);
    [SerializeField] private Vector2 ledgeCheckSize = new Vector2(0.45f, 0.18f);

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackRange = 0.6f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float attackActiveTime = 0.15f;
    [SerializeField] private float attackRecovery = 0.35f;

    [Header("Death")]
    [SerializeField] private float despawnDelay = 5f;

    private Rigidbody2D rb;
    private EnemyHealth2D health;
    private Transform target;
    private int facingDirection;
    private float wallCheckLocalX;
    private float ledgeCheckLocalX;
    private float attackPointLocalX;
    private float attackCooldownTimer;
    private float attackTimer;
    private bool isAttacking;
    private bool isDead;
    private bool attackHitApplied;
    private bool missingReceiverLoggedThisAttack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth2D>();
        facingDirection = initialFacingDirection >= 0 ? 1 : -1;
        wallCheckLocalX = GetAbsoluteLocalX(wallCheck);
        ledgeCheckLocalX = GetAbsoluteLocalX(ledgeCheck);
        attackPointLocalX = GetAbsoluteLocalX(attackPoint);
        ApplyFacingDirection();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<EnemyHealth2D>();
        }

        health.Damaged += HandleDamaged;
        health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health == null)
        {
            return;
        }

        health.Damaged -= HandleDamaged;
        health.Died -= HandleDied;
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            StopHorizontalMovement();
            return;
        }

        attackCooldownTimer -= Time.fixedDeltaTime;

        if (isAttacking)
        {
            UpdateAttack();
            return;
        }

        UpdateTargetInRadius();

        if (target != null)
        {
            ChaseTarget();
        }
        else
        {
            Patrol();
        }

        animatorBridge?.SetSpeed(Mathf.Abs(rb.linearVelocity.x));
    }

    private void Patrol()
    {
        if (!HasGroundBelow())
        {
            StopHorizontalMovement();
            return;
        }

        if (HasWallAhead() || !HasGroundAhead())
        {
            TurnAround();
        }

        MoveHorizontally(patrolSpeed);
    }

    private void ChaseTarget()
    {
        float xDelta = target.position.x - transform.position.x;
        if (Mathf.Abs(xDelta) > 0.05f)
        {
            SetFacingDirection(xDelta > 0f ? 1 : -1);
        }

        if (IsTargetInAttackRange() && attackCooldownTimer <= 0f)
        {
            StartAttack();
            return;
        }

        if (!HasGroundBelow() || HasWallAhead() || !HasGroundAhead())
        {
            StopHorizontalMovement();
            return;
        }

        MoveHorizontally(chaseSpeed);
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackWindup + attackActiveTime + attackRecovery;
        attackHitApplied = false;
        missingReceiverLoggedThisAttack = false;
        StopHorizontalMovement();
        animatorBridge?.TriggerAttack();
    }

    private void UpdateAttack()
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
        isAttacking = false;
    }

    private void ApplyAttackHit()
    {
        if (attackPoint == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, targetLayer);
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

            Vector2 direction = new Vector2(facingDirection, 0f);
            var hitInfo = new EnemyHitInfo2D(attackDamage, gameObject, attackPoint.position, direction);
            receiver.ReceiveEnemyHit(hitInfo);
            return;
        }
    }

    private void UpdateTargetInRadius()
    {
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.position);
            if (distance <= detectionRange)
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

            IPlayerDamageReceiver2D receiver = hit.GetComponentInParent<IPlayerDamageReceiver2D>();
            if (receiver == null)
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
        return target != null
            && attackPoint != null
            && Vector2.Distance(attackPoint.position, target.position) <= attackRange;
    }

    private bool HasGroundBelow()
    {
        return groundCheck == null
            || Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer) != null;
    }

    private bool HasWallAhead()
    {
        return wallCheck != null
            && Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, groundLayer) != null;
    }

    private bool HasGroundAhead()
    {
        return ledgeCheck == null
            || Physics2D.OverlapBox(ledgeCheck.position, ledgeCheckSize, 0f, groundLayer) != null;
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
        if (direction == 0 || direction == facingDirection)
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

    private void HandleDamaged(DamageInfo2D damage)
    {
        animatorBridge?.TriggerHurt();
    }

    private void HandleDied(DamageInfo2D damage)
    {
        isDead = true;
        target = null;
        isAttacking = false;
        StopHorizontalMovement();
        animatorBridge?.SetDead(true);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.excludeLayers = col.excludeLayers.value | targetLayer.value;
        }

        Destroy(gameObject, despawnDelay);
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

        DrawBox(groundCheck, groundCheckSize, Color.green);
        DrawBox(wallCheck, wallCheckSize, Color.cyan);
        DrawBox(ledgeCheck, ledgeCheckSize, Color.magenta);
    }

    private static void DrawBox(Transform check, Vector2 size, Color color)
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
