using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Normal Bleed",
    fileName = "OlafNormalBleedEffect")]
public class OlafNormalBleedEffect : SkillEffectDefinition
{
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

        int bleedAmount = 1;

        OlafMadnessMechanic madness =
            context.Owner.GetMechanic<OlafMadnessMechanic>();

        if (madness != null)
        {
            bleedAmount =
                madness.GetNormalAttackBleedAmount();
        }

        context.Resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                context.Owner,
                context.Target,
                context.TargetPart,
                new Bleeding(bleedAmount)));

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 일반공격 효과 : 출혈 {bleedAmount} 부여");
    }
}