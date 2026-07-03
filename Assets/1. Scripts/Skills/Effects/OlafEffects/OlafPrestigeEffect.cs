using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Prestige Effect",
    fileName = "OlafPrestigeEffect")]
public class OlafPrestigeEffect : SkillEffectDefinition
{
    [Header("Bleeding Explosion")]
    [SerializeField]
    private bool consumeBleeding = true;

    [SerializeField]
    private int damagePerBleedingStack = 1;

    [Header("Madness Bonus")]
    [SerializeField]
    private bool consumeMadness = true;

    [SerializeField]
    private int damagePerMadness = 2;

    [Header("Break")]
    [SerializeField]
    private bool forceBreakTargetPart = true;

    public override void Apply(SkillEffectContext context)
    {
        if (context == null)
            return;

        if (context.Owner == null)
            return;

        if (context.Target == null)
            return;

        if (context.TargetPart == null)
            return;

        if (context.Resolver == null)
            return;

        BodyPart targetPart =
            context.TargetPart;

        Character owner =
            context.Owner;

        Character target =
            context.Target;

        OlafMadnessMechanic madness =
            owner.GetMechanic<OlafMadnessMechanic>();

        int bleedingDamage =
            CalculateBleedingExplosionDamage(
                context,
                target,
                targetPart);

        int madnessDamage =
            CalculateMadnessBonusDamage(
                madness);

        int totalExtraDamage =
            bleedingDamage + madnessDamage;

        if (totalExtraDamage > 0)
        {
            context.Resolver.ApplyPartDamage(
                EffectRequest.PartDamage(
                    owner,
                    context.Action,
                    totalExtraDamage,
                    true));
        }

        if (forceBreakTargetPart)
        {
            context.Resolver.ForceBreakPart(
                EffectRequest.ForceBreak(
                    owner,
                    target,
                    targetPart));
        }

        Debug.Log(
            $"{owner.Data.CharacterName} 위세 효과 : " +
            $"{target.Data.CharacterName} {targetPart.Type}에 " +
            $"추가 피해 {totalExtraDamage}, 강제 파괴 {forceBreakTargetPart}");
    }

    private int CalculateBleedingExplosionDamage(
        SkillEffectContext context,
        Character target,
        BodyPart targetPart)
    {
        if (target == null)
            return 0;

        if (targetPart == null)
            return 0;

        Bleeding bleeding =
            target.GetPartStatus<Bleeding>(
                targetPart);

        if (bleeding == null)
            return 0;

        int stack =
            Mathf.Max(0, bleeding.Stack);

        if (consumeBleeding)
        {
            context.Resolver.RemoveBodyPartStatus(
                EffectRequest.RemoveBodyPartStatus(
                    context.Owner,
                    target,
                    targetPart,
                    bleeding));
        }

        return stack * damagePerBleedingStack;
    }

    private int CalculateMadnessBonusDamage(
        OlafMadnessMechanic madness)
    {
        if (madness == null)
            return 0;

        int currentMadness =
            madness.CurrentMadness;

        int damage =
            currentMadness * damagePerMadness;

        if (consumeMadness)
        {
            madness.ClearMadness();
        }

        return damage;
    }
}