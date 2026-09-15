using System.Collections.Generic;

/// <summary>
/// 스킬 효과가 언제 발동 판정을 하는지를 나타내는 트리거입니다.
///
/// Rules 2026-09 원칙:
/// - 신규 콘텐츠는 SkillEffectTimingCatalog.AuthoringTimings에 있는 정본 트리거만 사용합니다.
/// - SkillRollData.EffectEntries는 RollAuthoringTimings만 사용합니다.
/// - 0~30 값은 기존 ScriptableObject 직렬화 호환 때문에 절대 번호를 바꾸지 않습니다.
/// - 정본에서 제거된 세부 phase는 런타임 호환을 위해 남기되 Authoring UI에서는 숨깁니다.
/// </summary>
public enum SkillEffectTiming
{
    // ---------------------------------------------------------------------
    // Serialized compatibility block. 숫자를 바꾸거나 재사용하지 않는다.
    // ---------------------------------------------------------------------
    OnExecute = 0,                 // 정본: 사용시 / 준비 행동 발동
    OnClashWin = 1,                // 정본: 합 승리시 (맞붙은 교환 다수결)
    OnClashLose = 2,               // 정본: 합 패배시 (맞붙은 교환 다수결)
    AfterDamage = 3,               // 정본: 피해 적용 후
    OnCritical = 4,                // 정본: 크리티컬 피해 확정 후
    OnKill = 5,                    // 정본: 처치시
    OnActionEnd = 6,               // 호환 전용
    OnExchangeWin = 7,             // 정본: 교환 승리시
    OnExchangeLose = 8,            // 정본: 교환 패배시
    OnOneSideHit = 9,              // 호환 전용
    OnClashDraw = 10,              // 호환 전용
    OnMultiRollPenaltyStart = 11,   // 내부/호환 전용
    OnMultiRollPenaltyAfterRoll = 12,
    OnMultiRollPenaltyEnd = 13,
    OnRollWin = 14,                // 구 Roll OnWin 리스트 호환
    OnRollLose = 15,               // 구 Roll OnLose 리스트 호환

    // ---------------------------------------------------------------------
    // 과거 Gameplay v5 detailed phase. 번호 보존용.
    // AuthoringTimings에 포함된 값만 신규 콘텐츠에서 사용한다.
    // ---------------------------------------------------------------------
    OnBattleStart = 16,            // 정본
    OnTurnStart = 17,              // 정본
    BeforeUse = 18,                // 호환 전용
    OnClashStart = 19,             // 시스템/호환 전용
    OnOneSidedStart = 20,          // 호환 전용
    BeforeAttack = 21,             // 호환 전용
    OnRollStart = 22,              // 호환 전용
    OnRollSuccess = 23,            // 구 Gameplay v5 -> 승리시로 이관
    OnRollFailure = 24,            // 구 Gameplay v5 -> 패배시로 이관
    OnHit = 25,                    // 호환 전용
    OnRollEnd = 26,                // 호환 전용
    OnAttackEnd = 27,              // 호환 전용
    OnSkillEnd = 28,               // 호환 전용
    OnTurnEnd = 29,                // 정본
    OnBattleEnd = 30,              // 정본

    // ---------------------------------------------------------------------
    // Rules 2026-09 canonical additions.
    // ---------------------------------------------------------------------
    OnDuelMatched = 31,            // 정본: 매칭시
    OnPartWeakened = 32,           // 해당 피해로 정상 -> 약화 전이
    OnPartBroken = 33,             // 해당 피해로 약화 -> 파괴 전이
    OnClashEnd = 34                // 정본: 합 종료시 (남은 일방타격까지 모두 처리 후)
}

