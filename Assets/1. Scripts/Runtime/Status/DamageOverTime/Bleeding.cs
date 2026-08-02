using UnityEngine;

/// <summary>
/// 부위 단위 출혈. 전투 종료 전까지 유지되며 턴 종료 시 현재 스택만큼
/// 해당 부위에 비파괴 고정 피해를 준 뒤 스택이 1 감소한다.
/// </summary>
public sealed class Bleeding : StatusEffect
{
    public const int ExplosionThreshold = 10;

    public bool CanExplode =>
        Stack >= ExplosionThreshold;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.Permanent;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacks;

    public Bleeding(
        int stack = 1,
        int duration = -1)
    {
        Name = "Bleeding";
        Stack = Mathf.Max(0, stack);
        Duration = -1;
    }

    public void AddStacks(int amount)
    {
        Stack = Mathf.Max(
            0,
            Stack + Mathf.Max(0, amount));
    }

    public int ConsumeAll()
    {
        int consumed = Mathf.Max(0, Stack);
        Stack = 0;
        return consumed;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is Bleeding bleeding)
            AddStacks(bleeding.Stack);
    }

    public override void OnTurnEnd(
        StatusEffectTickContext context)
    {
        if (context?.TargetCharacter == null ||
            context.TargetPart == null ||
            context.TargetPart.IsBroken ||
            Stack <= 0)
        {
            return;
        }

        int tickDamage = Stack;

        DamageRequest request =
            DamageRequest.Custom(
                DamageType.StatusPart,
                Source ?? Owner,
                context.TargetCharacter,
                context.TargetPart,
                tickDamage,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyDefense: false,
                applyGuard: false,
                applyProtection: false,
                sourceAction: null,
                sourceEffect: this);

        request.ApplyFlatDamageBonus = false;
        request.ApplyOwnerMultiplier = false;
        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;
        request.WasCritical = false;

        context.ApplyDamage(request);

        Stack = Mathf.Max(0, Stack - 1);

        if (Stack <= 0)
            RemoveStatus();
    }
}
