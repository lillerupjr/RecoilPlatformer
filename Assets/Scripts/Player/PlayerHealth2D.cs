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

    [Header("References")]
    [SerializeField] private PlayerController2D controller;
    [Tooltip("Optional — flashed on hit.")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    private int lives;
    private float invulnerableUntil;

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
            Died?.Invoke(hit);
            Respawn();
            return;
        }

        Damaged?.Invoke(hit);
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
