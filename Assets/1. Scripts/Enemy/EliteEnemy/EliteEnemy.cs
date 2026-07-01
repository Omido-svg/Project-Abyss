using System.Collections.Generic;
using UnityEngine;

public class EliteEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------
    // EliteEnemy는 도사림 사용 허용
    //--------------------------------

    protected override bool AllowPreparationSkillAI => true;

    //--------------------------------
    // 부위 구성
    //--------------------------------

    protected override void BuildBodyParts()
    {
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
    }

    //--------------------------------
    // 메커닉 구성
    //--------------------------------

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new EliteEnemyMechanic());
    }

    //--------------------------------
    // 약화 디버프
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
    // 파괴 디버프
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
        base.Die();

        // CombatMechanic 해제는 Character 쪽에서
        // 일괄 처리하는 구조로 가는 게 좋음.
        // 여기서는 엘리트 전용 사망 연출이 필요할 때만 추가.
    }
}