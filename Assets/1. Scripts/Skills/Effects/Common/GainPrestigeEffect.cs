using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Gain Prestige",
    fileName = "GainPrestigeEffect")]
public class GainPrestigeEffect : SkillEffectDefinition
{
    [Min(0)] public int Amount = 10;
    public bool GiveToSelectedTarget;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Resolver == null ||
            context.Owner == null ||
            Amount <= 0)
        {
            return;
        }

        Character target =
            GiveToSelectedTarget
                ? context.Target
                : context.Owner;

        if (target == null)
            return;

        context.Resolver.AddPrestige(
            EffectRequest.Prestige(
                context.Owner,
                target,
                Amount));
    }
}
