using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Phase D-2 — Yujin runtime gate.
/// Y-01/Y-02/Y-03/Y-05/Y-07을 0916 정본 기준으로 검증한다.
/// 전체 스킬 SO/강화별 데이터는 Phase E(Y-06/C-38) 범위다.
/// </summary>
public sealed class PhaseDYujinGameSystemVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_d.yujin";
    public int Order => 510;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Static(
            "phased.yujin.y01.weapon_profiles",
            "Y-01",
            "유진 무기 Profile 단일 계약",
            GameSystemVerificationCategory.Contract,
            "백우 3/50%/7/Pierce/표식6·12/+20, 적설 2/50%/8/Blunt/12·24, 낙일 1/20%/24/Cut/처형/8·16",
            VerifyWeaponProfiles);

        yield return Runtime(
            "phased.yujin.y01.runtime_profile_usage",
            "Y-01",
            "유진 Runtime이 Weapon Profile을 직접 사용",
            GameSystemVerificationCategory.Damage,
            "물리 타입/낙일 교환승리 continuation이 현재 무기 profile과 일치",
            VerifyRuntimeProfileUsage);

        yield return Static(
            "phased.yujin.y02.coin_formula",
            "Y-02",
            "유진 코인 위력 11 고정 공식",
            GameSystemVerificationCategory.Resource,
            "뒷면=13+빛×2, 앞면=곡선(코인수)+크리값+빛×2 → 18/20/44",
            VerifyCoinFormula);

        yield return Runtime(
            "phased.yujin.y03.mark_ignition",
            "Y-03",
            "표식 44 발화 Profile",
            GameSystemVerificationCategory.Momentum,
            "표식44 소비, 백우 바 +50; 적설 봉인1; 낙일 즉시약화 profile",
            VerifyMarkIgnition);

        yield return Runtime(
            "phased.yujin.y05.sense_core",
            "Y-05",
            "살수의 감 획득/재굴림/초기화",
            GameSystemVerificationCategory.Resource,
            "매턴+1, 상한없음, 공격 슬롯 예약 재굴림, 처치+1, 전투종료0, 환형 완료 보너스 없음",
            VerifySenseCore);

        yield return Runtime(
            "phased.yujin.y05.finish_and_fold",
            "Y-05",
            "살수의 감 추가타/패 보기",
            GameSystemVerificationCategory.Contract,
            "잔혹한 마무리 기본 추가타 상한1; Duel×Duel 패보기 Fold는 Sense1 소비 후 승패/피해 없는 취소 hook",
            VerifyFinishAndFold);

        yield return Runtime(
            "phased.yujin.y07.delayed_trigger",
            "Y-07",
            "K/L 범용 지연 트리거",
            GameSystemVerificationCategory.Lifecycle,
            "표식발화 trigger, 지속턴 만료, N턴 자동발동을 공통 DelayedEffectTriggerQueue로 표현",
            VerifyDelayedTriggerQueue);

        yield return Runtime(
            "phased.yujin.y07.mark_rider_integration",
            "Y-07",
            "표식 발화 K/L rider 통합",
            GameSystemVerificationCategory.Momentum,
            "같은 anchor의 덫+시한이 발화에 함께 소비되고 시한 배율이 발화효과에 적용",
            VerifyMarkRiderIntegration);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected, probe, true);

    private static GameSystemVerificationCase Static(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category,
            GameSystemVerificationExecutionMode.StaticContract,
            expected, probe, true);

    private static bool Fixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out Yujin yujin,
        out GameSystemVerificationProbeResult failure)
    {
        yujin = null;
        if (!context.TryGetFixture(out fixture, out string reason))
        {
            failure = GameSystemVerificationProbeResult.Fail(reason);
            return false;
        }

        yujin = fixture.Player as Yujin;
        if (yujin == null)
        {
            failure = GameSystemVerificationProbeResult.Fail(
                $"Phase D Yujin Gate는 Player가 Yujin인 전투에서 실행해야 합니다. Player={fixture.Player?.GetType().Name ?? "NULL"}");
            return false;
        }

        failure = null;
        return true;
    }

    private static GameSystemVerificationProbeResult VerifyWeaponProfiles(
        GameSystemVerificationContext context)
    {
        YujinWeaponProfile b = YujinWeapons.Get(YujinWeaponType.Baeku);
        YujinWeaponProfile j = YujinWeapons.Get(YujinWeaponType.Jeokseol);
        YujinWeaponProfile n = YujinWeapons.Get(YujinWeaponType.Nakil);

        bool ok =
            b.CoinCount == 3 && Mathf.Approximately(b.FrontChance, 0.50f) && b.CriticalValue == 7 &&
            b.PhysicalType == PhysicalDamageType.Pierce && !b.CanExecute &&
            b.MarkIgnition == YujinMarkIgnitionType.MomentumPush &&
            b.NormalMarkAmount == 6 && b.DuelMarkAmount == 12 && b.AdditionalStaggerDamagePerHit == 20 &&
            j.CoinCount == 2 && Mathf.Approximately(j.FrontChance, 0.50f) && j.CriticalValue == 8 &&
            j.PhysicalType == PhysicalDamageType.Blunt && !j.CanExecute &&
            j.MarkIgnition == YujinMarkIgnitionType.Seal &&
            j.NormalMarkAmount == 12 && j.DuelMarkAmount == 24 &&
            n.CoinCount == 1 && Mathf.Approximately(n.FrontChance, 0.20f) && n.CriticalValue == 24 &&
            n.PhysicalType == PhysicalDamageType.Cut && n.CanExecute &&
            n.MarkIgnition == YujinMarkIgnitionType.Weaken &&
            n.NormalMarkAmount == 8 && n.DuelMarkAmount == 16;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Baeku/Jeokseol/Nakil canonical profiles MATCH")
            : GameSystemVerificationProbeResult.Fail(
                $"B={b.CoinCount}/{b.FrontChance:0.00}/{b.CriticalValue}/{b.PhysicalType}/{b.NormalMarkAmount}/{b.DuelMarkAmount}, " +
                $"J={j.CoinCount}/{j.FrontChance:0.00}/{j.CriticalValue}/{j.PhysicalType}/{j.NormalMarkAmount}/{j.DuelMarkAmount}, " +
                $"N={n.CoinCount}/{n.FrontChance:0.00}/{n.CriticalValue}/{n.PhysicalType}/{n.CanExecute}/{n.NormalMarkAmount}/{n.DuelMarkAmount}");
    }

    private static GameSystemVerificationProbeResult VerifyRuntimeProfileUsage(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out Yujin yujin, out GameSystemVerificationProbeResult fail))
            return fail;

        YujinMechanic mechanic = yujin.YujinMechanic;
        if (mechanic == null)
            return GameSystemVerificationProbeResult.Fail("YujinMechanic missing");

        mechanic.SetWeaponForVerification(YujinWeaponType.Baeku);
        bool baekuType = yujin.ResolveWeaponPhysicalType() == PhysicalDamageType.Pierce;
        mechanic.SetWeaponForVerification(YujinWeaponType.Jeokseol);
        bool jeokType = yujin.ResolveWeaponPhysicalType() == PhysicalDamageType.Blunt;
        mechanic.SetWeaponForVerification(YujinWeaponType.Nakil);
        bool nakilType = yujin.ResolveWeaponPhysicalType() == PhysicalDamageType.Cut;

        IExchangeContinuationRule continuation = mechanic;
        int nakilRemaining = continuation.ModifyOpponentRemainingRollCount(null, null, 4);
        // null winner must not accidentally delete; explicit action test below.
        SkillDefinition d = CreateDefinition("VERIFY_YUJIN_NAKIL", ActionType.NormalAttack, 0);
        Skill skill = yujin.CreateRuntimeSkillForLoadout(d);
        skill.Initialize(yujin, yujin.BattleContext._battleEvent);
        BattleAction action = CreateAction(yujin, skill, null, null, 920001);
        int removed = continuation.ModifyOpponentRemainingRollCount(action, null, 4);
        UnityEngine.Object.DestroyImmediate(d);

        bool hooked = typeof(ClashManager).GetMethod(
            "ApplyExchangeContinuationRules",
            BindingFlags.NonPublic | BindingFlags.Static) != null;

        bool ok = baekuType && jeokType && nakilType && nakilRemaining == 4 && removed == 0 && hooked;
        return ok
            ? GameSystemVerificationProbeResult.Pass("Physical types from profile; Nakil exchange-win continuation wired")
            : GameSystemVerificationProbeResult.Fail(
                $"Types={baekuType}/{jeokType}/{nakilType}, Null={nakilRemaining}, Nakil={removed}, Hook={hooked}");
    }

    private static GameSystemVerificationProbeResult VerifyCoinFormula(
        GameSystemVerificationContext context)
    {
        bool ok =
            YujinWeapons.FixedBasePower == 11 &&
            YujinWeapons.BackContribution == 2 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Baeku, false, 0) == 13 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Baeku, true, 0) == 18 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Jeokseol, true, 0) == 20 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Nakil, true, 0) == 44 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Baeku, false, 2) == 17 &&
            YujinWeapons.ResolveCoinPower(YujinWeaponType.Nakil, true, 2) == 48;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Tail 13+2L; Heads 18/20/44 +2L")
            : GameSystemVerificationProbeResult.Fail("YujinWeapons.ResolveCoinPower mismatch");
    }

    private static GameSystemVerificationProbeResult VerifyMarkIgnition(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Yujin yujin, out GameSystemVerificationProbeResult fail))
            return fail;

        YujinMechanic mechanic = yujin.YujinMechanic;
        Character enemy = f.Enemy;
        if (mechanic == null || enemy == null)
            return GameSystemVerificationProbeResult.Fail("Yujin mechanic/enemy missing");

        mechanic.SetWeaponForVerification(YujinWeaponType.Baeku);
        int before = f.MomentumManager?.CurrentMomentum ?? 0;
        mechanic.GrantMark(enemy, null, 44);
        int after = f.MomentumManager?.CurrentMomentum ?? 0;

        YujinWeaponProfile j = YujinWeapons.Get(YujinWeaponType.Jeokseol);
        YujinWeaponProfile n = YujinWeapons.Get(YujinWeaponType.Nakil);

        bool ok =
            mechanic.GetMark(enemy) == 0 &&
            after - before == 50 &&
            j.MarkIgnition == YujinMarkIgnitionType.Seal &&
            n.MarkIgnition == YujinMarkIgnitionType.Weaken;

        return ok
            ? GameSystemVerificationProbeResult.Pass($"Mark44 -> 0, Baeku momentum {before}->{after} (+50)")
            : GameSystemVerificationProbeResult.Fail(
                $"Mark={mechanic.GetMark(enemy)}, Momentum={before}->{after}, J={j.MarkIgnition}, N={n.MarkIgnition}");
    }

    private static GameSystemVerificationProbeResult VerifySenseCore(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Yujin yujin, out GameSystemVerificationProbeResult fail))
            return fail;

        YujinMechanic mechanic = yujin.YujinMechanic;
        if (mechanic == null)
            return GameSystemVerificationProbeResult.Fail("YujinMechanic missing");

        int initial = mechanic.Sense;
        f.Context._battleEvent.RaiseTurnStart(1);
        bool turnGain = mechanic.Sense == initial + 1;

        mechanic.GrantSense(100);
        bool uncapped = mechanic.Sense >= 101;

        int beforeKill = mechanic.Sense;
        f.Context._battleEvent.RaiseKill(KillEventContext.External(yujin, f.Enemy));
        bool killGain = mechanic.Sense == beforeKill + 1;

        SkillDefinition d = CreateDefinition("VERIFY_YUJIN_SENSE_NORMAL", ActionType.NormalAttack, 0);
        Skill skill = yujin.CreateRuntimeSkillForLoadout(d);
        skill.Initialize(yujin, f.Context._battleEvent);
        BattleAction mine = CreateAction(yujin, skill, f.Enemy, null, 920010);
        mine.Slot.UseCharacterRerollResource = true;
        mine.LastRollResult = new RollResult { FinalPower = 1, ClashPower = 1 };
        mine.ClashPower = 1;
        BattleAction opponent = new BattleAction
        {
            Slot = new ActionSlot { ActionId = 920011, Owner = f.Enemy, Speed = 5 }
        };
        opponent.ClashPower = 99;
        int beforeReroll = mechanic.Sense;
        bool reroll = mechanic.TryRequestExchangeReroll(
            new ExchangeRerollContext(mine, opponent, 0, 0));
        bool rerollConsumed = reroll && mechanic.Sense == beforeReroll - 1;
        UnityEngine.Object.DestroyImmediate(d);

        YujinHwanhyeongFlowPassive legacyPassive =
            ScriptableObject.CreateInstance<YujinHwanhyeongFlowPassive>();
        List<CombatMechanic> generated = new();
        legacyPassive.CreateMechanics(default, generated);
        bool noSwitchBonus = legacyPassive.SenseGainOnWeaponSwitch == 0 && generated.Count == 0;
        UnityEngine.Object.DestroyImmediate(legacyPassive);

        f.Context._battleEvent.RaiseBattleEnded();
        bool reset = mechanic.Sense == 0;

        bool ok = turnGain && uncapped && killGain && rerollConsumed && noSwitchBonus && reset;
        return ok
            ? GameSystemVerificationProbeResult.Pass("Turn+1 / uncapped / kill+1 / reserved reroll / switch+0 / BattleEnd=0")
            : GameSystemVerificationProbeResult.Fail(
                $"Turn={turnGain}, Uncap={uncapped}, Kill={killGain}, Reroll={rerollConsumed}, Switch0={noSwitchBonus}, Reset={reset}");
    }

    private static GameSystemVerificationProbeResult VerifyFinishAndFold(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Yujin yujin, out GameSystemVerificationProbeResult fail))
            return fail;

        YujinMechanic mechanic = yujin.YujinMechanic;
        if (mechanic == null)
            return GameSystemVerificationProbeResult.Fail("YujinMechanic missing");

        mechanic.SetWeaponForVerification(YujinWeaponType.Baeku);
        mechanic.GrantSense(5);

        SkillDefinition pursuitDef = CreateDefinition(YujinSkillIds.Pursuit, ActionType.Duel, 1);
        Skill pursuit = yujin.CreateRuntimeSkillForLoadout(pursuitDef);
        pursuit.Initialize(yujin, f.Context._battleEvent);
        BattleAction pursuitAction = CreateAction(yujin, pursuit, f.Enemy, null, 920020);
        int beforeSense = mechanic.Sense;
        int hits = mechanic.ResolvePursuitExtraCoinsForVerification(
            pursuitAction,
            new[] { true, true, true });
        bool finish = hits <= 1 && mechanic.Sense >= beforeSense - 1;
        UnityEngine.Object.DestroyImmediate(pursuitDef);

        SkillDefinition duelDef = CreateDefinition("VERIFY_YUJIN_PEEK", ActionType.Duel, 1);
        Skill duel = yujin.CreateRuntimeSkillForLoadout(duelDef);
        duel.Initialize(yujin, f.Context._battleEvent);
        BattleAction mine = CreateAction(yujin, duel, f.Enemy, null, 920021);
        BattleAction opponent = new BattleAction
        {
            Slot = new ActionSlot { ActionId = 920022, Owner = f.Enemy, Speed = 5 }
        };
        Skill enemyDuel = f.Enemy.RuntimeSkills?.FirstOrDefault(x => x?.ActionType == ActionType.Duel);
        opponent.Slot.Skill = enemyDuel;
        opponent.LastRollResult = new RollResult { FinalPower = 30, ClashPower = 30 };

        mechanic.GrantSense(1);
        int beforeFold = mechanic.Sense;
        mechanic.PeekDecisionProvider = new AlwaysFoldProvider();
        bool folded = ((IExchangePreResolutionRule)mechanic).TryCancelPairedExchange(
            mine,
            opponent,
            0,
            opponent.LastRollResult,
            out string reason);
        bool foldConsumed = folded && mechanic.Sense == beforeFold - 1 && !string.IsNullOrWhiteSpace(reason);
        UnityEngine.Object.DestroyImmediate(duelDef);

        bool hooked = typeof(ClashManager).GetMethod(
            "TryCancelPairedExchangeByRule",
            BindingFlags.NonPublic | BindingFlags.Static) != null;

        bool ok = finish && foldConsumed && hooked;
        return ok
            ? GameSystemVerificationProbeResult.Pass($"BrutalFinish hits={hits}; Fold consumed 1; generic pre-resolution hook wired")
            : GameSystemVerificationProbeResult.Fail(
                $"Finish={finish}/hits{hits}, Fold={foldConsumed}, Hook={hooked}");
    }

    private static GameSystemVerificationProbeResult VerifyDelayedTriggerQueue(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out _, out GameSystemVerificationProbeResult fail))
            return fail;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(f.Enemy, null);
        DelayedEffectTriggerQueue queue = new();

        queue.Schedule(anchor, "mark", 0, 0, "K");
        queue.AdvanceTurn();
        queue.AdvanceTurn();
        bool persistent = queue.Count == 1 && queue.ConsumeTriggered(anchor, "mark").Count == 1;

        queue.Schedule(anchor, "mark", 3, 0, "L");
        queue.AdvanceTurn();
        queue.AdvanceTurn();
        bool withinWindow = queue.ConsumeTriggered(anchor, "mark").Count == 1;

        queue.Schedule(anchor, "expire", 3, 0, "L-expire");
        queue.AdvanceTurn();
        queue.AdvanceTurn();
        queue.AdvanceTurn();
        bool expired = queue.Count == 0;

        queue.Schedule(anchor, "auto", 0, 2, "N-turn");
        bool autoFirst = queue.AdvanceTurn().Count == 0;
        bool autoSecond = queue.AdvanceTurn().Count == 1 && queue.Count == 0;

        queue.Schedule(anchor, "cleanup.character", 0, 0, "cleanup");
        bool characterCleanup =
            queue.RemoveForCharacter(f.Enemy) == 1 &&
            queue.Count == 0;

        BodyPart part = f.Enemy.BodyParts?.FirstOrDefault(p => p != null && !p.IsBroken);
        bool partCleanup = true;
        if (part != null)
        {
            CombatStatusAnchor partAnchor = CombatStatusAnchor.Resolve(f.Enemy, part);
            queue.Schedule(partAnchor, "cleanup.part", 0, 0, "cleanup");
            partCleanup =
                queue.RemoveForPart(f.Enemy, part) == 1 &&
                queue.Count == 0;
        }

        bool ok =
            persistent && withinWindow && expired &&
            autoFirst && autoSecond &&
            characterCleanup && partCleanup;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                "persistent trigger / 3-turn window / expiry / N-turn auto / target cleanup all PASS")
            : GameSystemVerificationProbeResult.Fail(
                $"Persistent={persistent}, Window={withinWindow}, Expired={expired}, " +
                $"Auto={autoFirst}/{autoSecond}, Cleanup={characterCleanup}/{partCleanup}");
    }

    private static GameSystemVerificationProbeResult VerifyMarkRiderIntegration(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Yujin yujin, out GameSystemVerificationProbeResult fail))
            return fail;

        YujinMechanic mechanic = yujin.YujinMechanic;
        mechanic.SetWeaponForVerification(YujinWeaponType.Baeku);

        mechanic.RegisterMarkTrap(f.Enemy, null, 20);
        mechanic.RegisterMarkDeadline(f.Enemy, null, 3, 2);
        int pendingBefore = mechanic.PendingDelayedTriggerCount;
        int momentumBefore = f.MomentumManager.CurrentMomentum;
        mechanic.GrantMark(f.Enemy, null, 44);
        int momentumAfter = f.MomentumManager.CurrentMomentum;

        bool ok =
            pendingBefore == 2 &&
            mechanic.PendingDelayedTriggerCount == 0 &&
            momentumAfter - momentumBefore == 100;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"K+L consumed together; Baeku deadline doubled +50 -> +100 ({momentumBefore}->{momentumAfter})")
            : GameSystemVerificationProbeResult.Fail(
                $"Pending={pendingBefore}->{mechanic.PendingDelayedTriggerCount}, Momentum={momentumBefore}->{momentumAfter}");
    }

    private static SkillDefinition CreateDefinition(
        string name,
        ActionType actionType,
        int energyCost)
    {
        SkillDefinition d = ScriptableObject.CreateInstance<SkillDefinition>();
        d.name = name;
        d.ActionType = actionType;
        d.OverrideEnergyCost = true;
        d.EnergyCost = Mathf.Max(0, energyCost);
        return d;
    }

    private static BattleAction CreateAction(
        Character owner,
        Skill skill,
        Character target,
        BodyPart targetPart,
        long actionId)
    {
        return new BattleAction
        {
            Slot = new ActionSlot
            {
                ActionId = actionId,
                Owner = owner,
                Part = owner?.BodyParts?.FirstOrDefault(part => part != null && !part.IsBroken),
                Skill = skill,
                TargetCharacter = target,
                TargetPart = targetPart,
                Speed = 5
            }
        };
    }

    private sealed class AlwaysFoldProvider : IYujinPeekDecisionProvider
    {
        public YujinPeekDecision Decide(YujinPeekDecisionContext context) =>
            YujinPeekDecision.Fold;
    }
}
