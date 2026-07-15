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

    // 약화 디버프는 부위가 파괴되면 제거되고 캐릭터 상태로 이전되지 않는다.
    public override bool TransferToCharacterOnPartBreak => false;

    public override void OnApply() { }
    public override void OnRemove() { }
}
