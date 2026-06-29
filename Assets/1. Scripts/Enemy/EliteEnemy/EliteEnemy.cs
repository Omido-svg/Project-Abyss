
using System.Collections.Generic;
using UnityEngine;

class EliteEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartType.HEAD, 50),
        new BodyPart(PartType.LEFT_HAND, 50),
        new BodyPart(PartType.RIGHT_HAND, 50),
        new BodyPart(PartType.LEGS, 50)
    };

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);
        
        foreach(BodyPart part in bodyParts)
            part.Initialize(this);
        
        passive = null;
    }

    //--------------------------------

    private void Awake()
    {

    }

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}