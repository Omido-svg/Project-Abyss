using System.Collections.Generic;

public class BattleVisualRequest
{
    public BattleAction SourceAction;
    public BattleAction OpponentAction;

    public Character Attacker;
    public Character Target;
    public BodyPart TargetPart;

    public ActionType ActionType;
    public SkillVisualDefinition VisualDefinition;

    public List<ClashRollVisualStep> ClashSteps = new();
    public List<int> HitDamages = new();

    public int FallbackDamage;

    public static BattleVisualRequest FromAction(
        BattleAction action,
        SkillVisualDefinition visualDefinition)
    {
        BattleVisualRequest request =
            new BattleVisualRequest();

        request.SourceAction = action;

        if (action == null)
            return request;

        request.Attacker = action.Owner;
        request.Target = action.Target;
        request.TargetPart = action.TargetPart;

        if (action.Skill != null)
            request.ActionType = action.Skill.ActionType;

        request.VisualDefinition = visualDefinition;

        return request;
    }

    public int GetDamageForHitIndex(int hitIndex)
    {
        if (HitDamages == null || HitDamages.Count == 0)
            return FallbackDamage;

        if (hitIndex < HitDamages.Count)
            return HitDamages[hitIndex];

        return HitDamages[HitDamages.Count - 1];
    }
}