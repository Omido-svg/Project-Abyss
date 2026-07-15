public abstract class CrowdControlStatus : StatusEffect
{
    protected CrowdControlStatus(int duration)
    {
        Duration = duration;
    }

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    // 지속시간 감소/만료 제거는 CharacterStatusController가 한 번만 담당한다.
    public override void OnTurnEnd() { }
}
