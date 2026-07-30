using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Gain Custom Resource",
    fileName = "GainCustomResourceEffect")]
public class GainCustomResourceEffect : SkillEffectDefinition
{
    public string ResourceKey = "Custom";
    public int Amount = 1;
    public int Maximum = 999;
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
        string resourceKey = overrides?.ResolveResourceKey(ResourceKey) ?? ResourceKey;
        int amount = overrides?.ResolveAmount(Amount) ?? Amount;
        int maximum = overrides?.ResolveMaximum(Maximum) ?? Maximum;
        bool giveToSelectedTarget = overrides?.ResolveGiveToSelectedTarget(GiveToSelectedTarget) ?? GiveToSelectedTarget;
        Character target =
            giveToSelectedTarget
                ? context?.Target
                : context?.Owner;

        if (target == null ||
            string.IsNullOrWhiteSpace(resourceKey))
        {
            return;
        }

        int result = SkillResourceAccess.Modify(
            target,
            resourceKey,
            amount,
            0,
            maximum);

        Debug.Log(
            $"{target.Data?.CharacterName} 특수 자원 " +
            $"{resourceKey} : {result}");
    }
}