using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Phase D-1 — Olaf runtime gate.
/// O-01/O-03/O-04/O-05를 0916 정본 기준으로 검증한다.
/// 전체 스킬 풀/최종 수치 데이터는 Phase E(O-02) 범위다.
/// </summary>
public sealed class PhaseDOlafGameSystemVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_d.olaf";
    public int Order => 500;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Runtime(
            "phased.olaf.o01.madness_core",
            "O-01",
            "올라프 광기 8 / 4단위 계약",
            GameSystemVerificationCategory.Resource,
            "광기 0~8, 4~7=+1, 8=+2 위력·받는피해, 만개=8",
            VerifyMadnessCore);

        yield return Runtime(
            "phased.olaf.o01.default_residue",
            "O-01",
            "올라프 평타/결투 기본 잔효과",
            GameSystemVerificationCategory.Status,
            "RED 평타 승리=출혈 1/2/3, BLUE 평타 승리=광기+1, Duel×Duel 패배=광기+1",
            VerifyDefaultResidue);

        yield return Runtime(
            "phased.olaf.o03.explicit_break_authority",
            "O-03",
            "올라프 파괴형 결투 명시 권한",
            GameSystemVerificationCategory.Damage,
            "표준=WeakenedOnly, 끈질기게(Rend legacy asset)=None; 현재 기세와 무관",
            VerifyExplicitBreakAuthority);

        yield return Runtime(
            "phased.olaf.o04.bloto",
            "O-04",
            "ShowOff → 블로토",
            GameSystemVerificationCategory.Status,
            "강한 도사림/빛1, 최저 정상 부위 약화(동점 팔→다리→머리), 광기+1, 적 공포 3턴",
            VerifyBloto);

        yield return Static(
            "phased.olaf.o05.rulebreaker_schema",
            "O-05",
            "올라프 룰브레이커 authoring 문법",
            GameSystemVerificationCategory.Contract,
            "동적 비용/조건부 파괴·선행면제/강제교체/modifier 면역/같은 target 포기/기세 조건/무한속도 데이터 필드 존재",
            VerifyRulebreakerSchema);

        yield return Runtime(
            "phased.olaf.o05.rulebreaker_runtime",
            "O-05",
            "올라프 룰브레이커 런타임",
            GameSystemVerificationCategory.Planning,
            "가변비용 최소1, 출혈 10+ 약화선행 면제, 현재 짓눌림 +6, 결투 매칭 강제교체, 공격슬롯 무한속도",
            VerifyRulebreakerRuntime);

        yield return Runtime(
            "phased.olaf.o05.fear_contract",
            "O-05",
            "올라프 공포 상태 계약",
            GameSystemVerificationCategory.Status,
            "위력 -1 / 3턴 / 비누적 / 재부여 시 duration refresh",
            VerifyFearContract);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(
            id,
            req,
            name,
            category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected,
            probe,
            true);

    private static GameSystemVerificationCase Static(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(
            id,
            req,
            name,
            category,
            GameSystemVerificationExecutionMode.StaticContract,
            expected,
            probe,
            true);

    private static bool Fixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out Olaf olaf,
        out GameSystemVerificationProbeResult failure)
    {
        olaf = null;
        if (!context.TryGetFixture(out fixture, out string reason))
        {
            failure = GameSystemVerificationProbeResult.Fail(reason);
            return false;
        }

        olaf = fixture.Player as Olaf;
        if (olaf == null)
        {
            failure = GameSystemVerificationProbeResult.Fail(
                $"Phase D Olaf Gate는 Player가 Olaf인 전투에서 실행해야 합니다. Player={fixture.Player?.GetType().Name ?? "NULL"}");
            return false;
        }

        failure = null;
        return true;
    }

    private static GameSystemVerificationProbeResult VerifyMadnessCore(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        OlafMadnessMechanic mechanic = olaf.MadnessMechanic;
        if (mechanic == null)
            return GameSystemVerificationProbeResult.Fail("OlafMadnessMechanic missing");

        DamageContext incoming = new DamageContext(
            DamageRequest.Direct(null, olaf, 10));

        mechanic.SetMadnessForDebug(0);
        bool zero =
            mechanic.CurrentMadness == 0 &&
            mechanic.MadnessStage == 0 &&
            mechanic.PassivePowerBonus == 0 &&
            mechanic.ModifyDamageTaken(incoming, 10) == 10 &&
            !mechanic.IsBlooming;

        mechanic.SetMadnessForDebug(4);
        bool four =
            mechanic.CurrentMadness == 4 &&
            mechanic.MadnessStage == 1 &&
            mechanic.PassivePowerBonus == 1 &&
            mechanic.ModifyDamageTaken(incoming, 10) == 11 &&
            mechanic.RedNormalBleedingGain == 2;

        mechanic.SetMadnessForDebug(8);
        bool eight =
            mechanic.CurrentMadness == 8 &&
            mechanic.MadnessStage == 2 &&
            mechanic.PassivePowerBonus == 2 &&
            mechanic.ModifyDamageTaken(incoming, 10) == 12 &&
            mechanic.RedNormalBleedingGain == 3 &&
            mechanic.IsBlooming;

        mechanic.AddMadness(99);
        bool clamp = mechanic.CurrentMadness == 8;

        bool ok =
            OlafMadnessMechanic.MaxMadnessValue == 8 &&
            OlafMadnessMechanic.MadnessPerStage == 4 &&
            zero && four && eight && clamp;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                "0:[P+0/D+0/Bleed1], 4:[P+1/D+1/Bleed2], 8:[P+2/D+2/Bleed3,Bloom]")
            : GameSystemVerificationProbeResult.Fail(
                $"Max={OlafMadnessMechanic.MaxMadnessValue}, Unit={OlafMadnessMechanic.MadnessPerStage}, 0={zero},4={four},8={eight},Clamp={clamp}");
    }

    private static GameSystemVerificationProbeResult VerifyDefaultResidue(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        SkillDefinition redDefinition = ScriptableObject.CreateInstance<SkillDefinition>();
        SkillDefinition blueDefinition = ScriptableObject.CreateInstance<SkillDefinition>();
        SkillDefinition duelDefinition = ScriptableObject.CreateInstance<SkillDefinition>();

        try
        {
            redDefinition.name = "VERIFY_OLAF_RED_NORMAL";
            redDefinition.ActionType = ActionType.NormalAttack;
            redDefinition.Color = SkillColor.Red;
            blueDefinition.name = "VERIFY_OLAF_BLUE_NORMAL";
            blueDefinition.ActionType = ActionType.NormalAttack;
            blueDefinition.Color = SkillColor.Blue;
            duelDefinition.name = "VERIFY_OLAF_DUEL";
            duelDefinition.ActionType = ActionType.Duel;
            duelDefinition.Color = SkillColor.Red;

            Skill red = olaf.CreateRuntimeSkillForLoadout(redDefinition);
            Skill blue = olaf.CreateRuntimeSkillForLoadout(blueDefinition);
            Skill duel = olaf.CreateRuntimeSkillForLoadout(duelDefinition);
            red.Initialize(olaf, f.Context._battleEvent);
            blue.Initialize(olaf, f.Context._battleEvent);
            duel.Initialize(olaf, f.Context._battleEvent);

            Character enemy = f.Enemy;
            BodyPart enemyPart = FirstUsablePart(enemy);
            BodyPart ownerPart = FirstUsablePart(olaf);
            Skill enemyDuel = enemy?.RuntimeSkills?.FirstOrDefault(item => item?.ActionType == ActionType.Duel);
            if (enemy == null || enemyDuel == null)
                return GameSystemVerificationProbeResult.Fail("Enemy Duel fixture missing");

            OlafMadnessMechanic madness = olaf.MadnessMechanic;
            madness.SetMadnessForDebug(0);
            RemoveBleeding(enemy, enemyPart);
            RaiseWonExchange(f, CreateAction(olaf, ownerPart, red, enemy, enemyPart, 910001), null, true);
            int redStage0 = GetBleeding(enemy, enemyPart);

            madness.SetMadnessForDebug(4);
            RemoveBleeding(enemy, enemyPart);
            RaiseWonExchange(f, CreateAction(olaf, ownerPart, red, enemy, enemyPart, 910002), null, true);
            int redStage1 = GetBleeding(enemy, enemyPart);

            madness.SetMadnessForDebug(8);
            RemoveBleeding(enemy, enemyPart);
            RaiseWonExchange(f, CreateAction(olaf, ownerPart, red, enemy, enemyPart, 910003), null, true);
            int redStage2 = GetBleeding(enemy, enemyPart);

            madness.SetMadnessForDebug(0);
            RaiseWonExchange(f, CreateAction(olaf, ownerPart, blue, enemy, enemyPart, 910004), null, true);
            int blueMadness = madness.CurrentMadness;

            madness.SetMadnessForDebug(0);
            BattleAction mine = CreateAction(olaf, ownerPart, duel, enemy, enemyPart, 910005);
            BattleAction theirs = CreateAction(enemy, enemyPart, enemyDuel, olaf, ownerPart, 910006);
            f.Context._battleEvent?.RaiseExchangeResolved(new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = theirs,
                WinnerAction = theirs,
                LoserAction = mine,
                IsDuelExchange = true,
                IsOneSided = false
            });
            int lostDuelMadness = madness.CurrentMadness;

            bool ok =
                redStage0 == 1 &&
                redStage1 == 2 &&
                redStage2 == 3 &&
                blueMadness == 1 &&
                lostDuelMadness == 1;

            return ok
                ? GameSystemVerificationProbeResult.Pass(
                    $"RED={redStage0}/{redStage1}/{redStage2}, BLUE Madness={blueMadness}, DuelLose Madness={lostDuelMadness}")
                : GameSystemVerificationProbeResult.Fail(
                    $"RED={redStage0}/{redStage1}/{redStage2}, BLUE Madness={blueMadness}, DuelLose Madness={lostDuelMadness}");
        }
        finally
        {
            DestroyVerificationObject(redDefinition);
            DestroyVerificationObject(blueDefinition);
            DestroyVerificationObject(duelDefinition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyExplicitBreakAuthority(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill standard = FindRuntimeSkill(olaf, OlafSkillIds.Standard);
        Skill rend = FindRuntimeSkill(olaf, OlafSkillIds.Rend);
        BodyPart targetPart = FirstUsablePart(f.Enemy) ?? FirstUsablePart(olaf);
        if (standard?.Definition == null || rend?.Definition == null || targetPart == null)
            return GameSystemVerificationProbeResult.Fail(
                $"Standard={standard != null}, Rend={rend != null}, TargetPart={targetPart != null}. Core Data migration을 확인하세요.");

        BattleAction standardAction = CreateAction(
            olaf,
            FirstUsablePart(olaf),
            standard,
            f.Enemy,
            FirstUsablePart(f.Enemy),
            920001);

        f.MomentumManager?.SetMomentumForDebug(-100);
        PartBreakMode atLastStand = standard.ResolvePartBreakMode(standardAction, targetPart);
        f.MomentumManager?.SetMomentumForDebug(100);
        PartBreakMode atOverwhelm = standard.ResolvePartBreakMode(standardAction, targetPart);

        bool ok =
            standard.Definition.CanBreakPart &&
            standard.Definition.BreakMode == PartBreakMode.WeakenedOnly &&
            atLastStand == PartBreakMode.WeakenedOnly &&
            atOverwhelm == PartBreakMode.WeakenedOnly &&
            !rend.Definition.CanBreakPart &&
            rend.Definition.BreakMode == PartBreakMode.None;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Standard={atLastStand}/{atOverwhelm}, Rend={rend.Definition.BreakMode}")
            : GameSystemVerificationProbeResult.Fail(
                $"StandardLegacy={standard.Definition.CanBreakPart}, StandardMode={standard.Definition.BreakMode}, " +
                $"Bands={atLastStand}/{atOverwhelm}, RendLegacy={rend.Definition.CanBreakPart}, RendMode={rend.Definition.BreakMode}");
    }

    private static GameSystemVerificationProbeResult VerifyBloto(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill bloto = FindRuntimeSkill(olaf, OlafSkillIds.Bloto);
        if (bloto?.Definition == null)
        {
            return GameSystemVerificationProbeResult.Fail(
                "olaf.preparation.bloto runtime skill missing. PhaseDOlafMigration이 기존 Olaf_ShowOff.asset을 이관했는지 확인하세요.");
        }

        BodyPart head = FindPart(olaf, PartType.HEAD);
        BodyPart left = FindPart(olaf, PartType.LEFT_HAND);
        BodyPart right = FindPart(olaf, PartType.RIGHT_HAND);
        BodyPart legs = FindPart(olaf, PartType.LEGS);
        if (head == null || left == null || right == null || legs == null)
            return GameSystemVerificationProbeResult.Fail("Olaf 4 body parts missing");

        foreach (BodyPart part in olaf.BodyParts)
            part?.SetDebugState(20, Mathf.Max(20f, part.MaxPartHP), false, false);

        olaf.MadnessMechanic.SetMadnessForDebug(0);
        RemoveFear(f.Enemy);

        ActionSlot slot = new ActionSlot
        {
            ActionId = 930001,
            Owner = olaf,
            Part = legs,
            Skill = bloto,
            Speed = 5
        };
        BattleAction action = new BattleAction { Slot = slot };
        bloto.Execute(action);

        OlafFearStatus fear = f.Enemy.GetStatus<OlafFearStatus>();
        bool ok =
            bloto.Definition.ActionType == ActionType.Preparation &&
            bloto.Definition.PreparationTier == PreparationTier.Strong &&
            bloto.EnergyCost == 1 &&
            left.IsWeakened &&
            !right.IsWeakened &&
            !legs.IsWeakened &&
            !head.IsWeakened &&
            olaf.MadnessMechanic.CurrentMadness == 1 &&
            fear != null &&
            fear.Stack == 1 &&
            fear.Duration == 3;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Part={left.Type}:{left.State}, Madness={olaf.MadnessMechanic.CurrentMadness}, Fear={fear.Stack}/{fear.Duration}, Cost={bloto.EnergyCost}")
            : GameSystemVerificationProbeResult.Fail(
                $"Tier={bloto.Definition.PreparationTier}, Cost={bloto.EnergyCost}, " +
                $"Parts L/R/Leg/H={left.State}/{right.State}/{legs.State}/{head.State}, " +
                $"Madness={olaf.MadnessMechanic.CurrentMadness}, Fear={(fear == null ? "NULL" : $"{fear.Stack}/{fear.Duration}")}");
    }

    private static GameSystemVerificationProbeResult VerifyRulebreakerSchema(
        GameSystemVerificationContext _)
    {
        SkillRulebreakerSettings rule = new SkillRulebreakerSettings
        {
            Enabled = true,
            EnergyCostReductionPerCommittedUse = 1,
            MinimumEnergyCost = 1,
            MirrorThisSkillToOpponentOnDuelMatch = true,
            IgnoreOwnerRollModifiers = true,
            SuppressFriendlyOneSidedHitsOnSameTargetSlot = true,
            ConsumeAllOwnerBleedingOnExecute = true,
            BreakAuthorityBleedingThreshold = 5,
            IgnoreWeakenPrerequisiteBleedingThreshold = 10,
            RequirePreviousTurnLastStand = true,
            ApplyCurrentLastStandPowerBonus = true,
            CurrentLastStandPowerBonus = 6,
            GrantInfiniteAttackSlotSpeedThisTurn = true
        };

        bool ok =
            rule.HasDynamicCost &&
            rule.ResolveMinimumEnergyCost() == 1 &&
            rule.MirrorThisSkillToOpponentOnDuelMatch &&
            rule.IgnoreOwnerRollModifiers &&
            rule.SuppressFriendlyOneSidedHitsOnSameTargetSlot &&
            rule.ConsumeAllOwnerBleedingOnExecute &&
            rule.BreakAuthorityBleedingThreshold == 5 &&
            rule.IgnoreWeakenPrerequisiteBleedingThreshold == 10 &&
            rule.RequirePreviousTurnLastStand &&
            rule.ApplyCurrentLastStandPowerBonus &&
            rule.CurrentLastStandPowerBonus == 6 &&
            rule.GrantInfiniteAttackSlotSpeedThisTurn &&
            Enum.IsDefined(typeof(PartBreakMode), PartBreakMode.IgnoreWeakenedPrerequisite);

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                "DynamicCost/Mirror/ModifierHook/Suppress/BreakThresholds/Momentum/InfiniteSpeed present")
            : GameSystemVerificationProbeResult.Fail("Rulebreaker schema mismatch");
    }

    private static GameSystemVerificationProbeResult VerifyRulebreakerRuntime(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        OlafRulebreakerMechanic mechanic = olaf.RulebreakerMechanic;
        if (mechanic == null)
            return GameSystemVerificationProbeResult.Fail("OlafRulebreakerMechanic missing");

        SkillDefinition dynamicDefinition = ScriptableObject.CreateInstance<SkillDefinition>();
        SkillDefinition infiniteDefinition = ScriptableObject.CreateInstance<SkillDefinition>();
        SkillDefinition normalDefinition = ScriptableObject.CreateInstance<SkillDefinition>();

        try
        {
            dynamicDefinition.name = "VERIFY_OLAF_RULEBREAKER";
            dynamicDefinition.ActionType = ActionType.Duel;
            dynamicDefinition.OverrideEnergyCost = true;
            dynamicDefinition.EnergyCost = 4;
            dynamicDefinition.Rulebreaker.Enabled = true;
            dynamicDefinition.Rulebreaker.EnergyCostReductionPerCommittedUse = 1;
            dynamicDefinition.Rulebreaker.MinimumEnergyCost = 1;
            dynamicDefinition.Rulebreaker.ConsumeAllOwnerBleedingOnExecute = true;
            dynamicDefinition.Rulebreaker.BreakAuthorityBleedingThreshold = 5;
            dynamicDefinition.Rulebreaker.IgnoreWeakenPrerequisiteBleedingThreshold = 10;
            dynamicDefinition.Rulebreaker.ApplyCurrentLastStandPowerBonus = true;
            dynamicDefinition.Rulebreaker.CurrentLastStandPowerBonus = 6;
            dynamicDefinition.Rulebreaker.MirrorThisSkillToOpponentOnDuelMatch = true;
            dynamicDefinition.Rulebreaker.SuppressFriendlyOneSidedHitsOnSameTargetSlot = true;

            int cost0 = mechanic.ResolveEnergyCost(dynamicDefinition, 4);
            mechanic.RecordCommittedUse(dynamicDefinition);
            int cost1 = mechanic.ResolveEnergyCost(dynamicDefinition, 4);
            mechanic.RecordCommittedUse(dynamicDefinition);
            int cost2 = mechanic.ResolveEnergyCost(dynamicDefinition, 4);
            mechanic.RecordCommittedUse(dynamicDefinition);
            int cost3 = mechanic.ResolveEnergyCost(dynamicDefinition, 4);
            mechanic.RecordCommittedUse(dynamicDefinition);
            int cost4 = mechanic.ResolveEnergyCost(dynamicDefinition, 4);

            Skill dynamicSkill = olaf.CreateRuntimeSkillForLoadout(dynamicDefinition);
            dynamicSkill.Initialize(olaf, f.Context._battleEvent);
            BodyPart ownerPart = FirstUsablePart(olaf);
            BodyPart enemyPart = FirstUsablePart(f.Enemy);
            ActionSlot sourceSlot = new ActionSlot
            {
                ActionId = 940001,
                Owner = olaf,
                Part = ownerPart,
                Skill = dynamicSkill,
                TargetCharacter = f.Enemy,
                TargetPart = enemyPart
            };
            BattleAction sourceAction = new BattleAction { Slot = sourceSlot };

            olaf.AddStatus(new Bleeding(10), olaf);
            f.MomentumManager?.SetMomentumForDebug(-70);
            mechanic.Execute(sourceAction);

            bool conditionalBreak =
                sourceAction.PartBreakModeOverride == PartBreakMode.IgnoreWeakenedPrerequisite &&
                olaf.GetStatus<Bleeding>() == null;
            bool currentBandPower = sourceAction.RulebreakerFlatPowerBonus == 6;

            Skill enemyOldSkill = f.Enemy.RuntimeSkills?.FirstOrDefault(item => item?.ActionType == ActionType.Duel) ??
                                  f.Enemy.RuntimeSkills?.FirstOrDefault();
            ActionSlot enemySlot = new ActionSlot
            {
                ActionId = 940002,
                Owner = f.Enemy,
                Part = enemyPart,
                Skill = enemyOldSkill,
                TargetCharacter = olaf,
                TargetPart = ownerPart
            };
            BattleAction enemyAction = new BattleAction { Slot = enemySlot };
            mechanic.NotifyDuelMatched(sourceAction, enemyAction);
            bool mirrored =
                enemySlot.Skill != null &&
                enemySlot.Skill.Definition == dynamicDefinition;

            normalDefinition.name = "VERIFY_OLAF_NORMAL";
            normalDefinition.ActionType = ActionType.NormalAttack;
            normalDefinition.Color = SkillColor.Red;
            Skill normalSkill = olaf.CreateRuntimeSkillForLoadout(normalDefinition);
            normalSkill.Initialize(olaf, f.Context._battleEvent);

            // 일기토의 포기 규칙은 해결 순서와 무관하게 pairing 전에 확정되어야 한다.
            sourceSlot.Phase = ActionPhase.COMBAT;
            sourceSlot.Speed = 5;
            sourceSlot.TargetSlot = enemySlot;
            enemySlot.Phase = ActionPhase.COMBAT;
            enemySlot.Speed = 4;
            enemySlot.TargetSlot = sourceSlot;
            ActionSlot siblingSlot = new ActionSlot
            {
                ActionId = 940005,
                Owner = olaf,
                Part = FindPart(olaf, PartType.RIGHT_HAND) ?? ownerPart,
                Skill = normalSkill,
                TargetCharacter = f.Enemy,
                TargetPart = enemyPart,
                TargetSlot = enemySlot,
                Phase = ActionPhase.COMBAT,
                Speed = 3
            };
            new ClashBuilder().BuildQueue(new[] { sourceSlot, siblingSlot, enemySlot });
            bool oneSidedSuppressed = siblingSlot.SuppressOneSidedResolution;

            f.ActionManager?.Clear();
            ActionSlot attackSlot = new ActionSlot
            {
                ActionId = 940003,
                Owner = olaf,
                Part = ownerPart,
                Skill = normalSkill,
                Speed = 3
            };
            f.ActionManager?.AddSlot(attackSlot);

            infiniteDefinition.name = "VERIFY_OLAF_INFINITY_PREP";
            infiniteDefinition.ActionType = ActionType.Preparation;
            infiniteDefinition.Rulebreaker.Enabled = true;
            infiniteDefinition.Rulebreaker.GrantInfiniteAttackSlotSpeedThisTurn = true;
            Skill infiniteSkill = olaf.CreateRuntimeSkillForLoadout(infiniteDefinition);
            infiniteSkill.Initialize(olaf, f.Context._battleEvent);
            BattleAction infiniteAction = new BattleAction
            {
                Slot = new ActionSlot
                {
                    ActionId = 940004,
                    Owner = olaf,
                    Part = FindPart(olaf, PartType.LEGS) ?? ownerPart,
                    Skill = infiniteSkill
                }
            };
            mechanic.Execute(infiniteAction);
            bool infiniteSpeed =
                mechanic.HasInfiniteAttackSpeedThisTurn &&
                attackSlot.Speed == OlafRulebreakerMechanic.InfiniteAttackSpeed;

            bool costs = cost0 == 4 && cost1 == 3 && cost2 == 2 && cost3 == 1 && cost4 == 1;
            bool ok = costs && conditionalBreak && currentBandPower && mirrored && oneSidedSuppressed && infiniteSpeed;

            return ok
                ? GameSystemVerificationProbeResult.Pass(
                    $"Cost={cost0}->{cost1}->{cost2}->{cost3}->{cost4}, Break={sourceAction.PartBreakModeOverride}, " +
                    $"Power+={sourceAction.RulebreakerFlatPowerBonus}, Mirror={mirrored}, Suppress={oneSidedSuppressed}, Speed={attackSlot.Speed}")
                : GameSystemVerificationProbeResult.Fail(
                    $"Costs={cost0}/{cost1}/{cost2}/{cost3}/{cost4}, Break={conditionalBreak}:{sourceAction.PartBreakModeOverride}, " +
                    $"Power={currentBandPower}:{sourceAction.RulebreakerFlatPowerBonus}, Mirror={mirrored}, Suppress={oneSidedSuppressed}, Speed={infiniteSpeed}:{attackSlot.Speed}");
        }
        finally
        {
            DestroyVerificationObject(dynamicDefinition);
            DestroyVerificationObject(infiniteDefinition);
            DestroyVerificationObject(normalDefinition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyFearContract(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Olaf olaf, out GameSystemVerificationProbeResult fail))
            return fail;

        Character enemy = f.Enemy;
        RemoveFear(enemy);
        enemy.AddStatus(new OlafFearStatus(), olaf);
        OlafFearStatus first = enemy.GetStatus<OlafFearStatus>();
        if (first == null)
            return GameSystemVerificationProbeResult.Fail("Fear apply failed");

        BattleAction fearProbe = new BattleAction
        {
            Slot = new ActionSlot
            {
                ActionId = 950001,
                Owner = enemy,
                Skill = enemy.RuntimeSkills?.FirstOrDefault(),
                Part = FirstUsablePart(enemy)
            }
        };
        int modified = first.ModifyRoll(fearProbe, 10);
        first.DecreaseDuration();
        int afterTick = first.Duration;
        enemy.AddStatus(new OlafFearStatus(), olaf);
        OlafFearStatus refreshed = enemy.GetStatus<OlafFearStatus>();

        int count = enemy.StatusEffects?.Count(effect => effect is OlafFearStatus) ?? 0;
        bool ok =
            modified == 9 &&
            afterTick == 2 &&
            refreshed != null &&
            refreshed.Duration == 3 &&
            refreshed.Stack == 1 &&
            count == 1;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Roll10->{modified}, Duration 3->{afterTick}->3, Count={count}, Stack={refreshed.Stack}")
            : GameSystemVerificationProbeResult.Fail(
                $"Roll={modified}, Duration={afterTick}/{refreshed?.Duration ?? -1}, Count={count}, Stack={refreshed?.Stack ?? -1}");
    }

    private static Skill FindRuntimeSkill(Olaf olaf, string skillId) =>
        olaf?.RuntimeSkills?.FirstOrDefault(
            skill => skill?.Definition?.SkillId == skillId);

    private static BodyPart FirstUsablePart(Character character) =>
        character?.BodyParts?.FirstOrDefault(part => part != null && !part.IsBroken);

    private static BodyPart FindPart(Character character, PartType type) =>
        character?.BodyParts?.FirstOrDefault(part => part?.Type == type);

    private static BattleAction CreateAction(
        Character owner,
        BodyPart ownerPart,
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
                Part = ownerPart,
                Skill = skill,
                TargetCharacter = target,
                TargetPart = targetPart,
                Speed = 5
            }
        };
    }

    private static void RaiseWonExchange(
        GameSystemVerificationFixture fixture,
        BattleAction mine,
        BattleAction opponent,
        bool oneSided)
    {
        fixture.Context?._battleEvent?.RaiseExchangeResolved(
            new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = opponent,
                WinnerAction = mine,
                LoserAction = opponent,
                IsDuelExchange = false,
                IsOneSided = oneSided
            });
    }

    private static int GetBleeding(Character target, BodyPart part)
    {
        if (target == null)
            return 0;
        return part != null
            ? target.GetPartStatus<Bleeding>(part)?.Stack ?? 0
            : target.GetStatus<Bleeding>()?.Stack ?? 0;
    }

    private static void RemoveBleeding(Character target, BodyPart part)
    {
        if (target == null)
            return;

        if (part != null)
        {
            Bleeding bleeding = target.GetPartStatus<Bleeding>(part);
            if (bleeding != null)
                target.RemovePartStatus(part, bleeding, StatusEffectRemoveReason.Manual);
            return;
        }

        Bleeding characterBleeding = target.GetStatus<Bleeding>();
        if (characterBleeding != null)
            target.RemoveStatus(characterBleeding, StatusEffectRemoveReason.Manual);
    }

    private static void RemoveFear(Character target)
    {
        OlafFearStatus fear = target?.GetStatus<OlafFearStatus>();
        if (fear != null)
            target.RemoveStatus(fear, StatusEffectRemoveReason.Manual);
    }

    private static void DestroyVerificationObject(UnityEngine.Object value)
    {
        if (value == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(value);
        else
            UnityEngine.Object.DestroyImmediate(value);
    }
}
