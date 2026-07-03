using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Elite Enemy Skill Set",
    fileName = "EliteEnemySkillSet")]
public class EliteEnemySkillSet : ScriptableObject
{
    [Header("Elite Enemy Skills")]
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PreparationSkill;
    public SkillDefinition PrestigeSkill;
}