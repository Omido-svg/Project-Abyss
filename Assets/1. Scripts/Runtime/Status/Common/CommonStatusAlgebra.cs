using System;

/// <summary>
/// 0915 C-49 공용 반대 상태의 대수 상쇄 규칙.
/// 미정 상태(Sturdy/Disarm)는 Runtime 신규 부여가 비활성이지만 구 에셋 호환을 위해 쌍 정의는 유지한다.
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
