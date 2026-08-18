using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

/// <summary>
/// 캐릭터 개별 구현이 아니라 Project Abyss 전투 규칙 자체를 검증한다.
/// Focused Encounter의 속도/TargetSlot/합-일방공격, 부위 공유 속도,
/// ActionManager 계획 취소, 도사림 큐, 현재 Live Plan 일관성을 실제 런타임 클래스로 검사한다.
/// </summary>
public static class GameSystemVerificationRunner
{
    private sealed class ProbeResult
    {
        public bool Passed;
        public bool Skipped;
        public string Actual;
        public string Details;

        public static ProbeResult Pass(
            string actual,
            string details = null) =>
            new ProbeResult
            {
                Passed = true,
                Actual = actual,
                Details = details
            };

        public static ProbeResult Fail(
            string actual,
            string details = null) =>
            new ProbeResult
            {
                Passed = false,
                Actual = actual,
                Details = details
            };

        public static ProbeResult Skip(
            string actual,
            string details = null) =>
            new ProbeResult
            {
                Skipped = true,
                Actual = actual,
                Details = details
            };
    }

    private sealed class FocusedFixtureData
    {
        public Character Player;
        public Character Enemy;
        public BodyPart PrimaryPart;
        public BodyPart AlternatePart;
        public BodyPart EnemyPart;
        public Skill PlayerCombatSkill;
        public Skill EnemyCombatSkill;
    }

    public static GameSystemVerificationReport RunDataChecks()
    {
        DateTime started =
            DateTime.Now;

        GameSystemVerificationReport report =
            CreateReport(
                started,
                manager: null);

        RunDataCases(report);
        Complete(report, started);
        return report;
    }

    public static GameSystemVerificationReport RunLivePlanAudit(
        BattleManager manager)
    {
        DateTime started =
            DateTime.Now;

        GameSystemVerificationReport report =
            CreateReport(
                started,
                manager);

        RunCase(
            report,
            "system.live.manager_initialized",
            "BattleManager·Roster 초기화",
            GameSystemVerificationCategory.Lifecycle,
            "BattleManager Initialized + Player + Enemy 1명 이상",
            () => VerifyManagerInitialized(manager));

        if (manager != null &&
            manager.IsInitialized &&
            manager.BattleContext?.Player != null)
        {
            RunLiveStateCases(
                report,
                manager);
        }

        Complete(
            report,
            started);

        return report;
    }

    public static GameSystemVerificationReport RunFull(
        BattleManager manager)
    {
        DateTime started =
            DateTime.Now;

        GameSystemVerificationReport report =
            CreateReport(
                started,
                manager);

        RunDataCases(report);

        RunCase(
            report,
            "system.live.manager_initialized",
            "BattleManager·Roster 초기화",
            GameSystemVerificationCategory.Lifecycle,
            "BattleManager Initialized + Player + Enemy 1명 이상",
            () => VerifyManagerInitialized(manager));

        if (manager == null ||
            !manager.IsInitialized ||
            manager.BattleContext?.Player == null)
        {
            Complete(report, started);
            return report;
        }

        Character liveEnemy =
            manager.BattleContext.Enemies?
                .FirstOrDefault(
                    enemy =>
                        enemy != null &&
                        !enemy.IsDead) ??
            manager.BattleContext.Enemies?
                .FirstOrDefault(
                    enemy => enemy != null);

        if (liveEnemy == null)
        {
            AddError(
                report,
                "system.live.fixture",
                "System Verification Sandbox",
                GameSystemVerificationCategory.Lifecycle,
                "검증 가능한 Enemy가 필요합니다.",
                "Enemy=NULL");

            Complete(report, started);
            return report;
        }

        try
        {
            using GameSystemVerificationFixture fixture =
                GameSystemVerificationFixture.Create(
                    manager.BattleContext.Player,
                    liveEnemy);

            RunSandboxCases(
                report,
                fixture);
        }
        catch (Exception exception)
        {
            AddError(
                report,
                "system.live.fixture",
                "System Verification Sandbox",
                GameSystemVerificationCategory.Lifecycle,
                "현재 Roster 복제 Fixture 초기화",
                exception.Message,
                exception.ToString());
        }

        RunLiveStateCases(
            report,
            manager);

        Complete(report, started);
        return report;
    }

    private static void RunDataCases(
        GameSystemVerificationReport report)
    {
        RunCase(
            report,
            "system.data.bodypart_skill_access_matrix",
            "부위별 스킬 접근 Matrix",
            GameSystemVerificationCategory.Data,
            "HEAD=전체 / 양팔=일반·결투 / LEGS=도사림",
            VerifyBodyPartSkillAccessMatrix);

        RunCase(
            report,
            "system.data.phase_order",
            "전투 실행 페이즈 순서",
            GameSystemVerificationCategory.Phase,
            "PRETURN -> FORESIGHT -> COMBAT",
            VerifyPhaseOrder);

        RunCase(
            report,
            "system.speed.combat_descending_order",
            "COMBAT 속도 내림차순 실행",
            GameSystemVerificationCategory.Speed,
            "COMBAT은 높은 Speed부터 실행하며 동속은 ActionIndex/ActionId로 안정 정렬",
            VerifyCombatSpeedOrder);
    }

