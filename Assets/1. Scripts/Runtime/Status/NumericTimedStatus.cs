using UnityEngine;

/// <summary>
/// 0922 NumericTimed 저장 모델의 공통 기반.
/// N(Stack)과 T(Duration)를 독립 보존하며 같은 타입끼리 병합하지 않는다.
/// </summary>
public abstract class NumericTimedStatus : StatusEffect
{
    public override StatusEffectStorageKind StorageKind =>
        StatusEffectStorageKind.NumericTimed;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.Ignore;

    protected NumericTimedStatus(
        string name,
        int value,
        int duration = 1)
    {
        Name = name;
        Stack = Mathf.Max(0, value);
        Duration = NormalizeTimedDuration(duration);
    }

    public override bool CanMergeWith(StatusEffect other) => false;

    public override void Merge(StatusEffect other)
    {
        // NumericTimed는 저장 단계에서 merge하지 않는다.
    }
}
