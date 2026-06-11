using UnityEngine;

// Reusable contact-damage for environmental hazards (saws, spikes, lava, ...). Unlike
// EnemyContactDamage2D this has no EnemyHealth2D dependency — hazards aren't killable. It feeds
// the same damage seam the enemies use: IPlayerDamageReceiver2D.ReceiveEnemyHit, so the player's
// lives, away+up knockback, and i-frame blink all apply for free.
//
// Put this on a hazard with an isTrigger Collider2D. Continuous contact is naturally rate-limited
// by the player's i-frames, so there's no local cooldown. Only the player implements
// IPlayerDamageReceiver2D, so that interface check is the precise "is it the player?" gate.
[RequireComponent(typeof(Collider2D))]
public class HazardContactDamage2D : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField] private int contactDamage = 1;
    [Tooltip("Whether the shotgun can pogo off this hazard. Off for timed hazards like the piston.")]
    [SerializeField] private bool pogoable = true;

    public bool Pogoable => pogoable;

    private void OnTriggerStay2D(Collider2D other)
    {
        IPlayerDamageReceiver2D receiver = other.GetComponentInParent<IPlayerDamageReceiver2D>();
        if (receiver == null)
        {
            return;
        }

        // Informational direction (hazard -> player); the player recomputes its own knockback
        // vector from positions.
        Vector2 origin = transform.position;
        Vector2 dirToPlayer = ((Vector2)other.transform.position - origin).normalized;
        var hitInfo = new EnemyHitInfo2D(contactDamage, gameObject, origin, dirToPlayer);
        receiver.ReceiveEnemyHit(hitInfo);
    }
}
