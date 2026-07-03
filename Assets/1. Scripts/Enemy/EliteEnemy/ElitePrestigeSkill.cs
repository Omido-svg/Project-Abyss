using UnityEngine;

public class ElitePrestigeSkill : PrestigeSkill
{
    public override bool CanBreakPart => true;

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

        BattleEffectResolver resolver =
            action.Owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        bool wasDead =
            action.Target.IsDead;

        int beforePartHP =
            Mathf.RoundToInt(
                action.TargetPart.PartHP);

        bool wasWeakenedBeforeDamage =
            action.TargetPart.IsWeakened;

        bool wasBrokenBeforeDamage =
            action.TargetPart.IsBroken;

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

            Debug.Log(
                $"{action.Owner.Data.CharacterName} 위세 피해 : " +
                $"{action.Target.Data.CharacterName} {action.TargetPart.Type}에 {damage}");
        }

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                action.Owner,
                action.Target,
                action.TargetPart,
                new Bleeding(3)));

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 위세 효과 : " +
            $"{action.Target.Data.CharacterName} {action.TargetPart.Type}에 출혈 3 부여");

        if (wasWeakenedBeforeDamage &&
            !wasBrokenBeforeDamage &&
            action.TargetPart.IsBroken)
        {
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