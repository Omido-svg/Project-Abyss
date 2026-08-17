public static class HifumiSkillIds
{
    public const string SmallChange = "hifumi.normal.small_change";       // 푼돈 걸기
    public const string BoldJudgment = "hifumi.normal.bold_judgment";     // 과감한 판단

    public const string Yukcham = "hifumi.duel.yukcham";                 // 육참
    public const string Goldan = "hifumi.duel.goldan";                   // 골단
    public const string RecklessBet = "hifumi.duel.reckless_bet";        // 무모한 베팅

    public const string PokerFace = "hifumi.preparation.poker_face";
    public const string EngraveBone = "hifumi.preparation.engrave_bone";
    public const string GiveFlesh = "hifumi.preparation.give_flesh";
    public const string FoldHand = "hifumi.preparation.fold_hand";

    public const string GamblerMove = "hifumi.prestige.gambler_move";
    public const string AllIn = "hifumi.prestige.all_in";
    public const string Trick = "hifumi.prestige.trick";

    public static bool IsCounterEnabled(string skillId) =>
        skillId == Yukcham || skillId == Goldan;
}
