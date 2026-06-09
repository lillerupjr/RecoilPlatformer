using System;
using UnityEngine;

public class EnemyHealth2D : MonoBehaviour, IDamageable2D
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    private int currentHealth;

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

        int amount = Mathf.Max(0, damage.Amount);
        if (amount == 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth == 0)
        {
            Died?.Invoke(damage);
            return;
        }

        Damaged?.Invoke(damage);
    }
}
