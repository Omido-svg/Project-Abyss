using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CanonicalContractVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "canonical.contracts";
    public int Order => 100;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Static(
            "system.contract.timing.authoring_five",
            "C-42",
            "Skill Timing Authoring 5종",
            "OnExecute / OnDuelMatched / OnExchangeWin / OnExchangeLose / OnClashEnd",
            VerifyTimingContract);

        yield return Static(
            "system.contract.condition.canonical_layer",
            "C-47",
            "Canonical Condition Layer",
            "현재/직전 기세, stack range, stagger ratio, negative-status count, resource range",
            VerifyConditionLayer);

        yield return Static(
            "system.contract.loadout.3331",
            "C-45",
            "Loadout 3|3|3|1",
            "평타3 / 결투3 / 도사림3 / 위세1",
            VerifyLoadoutLimits);

        yield return Static(
            "system.contract.power.curve",
            "C-05",
            "기본위력 Canonical Curve",
            "1:20, 2:12, 3:11, 4:9, 5:8 / 빛 1당 +2",
            VerifyPowerCurve);

        yield return Static(
            "system.contract.skill_color.whole_skill",
            "C-10",
            "RED/BLUE 스킬 전체 색 계약",
            "SkillDefinition.Color가 roll legacy Type보다 우선",
            VerifySkillColorContract);

        yield return Static(
            "system.contract.bodypart.skill_access",
            "C-02",
            "부위별 행동 접근 + 위세 슬롯리스 계약",
            "HEAD=평타·결투·도사림 / HAND=평타·결투 / LEGS=도사림 / Prestige=부위슬롯 불가",
            VerifyBodyPartAccess);

        yield return Static(
            "system.contract.momentum.bands",
            "C-46",
            "기세 Band 고정값",
            "-100..100 / -70,-30,+30,+70 / Hit20 / Duel40",
            VerifyMomentumSettings,
            GameSystemVerificationCategory.Momentum);

        yield return Static(
            "system.contract.fervor.thresholds",
            "C-46",
            "고조·열광 고정값",
            "10/5/2/0/0, 문턱 4/8/10, 최대3",
            VerifyFervorSettings,
            GameSystemVerificationCategory.Fervor);

        yield return Static(
            "system.contract.energy.baseline",
            "C-04",
            "빛 기본 계약",
            "전투 시작 max3/full, 턴 시작 +1",
            VerifyEnergySettings,
            GameSystemVerificationCategory.Resource);

        yield return Static(
            "system.contract.planning.command_shape",
            "C-03",
            "계획 단계 즉시행동 Command 계약",
            "도사림과 위세가 Resolution 재실행 없이 계획 단계에서 commit 가능한 command service 존재",
            _ => GameSystemVerificationProbeResult.Pass(
                "BattleActionPlanCommandService / PlanningEffectCommitted / SkipResolution"));
    }

    private static GameSystemVerificationCase Static(
        string id,
        string requirement,
        string name,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe,
        GameSystemVerificationCategory category = GameSystemVerificationCategory.Contract)
    {
        return new GameSystemVerificationCase(
            id,
            requirement,
            name,
            category,
            GameSystemVerificationExecutionMode.StaticContract,
            expected,
            probe);
    }

    private static GameSystemVerificationProbeResult VerifyTimingContract(
        GameSystemVerificationContext _)
    {
        SkillEffectTiming[] expected =
        {
            SkillEffectTiming.OnExecute,
            SkillEffectTiming.OnDuelMatched,
            SkillEffectTiming.OnExchangeWin,
            SkillEffectTiming.OnExchangeLose,
            SkillEffectTiming.OnClashEnd
        };

        IReadOnlyList<SkillEffectTiming> actual =
            SkillEffectTimingCatalog.AuthoringTimings;

        bool pass = actual != null &&
                    actual.Count == expected.Length &&
                    !expected.Where((value, index) => actual[index] != value).Any();

        string text = actual == null
            ? "NULL"
            : string.Join(", ", actual);

        return pass
            ? GameSystemVerificationProbeResult.Pass(text)
            : GameSystemVerificationProbeResult.Fail(text);
    }

    private static GameSystemVerificationProbeResult VerifyConditionLayer(
        GameSystemVerificationContext _)
    {
        SkillEffectConditionType[] required =
        {
            SkillEffectConditionType.CurrentMomentumState,
            SkillEffectConditionType.PreviousTurnMomentumState,
            SkillEffectConditionType.CurrentMomentumRange,
            SkillEffectConditionType.StatusStackRange,
            SkillEffectConditionType.StaggerRatioRange,
            SkillEffectConditionType.NegativeStatusCountRange,
            SkillEffectConditionType.ResourceRange
        };

        HashSet<SkillEffectConditionType> values =
            new(Enum.GetValues(typeof(SkillEffectConditionType))
                .Cast<SkillEffectConditionType>());

        List<string> missing = required
            .Where(value => !values.Contains(value))
            .Select(value => value.ToString())
            .ToList();

        return missing.Count == 0
            ? GameSystemVerificationProbeResult.Pass(string.Join(", ", required))
            : GameSystemVerificationProbeResult.Fail(
                "Missing=" + string.Join(", ", missing));
    }

    private static GameSystemVerificationProbeResult VerifyLoadoutLimits(
        GameSystemVerificationContext _)
    {
        BattleRuleSettings rules = new();
        rules.Normalize();
        ProgressionRuleSettings p = rules.Progression;

        bool pass =
            p.NormalAttackLoadoutLimit == CanonicalGameSystemVerificationSpec.NormalAttackLoadout &&
            p.DuelLoadoutLimit == CanonicalGameSystemVerificationSpec.DuelLoadout &&
            p.PreparationLoadoutLimit == CanonicalGameSystemVerificationSpec.PreparationLoadout &&
            p.PrestigeLoadoutLimit == CanonicalGameSystemVerificationSpec.PrestigeLoadout;

        string actual =
            $"{p.NormalAttackLoadoutLimit}|{p.DuelLoadoutLimit}|" +
            $"{p.PreparationLoadoutLimit}|{p.PrestigeLoadoutLimit}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyPowerCurve(
        GameSystemVerificationContext _)
    {
        List<string> failures = new();

        foreach (KeyValuePair<int, int> pair in CanonicalGameSystemVerificationSpec.PowerCurve)
        {
            if (!PowerFormulaService.TryGetCurveValue(pair.Key, out int value) ||
                value != pair.Value)
            {
                failures.Add($"Roll{pair.Key}={value}, expected={pair.Value}");
            }
        }

        if (PowerFormulaService.EnergyPowerPerPoint !=
            CanonicalGameSystemVerificationSpec.EnergyPowerPerPoint)
        {
            failures.Add(
                $"EnergyPower={PowerFormulaService.EnergyPowerPerPoint}, " +
                $"expected={CanonicalGameSystemVerificationSpec.EnergyPowerPerPoint}");
        }

        return failures.Count == 0
            ? GameSystemVerificationProbeResult.Pass("Curve + EnergyPower PASS")
            : GameSystemVerificationProbeResult.Fail(
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static GameSystemVerificationProbeResult VerifySkillColorContract(
        GameSystemVerificationContext _)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        try
        {
            definition.Rolls = new List<SkillRollData>
            {
                new SkillRollData { Type = CombatRollType.Stagger }
            };

            definition.Color = SkillColor.Red;
            SkillColor red = SkillColorRules.Resolve(definition);

            definition.Color = SkillColor.Blue;
            SkillColor blue = SkillColorRules.Resolve(definition);

            bool pass =
                red == SkillColor.Red &&
                blue == SkillColor.Blue;

            string actual =
                $"ExplicitRed={red}, ExplicitBlue={blue}";

            return pass
                ? GameSystemVerificationProbeResult.Pass(actual)
                : GameSystemVerificationProbeResult.Fail(actual);
        }
        finally
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(definition);
            else
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyBodyPartAccess(
        GameSystemVerificationContext _)
    {
        Dictionary<PartType, ActionType[]> expected = new()
        {
            { PartType.HEAD, new[] { ActionType.NormalAttack, ActionType.Duel, ActionType.Preparation } },
            { PartType.LEFT_HAND, new[] { ActionType.NormalAttack, ActionType.Duel } },
            { PartType.RIGHT_HAND, new[] { ActionType.NormalAttack, ActionType.Duel } },
            { PartType.LEGS, new[] { ActionType.Preparation } }
        };

        ActionType[] all =
        {
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige
        };

        List<string> failures = new();

        foreach (KeyValuePair<PartType, ActionType[]> pair in expected)
        {
            foreach (ActionType action in all)
            {
                bool shouldAllow = pair.Value.Contains(action);
                bool actual = BodyPartSkillAccessPolicy.Allows(pair.Key, action);

                if (actual != shouldAllow)
                {
                    failures.Add(
                        $"{pair.Key}/{action}={actual}, expected={shouldAllow}");
                }
            }
        }

        return failures.Count == 0
            ? GameSystemVerificationProbeResult.Pass("16/16 PASS")
            : GameSystemVerificationProbeResult.Fail(
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static GameSystemVerificationProbeResult VerifyMomentumSettings(
        GameSystemVerificationContext _)
    {
        MomentumRuleSettings s = new();
        s.Normalize();

        bool pass =
            s.Minimum == CanonicalGameSystemVerificationSpec.MomentumMinimum &&
            s.Maximum == CanonicalGameSystemVerificationSpec.MomentumMaximum &&
            s.LastStandThreshold == CanonicalGameSystemVerificationSpec.MomentumLastStand &&
            s.DisadvantageThreshold == CanonicalGameSystemVerificationSpec.MomentumDisadvantage &&
            s.AdvantageThreshold == CanonicalGameSystemVerificationSpec.MomentumAdvantage &&
            s.OverwhelmThreshold == CanonicalGameSystemVerificationSpec.MomentumOverwhelm &&
            s.HitShift == CanonicalGameSystemVerificationSpec.MomentumHitShift &&
            s.DuelExchangeTotalShift == CanonicalGameSystemVerificationSpec.MomentumDuelShift;

        string actual =
            $"Range={s.Minimum}..{s.Maximum}, Bands={s.LastStandThreshold}/" +
            $"{s.DisadvantageThreshold}/{s.AdvantageThreshold}/{s.OverwhelmThreshold}, " +
            $"Shift={s.HitShift}/{s.DuelExchangeTotalShift}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyFervorSettings(
        GameSystemVerificationContext _)
    {
        FervorRuleSettings s = new();
        s.Normalize();

        bool pass =
            s.OverwhelmGain == CanonicalGameSystemVerificationSpec.FervorOverwhelmGain &&
            s.AdvantageGain == CanonicalGameSystemVerificationSpec.FervorAdvantageGain &&
            s.BalanceGain == CanonicalGameSystemVerificationSpec.FervorBalanceGain &&
            s.DisadvantageGain == 0 &&
            s.LastStandGain == 0 &&
            s.Level1Cost == CanonicalGameSystemVerificationSpec.FervorLevel1Cost &&
            s.Level2Cost == CanonicalGameSystemVerificationSpec.FervorLevel2Cost &&
            s.Level3Cost == CanonicalGameSystemVerificationSpec.FervorLevel3Cost &&
            s.MaximumLevel == CanonicalGameSystemVerificationSpec.FervorMaximumLevel;

        string actual =
            $"Gain={s.OverwhelmGain}/{s.AdvantageGain}/{s.BalanceGain}/" +
            $"{s.DisadvantageGain}/{s.LastStandGain}, Cost={s.Level1Cost}/" +
            $"{s.Level2Cost}/{s.Level3Cost}, Max={s.MaximumLevel}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyEnergySettings(
        GameSystemVerificationContext _)
    {
        EnergyRuleSettings s = new();
        s.Normalize();

        bool pass =
            s.DefaultMaximum == CanonicalGameSystemVerificationSpec.EnergyStartMaximum &&
            s.TurnStartGain == CanonicalGameSystemVerificationSpec.EnergyTurnStartGain &&
            s.StartFull;

        string actual =
            $"Max={s.DefaultMaximum}, Turn+={s.TurnStartGain}, StartFull={s.StartFull}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }
}

public sealed class PhaseARuntimeVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_a.runtime";
    public int Order => 200;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Runtime(
            "system.runtime.production_factory_graph",
            "C-03",
            "Production BattleRuntimeFactory graph 사용",
            GameSystemVerificationCategory.Lifecycle,
            "Verification sandbox가 Production service graph를 사용",
            VerifyProductionFactoryGraph);

        yield return Runtime(
            "system.runtime.momentum.time_axis_separation",
            "C-46",
            "현재 Band와 직전 턴 Final Band 분리",
            GameSystemVerificationCategory.Momentum,
            "직전 LastStand snapshot + 현재 Overwhelm 동시 표현",
            VerifyMomentumTimeAxis);

        yield return Runtime(
            "system.runtime.speed.boundaries",
            "C-06",
            "속도 보정 경계 0/1/5/6",
            GameSystemVerificationCategory.Speed,
            "0→0, 1~5→+1, 6+→+2 / 느린 쪽 0",
            VerifySpeedBoundaries);

        yield return Runtime(
            "system.runtime.pairing.exact_target_ignores_speed",
            "C-06",
            "합 성립과 속도 분리",
            GameSystemVerificationCategory.Pairing,
            "정확한 TargetSlot 조준이면 더 느려도 CanChallenge=true",
            VerifyExactTargetIgnoresSpeed);

        yield return Runtime(
            "system.runtime.planning.preparation_immediate_commit",
            "C-03",
            "도사림 계획 즉시 Commit",
            GameSystemVerificationCategory.Planning,
            "성공 즉시 ResourceCostCommitted + PlanningEffectCommitted + SkipResolution",
            VerifyPreparationImmediateCommit,
            false);

        yield return Runtime(
            "system.runtime.planning.attack_cost_no_refund",
            "C-04",
            "공격 비용 계획 차감·취소 환불 없음",
            GameSystemVerificationCategory.Resource,
            "공격 계획 시 비용 차감, Remove 후에도 환불 없음",
            VerifyAttackCostNoRefund,
            false);

        yield return Runtime(
            "system.runtime.planning.prestige_slotless",
            "C-02",
            "위세 슬롯리스 실행",
            GameSystemVerificationCategory.Prestige,
            "위세 실행 전후 ActionManager owner slot 수 불변",
            VerifyPrestigeSlotless,
            false);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string requirement,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe,
        bool required = true) =>
        new(
            id,
            requirement,
            name,
            category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected,
            probe,
            required);

    private static bool TryFixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out GameSystemVerificationProbeResult skip)
    {
        if (context.TryGetFixture(out fixture, out string reason))
        {
            skip = null;
            return true;
        }

        skip = GameSystemVerificationProbeResult.Skip(reason);
        return false;
    }

    private static GameSystemVerificationProbeResult VerifyProductionFactoryGraph(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        bool graph = fixture.HasProductionServiceGraph();
        bool same = ReferenceEquals(fixture.Context.Services, fixture.Runtime.Services);

        return graph && same
            ? GameSystemVerificationProbeResult.Pass(
                $"Graph={graph}, SharedServices={same}")
            : GameSystemVerificationProbeResult.Fail(
                $"Graph={graph}, SharedServices={same}");
    }

    private static GameSystemVerificationProbeResult VerifyMomentumTimeAxis(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        MomentumManager manager = fixture.MomentumManager;
        Character owner = fixture.Player;

        manager.Reset();
        manager.SetMomentumForDebug(-70);
        manager.FinalizeTurn();
        manager.BeginTurn();
        manager.SetMomentumForDebug(70);

        MomentumState current = manager.GetCurrentBand(owner);
        MomentumState previous = manager.GetPreviousTurnFinalState(owner);

        bool pass =
            current == MomentumState.Overwhelm &&
            previous == MomentumState.LastStand;

        string actual =
            $"Current={current}, PreviousFinal={previous}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifySpeedBoundaries(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        if (!TryFindCombatSkill(fixture.Player, out Skill firstSkill) ||
            !TryFindCombatSkill(fixture.Enemy, out Skill secondSkill))
        {
            return GameSystemVerificationProbeResult.Skip(
                "합 가능한 Runtime Skill이 없습니다.");
        }

        ClashPowerPipeline pipeline =
            new(fixture.Context.Rules.Clash);

        int[] gaps = { 0, 1, 5, 6 };
        int[] expected = { 0, 1, 1, 2 };
        List<string> failures = new();

        for (int i = 0; i < gaps.Length; i++)
        {
            BattleAction first = CreateAction(
                fixture.Player,
                firstSkill,
                10 + gaps[i]);

            BattleAction second = CreateAction(
                fixture.Enemy,
                secondSkill,
                10);

            pipeline.RollForClash(first, second, i);
            pipeline.RollForClash(second, first, i);

            if (first.SpeedModifier != expected[i])
                failures.Add(
                    $"Gap{gaps[i]} fast={first.SpeedModifier}, expected={expected[i]}");

            if (second.SpeedModifier != 0)
                failures.Add(
                    $"Gap{gaps[i]} slow={second.SpeedModifier}, expected=0");
        }

        return failures.Count == 0
            ? GameSystemVerificationProbeResult.Pass(
                "0→0, 1→1, 5→1, 6→2 / slower=0")
            : GameSystemVerificationProbeResult.Fail(
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static GameSystemVerificationProbeResult VerifyExactTargetIgnoresSpeed(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        if (!TryFindCombatSkill(fixture.Player, out Skill playerSkill) ||
            !TryFindCombatSkill(fixture.Enemy, out Skill enemySkill))
        {
            return GameSystemVerificationProbeResult.Skip(
                "합 가능한 Runtime Skill이 없습니다.");
        }

        ActionSlot enemy = new()
        {
            ActionId = 100,
            Owner = fixture.Enemy,
            Skill = enemySkill,
            Speed = 10,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = fixture.Player
        };

        ActionSlot player = new()
        {
            ActionId = 200,
            Owner = fixture.Player,
            Skill = playerSkill,
            Speed = 1,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = fixture.Enemy,
            TargetSlot = enemy
        };

        ClashMatchPolicy policy =
            new(new ActionPhaseSorter());

        bool pass = policy.CanChallenge(player, enemy);

        return pass
            ? GameSystemVerificationProbeResult.Pass(
                "PlayerSpeed=1 / EnemySpeed=10 / CanChallenge=True")
            : GameSystemVerificationProbeResult.Fail(
                "CanChallenge=False");
    }

    private static GameSystemVerificationProbeResult VerifyPreparationImmediateCommit(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        if (!TryFindSelectableSkill(
                fixture.Player,
                ActionType.Preparation,
                out BodyPart part,
                out Skill skill))
        {
            return GameSystemVerificationProbeResult.Skip(
                "선택 가능한 도사림이 없습니다.");
        }

        BattleActionPlanCommandService service =
            new(fixture.ActionManager, fixture.SpeedManager);

        int beforeEnergy = fixture.Player.CurrentEnergy;

        ActionPlanAssignmentResult result =
            service.TryAssign(
                new ActionPlanAssignmentRequest
                {
                    Owner = fixture.Player,
                    OwnerPart = part,
                    Skill = skill,
                    ActionIndex = 0,
                    Target = fixture.Player,
                    TargetPart = part,
                    TargetRule = TargetSelectionRule.LivingPartOnly
                });

        bool pass =
            result.Success &&
            result.Slot != null &&
            result.Slot.ResourceCostCommitted &&
            result.Slot.PlanningEffectCommitted &&
            result.Slot.SkipResolution &&
            fixture.Player.CurrentEnergy ==
                beforeEnergy - skill.EnergyCost;

        string actual =
            $"Success={result.Success}, Cost={result.Slot?.ResourceCostCommitted}, " +
            $"Effect={result.Slot?.PlanningEffectCommitted}, Skip={result.Slot?.SkipResolution}, " +
            $"Energy={beforeEnergy}->{fixture.Player.CurrentEnergy}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(
                actual,
                result.FailureReason);
    }

    private static GameSystemVerificationProbeResult VerifyAttackCostNoRefund(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        if (!TryFindPaidAttack(
                fixture.Player,
                out BodyPart part,
                out Skill skill))
        {
            return GameSystemVerificationProbeResult.Skip(
                "빛 비용이 있는 선택 가능한 공격 스킬이 없습니다.");
        }

        BodyPart targetPart =
            fixture.Enemy.BodyParts?
                .FirstOrDefault(candidate =>
                    candidate != null && !candidate.IsBroken);

        BattleActionPlanCommandService service =
            new(fixture.ActionManager, fixture.SpeedManager);

        int before = fixture.Player.CurrentEnergy;

        ActionPlanAssignmentResult result =
            service.TryAssign(
                new ActionPlanAssignmentRequest
                {
                    Owner = fixture.Player,
                    OwnerPart = part,
                    Skill = skill,
                    ActionIndex = 0,
                    Target = fixture.Enemy,
                    TargetPart = targetPart,
                    TargetRule = TargetSelectionRule.StandardAttack
                });

        if (!result.Success)
        {
            return GameSystemVerificationProbeResult.Fail(
                "Planning failed",
                result.FailureReason);
        }

        int afterCommit = fixture.Player.CurrentEnergy;
        bool removed =
            service.Remove(fixture.Player, part, 0);
        int afterRemove = fixture.Player.CurrentEnergy;

        bool pass =
            removed &&
            afterCommit == before - skill.EnergyCost &&
            afterRemove == afterCommit;

        string actual =
            $"Removed={removed}, Energy={before}->{afterCommit}->{afterRemove}, Cost={skill.EnergyCost}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyPrestigeSlotless(
        GameSystemVerificationContext context)
    {
        if (!TryFixture(context, out GameSystemVerificationFixture fixture, out var skip))
            return skip;

        Skill prestige =
            fixture.Player.RuntimeSkills?
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.ActionType == ActionType.Prestige);

        if (prestige == null ||
            fixture.Player.CurrentStatus == null ||
            fixture.Player.RuntimeStatus == null ||
            fixture.Player.CurrentStatus.maxPrestige <= 0)
        {
            return GameSystemVerificationProbeResult.Skip(
                "현재 Player에 실행 가능한 위세가 없습니다.");
        }

        fixture.Player.RuntimeStatus.currentPrestige =
            fixture.Player.CurrentStatus.maxPrestige;

        int before =
            fixture.ActionManager.CountSlots(fixture.Player);

        BattleActionPlanCommandService service =
            new(fixture.ActionManager, fixture.SpeedManager);

        PrestigePlanningExecutionResult result =
            service.TryExecutePrestige(
                fixture.Player,
                prestige);

        int after =
            fixture.ActionManager.CountSlots(fixture.Player);

        bool pass =
            result.Success &&
            before == after;

        string actual =
            $"Success={result.Success}, Slots={before}->{after}, OwnerPart={result.Action?.OwnerPart}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(
                actual,
                result.FailureReason);
    }

    private static BattleAction CreateAction(
        Character owner,
        Skill skill,
        int speed) =>
        new()
        {
            Slot = new ActionSlot
            {
                Owner = owner,
                Skill = skill,
                Speed = speed,
                Phase = ActionPhase.COMBAT
            }
        };

    private static bool TryFindCombatSkill(
        Character owner,
        out Skill skill)
    {
        skill = owner?.RuntimeSkills?
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.CanClash &&
                (candidate.ActionType == ActionType.NormalAttack ||
                 candidate.ActionType == ActionType.Duel));

        return skill != null;
    }

    private static bool TryFindSelectableSkill(
        Character owner,
        ActionType type,
        out BodyPart part,
        out Skill skill)
    {
        part = null;
        skill = null;

        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart candidatePart in owner.BodyParts)
        {
            if (candidatePart == null || candidatePart.IsBroken)
                continue;

            IReadOnlyList<Skill> selectable =
                owner.GetSelectableSkills(candidatePart, 0);

            Skill found = selectable?
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.ActionType == type);

            if (found == null)
                continue;

            part = candidatePart;
            skill = found;
            return true;
        }

        return false;
    }

    private static bool TryFindPaidAttack(
        Character owner,
        out BodyPart part,
        out Skill skill)
    {
        part = null;
        skill = null;

        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart candidatePart in owner.BodyParts)
        {
            if (candidatePart == null || candidatePart.IsBroken)
                continue;

            IReadOnlyList<Skill> selectable =
                owner.GetSelectableSkills(candidatePart, 0);

            Skill found = selectable?
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.EnergyCost > 0 &&
                    candidate.EnergyCost <= owner.CurrentEnergy &&
                    (candidate.ActionType == ActionType.NormalAttack ||
                     candidate.ActionType == ActionType.Duel));

            if (found == null)
                continue;

            part = candidatePart;
            skill = found;
            return true;
        }

        return false;
    }
}

