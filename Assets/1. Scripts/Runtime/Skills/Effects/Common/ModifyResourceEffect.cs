using UnityEngine;

public enum SkillResourceType
{
    Prestige,
    Custom
}

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Modify Resource",
    fileName = "ModifyResourceEffect")]
public class ModifyResourceEffect : SkillEffectDefinition
{
    public SkillResourceType ResourceType;
    public string CustomResourceKey;
    public int Amount = 1;
    public int Minimum;
    public int Maximum = 999;

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
        int minimum = overrides?.ResolveMinimum(Minimum) ?? Minimum;
        int maximum = overrides?.ResolveMaximum(Maximum) ?? Maximum;
        string customResourceKey = overrides?.ResolveResourceKey(CustomResourceKey) ?? CustomResourceKey;
        Character target =
            context?.Target ??
            context?.Owner;

        if (target == null)
            return;

        if (ResourceType == SkillResourceType.Prestige)
        {
            if (target.RuntimeStatus == null)
                return;

            target.RuntimeStatus.currentPrestige =
                Mathf.Clamp(
                    target.RuntimeStatus.currentPrestige +
                    amount,
                    minimum,
                    target.CurrentStatus?.maxPrestige ?? maximum);
            return;
        }

        SkillResourceAccess.Modify(
            target,
            customResourceKey,
            amount,
            minimum,
            maximum);
    }
}