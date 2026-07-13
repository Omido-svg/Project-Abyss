using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Rolled Damage",
    fileName = "ApplyRolledDamageEffect")]
public class ApplyRolledPartDamageEffect : SkillEffectDefinition
{
    [Header("Damage")]
    [SerializeField] private float powerMultiplier = 1f;
    [SerializeField] private int flatBonus;
    [SerializeField] private DamageType damageType =
        DamageType.SkillPart;

    [SerializeField]
    private bool useSkillCanBreakPart = true;

    [SerializeField]
    private bool canBreakPart;

    [Header("Pipeline")]
    [SerializeField] private bool applyMomentum;
    [SerializeField] private bool applyDefense = true;
    [SerializeField] private bool applyGuard = true;
    [SerializeField] private bool applyProtection = true;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Action == null ||
            context.Owner == null ||
            context.Target == null)
        {
            return;
        }

        int rolledPower =
            GetRolledPower(context.Action);

        int damage = Mathf.Max(
            0,
            Mathf.RoundToInt(
                rolledPower * powerMultiplier) +
            flatBonus);

        if (damage <= 0)
            return;

        DamageType finalType =
            context.TargetPart == null &&
            damageType == DamageType.SkillPart
                ? DamageType.Direct
                : damageType;

        DamageRequest request =
            DamageRequest.Custom(
                finalType,
                context.Owner,
                context.Target,
                context.TargetPart,
                damage,
                1f,
                useSkillCanBreakPart
                    ? context.SkillDefinition?.CanBreakPart == true
                    : canBreakPart,
                applyMomentum,
                applyDefense,
                applyGuard,
                applyProtection,
                context.Action);

        context.ApplyDamage(request);
    }

    private int GetRolledPower(
        BattleAction action)
    {
        if (action == null)
            return 0;

        if (!action.HasRolled)
        {
            int power = action.RollPower();
            action.RolledPower = power;
            action.finalPower = power;
            action.HasRolled = true;
            return power;
        }

        return action.finalPower > 0
            ? action.finalPower
            : action.RolledPower;
    }
}
