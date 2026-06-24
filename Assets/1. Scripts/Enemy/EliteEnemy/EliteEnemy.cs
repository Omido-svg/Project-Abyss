
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

    //--------------------------------

    public override BattleAction DecideAction(BattleContext context)
    {
        Character target = context.Player;

        // 사용할 부위
        BodyPart ownerPart = BodyParts[0];

        // 사용할 스킬
        Skill skill =
            SkillPool[Random.Range(0, SkillPool.Count)];

        // 가장 점수가 높은 부위 선택
        BodyPart bestPart = null;
        float bestScore = float.MinValue;

        foreach (BodyPart part in target.BodyParts)
        {
            float score = CalculateTargetScore(part);

            if (score > bestScore)
            {
                bestScore = score;
                bestPart = part;
            }
        }

        return new BattleAction()
        {
            Owner = this,
            Target = target,

            OwnerPart = ownerPart,
            TargetPart = bestPart,

            Skill = skill
        };
    }

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}