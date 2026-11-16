using UnityEngine;

namespace ProjectAbyss.Abilities
{
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Modifier Settings",fileName="ModifierSettings")]
    public sealed class CombatModifierDefinition : ScriptableObject,IAbilityRuleSettingsDefinition
    {
        public CombatModifierStage Stage;
        public CombatModifierKind Kind;
        public int Amount,Priority;
        [Min(0)] public int DurationTurns;
        public IAbilityRuleSettings CaptureSettings()=>new CombatModifierSpec(Stage,Kind,Amount,Priority,DurationTurns);
    }
}
