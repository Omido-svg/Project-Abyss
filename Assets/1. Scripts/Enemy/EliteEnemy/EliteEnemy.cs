using System.Collections.Generic;
using UnityEngine;

public class EliteEnemy : Enemy
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
                    new EliteNormalAttack(),
                    new EliteDuelSkill(),
                    new ElitePreparationSkill(),
                    new ElitePrestigeSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                50,
                new Skill[]
                {
                    new EliteNormalAttack(),
                    new EliteDuelSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                50,
                new Skill[]
                {
                    new EliteNormalAttack(),
                    new EliteDuelSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                50,
                new Skill[]
                {
                    new ElitePreparationSkill()
                }));

        //--------------------------------
        // 2. Character 기본 초기화
        //--------------------------------

        base.Initialize(battleEvent);

        passive = new EliteEnemyPassive();
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
            PartType.LEFT_HAND => new ArmDisabled(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new ArmDisabled(PartType.RIGHT_HAND),
            PartType.LEGS => new LegsDisabled(),
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
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
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