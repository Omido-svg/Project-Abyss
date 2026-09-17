/// <summary>
/// 0917 canonical Yujin skill ids.
/// Phase-D/0916 constant names remain as aliases so existing weapon/mark runtime code keeps working.
/// </summary>
public static class YujinSkillIds
{
    // Normal 9
    public const string BackgroundCheck = "yujin.normal.background_check";
    public const string CatchBreath = "yujin.normal.catch_breath";
    public const string LedgerCleanup = "yujin.normal.ledger_cleanup";
    public const string StepBack = "yujin.normal.step_back";
    public const string Aim = "yujin.normal.aim";
    public const string ConfirmKill = "yujin.normal.confirm_kill";
    public const string FamiliarHand = "yujin.normal.familiar_hand";
    public const string AceInTheHole = "yujin.normal.ace_in_the_hole";
    public const string Wanted = "yujin.normal.wanted";

    // Duel 16 (A~P)
    public const string ContractTarget = "yujin.duel.contract_target";
    public const string BrutalFinish = "yujin.duel.brutal_finish";
    public const string DesignateTarget = "yujin.duel.designate_target";
    public const string IntoShadows = "yujin.duel.into_shadows";
    public const string ShakeUp = "yujin.duel.shake_up";
    public const string CutThroat = "yujin.duel.cut_throat";
    public const string SpreadRumor = "yujin.duel.spread_rumor";
    public const string ProbeWeakness = "yujin.duel.probe_weakness";
    public const string BlockRetreat = "yujin.duel.block_retreat";
    public const string BorrowWeapon = "yujin.duel.borrow_weapon";
    public const string Trap = "yujin.duel.trap";
    public const string Deadline = "yujin.duel.deadline";
    public const string AllTargets = "yujin.duel.all_targets";
    public const string Period = "yujin.duel.period";

    // 0917 신규 O / P
    public const string AdvanceTiming = "yujin.duel.o.advance_timing"; // 때를 앞당기다
    public const string FinishIt = "yujin.duel.p.finish_it";           // 끝장을 보다

    // Preparation 2 common + 7 Yujin
    public const string Crouch = "common.preparation.crouch";
    public const string Glare = "common.preparation.glare";
    public const string Read = "yujin.preparation.read";                 // 노림수
    public const string AdvancePay = "yujin.preparation.advance_pay";   // 선금 받기
    public const string HwanhyeongAttack = "yujin.preparation.hwanhyeong.attack";
    public const string HwanhyeongDefense = "yujin.preparation.hwanhyeong.defense";
    public const string WaitChance = "yujin.preparation.wait_chance";
    public const string HoldBreath = "yujin.preparation.hold_breath";
    public const string Sharpen = "yujin.preparation.sharpen";

    // Prestige 3
    public const string PrepWork = "yujin.prestige.prep_work";
    public const string CertainExecution = "yujin.prestige.certain_execution";
    public const string FlexibleResponse = "yujin.prestige.flexible_response";

    // Legacy source aliases.
    public const string Inspection = BackgroundCheck;
    public const string Breakfast = CatchBreath;
    public const string Inscription = ContractTarget;
    public const string Pursuit = BrutalFinish;
    public const string Capture = Read;
    public const string Sentencing = AdvancePay;
    public const string Brand = PrepWork;
    public const string JointLiability = CertainExecution;
    public const string Retrial = FlexibleResponse;

    // P0 migration aliases. Old assets may still contain these ids; closure migration rewrites them.
    public const string HwanhyeongBaeku = "yujin.preparation.hwanhyeong.baeku";
    public const string HwanhyeongJeokseol = "yujin.preparation.hwanhyeong.jeokseol";
    public const string HwanhyeongNakil = "yujin.preparation.hwanhyeong.nakil";

    public static readonly string[] CanonicalNormal =
    {
        BackgroundCheck, CatchBreath, LedgerCleanup, StepBack, Aim,
        ConfirmKill, FamiliarHand, AceInTheHole, Wanted
    };

    public static readonly string[] CanonicalDuel =
    {
        ContractTarget, BrutalFinish, DesignateTarget, IntoShadows, ShakeUp,
        CutThroat, SpreadRumor, ProbeWeakness, BlockRetreat, BorrowWeapon,
        Trap, Deadline, AllTargets, Period, AdvanceTiming, FinishIt
    };

    public static readonly string[] CanonicalPreparation =
    {
        Crouch, Glare, Read, AdvancePay, HwanhyeongAttack, HwanhyeongDefense,
        WaitChance, HoldBreath, Sharpen
    };

    public static readonly string[] CanonicalPrestige =
    {
        PrepWork, CertainExecution, FlexibleResponse
    };
}
