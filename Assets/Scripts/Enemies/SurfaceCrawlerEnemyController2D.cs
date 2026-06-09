using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth2D))]
public abstract class SurfaceCrawlerEnemyController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimator2D animatorBridge;
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private Transform surfaceProbe;
    [SerializeField] private Transform forwardProbe;
    [SerializeField] private Transform attackPoint;

    [Header("Movement")]
    [SerializeField] private float crawlSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 2.4f;
    [SerializeField] private int initialFacingDirection = 1;
    [SerializeField] private float surfaceSnapDistance = 0.45f;
    [SerializeField] private float cornerProbeDistance = 0.5f;
    [SerializeField] private float turnRetryDelay = 0.18f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float loseTargetRange = 8f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(0.8f, 0.6f);
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
    private float forwardProbeLocalX;
    private float attackPointLocalX;
    private Vector2 surfaceNormal = Vector2.up;
    private float attackCooldownTimer;
    private float turnRetryTimer;
    private float attackTimer;
    private bool attackHitApplied;
    private bool missingReceiverLoggedThisAttack;
    private CrawlerState state;

    private enum CrawlerState
    {
        Crawl,
        Chase,
        Attack,
        Dead
    }

    protected Rigidbody2D Rigidbody => rb;
    protected Transform CurrentTarget => target;
    protected int FacingDirection => facingDirection;
    protected Vector2 SurfaceNormal => surfaceNormal;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        health = GetComponent<EnemyHealth2D>();
        facingDirection = initialFacingDirection >= 0 ? 1 : -1;
        forwardProbeLocalX = GetAbsoluteLocalX(forwardProbe);
        attackPointLocalX = GetAbsoluteLocalX(attackPoint);
        ApplyFacing();
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
        if (state == CrawlerState.Dead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        attackCooldownTimer -= Time.fixedDeltaTime;
        turnRetryTimer -= Time.fixedDeltaTime;

        if (state == CrawlerState.Attack)
        {
            UpdateAttack();
            return;
        }

        AcquireOrReleaseTarget();
        if (!FollowSurface())
        {
            rb.linearVelocity = Vector2.zero;
            animatorBridge?.SetSpeed(0f);
            return;
        }

        if (target != null && IsTargetInAttackRange() && attackCooldownTimer <= 0f)
        {
            StartAttack();
            return;
        }

        state = target == null ? CrawlerState.Crawl : CrawlerState.Chase;
        if (target != null)
        {
            FaceTargetAlongSurface();
        }

        MoveAlongSurface(target == null ? crawlSpeed : chaseSpeed);
        animatorBridge?.SetSpeed(rb.linearVelocity.magnitude);
    }

    protected virtual void StartAttack()
    {
        state = CrawlerState.Attack;
        attackTimer = attackWindup + attackActiveTime + attackRecovery;
        attackHitApplied = false;
        missingReceiverLoggedThisAttack = false;
        rb.linearVelocity = Vector2.zero;
        animatorBridge?.SetSpeed(0f);
        animatorBridge?.TriggerAttack();
    }

    protected virtual void UpdateAttack()
    {
        rb.linearVelocity = Vector2.zero;

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
        state = target == null ? CrawlerState.Crawl : CrawlerState.Chase;
    }

    protected virtual void ApplyAttackHit()
    {
        if (attackPoint == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPoint.position,
            attackBoxSize,
            transform.eulerAngles.z,
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

            receiver.ReceiveEnemyHit(CreateHitInfo());
            return;
        }
    }

    protected virtual EnemyHitInfo2D CreateHitInfo()
    {
        Vector2 tangent = GetSurfaceTangent();
        Vector2 direction = tangent * facingDirection;
        Vector2 point = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        return new EnemyHitInfo2D(attackDamage, gameObject, point, direction);
    }

    protected virtual void HandleDamaged(DamageInfo2D damage)
    {
        animatorBridge?.TriggerHurt();
    }

    protected virtual void HandleDied(DamageInfo2D damage)
    {
        state = CrawlerState.Dead;
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

    private void MoveAlongSurface(float speed)
    {
        Vector2 tangent = GetSurfaceTangent();
        rb.linearVelocity = tangent * facingDirection * speed;
    }

    private bool FollowSurface()
    {
        Vector2 tangent = GetSurfaceTangent();
        Vector2 origin = surfaceProbe != null ? (Vector2)surfaceProbe.position : (Vector2)transform.position;

        RaycastHit2D snapHit = Physics2D.Raycast(origin, -surfaceNormal, surfaceSnapDistance, groundLayer);
        if (snapHit.collider != null)
        {
            surfaceNormal = snapHit.normal.normalized;
            rb.position += snapHit.normal * (surfaceSnapDistance - snapHit.distance) * 0.1f;
            ApplySurfaceRotation();
            return true;
        }

        Vector2 cornerOrigin = forwardProbe != null
            ? (Vector2)forwardProbe.position
            : (Vector2)transform.position + tangent * facingDirection * cornerProbeDistance;
        RaycastHit2D cornerHit = Physics2D.Raycast(cornerOrigin, -tangent * facingDirection, cornerProbeDistance, groundLayer);
        if (cornerHit.collider != null)
        {
            surfaceNormal = cornerHit.normal.normalized;
            ApplySurfaceRotation();
            return true;
        }

        if (turnRetryTimer <= 0f)
        {
            SetFacingDirection(-facingDirection);
            turnRetryTimer = turnRetryDelay;
        }

        return false;
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

        Vector2 delta = target.position - attackPoint.position;
        return Mathf.Abs(Vector2.Dot(delta, GetSurfaceTangent())) <= attackBoxSize.x * 0.5f
            && Mathf.Abs(Vector2.Dot(delta, surfaceNormal)) <= attackBoxSize.y * 0.5f;
    }

    private Vector2 GetSurfaceTangent()
    {
        return new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
    }

    private void SetFacingDirection(int direction)
    {
        if (direction == 0 || facingDirection == direction)
        {
            return;
        }

        facingDirection = direction > 0 ? 1 : -1;
        ApplyFacing();
    }

    private void FaceTargetAlongSurface()
    {
        if (target == null)
        {
            return;
        }

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float alongSurface = Vector2.Dot(toTarget, GetSurfaceTangent());
        if (Mathf.Abs(alongSurface) > 0.05f)
        {
            SetFacingDirection(alongSurface > 0f ? 1 : -1);
        }
    }

    private void ApplySurfaceRotation()
    {
        float zAngle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, zAngle);
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        animatorBridge?.FaceDirection(facingDirection);
        MirrorLocalX(forwardProbe, forwardProbeLocalX);
        MirrorLocalX(attackPoint, attackPointLocalX);
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
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(attackPoint.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
            Gizmos.matrix = oldMatrix;
        }

        DrawProbe(surfaceProbe, Color.green);
        DrawProbe(forwardProbe, Color.cyan);
    }

    private static void DrawProbe(Transform probe, Color color)
    {
        if (probe == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(probe.position, 0.08f);
    }

    private static float GetAbsoluteLocalX(Transform probe)
    {
        return probe == null ? 0f : Mathf.Abs(probe.localPosition.x);
    }

    private void MirrorLocalX(Transform probe, float absoluteLocalX)
    {
        if (probe == null)
        {
            return;
        }

        Vector3 localPosition = probe.localPosition;
        localPosition.x = absoluteLocalX * facingDirection;
        probe.localPosition = localPosition;
    }
}
