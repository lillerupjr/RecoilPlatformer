using UnityEngine;
using UnityEngine.Serialization;

// 8-direction quantized mouse aim + high-recoil shotgun. The recoil impulse (opposite the aim)
// is the core traversal tool: fire down to launch up, redirect mid-air, chain with jumps. Lives
// on the Player root alongside PlayerController2D, which owns the actual velocity change.
//
// Visual (v1): a single right (weapon) arm parented to ArmPivot that follows the aim. Two sprites
// cover all 8 directions with lossless 90° rotations — the E sprite for the 4 cardinals
// (E,N,W,S) and the SE sprite for the 4 diagonals (SE,NE,NW,SW). Off arm + gun come later.
[RequireComponent(typeof(PlayerController2D))]
public class ShotgunRecoil2D : MonoBehaviour
{
    [Header("Aim")]
    [Tooltip("Aim origin / arm anchor. The arm rotates around this to follow the mouse.")]
    [FormerlySerializedAs("gunPivot")]
    [SerializeField] private Transform armPivot;

    [Header("Arm")]
    [Tooltip("Right (weapon) arm renderer, child of ArmPivot.")]
    [SerializeField] private SpriteRenderer armRenderer;
    [Tooltip("Arm drawn facing E. Rotated 0/90/180/270 CCW for the cardinals E, N, W, S.")]
    [SerializeField] private Sprite armE;
    [Tooltip("Arm drawn facing SE. Rotated 0/90/180/270 CCW for the diagonals SE, NE, NW, SW.")]
    [SerializeField] private Sprite armSE;

    [Header("Gun")]
    [Tooltip("Gun renderer, child of ArmPivot at the hand — inherits the arm's rotation + mirror.")]
    [SerializeField] private SpriteRenderer gunRenderer;
    [Tooltip("Gun drawn horizontal (E) — used for the cardinals.")]
    [SerializeField] private Sprite gunHorizontal;
    [Tooltip("Gun drawn diagonal (NE) — used for the diagonals.")]
    [SerializeField] private Sprite gunDiagonal;
    [Tooltip("Flip the diagonal gun on Y. Off by default: Gun_1_2 is SE-based like the arm.")]
    [SerializeField] private bool gunDiagonalFlipY = false;
    [Tooltip("Extra Gun local offset for the diagonals only (the hand sits differently in the "
        + "SE arm sprite). Cardinals use the Gun's authored position; this shifts diagonals.")]
    [SerializeField] private Vector2 gunDiagonalOffset = Vector2.zero;
    [Tooltip("Extra Gun rotation (degrees CCW) for the diagonals only, to fit the gun's barrel "
        + "angle to the diagonal arm pose. Mirrors automatically with facing.")]
    [SerializeField] private float gunDiagonalRotation = 0f;

    [Header("Anchor Per Animation")]
    [Tooltip("Authored ArmPivot position is the idle anchor. These offsets shift the shoulder for "
        + "other animations, in facing-right space (forward = +X, down = -Y; X mirrors facing left).")]
    [SerializeField] private Vector2 runAnchorOffset = new Vector2(0.1f, -0.1f);
    [SerializeField] private Vector2 jumpAnchorOffset = Vector2.zero;

    [Header("Fire")]
    [Tooltip("Seconds between shots. Refreshed to ready on a pogo hit. Infinite ammo for the greybox.")]
    [SerializeField] private float fireCooldown = 1f;

    [Header("Recoil")]
    [Tooltip("Impulse magnitude in velocity units (Rigidbody2D mass is 1).")]
    [SerializeField] private float recoilForce = 14f;
    [Tooltip("Seconds the controller yields horizontal control so the impulse carries.")]
    [SerializeField] private float controlLockout = 0.12f;
    [Tooltip("Clamp total speed right after a shot. 0 = uncapped.")]
    [SerializeField] private float maxRecoilSpeed = 0f;

    [Header("Pogo")]
    [Tooltip("How far the pogo probe reaches along the aim, in world units.")]
    [SerializeField] private float pogoProbeDistance = 1.8f;
    [Tooltip("Probe thickness — forgiveness for the 8-direction aim.")]
    [SerializeField] private float pogoProbeRadius = 0.4f;
    [Tooltip("Extra recoil impulse added on top of recoilForce when a pogo target is hit.")]
    [SerializeField] private float pogoBonus = 10f;
    [Tooltip("Cooldown applied after a pogo refresh (instead of 0) — a small grace so the next "
        + "shot is a deliberate beat, not a same-frame re-fire.")]
    [SerializeField] private float pogoGracePeriod = 0.10f;
    [Tooltip("Layers the probe scans for pogo targets (hazards + enemies).")]
    [SerializeField] private LayerMask pogoLayers;
    [Tooltip("Geometry that blocks the probe so you can't pogo through walls/floors.")]
    [SerializeField] private LayerMask blockerLayers;

