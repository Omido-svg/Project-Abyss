using System.Collections.Generic;
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
        SkillVisualDefinition result = actionType switch
        {
            ActionType.NormalAttack => NormalAttackVisual,
            ActionType.Duel => DuelVisual,
            ActionType.Preparation => PreparationVisual,
            ActionType.Prestige => PrestigeVisual,
            _ => NormalAttackVisual
        };

        return result != null && result.AllowAsProfileFallback
            ? result
            : null;
    }

    public IEnumerable<SkillVisualDefinition> EnumerateDefinitions()
    {
        if (NormalAttackVisual != null)
            yield return NormalAttackVisual;

        if (DuelVisual != null)
            yield return DuelVisual;

        if (PreparationVisual != null)
            yield return PreparationVisual;

        if (PrestigeVisual != null)
            yield return PrestigeVisual;
    }
}
