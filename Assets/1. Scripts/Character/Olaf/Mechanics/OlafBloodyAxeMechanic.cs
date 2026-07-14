using System.Collections.Generic;
using UnityEngine;

public sealed class OlafBloodyAxeMechanic :
    ReactiveCombatMechanic
{
    private readonly int bleedingAmount;
    private readonly bool requireNormalAttack;
    private readonly bool requirePositiveDamage;
    private readonly bool ignoreStatusDamage;
    private readonly bool triggerOncePerAction;
    private readonly bool applyToCharacterWhenPartUnavailable;

    private readonly HashSet<long> processedActionIds =
        new();

    public override string MechanicName =>
        "피 묻은 도끼";

    protected override ReactiveCombatEventMask EventMask =>
        ReactiveCombatEventMask.DamageResolved |
        ReactiveCombatEventMask.ActionEnd;

    public OlafBloodyAxeMechanic()
        : this(
            1,
            true,
            true,
            true,
            true,
            true)
    {
    }

    public OlafBloodyAxeMechanic(
        int bleedingAmount,
        bool requireNormalAttack,
        bool requirePositiveDamage,
        bool ignoreStatusDamage,
        bool triggerOncePerAction,
        bool applyToCharacterWhenPartUnavailable)
    {
        this.bleedingAmount =
            Mathf.Max(
                1,
                bleedingAmount);

        this.requireNormalAttack =
            requireNormalAttack;

        this.requirePositiveDamage =
            requirePositiveDamage;

        this.ignoreStatusDamage =
            ignoreStatusDamage;

        this.triggerOncePerAction =
            triggerOncePerAction;

        this.applyToCharacterWhenPartUnavailable =
            applyToCharacterWhenPartUnavailable;
    }

    protected override void OnDamageResolved(
        DamageEventResult eventResult)
    {
        DamageContext context =
            eventResult?.Context;

        if (!IsValidDamage(context))
            return;

        long actionId =
            context.Action?.ActionId ?? 0;

        if (triggerOncePerAction &&
            actionId > 0 &&
            processedActionIds.Contains(actionId))
        {
            return;
        }

        BattleEffectResolver resolver =
            battleContext?.EffectResolver;

        if (resolver == null)
            return;

        Bleeding bleeding =
            new(bleedingAmount);

        bool applied =
            TryApplyBleeding(
                resolver,
                context,
                bleeding);

        if (!applied)
            return;

        if (triggerOncePerAction &&
            actionId > 0)
        {
            processedActionIds.Add(actionId);
        }

        Debug.Log(
            $"{GetOwnerName()} 아이템 발동 : " +
            $"{MechanicName} / 출혈 {bleedingAmount} 추가 / " +
            $"ActionId={actionId}, " +
            $"Target={GetTargetName(context)}");
    }

    protected override void OnActionEnded(
        BattleAction action)
    {
        if (action == null ||
            action.ActionId <= 0)
        {
            return;
        }

        processedActionIds.Remove(
            action.ActionId);
    }

    protected override void OnReactiveUnregistered()
    {
        processedActionIds.Clear();
    }

    private bool IsValidDamage(
        DamageContext context)
    {
        if (context == null ||
            context.Attacker != owner ||
            context.Target == null ||
            context.Target.IsDead)
        {
            return false;
        }

        BattleAction action =
            context.Action;

        if (requireNormalAttack)
        {
            if (action?.Skill == null ||
                action.ActionType !=
                    ActionType.NormalAttack)
            {
                return false;
            }
        }

        if (requirePositiveDamage &&
            context.GetDisplayDamage() <= 0)
        {
            return false;
        }

        if (ignoreStatusDamage &&
            context.Request.SourceEffect != null)
        {
            return false;
        }

        return true;
    }

    private bool TryApplyBleeding(
        BattleEffectResolver resolver,
        DamageContext context,
        Bleeding bleeding)
    {
        if (resolver == null ||
            context == null ||
            bleeding == null)
        {
            return false;
        }

        if (context.TargetPart != null &&
            !context.TargetPart.IsBroken)
        {
            return resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    owner,
                    context.Target,
                    context.TargetPart,
                    bleeding));
        }

        if (!applyToCharacterWhenPartUnavailable)
            return false;

        return resolver.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(
                owner,
                context.Target,
                bleeding));
    }

    private string GetOwnerName()
    {
        return owner?.Data?.CharacterName ??
               owner?.name ??
               "NULL_OWNER";
    }

    private static string GetTargetName(
        DamageContext context)
    {
        if (context?.Target == null)
            return "NULL_TARGET";

        string name =
            context.Target.Data?.CharacterName ??
            context.Target.name;

        return context.TargetPart == null
            ? $"{name}/SINGLE_HP"
            : $"{name}/{context.TargetPart.Type}";
    }
}
