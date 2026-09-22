using System;
using System.Collections.Generic;
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
    KilledTarget = 14,
    RollSucceeded = 15,
    RollFailed = 16,
    RollNumberEquals = 17,
    RollTypeAttack = 18,   // legacy compatibility
    RollTypeStagger = 19,  // legacy compatibility

    // 0915 canonical condition layer. Trigger는 5종으로 유지하고 세부 사건/상태는 조건으로 표현한다.
    CurrentMomentumState = 20,
    PreviousTurnMomentumState = 21,
    CurrentMomentumRange = 22,
    StatusStackRange = 23,
    StaggerRatioRange = 24,
    NegativeStatusCountRange = 25,
    ResourceRange = 26,
    TargetWasWeakenedByThisDamage = 27,
    TargetWasBrokenByThisDamage = 28,
    RuntimeAfterDamage = 29,

    // 0922 condition API: raw presence / numeric total / effective axis를 분리한다.
    StatusPresent = 30,
    StatusNumericTotalRange = 31,
    StatusEffectiveAxisRange = 32
}

public enum SkillEffectConditionSubject
{
    Owner = 0,
    Target = 1
}

[Serializable]
public class SkillEffectCondition
{
    public SkillEffectConditionType Type = SkillEffectConditionType.Always;
    public bool Invert;

    [Header("Subject")]
    public SkillEffectConditionSubject Subject = SkillEffectConditionSubject.Target;

    [Header("Status")]
    public StatusEffectId StatusEffectId;
    public bool CheckCharacterStatus = true;
    public bool CheckPartStatus = true;
    public StatusEffectEffectiveAxis EffectiveStatusAxis =
        StatusEffectEffectiveAxis.RollShift;

    [Header("Part")]
    public bool AnyOwnerPart = true;
    public PartType OwnerPartType;

    [Header("Threshold / Range")]
    public int Threshold = 1;
    public int Minimum = 0;
    public int Maximum = int.MaxValue;
    [Range(0f, 1f)] public float Ratio = 0.5f;
    [Range(0f, 1f)] public float MinimumRatio = 0f;
    [Range(0f, 1f)] public float MaximumRatio = 1f;
    public string ResourceKey;
    public MomentumState MomentumState = MomentumState.Balance;

    public bool Evaluate(SkillEffectContext context)
    {
        bool result = EvaluateInternal(context);
        return Invert ? !result : result;
    }

    private bool EvaluateInternal(SkillEffectContext context)
    {
        if (context == null)
            return false;

        Character subject = Subject == SkillEffectConditionSubject.Owner
            ? context.Owner
            : context.Target;
        BodyPart subjectPart = Subject == SkillEffectConditionSubject.Owner
            ? context.OwnerPart
            : context.TargetPart;

        switch (Type)
        {
            case SkillEffectConditionType.Always:
                return true;

            case SkillEffectConditionType.ClashWon:
                return context.ClashResult != null &&
                       context.ClashResult.WinnerAction == context.Action;

            case SkillEffectConditionType.ClashLost:
                return context.ClashResult != null &&
                       context.ClashResult.LoserAction == context.Action;

            case SkillEffectConditionType.WasCritical:
                return context.DamageContext?.WasCritical == true;

            case SkillEffectConditionType.TargetHasStatus:
                // Legacy 이름은 유지하지만 판정 의미는 raw presence다.
                return StatusEffectFactory.HasStatus(
                    context.Target,
                    context.TargetPart,
                    StatusEffectId,
                    CheckCharacterStatus,
                    CheckPartStatus);

            case SkillEffectConditionType.OwnerHasBrokenPart:
                return OwnerHasBrokenPart(context.Owner);

            case SkillEffectConditionType.CustomResourceAtLeast:
                return SkillResourceAccess.Get(context.Owner, ResourceKey) >= Threshold;

            case SkillEffectConditionType.PrestigeAtLeast:
                return context.Owner?.RuntimeStatus != null &&
                       context.Owner.RuntimeStatus.currentPrestige >= Threshold;

            case SkillEffectConditionType.FirstUseThisTurn:
                return context.IsFirstUseThisTurn;

            case SkillEffectConditionType.DamageDealtAtLeast:
                return context.DamageContext != null &&
                       context.DamageContext.GetDisplayDamage() >= Threshold;

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

            case SkillEffectConditionType.RollSucceeded:
                return context.RollNumber > 0 && context.RollSucceeded;

            case SkillEffectConditionType.RollFailed:
                return context.RollNumber > 0 && !context.RollSucceeded;

            case SkillEffectConditionType.RollNumberEquals:
                return context.RollNumber > 0 &&
                       context.RollNumber == Mathf.Max(1, Threshold);

            case SkillEffectConditionType.RollTypeAttack:
                return context.RollNumber > 0 &&
                       context.RollType == CombatRollType.Attack;

            case SkillEffectConditionType.RollTypeStagger:
                return context.RollNumber > 0 &&
                       context.RollType == CombatRollType.Stagger;

            case SkillEffectConditionType.CurrentMomentumState:
                return context.BattleContext?.Services?.MomentumManager?
                           .GetCurrentBand(subject) == MomentumState;

            case SkillEffectConditionType.PreviousTurnMomentumState:
                return context.BattleContext?.Services?.MomentumManager?
                           .GetPreviousTurnFinalState(subject) == MomentumState;

            case SkillEffectConditionType.CurrentMomentumRange:
            {
                int value = context.BattleContext?.Services?.MomentumManager?
                                .GetPerspectiveValue(subject) ?? 0;
                return IsInRange(value);
            }

            case SkillEffectConditionType.StatusStackRange:
                // Legacy serialized condition. 기존 Stack 합산 의미를 보존한다.
                return IsInRange(GetStatusStack(subject, subjectPart));

            case SkillEffectConditionType.StatusPresent:
                return StatusEffectFactory.HasStatus(
                    subject,
                    subjectPart,
                    StatusEffectId,
                    CheckCharacterStatus,
                    CheckPartStatus);

            case SkillEffectConditionType.StatusNumericTotalRange:
                return IsInRange(
                    StatusEffectFactory.GetNumericTotal(
                        subject,
                        subjectPart,
                        StatusEffectId,
                        CheckCharacterStatus,
                        CheckPartStatus));

            case SkillEffectConditionType.StatusEffectiveAxisRange:
                return IsInRange(
                    StatusEffectFactory.GetEffectiveAxis(
                        subject,
                        subjectPart,
                        EffectiveStatusAxis,
                        CheckCharacterStatus,
                        CheckPartStatus));

            case SkillEffectConditionType.StaggerRatioRange:
            {
                StaggerGaugeMechanic stagger = subject?.GetMechanic<StaggerGaugeMechanic>();
                float ratio = stagger == null || stagger.MaxGauge <= 0
                    ? 1f
                    : Mathf.Clamp01(stagger.CurrentGauge / (float)stagger.MaxGauge);
                return ratio >= Mathf.Clamp01(MinimumRatio) &&
                       ratio <= Mathf.Clamp01(MaximumRatio);
            }

            case SkillEffectConditionType.NegativeStatusCountRange:
                return IsInRange(CountNegativeStatuses(subject, subjectPart));

            case SkillEffectConditionType.ResourceRange:
                return IsInRange(SkillResourceAccess.Get(subject, ResourceKey));

            case SkillEffectConditionType.TargetWasWeakenedByThisDamage:
                return context.DamageContext?.WeakenedPart == true;

            case SkillEffectConditionType.TargetWasBrokenByThisDamage:
                return context.DamageContext?.BrokePart == true;

            case SkillEffectConditionType.RuntimeAfterDamage:
                return context.Timing == SkillEffectTiming.AfterDamage &&
                       context.DamageContext != null;

            default:
                return false;
        }
    }

