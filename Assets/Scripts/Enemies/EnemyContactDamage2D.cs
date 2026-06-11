using UnityEngine;

// Reusable "touch the body = take damage" component. Drop on any enemy that should hurt the
// player on contact (independent of its telegraphed melee attack). Mirrors the overlap idioms
// in RoboDogController2D.ApplyAttackHit. The player's own i-frames dedup re-hits across all
// sources, so no local cooldown is needed here.
[RequireComponent(typeof(EnemyHealth2D))]
public class EnemyContactDamage2D : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private LayerMask targetLayer;

    [Header("Contact Box")]
    [Tooltip("Size of the centered overlap box used to detect the player on contact.")]
    [SerializeField] private Vector2 contactBoxSize = new Vector2(0.6f, 0.9f);
    [Tooltip("Local offset of the contact box from the enemy origin.")]
    [SerializeField] private Vector2 contactBoxOffset = Vector2.zero;

    private EnemyHealth2D health;

    private void Awake()
    {
        health = GetComponent<EnemyHealth2D>();
    }

    private void FixedUpdate()
    {
        // Dead enemies don't deal contact damage (so the player can walk through a corpse
        // during its despawn delay).
        if (health != null && !health.IsAlive)
        {
            return;
        }

        Vector2 center = (Vector2)transform.position + contactBoxOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, contactBoxSize, 0f, targetLayer);
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

            // Informational direction (enemy -> target); the player recomputes its own
            // knockback vector from positions.
            Vector2 dirToTarget = ((Vector2)hit.transform.position - center).normalized;
            var hitInfo = new EnemyHitInfo2D(contactDamage, gameObject, center, dirToTarget);
            receiver.ReceiveEnemyHit(hitInfo);
            return;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Vector2 center = (Vector2)transform.position + contactBoxOffset;
        Gizmos.DrawWireCube(center, contactBoxSize);
    }
}
