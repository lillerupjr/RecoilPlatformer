using UnityEngine;

// Central "juice" hub: camera shake + hit stop, driven by combat events. One tuning surface so
// future feedback (hit flash, scale punch, controller rumble) has an obvious home. Combat scripts
// call the static helpers (ShotgunFired / EnemyHit / PlayerHit); a single instance lives in the
// scene. The statics are no-ops if no GameFeel is present, so nothing hard-depends on it.
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [System.Serializable]
    private struct FeelPreset
    {
        [Tooltip("Camera shake trauma added (0-1). Accumulates across rapid hits and decays.")]
        [Range(0f, 1f)] public float trauma;
        [Tooltip("Hit-stop freeze duration in seconds (unscaled). 0 = no freeze.")]
        public float hitStop;
    }

    [Header("Presets")]
    [Tooltip("Every shot, even a miss — light weapon punch, no freeze by default.")]
    [SerializeField] private FeelPreset shotgunFire = new FeelPreset { trauma = 0.25f, hitStop = 0f };
    [Tooltip("A pellet connects with an enemy.")]
    [SerializeField] private FeelPreset enemyHit = new FeelPreset { trauma = 0.4f, hitStop = 0.05f };
    [Tooltip("The player takes a hit — biggest shake + freeze.")]
    [SerializeField] private FeelPreset playerHit = new FeelPreset { trauma = 0.6f, hitStop = 0.08f };

    [Header("References")]
    [Tooltip("Camera shake target. Auto-resolved from Camera.main if left unset.")]
    [SerializeField] private CameraFollow2D cameraShake;

    private float freezeTimer;
    private bool frozen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (cameraShake == null && Camera.main != null)
        {
            cameraShake = Camera.main.GetComponent<CameraFollow2D>();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;

        // Never leave the game frozen if we vanish mid-hit-stop.
        if (frozen)
        {
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (!frozen)
        {
            return;
        }

        // Hit stop runs on unscaled time because the world itself is frozen (timeScale = 0).
        freezeTimer -= Time.unscaledDeltaTime;
        if (freezeTimer <= 0f)
        {
            frozen = false;
            Time.timeScale = 1f;
        }
    }

    private void Trigger(FeelPreset preset)
    {
        if (preset.trauma > 0f && cameraShake != null)
        {
            cameraShake.AddTrauma(preset.trauma);
        }

        if (preset.hitStop > 0f)
        {
            Freeze(preset.hitStop);
        }
    }

    private void Freeze(float duration)
    {
        // Overlapping freezes take the longest pending duration rather than summing — keeps a
        // burst of hits from snowballing into a long, mushy pause.
        freezeTimer = Mathf.Max(freezeTimer, duration);
        if (!frozen)
        {
            frozen = true;
            Time.timeScale = 0f;
        }
    }

    // --- Call sites for combat scripts (safe no-ops when no GameFeel is in the scene) ---
    public static void ShotgunFired() => Instance?.Trigger(Instance.shotgunFire);
    public static void EnemyHit() => Instance?.Trigger(Instance.enemyHit);
    public static void PlayerHit() => Instance?.Trigger(Instance.playerHit);
}