/// <summary>
/// 정본 전투 규칙에 맞춘 SkillEffectTiming의 단일 카탈로그입니다.
/// Runtime, UI, Authoring, Migration이 모두 이 정의를 공유합니다.
/// </summary>
public static class SkillEffectTimingCatalog
{
    private static readonly SkillEffectTiming[] authoringTimings =
    {
        SkillEffectTiming.OnBattleStart,
        SkillEffectTiming.OnTurnStart,

        // 카드/준비 행동의 핵심 문법
        SkillEffectTiming.OnExecute,
        SkillEffectTiming.OnDuelMatched,
        SkillEffectTiming.OnExchangeWin,
        SkillEffectTiming.OnExchangeLose,

        // 실제 결과 기반 이벤트
        SkillEffectTiming.AfterDamage,
        SkillEffectTiming.OnCritical,
        SkillEffectTiming.OnPartWeakened,
        SkillEffectTiming.OnPartBroken,
        SkillEffectTiming.OnKill,

        // 합 단위 결과 / 종료. 승패는 맞붙은 교환 다수결, 종료는 남은 일방타격까지 처리한 뒤다.
        SkillEffectTiming.OnClashWin,
        SkillEffectTiming.OnClashLose,
        SkillEffectTiming.OnClashEnd,

        SkillEffectTiming.OnTurnEnd,
        SkillEffectTiming.OnBattleEnd
    };

    private static readonly SkillEffectTiming[] rollAuthoringTimings =
    {
        SkillEffectTiming.OnDuelMatched,
        SkillEffectTiming.OnExchangeWin,
        SkillEffectTiming.OnExchangeLose,
        SkillEffectTiming.AfterDamage,
        SkillEffectTiming.OnCritical,
        SkillEffectTiming.OnPartWeakened,
        SkillEffectTiming.OnPartBroken,
        SkillEffectTiming.OnKill
    };

    public static IReadOnlyList<SkillEffectTiming> AuthoringTimings =>
        authoringTimings;

    /// <summary>
    /// SkillRollData.EffectEntries에서 사용할 굴림 위치 기반 정본 트리거.
    /// 전투/턴/사용/합 단위 트리거는 SkillDefinition.EffectEntries에서 작성한다.
    /// </summary>
    public static IReadOnlyList<SkillEffectTiming> RollAuthoringTimings =>
        rollAuthoringTimings;

    public static string GetDisplayName(SkillEffectTiming timing) => timing switch
    {
        SkillEffectTiming.OnBattleStart => "전투 시작시",
        SkillEffectTiming.OnTurnStart => "턴 시작시",
        SkillEffectTiming.OnExecute => "사용시",
        SkillEffectTiming.OnDuelMatched => "매칭시 (결투)",
        SkillEffectTiming.OnExchangeWin => "교환 승리시",
        SkillEffectTiming.OnExchangeLose => "교환 패배시",
        SkillEffectTiming.AfterDamage => "피해 적용 후",
        SkillEffectTiming.OnCritical => "크리티컬시",
        SkillEffectTiming.OnPartWeakened => "부위 약화시",
        SkillEffectTiming.OnPartBroken => "부위 파괴시",
        SkillEffectTiming.OnKill => "처치시",
        SkillEffectTiming.OnClashWin => "합 승리시 (다수결)",
        SkillEffectTiming.OnClashLose => "합 패배시 (다수결)",
        SkillEffectTiming.OnClashEnd => "합 종료시",
        SkillEffectTiming.OnTurnEnd => "턴 종료시",
        SkillEffectTiming.OnBattleEnd => "전투 종료시",

        // 아래는 Serialized compatibility / 내부 파이프라인용.
        SkillEffectTiming.OnActionEnd => "행동 종료시 (호환 전용)",
        SkillEffectTiming.OnOneSideHit => "일방 적중시 (호환 전용)",
        SkillEffectTiming.OnClashDraw => "합 무승부시 (호환 전용)",
        SkillEffectTiming.OnMultiRollPenaltyStart => "다굴림 페널티 시작 (내부)",
        SkillEffectTiming.OnMultiRollPenaltyAfterRoll => "다굴림 페널티 굴림 후 (내부)",
        SkillEffectTiming.OnMultiRollPenaltyEnd => "다굴림 페널티 종료 (내부)",
        SkillEffectTiming.OnRollWin => "굴림 승리시 (구형)",
        SkillEffectTiming.OnRollLose => "굴림 패배시 (구형)",
        SkillEffectTiming.BeforeUse => "사용 전 (호환 전용)",
        SkillEffectTiming.OnClashStart => "합 시작시 (시스템/호환)",
        SkillEffectTiming.OnOneSidedStart => "일방 공격 시작시 (호환 전용)",
        SkillEffectTiming.BeforeAttack => "공격 시작 전 (호환 전용)",
        SkillEffectTiming.OnRollStart => "굴림 시작시 (호환 전용)",
        SkillEffectTiming.OnRollSuccess => "굴림 성공시 (구형 -> 승리시)",
        SkillEffectTiming.OnRollFailure => "굴림 실패시 (구형 -> 패배시)",
        SkillEffectTiming.OnHit => "적중시 (호환 전용)",
        SkillEffectTiming.OnRollEnd => "굴림 종료시 (호환 전용)",
        SkillEffectTiming.OnAttackEnd => "공격 종료시 (호환 전용)",
        SkillEffectTiming.OnSkillEnd => "스킬 종료시 (호환 전용)",
        _ => timing.ToString()
    };

