using UnityEngine;

public class OlafPreparationSkill : PreparationSkill
{
    public OlafPreparationSkill()
    {
        SkillName = "광기의 난도질(도사림)";

        BasePower = 12;

        Resolver = new CoinResolver(2);
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
        // 위력 굴림
        //--------------------------------

        int power = action.RollPower();

        action.RolledPower = power;
        action.finalPower = power;
        action.HasRolled = true;

        //--------------------------------
        // 대상 피해 기록
        //--------------------------------

        int beforeHP =
            Mathf.RoundToInt(action.TargetPart.PartHP);

        action.Target.TakeDamage(
            action.TargetPart,
            power,
            false);

        int afterHP =
            Mathf.RoundToInt(action.TargetPart.PartHP);

        action.SetDamageLog(
            power,
            beforeHP,
            afterHP);

        //--------------------------------
        // 추가 효과
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(2),
            action.Owner);

        //--------------------------------
        // 자신의 랜덤 부위 피해
        //--------------------------------

        BodyPart randomPart =
            action.Owner.GetRandomUsablePart();

        if (randomPart == null)
            return;

        action.Owner.TakeDamage(
            randomPart,
            5,
            false);
    }
}