    private static void RunSandboxCases(
        GameSystemVerificationReport report,
        GameSystemVerificationFixture fixture)
    {
        FocusedFixtureData data =
            BuildFocusedFixtureData(fixture);

        RunCase(
            report,
            "system.speed.same_bodypart_same_speed",
            "같은 부위 다중 슬롯 공유 속도",
            GameSystemVerificationCategory.Speed,
            "같은 BodyPart의 ActionIndex 0/1은 항상 같은 Speed",
            () => VerifySharedPartSpeed(fixture));

        RunCase(
            report,
            "system.target.original_part_slower_can_clash",
            "원래 대상 부위의 느린 대응 합",
            GameSystemVerificationCategory.Targeting,
            "적이 노린 정확한 BodyPart 행동은 더 느려도 대응 합 가능",
            () => VerifyOriginalPartOpposition(data));

        RunCase(
            report,
            "system.target.crosspart_slower_redirect_fails",
            "다른 부위의 느린 가로채기 실패",
            GameSystemVerificationCategory.Targeting,
            "challengerSpeed < incomingSpeed 이면 일방공격",
            () => VerifyCrossPartRedirect(data, 9, 10, expected: false));

        RunCase(
            report,
            "system.target.crosspart_equal_redirect_fails",
            "동속 가로채기 실패",
            GameSystemVerificationCategory.Targeting,
            "challengerSpeed == incomingSpeed 이면 일방공격",
            () => VerifyCrossPartRedirect(data, 10, 10, expected: false));

        RunCase(
            report,
            "system.target.crosspart_faster_redirect_clashes",
            "더 빠른 가로채기 합",
            GameSystemVerificationCategory.Targeting,
            "challengerSpeed > incomingSpeed 이면 가로채기 합",
            () => VerifyCrossPartRedirect(data, 11, 10, expected: true));

        RunCase(
            report,
            "system.target.exact_targetslot_required",
            "Focused Encounter exact TargetSlot 필수",
            GameSystemVerificationCategory.Targeting,
            "TargetCharacter/TargetPart만 같아서는 합이 성립하지 않음",
            () => VerifyExactTargetSlotRequired(data));

        RunCase(
            report,
            "system.target.explicit_target_not_stolen",
            "명시 TargetSlot 자동 탈취 금지",
            GameSystemVerificationCategory.Targeting,
            "지정한 A와 합 실패해도 나를 노리는 B로 자동 재매칭하지 않음",
            () => VerifyExplicitTargetNotStolen(data));

        RunCase(
            report,
            "system.pairing.failed_redirect_two_onesided",
            "실패한 가로채기 = 양쪽 일방공격",
            GameSystemVerificationCategory.Pairing,
            "느린 제3자 지정은 Clash 0 / OneSided 2",
            () => VerifyFailedRedirectPairing(data));

        RunCase(
            report,
            "system.pairing.valid_redirect_one_clash",
            "성공한 가로채기 = 실제 합",
            GameSystemVerificationCategory.Pairing,
            "더 빠른 제3자 지정은 Clash 1",
            () => VerifySuccessfulRedirectPairing(data));

        RunCase(
            report,
            "system.pairing.single_target_single_clash",
            "하나의 적 행동은 한 합만 점유",
            GameSystemVerificationCategory.Pairing,
            "같은 적 ActionSlot을 여러 행동이 노려도 실제 Clash는 1개",
            () => VerifySingleTargetSingleClash(data));

        RunCase(
            report,
            "system.pairing.preview_does_not_mutate_links",
            "합 Preview는 계획 TargetSlot을 오염시키지 않음",
            GameSystemVerificationCategory.Pairing,
            "BuildClashPreview는 기존 exact TargetSlot을 유지하고 반대쪽 링크를 쓰지 않음",
            () => VerifyPreviewDoesNotMutateLinks(data));

        RunCase(
            report,
            "system.pairing.execution_links_actual_clash_only",
            "실행 큐는 실제 합만 상호 링크",
            GameSystemVerificationCategory.Pairing,
            "BuildQueue는 성립한 합만 mutual TargetSlot로 연결하고 실패한 redirect는 일방공격 타깃만 보존",
            () => VerifyExecutionTargetLinks(data));

        RunCase(
            report,
            "system.resource.plan_energy_overspend_rejected",
            "계획 에너지 초과 예약 거부",
            GameSystemVerificationCategory.Resource,
            "ActionManager는 현재 에너지보다 큰 누적 계획 비용을 등록하지 않음",
            () => VerifyEnergyOverspendRejected(fixture));

        RunCase(
            report,
            "system.planning.preparation_plan_cancel",
            "도사림 계획·취소·에너지 예약",
            GameSystemVerificationCategory.Planning,
            "START 전 효과/에너지 소비 없이 FORESIGHT 슬롯 계획, 취소 시 예약 해제",
            () => VerifyPreparationPlanning(fixture));

        RunCase(
            report,
            "system.phase.preparation_before_combat_queue",
            "도사림 큐가 COMBAT보다 선행",
            GameSystemVerificationCategory.Phase,
            "PreparationQueue와 ClashQueue가 분리되고 Resolve 순서는 PRETURN->FORESIGHT->COMBAT",
            () => VerifyPreparationQueue(fixture));
    }

    private static void RunLiveStateCases(
        GameSystemVerificationReport report,
        BattleManager manager)
    {
        RunCase(
            report,
            "system.live.actual_slot_speed_consistency",
            "현재 인게임 슬롯 속도 일관성",
            GameSystemVerificationCategory.LiveState,
            "모든 ActionSlot.Speed == 현재 Owner/BodyPart Speed, 동일 부위 슬롯은 동일",
            () => VerifyActualSlotSpeedConsistency(manager));

        RunCase(
            report,
            "system.live.player_exact_target_contract",
            "현재 플레이어 계획 exact TargetSlot",
            GameSystemVerificationCategory.LiveState,
            "플레이어 COMBAT 계획은 적 행동 ActionSlot을 명시적으로 지정",
            () => VerifyCurrentPlayerTargetContract(manager));

        RunCase(
            report,
            "system.live.preview_pair_policy_consistency",
            "현재 Preview 합과 백엔드 Policy 일치",
            GameSystemVerificationCategory.LiveState,
            "Preview의 모든 Clash pair가 ClashMatchPolicy.CanChallenge로 정당화됨",
            () => VerifyCurrentPreviewConsistency(manager));

        RunCase(
            report,
            "system.live.targeting_matrix_snapshot",
            "현재 타깃·속도·합 Matrix Snapshot",
            GameSystemVerificationCategory.LiveState,
            "현재 COMBAT ActionSlot의 속도, 선언 타깃, exact TargetSlot, Preview 합/일방공격을 진단용으로 기록",
            () => BuildCurrentTargetingMatrix(manager));
    }

