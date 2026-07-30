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
        Apply(context, null);
    }

    public override void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        int amount = overrides?.ResolveAmount(Amount) ?? Amount;
        bool giveToSelectedTarget =
            overrides?.ResolveGiveToSelectedTarget(GiveToSelectedTarget) ??
            GiveToSelectedTarget;
        if (context?.Resolver == null ||
            context.Owner == null ||
            amount <= 0)
        {
            return;
        }

        Character target =
            giveToSelectedTarget
                ? context.Target
                : context.Owner;

        if (target == null)
            return;

        context.Resolver.AddPrestige(
            EffectRequest.Prestige(
                context.Owner,
                target,
                amount));
    }
}