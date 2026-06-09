public interface IDamageable2D
{
    bool IsAlive { get; }

    void TakeDamage(DamageInfo2D damage);
}
