using UnityEngine;

public class OlafPrestigeSkill : PrestigeSkill
{
    private const int BleedExplosionDamagePerStack = 5;

    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란(위세)";

        BasePower = 20;

        Resolver = new CoinResolver(5);
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

        int totalDamage = 0;

        OlafMadnessMechanic madness =
            action.Owner.GetMechanic<OlafMadnessMechanic>();

        //--------------------------------
        // 위세 처리 중 발생하는 부위 파괴는
        // 광기 증가에서 제외
        //--------------------------------

        madness?.BeginSuppressPartBreakMadness();

        try
        {
            //--------------------------------
            // 위세 기본 피해
            //--------------------------------

            int damage =
                GetRolledPower(action);

            if (damage > 0)
            {
                action.Target.TakeDamage(
                    action.TargetPart,
                    damage,
                    true);

                totalDamage += damage;

                Debug.Log(
                    $"{action.Owner.Data.CharacterName} 위세 기본 피해 : {damage}");
            }

            //--------------------------------
            // 출혈 폭발
            //--------------------------------

            Bleeding bleeding =
                action.Target.GetStatus<Bleeding>();

            if (bleeding != null)
            {
                int explosionDamage =
                    bleeding.Stack * BleedExplosionDamagePerStack;

                if (explosionDamage > 0)
                {
                    action.Target.TakeTrueDamage(
                        explosionDamage,
                        bleeding);

                    totalDamage += explosionDamage;

                    Debug.Log(
                        $"{action.Target.Data.CharacterName} 출혈 폭발 피해 : {explosionDamage}");
                }

                action.Target.RemoveStatus(
                    bleeding);
            }

            //--------------------------------
            // 광기 추가 피해
            //--------------------------------

            if (madness != null)
            {
                int madnessDamage =
                    madness.ConsumeMadnessForPrestigeDamage();

                if (madnessDamage > 0)
                {
                    action.Target.TakeTrueDamage(
                        madnessDamage,
                        null);

                    totalDamage += madnessDamage;

                    Debug.Log(
                        $"{action.Owner.Data.CharacterName} 광기 추가 피해 : {madnessDamage}");
                }
            }

            //--------------------------------
            // 여기서 먼저 로그용 HP를 기록한다.
            // 강제 파괴 후 HP 0이 아니라,
            // 실제 피해 직후의 부위 HP를 기록하기 위함.
            //--------------------------------

            int afterDamagePartHP =
                Mathf.RoundToInt(
                    action.TargetPart.PartHP);

            action.SetDamageLog(
                totalDamage,
                beforePartHP,
                afterDamagePartHP);

            //--------------------------------
            // 위세 효과: 대상 부위 강제 파괴
            //--------------------------------

            if (!action.TargetPart.IsBroken)
            {
                action.Target.ForceBreakPart(
                    action.TargetPart);

                Debug.Log(
                    $"{action.Target.Data.CharacterName} {action.TargetPart.Type} 부위 강제 파괴");
            }

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
        finally
        {
            madness?.EndSuppressPartBreakMadness();
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