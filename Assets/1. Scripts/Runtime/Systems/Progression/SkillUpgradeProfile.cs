using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Progression/Skill Upgrade Profile", fileName = "NewSkillUpgradeProfile")]
public sealed class SkillUpgradeProfile : ScriptableObject
{
    [Min(0)] public int BaseCostOverride;
    public SkillUpgradeStep Upgrade1 = new();
    public SkillUpgradeStep Upgrade2 = new();

    public int GetBaseCost(ActionType type) => BaseCostOverride > 0
        ? BaseCostOverride
        : type switch
        {
            ActionType.NormalAttack => 50,
            ActionType.Preparation => 100,
            ActionType.Duel => 125,
            ActionType.Prestige => 150,
            _ => 0
        };

    public SkillUpgradeStep GetStep(int targetLevel) => targetLevel switch
    {
        1 => Upgrade1,
        2 => Upgrade2,
        _ => null
    };
}
