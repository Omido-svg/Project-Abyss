using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new()
    {
        new BodyPart(
            PartType.HEAD,
            4,
            8,
            50,
            new Skill[]
            {
                new EnemyNormalAttack(),
                new EnemyDuelSkill()
            })
    };

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        passive = null;
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}