using UnityEngine;

public class Bleeding : DamageStatus
{
    public const int ExplosionThreshold = 3;

    public bool CanExplode => Stack >= ExplosionThreshold;

    public Bleeding(int stack = 1, int duration = 3)
        : base(stack, duration)
    {
        Name = "Bleeding";
        Stack = stack;
        Duration = duration;

        // 출혈 피해량 = 스택
        Damage = Stack;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not Bleeding bleeding)
            return;

        Stack += bleeding.Stack;

        // 출혈 피해량 = 현재 스택 수
        Damage = Stack;

        // 새로 부여되면 지속시간 갱신
        Duration = Mathf.Max(Duration, bleeding.Duration);
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

        if (context.IsPartStatus)
        {
            resolver.ApplyStatusPartDamage(
                EffectRequest.StatusPartDamage(
                    Source,
                    context.TargetCharacter,
                    context.TargetPart,
                    damage,
                    this));
        }
        else
        {
            resolver.ApplyTrueDamage(
                EffectRequest.TrueDamage(
                    Source,
                    context.TargetCharacter,
                    damage,
                    this));
        }
    }
}