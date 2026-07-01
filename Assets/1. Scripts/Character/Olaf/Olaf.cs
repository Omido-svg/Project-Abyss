using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    public OlafMadnessMechanic MadnessMechanic =>
        GetMechanic<OlafMadnessMechanic>();

    public OlafImmortalFuryMechanic ImmortalFuryMechanic =>
        GetMechanic<OlafImmortalFuryMechanic>();

    //------------------------------------------------
    // 부위 구성
    //------------------------------------------------

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                40,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill(),
                    new OlafPreparationSkill(),
                    new OlafPrestigeSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                30,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill(),
                    new OlafPrestigeSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                30,
                new Skill[]
                {
                    new OlafNormalAttack(),
                    new OlafDuelSkill(),
                    new OlafPrestigeSkill()
                }));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                50,
                new Skill[]
                {
                    new OlafPreparationSkill(),
                    new OlafPrestigeSkill()
                }));
    }

    //------------------------------------------------
    // 고유 메커닉 구성
    //------------------------------------------------

    protected override void BuildMechanics()
    {
        AddMechanic(new OlafMadnessMechanic());
        AddMechanic(new OlafImmortalFuryMechanic());
    }

    //------------------------------------------------
    // 약화 디버프
    //------------------------------------------------

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

    //------------------------------------------------
    // 파괴 디버프
    //------------------------------------------------

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

    //------------------------------------------------

    public override void Die()
    {
        base.Die();

        // base.Die()에서 이미 사망 로그를 찍고 있으므로
        // 여기서는 올라프 전용 연출이 필요할 때만 추가.
    }
}