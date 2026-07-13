using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Add Status",
    fileName = "AddStatusEffect")]
public class AddBodyPartStatusEffect : SkillEffectDefinition
{
    public StatusEffectId StatusEffectId;
    [Min(1)] public int Stack = 1;
    [Min(1)] public int Duration = 3;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Resolver == null ||
            context.Owner == null ||
            context.Target == null)
        {
            return;
        }

        StatusEffect effect =
            StatusEffectFactory.Create(
                StatusEffectId,
                Stack,
                Duration);

        if (effect == null)
            return;

        if (context.TargetPart != null)
        {
            context.Resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
                    effect));
            return;
        }

        context.Resolver.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(
                context.Owner,
                context.Target,
                effect));
    }
}
