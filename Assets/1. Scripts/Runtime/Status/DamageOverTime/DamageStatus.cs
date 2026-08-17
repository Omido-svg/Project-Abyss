using UnityEngine;

public abstract class DamageStatus : StatusEffect
{
    protected int Damage;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    protected DamageStatus(
        int damage,
        int duration)
    {
        Damage = Mathf.Max(0, damage);
        Duration = Mathf.Max(0, duration);
    }

    protected virtual bool UsePartDamage =>
        IsPartEffect;

    protected virtual int GetTickDamage(
        StatusEffectTickContext context)
    {
        return Mathf.Max(0, Stack);
    }

    public override void OnTurnEnd(
        StatusEffectTickContext context)
    {
        if (context?.TargetCharacter == null)
            return;

        int damage = GetTickDamage(context);

        if (damage <= 0)
            return;

        bool usePartDamage =
            UsePartDamage &&
            context.TargetPart != null &&
            !context.TargetPart.IsBroken;

        DamageRequest request =
            DamageRequest.Custom(
                usePartDamage
                    ? DamageType.StatusPart
                    : DamageType.True,
                Source ?? Owner,
                context.TargetCharacter,
                usePartDamage
                    ? context.TargetPart
                    : null,
                damage,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: false,
                sourceAction: null,
                sourceEffect: this);

        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;
        request.WasCritical = false;

        context.ApplyDamage(request);
    }

    // 기존 직접 호출 호환용. 지속시간 감소는 컨트롤러가 담당한다.
    public override void OnTurnEnd()
    {
        if (Owner == null)
            return;

        OnTurnEnd(
            new StatusEffectTickContext(
                Owner,
                OwnerPart,
                this,
                StatusEffectTickTiming.TurnEnd));
    }
}
