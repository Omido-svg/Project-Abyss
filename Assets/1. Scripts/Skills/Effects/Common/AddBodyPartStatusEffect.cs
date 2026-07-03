using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Add Body Part Status",
    fileName = "AddBodyPartStatusEffect")]
public class AddBodyPartStatusEffect : SkillEffectDefinition
{
    public StatusEffectId StatusEffectId;
    public int Stack = 1;

    public override void Apply(SkillEffectContext context)
    {
        if (context == null)
            return;

        if (context.Resolver == null)
            return;

        if (context.Owner == null)
            return;

        if (context.Target == null)
            return;

        if (context.TargetPart == null)
            return;

        StatusEffect effect =
            StatusEffectFactory.Create(
                StatusEffectId,
                Stack);

        if (effect == null)
            return;

        context.Resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                context.Owner,
                context.Target,
                context.TargetPart,
                effect));
    }
}