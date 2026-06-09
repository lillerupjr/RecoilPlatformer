using UnityEngine;

public readonly struct EnemyHitInfo2D
{
    public EnemyHitInfo2D(int damage, GameObject source, Vector2 point, Vector2 direction)
    {
        Damage = damage;
        Source = source;
        Point = point;
        Direction = direction;
    }

    public int Damage { get; }
    public GameObject Source { get; }
    public Vector2 Point { get; }
    public Vector2 Direction { get; }
}
