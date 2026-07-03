using UnityEngine;

public class ElitePreparationSkill : PreparationSkill
{
    public ElitePreparationSkill()
    {
        SkillName = "전투 태세(도사림)";

        BasePower = 0;

        Resolver = new DiceResolver(0, 0);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        if (action.Target == null)
            return;

        if (action.TargetPart == null)
            return;

        if (action.Owner.BattleContext == null)
            return;

        if (action.Owner.BattleContext.EffectResolver == null)
            return;

        BattleEffectResolver resolver =
            action.Owner.BattleContext.EffectResolver;

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Burn(1)));

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Bleeding(1)));

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 도사림 효과 : " +
            $"{action.Target.Data.CharacterName} {action.TargetPart.Type}에 화상 1, 출혈 1 부여");
    }
}