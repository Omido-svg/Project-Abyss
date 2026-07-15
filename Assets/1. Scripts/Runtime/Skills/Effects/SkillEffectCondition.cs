using System;
using UnityEngine;

public enum SkillEffectConditionType
{
    Always = 0,
    ClashWon = 1,
    ClashLost = 2,
    WasCritical = 3,
    TargetHasStatus = 4,
    OwnerHasBrokenPart = 5,
    CustomResourceAtLeast = 6,
    PrestigeAtLeast = 7,
    FirstUseThisTurn = 8,
    DamageDealtAtLeast = 9,
    TargetPartIsWeakened = 10,
    TargetPartIsBroken = 11,
    TargetHpRatioAtMost = 12,
    OwnerHpRatioAtMost = 13,
    KilledTarget = 14
}

[Serializable]
public class SkillEffectCondition
{
    public SkillEffectConditionType Type =
        SkillEffectConditionType.Always;

    public bool Invert;

    [Header("Status")]
    public StatusEffectId StatusEffectId;
    public bool CheckCharacterStatus = true;
    public bool CheckPartStatus = true;

    [Header("Part")]
    public bool AnyOwnerPart = true;
    public PartType OwnerPartType;

    [Header("Threshold")]
    public int Threshold = 1;
    [Range(0f, 1f)] public float Ratio = 0.5f;
    public string ResourceKey;

    public bool Evaluate(SkillEffectContext context)
    {
        bool result = EvaluateInternal(context);
        return Invert ? !result : result;
    }

    private bool EvaluateInternal(
        SkillEffectContext context)
    {
        if (context == null)
            return false;

        switch (Type)
        {
            case SkillEffectConditionType.Always:
                return true;

            case SkillEffectConditionType.ClashWon:
                return context.Timing ==
                       SkillEffectTiming.OnClashWin;

            case SkillEffectConditionType.ClashLost:
                return context.Timing ==
                       SkillEffectTiming.OnClashLose;

            case SkillEffectConditionType.WasCritical:
                return context.DamageContext?.WasCritical == true;

            case SkillEffectConditionType.TargetHasStatus:
                return HasRequestedStatus(
                    context.Target,
                    context.TargetPart);

            case SkillEffectConditionType.OwnerHasBrokenPart:
                return OwnerHasBrokenPart(
                    context.Owner);

            case SkillEffectConditionType.CustomResourceAtLeast:
                return SkillResourceAccess.Get(
                           context.Owner,
                           ResourceKey) >=
                       Threshold;

            case SkillEffectConditionType.PrestigeAtLeast:
                return context.Owner?.RuntimeStatus != null &&
                       context.Owner.RuntimeStatus.currentPrestige >=
                       Threshold;

            case SkillEffectConditionType.FirstUseThisTurn:
                return context.IsFirstUseThisTurn;

            case SkillEffectConditionType.DamageDealtAtLeast:
                return context.DamageContext != null &&
                       context.DamageContext.GetDisplayDamage() >=
                       Threshold;

            case SkillEffectConditionType.TargetPartIsWeakened:
                return context.TargetPart?.IsWeakened == true;

            case SkillEffectConditionType.TargetPartIsBroken:
                return context.TargetPart?.IsBroken == true;

            case SkillEffectConditionType.TargetHpRatioAtMost:
                return GetHpRatio(context.Target) <= Ratio;

            case SkillEffectConditionType.OwnerHpRatioAtMost:
                return GetHpRatio(context.Owner) <= Ratio;

            case SkillEffectConditionType.KilledTarget:
                return context.KillContext?.Victim != null ||
                       context.DamageContext?.WasKilled == true;

            default:
                return false;
        }
    }

    private bool HasRequestedStatus(
        Character target,
        BodyPart part)
    {
        if (target == null)
            return false;

        bool characterResult = false;
        bool partResult = false;

        switch (StatusEffectId)
        {
            case StatusEffectId.Bleeding:
                if (CheckCharacterStatus)
                    characterResult =
                        target.GetStatus<Bleeding>() != null;

                if (CheckPartStatus && part != null)
                    partResult =
                        target.GetPartStatus<Bleeding>(part) != null;
                break;

            case StatusEffectId.Burn:
                if (CheckCharacterStatus)
                    characterResult =
                        target.GetStatus<Burn>() != null;

                if (CheckPartStatus && part != null)
                    partResult =
                        target.GetPartStatus<Burn>(part) != null;
                break;

            case StatusEffectId.Stun:
                if (CheckCharacterStatus)
                    characterResult =
                        target.GetStatus<Stun>() != null;

                if (CheckPartStatus && part != null)
                    partResult =
                        target.GetPartStatus<Stun>(part) != null;
                break;
        }

        return characterResult || partResult;
    }

    private bool OwnerHasBrokenPart(
        Character owner)
    {
        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || !part.IsBroken)
                continue;

            if (AnyOwnerPart ||
                part.Type == OwnerPartType)
            {
                return true;
            }
        }

        return false;
    }

    private float GetHpRatio(Character target)
    {
        if (target == null)
            return 1f;

        int maxHp = Mathf.Max(1, target.MaxCombatHP);
        return Mathf.Clamp01(
            target.CurrentHP / (float)maxHp);
    }
}
