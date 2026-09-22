using UnityEngine;

/// <summary>
/// 0922 침체 N·T.
/// TurnEnd 위세 축에서 Heat와 algebraically offset하며,
/// 저장 단계에서는 Heat와 서로 제거하지 않는다.
/// </summary>
public sealed class StagnationStatus : NumericTimedStatus
{
    public StagnationStatus(
        int stack = 1,
        int duration = 1)
        : base("침체", stack, duration)
    {
    }
}