    public static string GetColorHex(SkillEffectTiming timing)
    {
        // 발동 시점은 이로운/해로운 효과와 별개의 정보이므로 한 계열로 통일한다.
        return "#73D673";
    }

    public static string GetRichTextLabel(SkillEffectTiming timing)
    {
        return
            $"<color={GetColorHex(timing)}><b>[{GetDisplayName(timing)}]</b></color>";
    }

    public static bool IsAuthoringTiming(SkillEffectTiming timing)
    {
        return Contains(authoringTimings, timing);
    }

    public static bool IsRollAuthoringTiming(SkillEffectTiming timing)
    {
        return Contains(rollAuthoringTimings, timing);
    }

    // 구 호출부 호환.
    public static bool IsGameplayV5AuthoringTiming(SkillEffectTiming timing) =>
        IsAuthoringTiming(timing);

    /// <summary>
    /// 새 계약으로 기계적으로 옮길 수 있는 구형 승패 타이밍만 반환한다.
    /// 합 시작/적중/행동 종료처럼 정본에서 의미가 달라진 phase는 false로 남겨
    /// 카드 문구를 보고 수동 검토하게 한다.
    /// </summary>
    public static bool TryGetCanonicalMigration(
        SkillEffectTiming legacy,
        out SkillEffectTiming replacement)
    {
        switch (legacy)
        {
            case SkillEffectTiming.OnRollSuccess:
            case SkillEffectTiming.OnRollWin:
                replacement = SkillEffectTiming.OnExchangeWin;
                return true;

            case SkillEffectTiming.OnRollFailure:
            case SkillEffectTiming.OnRollLose:
                replacement = SkillEffectTiming.OnExchangeLose;
                return true;

            default:
                replacement = legacy;
                return IsAuthoringTiming(legacy);
        }
    }

    // 이전 이름을 사용한 외부/에디터 코드 호환.
    public static bool TryGetSafeMigration(
        SkillEffectTiming legacy,
        out SkillEffectTiming replacement) =>
        TryGetCanonicalMigration(legacy, out replacement);

