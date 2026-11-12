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
        if (skill is StageOneTemporarySkill1112) return true;
        if (skill?.ActionType != ActionType.Preparation) return false;
        var definition = skill.Definition;
        return skill.Owner is Enemy ||
            definition?.PreparationTiming == PreparationActivationTiming.Immediate ||
            YujinMechanic.IsHwanhyeongSkill(definition?.SkillId) ||
            definition?.SkillId == OlafSkillIds.BreathBoughtWithBlood;
    }
}
