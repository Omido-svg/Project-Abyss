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
    [SerializeField] private bool applyGuard = true;

    public override void Apply(
        SkillEffectContext context)
    {
        Apply(context, null);
    }

    public override void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        if (context?.Action == null ||
            context.Owner == null ||
            context.Target == null)
        {
            return;
        }

        int rolledPower =
            GetRolledPower(context.Action);

        float finalMultiplier =
            overrides?.ResolveMultiplier(powerMultiplier) ?? powerMultiplier;
        int finalFlatBonus =
            overrides?.ResolveFlatValue(flatBonus) ?? flatBonus;

        int damage = Mathf.Max(
            0,
            Mathf.RoundToInt(
                rolledPower * finalMultiplier) +
            finalFlatBonus);

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
                applyGuard,
                context.Action);

        context.ApplyDamage(request);
    }

    /// <summary>
    /// R7: immutable request builder for the native R5 Damage Command.
    /// Does not mutate HP, stagger or parts. The old Apply() remains intact
    /// until the two mixed cards complete deterministic Golden parity.
    /// </summary>
    public DamageRequest? CreateNativeDamageRequest(
        SkillEffectContext context,SkillEffectOverrides overrides)
    {
        if(context?.Action==null||context.Owner==null||context.Target==null)
            return null;
        int rolledPower=GetRolledPower(context.Action);
        float multiplier=overrides?.ResolveMultiplier(powerMultiplier)??powerMultiplier;
        int bonus=overrides?.ResolveFlatValue(flatBonus)??flatBonus;
        int value=Mathf.Max(0,Mathf.RoundToInt(rolledPower*multiplier)+bonus);
        if(value<=0)return null;
        DamageType type=context.TargetPart==null&&damageType==DamageType.SkillPart
            ?DamageType.Direct:damageType;
        return DamageRequest.Custom(type,context.Owner,context.Target,
            context.TargetPart,value,1f,
            useSkillCanBreakPart?context.SkillDefinition?.CanBreakPart==true:canBreakPart,
            applyMomentum,applyGuard,context.Action);
    }

    private int GetRolledPower(
        BattleAction action)
    {
        if (action == null)
            return 0;

        if (!action.HasRolled)
            action.RollPower();

        // DMG-01: 굴림 자체 보정(힘/쇠약/공포 등)을 포함한 피해 기준 위력을 쓴다.
        return action.GetDamagePower();
    }
}
