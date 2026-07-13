using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Prestige Effect",
    fileName = "OlafPrestigeEffect")]
public class OlafPrestigeEffect : SkillEffectDefinition
{
    [Header("Bleeding Explosion")]
    [SerializeField] private bool consumeBleeding = true;
    [SerializeField] private int damagePerBleedingStack = 1;

    [Header("Madness Bonus")]
    [SerializeField] private bool consumeMadness = true;
    [SerializeField] private int damagePerMadness = 2;

    [Header("Break")]
    [SerializeField] private bool forceBreakTargetPart = true;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner == null ||
            context.Target == null ||
            context.Resolver == null)
        {
            return;
        }

        Character owner = context.Owner;
        Character target = context.Target;
        BodyPart targetPart = context.TargetPart;

        OlafMadnessMechanic madness =
            owner.GetMechanic<OlafMadnessMechanic>();

        int bleedingDamage =
            CalculateBleedingExplosionDamage(
                context,
                target,
                targetPart);

        int madnessDamage =
            CalculateMadnessBonusDamage(madness);

        int totalExtraDamage =
            bleedingDamage + madnessDamage;

        if (totalExtraDamage > 0)
        {
            DamageType damageType =
                targetPart == null
                    ? DamageType.Direct
                    : DamageType.BleedExplosion;

            DamageRequest request =
                DamageRequest.Custom(
                    damageType,
                    owner,
                    target,
                    targetPart,
                    totalExtraDamage,
                    1f,
                    true,
                    false,
                    false,
                    false,
                    true,
                    context.Action);

            context.ApplyDamage(request);
        }

        if (forceBreakTargetPart &&
            targetPart != null &&
            !targetPart.IsBroken)
        {
            EffectRequest request =
                EffectRequest.ForceBreak(
                    owner,
                    target,
                    targetPart);

            request.SourceAction = context.Action;
            context.Resolver.ForceBreakPart(request);
        }

        Debug.Log(
            $"{owner.Data.CharacterName} 위세 효과 : " +
            $"{target.Data.CharacterName} " +
            $"{(targetPart == null ? "SINGLE_HP" : targetPart.Type.ToString())}에 " +
            $"추가 피해 {totalExtraDamage}, " +
            $"강제 파괴 {forceBreakTargetPart && targetPart != null}");
    }

    private int CalculateBleedingExplosionDamage(
        SkillEffectContext context,
        Character target,
        BodyPart targetPart)
    {
        Bleeding bleeding =
            targetPart != null
                ? target.GetPartStatus<Bleeding>(targetPart)
                : target.GetStatus<Bleeding>();

        if (bleeding == null)
            return 0;

        int stack = Mathf.Max(0, bleeding.Stack);

        if (consumeBleeding)
        {
            if (targetPart != null)
            {
                context.Resolver.RemoveBodyPartStatus(
                    EffectRequest.RemoveBodyPartStatus(
                        context.Owner,
                        target,
                        targetPart,
                        bleeding));
            }
            else
            {
                target.RemoveStatus(
                    bleeding,
                    StatusEffectRemoveReason.Manual);
            }
        }

        return stack * damagePerBleedingStack;
    }

    private int CalculateMadnessBonusDamage(
        OlafMadnessMechanic madness)
    {
        if (madness == null)
            return 0;

        int currentMadness = madness.CurrentMadness;
        int damage = currentMadness * damagePerMadness;

        if (consumeMadness)
            madness.ClearMadness();

        return damage;
    }
}
