using System;
using System.Collections.Generic;

/// <summary>
/// [0922_CANONICAL_BASELINE]
/// Project Abyss의 현재 정본(0922)을 Verification/Editor 도구가 참조하기 위한
/// Phase 0 canonical manifest.
///
/// 주의:
/// - 이 타입은 gameplay runtime을 변경하지 않는다.
/// - 실제 상태 동작은 Phase 2+에서 이 계약에 맞춰 이관한다.
/// - 0915~0917 CanonicalGameSystemVerificationSpec은 legacy regression oracle이다.
/// </summary>
public static class Canonical0922BaselineSpec
{
    public static readonly string CanonicalVersion = "0922";
    public static readonly string RulesDocumentName = "전투 시스템 규칙 (0922)(1).md";
    public static readonly string SkillSpecDocumentName = "캐릭터 스킬 스펙 (0922)(1).xlsx";

    public static readonly bool NumericStatusesUseIndependentEntries = true;
    public static readonly bool NumericStatusesAllowInfiniteDuration = true;
    public static readonly bool PresenceStatusesUseMaxDurationRefresh = true;
    public static readonly bool CommonStatusCapsEnabled = false;
    public static readonly bool SealIsCommonStatus = false;

    public enum StatusKind
    {
        NumericTimed,
        PresenceTimed,
        BespokeAccumulating,
        NonCommonKeyword
    }

    private static readonly string[] NumericTimed =
    {
        "힘",
        "쇠약",
        "골절",
        "보호",
        "균열",
        "열기",
        "침체",
        "신속",
        "견고",
        "무장해제",
        "재생",
        "지정"
    };

    private static readonly string[] PresenceTimed =
    {
        "고통",
        "공포"
    };

    private static readonly string[] BespokeAccumulating =
    {
        "출혈",
        "표식"
    };

    private static readonly string[] NonCommonKeywords =
    {
        "봉인"
    };

    private static readonly string[] Phase0ConfirmedRules =
    {
        "STATUS_KIND: NumericTimed / PresenceTimed / BespokeAccumulating",
        "NUMERIC: N + T, same-type grants create independent entries",
        "NUMERIC: finite T and infinite duration are canonical",
        "PRESENCE: no N, effect does not stack, duration refreshes by max(current,new)",
        "BESPOKE: Bleeding and Mark do not inherit generic N/T storage rules",
        "OPPOSITE: opposite numeric statuses remain stored and cancel only at calculation",
        "HEAT: turn-end prestige +N",
        "STAGNATION: turn-end prestige -N",
        "REGENERATION: turn-end HP +N and Stagger +N",
        "PAIN: HP/Stagger healing is halved once while active",
        "FEAR: fixed roll power -1 while active, no effect stacking",
        "DESIGNATION: actual Mark gain receives +current Designation total N",
        "SWIFT: normal numeric status timing; reserved next-turn effects materialize before speed roll",
        "COMMON_CAP: no system-wide Value/Duration/entry-count cap",
        "SEAL: shared keyword name, but not a common status"
    };

    // 0922 문서에 아직 (미정)이 남아 있는 대표 도메인.
    // 이 목록은 "구현 누락" 목록이 아니라 "임의 하드코딩 금지" 목록이다.
    // Phase별 verifier는 필요하면 더 세분화할 수 있다.
    private static readonly string[] PendingCanonicalDomainItems =
    {
        "Targeting: separate leg-target handling",
        "Energy: battle-start value when max is increased by augment/item",
        "Enemy: destruction-skill ownership/counts and several enemy numerical values",
        "Enemy: tank duplicate-cover / cover duration-release details",
        "Enemy: duel quota handling when eligible candidates are insufficient",
        "Elite/Boss: unresolved absolute stats/content entries",
        "Hifumi: unresolved card values / temporary keywords such as 핏값",
        "EmotionAugment: unresolved card-specific values",
        "Items/Upgrades: unresolved final effect values and some later costs"
    };

    public static IReadOnlyList<string> NumericTimedStatuses => NumericTimed;
    public static IReadOnlyList<string> PresenceTimedStatuses => PresenceTimed;
    public static IReadOnlyList<string> BespokeAccumulatingStatuses => BespokeAccumulating;
    public static IReadOnlyList<string> NonCommonStatusKeywords => NonCommonKeywords;
    public static IReadOnlyList<string> ConfirmedPhase0Rules => Phase0ConfirmedRules;
    public static IReadOnlyList<string> PendingCanonicalDomains => PendingCanonicalDomainItems;

    public static bool TryGetStatusKind(
        string keyword,
        out StatusKind kind)
    {
        if (Contains(NumericTimed, keyword))
        {
            kind = StatusKind.NumericTimed;
            return true;
        }

        if (Contains(PresenceTimed, keyword))
        {
            kind = StatusKind.PresenceTimed;
            return true;
        }

        if (Contains(BespokeAccumulating, keyword))
        {
            kind = StatusKind.BespokeAccumulating;
            return true;
        }

        if (Contains(NonCommonKeywords, keyword))
        {
            kind = StatusKind.NonCommonKeyword;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool Contains(
        IReadOnlyList<string> values,
        string keyword)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(
                values[i],
                keyword,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
