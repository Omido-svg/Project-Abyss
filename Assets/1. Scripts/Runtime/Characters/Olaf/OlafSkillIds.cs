/// <summary>
/// 0916(3) canonical Olaf skill ids.
/// Legacy constant names are retained as aliases so Phase-D mechanics/tests keep compiling,
/// but all aliases resolve to the current canonical ids.
/// </summary>
public static class OlafSkillIds
{
    // Normal 16
    public const string Bite = "olaf.normal.bite";
    public const string ShieldWall = "olaf.normal.shield_wall";
    public const string Split = "olaf.normal.split";
    public const string Endure = "olaf.normal.endure";
    public const string Smash = "olaf.normal.smash";
    public const string Parry = "olaf.normal.parry";
    public const string Flurry = "olaf.normal.flurry";
    public const string RiseAgain = "olaf.normal.rise_again";
    public const string DesperateBlow = "olaf.normal.desperate_blow";
    public const string Cover = "olaf.normal.cover";
    public const string ExploitGap = "olaf.normal.exploit_gap";
    public const string FirstStrike = "olaf.normal.first_strike";
    public const string AimWeakness = "olaf.normal.aim_weakness";
    public const string Choke = "olaf.normal.choke";
    public const string BlockWay = "olaf.normal.block_way";
    public const string SliceThin = "olaf.normal.slice_thin";

    // Duel 21
    public const string Standard = "olaf.duel.standard";
    public const string Tenacious = "olaf.duel.tenacious";
    public const string SingleCut = "olaf.duel.single_cut";
    public const string MadBite = "olaf.duel.mad_bite";
    public const string BloodCharge = "olaf.duel.blood_charge";
    public const string HoldOn = "olaf.duel.hold_on";
    public const string SelfCut = "olaf.duel.self_cut";
    public const string NoRetreat = "olaf.duel.no_retreat";
    public const string HeadOn = "olaf.duel.head_on";
    public const string StokePrestige = "olaf.duel.stoke_prestige";
    public const string Fortify = "olaf.duel.fortify";
    public const string ProtectSelf = "olaf.duel.protect_self";
    public const string CatchOffGuard = "olaf.duel.catch_off_guard";
    public const string AllIn = "olaf.duel.all_in";
    public const string BloodPrice = "olaf.duel.blood_price";
    public const string AbsorbBlood = "olaf.duel.absorb_blood";
    public const string WhateverComes = "olaf.duel.whatever_comes";
    public const string HonorableFight = "olaf.duel.honorable_fight";
    public const string BloodOathDuel = "olaf.duel.blood_oath";
    public const string PaybackAll = "olaf.duel.payback_all";
    public const string SingleCombat = "olaf.duel.single_combat";

    // Preparation 2 common + 7 Olaf
    public const string Crouch = "common.preparation.crouch";
    public const string Glare = "common.preparation.glare";
    public const string Bloto = "olaf.preparation.bloto";
    public const string BloodOathPreparation = "olaf.preparation.blood_oath";
    public const string BreathBoughtWithBlood = "olaf.preparation.breath_bought_with_blood";
    public const string CalmMadness = "olaf.preparation.calm_madness";
    public const string EraseWound = "olaf.preparation.erase_wound";
    public const string DevourStagger = "olaf.preparation.devour_stagger";
    public const string LastResistance = "olaf.preparation.last_resistance";

    // Prestige 3
    public const string RuneCarving = "olaf.prestige.rune_carving";
    public const string Berserk = "olaf.prestige.berserk";
    public const string Ragnarok = "olaf.prestige.ragnarok";

    // Phase-D source aliases. Do not author new data with these semantic names.
    public const string EnduringSlash = Bite;
    public const string OverheadSmash = ShieldWall;
    public const string WildHack = Split;
    public const string Rend = Tenacious;
    public const string BloomingWound = RuneCarving;
    public const string BurstingMadness = Berserk;
    public const string BacksToWall = Ragnarok;

    public static readonly string[] CanonicalNormal =
    {
        Bite, ShieldWall, Split, Endure, Smash, Parry, Flurry, RiseAgain,
        DesperateBlow, Cover, ExploitGap, FirstStrike, AimWeakness, Choke,
        BlockWay, SliceThin
    };

    public static readonly string[] CanonicalDuel =
    {
        Standard, Tenacious, SingleCut, MadBite, BloodCharge, HoldOn,
        SelfCut, NoRetreat, HeadOn, StokePrestige, Fortify, ProtectSelf,
        CatchOffGuard, AllIn, BloodPrice, AbsorbBlood, WhateverComes,
        HonorableFight, BloodOathDuel, PaybackAll, SingleCombat
    };

    public static readonly string[] CanonicalPreparation =
    {
        Crouch, Glare, Bloto, BloodOathPreparation, BreathBoughtWithBlood,
        CalmMadness, EraseWound, DevourStagger, LastResistance
    };

    public static readonly string[] CanonicalPrestige =
    {
        RuneCarving, Berserk, Ragnarok
    };
}