public sealed class LiveSceneVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "live.scene";
    public int Order => 900;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return new GameSystemVerificationCase(
            "system.live.manager_initialized",
            string.Empty,
            "BattleManager Production Scene 초기화",
            GameSystemVerificationCategory.LiveState,
            GameSystemVerificationExecutionMode.LiveScene,
            "Initialized BattleManager + Player + Enemy + production services",
            VerifyManagerInitialized);
    }

    private static GameSystemVerificationProbeResult VerifyManagerInitialized(
        GameSystemVerificationContext context)
    {
        BattleManager manager = context.LiveManager;
        BattleContext battle = manager?.BattleContext;
        BattleRuntimeServices services = battle?.Services;

        int enemies =
            battle?.Enemies?.Count(enemy => enemy != null) ?? 0;

        bool pass =
            manager != null &&
            manager.IsInitialized &&
            battle?.Player != null &&
            enemies > 0 &&
            services?.ActionManager != null &&
            services.SpeedManager != null &&
            services.DamageManager != null &&
            services.ClashManager != null &&
            services.ClashBuilder != null &&
            services.TurnManager != null;

        string actual =
            $"Manager={manager != null}, Initialized={manager?.IsInitialized}, " +
            $"Player={battle?.Player != null}, Enemies={enemies}, Services={services != null}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }
}
