using UnityEngine;
using UnityEngine.Serialization;

public enum OlafPrestigeBreakPolicy
{
    // 기존 SO에 새 필드가 추가되어도 안전한 기본값이 되도록 0을 유지한다.
    WeakenedOnly = 0,
    WeakenedOrBleedingThreshold = 1,
    Always = 2,
    Never = 3
}

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Prestige Effect",
    fileName = "OlafPrestigeEffect")]
public class OlafPrestigeEffect : SkillEffectDefinition
{
    [Header("Bleeding Explosion")]
    [SerializeField]
    private bool consumeBleeding = true;

    [SerializeField, Min(0)]
    private int damagePerBleedingStack = 1;

    [Header("Madness Bonus")]
    [SerializeField]
    private bool consumeMadness = true;

    [SerializeField, Min(0)]
    private int damagePerMadness = 2;

    [Header("Break")]
    [FormerlySerializedAs("forceBreakTargetPart")]
    [SerializeField]
    private bool enablePartBreak = true;

    [SerializeField]
    private OlafPrestigeBreakPolicy breakPolicy =
        OlafPrestigeBreakPolicy.WeakenedOnly;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner is not Olaf olaf ||
            context.Target == null ||
            context.Resolver == null)
        {
            return;
        }

        Character target =
            context.Target;

        BodyPart targetPart =
            context.TargetPart;

        OlafMadnessMechanic madness =
            olaf.MadnessMechanic;

        int consumedBleedingStacks =
            ConsumeBleedingIfNeeded(
                context,
                target,
                targetPart);

        int bleedingDamage =
            consumedBleedingStacks *
            Mathf.Max(
                0,
                damagePerBleedingStack);

        int madnessDamage =
            madness?.ConsumeMadnessForPrestigeDamage(
                context.Action,
                damagePerMadness,
                consumeMadness) ?? 0;

        int totalExtraDamage =
            bleedingDamage +
            madnessDamage;

        DamageContext damageContext = null;

        if (totalExtraDamage > 0)
        {
            DamageType damageType =
                targetPart == null
                    ? DamageType.Direct
                    : DamageType.BleedExplosion;

            DamageRequest request =
                DamageRequest.Custom(
                    damageType,
                    olaf,
                    target,
                    targetPart,
                    totalExtraDamage,
                    1f,
                    false,
                    false,
                    false,
                    false,
                    true,
                    context.Action);

            damageContext =
                context.ApplyDamage(
                    request);
        }

        bool breakApplied =
            TryBreakTargetPart(
                context,
                target,
                targetPart,
                consumedBleedingStacks);

        string targetPoint =
            targetPart == null
                ? "SINGLE_HP"
                : targetPart.Type.ToString();

        Debug.Log(
            $"{olaf.Data.CharacterName} 위세 효과 : " +
            $"{target.Data.CharacterName} {targetPoint} / " +
            $"출혈 피해={bleedingDamage}, " +
            $"광기 피해={madnessDamage}, " +
            $"실제 피해={damageContext?.GetDisplayDamage() ?? totalExtraDamage}, " +
            $"부위 파괴={breakApplied}, " +
            $"정책={breakPolicy}");
    }

    private int ConsumeBleedingIfNeeded(
        SkillEffectContext context,
        Character target,
        BodyPart targetPart)
    {
        Bleeding bleeding =
            targetPart != null
                ? target.GetPartStatus<Bleeding>(
                    targetPart)
                : target.GetStatus<Bleeding>();

        if (bleeding == null)
            return 0;

        int stack =
            Mathf.Max(
                0,
                bleeding.Stack);

        if (!consumeBleeding)
            return stack;

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

        return stack;
    }

    private bool TryBreakTargetPart(
        SkillEffectContext context,
        Character target,
        BodyPart targetPart,
        int bleedingStacks)
    {
        if (!enablePartBreak ||
            target == null ||
            target.IsDead ||
            targetPart == null ||
            targetPart.IsBroken)
        {
            return false;
        }

        bool conditionMet =
            breakPolicy switch
            {
                OlafPrestigeBreakPolicy.WeakenedOnly =>
                    targetPart.IsWeakened,

                OlafPrestigeBreakPolicy
                    .WeakenedOrBleedingThreshold =>
                    targetPart.IsWeakened ||
                    bleedingStacks >=
                    Bleeding.ExplosionThreshold,

                OlafPrestigeBreakPolicy.Always =>
                    true,

                OlafPrestigeBreakPolicy.Never =>
                    false,

                _ => false
            };

        if (!conditionMet)
            return false;

        EffectRequest request =
            EffectRequest.ForceBreak(
                context.Owner,
                target,
                targetPart);

        request.SourceAction =
            context.Action;

        return context.Resolver.ForceBreakPart(
            request);
    }
}
