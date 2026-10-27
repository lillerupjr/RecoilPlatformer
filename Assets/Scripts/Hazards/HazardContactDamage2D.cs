using UnityEngine;

// Reusable contact-damage for environmental hazards (saws, spikes, lava, the piston, ...). Unlike
// EnemyContactDamage2D this has no EnemyHealth2D dependency — hazards aren't killable. It feeds the
// same damage seam the enemies use: IPlayerDamageReceiver2D.ReceiveEnemyHit, so the player's lives,
// away+up knockback, and i-frame blink all apply for free.
//
// Detection is an ACTIVE overlap poll, NOT OnTriggerStay2D: a stationary player's Rigidbody2D sleeps,
// and sleeping bodies don't receive trigger-stay callbacks — so a player standing in a zone that
// switches on (the piston slamming down on them) would take no damage. Polling the hazard's own
// collider each FixedUpdate finds the player regardless of sleep state, and sidesteps the layer
// collision matrix. Continuous contact is still naturally rate-limited by the player's i-frames, so
// there's no local cooldown. Only the player implements IPlayerDamageReceiver2D, so that interface
// check is the precise "is it the player?" gate.
[RequireComponent(typeof(Collider2D))]
public class HazardContactDamage2D : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField] private int contactDamage = 1;
    [Tooltip("Whether the shotgun can pogo off this hazard. Off for timed hazards like the piston.")]
    [SerializeField] private bool pogoable = true;
    [Tooltip("When false, contact deals no damage. Always-on hazards (saws/spikes) leave this true; "
        + "timed hazards (the piston) drive it from their controller / animation events.")]
    [SerializeField] private bool damageActive = true;

    private Collider2D hazardCollider;
    private ContactFilter2D filter;
    private readonly Collider2D[] overlaps = new Collider2D[16];

    public bool Pogoable => pogoable;

    // Explicit gate, polled in FixedUpdate. Used instead of toggling this component's `enabled`,
    // which does not reliably suppress damage once an overlap is already in progress.
    public bool DamageActive { get => damageActive; set => damageActive = value; }

    private void Awake()
    {
        hazardCollider = GetComponent<Collider2D>();
        // No layer mask (scan everything) + include triggers; the IPlayerDamageReceiver2D check
        // below is the real filter, so this can't accidentally miss the player on a layer detail.
        filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
    }

    private void FixedUpdate()
    {
        if (!damageActive || hazardCollider == null)
        {
            return;
        }

        int count = hazardCollider.Overlap(filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null)
            {
                continue;
            }

            IPlayerDamageReceiver2D receiver = other.GetComponentInParent<IPlayerDamageReceiver2D>();
            if (receiver == null)
            {
                continue;
            }

            // Informational direction (hazard -> player); the player recomputes its own knockback
            // vector from positions.
            Vector2 origin = transform.position;
            Vector2 dirToPlayer = ((Vector2)other.transform.position - origin).normalized;
            receiver.ReceiveEnemyHit(new EnemyHitInfo2D(contactDamage, gameObject, origin, dirToPlayer));
        }
    }
}
