using UnityEngine;

public class OlafNormalAttack : NormalSkill
{
    public OlafNormalAttack()
    {
        SkillName = "피의 일격(일반공격)";

        BasePower = 5;

        Resolver = new CoinResolver(1);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        if (action.Target == null)
            return;

        int bleedAmount = 1;

        OlafMadnessMechanic madness =
            action.Owner.GetMechanic<OlafMadnessMechanic>();

        if (madness != null)
        {
            bleedAmount =
                madness.GetNormalAttackBleedAmount();
        }

        action.Target.AddStatus(
            new Bleeding(bleedAmount),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 평타 효과 : 출혈 {bleedAmount} 부여");
    }
}