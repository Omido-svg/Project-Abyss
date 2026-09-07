using UnityEngine;

public readonly struct DamagePowerResolution
{
    public bool HasDamage { get; }
    public bool WasDefenseResolution { get; }

    public int PrimaryPower { get; }
    public bool ApplyPrimaryMomentum { get; }

    public int SecondaryPower { get; }
    public bool ApplySecondaryMomentum { get; }

    public DamagePowerResolution(
        bool hasDamage,
        bool wasDefenseResolution,
        int primaryPower,
        bool applyPrimaryMomentum,
        int secondaryPower,
        bool applySecondaryMomentum)
    {
        HasDamage = hasDamage;
        WasDefenseResolution = wasDefenseResolution;

        PrimaryPower =
            Mathf.Max(
                0,
                primaryPower);

        ApplyPrimaryMomentum =
            applyPrimaryMomentum;

        SecondaryPower =
            Mathf.Max(
                0,
                secondaryPower);

        ApplySecondaryMomentum =
            applySecondaryMomentum;
    }

    public static DamagePowerResolution None(
        bool wasDefenseResolution = false)
    {
        return new DamagePowerResolution(
            false,
            wasDefenseResolution,
            0,
            false,
            0,
            false);
    }
}

/// <summary>
/// 합 승패 결과를 실제 피해 기준 위력으로 변환합니다.
///
/// 공격 대 공격:
///     승자의 순수 굴림 위력
///
/// 공격 대 수비:
///     max(1, 공격 합 수치 - 수비 합 수치)
///
/// 수비 승리:
///     피해 없음
/// </summary>
public static class DamagePowerResolver
{
    public static DamagePowerResolution ResolvePaired(
        BattleAction winner,
        BattleAction loser)
    {
        if (winner == null ||
            loser == null)
        {
            return DamagePowerResolution.None();
        }

        bool winnerDefense =
            winner.CurrentRollType ==
            CombatRollType.Defense;

        bool loserDefense =
            loser.CurrentRollType ==
            CombatRollType.Defense;

        bool defenseResolution =
            winnerDefense ||
            loserDefense;

        if (winnerDefense)
        {
            return DamagePowerResolution.None(
                wasDefenseResolution: true);
        }

        if (loserDefense)
        {
            return new DamagePowerResolution(
                hasDamage: true,
                wasDefenseResolution: true,
                primaryPower:
                    Mathf.Max(
                        1,
                        winner.ClashPower -
                        loser.ClashPower),
                applyPrimaryMomentum: false,

                // 기존 계산 결과를 유지합니다.
                // Attack Weight의 추가 대상은 수비 합 격차가 아니라
                // 공격자의 순수 굴림 위력을 사용합니다.
                secondaryPower:
                    winner.GetDamagePower(),
                applySecondaryMomentum: true);
        }

        int purePower =
            winner.GetDamagePower();

        return new DamagePowerResolution(
            hasDamage: true,
            wasDefenseResolution: defenseResolution,
            primaryPower: purePower,
            applyPrimaryMomentum: true,
            secondaryPower: purePower,
            applySecondaryMomentum: true);
    }

    public static DamagePowerResolution ResolveOneSided(
        BattleAction action)
    {
        if (action == null)
            return DamagePowerResolution.None();

        if (action.CurrentRollType ==
            CombatRollType.Defense)
        {
            return DamagePowerResolution.None(
                wasDefenseResolution: true);
        }

        int purePower =
            action.GetDamagePower();

        return new DamagePowerResolution(
            hasDamage: true,
            wasDefenseResolution: false,
            primaryPower: purePower,
            applyPrimaryMomentum: true,
            secondaryPower: purePower,
            applySecondaryMomentum: true);
    }
}
