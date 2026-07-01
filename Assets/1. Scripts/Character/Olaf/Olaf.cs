using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    public OlafPassive Passive => passive as OlafPassive;

    //------------------------------------------------
    // 상태 초기화
    //------------------------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        //--------------------------------
        // 1. 부위 먼저 생성
        //--------------------------------

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

        //--------------------------------
        // 2. Character 기본 초기화
        // 여기서 BodyPart.Initialize(this),
        // RuntimeStatus.currentHP 계산 등이 처리됨
        //--------------------------------

        base.Initialize(battleEvent);

        //--------------------------------
        // 3. 패시브 생성
        //--------------------------------

        passive = new OlafPassive();
        passive.Initialize(this, battleEvent);
        passive.Register();
    }

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

        Debug.Log($"{Data.CharacterName} 사망");
    }
}