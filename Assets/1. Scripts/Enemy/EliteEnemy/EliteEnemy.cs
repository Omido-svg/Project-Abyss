
using System.Collections.Generic;
using UnityEngine;

class EliteEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartType.HEAD, 4, 8, 50),
        new BodyPart(PartType.LEFT_HAND, 4, 8, 50),
        new BodyPart(PartType.RIGHT_HAND, 4, 8, 50),
        new BodyPart(PartType.LEGS, 4, 8, 50),
    };

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        passive = null;
    }

    //--------------------------------

    private void Awake()
    {
        SkillPool = new List<Skill>()
        {
            new EnemyNormalAttack(),
            new EnemyDuelSkill()
        };
    }

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}