using UnityEngine;
using UnityEngine.UI;

// Screen-space HUD view for the two staged Craftpix bars:
//   * Health  -> driven by PlayerHealth2D.Lives / MaxLives
//   * Energy  -> driven by ShotgunRecoil2D.CooldownNormalized (weapon recharge / readiness)
//
// Pure view: it owns no state, it only reads the gameplay scripts and swaps each Image's sprite to
// the matching pre-rendered fill stage. The art is authored as discrete stages (HealthBar1..8 /
// EnergyBar1..8, 1 = full), so we sprite-swap rather than clip a Filled image. Assign each stage
// array in full->empty order (i.e. ...Bar1 first, ...Bar8 last). Lives on a Canvas object.
public class PlayerHudBars2D : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Player life source. Auto-resolves via FindFirstObjectByType if left empty.")]
    [SerializeField] private PlayerHealth2D health;
    [Tooltip("Shotgun, for the weapon recharge meter. Auto-resolves if left empty.")]
    [SerializeField] private ShotgunRecoil2D shotgun;

    [Header("Health Bar")]
    [Tooltip("UI Image that shows the health stage.")]
    [SerializeField] private Image healthImage;
    [Tooltip("Health fill stages in FULL -> EMPTY order (HealthBar1 ... HealthBar8).")]
    [SerializeField] private Sprite[] healthStages;

    [Header("Energy Bar")]
    [Tooltip("UI Image that shows the weapon-charge stage.")]
    [SerializeField] private Image energyImage;
    [Tooltip("Energy fill stages in FULL -> EMPTY order (EnergyBar1 ... EnergyBar8).")]
    [SerializeField] private Sprite[] energyStages;

    private int lastHealthStage = int.MinValue;
    private int lastEnergyStage = int.MinValue;

    private void Awake()
    {
        if (health == null)
        {
            health = FindFirstObjectByType<PlayerHealth2D>();
        }

        if (shotgun == null)
        {
            shotgun = FindFirstObjectByType<ShotgunRecoil2D>();
        }
    }

    private void Update()
    {
        if (health != null && health.MaxLives > 0)
        {
            float t = (float)health.Lives / health.MaxLives;
            ApplyStage(healthImage, healthStages, t, ref lastHealthStage);
        }

        if (shotgun != null)
        {
            ApplyStage(energyImage, energyStages, shotgun.CooldownNormalized, ref lastEnergyStage);
        }
    }

    // Maps t (1 = full, 0 = empty) to a stage index in a full->empty ordered sprite array and
    // assigns it, but only when the index actually changes (avoids per-frame sprite churn).
    private static void ApplyStage(Image image, Sprite[] stages, float t, ref int lastStage)
    {
        if (image == null || stages == null || stages.Length == 0)
        {
            return;
        }

        int maxIndex = stages.Length - 1;
        int stage = Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(t)) * maxIndex), 0, maxIndex);
        if (stage == lastStage)
        {
            return;
        }

        lastStage = stage;
        image.sprite = stages[stage];
    }
}
