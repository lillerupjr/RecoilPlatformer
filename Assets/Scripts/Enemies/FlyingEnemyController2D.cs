using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth2D))]
public abstract class FlyingEnemyController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimator2D animatorBridge;
    [SerializeField] private Transform attackPoint;

    [Header("Movement")]
    [SerializeField] private float hoverSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private Vector2 preferredTargetOffset = new Vector2(0f, 1.25f);
    [SerializeField] private float idleBobAmplitude = 0.12f;
    [SerializeField] private float idleBobFrequency = 2f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 7f;
    [SerializeField] private float loseTargetRange = 9f;
    [SerializeField] private LayerMask targetLayer;

    [Header("Ram Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(0.9f, 0.65f);
    [SerializeField] private float attackRange = 1.4f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float ramDuration = 0.25f;
    [SerializeField] private float ramSpeed = 7f;
    [SerializeField] private float attackRecovery = 0.35f;

    [Header("Death")]
    [SerializeField] private float despawnDelay = 5f;

    private Rigidbody2D rb;
    private EnemyHealth2D health;
    private Transform target;
    private Vector2 spawnPosition;
    private Vector2 ramDirection;
    private int facingDirection = 1;
    private float attackCooldownTimer;
    private float attackTimer;
    private bool attackHitApplied;
    private bool missingReceiverLoggedThisAttack;
    private FlyingState state;

    private enum FlyingState
    {
        Loiter,
        Chase,
        Ram,
        Dead
    }

    protected Rigidbody2D Rigidbody => rb;
    protected Transform CurrentTarget => target;
    protected int FacingDirection => facingDirection;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        health = GetComponent<EnemyHealth2D>();
        spawnPosition = transform.position;
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
        if (state == FlyingState.Dead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        attackCooldownTimer -= Time.fixedDeltaTime;

        if (state == FlyingState.Ram)
        {
            UpdateRamAttack();
            return;
        }

        AcquireOrReleaseTarget();

        if (target != null && IsTargetInAttackRange() && attackCooldownTimer <= 0f)
        {
            StartRamAttack();
            return;
        }

        if (target != null)
        {
            state = FlyingState.Chase;
            MoveToward((Vector2)target.position + preferredTargetOffset, chaseSpeed);
        }
        else
        {
            state = FlyingState.Loiter;
            Vector2 bob = Vector2.up * (Mathf.Sin(Time.time * idleBobFrequency) * idleBobAmplitude);
            MoveToward(spawnPosition + bob, hoverSpeed);
        }

        animatorBridge?.SetSpeed(rb.linearVelocity.magnitude);
    }

    protected virtual void StartRamAttack()
    {
        state = FlyingState.Ram;
        attackTimer = attackWindup + ramDuration + attackRecovery;
        attackHitApplied = false;
        missingReceiverLoggedThisAttack = false;

        Vector2 toTarget = target != null
            ? ((Vector2)target.position - (Vector2)transform.position)
            : Vector2.right * facingDirection;
        ramDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.right * facingDirection;
        SetFacingDirection(ramDirection.x >= 0f ? 1 : -1);
        rb.linearVelocity = Vector2.zero;
        animatorBridge?.SetSpeed(0f);
        animatorBridge?.TriggerAttack();
    }

    protected virtual void UpdateRamAttack()
    {
        float totalDuration = attackWindup + ramDuration + attackRecovery;
        float elapsed = totalDuration - attackTimer;

        if (elapsed < attackWindup)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else if (elapsed < attackWindup + ramDuration)
        {
            rb.linearVelocity = ramDirection * ramSpeed;

            if (!attackHitApplied)
            {
                ApplyAttackHit();
                attackHitApplied = true;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        attackTimer -= Time.fixedDeltaTime;
        if (attackTimer > 0f)
        {
            return;
        }

        attackCooldownTimer = attackCooldown;
        state = target == null ? FlyingState.Loiter : FlyingState.Chase;
    }

    protected virtual void ApplyAttackHit()
    {
        Vector2 point = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        Collider2D[] hits = Physics2D.OverlapBoxAll(point, attackBoxSize, 0f, targetLayer);

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

            receiver.ReceiveEnemyHit(new EnemyHitInfo2D(attackDamage, gameObject, point, ramDirection));
            return;
        }
    }

    protected virtual void HandleDamaged(DamageInfo2D damage)
    {
        animatorBridge?.TriggerHurt();
    }

    protected virtual void HandleDied(DamageInfo2D damage)
    {
        state = FlyingState.Dead;
        target = null;
        rb.linearVelocity = Vector2.zero;
        animatorBridge?.SetSpeed(0f);
        animatorBridge?.SetDead(true);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.excludeLayers = col.excludeLayers.value | targetLayer.value;
        }

        Destroy(gameObject, despawnDelay);
    }

    private void MoveToward(Vector2 destination, float speed)
    {
        Vector2 toDestination = destination - rb.position;
        Vector2 desiredVelocity = toDestination.sqrMagnitude > 0.01f
            ? toDestination.normalized * speed
            : Vector2.zero;

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.18f);

        if (Mathf.Abs(rb.linearVelocity.x) > 0.05f)
        {
            SetFacingDirection(rb.linearVelocity.x > 0f ? 1 : -1);
        }
    }

    private void AcquireOrReleaseTarget()
    {
        if (target != null)
        {
            if (Vector2.Distance(transform.position, target.position) <= loseTargetRange)
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
            && Vector2.Distance(transform.position, target.position) <= attackRange;
    }

    private void SetFacingDirection(int direction)
    {
        if (direction == 0 || direction == facingDirection)
        {
            return;
        }

        facingDirection = direction > 0 ? 1 : -1;
        animatorBridge?.FaceDirection(facingDirection);

        if (attackPoint != null)
        {
            Vector3 localPosition = attackPoint.localPosition;
            localPosition.x = Mathf.Abs(localPosition.x) * facingDirection;
            attackPoint.localPosition = localPosition;
        }
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

        Gizmos.color = Color.red;
        Vector2 point = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        Gizmos.DrawWireCube(point, attackBoxSize);
    }
}
