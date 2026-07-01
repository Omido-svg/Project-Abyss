using UnityEngine;

public class ElitePrestigeSkill : PrestigeSkill
{
    public ElitePrestigeSkill()
    {
        SkillName = "처형자의 일격(위세)";

        BasePower = 14;

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

        bool wasDead =
            action.Target.IsDead;

        int beforePartHP =
            Mathf.RoundToInt(
                action.TargetPart.PartHP);

        //--------------------------------
        // 위세 직접 피해
        //--------------------------------

        int damage =
            GetRolledPower(action);

        if (damage > 0)
        {
            action.Target.TakeDamage(
                action.TargetPart,
                damage,
                true);

            Debug.Log(
                $"{action.Owner.Data.CharacterName} 위세 피해 : {damage}");
        }

        //--------------------------------
        // 추가 효과
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(3),
            action.Owner);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 위세 효과 : 출혈 3 부여");

        //--------------------------------
        // 약화된 부위라면 파괴
        //--------------------------------

        if (action.TargetPart.IsWeakened &&
            !action.TargetPart.IsBroken)
        {
            action.Target.ForceBreakPart(
                action.TargetPart);

            Debug.Log(
                $"{action.Target.Data.CharacterName} {action.TargetPart.Type} 약화 부위 파괴");
        }

        //--------------------------------
        // 로그용 데미지 기록
        //--------------------------------

        int afterPartHP =
            Mathf.RoundToInt(
                action.TargetPart.PartHP);

        action.SetDamageLog(
            damage,
            beforePartHP,
            afterPartHP);

        //--------------------------------
        // 처치 이벤트
        //--------------------------------

        if (!wasDead && action.Target.IsDead)
        {
            battleEvent?.RaiseKill(
                action.Owner,
                action.Target);
        }
    }

    private int GetRolledPower(
        BattleAction action)
    {
        if (action == null)
            return 0;

        if (!action.HasRolled)
        {
            int power =
                action.RollPower();

            action.RolledPower = power;
            action.finalPower = power;
            action.HasRolled = true;

            return power;
        }

        if (action.finalPower > 0)
            return action.finalPower;

        return action.RolledPower;
    }
}