using UnityEngine;

public class EnemyNormalAttack : NormalSkill
{
    public EnemyNormalAttack()
    {
        SkillName = "물어뜯기(일반공격)";

        BasePower = 5;

        Resolver = new DiceResolver(1, 2);
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
            $"{action.Owner.Data.CharacterName} 일반공격 효과 : " +
            $"{action.Target.Data.CharacterName} {action.TargetPart.Type}에 출혈 1 부여");
    }
}