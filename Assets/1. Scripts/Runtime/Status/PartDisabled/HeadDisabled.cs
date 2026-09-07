public sealed class HeadDisabled : PartDisabledStatus
{
    public HeadDisabled()
        : base("Head Weakened")
    {
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        // 머리 약화의 스킬 제한은 HEAD 슬롯에만 적용한다.
        // PartDisabledStatus는 CharacterStatus에 저장되므로 SourcePart 스코프를
        // 확인하지 않으면 팔 슬롯과 part == null인 보스 글로벌 슬롯까지 막힌다.
        if (!AffectsPart(part))
            return true;

        return skill.ActionType == ActionType.NormalAttack;
    }
}
