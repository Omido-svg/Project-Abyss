using UnityEngine;

public class OlafChainDuelSkill : DuelSkill
{
    public OlafChainDuelSkill()
    {
        SkillName = "사슬 결투(결투)";

        BasePower = 6;

        Resolver = new CoinResolver(3);
    }

    public override int GetMomentumPushBonus(
        BattleAction action)
    {
        if (action == null || action.Owner == null)
            return 0;

        OlafMadnessMechanic madness =
            action.Owner.GetMechanic<OlafMadnessMechanic>();

        int bonus = 5;

        if (madness != null)
        {
            bonus += madness.GetDuelPushBonus();
        }

        return bonus;
    }

    public override void Execute(
        BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        if (action.Target == null)
            return;

        //--------------------------------
        // 기본 피해는 DamageManager가 처리
        // 여기서는 추가 효과만 처리
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 사슬 결투 효과 : 출혈 1 추가");
    }
}