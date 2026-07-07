using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Visual/Skill Visual Profile",
    fileName = "SkillVisualProfile")]
public class SkillVisualProfile : ScriptableObject
{
    public SkillVisualDefinition NormalAttackVisual;
    public SkillVisualDefinition DuelVisual;
    public SkillVisualDefinition PreparationVisual;
    public SkillVisualDefinition PrestigeVisual;

    public SkillVisualDefinition GetDefault(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.NormalAttack:
                return NormalAttackVisual;

            case ActionType.Duel:
                return DuelVisual;

            case ActionType.Preparation:
                return PreparationVisual;

            case ActionType.Prestige:
                return PrestigeVisual;

            default:
                return NormalAttackVisual;
        }
    }
}