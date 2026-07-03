using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Rolled Part Damage",
    fileName = "ApplyRolledPartDamageEffect")]
public class ApplyRolledPartDamageEffect : SkillEffectDefinition
{
    [Header("Damage")]
    [SerializeField]
    private bool useSkillCanBreakPart = true;

    [SerializeField]
    private bool canBreakPart = false;

    [Header("Log")]
    [SerializeField]
    private bool writeDamageLog = true;

    [SerializeField]
    private bool raiseKillEvent = true;

    public override void Apply(SkillEffectContext context)
    {
        if (context == null)
            return;

        if (context.Action == null)
            return;

        if (context.Owner == null)
            return;

        if (context.Target == null)
            return;

        if (context.TargetPart == null)
            return;

        if (context.Resolver == null)
            return;

        BattleAction action =
            context.Action;

        bool wasDead =
            context.Target.IsDead;

        int beforePartHP =
            Mathf.RoundToInt(
                context.TargetPart.PartHP);

        bool finalCanBreakPart =
            GetCanBreakPart(context);

        int damage =
            GetRolledPower(action);

        if (damage <= 0)
            return;

        context.Resolver.ApplyPartDamage(
            EffectRequest.PartDamage(
                context.Owner,
                action,
                damage,
                finalCanBreakPart));

        int afterPartHP =
            Mathf.RoundToInt(
                context.TargetPart.PartHP);

        if (writeDamageLog)
        {
            action.SetDamageLog(
                damage,
                beforePartHP,
                afterPartHP);
        }

        if (raiseKillEvent &&
            !wasDead &&
            context.Target.IsDead)
        {
            context.BattleContext?._battleEvent?.RaiseKill(
                context.Owner,
                context.Target);
        }

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 스킬 피해 : " +
            $"{context.Target.Data.CharacterName} {context.TargetPart.Type}에 {damage}");
    }

    private bool GetCanBreakPart(
        SkillEffectContext context)
    {
        if (!useSkillCanBreakPart)
            return canBreakPart;

        if (context.SkillDefinition == null)
            return canBreakPart;

        return context.SkillDefinition.CanBreakPart;
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