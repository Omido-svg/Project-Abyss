using System;

public enum PreparationActivationTiming
{
    Resolution = 0,
    Immediate = 1
}

/// <summary>1112 §8. Timing is authored independently of cost and effect.</summary>
public static class PreparationActivationPolicy
{
    public static bool IsImmediate(Skill skill)
    {
        // 12.5(11-17): 기계 보스 냉각 카드는 해결 도사림 — 해결 단계 시작에 발동한다.
        // 심문관 고해 임시 카드는 봉인된 장착 스킬을 계획 단계에 되돌리므로 즉시 그대로.
        if (skill is StageOneTemporarySkill temporary) return !temporary.IsCooling;
        if (skill?.ActionType != ActionType.Preparation) return false;
        var definition = skill.Definition;
        return skill.Owner is Enemy ||
            definition?.PreparationTiming == PreparationActivationTiming.Immediate ||
            YujinMechanic.IsHwanhyeongSkill(definition?.SkillId) ||
            definition?.SkillId == OlafSkillIds.BreathBoughtWithBlood;
    }
}
