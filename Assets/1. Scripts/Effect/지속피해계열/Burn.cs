using UnityEngine;

public class Burn : DamageStatus
{
    public Burn(int stack = 1, int duration = 3)
        : base(stack, duration)
    {
        Name = "Burn";
        Stack = stack;
        Damage = stack;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not Burn burn)
            return;

        Stack += burn.Stack;
        Damage = Stack;

        Duration = Mathf.Max(Duration, burn.Duration);
    }

    public override void OnTurnEnd()
    {
        if (owner == null)
            return;

        owner.TakeTrueDamage(Damage, this);

        DecreaseDuration();

        if (IsExpired())
            RemoveStatus();
    }
}