using UnityEngine;

// Telegraphed slam hazard. The Hammer sprite's own frames punch the head ~2 units DOWN within the
// frame while the mount stays fixed (the art is authored for this), so the visual slam is the
// Animator's Slam clip — NOT a transform move. This controller paces the rhythm; the HammerHead
// hitbox is turned on/off by ANIMATION EVENTS on the Slam clip (EnableHammerDamage /
// DisableHammerDamage), placed on the down frames so the hit window lands exactly when the head is
// squashing — no drift between the timer and the visual. The hitbox is non-pogoable, so the player
// can't bounce off the piston.
public class PistonHazard2D : MonoBehaviour
{
    private static readonly int SlamHash = Animator.StringToHash("Slam");

    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("Damage component on the HammerHead child; enabled only during the down frames "
        + "(driven by the Slam clip's animation events).")]
    [SerializeField] private HazardContactDamage2D hammerDamage;

    [Header("Timing")]
    [Tooltip("Seconds held raised between slams (the telegraph pause).")]
    [SerializeField] private float slamInterval = 2f;
    [Tooltip("Length of the Slam clip; after this the piston is raised again (8 frames @ 12fps). "
        + "Acts as a backstop that force-clears the hit window if the disable event is missed.")]
    [SerializeField] private float slamLength = 0.667f;

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

        // Damage on/off is driven by the Slam clip's animation events (EnableHammerDamage /
        // DisableHammerDamage). Here we only end the cycle; the SetDamageActive(false) is a
        // backstop in case the disable event was missed (e.g. the clip was interrupted).
        if (timer >= slamLength)
        {
            slamming = false;
            timer = 0f;
            SetDamageActive(false);
        }
    }

    // Animation-event hooks fired by the Slam clip on the head's down frames. Public + parameterless
    // so the AnimationClip can call them on this component (same GameObject as the Animator).
    public void EnableHammerDamage() => SetDamageActive(true);

    public void DisableHammerDamage() => SetDamageActive(false);

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
        if (hammerDamage != null)
        {
            // Gate via the explicit flag, not the component's `enabled` — disabling the behaviour
            // does not reliably stop OnTriggerStay2D once the player is already overlapping.
            hammerDamage.DamageActive = active;
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
