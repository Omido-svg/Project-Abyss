using UnityEngine;

public readonly struct AutoPlanCandidateContext
{
    public AutoPlanCandidateContext(
        BattleContext battleContext,
        PlayerAutoPlanMode mode,
        Character owner,
        BodyPart ownerPart,
        Skill skill,
        Character target,
        BodyPart targetPart,
        ActionSlot opposingSlot,
        float estimatedWinRate,
        float estimatedDamage)
    {
        BattleContext = battleContext;
        Mode = mode;
        Owner = owner;
        OwnerPart = ownerPart;
        Skill = skill;
        Target = target;
        TargetPart = targetPart;
        OpposingSlot = opposingSlot;
        EstimatedWinRate = estimatedWinRate;
        EstimatedDamage = estimatedDamage;
    }

    public BattleContext BattleContext { get; }
    public PlayerAutoPlanMode Mode { get; }
    public Character Owner { get; }
    public BodyPart OwnerPart { get; }
    public Skill Skill { get; }
    public Character Target { get; }
    public BodyPart TargetPart { get; }
    public ActionSlot OpposingSlot { get; }
    public float EstimatedWinRate { get; }
    public float EstimatedDamage { get; }
    public string SkillId => Skill?.Definition?.SkillId ?? string.Empty;
}

public interface ICharacterAutoPlanAdvisor
{
    bool Supports(Character character);
    void PreparePlan(BattleContext context, Character character, PlayerAutoPlanMode mode);
    float ScoreCandidate(in AutoPlanCandidateContext context);
    string GetSummary(Character character);
}

public static class CharacterAutoPlanAdvisorRegistry
{
    private static readonly ICharacterAutoPlanAdvisor[] Advisors =
    {
        new YujinAutoPlanAdvisor(),
        new HifumiAutoPlanAdvisor(),
        new OlafAutoPlanAdvisor()
    };

    public static void PreparePlan(BattleContext context, Character character, PlayerAutoPlanMode mode)
    {
        Resolve(character)?.PreparePlan(context, character, mode);
    }

    public static float ScoreCandidate(in AutoPlanCandidateContext context)
    {
        return Resolve(context.Owner)?.ScoreCandidate(context) ?? 0f;
    }

    public static string GetPlanSummary(Character character)
    {
        return Resolve(character)?.GetSummary(character) ?? string.Empty;
    }

    private static ICharacterAutoPlanAdvisor Resolve(Character character)
    {
        if (character == null)
            return null;

        for (int i = 0; i < Advisors.Length; i++)
        {
            ICharacterAutoPlanAdvisor advisor = Advisors[i];
            if (advisor != null && advisor.Supports(character))
                return advisor;
        }

        return null;
    }
}