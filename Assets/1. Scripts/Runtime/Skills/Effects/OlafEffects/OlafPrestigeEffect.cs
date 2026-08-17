using UnityEngine;

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

    // 부위 파괴는 이 효과에서 처리하지 않는다.
    // 확정된 고유 파괴 루트:
    // 혈상 5스택 + 결투 대 결투에서 올라프가 최종 승리했을 때만 별도 전투 규칙이 처리한다.
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
                    context.Action);

            damageContext =
                context.ApplyDamage(
                    request);
        }

        string targetPoint =
            targetPart == null
                ? "SINGLE_HP"
                : targetPart.Type.ToString();

        Debug.Log(
            $"{olaf.Data.CharacterName} 위세 효과 : " +
            $"{target.Data.CharacterName} {targetPoint} / " +
            $"혈상 피해={bleedingDamage}, " +
            $"광기 피해={madnessDamage}, " +
            $"실제 피해={damageContext?.GetDisplayDamage() ?? totalExtraDamage}, " +
            "부위 파괴=위세 효과에서 미수행");
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

}
