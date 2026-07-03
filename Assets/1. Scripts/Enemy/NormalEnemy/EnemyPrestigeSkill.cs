using UnityEngine;

public class EnemyPrestigeSkill : PrestigeSkill
{
    public EnemyPrestigeSkill()
    {
        SkillName = "광폭한 포식(위세)";

        BasePower = 10;

        Resolver = new DiceResolver(2, 8);
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

        int damage =
            GetRolledPower(action);

        if (damage > 0)
        {
            resolver.ApplyPartDamage(
                EffectRequest.PartDamage(
                    action.Owner,
                    action,
                    damage,
                    true));

            Debug.Log(
                $"{action.Owner.Data.CharacterName} 위세 피해 : {damage}");
        }

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Bleeding(2)));

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Burn(1)));

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 위세 효과 : 출혈 2, 화상 1 부여");

        if (action.TargetPart.IsWeakened &&
            !action.TargetPart.IsBroken)
        {
            resolver.BreakWeakenedPart(
                EffectRequest.ForceBreak(
                    action.Owner,
                    action.Target,
                    action.TargetPart));

            Debug.Log(
                $"{action.Target.Data.CharacterName} {action.TargetPart.Type} 약화 부위 파괴");
        }

        int afterPartHP =
            Mathf.RoundToInt(
                action.TargetPart.PartHP);

        action.SetDamageLog(
            damage,
            beforePartHP,
            afterPartHP);

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