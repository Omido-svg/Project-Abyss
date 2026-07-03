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

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (context == null)
            return;

        if (context.TargetCharacter == null)
            return;

        BattleEffectResolver resolver =
            context.Resolver;

        if (resolver == null)
            return;

        int damage =
            Stack;

        if (damage <= 0)
            return;

        resolver.ApplyTrueDamage(
            EffectRequest.TrueDamage(
                Source,
                context.TargetCharacter,
                damage,
                this));
    }
}