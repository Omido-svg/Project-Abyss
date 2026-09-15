using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재사용 가능한 SkillEffectDefinition과 스킬별 파라미터/발동 Trigger를 결합합니다.
/// 같은 Effect Template 하나를 여러 스킬/여러 정본 Trigger에서 재사용할 수 있습니다.
/// </summary>
[Serializable]
public sealed class SkillEffectEntry
{
    public SkillEffectDefinition Definition;
    public SkillEffectOverrides Overrides = new SkillEffectOverrides();

    [Header("Trigger Override — optional")]
    [Tooltip(
        "끄면 Effect Definition의 기본 Timing을 사용합니다. " +
        "켜면 같은 Effect Template을 이 Entry에서 다른 정본 Trigger에 재사용할 수 있습니다.")]
    public bool OverrideTiming;

    public SkillEffectTiming Timing =
        SkillEffectTiming.OnExecute;

    [Header("Entry Conditions — optional")]
    [Tooltip("Trigger는 정본 5종만 사용하고, 기세/스택/흐트러짐/결과 사건 등 세부 조건은 여기서 작성합니다.")]
    public List<SkillEffectCondition> Conditions = new();

    [Header("Roll Filter — optional")]
    [Tooltip(
        "켜면 이 Entry는 지정한 n번째 굴림에서만 실행됩니다. " +
        "굴림 번호는 UI 기준 1부터 시작합니다.")]
    public bool RestrictToRoll;

    [Min(1)]
    public int RollNumber = 1;

    public bool IsValid => Definition != null;

    public SkillEffectTiming EffectiveTiming =>
        OverrideTiming
            ? Timing
            : Definition?.Timing ?? Timing;

    public SkillEffectResult TryApply(
        SkillEffectContext context,
        SkillEffectTiming currentTiming)
    {
        if (Definition == null)
            return null;

        SkillEffectTiming scheduledTiming =
            EffectiveTiming;

        bool runtimeEventWake =
            currentTiming != scheduledTiming &&
            CanWakeFromRuntimeEvent(currentTiming);

        if (currentTiming != scheduledTiming && !runtimeEventWake)
        {
            return SkillEffectResult.NotScheduled(
                Definition,
                currentTiming);
        }

        if (RestrictToRoll)
        {
            int expected =
                Mathf.Max(1, RollNumber);

            if (context == null ||
                context.RollNumber != expected)
            {
                return SkillEffectResult.NotScheduled(
                    Definition,
                    currentTiming);
            }
        }

        if (!EvaluateEntryConditions(context))
        {
            return SkillEffectResult.ConditionFailed(
                Definition,
                currentTiming,
                context,
                "Entry condition failed.");
        }

        // Runtime Event wake일 때는 내부 사건 시점을 실제 실행 시점으로 넘긴다.
        // Authoring Timing 값 자체는 5종 계약을 유지한다.
        return Definition.TryApply(
            context,
            currentTiming,
            Overrides,
            runtimeEventWake ? currentTiming : scheduledTiming);
    }

    private bool EvaluateEntryConditions(SkillEffectContext context)
    {
        if (Conditions == null || Conditions.Count == 0)
            return true;

        for (int i = 0; i < Conditions.Count; i++)
        {
            SkillEffectCondition condition = Conditions[i];
            if (condition != null && !condition.Evaluate(context))
                return false;
        }
        return true;
    }

    private bool CanWakeFromRuntimeEvent(SkillEffectTiming runtimeTiming)
    {
        if (SkillEffectTimingCatalog.IsAuthoringTiming(runtimeTiming))
            return false;

        if (Conditions != null)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                SkillEffectCondition condition = Conditions[i];
                if (condition != null &&
                    condition.IsRuntimeEventConditionFor(runtimeTiming))
                {
                    return true;
                }
            }
        }

        return Definition != null &&
               Definition.CanWakeFromRuntimeEvent(runtimeTiming);
    }

    public static SkillEffectEntry FromLegacy(
        SkillEffectDefinition definition)
    {
        return new SkillEffectEntry
        {
            Definition = definition,
            Overrides = new SkillEffectOverrides(),
            OverrideTiming = false,
            Timing = definition != null
                ? definition.Timing
                : SkillEffectTiming.OnExecute,
            Conditions = new List<SkillEffectCondition>(),
            RestrictToRoll = false,
            RollNumber = 1
        };
    }
}
