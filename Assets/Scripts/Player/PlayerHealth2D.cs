using System;
using UnityEngine;

// Player life system. Implements IPlayerDamageReceiver2D (the interface enemy AI already calls on
// whatever it overlaps). Lives, brief i-frames so one melee swing costs one life, and a greybox
// respawn-at-spawn on death.
public class PlayerHealth2D : MonoBehaviour, IPlayerDamageReceiver2D
{
    [Header("Health")]
    [SerializeField] private int maxLives = 4;
    [Tooltip("Seconds of invulnerability after taking a hit.")]
    [SerializeField] private float hitInvulnerability = 0.8f;

    [Header("Knockback")]
    [Tooltip("Impulse magnitude applied away from the enemy on a non-fatal hit.")]
    [SerializeField] private float knockbackForce = 8f;
    [Tooltip("Upward component added to the away-from-enemy direction (before normalization weighting).")]
    [SerializeField] private float knockbackUpward = 0.6f;
    [Tooltip("Seconds of control lockout so the knockback isn't immediately overridden by input.")]
    [SerializeField] private float knockbackLockout = 0.2f;
    [Tooltip("Speed cap applied after the knockback impulse (0 = uncapped).")]
    [SerializeField] private float maxKnockbackSpeed = 0f;

    [Header("Hit Feedback")]
    [Tooltip("Seconds between alpha toggles while blinking during i-frames.")]
    [SerializeField] private float blinkInterval = 0.08f;
    [Tooltip("Renderers blinked during i-frames. Auto-filled from child SpriteRenderers if empty.")]
    [SerializeField] private SpriteRenderer[] blinkRenderers;

    [Header("References")]
    [SerializeField] private PlayerController2D controller;
    [Tooltip("Optional — flashed on hit.")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    private int lives;
    private float invulnerableUntil;
    private bool blinkHidden;
    private float nextBlinkToggle;

    public event Action<EnemyHitInfo2D> Damaged;
    public event Action<EnemyHitInfo2D> Died;

    public int Lives => lives;
    public int MaxLives => maxLives;
    public bool IsAlive => lives > 0;

    private void Awake()
    {
        lives = Mathf.Max(1, maxLives);
        if (controller == null)
        {
            controller = GetComponent<PlayerController2D>();
        }

        if (blinkRenderers == null || blinkRenderers.Length == 0)
        {
            blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void Update()
    {
        // Blink the player's renderers for the full i-frame window so invulnerability is readable.
        if (Time.time < invulnerableUntil)
        {
            if (Time.time >= nextBlinkToggle)
            {
                blinkHidden = !blinkHidden;
                SetRenderersAlpha(blinkHidden ? 0f : 1f);
                nextBlinkToggle = Time.time + blinkInterval;
            }
        }
        else if (blinkHidden)
        {
            blinkHidden = false;
            SetRenderersAlpha(1f);
        }
    }

    private void SetRenderersAlpha(float alpha)
    {
        if (blinkRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in blinkRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    public void ReceiveEnemyHit(EnemyHitInfo2D hit)
    {
        if (Time.time < invulnerableUntil)
        {
            return;
        }

        int amount = Mathf.Max(0, hit.Damage);
        if (amount == 0)
        {
            return;
        }

        lives = Mathf.Max(0, lives - amount);
        invulnerableUntil = Time.time + hitInvulnerability;

        if (lives == 0)
        {
            // No knockback on death — Respawn -> ResetToSpawn zeroes velocity anyway.
            Died?.Invoke(hit);
            Respawn();
            return;
        }

        ApplyKnockback(hit);
        Damaged?.Invoke(hit);
    }

    private void ApplyKnockback(EnemyHitInfo2D hit)
    {
        if (controller == null || knockbackForce <= 0f)
        {
            return;
        }

        // Push away from the enemy. Fall back to the hit's reported direction if the source
        // GameObject is gone.
        Vector2 away;
        if (hit.Source != null)
        {
            away = (Vector2)transform.position - (Vector2)hit.Source.transform.position;
        }
        else
        {
            away = hit.Direction;
        }

        float horizontal = away.x;
        if (Mathf.Abs(horizontal) < 0.0001f)
        {
            // Directly on top of each other — pick a side from the reported direction.
            horizontal = hit.Direction.x >= 0f ? 1f : -1f;
        }

        Vector2 direction = new Vector2(Mathf.Sign(horizontal), 0f);
        direction.y = knockbackUpward;
        direction = direction.normalized;

        controller.ApplyRecoil(direction * knockbackForce, knockbackLockout, maxKnockbackSpeed);
    }

    private void Respawn()
    {
        if (controller != null)
        {
            controller.ResetToSpawn();
        }

        // Greybox placeholder: restore lives at the spawn point (no game-over screen yet).
        lives = Mathf.Max(1, maxLives);
        invulnerableUntil = Time.time + hitInvulnerability;
    }
}
