using UnityEngine;

public readonly struct DamageInfo2D
{
    public DamageInfo2D(int amount, GameObject source, Vector2 point, Vector2 direction)
    {
        Amount = amount;
        Source = source;
        Point = point;
        Direction = direction;
    }

    public int Amount { get; }
    public GameObject Source { get; }
    public Vector2 Point { get; }
    public Vector2 Direction { get; }
}
