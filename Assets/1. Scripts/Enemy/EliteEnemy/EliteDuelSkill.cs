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

        //--------------------------------
        // 결투 피해는 DamageManager에서 처리
        // 여기서는 추가 효과만 처리
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(2),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 결투 효과 : 출혈 2 부여");
    }
}