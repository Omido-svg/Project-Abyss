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

        //--------------------------------
        // 도사림은 선행 행동
        // 직접 피해 없이 대상 부위에 상태이상만 부여
        //--------------------------------

        action.Target.AddPartStatus(
            action.TargetPart,
            new Burn(1),
            action.Owner);

        action.Target.AddPartStatus(
            action.TargetPart,
            new Bleeding(1),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 도사림 효과 : " +
            $"{action.Target.Data.CharacterName} {action.TargetPart.Type}에 화상 1, 출혈 1 부여");
    }
}