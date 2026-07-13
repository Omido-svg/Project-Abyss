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
        Character target =
            GiveToSelectedTarget
                ? context?.Target
                : context?.Owner;

        if (target == null ||
            string.IsNullOrWhiteSpace(ResourceKey))
        {
            return;
        }

        int result = SkillResourceAccess.Modify(
            target,
            ResourceKey,
            Amount,
            0,
            Maximum);

        Debug.Log(
            $"{target.Data?.CharacterName} 특수 자원 " +
            $"{ResourceKey} : {result}");
    }
}