    public static string GetSemantics(SkillEffectTiming timing) => timing switch
    {
        SkillEffectTiming.OnBattleStart =>
            "전투 진입 직후 1회. 전투 단위 초기 버프/초기 자원에 사용합니다.",

        SkillEffectTiming.OnTurnStart =>
            "해당 턴 시작 시점. 턴 시작형 효과에 사용합니다.",

        SkillEffectTiming.OnExecute =>
            "정본의 '사용시'. 스킬/준비 행동을 실제로 사용한 순간 확정되며 매칭·승패와 무관합니다.",

        SkillEffectTiming.OnDuelMatched =>
            "정본의 '매칭시'. 원래 행동이 결투 대 결투로 매칭된 경우 각 굴림 위치마다 발동합니다. 상대 굴림이 먼저 소진되어 뒤쪽 위치가 일방타격이 되어도 원래 결투 대 결투 게이트는 유지됩니다.",

        SkillEffectTiming.OnExchangeWin =>
            "정본의 '승리시' = 교환 승리시. 해당 굴림이 상대 굴림과 실제로 맞붙어 이긴 직후, 피해 적용 전에 발동합니다. 평타는 유효한 일방타격도 승리로 보지만 결투는 결투 대 결투에서 실제로 맞붙어 이긴 경우에만 발동합니다. 합 전체 다수결 승리와는 별개입니다.",

        SkillEffectTiming.OnExchangeLose =>
            "정본의 '패배시' = 교환 패배시. 해당 굴림이 상대 굴림과 실제로 맞붙어 진 직후, 피해 적용 전에 발동합니다. 결투의 패배 효과는 결투 대 결투 게이트 안에서만 발동합니다. 합 전체 다수결 패배와는 별개입니다.",

        SkillEffectTiming.AfterDamage =>
            "실제 피해 적용이 끝난 직후. DamageContext의 적용 피해량/가드/부위 HP 결과를 참조할 수 있습니다.",

        SkillEffectTiming.OnCritical =>
            "해당 피해가 크리티컬로 확정되어 적용된 직후입니다.",

        SkillEffectTiming.OnPartWeakened =>
            "이 스킬의 해당 피해로 부위가 정상에서 약화로 실제 전이했을 때 1회 발동합니다. 이미 약화 상태였던 타격은 포함하지 않습니다.",

        SkillEffectTiming.OnPartBroken =>
            "이 스킬의 해당 피해로 부위가 약화에서 파괴로 실제 전이했을 때 1회 발동합니다.",

        SkillEffectTiming.OnKill =>
            "이 스킬의 피해로 대상 처치가 확정된 직후입니다.",

        SkillEffectTiming.OnClashWin =>
            "합 전체 다수결 승리. 실제 맞부딪힌 교환 승수만 세고 일방타격은 제외합니다. 맞대결 교환이 모두 끝난 뒤, 남은 일방타격 처리 전에 확정됩니다.",

        SkillEffectTiming.OnClashLose =>
            "합 전체 다수결 패배. 실제 맞부딪힌 교환 승수만 세고 일방타격은 제외합니다. 맞대결 교환이 모두 끝난 뒤, 남은 일방타격 처리 전에 확정됩니다.",

        SkillEffectTiming.OnClashEnd =>
            "정본의 '합 종료시'. 합 승패 확정과 남은 굴림의 일방타격까지 모두 처리된 뒤 양쪽 스킬에 1회 발동합니다. 합이 붙지 않은 일방 공격에는 발동하지 않습니다. SkillEffectContext.ClashResult에서 교환 승수/일방타격 수 등 합 전체 결과를 참조할 수 있습니다.",

        SkillEffectTiming.OnTurnEnd =>
            "턴 종료 정산 시점입니다.",

        SkillEffectTiming.OnBattleEnd =>
            "전투 종료가 확정된 뒤 1회입니다.",

        SkillEffectTiming.OnRollSuccess =>
            "구형 Gameplay v5 값입니다. 신규 데이터는 '교환 승리시(OnExchangeWin)'를 사용하세요.",

        SkillEffectTiming.OnRollFailure =>
            "구형 Gameplay v5 값입니다. 신규 데이터는 '교환 패배시(OnExchangeLose)'를 사용하세요.",

        _ =>
            "현재 정본 Skill Effect Authoring에는 노출하지 않는 내부/호환 타이밍입니다."
    };

    private static bool Contains(
        IReadOnlyList<SkillEffectTiming> values,
        SkillEffectTiming timing)
    {
        if (values == null)
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == timing)
                return true;
        }

        return false;
    }
}
