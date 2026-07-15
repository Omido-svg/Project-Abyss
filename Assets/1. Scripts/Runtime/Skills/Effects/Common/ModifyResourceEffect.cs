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
                    Amount,
                    Minimum,
                    target.CurrentStatus?.maxPrestige ?? Maximum);
            return;
        }

        SkillResourceAccess.Modify(
            target,
            CustomResourceKey,
            Amount,
            Minimum,
            Maximum);
    }
}
