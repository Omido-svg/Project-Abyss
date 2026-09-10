using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 히후미 — 친치로 / 뼈 / 반격 캐릭터.
/// 최대 체력 500을 4부위 × 125로 구성한다.
/// </summary>
public sealed class Hifumi : Character, ICharacterAuthoringTarget
{
    [SerializeField, Min(1)] private int partHitPoints = 125;

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;
    public override bool SupportsLastStand => true;

    public HifumiMechanic HifumiMechanic => GetMechanic<HifumiMechanic>();

    public bool ApplyCharacterAuthoring(CharacterAuthoringBundle bundle) =>
        bundle != null && bundle.SkillSet == null;

    public override Skill CreateRuntimeSkillForLoadout(SkillDefinition definition)
    {
        if (definition == null)
            return null;

        return definition.ActionType switch
        {
            ActionType.NormalAttack => new HifumiNormalRuntimeSkill(definition),
            ActionType.Duel => new HifumiDuelRuntimeSkill(definition),
            ActionType.Preparation => new HifumiPreparationRuntimeSkill(definition),
            ActionType.Prestige => new HifumiPrestigeRuntimeSkill(definition),
            _ => base.CreateRuntimeSkillForLoadout(definition)
        };
    }

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();
        int hp = Mathf.Max(1, partHitPoints);
        Skill[] none = System.Array.Empty<Skill>();

        bodyParts.Add(new BodyPart(PartType.HEAD, hp, none));
        bodyParts.Add(new BodyPart(PartType.LEFT_HAND, hp, none));
        bodyParts.Add(new BodyPart(PartType.RIGHT_HAND, hp, none));
        bodyParts.Add(new BodyPart(PartType.LEGS, hp, none));
    }

    protected override void BuildMechanics()
    {
        AddMechanic(new HifumiMechanic());
    }

    protected override StatusEffect CreateDisabledDebuff(BodyPart part)
    {
        return part?.Type switch
        {
            PartType.HEAD => new HeadDisabled(),
            PartType.LEFT_HAND => new ArmDisabled(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new ArmDisabled(PartType.RIGHT_HAND),
            PartType.LEGS => new LegsDisabled(),
            _ => null
        };
    }

    protected override StatusEffect CreateBrokenPartStatus(BodyPart part)
    {
        return part?.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
            _ => null
        };
    }
}
