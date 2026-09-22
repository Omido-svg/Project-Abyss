using System;

/// <summary>
/// 0922 공용 반대 상태의 계산 축 pair map.
/// 이 타입은 더 이상 상태 저장/부여 단계에서 Stack을 소모하거나 Entry를 제거하지 않는다.
/// 실제 aggregate 계산 적용은 Phase 3이 소유한다.
/// </summary>
public static class CommonStatusAlgebra
{
    public static bool AreOpposites(StatusEffect first, StatusEffect second)
    {
        if (first == null || second == null)
            return false;

        Type a = first.GetType();
        Type b = second.GetType();

        return IsPair<StrengthStatus, WeaknessStatus>(a, b) ||
               IsPair<ProtectionStatus, RuptureStatus>(a, b) ||
               IsPair<SturdyStatus, DisarmStatus>(a, b);
    }

    private static bool IsPair<TA, TB>(Type a, Type b)
    {
        return (a == typeof(TA) && b == typeof(TB)) ||
               (a == typeof(TB) && b == typeof(TA));
    }
}