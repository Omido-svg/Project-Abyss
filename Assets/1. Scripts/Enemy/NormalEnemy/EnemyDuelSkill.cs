using UnityEngine;

public class EnemyDuelSkill : DuelSkill
{
    public EnemyDuelSkill()
    {
        SkillName = "난폭한 공격(결투)";

        BasePower = 2;

        Resolver = new DiceResolver(2, 6);
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
                new Bleeding(1)));

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 결투 효과 : 출혈 1 부여");
    }
}