using System;
using UnityEngine;

public class EnemyHealth2D : MonoBehaviour, IDamageable2D
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [Tooltip("Seconds of damage immunity after a hit. Dedups the shotgun's 3 pellets to 1 dmg/shot.")]
    [SerializeField] private float hitInvulnerability = 0.12f;

    private int currentHealth;
    private float lastHitTime = float.NegativeInfinity;

    public event Action<DamageInfo2D> Damaged;
    public event Action<DamageInfo2D> Died;

    public bool IsAlive => currentHealth > 0;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(DamageInfo2D damage)
    {
        if (!IsAlive)
        {
            return;
        }

        // Dedup near-simultaneous hits (e.g. the 3 pellets of one shotgun blast → 1 damage).
        if (Time.time - lastHitTime < hitInvulnerability)
        {
            return;
        }

        int amount = Mathf.Max(0, damage.Amount);
        if (amount == 0)
        {
            return;
        }

        lastHitTime = Time.time;
        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth == 0)
        {
            Died?.Invoke(damage);
            return;
        }

        Damaged?.Invoke(damage);
    }
}
