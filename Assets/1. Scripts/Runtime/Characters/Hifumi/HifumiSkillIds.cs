public static class HifumiSkillIds
{
    // Normal 3
    public const string SmallChange = "hifumi.normal.small_change";
    public const string BoldJudgment = "hifumi.normal.bold_judgment";
    public const string MutualDestruction = "hifumi.normal.mutual_destruction";

    // 0922 canonical Duel 11: A/B/C/E/G/H/I/J/K/L/M.
    public const string Yukcham = "hifumi.duel.yukcham";                    // A 육참
    public const string Goldan = "hifumi.duel.goldan";                      // B 골단
    public const string RecklessBet = "hifumi.duel.reckless_bet";           // C 무모한 베팅
    public const string RaiseStake = "hifumi.duel.e.raise_stake";            // E 판돈을 올리다
    public const string DoubleOrNothing = "hifumi.duel.g.double_or_nothing"; // G 곱절을 노리다
    public const string ShakeFate = "hifumi.duel.h.shake_fate";              // H 운명을 흔들다
    public const string EngraveBody = "hifumi.duel.i.engrave_body";          // I 몸에 새기다
    public const string RollCompendium = "hifumi.duel.j.roll_compendium";    // J 굴림 도감
    public const string FlipTable = "hifumi.duel.k.flip_table";              // K 판을 엎다
    public const string Provoke = "hifumi.duel.l.provoke";                   // L 도발형
    public const string CleanseAll = "hifumi.duel.m.cleanse_all";            // M 전체 정화형

    // Pre-0922 IDs are intentionally retained only for source/save compatibility.
    // They must not appear in the 0922 canonical pool.
    public const string RiskLife = "hifumi.duel.d.risk_life";
    public const string RecoverPrincipal = "hifumi.duel.f.recover_principal";

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
        RaiseStake,
        DoubleOrNothing,
        ShakeFate,
        EngraveBody,
        RollCompendium,
        FlipTable,
        Provoke,
        CleanseAll
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
        skillId == Goldan ||
        skillId == RollCompendium;

    public static bool IsNormalCounterSkill(string skillId) =>
        skillId == SmallChange;

    public static bool IsLegacyRemovedDuel(string skillId) =>
        skillId == RiskLife ||
        skillId == RecoverPrincipal;

    // K/L/M have confirmed structural slots but still contain unresolved canonical
    // numbers. Phase 8 installs them into the pool while keeping runtime activation
    // guarded until their data is resolved.
    public static bool HasPendingCanonicalRuntimeNumbers(string skillId) =>
        skillId == FlipTable ||
        skillId == Provoke ||
        skillId == CleanseAll;

    public static bool IsCounterEnabled(string skillId) =>
        IsCounterSkill(skillId);
}
