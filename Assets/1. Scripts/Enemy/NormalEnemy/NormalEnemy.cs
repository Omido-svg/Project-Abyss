using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

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
                    new EnemyNormalAttack(),
                    new EnemyDuelSkill(),
                    new EnemyPrestigeSkill()
                }));
    }

    //--------------------------------
    // 메커닉 구성
    //--------------------------------

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new NormalEnemyBloodScentMechanic());
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
            _ => null
        };
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();

        // base.Die()에서 이미 사망 로그를 찍고 있다면
        // 여기서 중복 로그는 찍지 않는 게 좋다.
    }
}