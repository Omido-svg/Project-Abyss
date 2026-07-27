public abstract class PartDisabledStatus : StatusEffect
{
    protected PartDisabledStatus(string name)
    {
        Name = name;
        Duration = -1;
    }

    public override StatusEffectDurationPolicy DurationPolicy => StatusEffectDurationPolicy.Permanent;
    public override StatusEffectStackPolicy StackPolicy => StatusEffectStackPolicy.Ignore;
    public override bool TransferToCharacterOnPartBreak => false;

    protected override string GetMergeKey() =>
        $"{GetType().FullName}:{SourcePart?.Type.ToString() ?? "NONE"}";

    protected bool IsOwnerAction(BattleAction action) =>
        action != null && action.Owner == Owner;

    public override void OnApply() { }
    public override void OnRemove() { }
}
