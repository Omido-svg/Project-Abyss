public abstract class PartDisabledStatus : StatusEffect
{
    protected PartDisabledStatus(string name)
    {
        Name = name;
        Duration = -1;
    }

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.Permanent;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.Ignore;

    public override bool TransferToCharacterOnPartBreak => false;

    protected override string GetMergeKey() =>
        $"{GetType().FullName}:{SourcePart?.Type.ToString() ?? "NONE"}";

    /// <summary>
    /// PartDisabledStatus는 CharacterStatus에 보관되지만
    /// 실제 적용 범위는 약화를 발생시킨 SourcePart다.
    /// </summary>
    protected bool AffectsPart(BodyPart part) =>
        SourcePart != null &&
        part != null &&
        part == SourcePart;

    /// <summary>
    /// SourcePart에서 발생한 행동에만 부위 약화 효과를 적용한다.
    /// part == null인 글로벌/보스 슬롯은 특정 부위 행동이 아니므로
    /// 부위 약화의 행동 제한/굴림 패널티를 받지 않는다.
    /// </summary>
    protected bool AffectsAction(BattleAction action) =>
        action != null &&
        action.Owner == Owner &&
        SourcePart != null &&
        action.OwnerPart == SourcePart;

    public override void OnApply() { }
    public override void OnRemove() { }
}
