using System;
using UnityEngine;

/// <summary>
/// 재사용 가능한 SkillEffectDefinition과 스킬별 파라미터/실행 시점을 결합합니다.
/// 같은 Effect Template 하나를 여러 스킬/여러 세부 페이즈에서 재사용할 수 있습니다.
/// </summary>
[Serializable]
public sealed class SkillEffectEntry
{
    public SkillEffectDefinition Definition;
    public SkillEffectOverrides Overrides = new SkillEffectOverrides();

    [Header("Schedule Override — optional")]
    [Tooltip(
        "끄면 Effect Definition의 기본 Timing을 사용합니다. " +
        "켜면 같은 Effect Template을 이 Entry에서 다른 세부 페이즈에 재사용할 수 있습니다.")]
    public bool OverrideTiming;

    public SkillEffectTiming Timing =
        SkillEffectTiming.OnExecute;

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

        if (currentTiming != scheduledTiming)
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

        return Definition.TryApply(
            context,
            currentTiming,
            Overrides,
            scheduledTiming);
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
            RestrictToRoll = false,
            RollNumber = 1
        };
    }
}
