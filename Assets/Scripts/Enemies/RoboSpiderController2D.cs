using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth2D))]
public class RoboSpiderController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimator2D animatorBridge;
    [SerializeField] private Transform attackPoint;

    [Header("Movement")]
    [SerializeField] private float crawlSpeed = 1.4f;
    [SerializeField] private int initialFacingDirection = 1;
    [SerializeField] private float surfaceOffset = 0.55f;
    [SerializeField] private float spawnSearchRadius = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(0.85f, 0.65f);
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float attackActiveTime = 0.15f;
    [SerializeField] private float attackRecovery = 0.35f;

    [Header("Death")]
    [SerializeField] private float despawnDelay = 5f;

    private Rigidbody2D rb;
    private EnemyHealth2D health;
    private SurfaceLoop activeLoop;
    private int movementDirection;
    private float attackPointLocalX;
    private float distanceAlongLoop;
    private float attackCooldownTimer;
    private float attackTimer;
    private bool hasLoop;
    private bool isAttacking;
    private bool isDead;
    private bool attackHitApplied;
    private bool missingReceiverLoggedThisAttack;
    private bool missingLoopLogged;
    private Vector2 currentTangent = Vector2.right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        health = GetComponent<EnemyHealth2D>();
        movementDirection = initialFacingDirection >= 0 ? 1 : -1;
        attackPointLocalX = GetAbsoluteLocalX(attackPoint);
        ApplyFacing();
    }

    private void Start()
    {
        BuildSurfaceLoop();
        if (hasLoop)
        {
            ApplyLoopPose();
        }
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
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!hasLoop)
        {
            StopMovement();
            return;
        }

        attackCooldownTimer -= Time.fixedDeltaTime;

        if (isAttacking)
        {
            UpdateAttack();
            return;
        }

        if (attackCooldownTimer <= 0f && HasAttackTarget())
        {
            StartAttack();
            return;
        }

        distanceAlongLoop = Mathf.Repeat(
            distanceAlongLoop + crawlSpeed * movementDirection * Time.fixedDeltaTime,
            activeLoop.TotalLength
        );
        ApplyLoopPose();
        animatorBridge?.SetSpeed(crawlSpeed);
    }

    private void BuildSurfaceLoop()
    {
        Collider2D[] groundHits = Physics2D.OverlapCircleAll(transform.position, spawnSearchRadius, groundLayer);
        Vector2 spawnPosition = transform.position;
        float bestSqrDistance = float.PositiveInfinity;
        SurfaceLoop bestLoop = null;
        float bestDistanceAlongLoop = 0f;

        foreach (Collider2D hit in groundHits)
        {
            EvaluateColliderPaths(hit, spawnPosition, ref bestLoop, ref bestDistanceAlongLoop, ref bestSqrDistance);
        }

        activeLoop = bestLoop;
        distanceAlongLoop = bestDistanceAlongLoop;
        hasLoop = activeLoop != null;

        if (!hasLoop && !missingLoopLogged)
        {
            Debug.LogWarning(
                $"{name} could not derive a closed spider surface loop from nearby Ground colliders.",
                this
            );
            missingLoopLogged = true;
        }
    }

    private void EvaluateColliderPaths(
        Collider2D source,
        Vector2 spawnPosition,
        ref SurfaceLoop bestLoop,
        ref float bestDistanceAlongLoop,
        ref float bestSqrDistance
    )
    {
        switch (source)
        {
            case CompositeCollider2D composite:
                EvaluateComposite(composite, spawnPosition, ref bestLoop, ref bestDistanceAlongLoop, ref bestSqrDistance);
                break;
            case PolygonCollider2D polygon:
                EvaluatePolygon(polygon, spawnPosition, ref bestLoop, ref bestDistanceAlongLoop, ref bestSqrDistance);
                break;
            case BoxCollider2D box:
                EvaluateWorldPoints(GetBoxWorldPoints(box), spawnPosition, ref bestLoop, ref bestDistanceAlongLoop, ref bestSqrDistance);
                break;
        }
    }

    private void EvaluateComposite(
        CompositeCollider2D composite,
        Vector2 spawnPosition,
        ref SurfaceLoop bestLoop,
        ref float bestDistanceAlongLoop,
        ref float bestSqrDistance
    )
    {
        for (int pathIndex = 0; pathIndex < composite.pathCount; pathIndex++)
        {
            var localPoints = new Vector2[composite.GetPathPointCount(pathIndex)];
            composite.GetPath(pathIndex, localPoints);
            EvaluateLocalPoints(
                composite.transform,
                localPoints,
                spawnPosition,
                ref bestLoop,
                ref bestDistanceAlongLoop,
                ref bestSqrDistance
            );
        }
    }

    private void EvaluatePolygon(
        PolygonCollider2D polygon,
        Vector2 spawnPosition,
        ref SurfaceLoop bestLoop,
        ref float bestDistanceAlongLoop,
        ref float bestSqrDistance
    )
    {
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            EvaluateLocalPoints(
                polygon.transform,
                polygon.GetPath(pathIndex),
                spawnPosition,
                ref bestLoop,
                ref bestDistanceAlongLoop,
                ref bestSqrDistance
            );
        }
    }

    private void EvaluateLocalPoints(
        Transform sourceTransform,
        Vector2[] localPoints,
        Vector2 spawnPosition,
        ref SurfaceLoop bestLoop,
        ref float bestDistanceAlongLoop,
        ref float bestSqrDistance
    )
    {
        var worldPoints = new Vector2[localPoints.Length];
        for (int i = 0; i < localPoints.Length; i++)
        {
            worldPoints[i] = sourceTransform.TransformPoint(localPoints[i]);
        }

        EvaluateWorldPoints(worldPoints, spawnPosition, ref bestLoop, ref bestDistanceAlongLoop, ref bestSqrDistance);
    }

    private void EvaluateWorldPoints(
        Vector2[] worldPoints,
        Vector2 spawnPosition,
        ref SurfaceLoop bestLoop,
        ref float bestDistanceAlongLoop,
        ref float bestSqrDistance
    )
    {
        if (!SurfaceLoop.TryCreate(worldPoints, spawnPosition, out SurfaceLoop loop, out float distance, out float sqrDistance))
        {
            return;
        }

        if (sqrDistance >= bestSqrDistance)
        {
            return;
        }

        bestLoop = loop;
        bestDistanceAlongLoop = distance;
        bestSqrDistance = sqrDistance;
    }

    private static Vector2[] GetBoxWorldPoints(BoxCollider2D box)
    {
        Vector2 halfSize = box.size * 0.5f;
        Vector2 offset = box.offset;
        return new[]
        {
            (Vector2)box.transform.TransformPoint(offset + new Vector2(-halfSize.x, -halfSize.y)),
            (Vector2)box.transform.TransformPoint(offset + new Vector2(halfSize.x, -halfSize.y)),
            (Vector2)box.transform.TransformPoint(offset + new Vector2(halfSize.x, halfSize.y)),
            (Vector2)box.transform.TransformPoint(offset + new Vector2(-halfSize.x, halfSize.y))
        };
    }

    private void ApplyLoopPose()
    {
        activeLoop.Sample(distanceAlongLoop, out Vector2 surfacePoint, out Vector2 tangent, out Vector2 normal);
        currentTangent = tangent * movementDirection;

        Vector2 targetPosition = surfacePoint + normal * surfaceOffset;
        float zAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;

        rb.MovePosition(targetPosition);
        rb.MoveRotation(zAngle);
        ApplyFacing();
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackWindup + attackActiveTime + attackRecovery;
        attackHitApplied = false;
        missingReceiverLoggedThisAttack = false;
        StopMovement();
        animatorBridge?.TriggerAttack();
    }

    private void UpdateAttack()
    {
        StopMovement();

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
        ApplyLoopPose();
    }

    private bool HasAttackTarget()
    {
        if (attackPoint == null)
        {
            return false;
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

            if (hit.GetComponentInParent<IPlayerDamageReceiver2D>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyAttackHit()
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

            var hitInfo = new EnemyHitInfo2D(attackDamage, gameObject, attackPoint.position, currentTangent);
            receiver.ReceiveEnemyHit(hitInfo);
            return;
        }
    }

    private void ApplyFacing()
    {
        animatorBridge?.FaceDirection(movementDirection);
        MirrorLocalX(attackPoint, attackPointLocalX);
    }

    private void StopMovement()
    {
        rb.linearVelocity = Vector2.zero;
        animatorBridge?.SetSpeed(0f);
    }

    private void HandleDamaged(DamageInfo2D damage)
    {
        animatorBridge?.TriggerHurt();
    }

    private void HandleDied(DamageInfo2D damage)
    {
        isDead = true;
        isAttacking = false;
        StopMovement();
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
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(attackPoint.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
            Gizmos.matrix = oldMatrix;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnSearchRadius);

        if (activeLoop == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        activeLoop.DrawGizmos();
    }

    private static float GetAbsoluteLocalX(Transform point)
    {
        return point == null ? 0f : Mathf.Abs(point.localPosition.x);
    }

    private void MirrorLocalX(Transform point, float absoluteLocalX)
    {
        if (point == null)
        {
            return;
        }

        Vector3 localPosition = point.localPosition;
        localPosition.x = absoluteLocalX * movementDirection;
        point.localPosition = localPosition;
    }

    private sealed class SurfaceLoop
    {
        private const float MinSegmentLength = 0.001f;
        private readonly Vector2[] points;
        private readonly float[] segmentStarts;
        private readonly float[] segmentLengths;
        private readonly bool isCounterClockwise;

        private SurfaceLoop(
            Vector2[] points,
            float[] segmentStarts,
            float[] segmentLengths,
            float totalLength,
            bool isCounterClockwise
        )
        {
            this.points = points;
            this.segmentStarts = segmentStarts;
            this.segmentLengths = segmentLengths;
            TotalLength = totalLength;
            this.isCounterClockwise = isCounterClockwise;
        }

        public float TotalLength { get; }

        public static bool TryCreate(
            Vector2[] rawPoints,
            Vector2 referencePosition,
            out SurfaceLoop loop,
            out float closestDistanceAlongLoop,
            out float closestSqrDistance
        )
        {
            loop = null;
            closestDistanceAlongLoop = 0f;
            closestSqrDistance = float.PositiveInfinity;

            Vector2[] cleanedPoints = CleanPoints(rawPoints);
            if (cleanedPoints.Length < 3)
            {
                return false;
            }

            float signedArea = GetSignedArea(cleanedPoints);
            if (Mathf.Abs(signedArea) < 0.0001f)
            {
                return false;
            }

            int segmentCount = cleanedPoints.Length;
            var segmentStarts = new float[segmentCount];
            var segmentLengths = new float[segmentCount];
            float totalLength = 0f;

            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a = cleanedPoints[i];
                Vector2 b = cleanedPoints[(i + 1) % segmentCount];
                float length = Vector2.Distance(a, b);
                if (length < MinSegmentLength)
                {
                    return false;
                }

                segmentStarts[i] = totalLength;
                segmentLengths[i] = length;
                totalLength += length;
            }

            if (totalLength < MinSegmentLength)
            {
                return false;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a = cleanedPoints[i];
                Vector2 b = cleanedPoints[(i + 1) % segmentCount];
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(referencePosition - a, segment) / segment.sqrMagnitude);
                Vector2 closestPoint = a + segment * t;
                float sqrDistance = (referencePosition - closestPoint).sqrMagnitude;

                if (sqrDistance >= closestSqrDistance)
                {
                    continue;
                }

                closestSqrDistance = sqrDistance;
                closestDistanceAlongLoop = segmentStarts[i] + segmentLengths[i] * t;
            }

            loop = new SurfaceLoop(
                cleanedPoints,
                segmentStarts,
                segmentLengths,
                totalLength,
                signedArea > 0f
            );
            return true;
        }

        public void Sample(float distance, out Vector2 position, out Vector2 tangent, out Vector2 normal)
        {
            float wrappedDistance = Mathf.Repeat(distance, TotalLength);
            int segmentIndex = points.Length - 1;

            for (int i = 0; i < segmentStarts.Length; i++)
            {
                if (wrappedDistance < segmentStarts[i] + segmentLengths[i])
                {
                    segmentIndex = i;
                    break;
                }
            }

            Vector2 a = points[segmentIndex];
            Vector2 b = points[(segmentIndex + 1) % points.Length];
            float segmentDistance = wrappedDistance - segmentStarts[segmentIndex];
            float t = segmentLengths[segmentIndex] > MinSegmentLength
                ? segmentDistance / segmentLengths[segmentIndex]
                : 0f;

            position = Vector2.Lerp(a, b, t);
            tangent = (b - a).normalized;
            normal = isCounterClockwise
                ? new Vector2(tangent.y, -tangent.x)
                : new Vector2(-tangent.y, tangent.x);
        }

        public void DrawGizmos()
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                Gizmos.DrawLine(a, b);
            }
        }

        private static Vector2[] CleanPoints(Vector2[] rawPoints)
        {
            var points = new List<Vector2>(rawPoints.Length);
            for (int i = 0; i < rawPoints.Length; i++)
            {
                Vector2 point = rawPoints[i];
                if (points.Count > 0 && (points[points.Count - 1] - point).sqrMagnitude < MinSegmentLength * MinSegmentLength)
                {
                    continue;
                }

                points.Add(point);
            }

            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < MinSegmentLength * MinSegmentLength)
            {
                points.RemoveAt(points.Count - 1);
            }

            return points.ToArray();
        }

        private static float GetSignedArea(Vector2[] points)
        {
            float area = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }
    }
}
