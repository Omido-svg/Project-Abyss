using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Olaf Skill Set",
    fileName = "OlafSkillSet")]
public class OlafSkillSet : ScriptableObject
{
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PreparationSkill;
    public SkillDefinition PrestigeSkill;
}