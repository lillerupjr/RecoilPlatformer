using UnityEngine;

// Owner-aware projectile pellet. Travels in a straight line, damages the first IDamageable2D it
// overlaps (anything but its owner), and despawns on the ground layer or after flying its range.
// Reusable by enemies later — the owner is excluded so a shooter never hits itself.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile2D : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 18f;
    [Tooltip("Max distance the pellet travels before despawning (world units).")]
    [SerializeField] private float range = 10f;

    [Header("Appearance")]
    [Tooltip("Uniform scale applied to the pellet on spawn.")]
    [SerializeField] private float spriteScale = 1f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [Tooltip("Layers that stop the pellet (walls/floor).")]
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private GameObject owner;
    private Vector2 startPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        if (spriteScale > 0f)
        {
            transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
        }
    }

    // Aim the pellet and fire it. damageOverride < 0 keeps the prefab's damage.
    public void Launch(Vector2 direction, GameObject firedBy, int damageOverride = -1)
    {
        owner = firedBy;
        if (damageOverride >= 0)
        {
            damage = damageOverride;
        }

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.linearVelocity = dir * speed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        startPosition = transform.position;
    }

    private void Update()
    {
        // Despawn once the pellet has flown its range.
        if (((Vector2)transform.position - startPosition).sqrMagnitude >= range * range)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Never hit the shooter (or its child colliders).
        if (owner != null && (other.transform == owner.transform || other.transform.IsChildOf(owner.transform)))
        {
            return;
        }

        IDamageable2D target = other.GetComponentInParent<IDamageable2D>();
        if (target != null)
        {
            if (target.IsAlive)
            {
                Vector2 point = transform.position;
                Vector2 dir = rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized : Vector2.right;
                target.TakeDamage(new DamageInfo2D(damage, owner, point, dir));
            }

            Destroy(gameObject);
            return;
        }

        // Hit a wall/floor.
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }
    }
}
