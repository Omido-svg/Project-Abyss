using UnityEngine;

/// <summary>
/// 1029 DMG-07: 동률 재굴림 정책.
///
/// 정본 §3.1:
///   - 동률이면 승부가 날 때까지 **위력만** 다시 굴린다(속도 보정 고정).
///   - 서로 항상 같은 값이 나오는 고정 범위는 무한 루프 위험이 있으므로
///     '콘텐츠 오류'로 판정해 사전에 중단한다.
///   - 동률 → 강제 무승부를 게임 규칙으로 추가하지 않는다.
///
/// 이 클래스는 '값이 절대 변할 수 없는 굴림'인지만 판정한다.
/// 실제 동률 여부는 호출부가 ClashPower로 확인한다.
/// </summary>
public static class ClashTiePolicy
{
    /// <summary>
    /// 같은 동률 쌍이 반복될 때 '진단 경고'를 남기는 횟수.
    /// 1029 R7(리뷰 B): 확률적으로도 발생할 수 있으므로 이 값을 초과해도
    /// 교환을 취소하지 않는다. 실제 중단은 64회 안전 상한이 담당한다.
    /// 게임 규칙이 아니라 콘텐츠 오류 감지 보조 임계값이다.
    /// </summary>
    public const int RepeatedTiePairLimit = 4;

    public static bool IsDeterministicRoll(
        BattleAction action,
        int exchangeIndex)
    {
        if (action?.Skill == null)
            return false;

        SkillRollData data =
            action.Skill.GetRollData(exchangeIndex);

        if (data == null)
            return false;

        switch (ResolveType(data, action.Skill))
        {
            case SkillResolverType.Coin:
                // 앞/뒤 확률이 0 또는 1이면 결과가 고정된다.
                return data.CoinFrontChance <= 0f ||
                       data.CoinFrontChance >= 1f;

            case SkillResolverType.Slot:
            {
                int minimum = Mathf.Clamp(data.SlotMinimum, 1, 9);
                int maximum = Mathf.Clamp(data.SlotMaximum, minimum, 9);
                return minimum == maximum;
            }

            case SkillResolverType.Chinchiro:
                // 주사위 3개 조합은 항상 무작위다.
                return false;

            default:
                // Dice(절대/기본+굴림 공통): 최소 == 최대면 값이 고정된다.
                return data.SafeMinPower == data.SafeMaxPower;
        }
    }

    /// <summary>
    /// 양쪽 굴림이 모두 값이 고정되어 있고 현재 합 판정값이 같다면,
    /// 재굴림으로는 절대 승부가 나지 않는다.
    /// </summary>
    public static bool IsGuaranteedTie(
        BattleAction first,
        BattleAction second,
        int exchangeIndex)
    {
        return first != null &&
               second != null &&
               first.ClashPower == second.ClashPower &&
               IsDeterministicRoll(first, exchangeIndex) &&
               IsDeterministicRoll(second, exchangeIndex);
    }

    private static SkillResolverType ResolveType(
        SkillRollData data,
        Skill skill)
    {
        switch (data.RngSource)
        {
            case RollRngSource.Dice:
                return SkillResolverType.Dice;
            case RollRngSource.Coin:
                return SkillResolverType.Coin;
            case RollRngSource.Chinchiro:
                return SkillResolverType.Chinchiro;
            case RollRngSource.Slot:
                return SkillResolverType.Slot;
            default:
                return skill?.Definition?.ResolverType ??
                       SkillResolverType.Dice;
        }
    }
}
