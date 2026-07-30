using System;

/// <summary>
/// 재사용 가능한 SkillEffectDefinition과 스킬별 파라미터를 결합합니다.
/// 같은 출혈 Template 하나를 여러 스킬이 참조하면서 Stack/Duration만 다르게 지정할 수 있습니다.
/// </summary>
[Serializable]
public sealed class SkillEffectEntry
{
    public SkillEffectDefinition Definition;
    public SkillEffectOverrides Overrides = new SkillEffectOverrides();

    public bool IsValid => Definition != null;

    public SkillEffectResult TryApply(
        SkillEffectContext context,
        SkillEffectTiming currentTiming)
    {
        return Definition == null
            ? null
            : Definition.TryApply(context, currentTiming, Overrides);
    }

    public static SkillEffectEntry FromLegacy(
        SkillEffectDefinition definition)
    {
        return new SkillEffectEntry
        {
            Definition = definition,
            Overrides = new SkillEffectOverrides()
        };
    }
}
