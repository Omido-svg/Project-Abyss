using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        //--------------------------------
        // 1. 부위 먼저 생성
        //--------------------------------

        bodyParts.Clear();

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                50,
                new Skill[]
                {
                    new EnemyNormalAttack(),
                    new EnemyDuelSkill(),
                    new EnemyPreparationSkill(),
                    new EnemyPrestigeSkill()
                }));

        //--------------------------------
        // 2. Character 기본 초기화
        //--------------------------------

        base.Initialize(battleEvent);

        //--------------------------------
        // 3. 패시브 등록
        //--------------------------------

        passive = new NormalEnemyPassive();
        passive.Initialize(this, battleEvent);
        passive.Register();
    }

    //--------------------------------

    protected override StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new HeadDisabled(),
            _ => null
        };
    }

    //--------------------------------

    protected override StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            _ => null
        };
    }

    //--------------------------------

    public override void Die()
    {
        passive?.Unregister();

        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}