public static class HifumiSkillIds
{
    // Normal 3
    public const string SmallChange = "hifumi.normal.small_change";       // 푼돈 걸기
    public const string BoldJudgment = "hifumi.normal.bold_judgment";     // 과감한 판단
    public const string MutualDestruction = "hifumi.normal.mutual_destruction"; // 동귀어진

    // Duel 9 (A~I)
    public const string Yukcham = "hifumi.duel.yukcham";                 // A 육참
    public const string Goldan = "hifumi.duel.goldan";                   // B 골단
    public const string RecklessBet = "hifumi.duel.reckless_bet";        // C 무모한 베팅
    public const string RiskLife = "hifumi.duel.d.risk_life";             // D 명줄을 걸다
    public const string RaiseStake = "hifumi.duel.e.raise_stake";         // E 판돈을 올리다
    public const string RecoverPrincipal = "hifumi.duel.f.recover_principal"; // F 본전을 챙기다
    public const string DoubleOrNothing = "hifumi.duel.g.double_or_nothing"; // G 곱절을 노리다 (임시명)
    public const string ShakeFate = "hifumi.duel.h.shake_fate";           // H 운명을 흔들다 (임시명)
    public const string EngraveBody = "hifumi.duel.i.engrave_body";       // I 몸에 새기다 (임시명)

    // Preparation 4
    public const string PokerFace = "hifumi.preparation.poker_face";
    public const string EngraveBone = "hifumi.preparation.engrave_bone";
    public const string GiveFlesh = "hifumi.preparation.give_flesh";
    public const string FoldHand = "hifumi.preparation.fold_hand";

    // Prestige 3
    public const string GamblerMove = "hifumi.prestige.gambler_move";
    public const string AllIn = "hifumi.prestige.all_in";
    public const string Trick = "hifumi.prestige.trick";

    public static readonly string[] CanonicalNormal =
    {
        SmallChange,
        BoldJudgment,
        MutualDestruction
    };

    public static readonly string[] CanonicalDuel =
    {
        Yukcham,
        Goldan,
        RecklessBet,
        RiskLife,
        RaiseStake,
        RecoverPrincipal,
        DoubleOrNothing,
        ShakeFate,
        EngraveBody
    };

    public static readonly string[] CanonicalPreparation =
    {
        PokerFace,
        EngraveBone,
        GiveFlesh,
        FoldHand
    };

    public static readonly string[] CanonicalPrestige =
    {
        GamblerMove,
        AllIn,
        Trick
    };

    public static bool IsCounterSkill(string skillId) =>
        IsDuelCounterSkill(skillId) ||
        IsNormalCounterSkill(skillId);

    public static bool IsDuelCounterSkill(string skillId) =>
        skillId == Yukcham ||
        skillId == Goldan;

    public static bool IsNormalCounterSkill(string skillId) =>
        skillId == SmallChange;

    // Legacy call sites kept source-compatible while Phase D migrates to the explicit split.
    public static bool IsCounterEnabled(string skillId) =>
        IsCounterSkill(skillId);
}