    /// <summary>
    /// C-42: SkillEffectTiming은 5종만 authoring한다.
    /// 피해/크리/처치/부위 전이처럼 결과가 확정된 뒤에만 알 수 있는 사건은
    /// 내부 Runtime Event가 이 Condition을 깨워 재평가한다.
    /// </summary>
    public bool IsRuntimeEventConditionFor(SkillEffectTiming runtimeTiming)
    {
        return Type switch
        {
            SkillEffectConditionType.WasCritical =>
                runtimeTiming == SkillEffectTiming.OnCritical,
            SkillEffectConditionType.KilledTarget =>
                runtimeTiming == SkillEffectTiming.OnKill,
            SkillEffectConditionType.DamageDealtAtLeast =>
                runtimeTiming == SkillEffectTiming.AfterDamage,
            SkillEffectConditionType.TargetWasWeakenedByThisDamage =>
                runtimeTiming == SkillEffectTiming.OnPartWeakened,
            SkillEffectConditionType.TargetWasBrokenByThisDamage =>
                runtimeTiming == SkillEffectTiming.OnPartBroken,
            SkillEffectConditionType.RuntimeAfterDamage =>
                runtimeTiming == SkillEffectTiming.AfterDamage,
            _ => false
        };
    }

    private bool IsInRange(int value)
    {
        int min = Minimum;
        int max = Maximum < min ? min : Maximum;
        return value >= min && value <= max;
    }

    private int GetStatusStack(Character target, BodyPart part)
    {
        return StatusEffectFactory.GetLegacyStackTotal(
            target,
            part,
            StatusEffectId,
            CheckCharacterStatus,
            CheckPartStatus);
    }

    private bool MatchesStatus(StatusEffect effect)
    {
        return StatusEffectFactory.MatchesStatusId(
            StatusEffectId,
            effect);
    }

    private static int CountNegativeStatuses(Character target, BodyPart part)
    {
        if (target == null)
            return 0;

        // 정본의 "부정적 상태 개수"는 같은 키워드의 스택/부위 중복이 아니라
        // 서로 다른 부정 상태 종류 수로 해석한다.
        HashSet<Type> types = new();
        if (target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (IsNegative(effect))
                    types.Add(effect.GetType());
            }
        }
        if (part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (IsNegative(effect))
                    types.Add(effect.GetType());
            }
        }
        return types.Count;
    }

    private static bool IsNegative(StatusEffect effect) =>
        effect is Bleeding || effect is Burn || effect is Stun ||
        effect is WeaknessStatus || effect is DisarmStatus ||
        effect is FractureStatus || effect is RuptureStatus ||
        effect is StagnationStatus || effect is PainStatus ||
        effect is OlafFearStatus || effect is BrokenPartStatus ||
        effect is PartDisabledStatus;

    private bool OwnerHasBrokenPart(Character owner)
    {
        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || !part.IsBroken)
                continue;

            if (AnyOwnerPart || part.Type == OwnerPartType)
                return true;
        }
        return false;
    }

    private static float GetHpRatio(Character target)
    {
        if (target == null)
            return 1f;

        int maxHp = Mathf.Max(1, target.MaxCombatHP);
        return Mathf.Clamp01(target.CurrentHP / (float)maxHp);
    }
}