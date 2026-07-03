using UnityEngine;

public class EliteDuelSkill : DuelSkill
{
    public EliteDuelSkill()
    {
        SkillName = "정예병의 압박(결투)";

        BasePower = 7;

        Resolver = new DiceResolver(2, 4);
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

        BattleEffectResolver resolver =
            action.Owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Bleeding(2)));

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 결투 효과 : 출혈 2 부여");
    }
}