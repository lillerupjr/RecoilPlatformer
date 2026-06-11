using UnityEngine;

// Telegraphed slam hazard. The Hammer sprite's own frames punch the head ~2 units DOWN within the
// frame while the mount stays fixed (the art is authored for this), so the visual slam is the
// Animator's Slam clip — NOT a transform move. This controller just paces the rhythm and turns the
// HammerHead hitbox on during the strike frames. The hitbox sits at the head's extended position
// and is non-pogoable, so the player can't bounce off the piston.
public class PistonHazard2D : MonoBehaviour
{
    private static readonly int SlamHash = Animator.StringToHash("Slam");

    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("Damage component on the HammerHead child; enabled only during the strike frames.")]
    [SerializeField] private HazardContactDamage2D hammerDamage;

    [Header("Timing")]
    [Tooltip("Seconds held raised between slams (the telegraph pause).")]
    [SerializeField] private float slamInterval = 2f;
    [Tooltip("Length of the Slam clip; after this the piston is raised again (8 frames @ 12fps).")]
    [SerializeField] private float slamLength = 0.667f;
    [Tooltip("Seconds into the slam when the head is down and the hit window opens.")]
    [SerializeField] private float damageStart = 0.12f;
    [Tooltip("Seconds into the slam when the head lifts and the hit window closes.")]
    [SerializeField] private float damageEnd = 0.42f;

    private float timer;
    private bool slamming;

    private void Awake()
    {
        SetDamageActive(false);
    }

    private void OnEnable()
    {
        timer = 0f;
        slamming = false;
        SetDamageActive(false);
    }

    private void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;

        if (!slamming)
        {
            if (timer >= slamInterval)
            {
                StartSlam();
            }
            return;
        }

        // Dangerous only while the head is down (the strike frames of the Slam clip).
        SetDamageActive(timer >= damageStart && timer < damageEnd);

        if (timer >= slamLength)
        {
            slamming = false;
            timer = 0f;
            SetDamageActive(false);
        }
    }

    private void StartSlam()
    {
        slamming = true;
        timer = 0f;
        if (animator != null)
        {
            animator.SetTrigger(SlamHash);
        }
    }

    private void SetDamageActive(bool active)
    {
        if (hammerDamage != null && hammerDamage.enabled != active)
        {
            hammerDamage.enabled = active;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hammerDamage == null)
        {
            return;
        }

        Collider2D col = hammerDamage.GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
