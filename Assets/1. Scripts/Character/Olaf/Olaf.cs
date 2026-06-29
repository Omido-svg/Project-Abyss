using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    public OlafPassive Passive => passive as OlafPassive;

    //------------------------------------------------
    // 상태 초기화
    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        bodyParts.Clear();

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                40,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill(),
                    new OlafAmbushSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                30,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                30,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                50,
                new Skill[]
                {
                    new OlafAmbushSkill()
                }));

        passive = new OlafPassive(this, battleEvent);
        
        foreach(BodyPart part in bodyParts)
            part.Initialize(this);
    }

    //------------------------------------------------

    public override void Die()
    {
        base.Die();
        Debug.Log($"{Data.CharacterName} 사망");
    }
}