    [Header("Projectiles")]
    [Tooltip("Pellet prefab (Projectile2D). Each shot fires pelletCount of these in a spread.")]
    [SerializeField] private Projectile2D pelletPrefab;
    [SerializeField] private int pelletCount = 3;
    [Tooltip("Total spread cone in degrees; pellets fan evenly across it.")]
    [SerializeField] private float spreadDegrees = 12f;
    [SerializeField] private int pelletDamage = 1;
    [Tooltip("Muzzle spawn point. Falls back to ArmPivot + aim * muzzleOffset when unset.")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private float muzzleOffset = 1f;

    private PlayerController2D controller;
    private Camera cam;
    private float cooldownTimer;
    private Vector2 aimDirection = Vector2.right;
    private float snappedAngle;
    private int lastArmKey = int.MinValue;
    private Vector3 armPivotBasePos;
    private Vector3 armPivotBaseScale = Vector3.one;
    private Vector3 gunBaseLocalPos;
    private bool fireQueued;

    private void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        cam = Camera.main;

        if (armPivot != null)
        {
            // Capture the authored anchor transform so facing-right leaves it exactly as placed
            // and facing-left reflects it across the body center.
            armPivotBasePos = armPivot.localPosition;
            armPivotBaseScale = armPivot.localScale;
        }

        if (gunRenderer != null)
        {
            // Authored Gun position is the cardinal anchor; diagonals add gunDiagonalOffset.
            gunBaseLocalPos = gunRenderer.transform.localPosition;
        }
    }

