using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Normal Enemy Skill Set",
    fileName = "NormalEnemySkillSet")]
public class NormalEnemySkillSet : ScriptableObject
{
    [Header("Normal Enemy Skills")]
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PrestigeSkill;
}