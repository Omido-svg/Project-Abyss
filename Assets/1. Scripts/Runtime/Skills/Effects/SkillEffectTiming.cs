public enum SkillEffectTiming
{
    // ---------------------------------------------------------------------
    // Legacy values: 절대 숫자를 바꾸지 않는다.
    // 기존 ScriptableObject 직렬화 호환을 위해 0~15를 그대로 유지한다.
    // ---------------------------------------------------------------------
    OnExecute = 0,
    OnClashWin = 1,
    OnClashLose = 2,
    AfterDamage = 3,
    OnCritical = 4,
    OnKill = 5,
    OnActionEnd = 6,
    OnExchangeWin = 7,
    OnExchangeLose = 8,
    OnOneSideHit = 9,
    OnClashDraw = 10,
    OnMultiRollPenaltyStart = 11,
    OnMultiRollPenaltyAfterRoll = 12,
    OnMultiRollPenaltyEnd = 13,
    OnRollWin = 14,
    OnRollLose = 15,

    // ---------------------------------------------------------------------
    // Gameplay v5 detailed effect phases.
    // 문서의 "코인" 개념을 Project Abyss의 공용 "굴림"으로 일반화한다.
    // ---------------------------------------------------------------------
    OnBattleStart = 16,
    OnTurnStart = 17,
    BeforeUse = 18,
    OnClashStart = 19,
    OnOneSidedStart = 20,
    BeforeAttack = 21,
    OnRollStart = 22,
    OnRollSuccess = 23,
    OnRollFailure = 24,
    OnHit = 25,
    OnRollEnd = 26,
    OnAttackEnd = 27,
    OnSkillEnd = 28,
    OnTurnEnd = 29,
    OnBattleEnd = 30
}

/// <summary>
/// SkillEffectTiming의 Gameplay v5 의미를 한 곳에서 관리한다.
/// UI와 Authoring이 동일한 용어를 사용하도록 하는 표시 전용 카탈로그다.
/// </summary>
public static class SkillEffectTimingCatalog
{
    public static string GetDisplayName(SkillEffectTiming timing) => timing switch
    {
        SkillEffectTiming.OnBattleStart => "전투 시작시",
        SkillEffectTiming.OnTurnStart => "턴 시작시",
        SkillEffectTiming.BeforeUse => "사용 전",
        SkillEffectTiming.OnExecute => "사용시",
        SkillEffectTiming.OnClashStart => "합 진행 시작시",
        SkillEffectTiming.OnOneSidedStart => "일방 공격 시작시",
        SkillEffectTiming.BeforeAttack => "공격 시작 전",
        SkillEffectTiming.OnRollStart => "굴림 시작시",
        SkillEffectTiming.OnRollSuccess => "굴림 성공시",
        SkillEffectTiming.OnRollFailure => "굴림 실패시",
        SkillEffectTiming.OnHit => "적중시",
        SkillEffectTiming.AfterDamage => "피해 적용 후",
        SkillEffectTiming.OnCritical => "크리티컬시",
        SkillEffectTiming.OnKill => "대상 처치시",
        SkillEffectTiming.OnRollEnd => "굴림 종료시",
        SkillEffectTiming.OnAttackEnd => "공격 종료시",
        SkillEffectTiming.OnSkillEnd => "스킬 종료시",
        SkillEffectTiming.OnTurnEnd => "턴 종료시",
        SkillEffectTiming.OnBattleEnd => "전투 종료시",

        // Gameplay v5에는 "전체 합 다수결 승자"가 없다.
        // 아래 값은 기존 에셋 호환/구형 캐릭터 코드용으로 남긴다.
        SkillEffectTiming.OnClashWin => "합 승리시 (구형)",
        SkillEffectTiming.OnClashLose => "합 패배시 (구형)",
        SkillEffectTiming.OnExchangeWin => "교환 승리시 (구형)",
        SkillEffectTiming.OnExchangeLose => "교환 패배시 (구형)",
        SkillEffectTiming.OnOneSideHit => "일방 적중시 (구형)",
        SkillEffectTiming.OnClashDraw => "교환 무승부시",
        SkillEffectTiming.OnRollWin => "굴림 승리시 (구형)",
        SkillEffectTiming.OnRollLose => "굴림 패배시 (구형)",
        SkillEffectTiming.OnActionEnd => "행동 종료시 (구형)",
        SkillEffectTiming.OnMultiRollPenaltyStart => "다굴림 페널티 시작",
        SkillEffectTiming.OnMultiRollPenaltyAfterRoll => "다굴림 페널티 굴림 후",
        SkillEffectTiming.OnMultiRollPenaltyEnd => "다굴림 페널티 종료",
        _ => timing.ToString()
    };

    public static string GetColorHex(SkillEffectTiming timing)
    {
        // 발동 시점은 효과의 이로운/해로운 성질과 별개의 정보이므로
        // 문서 예시처럼 초록 계열로 통일한다.
        return "#73D673";
    }

    public static string GetRichTextLabel(SkillEffectTiming timing)
    {
        return
            $"<color={GetColorHex(timing)}><b>[{GetDisplayName(timing)}]</b></color>";
    }

    public static bool IsGameplayV5AuthoringTiming(SkillEffectTiming timing) => timing switch
    {
        SkillEffectTiming.OnBattleStart => true,
        SkillEffectTiming.OnTurnStart => true,
        SkillEffectTiming.BeforeUse => true,
        SkillEffectTiming.OnExecute => true,
        SkillEffectTiming.OnClashStart => true,
        SkillEffectTiming.OnOneSidedStart => true,
        SkillEffectTiming.BeforeAttack => true,
        SkillEffectTiming.OnRollStart => true,
        SkillEffectTiming.OnRollSuccess => true,
        SkillEffectTiming.OnRollFailure => true,
        SkillEffectTiming.OnHit => true,
        SkillEffectTiming.AfterDamage => true,
        SkillEffectTiming.OnCritical => true,
        SkillEffectTiming.OnKill => true,
        SkillEffectTiming.OnRollEnd => true,
        SkillEffectTiming.OnAttackEnd => true,
        SkillEffectTiming.OnSkillEnd => true,
        SkillEffectTiming.OnTurnEnd => true,
        SkillEffectTiming.OnBattleEnd => true,
        _ => false
    };

    public static string GetSemantics(SkillEffectTiming timing) => timing switch
    {
        SkillEffectTiming.OnRollSuccess =>
            "합에서는 해당 교환 승리, 일방 공격에서는 유효한 Attack/Stagger 굴림 성립을 성공으로 봅니다.",
        SkillEffectTiming.OnRollFailure =>
            "합에서는 해당 교환 패배, 일방 공격에서는 피해/흐트러짐 공격으로 성립하지 못한 굴림을 실패로 봅니다.",
        SkillEffectTiming.OnHit =>
            "HP 피해 또는 흐트러짐 피해가 실제 전투 결과로 성립한 직후입니다.",
        SkillEffectTiming.OnClashWin =>
            "Gameplay v5 표준 규칙에는 전체 합 승자가 없으므로 신규 데이터에는 사용하지 않습니다.",
        SkillEffectTiming.OnClashLose =>
            "Gameplay v5 표준 규칙에는 전체 합 패자가 없으므로 신규 데이터에는 사용하지 않습니다.",
        _ => string.Empty
    };
}
