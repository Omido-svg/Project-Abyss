using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 0915 Phase B — Damage / Resource 18개 Requirement gate.
/// 각 IsolatedRuntime case는 Runner가 새 Fixture로 실행한다.
/// </summary>
public sealed class PhaseBGameSystemVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_b.damage_resource";
    public int Order => 300;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Runtime("phaseb.c09.continuation", "C-09", "파괴·처치 후 남은 굴림 소멸", GameSystemVerificationCategory.Pairing,
            "이번 Action이 primary target을 파괴/처치하면 남은 굴림=0; 시작부터 파괴된 부위 조준은 허용", VerifyContinuation);
        yield return Runtime("phaseb.c12.whole_hp_full_request", "C-12", "부위 clamp와 Whole HP 분리", GameSystemVerificationCategory.Damage,
            "Part 적용량과 무관하게 Whole HP에는 요청 피해 전량", VerifyWholeHpFullRequest);
        yield return Runtime("phaseb.c13.weakened_rehit", "C-13", "약화 부위 재타격 Whole HP", GameSystemVerificationCategory.Damage,
            "Weakened PartHP=1 유지 + Whole HP 감소", VerifyWeakenedRehit);
        yield return Runtime("phaseb.c15.status_anchor", "C-15", "Single HP 상태 Anchor", GameSystemVerificationCategory.Status,
            "부위 없는 대상은 Character anchor / weaken·break=false", VerifyStatusAnchor);
        yield return Runtime("phaseb.c17.broken_head_energy", "C-17", "머리 파괴 최대 빛 -1", GameSystemVerificationCategory.Resource,
            "BrokenHead 적용 -1 / 제거 +1 복원", VerifyBrokenHeadEnergy);
        yield return Runtime("phaseb.c18.kill_weaken_energy_reward", "C-18", "약화·처치 빛 보상", GameSystemVerificationCategory.Resource,
            "플레이어가 적 부위 약화 +1, 적 처치 +1", VerifyEnergyRewards);
        yield return Runtime("phaseb.c19.turn_end_reward", "C-19", "짓누름/짓눌림 다음 턴 보상", GameSystemVerificationCategory.Momentum,
            "Overwhelm→next Light+1 / LastStand→next Judgment+1", VerifyMomentumTurnRewards);
        yield return Runtime("phaseb.c20.no_laststand_multiplier", "C-20", "구 발악 배수·히후미 반감 제거", GameSystemVerificationCategory.Momentum,
            "LastStand에서도 HitShift=20 / Hifumi damage·bone=1:1", VerifyNoLegacyLastStandMultiplier);
        yield return Static("phaseb.c21.hifumi_fervor", "C-21", "히후미 열세·짓눌림 고조 +3", GameSystemVerificationCategory.Fervor,
            "Disadvantage/LastStand gain=3", VerifyHifumiFervor);
        yield return Runtime("phaseb.c22.prestige_events", "C-22", "위세 사건별 +1/+1/+2/+5", GameSystemVerificationCategory.Prestige,
            "ClashStart1 + Exchange1 + ClashWin2 + Kill5", VerifyPrestigeEvents);
        yield return Runtime("phaseb.c23.armor_lifetime", "C-23", "방어도 전투 지속 누적", GameSystemVerificationCategory.Resource,
            "TurnEnd 유지 / BattleEnd 0", VerifyArmorLifetime);
        yield return Runtime("phaseb.c24.crouch_additive", "C-24", "웅크리기 방어도 +12 가산", GameSystemVerificationCategory.Resource,
            "10 + 12 = 22", VerifyCrouchAdditive);
        yield return Runtime("phaseb.c25.damage_min_before_armor", "C-25", "최종 피해 min1 후 armor", GameSystemVerificationCategory.Damage,
            "flat으로 0 이하가 되어도 armor 전 1, armor가 그 1을 흡수 가능", VerifyDamageMinimumBeforeArmor);
        yield return Static("phaseb.c27.common_status_syntax", "C-27", "공용 상태 0915 문법", GameSystemVerificationCategory.Status,
            "Strength/Weakness 전 굴림 적용, Sturdy/Disarm 신규 효과 비활성", VerifyCommonStatusSyntax);
        yield return Runtime("phaseb.c49.opposite_status_algebra", "C-49", "반대 상태 대수 상쇄", GameSystemVerificationCategory.Status,
            "Strength3 + Weakness2 => Strength1", VerifyOppositeStatusAlgebra);
        yield return Static("phaseb.c48.regeneration_dimensions", "C-48", "재생 duration/heal/channel 분리", GameSystemVerificationCategory.Status,
            "3턴 HP+8 / 3턴 Stagger+5를 독립 표현", VerifyRegenerationDimensions);
        yield return Static("phaseb.c41.elite_stagger_unset", "C-41", "Elite Stagger 미정=Unset", GameSystemVerificationCategory.Data,
            "Elite default maximum=0(Unset), 임의 100 금지", VerifyEliteStaggerUnset);
        yield return Static("phaseb.c43.normal_enemy_d8", "C-43", "일반 몹 D8 fallback", GameSystemVerificationCategory.Data,
            "미지정 dice=1..8", VerifyNormalEnemyD8);
    }

    private static GameSystemVerificationCase Runtime(string id, string req, string name,
        GameSystemVerificationCategory category, string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category, GameSystemVerificationExecutionMode.IsolatedRuntime, expected, probe, true);

    private static GameSystemVerificationCase Static(string id, string req, string name,
        GameSystemVerificationCategory category, string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category, GameSystemVerificationExecutionMode.StaticContract, expected, probe, true);

    private static bool Fixture(GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture, out GameSystemVerificationProbeResult failure)
    {
        if (context.TryGetFixture(out fixture, out string reason))
        {
            failure = null;
            return true;
        }
        failure = GameSystemVerificationProbeResult.Fail(reason);
        return false;
    }

    private static BodyPart FirstPart(Character character) =>
        character?.BodyParts?.FirstOrDefault(part => part != null);

    private static Skill FirstSkill(Character character) =>
        character?.RuntimeSkills?.FirstOrDefault(skill => skill != null);

    private static GameSystemVerificationProbeResult VerifyContinuation(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        BodyPart targetPart = FirstPart(f.Player);
        Skill sourceSkill = FirstSkill(f.Enemy) ?? FirstSkill(f.Player);
        if (targetPart == null || sourceSkill == null)
            return GameSystemVerificationProbeResult.Fail("fixture part/skill missing");

        // 1.5 계약: 이미 파괴된 부위를 새 Action이 조준하는 것은 허용한다.
        targetPart.SetDebugState(0, Mathf.Max(1, targetPart.MaxPartHP), false, true);
        BattleAction action = new()
        {
            Slot = new ActionSlot
            {
                Owner = f.Enemy,
                Skill = sourceSkill,
                TargetCharacter = f.Player,
                TargetPart = targetPart
            }
        };
        action.BeginResolutionSequence();
        bool preBrokenCanStart = ClashContinuationPolicy.CanContinue(action);

        // C-09: 이 Action의 앞선 타격이 같은 primary target/part를 방금 파괴한 경우만 latch한다.
        DamageContext breakContext = new()
        {
            Target = f.Player,
            TargetPart = targetPart,
            WasBroken = true
        };
        action.SetDamageContext(breakContext);
        bool afterOwnBreak = ClashContinuationPolicy.CanContinue(action);

        bool ok = preBrokenCanStart && !afterOwnBreak;
        return ok
            ? GameSystemVerificationProbeResult.Pass($"PreBrokenStart={preBrokenCanStart}, AfterOwnBreak={afterOwnBreak}")
            : GameSystemVerificationProbeResult.Fail($"PreBrokenStart={preBrokenCanStart}, AfterOwnBreak={afterOwnBreak}");
    }

    private static GameSystemVerificationProbeResult VerifyWholeHpFullRequest(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        BodyPart part = FirstPart(f.Player);
        if (part == null) return GameSystemVerificationProbeResult.Fail("Player part missing");
        f.Player.RuntimeStatus.currentHP = f.Player.MaxCombatHP;
        part.SetDebugState(2, Mathf.Max(2, part.MaxPartHP), false, false);
        int beforeHp = f.Player.CurrentHP;
        f.Player.TakeDamage(part, 5, false);
        int whole = beforeHp - f.Player.CurrentHP;
        int partAfter = Mathf.RoundToInt(part.PartHP);
        return whole == 5 && partAfter == 1
            ? GameSystemVerificationProbeResult.Pass($"Whole=-{whole}, Part=2->{partAfter}")
            : GameSystemVerificationProbeResult.Fail($"Whole=-{whole}, PartAfter={partAfter}");
    }

    private static GameSystemVerificationProbeResult VerifyWeakenedRehit(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        BodyPart part = FirstPart(f.Player);
        if (part == null) return GameSystemVerificationProbeResult.Fail("Player part missing");
        f.Player.RuntimeStatus.currentHP = f.Player.MaxCombatHP;
        part.SetDebugState(1, Mathf.Max(2, part.MaxPartHP), true, false);
        int before = f.Player.CurrentHP;
        f.Player.TakeDamage(part, 5, false);
        int delta = before - f.Player.CurrentHP;
        return delta == 5 && part.IsWeakened && Mathf.RoundToInt(part.PartHP) == 1
            ? GameSystemVerificationProbeResult.Pass($"Whole=-{delta}, Part={part.PartHP}")
            : GameSystemVerificationProbeResult.Fail($"Whole=-{delta}, Part={part.PartHP}, State={part.State}");
    }

    private static GameSystemVerificationProbeResult VerifyStatusAnchor(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(f.Enemy, null);

        // Fixture source가 이미 상태를 갖고 있어도 C-15 probe가 외부 상태에
        // 영향을 받지 않도록 동일 상태를 먼저 정리한다.
        Bleeding existing = f.Enemy.GetStatus<Bleeding>();
        if (existing != null)
            f.Enemy.RemoveStatus(existing);

        int beforeHp = f.Enemy.CurrentHP;

        f.Enemy.AddStatus(
            new Bleeding(2, damagePerStack: 1),
            f.Player);

        Bleeding bleeding =
            f.Enemy.GetStatus<Bleeding>();

        bool applied =
            bleeding != null;

        int stackBeforeTick =
            bleeding?.Stack ?? -1;

        f.Enemy.TurnEnd();

        Bleeding bleedingAfter =
            f.Enemy.GetStatus<Bleeding>();

        int stackAfterTick =
            bleedingAfter?.Stack ?? 0;

        int hpDamage =
            beforeHp - f.Enemy.CurrentHP;

        bool ok =
            anchor.Character == f.Enemy &&
            !anchor.IsPartAnchor &&
            !anchor.IsWeakened &&
            !anchor.IsBroken &&
            anchor.StatusDamageType == DamageType.Direct &&
            applied &&
            stackBeforeTick == 2 &&
            hpDamage == 2 &&
            stackAfterTick == 1;

        string actual =
            $"Anchor={(anchor.IsPartAnchor ? "Part" : "Character")}, " +
            $"Route={anchor.StatusDamageType}, " +
            $"Applied={applied}, " +
            $"Stack={stackBeforeTick}->{stackAfterTick}, " +
            $"Tick={hpDamage}";

        return ok
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyBrokenHeadEnergy(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        BodyPart head = f.Player.GetBodyPart(PartType.HEAD);
        if (head == null) return GameSystemVerificationProbeResult.Fail("HEAD missing");
        int before = f.Player.MaxEnergy;
        BrokenHead status = new();
        f.Player.AddStatus(status, f.Player, head);
        int broken = f.Player.MaxEnergy;
        f.TurnManager.EndBattle();
        int restored = f.Player.MaxEnergy;
        return broken == Mathf.Max(0, before - 1) && restored == before
            ? GameSystemVerificationProbeResult.Pass($"MaxEnergy={before}->{broken}->{restored}")
            : GameSystemVerificationProbeResult.Fail($"MaxEnergy={before}->{broken}->{restored}");
    }

    private static GameSystemVerificationProbeResult VerifyEnergyRewards(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        f.Player.TryConsumeEnergy(Mathf.Min(2, f.Player.CurrentEnergy));
        int before = f.Player.CurrentEnergy;

        // NormalEnemy처럼 실제 BodyPart가 없는 적도 event contract 자체는 동일하다.
        BodyPart eventPart = new(PartType.HEAD, 10);
        eventPart.Initialize(f.Enemy);
        eventPart.Weaken();

        f.Context._battleEvent.RaiseBodyPartWeakened(
            BodyPartWeakenEventContext.External(
                f.Player,
                f.Enemy,
                eventPart));

        int afterWeaken = f.Player.CurrentEnergy;

        f.Context._battleEvent.RaiseKill(
            KillEventContext.External(
                f.Player,
                f.Enemy));

        int afterKill = f.Player.CurrentEnergy;
        bool ok =
            afterWeaken == Mathf.Min(f.Player.MaxEnergy, before + 1) &&
            afterKill == Mathf.Min(f.Player.MaxEnergy, afterWeaken + 1);

        return ok
            ? GameSystemVerificationProbeResult.Pass($"Energy={before}->{afterWeaken}->{afterKill}")
            : GameSystemVerificationProbeResult.Fail($"Energy={before}->{afterWeaken}->{afterKill}");
    }

    private static GameSystemVerificationProbeResult VerifyMomentumTurnRewards(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        MomentumManager m = f.MomentumManager;
        m.Reset();
        f.Player.TryConsumeEnergy(Mathf.Min(1, f.Player.CurrentEnergy));
        int before = f.Player.CurrentEnergy;

        m.SetMomentumForDebug(70);
        m.FinalizeTurn();
        m.BeginTurn();
        int afterLight = f.Player.CurrentEnergy;

        // 같은 pending을 BeginTurn 두 번으로 중복 소비하면 안 된다.
        m.BeginTurn();
        int afterSecondBegin = f.Player.CurrentEnergy;

        m.SetMomentumForDebug(-70);
        m.FinalizeTurn();
        m.BeginTurn();
        bool judgment = m.HasLastStandJudgmentBonus(f.Player);

        bool ok =
            afterLight == Mathf.Min(f.Player.MaxEnergy, before + 1) &&
            afterSecondBegin == afterLight &&
            judgment;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Light={before}->{afterLight}->{afterSecondBegin}, Judgment={judgment}")
            : GameSystemVerificationProbeResult.Fail(
                $"Light={before}->{afterLight}->{afterSecondBegin}, Judgment={judgment}");
    }

    private static GameSystemVerificationProbeResult VerifyNoLegacyLastStandMultiplier(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        MomentumManager m = f.MomentumManager;
        m.Reset();
        m.SetMomentumForDebug(-70);
        m.FinalizeTurn();
        m.BeginTurn();
        MomentumShiftResult shift = m.ApplyHit(f.Player);
        HifumiMechanic h = new();
        int damage = h.ResolveIncomingDamageForVerification(10, true, false);
        int bone = h.ResolveBoneGainForVerification(10, true, false, false);
        bool ok = Mathf.Abs(shift.SignedShift) == 20 && damage == 10 && bone == 10;
        return ok ? GameSystemVerificationProbeResult.Pass($"Shift={shift.SignedShift}, Damage={damage}, Bone={bone}")
                  : GameSystemVerificationProbeResult.Fail($"Shift={shift.SignedShift}, Damage={damage}, Bone={bone}");
    }

    private static GameSystemVerificationProbeResult VerifyHifumiFervor(GameSystemVerificationContext _)
    {
        IFervorTurnEndGainModifier h = new HifumiMechanic();
        int d = h.ModifyTurnEndFervorGain(MomentumState.Disadvantage, 0);
        int l = h.ModifyTurnEndFervorGain(MomentumState.LastStand, 0);
        return d == 3 && l == 3 ? GameSystemVerificationProbeResult.Pass($"Disadvantage={d}, LastStand={l}")
                                : GameSystemVerificationProbeResult.Fail($"Disadvantage={d}, LastStand={l}");
    }

    private static GameSystemVerificationProbeResult VerifyPrestigeEvents(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        PrestigeChargeService p = f.Context.Services?.PrestigeChargeService;
        if (p == null) return GameSystemVerificationProbeResult.Fail("PrestigeChargeService missing");
        f.Player.RuntimeStatus.currentPrestige = 0;
        int a = p.ChargeClashStart(f.Player, f.Enemy, null);
        int b = p.ChargeExchangeParticipant(f.Player, f.Enemy, null);
        int c = p.ChargeClashWinner(f.Player, f.Enemy, null);
        int d = p.ChargeKill(f.Player, f.Enemy, null);
        bool ok = a == 1 && b == 1 && c == 2 && d == 5 && f.Player.RuntimeStatus.currentPrestige == 9;
        return ok ? GameSystemVerificationProbeResult.Pass($"{a}+{b}+{c}+{d}={f.Player.RuntimeStatus.currentPrestige}")
                  : GameSystemVerificationProbeResult.Fail($"{a}+{b}+{c}+{d}, total={f.Player.RuntimeStatus.currentPrestige}");
    }

    private static GameSystemVerificationProbeResult VerifyArmorLifetime(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        f.Player.ClearBlock();
        f.Player.AddBlock(10);
        f.Player.TurnEnd();
        int afterTurn = f.Player.RuntimeStatus.currentBlock;
        f.TurnManager.EndBattle();
        int afterBattle = f.Player.RuntimeStatus.currentBlock;
        return afterTurn == 10 && afterBattle == 0
            ? GameSystemVerificationProbeResult.Pass($"Block=10->{afterTurn}->{afterBattle}")
            : GameSystemVerificationProbeResult.Fail($"Block=10->{afterTurn}->{afterBattle}");
    }

    private static GameSystemVerificationProbeResult VerifyCrouchAdditive(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        f.Player.ClearBlock();
        f.Player.AddBlock(10);
        f.Player.AddBlock(OlafMadnessMechanic.CrouchBlockGain);
        int actual = f.Player.RuntimeStatus.currentBlock;
        return OlafMadnessMechanic.CrouchBlockGain == 12 && actual == 22
            ? GameSystemVerificationProbeResult.Pass($"10+{OlafMadnessMechanic.CrouchBlockGain}={actual}")
            : GameSystemVerificationProbeResult.Fail($"10+{OlafMadnessMechanic.CrouchBlockGain}={actual}");
    }

    private static GameSystemVerificationProbeResult VerifyDamageMinimumBeforeArmor(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        f.Player.RuntimeStatus.currentHP = Mathf.Max(10, f.Player.MaxCombatHP);
        f.Player.AddStatus(new ProtectionStatus(4), f.Enemy);
        DamageContext first = f.DamageManager.ApplyDamageContext(DamageRequest.Direct(f.Enemy, f.Player, 1));
        if (first == null) return GameSystemVerificationProbeResult.Fail("DamageContext null");
        f.Player.AddBlock(1);
        DamageContext second = f.DamageManager.ApplyDamageContext(DamageRequest.Direct(f.Enemy, f.Player, 1));
        if (second == null) return GameSystemVerificationProbeResult.Fail("Second DamageContext null");
        bool ok = first.FinalDamage == 1 && second.TargetModifiedDamage == 1 && second.GuardAbsorbed == 1 && second.FinalDamage == 0;
        return ok ? GameSystemVerificationProbeResult.Pass($"NoArmor={first.FinalDamage}, PreArmor={second.TargetModifiedDamage}, Absorb={second.GuardAbsorbed}, Final={second.FinalDamage}")
                  : GameSystemVerificationProbeResult.Fail($"NoArmor={first.FinalDamage}, PreArmor={second.TargetModifiedDamage}, Absorb={second.GuardAbsorbed}, Final={second.FinalDamage}");
    }

    private static GameSystemVerificationProbeResult VerifyCommonStatusSyntax(GameSystemVerificationContext _)
    {
        BattleAction action = new() { Slot = new ActionSlot() };
        int strength = new StrengthStatus(2).GetRollShift(action);
        int weakness = new WeaknessStatus(2).GetRollShift(action);
        bool tbdDisabled = StatusEffectFactory.Create(StatusEffectId.Sturdy, 1, 1) == null &&
                           StatusEffectFactory.Create(StatusEffectId.Disarm, 1, 1) == null;
        bool ok = strength == 2 && weakness == -2 && tbdDisabled;
        return ok ? GameSystemVerificationProbeResult.Pass($"Strength={strength}, Weakness={weakness}, TBDDisabled={tbdDisabled}")
                  : GameSystemVerificationProbeResult.Fail($"Strength={strength}, Weakness={weakness}, TBDDisabled={tbdDisabled}");
    }

    private static GameSystemVerificationProbeResult VerifyOppositeStatusAlgebra(GameSystemVerificationContext context)
    {
        if (!Fixture(context, out var f, out var fail)) return fail;
        f.Player.AddStatus(new StrengthStatus(3), f.Player);
        f.Player.AddStatus(new WeaknessStatus(2), f.Enemy);
        StrengthStatus strength = f.Player.StatusEffects.OfType<StrengthStatus>().FirstOrDefault();
        WeaknessStatus weakness = f.Player.StatusEffects.OfType<WeaknessStatus>().FirstOrDefault();
        bool ok = strength?.Stack == 1 && weakness == null;
        return ok ? GameSystemVerificationProbeResult.Pass($"Strength={strength?.Stack}, Weakness={(weakness == null ? 0 : weakness.Stack)}")
                  : GameSystemVerificationProbeResult.Fail($"Strength={strength?.Stack}, Weakness={weakness?.Stack}");
    }

    private static GameSystemVerificationProbeResult VerifyRegenerationDimensions(GameSystemVerificationContext _)
    {
        RegenerationStatus hp = new(3, 8, RegenerationRecoveryChannel.HitPoints);
        RegenerationStatus stagger = new(3, 5, RegenerationRecoveryChannel.Stagger);
        bool ok = hp.Duration == 3 && hp.Stack == 3 && hp.HealAmount == 8 && hp.Channel == RegenerationRecoveryChannel.HitPoints &&
                  stagger.Duration == 3 && stagger.Stack == 3 && stagger.HealAmount == 5 && stagger.Channel == RegenerationRecoveryChannel.Stagger;
        return ok ? GameSystemVerificationProbeResult.Pass("HP:3x8 / Stagger:3x5")
                  : GameSystemVerificationProbeResult.Fail("Regeneration dimensions mismatch");
    }

    private static GameSystemVerificationProbeResult VerifyEliteStaggerUnset(GameSystemVerificationContext _)
    {
        StaggerRuleSettings settings = new();
        settings.Normalize();
        bool ok = settings.EliteEnemyMaximum == 0 && settings.GetTierMaximum(CombatantTier.EliteEnemy) == 0;
        return ok ? GameSystemVerificationProbeResult.Pass("EliteEnemyMaximum=0(Unset)")
                  : GameSystemVerificationProbeResult.Fail($"EliteEnemyMaximum={settings.EliteEnemyMaximum}");
    }

    private static GameSystemVerificationProbeResult VerifyNormalEnemyD8(GameSystemVerificationContext _)
    {
        bool ok = NormalEnemyRuntimeSkill.FallbackDiceMin == 1 && NormalEnemyRuntimeSkill.FallbackDiceMax == 8;
        return ok ? GameSystemVerificationProbeResult.Pass("Fallback=1..8")
                  : GameSystemVerificationProbeResult.Fail($"Fallback={NormalEnemyRuntimeSkill.FallbackDiceMin}..{NormalEnemyRuntimeSkill.FallbackDiceMax}");
    }
}
