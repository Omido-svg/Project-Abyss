using UnityEngine;

public class OlafPrestigeSkill : PrestigeSkill
{
    private const int BleedExplosionDamagePerStack = 5;
    public override bool CanBreakPart => true;

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

        BattleEffectResolver resolver =
            action.Owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        bool wasDead =
            action.Target.IsDead;

        int beforePartHP =
            Mathf.RoundToInt(
                action.TargetPart.PartHP);

        int totalDamage = 0;

        OlafMadnessMechanic madness =
            action.Owner.GetMechanic<OlafMadnessMechanic>();

        madness?.BeginSuppressPartBreakMadness();

        try
        {
            int damage =
                GetRolledPower(action);

            if (damage > 0)
            {
                resolver.ApplyPartDamage(
                    EffectRequest.PartDamage(
                        action.Owner,
                        action,
                        damage,
                        CanBreakPart));

                totalDamage += damage;

                Debug.Log(
                    $"{action.Owner.Data.CharacterName} 위세 기본 피해 : {damage}");
            }

            Bleeding bleeding =
                action.Target.GetPartStatus<Bleeding>(
                    action.TargetPart);

            if (bleeding != null)
            {
                int explosionDamage =
                    bleeding.Stack * BleedExplosionDamagePerStack;

                if (explosionDamage > 0)
                {
                    resolver.ApplyTrueDamage(
                        EffectRequest.TrueDamage(
                            action.Owner,
                            action.Target,
                            explosionDamage,
                            bleeding));

                    totalDamage += explosionDamage;

                    Debug.Log(
                        $"{action.Target.Data.CharacterName} 출혈 폭발 피해 : {explosionDamage}");
                }

                resolver.RemoveBodyPartStatus(
                    EffectRequest.RemoveBodyPartStatus(
                        action.Owner,
                        action.Target,
                        action.TargetPart,
                        bleeding));
            }

            if (madness != null)
            {
                int madnessDamage =
                    madness.ConsumeMadnessForPrestigeDamage();

                if (madnessDamage > 0)
                {
                    resolver.ApplyTrueDamage(
                        EffectRequest.TrueDamage(
                            action.Owner,
                            action.Target,
                            madnessDamage,
                            null));

                    totalDamage += madnessDamage;

                    Debug.Log(
                        $"{action.Owner.Data.CharacterName} 광기 추가 피해 : {madnessDamage}");
                }
            }

            int afterDamagePartHP =
                Mathf.RoundToInt(
                    action.TargetPart.PartHP);

            action.SetDamageLog(
                totalDamage,
                beforePartHP,
                afterDamagePartHP);

            if (!action.TargetPart.IsBroken)
            {
                resolver.ForceBreakPart(
                    EffectRequest.ForceBreak(
                        action.Owner,
                        action.Target,
                        action.TargetPart));

                Debug.Log(
                    $"{action.Target.Data.CharacterName} {action.TargetPart.Type} 부위 강제 파괴");
            }

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