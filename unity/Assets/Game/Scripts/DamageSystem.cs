using UnityEngine;

public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison
}

public enum StatusEffectType
{
    None = 0,
    Burn = 1,
    Freeze = 2,
    Poison = 3,
    Shock = 4,
    SpeedBoost = 5
}

public struct DamageInfo
{
    public float Amount;
    public DamageType Type;
    public Vector2 KnockbackForce;
    public GameObject Source;

    public DamageInfo(float amount, DamageType type, Vector2 knockbackForce, GameObject source)
    {
        Amount = amount;
        Type = type;
        KnockbackForce = knockbackForce;
        Source = source;
    }
}
