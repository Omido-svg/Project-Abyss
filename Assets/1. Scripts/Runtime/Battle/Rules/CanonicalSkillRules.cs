using UnityEngine;

public enum SkillColor
{
    Unset = 0,
    Red = 1,
    Blue = 2
}

public enum SkillBasePowerRule
{
    CanonicalCurve = 0,
    FixedBase = 1,
    CanonicalPlusFlat = 2,
    LegacyExplicit = 3
}

/// <summary>
/// 0915 정본의 공통 스킬 계약 단일 소스.
/// - 기본위력 = 굴림 수 곡선 + 빛(에너지 비용) * 2
/// - 의도된 캐릭터/카드 예외만 명시적 Rule로 빠진다.
/// </summary>
public static class PowerFormulaService
{
    public const int EnergyPowerPerPoint = 2;

    public static bool TryGetCurveValue(int rollCount, out int value)
    {
        value = rollCount switch
        {
            1 => 20,
            2 => 12,
            3 => 11,
            4 => 9,
            5 => 8,
            _ => 0
        };
        return value > 0;
    }


    public static int GetEffectiveEnergyCost(SkillDefinition definition)
    {
        if (definition == null)
            return 0;

        if (definition.OverrideEnergyCost)
            return Mathf.Max(0, definition.EnergyCost);

        return definition.ActionType switch
        {
            ActionType.Duel => 1,
            ActionType.NormalAttack => 0,
            ActionType.Preparation => 1,
            ActionType.Prestige => 0,
            _ => 0
        };
    }

    public static int ResolveBasePower(SkillDefinition definition)
    {
        if (definition == null)
            return 0;

        int energyBonus = GetEffectiveEnergyCost(definition) * EnergyPowerPerPoint;
        int explicitBase = definition.BasePower;

        switch (definition.BasePowerRule)
        {
            case SkillBasePowerRule.FixedBase:
                return explicitBase + energyBonus;

            case SkillBasePowerRule.CanonicalPlusFlat:
                if (TryGetCurveValue(definition.EffectiveRollCount, out int curveWithFlat))
                    return curveWithFlat + energyBonus + definition.BasePowerFlatAdjustment;
                return explicitBase;

            case SkillBasePowerRule.LegacyExplicit:
                return explicitBase;

            default:
                if (TryGetCurveValue(definition.EffectiveRollCount, out int curve))
                    return curve + energyBonus;
                // 0915 곡선이 아직 정의하지 않은 6~8굴림은 임의 외삽하지 않는다.
                return explicitBase;
        }
    }
}

public static class SkillColorRules
{
    public static SkillColor Resolve(SkillDefinition definition)
    {
        if (definition == null)
            return SkillColor.Red;

        if (definition.Color != SkillColor.Unset)
            return definition.Color;

        // 기존 SO 마이그레이션 전 안전 호환: 첫 roll의 legacy Type만 읽는다.
        // 신규 authoring은 SkillDefinition.Color를 반드시 지정한다.
        SkillRollData first = definition.Rolls != null && definition.Rolls.Count > 0
            ? definition.Rolls[0]
            : null;
        return first != null && first.Type == CombatRollType.Stagger
            ? SkillColor.Blue
            : SkillColor.Red;
    }
}