    private void Update()
    {
        UpdateAim();

        cooldownTimer -= Time.deltaTime;

        // Raw mouse button 0 — no dependency on an Input Manager "Fire1" axis being set up.
        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f)
        {
            fireQueued = true;
        }
    }

    private void UpdateAim()
    {
        if (armPivot == null || cam == null)
        {
            return;
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = (Vector2)mouseWorld - (Vector2)armPivot.position;

        // Snap the raw angle to the nearest of 8 compass directions (45-degree steps).
        snappedAngle = Mathf.Round(Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg / 45f) * 45f;
        aimDirection = new Vector2(
            Mathf.Cos(snappedAngle * Mathf.Deg2Rad),
            Mathf.Sin(snappedAngle * Mathf.Deg2Rad)
        );

        UpdateArm(snappedAngle);
    }

    // Rotates the arm to the aim using 2 sprites (E for cardinals, SE for diagonals). Facing right
    // points the sprite directly (rotation = angle, or angle+45 for the SE sprite). Facing left
    // adds the scale.x = -1 mirror, which makes the sprite point at rot+180 (E) / rot+225 (SE), so
    // the rotation is offset by -180 / -225 to cancel that — otherwise N↔S and NW↔SE would swap.
    // Reassigns only when the (octant, facing) pair changes.
    private void UpdateArm(float angle)
    {
        bool facingLeft = controller != null && controller.FacingLeft;

        // Anchor follows the current animation (idle = authored pos; run/jump add their offset).
        // Set every frame since the locomotion can change without the aim octant changing.
        // Facing right: authored anchor as-is. Facing left: reflect X across the body center.
        Vector3 localPosition = armPivotBasePos + (Vector3)AnchorOffset();
        Vector3 localScale = armPivotBaseScale;
        if (facingLeft)
        {
            localPosition.x = -localPosition.x;
            localScale.x = -localScale.x;
        }

        armPivot.localPosition = localPosition;
        armPivot.localScale = localScale;

        float a = angle % 360f;
        if (a < 0f)
        {
            a += 360f;
        }

        int octant = Mathf.RoundToInt(a / 45f) % 8;
        int key = octant * 2 + (facingLeft ? 1 : 0);
        if (key == lastArmKey)
        {
            return;
        }

        lastArmKey = key;

        bool diagonal = (octant % 2) != 0;

        // Facing right points the sprite directly; facing left offsets the rotation to cancel the
        // scale.x = -1 mirror (otherwise N↔S and NW↔SE swap).
        float rot;
        if (!facingLeft)
        {
            rot = diagonal ? a + 45f : a;
        }
        else
        {
            rot = diagonal ? a - 225f : a - 180f;
        }

        if (armRenderer != null)
        {
            armRenderer.sprite = diagonal ? armSE : armE;
        }

        // Gun rides ArmPivot (rotation + mirror inherited); just swap its sprite by octant parity.
        // The diagonal art is NE-based, so flipY aligns it with the arm's SE base.
        if (gunRenderer != null)
        {
            gunRenderer.sprite = diagonal ? gunDiagonal : gunHorizontal;
            gunRenderer.flipY = diagonal && gunDiagonalFlipY;
            gunRenderer.transform.localPosition = diagonal
                ? gunBaseLocalPos + (Vector3)gunDiagonalOffset
                : gunBaseLocalPos;
            gunRenderer.transform.localRotation = Quaternion.Euler(
                0f, 0f, diagonal ? gunDiagonalRotation : 0f);
        }

        armPivot.localRotation = Quaternion.Euler(0f, 0f, rot);
    }

    private Vector2 AnchorOffset()
    {
        if (controller == null)
        {
            return Vector2.zero;
        }

        switch (controller.Locomotion)
        {
            case PlayerController2D.LocomotionState.Run:
                return runAnchorOffset;
            case PlayerController2D.LocomotionState.Jump:
                return jumpAnchorOffset;
            default:
                return Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        if (!fireQueued)
        {
            return;
        }

        // A pogo hit on a hazard / alive enemy boosts the recoil and refreshes the cooldown,
        // letting the player chain shots into movement.
        bool pogo = ProbePogo();

        // Recoil pushes opposite the aim direction (extra impulse on a pogo).
        float force = recoilForce + (pogo ? pogoBonus : 0f);
        controller.ApplyRecoil(-aimDirection * force, controlLockout, maxRecoilSpeed);
        FirePellets();

        cooldownTimer = pogo ? pogoGracePeriod : fireCooldown;
        fireQueued = false;
    }

    // Casts a forgiving circle along the aim. Returns true if the nearest thing in reach is a
    // pogo target (hazard or alive enemy); a wall/floor in front blocks it; dead enemies and
    // other colliders are looked past.
    private bool ProbePogo()
    {
        Vector2 origin = MuzzleOrigin();
        int mask = pogoLayers.value | blockerLayers.value;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin, pogoProbeRadius, aimDirection, pogoProbeDistance, mask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D col = hit.collider;
            if (col == null)
            {
                continue;
            }

            // Never the player itself.
            if (col.transform == transform || col.transform.IsChildOf(transform))
            {
                continue;
            }

            // A wall/floor in front blocks the pogo.
            if ((blockerLayers.value & (1 << col.gameObject.layer)) != 0)
            {
                return false;
            }

            if (IsPogoable(col))
            {
                return true;
            }

            // Non-pogoable, non-blocking (e.g. a dead enemy) — look past it.
        }

        return false;
    }

    private bool IsPogoable(Collider2D col)
    {
        HazardContactDamage2D hazard = col.GetComponentInParent<HazardContactDamage2D>();
        if (hazard != null)
        {
            return hazard.Pogoable;
        }

        IDamageable2D damageable = col.GetComponentInParent<IDamageable2D>();
        return damageable != null && damageable.IsAlive;
    }

    // Shared shot origin for pellets and the pogo probe.
    private Vector2 MuzzleOrigin()
    {
        if (muzzle != null)
        {
            return muzzle.position;
        }

        Vector2 basePos = armPivot != null ? (Vector2)armPivot.position : (Vector2)transform.position;
        return basePos + aimDirection * muzzleOffset;
    }

    private void FirePellets()
    {
        if (pelletPrefab == null || armPivot == null)
        {
            return;
        }

        Vector2 origin = MuzzleOrigin();

        int count = Mathf.Max(1, pelletCount);
        float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        // Fan evenly across the spread cone; a single pellet fires straight down the aim.
        float start = count > 1 ? -spreadDegrees * 0.5f : 0f;
        float step = count > 1 ? spreadDegrees / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = (baseAngle + start + step * i) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Projectile2D pellet = Instantiate(pelletPrefab, origin, Quaternion.identity);
            pellet.Launch(dir, gameObject, pelletDamage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (armPivot == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(armPivot.position, aimDirection * 2f);

        // Pogo probe reach (origin + swept circle along the aim).
        Vector2 origin = MuzzleOrigin();
        Vector2 end = origin + aimDirection * pogoProbeDistance;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin, pogoProbeRadius);
        Gizmos.DrawWireSphere(end, pogoProbeRadius);
        Gizmos.DrawLine(origin, end);
    }
}
