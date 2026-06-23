using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartsType.Body, 6, 20)
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

        //--------------------------------
        // 사용할 부위
        //--------------------------------

        BodyPart ownerPart = BodyParts[0];

        //--------------------------------
        // 사용할 스킬
        //--------------------------------

        Skill skill =
            SkillPool[Random.Range(0, SkillPool.Count)];

        //--------------------------------
        // 가장 점수가 높은 부위 선택
        //--------------------------------

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

        //--------------------------------

        return new BattleAction()
        {
            Owner = this,
            Target = target,

            OwnerPart = ownerPart,
            TargetPart = bestPart,

            Skill = skill
        };
    }

    //--------------------------------

    private float CalculateTargetScore(BodyPart part)
    {
        float score = 0f;

        // 이미 부숴진 부위는 대미지가 더 잘 들어간다면 높은 우선순위
        if (part.IsBroken)
            score += 100f;

        // HP가 적을수록 마무리하기 쉬움
        score += part.PartMaxHP - part.PartHP;

        // 속도가 빠른 부위를 우선 제거
        score += part.CurrentSpeed * 3f;

        return score;
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}