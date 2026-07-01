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

        //--------------------------------
        // 결투 기본 피해는 DamageManager에서 처리
        // 여기서는 추가 효과만 처리
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 결투 효과 : 출혈 1 부여");
    }
}