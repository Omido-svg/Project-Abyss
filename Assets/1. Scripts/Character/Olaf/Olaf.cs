using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    private List<BodyPart> bodyParts = new();
    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;
    public OlafPassive Passive => passive as OlafPassive;
    
    //------------------------------------------------
    // 상태 초기화
    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);
        
        bodyParts.Clear();

        bodyParts.Add(new BodyPart(PartType.HEAD, 6, 10, 20));
        bodyParts.Add(new BodyPart(PartType.LEFT_HAND, 4, 8, 20));
        bodyParts.Add(new BodyPart(PartType.RIGHT_HAND, 4, 8, 20));
        bodyParts.Add(new BodyPart(PartType.LEGS, 3, 7, 20));

        SkillPool = new List<Skill>()
        {
            new OlafNormalAttack(),
            new OlafDuelSkill(),
            new OlafAmbushSkill(),
            new OlafPrestigeSkill()
        };
        
        passive = new OlafPassive(this, battleEvent);
    }
    //------------------------------------------------

    public override void Die()
    {
        base.Die();
        Debug.Log($"{Data.CharacterName} 사망");
    }

}