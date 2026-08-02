using UnityEngine;

public sealed class OlafAutoPlanAdvisor : ICharacterAutoPlanAdvisor
{
    public bool Supports(Character character) => character is Olaf;

    public void PreparePlan(BattleContext context, Character character, PlayerAutoPlanMode mode)
    {
        // 올라프는 계획 시 즉시 변경해야 하는 토글이 없다.
        // 광기/만개/출혈/자신 부위 상태는 후보 평가에서 직접 읽는다.
    }

    public float ScoreCandidate(in AutoPlanCandidateContext context)
    {
        Olaf olaf = context.Owner as Olaf;
        OlafMadnessMechanic mechanic = olaf?.MadnessMechanic;
        if (mechanic == null)
            return 0f;

        string id = context.SkillId;
        int madness = mechanic.CurrentMadness;
        int bleeding = context.TargetPart == null
            ? context.Target?.GetStatus<Bleeding>()?.Stack ?? 0
            : context.Target?.GetPartStatus<Bleeding>(context.TargetPart)?.Stack ?? 0;
        int damagedParts = CountDamagedParts(olaf);
        float score = bleeding * (context.Mode == PlayerAutoPlanMode.Damage ? 45f : 18f);

        if (id == OlafSkillIds.Standard)
            score += bleeding >= 5 ? 1000f : 180f;
        else if (id == OlafSkillIds.Rend)
            score += bleeding * 70f + madness * 80f;
        else if (id == OlafSkillIds.BloomingWound)
            score += madness >= mechanic.MaxMadness ? 1800f : madness * 90f;
        else if (id == OlafSkillIds.BurstingMadness)
            score += madness * (context.Mode == PlayerAutoPlanMode.Damage ? 260f : 120f);
        else if (id == OlafSkillIds.BacksToWall)
            score += damagedParts * 550f + (olaf.CurrentHP <= olaf.MaxCombatHP / 3 ? 900f : 0f);
        else if (id == OlafSkillIds.Crouch)
            score += damagedParts > 0 ? 300f : 30f;
        else if (id == OlafSkillIds.Glare)
            score += context.Mode == PlayerAutoPlanMode.WinRate ? 360f : 80f;

        if (mechanic.IsBlooming)
            score += context.Skill?.ActionType == ActionType.NormalAttack ? 180f : 0f;

        return score;
    }

    public string GetSummary(Character character)
    {
        OlafMadnessMechanic mechanic = (character as Olaf)?.MadnessMechanic;
        return mechanic == null
            ? string.Empty
            : $"광기 {mechanic.CurrentMadness}/{mechanic.MaxMadness}{(mechanic.IsBlooming ? " / 만개" : string.Empty)}";
    }

    private static int CountDamagedParts(Olaf olaf)
    {
        if (olaf?.BodyParts == null)
            return 0;
        int count = 0;
        foreach (BodyPart part in olaf.BodyParts)
        {
            if (part != null && (part.IsBroken || part.IsWeakened))
                count++;
        }
        return count;
    }
}
