using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Conditional Damage",
    fileName = "ConditionalDamageEffect")]
public class ConditionalDamageEffect : SkillEffectDefinition
{
    [Header("Base Value")]
    [SerializeField] private bool useRolledPower;
    [SerializeField] private bool useResolvedDamage;
    [SerializeField] private int flatDamage = 1;
    [SerializeField] private float multiplier = 1f;

    [Header("Damage Contract")]
    [SerializeField] private DamageType damageType =
        DamageType.SkillPart;
    [SerializeField] private bool canBreakPart;
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

        int baseValue = flatDamage;

        if (useResolvedDamage &&
            context.DamageContext != null)
        {
            baseValue +=
                context.DamageContext.GetDisplayDamage();
        }

        if (useRolledPower)
        {
            baseValue +=
                context.Action.HasRolled
                    ? context.Action.RolledPower
                    : context.Action.RollPower();
        }

        int damage = Mathf.Max(
            0,
            Mathf.RoundToInt(
                baseValue * multiplier));

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
                canBreakPart,
                applyMomentum,
                applyDefense,
                applyGuard,
                applyProtection,
                context.Action);

        context.ApplyDamage(request);
    }
}
