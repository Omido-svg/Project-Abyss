using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Normal Bleed",
    fileName = "OlafNormalBleedEffect")]
public class OlafNormalBleedEffect : SkillEffectDefinition
{
    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner == null ||
            context.Target == null ||
            context.Resolver == null)
        {
            return;
        }

        int bleedAmount = 1;

        OlafMadnessMechanic madness =
            context.Owner.GetMechanic<OlafMadnessMechanic>();

        if (madness != null)
        {
            bleedAmount =
                madness.GetNormalAttackBleedAmount();
        }

        Bleeding bleeding =
            new Bleeding(bleedAmount);

        if (context.TargetPart != null)
        {
            context.Resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
                    bleeding));
        }
        else
        {
            context.Resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(
                    context.Owner,
                    context.Target,
                    bleeding));
        }

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 일반공격 효과 : " +
            $"출혈 {bleedAmount} 부여");
    }
}
