using System.Collections.Generic;
using UnityEngine;

// 올라프의 광전사의 결투를
// 사슬 결투로 교체

[CreateAssetMenu(
    menuName = "Battle/Item/Olaf/Chain Grip")]
public class OlafChainGripItem : CharacterItem
{
    public override void ModifySkills(
        Character owner,
        BodyPart part,
        List<Skill> skills)
    {
        if (!(owner is Olaf))
            return;

        if (skills == null)
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] is OlafDuelSkill)
            {
                skills[i] = new OlafChainDuelSkill();
            }
        }
    }
}