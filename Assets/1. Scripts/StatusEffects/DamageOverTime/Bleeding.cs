using UnityEngine;

public class Bleeding : DamageStatus
{
    public const int ExplosionThreshold = 3;

    public bool CanExplode => Stack >= ExplosionThreshold;

    public Bleeding(int stack = 1, int duration = 3)
        : base(stack, duration)
    {
        Name = "Bleeding";
        Stack = Mathf.Max(0, stack);
        Damage = Stack;
    }

    protected override bool UsePartDamage =>
        IsPartEffect;

    public override void Merge(StatusEffect other)
    {
        if (other is not Bleeding bleeding)
            return;

        Stack += Mathf.Max(0, bleeding.Stack);
        Damage = Stack;
        Duration = Mathf.Max(Duration, bleeding.Duration);
    }
}