    private static ProbeResult VerifyManagerInitialized(
        BattleManager manager)
    {
        int enemyCount =
            manager?.BattleContext?.Enemies?
                .Count(enemy => enemy != null) ?? 0;

        bool valid =
            manager != null &&
            manager.IsInitialized &&
            manager.BattleContext != null &&
            manager.BattleContext.Player != null &&
            enemyCount > 0 &&
            manager.ActionManager != null &&
            manager.SpeedManager != null &&
            manager.ClashBuilder != null;

        string actual =
            $"Manager={manager != null}, Initialized={manager?.IsInitialized}, " +
            $"Player={manager?.BattleContext?.Player?.Data?.CharacterName ?? "NULL"}, " +
            $"Enemies={enemyCount}, ActionManager={manager?.ActionManager != null}, " +
            $"SpeedManager={manager?.SpeedManager != null}, ClashBuilder={manager?.ClashBuilder != null}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyBodyPartSkillAccessMatrix()
    {
        List<string> failures =
            new List<string>();

        PartType[] parts =
        {
            PartType.HEAD,
            PartType.LEFT_HAND,
            PartType.RIGHT_HAND,
            PartType.LEGS
        };

        ActionType[] actions =
        {
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige
        };

        foreach (PartType part in parts)
        {
            foreach (ActionType action in actions)
            {
                bool expected =
                    part switch
                    {
                        PartType.HEAD => true,
                        PartType.LEFT_HAND =>
                            action == ActionType.NormalAttack ||
                            action == ActionType.Duel,
                        PartType.RIGHT_HAND =>
                            action == ActionType.NormalAttack ||
                            action == ActionType.Duel,
                        PartType.LEGS =>
                            action == ActionType.Preparation,
                        _ => false
                    };

                bool actual =
                    BodyPartSkillAccessPolicy.Allows(
                        part,
                        action);

                if (actual != expected)
                {
                    failures.Add(
                        $"{part}/{action}: {actual}, expected {expected}");
                }
            }
        }

        return failures.Count == 0
            ? ProbeResult.Pass("16/16 matrix PASS")
            : ProbeResult.Fail(
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static ProbeResult VerifyPhaseOrder()
    {
        ActionPhaseSorter sorter =
            new ActionPhaseSorter();

        ActionSlot prestige =
            new ActionSlot
            {
                ActionId = 1,
                Phase = ActionPhase.PRETURN,
                Speed = 1
            };

        ActionSlot preparation =
            new ActionSlot
            {
                ActionId = 2,
                Phase = ActionPhase.FORESIGHT,
                Speed = 99
            };

        ActionSlot combat =
            new ActionSlot
            {
                ActionId = 3,
                Phase = ActionPhase.COMBAT,
                Speed = 999
            };

        List<ActionSlot> ordered =
            new List<ActionSlot>
            {
                combat,
                preparation,
                prestige
            };

        ordered.Sort(
            sorter.CompareForExecution);

        bool valid =
            ordered.Count == 3 &&
            ordered[0] == prestige &&
            ordered[1] == preparation &&
            ordered[2] == combat;

        return valid
            ? ProbeResult.Pass("PRETURN -> FORESIGHT -> COMBAT")
            : ProbeResult.Fail(
                string.Join(
                    " -> ",
                    ordered.Select(slot => slot.Phase.ToString())));
    }

    private static ProbeResult VerifyCombatSpeedOrder()
    {
        ActionPhaseSorter sorter =
            new ActionPhaseSorter();

        ActionSlot slow =
            new ActionSlot
            {
                ActionId = 30,
                ActionIndex = 0,
                Phase = ActionPhase.COMBAT,
                Speed = 3
            };

        ActionSlot fastIndex1 =
            new ActionSlot
            {
                ActionId = 20,
                ActionIndex = 1,
                Phase = ActionPhase.COMBAT,
                Speed = 9
            };

        ActionSlot fastIndex0LaterId =
            new ActionSlot
            {
                ActionId = 11,
                ActionIndex = 0,
                Phase = ActionPhase.COMBAT,
                Speed = 9
            };

        ActionSlot fastIndex0EarlierId =
            new ActionSlot
            {
                ActionId = 10,
                ActionIndex = 0,
                Phase = ActionPhase.COMBAT,
                Speed = 9
            };

        List<ActionSlot> ordered =
            new List<ActionSlot>
            {
                slow,
                fastIndex1,
                fastIndex0LaterId,
                fastIndex0EarlierId
            };

        ordered.Sort(
            sorter.CompareForExecution);

        bool valid =
            ordered.Count == 4 &&
            ordered[0] == fastIndex0EarlierId &&
            ordered[1] == fastIndex0LaterId &&
            ordered[2] == fastIndex1 &&
            ordered[3] == slow;

        string actual =
            string.Join(
                " -> ",
                ordered.Select(
                    slot =>
                        $"S{slot.Speed}/I{slot.ActionIndex}/A{slot.ActionId}"));

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifySharedPartSpeed(
        GameSystemVerificationFixture fixture)
    {
        Character player =
            fixture?.Player;

        BodyPart part =
            player?.BodyParts?
                .FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        !candidate.IsBroken);

        if (player == null ||
            part == null ||
            fixture.SpeedManager == null)
        {
            return ProbeResult.Skip(
                "공유 속도 검사 가능한 Player/Part 없음");
        }

        ActionSlot first =
            new ActionSlot
            {
                Owner = player,
                Part = part,
                ActionIndex = 0,
                Speed = 10
            };

        ActionSlot second =
            new ActionSlot
            {
                Owner = player,
                Part = part,
                ActionIndex = 1,
                Speed = 10
            };

        player.ConfigureActionSlot(first);
        player.ConfigureActionSlot(second);

        bool configurePreserved =
            first.Speed == 10 &&
            second.Speed == 10;

        fixture.SpeedManager.SetSpeedForDebug(
            player,
            part,
            10);

        fixture.SpeedManager.ApplySpeedToSlots(
            new[] { first, second });

        bool ten =
            first.Speed == 10 &&
            second.Speed == 10;

        fixture.SpeedManager.SetSpeedForDebug(
            player,
            part,
            14);

        fixture.SpeedManager.ApplySpeedToSlots(
            new[] { first, second });

        bool fourteen =
            first.Speed == 14 &&
            second.Speed == 14;

        bool valid =
            configurePreserved &&
            ten &&
            fourteen;

        string actual =
            $"Part={part.Type}, Configure={configurePreserved}, " +
            $"10={ten}, 14={fourteen}, Final={first.Speed}/{second.Speed}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyOriginalPartOpposition(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                speed: 15,
                actionId: 900001);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.PrimaryPart,
                speed: 5,
                incoming,
                actionId: 900002);

        bool can =
            NewPolicy().CanChallenge(
                challenger,
                incoming);

        return can
            ? ProbeResult.Pass(
                "Player 5 vs Enemy 15 / exact original BodyPart => Clash")
            : ProbeResult.Fail(
                "Player 5 vs Enemy 15 / exact original BodyPart => OneSided");
    }

    private static ProbeResult VerifyCrossPartRedirect(
        FocusedFixtureData data,
        int challengerSpeed,
        int incomingSpeed,
        bool expected)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                incomingSpeed,
                901001);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                challengerSpeed,
                incoming,
                901002);

        bool can =
            NewPolicy().CanChallenge(
                challenger,
                incoming);

        string actual =
            $"Challenger={challengerSpeed}, Incoming={incomingSpeed}, " +
            $"CrossPart={data.AlternatePart.Type}->{data.PrimaryPart.Type}, CanChallenge={can}";

        return can == expected
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyExactTargetSlotRequired(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                902001);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.PrimaryPart,
                20,
                incoming,
                902002);

        challenger.TargetSlot = null;

        bool can =
            NewPolicy().CanChallenge(
                challenger,
                incoming);

        return !can
            ? ProbeResult.Pass(
                "TargetCharacter/TargetPart only => CanChallenge=false")
            : ProbeResult.Fail(
                "TargetSlot=NULL인데 CanChallenge=true");
    }

    private static ProbeResult VerifyExplicitTargetNotStolen(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot chosenA =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                903001);

        ActionSlot unrelatedB =
            CreateIncoming(
                data,
                data.AlternatePart,
                10,
                903002);

        ActionSlot source =
            CreateChallenger(
                data,
                data.AlternatePart,
                5,
                chosenA,
                903003);

        ClashMatchPolicy policy =
            NewPolicy();

        ActionSlot match =
            policy.FindBestMatch(
                source,
                new[]
                {
                    source,
                    chosenA,
                    unrelatedB
                },
                new HashSet<ActionSlot>());

        bool wouldClashWithB =
            ClashMatchPolicy.CanRedirectOrOppose(
                source.Owner,
                source.Part,
                source.Speed,
                unrelatedB,
                out _);

        bool valid =
            match == null &&
            wouldClashWithB;

        string actual =
            $"ChosenAValid={policy.CanChallenge(source, chosenA)}, " +
            $"BWouldOppose={wouldClashWithB}, Match={(match == null ? "NULL" : match.ActionId.ToString())}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(
                actual,
                "A에 대한 느린 가로채기 실패 후 B로 자동 재매칭되면 안 됩니다.");
    }

    private static ProbeResult VerifyFailedRedirectPairing(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                904001);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                5,
                incoming,
                904002);

        ActionPairingResult result =
            new ClashBuilder()
                .BuildPairingResult(
                    new[]
                    {
                        incoming,
                        challenger
                    });

        int clashCount =
            result.Pairs.Count(pair => pair?.IsClash == true);

        int oneSideCount =
            result.Pairs.Count(pair => pair != null && !pair.IsClash);

        bool valid =
            clashCount == 0 &&
            oneSideCount == 2;

        string actual =
            $"Pairs={result.Count}, Clash={clashCount}, OneSided={oneSideCount}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifySuccessfulRedirectPairing(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                905001);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                11,
                incoming,
                905002);

        ActionPairingResult result =
            new ClashBuilder()
                .BuildPairingResult(
                    new[]
                    {
                        incoming,
                        challenger
                    });

        int clashCount =
            result.Pairs.Count(pair => pair?.IsClash == true);

        int oneSideCount =
            result.Pairs.Count(pair => pair != null && !pair.IsClash);

        bool valid =
            result.Count == 1 &&
            clashCount == 1 &&
            oneSideCount == 0;

        string actual =
            $"Pairs={result.Count}, Clash={clashCount}, OneSided={oneSideCount}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifySingleTargetSingleClash(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                906001);

        ActionSlot fastRedirect =
            CreateChallenger(
                data,
                data.AlternatePart,
                12,
                incoming,
                906002);

        ActionSlot originalResponse =
            CreateChallenger(
                data,
                data.PrimaryPart,
                5,
                incoming,
                906003);

        originalResponse.ActionIndex = 1;

        ActionPairingResult result =
            new ClashBuilder()
                .BuildPairingResult(
                    new[]
                    {
                        incoming,
                        fastRedirect,
                        originalResponse
                    });

        int clashCount =
            result.Pairs.Count(pair => pair?.IsClash == true);

        int oneSideCount =
            result.Pairs.Count(pair => pair != null && !pair.IsClash);

        bool incomingUsedOnce =
            result.Pairs.Count(
                pair =>
                    pair != null &&
                    (pair.First == incoming ||
                     pair.Second == incoming)) == 1;

        bool valid =
            result.Count == 2 &&
            clashCount == 1 &&
            oneSideCount == 1 &&
            incomingUsedOnce;

        string actual =
            $"Pairs={result.Count}, Clash={clashCount}, " +
            $"OneSided={oneSideCount}, IncomingUseCount=" +
            result.Pairs.Count(
                pair =>
                    pair != null &&
                    (pair.First == incoming || pair.Second == incoming));

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyPreviewDoesNotMutateLinks(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot incoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                906101);

        ActionSlot challenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                11,
                incoming,
                906102);

        ActionSlot before =
            challenger.TargetSlot;

        IReadOnlyList<ClashPair> preview =
            new ClashBuilder()
                .BuildClashPreview(
                    new[]
                    {
                        incoming,
                        challenger
                    });

        bool valid =
            preview.Count == 1 &&
            preview[0]?.IsClash == true &&
            challenger.TargetSlot == before &&
            incoming.TargetSlot == null;

        string actual =
            $"Pairs={preview.Count}, Clash={preview.FirstOrDefault()?.IsClash}, " +
            $"PlayerTargetPreserved={challenger.TargetSlot == before}, " +
            $"IncomingMutated={incoming.TargetSlot != null}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyExecutionTargetLinks(
        FocusedFixtureData data)
    {
        if (!TryValidateFocusedFixture(
                data,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        ActionSlot clashIncoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                906201);

        ActionSlot clashChallenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                11,
                clashIncoming,
                906202);

        ActionExecutionQueue clashQueue =
            new ClashBuilder()
                .BuildQueue(
                    new[]
                    {
                        clashIncoming,
                        clashChallenger
                    });

        bool clashLinked =
            clashQueue.ClashQueue.Count == 1 &&
            clashQueue.ClashQueue.Peek()?.IsClash == true &&
            clashChallenger.TargetSlot == clashIncoming &&
            clashIncoming.TargetSlot == clashChallenger;

        ActionSlot failedIncoming =
            CreateIncoming(
                data,
                data.PrimaryPart,
                10,
                906203);

        ActionSlot failedChallenger =
            CreateChallenger(
                data,
                data.AlternatePart,
                5,
                failedIncoming,
                906204);

        Character declaredTarget =
            failedChallenger.TargetCharacter;

        BodyPart declaredPart =
            failedChallenger.TargetPart;

        ActionExecutionQueue failedQueue =
            new ClashBuilder()
                .BuildQueue(
                    new[]
                    {
                        failedIncoming,
                        failedChallenger
                    });

        int failedClashes =
            failedQueue.ClashQueue.Count(
                pair => pair?.IsClash == true);

        int failedOneSided =
            failedQueue.ClashQueue.Count(
                pair => pair != null && !pair.IsClash);

        bool failedPreserved =
            failedClashes == 0 &&
            failedOneSided == 2 &&
            failedChallenger.TargetSlot == null &&
            failedChallenger.TargetCharacter == declaredTarget &&
            failedChallenger.TargetPart == declaredPart;

        bool valid =
            clashLinked &&
            failedPreserved;

        string actual =
            $"ValidClashLinked={clashLinked}, FailedRedirectClash={failedClashes}, " +
            $"FailedRedirectOneSided={failedOneSided}, " +
            $"DeclaredTargetPreserved={failedChallenger.TargetCharacter == declaredTarget && failedChallenger.TargetPart == declaredPart}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyEnergyOverspendRejected(
        GameSystemVerificationFixture fixture)
    {
        Character player =
            fixture?.Player;

        if (player == null)
            return ProbeResult.Skip("Player=NULL");

        if (player.CurrentEnergy <= 0)
            player.AddEnergy(player.MaxEnergy);

        int budget =
            player.CurrentEnergy;

        Skill skill =
            player.RuntimeSkills?
                .Where(
                    candidate =>
                        candidate != null &&
                        candidate.CanClash &&
                        candidate.EnergyCost > 0)
                .OrderByDescending(
                    candidate => candidate.EnergyCost)
                .FirstOrDefault();

        if (skill == null)
        {
            return ProbeResult.Skip(
                "에너지 비용이 있는 COMBAT Skill 없음");
        }

        List<BodyPart> parts =
            player.BodyParts?
                .Where(
                    part =>
                        part != null &&
                        !part.IsBroken &&
                        BodyPartSkillAccessPolicy.Allows(
                            part,
                            skill.ActionType))
                .ToList() ??
            new List<BodyPart>();

        int requiredSlots =
            budget / skill.EnergyCost +
            1;

        if (requiredSlots < 2)
            requiredSlots = 2;

        if (parts.Count < requiredSlots ||
            requiredSlots > player.GetMaxActionSlots())
        {
            return ProbeResult.Skip(
                $"Overspend 재현 슬롯 부족 / Skill={skill.SkillName}, " +
                $"Cost={skill.EnergyCost}, Energy={budget}, " +
                $"Need={requiredSlots}, Parts={parts.Count}, MaxSlots={player.GetMaxActionSlots()}");
        }

        ActionManager manager =
            new ActionManager();

        try
        {
            int added = 0;
            bool rejected = false;

            for (int i = 0; i < requiredSlots; i++)
            {
                ActionSlot slot =
                    new ActionSlot
                    {
                        Owner = player,
                        Part = parts[i],
                        Skill = skill,
                        ActionIndex = 0,
                        Phase = ActionPhase.COMBAT,
                        TargetCharacter = fixture.Enemy
                    };

                bool accepted =
                    manager.TryAddOrReplaceSlot(
                        slot);

                if (accepted)
                    added++;
                else
                {
                    rejected = true;
                    break;
                }
            }

            int planned =
                manager.GetPlannedEnergyCost(
                    player);

            bool valid =
                rejected &&
                planned <= budget &&
                added == requiredSlots - 1;

            string actual =
                $"Skill={skill.SkillName}, Cost={skill.EnergyCost}, Energy={budget}, " +
                $"Attempt={requiredSlots}, Added={added}, Rejected={rejected}, Planned={planned}";

            return valid
                ? ProbeResult.Pass(actual)
                : ProbeResult.Fail(actual);
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static ProbeResult VerifyPreparationPlanning(
        GameSystemVerificationFixture fixture)
    {
        if (!TryFindPreparation(
                fixture,
                out Skill preparation,
                out BodyPart part,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        Character player =
            fixture.Player;

        if (player.CurrentEnergy < preparation.EnergyCost)
            player.AddEnergy(player.MaxEnergy);

        int energyBefore =
            player.CurrentEnergy;

        ActionManager manager =
            new ActionManager();

        try
        {
            ActionSlot slot =
                CreatePreparationSlot(
                    player,
                    part,
                    preparation,
                    fixture.SpeedManager.GetSpeed(player, part),
                    907001);

            bool added =
                manager.TryAddOrReplaceSlot(slot);

            int energyAfterPlan =
                player.CurrentEnergy;

            int reserved =
                manager.GetPlannedEnergyCost(player);

            bool removed =
                manager.RemoveSlot(
                    player,
                    part,
                    0);

            int energyAfterCancel =
                player.CurrentEnergy;

            int reservedAfter =
                manager.GetPlannedEnergyCost(player);

            bool valid =
                added &&
                removed &&
                slot.Phase == ActionPhase.FORESIGHT &&
                energyAfterPlan == energyBefore &&
                energyAfterCancel == energyBefore &&
                reserved == preparation.EnergyCost &&
                reservedAfter == 0;

            string actual =
                $"Skill={preparation.SkillName}, Part={part.Type}, " +
                $"Add/Remove={added}/{removed}, Energy={energyBefore}->{energyAfterPlan}->{energyAfterCancel}, " +
                $"Reserved={reserved}->{reservedAfter}, Phase={slot.Phase}";

            return valid
                ? ProbeResult.Pass(actual)
                : ProbeResult.Fail(actual);
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static ProbeResult VerifyPreparationQueue(
        GameSystemVerificationFixture fixture)
    {
        if (!TryFindPreparation(
                fixture,
                out Skill preparation,
                out BodyPart preparationPart,
                out string reason))
        {
            return ProbeResult.Skip(reason);
        }

        Character player =
            fixture.Player;

        Skill combat =
            player.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill != null &&
                        skill.CanClash);

        BodyPart combatPart =
            player.BodyParts?
                .FirstOrDefault(
                    part =>
                        part != null &&
                        !part.IsBroken &&
                        combat != null &&
                        BodyPartSkillAccessPolicy.Allows(
                            part,
                            combat.ActionType));

        if (combat == null ||
            combatPart == null)
        {
            return ProbeResult.Skip(
                "PreparationQueue 비교용 COMBAT Skill/Part 없음");
        }

        BodyPart enemyPart =
            fixture.Enemy.IsSingleHpTarget
                ? null
                : fixture.Enemy.BodyParts?
                    .FirstOrDefault(
                        part =>
                            part != null &&
                            !part.IsBroken);

        ActionSlot prepSlot =
            CreatePreparationSlot(
                player,
                preparationPart,
                preparation,
                fixture.SpeedManager.GetSpeed(player, preparationPart),
                908001);

        ActionSlot combatSlot =
            new ActionSlot
            {
                ActionId = 908002,
                Owner = player,
                Part = combatPart,
                Skill = combat,
                Speed = fixture.SpeedManager.GetSpeed(player, combatPart),
                ActionIndex = 0,
                Phase = ActionPhase.COMBAT,
                TargetCharacter = fixture.Enemy,
                TargetPart = enemyPart,
                TargetSlot = null
            };

        ActionExecutionQueue queue =
            new ClashBuilder()
                .BuildQueue(
                    new[]
                    {
                        combatSlot,
                        prepSlot
                    });

        bool valid =
            queue.PrestigeQueue.Count == 0 &&
            queue.PreparationQueue.Count == 1 &&
            queue.PreparationQueue.Peek() == prepSlot &&
            queue.ClashQueue.Count == 1 &&
            queue.ClashQueue.Peek()?.First == combatSlot &&
            queue.ClashQueue.Peek()?.IsClash == false;

        string actual =
            $"Prestige={queue.PrestigeQueue.Count}, Preparation={queue.PreparationQueue.Count}, " +
            $"CombatPairs={queue.ClashQueue.Count}, CombatIsClash={queue.ClashQueue.Peek()?.IsClash}";

        return valid
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(actual);
    }

    private static ProbeResult VerifyActualSlotSpeedConsistency(
        BattleManager manager)
    {
        IReadOnlyList<ActionSlot> slots =
            manager?.ActionManager?.Slots;

        if (slots == null ||
            slots.Count == 0)
        {
            return ProbeResult.Skip(
                "현재 ActionSlot이 없습니다. Turn Start/Planning 중 다시 실행하세요.");
        }

        List<string> failures =
            new List<string>();

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Owner == null)
                continue;

            int expected =
                manager.SpeedManager.GetSpeed(
                    slot.Owner,
                    slot.Part);

            if (slot.Speed != expected)
            {
                failures.Add(
                    $"Id={slot.ActionId} {CharacterName(slot.Owner)}/{PartName(slot.Part)} " +
                    $"SlotSpeed={slot.Speed}, Current={expected}");
            }
        }

        var groups =
            slots
                .Where(slot => slot?.Owner != null)
                .GroupBy(slot => new
                {
                    slot.Owner,
                    slot.Part
                });

        foreach (var group in groups)
        {
            int distinct =
                group.Select(slot => slot.Speed)
                    .Distinct()
                    .Count();

            if (distinct > 1)
            {
                failures.Add(
                    $"동일 부위 속도 분산: {CharacterName(group.Key.Owner)}/" +
                    $"{PartName(group.Key.Part)} => " +
                    string.Join(", ", group.Select(slot => slot.Speed)));
            }
        }

        return failures.Count == 0
            ? ProbeResult.Pass(
                $"Slots={slots.Count}, 모든 슬롯 현재 Speed와 일치")
            : ProbeResult.Fail(
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static ProbeResult VerifyCurrentPlayerTargetContract(
        BattleManager manager)
    {
        Character player =
            manager?.BattleContext?.Player;

        IReadOnlyList<ActionSlot> slots =
            manager?.ActionManager?.Slots;

        if (player == null ||
            slots == null)
        {
            return ProbeResult.Skip(
                "현재 Player/ActionManager 없음");
        }

        List<ActionSlot> combatPlans =
            slots
                .Where(
                    slot =>
                        slot?.Owner == player &&
                        slot.Phase == ActionPhase.COMBAT &&
                        slot.TargetCharacter != null &&
                        slot.TargetCharacter != player)
                .ToList();

        if (combatPlans.Count == 0)
        {
            return ProbeResult.Skip(
                "현재 플레이어 COMBAT 계획이 없습니다. 스킬 타깃 지정 후 다시 실행하면 실제 계획까지 검사합니다.");
        }

        List<string> failures =
            new List<string>();

        foreach (ActionSlot slot in combatPlans)
        {
            if (slot.TargetSlot == null)
            {
                failures.Add(
                    $"Id={slot.ActionId} {PartName(slot.Part)} / " +
                    $"{slot.Skill?.SkillName}: TargetSlot=NULL");

                continue;
            }

            if (!slots.Contains(slot.TargetSlot))
            {
                failures.Add(
                    $"Id={slot.ActionId}: TargetSlot이 현재 ActionManager에 없음 / " +
                    $"TargetId={slot.TargetSlot.ActionId}");
            }

            if (slot.TargetSlot.Owner == null ||
                slot.TargetSlot.Owner == player ||
                slot.TargetSlot.Phase != ActionPhase.COMBAT)
            {
                failures.Add(
                    $"Id={slot.ActionId}: 잘못된 TargetSlot / " +
                    $"Owner={CharacterName(slot.TargetSlot.Owner)}, Phase={slot.TargetSlot.Phase}");
            }

            if (slot.TargetCharacter != slot.TargetSlot.Owner ||
                slot.TargetPart != slot.TargetSlot.Part)
            {
                failures.Add(
                    $"Id={slot.ActionId}: 선언 Target과 exact TargetSlot 불일치 / " +
                    $"Declared={CharacterName(slot.TargetCharacter)}/{PartName(slot.TargetPart)}, " +
                    $"Exact={CharacterName(slot.TargetSlot.Owner)}/{PartName(slot.TargetSlot.Part)}");
            }
        }

        return failures.Count == 0
            ? ProbeResult.Pass(
                $"Player COMBAT Plans={combatPlans.Count}, exact TargetSlot valid={combatPlans.Count}")
            : ProbeResult.Fail(
                $"TargetSlot 계약 위반 {failures.Count}건 / Plans={combatPlans.Count}",
                string.Join("\n", failures));
    }

    private static ProbeResult VerifyCurrentPreviewConsistency(
        BattleManager manager)
    {
        IReadOnlyList<ActionSlot> slots =
            manager?.ActionManager?.Slots;

        if (slots == null ||
            slots.Count == 0)
        {
            return ProbeResult.Skip(
                "현재 ActionSlot 없음");
        }

        IReadOnlyList<ClashPair> preview =
            manager.ClashBuilder.BuildClashPreview(
                slots);

        ClashMatchPolicy policy =
            NewPolicy();

        List<string> failures =
            new List<string>();

        HashSet<ActionSlot> used =
            new HashSet<ActionSlot>();

        int clashCount = 0;
        int oneSideCount = 0;

        foreach (ClashPair pair in preview)
        {
            if (pair == null ||
                pair.First == null)
            {
                failures.Add("NULL Pair/First");
                continue;
            }

            if (!used.Add(pair.First))
            {
                failures.Add(
                    $"ActionSlot 중복 사용: {pair.First.ActionId}");
            }

            if (!pair.IsClash)
            {
                oneSideCount++;
                continue;
            }

            clashCount++;

            if (!used.Add(pair.Second))
            {
                failures.Add(
                    $"ActionSlot 중복 사용: {pair.Second.ActionId}");
            }

            bool forward =
                policy.CanChallenge(
                    pair.First,
                    pair.Second);

            bool reverse =
                policy.CanChallenge(
                    pair.Second,
                    pair.First);

            if (!forward && !reverse)
            {
                failures.Add(
                    $"정당화되지 않은 Clash: {pair.First.ActionId}<->{pair.Second.ActionId}");
            }
        }

        string actual =
            $"PreviewPairs={preview.Count}, Clash={clashCount}, OneSided={oneSideCount}, " +
            $"UsedSlots={used.Count}";

        return failures.Count == 0
            ? ProbeResult.Pass(actual)
            : ProbeResult.Fail(
                actual,
                string.Join("\n", failures));
    }

    private static ProbeResult BuildCurrentTargetingMatrix(
        BattleManager manager)
    {
        IReadOnlyList<ActionSlot> slots =
            manager?.ActionManager?.Slots;

        if (slots == null ||
            slots.Count == 0)
        {
            return ProbeResult.Skip(
                "현재 ActionSlot 없음");
        }

        List<ActionSlot> combat =
            slots
                .Where(
                    slot =>
                        slot != null &&
                        slot.Phase == ActionPhase.COMBAT)
                .OrderByDescending(slot => slot.Speed)
                .ThenBy(slot => slot.ActionIndex)
                .ThenBy(slot => slot.ActionId)
                .ToList();

        if (combat.Count == 0)
        {
            return ProbeResult.Skip(
                "현재 COMBAT ActionSlot 없음");
        }

        IReadOnlyList<ClashPair> preview =
            manager.ClashBuilder.BuildClashPreview(
                slots);

        Dictionary<ActionSlot, ClashPair> pairBySlot =
            new Dictionary<ActionSlot, ClashPair>();

        foreach (ClashPair pair in preview)
        {
            if (pair?.First != null)
                pairBySlot[pair.First] = pair;

            if (pair?.Second != null)
                pairBySlot[pair.Second] = pair;
        }

        ClashMatchPolicy policy =
            NewPolicy();

        List<string> lines =
            new List<string>();

        foreach (ActionSlot slot in combat)
        {
            string exact =
                slot.TargetSlot == null
                    ? "NULL"
                    : $"{slot.TargetSlot.ActionId}:{CharacterName(slot.TargetSlot.Owner)}/{PartName(slot.TargetSlot.Part)}";

            string pairText =
                "UNPAIRED";

            if (pairBySlot.TryGetValue(
                    slot,
                    out ClashPair pair))
            {
                if (pair.IsClash)
                {
                    ActionSlot other =
                        pair.First == slot
                            ? pair.Second
                            : pair.First;

                    pairText =
                        $"CLASH({other?.ActionId})";
                }
                else
                {
                    pairText =
                        "ONE_SIDED";
                }
            }

            string challenge =
                "N/A";

            if (slot.TargetSlot != null)
            {
                challenge =
                    policy.CanChallenge(
                        slot,
                        slot.TargetSlot)
                        ? "CAN_CLASH"
                        : "CANNOT_CLASH";
            }

            lines.Add(
                $"Id={slot.ActionId} / {CharacterName(slot.Owner)}:{PartName(slot.Part)}[{slot.ActionIndex}] " +
                $"Speed={slot.Speed} / Skill={slot.Skill?.SkillName ?? "NULL"} / " +
                $"Declared={CharacterName(slot.TargetCharacter)}:{PartName(slot.TargetPart)} / " +
                $"Exact={exact} / Policy={challenge} / Preview={pairText}");
        }

        return ProbeResult.Pass(
            $"CombatSlots={combat.Count}, PreviewPairs={preview.Count}",
            string.Join("\n", lines));
    }

    private static FocusedFixtureData BuildFocusedFixtureData(
        GameSystemVerificationFixture fixture)
    {
        Character player =
            fixture?.Player;

        Character enemy =
            fixture?.Enemy;

        Skill playerSkill =
            player?.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill != null &&
                        skill.CanClash);

        Skill enemySkill =
            enemy?.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill != null &&
                        skill.CanClash);

        List<BodyPart> playerParts =
            player?.BodyParts?
                .Where(
                    part =>
                        part != null &&
                        !part.IsBroken &&
                        playerSkill != null &&
                        BodyPartSkillAccessPolicy.Allows(
                            part,
                            playerSkill.ActionType))
                .ToList() ??
            new List<BodyPart>();

        BodyPart primary =
            playerParts.FirstOrDefault(
                part => part.Type == PartType.HEAD) ??
            playerParts.FirstOrDefault();

        BodyPart alternate =
            playerParts.FirstOrDefault(
                part => part != primary);

        BodyPart enemyPart =
            enemy?.IsSingleHpTarget == true
                ? null
                : enemy?.BodyParts?
                    .FirstOrDefault(
                        part =>
                            part != null &&
                            !part.IsBroken);

        return new FocusedFixtureData
        {
            Player = player,
            Enemy = enemy,
            PrimaryPart = primary,
            AlternatePart = alternate,
            EnemyPart = enemyPart,
            PlayerCombatSkill = playerSkill,
            EnemyCombatSkill = enemySkill
        };
    }

    private static bool TryValidateFocusedFixture(
        FocusedFixtureData data,
        out string reason)
    {
        if (data == null ||
            data.Player == null ||
            data.Enemy == null ||
            data.PrimaryPart == null ||
            data.AlternatePart == null ||
            data.PlayerCombatSkill == null ||
            data.EnemyCombatSkill == null)
        {
            reason =
                "Focused Encounter probe에 Player/Enemy/서로 다른 2개 BodyPart/Clash Skill이 필요합니다.";

            return false;
        }

        reason = null;
        return true;
    }

    private static ActionSlot CreateIncoming(
        FocusedFixtureData data,
        BodyPart targetPart,
        int speed,
        long actionId)
    {
        return new ActionSlot
        {
            ActionId = actionId,
            Owner = data.Enemy,
            Part = data.EnemyPart,
            Skill = data.EnemyCombatSkill,
            Speed = speed,
            ActionIndex = 0,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = data.Player,
            TargetPart = targetPart,
            TargetSlot = null
        };
    }

    private static ActionSlot CreateChallenger(
        FocusedFixtureData data,
        BodyPart part,
        int speed,
        ActionSlot incoming,
        long actionId)
    {
        return new ActionSlot
        {
            ActionId = actionId,
            Owner = data.Player,
            Part = part,
            Skill = data.PlayerCombatSkill,
            Speed = speed,
            ActionIndex = 0,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = data.Enemy,
            TargetPart = data.EnemyPart,
            TargetSlot = incoming
        };
    }

    private static ActionSlot CreatePreparationSlot(
        Character owner,
        BodyPart part,
        Skill skill,
        int speed,
        long actionId)
    {
        return new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = part,
            Skill = skill,
            Speed = speed,
            ActionIndex = 0,
            Phase = ActionPhase.FORESIGHT,
            TargetCharacter = owner,
            TargetPart = part,
            TargetSlot = null
        };
    }

    private static bool TryFindPreparation(
        GameSystemVerificationFixture fixture,
        out Skill preparation,
        out BodyPart part,
        out string reason)
    {
        preparation =
            fixture?.Player?.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill != null &&
                        skill.ActionType == ActionType.Preparation);

        part = null;

        if (fixture?.Player == null ||
            preparation == null)
        {
            reason =
                "현재 Player에 Preparation RuntimeSkill이 없습니다.";

            return false;
        }

        BodyPart legs =
            fixture.Player.GetBodyPart(
                PartType.LEGS);

        // C#에서는 out/ref/in 매개변수를 lambda가 캡처할 수 없다.
        // out 매개변수 preparation의 현재 값을 일반 로컬 변수로 복사한 뒤
        // LINQ predicate에서는 그 로컬 값만 사용한다.
        Skill preparationSkill =
            preparation;

        if (CanSelectSkill(
                fixture.Player,
                legs,
                preparationSkill))
        {
            part = legs;
        }
        else
        {
            part =
                fixture.Player.BodyParts?
                    .FirstOrDefault(
                        candidate =>
                            CanSelectSkill(
                                fixture.Player,
                                candidate,
                                preparationSkill));
        }

        if (part == null)
        {
            reason =
                "Preparation을 실제 선택 가능한 BodyPart가 없습니다.";

            return false;
        }

        reason = null;
        return true;
    }

    private static bool CanSelectSkill(
        Character character,
        BodyPart part,
        Skill skill)
    {
        if (character == null ||
            part == null ||
            part.IsBroken ||
            skill == null ||
            !BodyPartSkillAccessPolicy.Allows(
                part,
                skill.ActionType))
        {
            return false;
        }

        IReadOnlyList<Skill> selectable =
            character.GetSelectableSkills(
                part,
                0);

        if (selectable == null)
            return false;

        string skillId =
            skill.Definition?.SkillId;

        return selectable.Any(
            candidate =>
                candidate == skill ||
                (!string.IsNullOrWhiteSpace(skillId) &&
                 string.Equals(
                     candidate?.Definition?.SkillId,
                     skillId,
                     StringComparison.Ordinal)));
    }

    private static ClashMatchPolicy NewPolicy() =>
        new ClashMatchPolicy(
            new ActionPhaseSorter());

    private static GameSystemVerificationReport CreateReport(
        DateTime started,
        BattleManager manager)
    {
        return new GameSystemVerificationReport
        {
            SessionId =
                started.ToString(
                    "yyyyMMdd_HHmmss_fff"),
            StartedAt =
                started.ToString("O"),
            SceneName =
                manager != null &&
                manager.gameObject.scene.IsValid()
                    ? manager.gameObject.scene.name
                    : "DataOnly",
            PlayerName =
                manager?.BattleContext?.Player?.Data?.CharacterName ??
                manager?.BattleContext?.Player?.name ??
                "N/A"
        };
    }

    private static void Complete(
        GameSystemVerificationReport report,
        DateTime started)
    {
        report.FinishedAt =
            DateTime.Now.ToString("O");

        report.RecalculateCounts();
    }

    private static void RunCase(
        GameSystemVerificationReport report,
        string caseId,
        string displayName,
        GameSystemVerificationCategory category,
        string expected,
        Func<ProbeResult> probe)
    {
        Stopwatch stopwatch =
            Stopwatch.StartNew();

        GameSystemVerificationCaseResult result =
            new GameSystemVerificationCaseResult
            {
                CaseId = caseId,
                DisplayName = displayName,
                Category = category,
                Expected = expected
            };

        try
        {
            ProbeResult outcome =
                probe?.Invoke();

            if (outcome == null)
            {
                result.Status =
                    GameSystemVerificationStatus.Error;

                result.Actual =
                    "ProbeResult=NULL";
            }
            else if (outcome.Skipped)
            {
                result.Status =
                    GameSystemVerificationStatus.Skip;

                result.Actual =
                    outcome.Actual;

                result.Details =
                    outcome.Details;
            }
            else
            {
                result.Status =
                    outcome.Passed
                        ? GameSystemVerificationStatus.Pass
                        : GameSystemVerificationStatus.Fail;

                result.Actual =
                    outcome.Actual;

                result.Details =
                    outcome.Details;
            }
        }
        catch (Exception exception)
        {
            result.Status =
                GameSystemVerificationStatus.Error;

            result.Actual =
                exception.Message;

            result.Details =
                exception.ToString();
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMilliseconds =
                stopwatch.ElapsedMilliseconds;

            report.Results.Add(result);
        }
    }

    private static void AddError(
        GameSystemVerificationReport report,
        string caseId,
        string displayName,
        GameSystemVerificationCategory category,
        string expected,
        string actual,
        string details = null)
    {
        report.Results.Add(
            new GameSystemVerificationCaseResult
            {
                CaseId = caseId,
                DisplayName = displayName,
                Category = category,
                Status = GameSystemVerificationStatus.Error,
                Expected = expected,
                Actual = actual,
                Details = details
            });
    }

    private static string CharacterName(
        Character character) =>
        character?.Data?.CharacterName ??
        character?.name ??
        "NULL";

    private static string PartName(
        BodyPart part) =>
        part == null
            ? "CHARACTER"
            : part.Type.ToString();
}
