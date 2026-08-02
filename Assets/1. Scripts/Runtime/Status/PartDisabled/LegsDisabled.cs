public sealed class LegsDisabled : PartDisabledStatus
{
    public LegsDisabled()
        : base("Legs Weakened")
    {
    }

    // 속도 최대치 -1은 SpeedManager가 캐릭터 전체에 한 번만 적용한다.
    public override bool CanUseSkill(
        BodyPart part,
        Skill skill) =>
        skill != null;
}
