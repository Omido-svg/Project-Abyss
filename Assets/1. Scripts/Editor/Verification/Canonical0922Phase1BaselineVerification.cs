#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [0922_PHASE1_BASELINE_HARNESS]
/// 0922 Phase 1 — Baseline Verification Harness.
///
/// 이 Gate는 현재 Runtime을 0922로 "수정"하지 않는다.
/// 현재 구현이 0922 정본에서 어디까지 PASS이고,
/// 무엇이 이후 Phase의 TARGET_GAP인지 기록한다.
///
/// TARGET_GAP / PENDING_CANONICAL은 Phase 1 자체의 실패가 아니다.
/// HARNESS_FAIL만 Phase 1 설치/검증 실패다.
/// </summary>
public static class Canonical0922Phase1BaselineVerification
{
    private enum BaselineState
    {
        Pass,
        TargetGap,
        PendingCanonical,
        HarnessFail
    }

    private sealed class BaselineCheck
    {
        public string Id;
        public string Name;
        public BaselineState State;
        public string OwnerPhase;
        public string Expected;
        public string Actual;
        public string Details;
    }

    private const string ReportDirectory =
        "Logs/GameSystemVerification";

    private const string ReportPath =
        ReportDirectory + "/0922_Phase1_Baseline.md";

    private const string StatusEffectPath =
        "Assets/1. Scripts/Runtime/Status/StatusEffect.cs";

    private const string CommonStatusesPath =
        "Assets/1. Scripts/Runtime/Status/Common/CommonCombatStatuses.cs";

    private const string CharacterStatusControllerPath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/CharacterStatusController.cs";

    private const string CommonStatusAlgebraPath =
        "Assets/1. Scripts/Runtime/Status/Common/CommonStatusAlgebra.cs";

    private const string StatusFactoryPath =
        "Assets/1. Scripts/Runtime/Skills/Effects/Common/StatusEffectFactory.cs";

    private const string DeferredStatusPath =
        "Assets/1. Scripts/Runtime/Status/Common/DeferredStatusEffect.cs";

    private const string DamagePipelinePath =
        "Assets/1. Scripts/Runtime/Battle/Damage/DamagePipeline.cs";

    private const string StaggerGaugePath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/StaggerGaugeMechanic.cs";

    private const string TurnManagerPath =
        "Assets/1. Scripts/Runtime/Systems/BattleFlow/TurnManager.cs";

    private const string YujinDesignationPath =
        "Assets/1. Scripts/Runtime/Characters/Yujin/Status/YujinDesignationStatus.cs";

    private const string YujinMechanicPath =
        "Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs";

    private const string OlafFearPath =
        "Assets/1. Scripts/Runtime/Status/CharacterSpecific/Olaf/OlafFearStatus.cs";

    [MenuItem("Game System Verification/0922 Canonical/Phase 1 - Run Baseline Harness")]
    public static void RunFromMenu()
    {
        List<BaselineCheck> checks = BuildChecks();

        WriteReport(checks);

        int pass =
            checks.Count(x => x.State == BaselineState.Pass);

        int gaps =
            checks.Count(x => x.State == BaselineState.TargetGap);

        int pending =
            checks.Count(x => x.State == BaselineState.PendingCanonical);

        int harnessFail =
            checks.Count(x => x.State == BaselineState.HarnessFail);

        string summary =
            "[0922 Phase 1 · Baseline Verification Harness]\n" +
            $"CANONICAL={Canonical0922BaselineSpec.CanonicalVersion}\n" +
            $"PASS={pass} TARGET_GAP={gaps} " +
            $"PENDING_CANONICAL={pending} HARNESS_FAIL={harnessFail}\n" +
            $"REPORT={ReportPath}\n\n" +
            BuildConsoleSection(
                "PASS",
                checks,
                BaselineState.Pass) +
            "\n\n" +
            BuildConsoleSection(
                "TARGET_GAP",
                checks,
                BaselineState.TargetGap) +
            "\n\n" +
            BuildConsoleSection(
                "PENDING_CANONICAL",
                checks,
                BaselineState.PendingCanonical) +
            "\n\n" +
            BuildConsoleSection(
                "HARNESS_FAIL",
                checks,
                BaselineState.HarnessFail);

        if (harnessFail > 0)
        {
            Debug.LogError(
                summary +
                "\n\nPHASE1_RESULT=FAIL");
        }
        else
        {
            Debug.Log(
                summary +
                "\n\nPHASE1_RESULT=PASS_BASELINE_CAPTURED");
        }
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 1 - Open Baseline Report")]
    public static void OpenReportFromMenu()
    {
        if (!File.Exists(ReportPath))
        {
            Debug.LogWarning(
                "[0922 Phase 1] Baseline report가 없습니다. " +
                "먼저 Phase 1 - Run Baseline Harness를 실행하세요.");
            return;
        }

        EditorUtility.RevealInFinder(
            Path.GetFullPath(ReportPath));
    }

    private static List<BaselineCheck> BuildChecks()
    {
        List<BaselineCheck> checks = new();

        AddHarnessInfrastructureChecks(checks);
        AddLegacyIsolationChecks(checks);
        AddNumericStorageChecks(checks);
        AddPresenceChecks(checks);
        AddHeatAndRegenerationChecks(checks);
        AddDesignationChecks(checks);
        AddTimingChecks(checks);
        AddDamageAxisChecks(checks);
        AddPendingCanonicalChecks(checks);

        return checks;
    }

    private static void AddHarnessInfrastructureChecks(
        List<BaselineCheck> checks)
    {
        Add(
            checks,
            "P1-H00",
            "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922"
                ? BaselineState.Pass
                : BaselineState.HarnessFail,
            "Phase 1",
            "Canonical=0922",
            "Canonical=" +
            Canonical0922BaselineSpec.CanonicalVersion,
            "Phase 0 manifest가 Phase 1의 유일한 정본 기준이다.");

        string[] requiredFiles =
        {
            StatusEffectPath,
            CommonStatusesPath,
            CharacterStatusControllerPath,
            CommonStatusAlgebraPath,
            StatusFactoryPath,
            DeferredStatusPath,
            DamagePipelinePath,
            StaggerGaugePath,
            TurnManagerPath,
            YujinDesignationPath,
            YujinMechanicPath,
            OlafFearPath
        };

        string[] missing =
            requiredFiles
                .Where(path => !File.Exists(path))
                .ToArray();

        Add(
            checks,
            "P1-H01",
            "Baseline source set available",
            missing.Length == 0
                ? BaselineState.Pass
                : BaselineState.HarnessFail,
            "Phase 1",
            "Required source files are readable",
            missing.Length == 0
                ? "All required sources found"
                : "Missing=" + string.Join(", ", missing),
            "소스 파일 자체가 없으면 baseline 판정을 신뢰할 수 없다.");
    }

    private static void AddLegacyIsolationChecks(
        List<BaselineCheck> checks)
    {
        IReadOnlyList<GameSystemVerificationCase> cases =
            GameSystemVerificationRunner.DiscoverCases();

        string[] staleCaseIds =
        {
            "phaseb.c27.common_status_syntax",
            "phaseb.c48.regeneration_dimensions",
            "phaseb.c49.opposite_status_algebra"
        };

        List<string> bad = new();

        foreach (string id in staleCaseIds)
        {
            GameSystemVerificationCase verificationCase =
                cases.FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        candidate.CaseId == id);

            if (verificationCase == null)
            {
                bad.Add(id + ":missing");
                continue;
            }

            if (verificationCase.Required)
                bad.Add(id + ":Required=true");

            if (verificationCase.DisplayName == null ||
                !verificationCase.DisplayName.Contains(
                    "[LEGACY BASELINE]",
                    StringComparison.Ordinal))
            {
                bad.Add(id + ":label");
            }
        }

        Add(
            checks,
            "P1-H02",
            "Stale PhaseB status expectations isolated",
            bad.Count == 0
                ? BaselineState.Pass
                : BaselineState.HarnessFail,
            "Phase 1",
            "C-27/C-48/C-49 = [LEGACY BASELINE], Required=false",
            bad.Count == 0
                ? "3/3 isolated"
                : string.Join(", ", bad),
            "0922 구현이 pre-0922 기대값 때문에 Required gate에서 실패하면 안 된다.");
    }

    private static void AddNumericStorageChecks(
        List<BaselineCheck> checks)
    {
        string common =
            Read(CommonStatusesPath);

        string controller =
            Read(CharacterStatusControllerPath);

        string algebra =
            Read(CommonStatusAlgebraPath);

        string factory =
            Read(StatusFactoryPath);

        string statusEffect =
            Read(StatusEffectPath);

        bool legacyOneTurn =
            common.Contains(
                "abstract class OneTurnCommonStatus",
                StringComparison.Ordinal) &&
            common.Contains(
                "Duration = 1;",
                StringComparison.Ordinal) &&
            common.Contains(
                "AddStacksAndRefreshDuration",
                StringComparison.Ordinal);

        bool legacyMergePath =
            controller.Contains(
                "FindSameStatus(effect)",
                StringComparison.Ordinal) &&
            controller.Contains(
                "existing.ApplyIncoming",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S01",
            "Numeric same-type grant preserves independent entries",
            !legacyOneTurn && !legacyMergePath
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 2",
            "Numeric N/T grants append independent entries",
            legacyOneTurn || legacyMergePath
                ? "Legacy same-type merge path detected"
                : "No legacy numeric merge path detected",
            "0922 NumericTimed의 핵심 저장 계약.");

        bool destructiveOpposite =
            controller.Contains(
                "ResolveOppositeCharacterStatus(effect)",
                StringComparison.Ordinal) ||
            controller.Contains(
                "ResolveOppositePartStatus(part, effect)",
                StringComparison.Ordinal) ||
            controller.Contains(
                "ConsumeStacks(cancelled)",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S02",
            "Opposite numeric statuses remain stored",
            !destructiveOpposite
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 2 / Phase 3",
            "Opposites cancel only in calculation, never by deleting stored value",
            destructiveOpposite
                ? "Destructive opposite cancellation detected"
                : "No destructive cancellation detected",
            algebra.Contains(
                    "AreOpposites",
                    StringComparison.Ordinal)
                ? "Legacy CommonStatusAlgebra pair map still exists."
                : "Pair map is no longer source of destructive storage behavior.");

        bool hardCaps =
            common.Contains(
                "base(\"힘\", stack, 4)",
                StringComparison.Ordinal) ||
            common.Contains(
                "base(\"견고\", stack, 3)",
                StringComparison.Ordinal) ||
            common.Contains(
                "base(\"열기\", stack, 99)",
                StringComparison.Ordinal) ||
            common.Contains(
                "Mathf.Clamp(Stack +",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S03",
            "No common Numeric Value cap",
            !hardCaps
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 2",
            "No system-wide Value/Duration/entry-count cap",
            hardCaps
                ? "Legacy 3/4/99 common caps detected"
                : "No legacy common hard cap pattern detected",
            "카드/키워드 개별 cap은 별도 규칙일 수 있다.");

        bool permanentFoundation =
            statusEffect.Contains(
                "Duration < 0",
                StringComparison.Ordinal) &&
            statusEffect.Contains(
                "StatusEffectDurationPolicy.Permanent",
                StringComparison.Ordinal) &&
            statusEffect.Contains(
                "public bool IsPermanent",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S04A",
            "Generic permanent-duration foundation",
            permanentFoundation
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 2",
            "Runtime can represent non-ticking permanent duration",
            permanentFoundation
                ? "StatusEffect already has permanent-duration foundation"
                : "Permanent-duration foundation not detected",
            "0922 ∞ Numeric 지원에 재사용 가능한 기반 여부.");

        bool numericInfiniteBlocked =
            common.Contains(
                "Duration = 1;",
                StringComparison.Ordinal) ||
            factory.Contains(
                "int safeDuration",
                StringComparison.Ordinal) &&
            factory.Contains(
                "Mathf.Max(1, duration)",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S04B",
            "Numeric statuses accept finite T and ∞",
            !numericInfiniteBlocked
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 2 / Phase 5",
            "NumericTimed supports arbitrary T and ∞",
            numericInfiniteBlocked
                ? "One-turn/common factory clamps prevent canonical Numeric T/∞"
                : "Numeric T/∞ authoring path detected",
            "∞를 999턴 같은 값으로 치환하면 안 된다.");

        bool factoryIgnoresDuration =
            factory.Contains(
                "new StrengthStatus(safeStack)",
                StringComparison.Ordinal) ||
            factory.Contains(
                "new ProtectionStatus(safeStack)",
                StringComparison.Ordinal) ||
            factory.Contains(
                "new HeatStatus(safeStack)",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S05",
            "Factory preserves Numeric N/T",
            !factoryIgnoresDuration
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 5",
            "Factory maps Numeric Value and Duration without discarding T",
            factoryIgnoresDuration
                ? "Factory still discards duration for common Numeric statuses"
                : "Factory duration discard pattern not detected",
            "Data migration 전 이 경로를 먼저 바꾸면 stale serialized Duration 위험이 있으므로 Phase 5 소유.");

        string deferred =
            Read(DeferredStatusPath);

        bool deferredMerges =
            deferred.Contains(
                "StackPolicy.AddStacksAndRefreshDuration",
                StringComparison.Ordinal) &&
            deferred.Contains(
                "Stack +=",
                StringComparison.Ordinal) &&
            deferred.Contains(
                "pendingDuration =",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-S06",
            "Deferred Numeric grants preserve identity",
            !deferredMerges
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 4",
            "Two queued Numeric grants materialize as two entries",
            deferredMerges
                ? "Deferred same-id stack merge/max-duration path detected"
                : "No legacy Deferred numeric merge pattern detected",
            "Presence 예약은 max refresh가 맞지만 Numeric 예약은 identity를 보존해야 한다.");
    }

    private static void AddPresenceChecks(
        List<BaselineCheck> checks)
    {
        try
        {
            PainStatus pain =
                new PainStatus(5);

            pain.Merge(
                new PainStatus(2));

            bool painDuration =
                pain.Duration == 5;

            bool painHealing =
                pain.ModifyHealing(9) == 4 &&
                pain.ModifyHealing(1) == 1;

            Add(
                checks,
                "P1-P01",
                "Pain max-duration refresh and single half-heal behavior",
                painDuration && painHealing
                    ? BaselineState.Pass
                    : BaselineState.TargetGap,
                "Phase 3",
                "Pain T-only presence: max refresh; positive healing ÷2 once",
                $"Duration={pain.Duration}, Heal9={pain.ModifyHealing(9)}, Heal1={pain.ModifyHealing(1)}",
                "현재 core behavior가 0922와 맞는지 직접 probe.");

            Add(
                checks,
                "P1-P02",
                "Pain has no gameplay Value N representation",
                pain.Stack == 0
                    ? BaselineState.Pass
                    : BaselineState.TargetGap,
                "Phase 2 / Phase 11",
                "Pain exposes duration/presence, not numeric Value",
                $"Legacy Stack={pain.Stack}",
                "현재 Stack=Duration이면 UI/조건식이 Pain을 수치형으로 오인할 수 있다.");
        }
        catch (Exception exception)
        {
            AddHarnessException(
                checks,
                "P1-P01",
                "Pain probe",
                exception);
        }

        try
        {
            OlafFearStatus fear =
                new OlafFearStatus(5);

            fear.Merge(
                new OlafFearStatus(2));

            BattleAction action =
                new BattleAction
                {
                    Slot = new ActionSlot()
                };

            int shifted =
                fear.ModifyRoll(
                    action,
                    10);

            bool core =
                fear.Duration == 5 &&
                shifted == 9;

            Add(
                checks,
                "P1-P03",
                "Fear fixed -1 and max-duration refresh",
                core
                    ? BaselineState.Pass
                    : BaselineState.TargetGap,
                "Phase 6",
                "Fear = fixed -1; no effect stacking; duration=max(current,new)",
                $"Duration={fear.Duration}, Roll10->{shifted}",
                "Weakness와 별도 축.");

            Add(
                checks,
                "P1-P04",
                "Fear has no gameplay Value N representation",
                fear.Stack == 0
                    ? BaselineState.Pass
                    : BaselineState.TargetGap,
                "Phase 6 / Phase 11",
                "Fear is T-only presence status",
                $"Legacy Stack={fear.Stack}",
                "Stack=1을 gameplay N으로 노출하지 않아야 한다.");
        }
        catch (Exception exception)
        {
            AddHarnessException(
                checks,
                "P1-P03",
                "Fear probe",
                exception);
        }
    }

    private static void AddHeatAndRegenerationChecks(
        List<BaselineCheck> checks)
    {
        string common =
            Read(CommonStatusesPath);

        string factory =
            Read(StatusFactoryPath);

        bool heatImmediate =
            common.Contains(
                "class HeatStatus",
                StringComparison.Ordinal) &&
            common.Contains(
                "public override void OnApply()",
                StringComparison.Ordinal) &&
            common.Contains(
                "Owner.AddPrestige(Stack)",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-E01",
            "Heat resolves at TurnEnd for T turns",
            !heatImmediate
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 3",
            "HeatTotal contributes +N prestige at each TurnEnd, then duration ticks",
            heatImmediate
                ? "Heat OnApply prestige path detected"
                : "Immediate Heat payout pattern not detected",
            "0922 Heat는 즉시 지급이 아니다.");

        bool stagnationExists =
            factory.Contains(
                "Stagnation",
                StringComparison.Ordinal) ||
            common.Contains(
                "StagnationStatus",
                StringComparison.Ordinal) ||
            common.Contains(
                "침체",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-E02",
            "Stagnation numeric status exists",
            stagnationExists
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 3 / Phase 5",
            "Stagnation N/T subtracts prestige at TurnEnd",
            stagnationExists
                ? "Stagnation symbol detected"
                : "No Stagnation status/factory symbol detected",
            "Heat와 저장 단계에서 상쇄하지 않는다.");

        bool regenChannelSplit =
            common.Contains(
                "enum RegenerationRecoveryChannel",
                StringComparison.Ordinal) &&
            common.Contains(
                "public RegenerationRecoveryChannel Channel",
                StringComparison.Ordinal) &&
            common.Contains(
                "Stack = Duration;",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-E03",
            "Regeneration heals HP and Stagger from one aggregate N",
            !regenChannelSplit
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 3",
            "Each TurnEnd: HP +RegenTotal AND Stagger +RegenTotal",
            regenChannelSplit
                ? "Legacy HP/Stagger channel split + Stack=Duration detected"
                : "Legacy channel split not detected",
            "여러 Regen Entry를 각각 회복 호출하지 않고 합산 후 한 번 적용해야 한다.");
    }

    private static void AddDesignationChecks(
        List<BaselineCheck> checks)
    {
        string designation =
            Read(YujinDesignationPath);

        string mechanic =
            Read(YujinMechanicPath);

        bool legacyDesignation =
            designation.Contains(
                "StatusEffectStackPolicy.RefreshDuration",
                StringComparison.Ordinal) ||
            designation.Contains(
                "Stack = 1;",
                StringComparison.Ordinal) ||
            mechanic.Contains(
                "amount += 2;",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-Y01",
            "Designation is Numeric N/T independent and adds current total N to Mark gain",
            !legacyDesignation
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 6",
            "DesignationTotal = sum alive N; actual Mark gain += DesignationTotal",
            legacyDesignation
                ? "Legacy RefreshDuration / Stack=1 / fixed +2 path detected"
                : "Legacy Designation pattern not detected",
            "카드 C/M의 canonical 값은 Designation 2·3.");
    }

    private static void AddTimingChecks(
        List<BaselineCheck> checks)
    {
        string turnManager =
            Read(TurnManagerPath);

        int characterStart =
            turnManager.IndexOf(
                "RunCharacterTurnStart();",
                StringComparison.Ordinal);

        int speedRoll =
            turnManager.IndexOf(
                "speedManager?.RollAllSpeed();",
                StringComparison.Ordinal);

        bool correctOrder =
            characterStart >= 0 &&
            speedRoll >= 0 &&
            characterStart < speedRoll;

        Add(
            checks,
            "P1-T01",
            "TurnStart queued effects resolve before speed roll",
            correctOrder
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 4",
            "TurnStart queued effects materialize before speed roll",
            $"RunCharacterTurnStart index={characterStart}, RollAllSpeed index={speedRoll}",
            "다음 턴 예약 Swift/Mark가 속도 굴림 전에 실제 상태/표식이 되어야 한다.");

        string deferred =
            Read(DeferredStatusPath);

        bool deferredMaterializesAtTurnStart =
            deferred.Contains(
                "StatusEffectDurationPolicy.TurnStart",
                StringComparison.Ordinal) &&
            deferred.Contains(
                "public override void OnTurnStart",
                StringComparison.Ordinal) &&
            deferred.Contains(
                "StatusEffectFactory.Create",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-T02",
            "Deferred status materializes on TurnStart",
            deferredMaterializesAtTurnStart
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 4",
            "Queued status becomes actual status at TurnStart",
            deferredMaterializesAtTurnStart
                ? "DeferredStatusEffect TurnStart materialization detected"
                : "TurnStart materialization pattern missing",
            "T01과 함께 reserved Swift timing의 기반을 이룬다.");
    }

    private static void AddDamageAxisChecks(
        List<BaselineCheck> checks)
    {
        string damage =
            Read(DamagePipelinePath);

        bool sequentialHpFlat =
            damage.Contains(
                "effect.ModifyDamageTaken(context.Action, damage)",
                StringComparison.Ordinal) &&
            damage.Contains(
                "Mathf.Max(",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-C01",
            "Protection/Rupture flat axis is order-independent",
            !sequentialHpFlat
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 3",
            "RuptureTotal - ProtectionTotal is aggregated before a single clamp",
            sequentialHpFlat
                ? "Sequential ModifyDamageTaken/clamp path detected"
                : "Sequential HP-flat pattern not detected",
            "예: raw2 + Rupture5 - Protection5 = 2.");

        string stagger =
            Read(StaggerGaugePath);

        bool staggerAggregate =
            stagger.Contains(
                "flatModifier +=",
                StringComparison.Ordinal) &&
            stagger.Contains(
                "GetStaggerDamageTakenFlatModifier",
                StringComparison.Ordinal) &&
            stagger.Contains(
                "damage + flatModifier",
                StringComparison.Ordinal);

        Add(
            checks,
            "P1-C02",
            "Sturdy/Disarm flat axis aggregates before clamp",
            staggerAggregate
                ? BaselineState.Pass
                : BaselineState.TargetGap,
            "Phase 3",
            "Original + DisarmTotal - SturdyTotal, then clamp",
            staggerAggregate
                ? "Aggregate stagger-flat path detected"
                : "Aggregate stagger-flat path not detected",
            "현재 구현에서 재사용 가능한 축인지 확인.");
    }

    private static void AddPendingCanonicalChecks(
        List<BaselineCheck> checks)
    {
        IReadOnlyList<string> pending =
            Canonical0922BaselineSpec.PendingCanonicalDomains;

        if (pending == null ||
            pending.Count == 0)
        {
            Add(
                checks,
                "P1-H03",
                "PENDING_CANONICAL inventory",
                BaselineState.HarnessFail,
                "Phase 1",
                "Phase 0 pending inventory remains explicit",
                "Pending inventory empty",
                "0922 규칙서의 미정값을 구현자가 임의로 확정하면 안 된다.");
            return;
        }

        Add(
            checks,
            "P1-H03",
            "PENDING_CANONICAL inventory",
            BaselineState.Pass,
            "Phase 1",
            "Phase 0 pending inventory remains explicit",
            $"Count={pending.Count}",
            "각 항목은 baseline report에도 개별 출력한다.");

        for (int i = 0; i < pending.Count; i++)
        {
            Add(
                checks,
                $"P1-PENDING-{i + 1:00}",
                pending[i],
                BaselineState.PendingCanonical,
                "Canonical Design",
                "Do not hardcode until canonical is resolved",
                "PENDING_CANONICAL",
                "Phase 0 manifest에서 전달된 미정 도메인.");
        }
    }

    private static void Add(
        List<BaselineCheck> checks,
        string id,
        string name,
        BaselineState state,
        string ownerPhase,
        string expected,
        string actual,
        string details)
    {
        checks.Add(
            new BaselineCheck
            {
                Id = id,
                Name = name,
                State = state,
                OwnerPhase = ownerPhase,
                Expected = expected,
                Actual = actual,
                Details = details
            });
    }

    private static void AddHarnessException(
        List<BaselineCheck> checks,
        string id,
        string name,
        Exception exception)
    {
        Add(
            checks,
            id,
            name,
            BaselineState.HarnessFail,
            "Phase 1",
            "Probe executes without exception",
            exception.GetType().Name + ": " + exception.Message,
            exception.StackTrace);
    }

    private static string Read(
        string path)
    {
        return File.Exists(path)
            ? File.ReadAllText(path)
            : string.Empty;
    }

    private static string BuildConsoleSection(
        string title,
        IReadOnlyList<BaselineCheck> checks,
        BaselineState state)
    {
        BaselineCheck[] selected =
            checks
                .Where(x => x.State == state)
                .ToArray();

        if (selected.Length == 0)
            return title + "\n- NONE";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(title);

        foreach (BaselineCheck check in selected)
        {
            builder.Append("\n- ");
            builder.Append(check.Id);
            builder.Append(" | ");
            builder.Append(check.Name);

            if (!string.IsNullOrWhiteSpace(check.OwnerPhase))
            {
                builder.Append(" | Owner=");
                builder.Append(check.OwnerPhase);
            }

            if (!string.IsNullOrWhiteSpace(check.Actual))
            {
                builder.Append(" | ");
                builder.Append(check.Actual);
            }
        }

        return builder.ToString();
    }

    private static void WriteReport(
        IReadOnlyList<BaselineCheck> checks)
    {
        Directory.CreateDirectory(
            ReportDirectory);

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "# Project Abyss — 0922 Phase 1 Baseline Verification");

        builder.AppendLine();
        builder.AppendLine(
            $"> Canonical: `{Canonical0922BaselineSpec.CanonicalVersion}`");

        builder.AppendLine(
            $"> Generated: `{DateTime.Now:O}`");

        builder.AppendLine(
            "> Phase 1 changes no gameplay runtime behavior.");

        builder.AppendLine();
        builder.AppendLine(
            "## Result");

        int pass =
            checks.Count(x => x.State == BaselineState.Pass);

        int gap =
            checks.Count(x => x.State == BaselineState.TargetGap);

        int pending =
            checks.Count(x => x.State == BaselineState.PendingCanonical);

        int fail =
            checks.Count(x => x.State == BaselineState.HarnessFail);

        builder.AppendLine();
        builder.AppendLine(
            $"`PASS={pass} / TARGET_GAP={gap} / PENDING_CANONICAL={pending} / HARNESS_FAIL={fail}`");

        builder.AppendLine();
        builder.AppendLine(
            fail == 0
                ? "**PHASE1_RESULT = PASS_BASELINE_CAPTURED**"
                : "**PHASE1_RESULT = FAIL**");

        builder.AppendLine();
        builder.AppendLine(
            "TARGET_GAP은 이후 Phase가 고칠 현재 구현 차이이며 Phase 1 자체 실패가 아니다.");

        builder.AppendLine();
        builder.AppendLine(
            "| ID | State | Check | Owner Phase | Expected | Actual |");

        builder.AppendLine(
            "|---|---|---|---|---|---|");

        foreach (BaselineCheck check in checks)
        {
            builder.Append("| ");
            builder.Append(Escape(check.Id));
            builder.Append(" | ");
            builder.Append(Escape(check.State.ToString()));
            builder.Append(" | ");
            builder.Append(Escape(check.Name));
            builder.Append(" | ");
            builder.Append(Escape(check.OwnerPhase));
            builder.Append(" | ");
            builder.Append(Escape(check.Expected));
            builder.Append(" | ");
            builder.Append(Escape(check.Actual));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Details");

        foreach (BaselineCheck check in checks)
        {
            builder.AppendLine();
            builder.Append("### ");
            builder.Append(check.Id);
            builder.Append(" — ");
            builder.AppendLine(check.Name);
            builder.AppendLine();
            builder.Append("- State: `");
            builder.Append(check.State);
            builder.AppendLine("`");
            builder.Append("- Owner Phase: `");
            builder.Append(check.OwnerPhase);
            builder.AppendLine("`");
            builder.Append("- Expected: ");
            builder.AppendLine(check.Expected);
            builder.Append("- Actual: ");
            builder.AppendLine(check.Actual);

            if (!string.IsNullOrWhiteSpace(check.Details))
            {
                builder.Append("- Details: ");
                builder.AppendLine(
                    check.Details.Replace(
                        "\r",
                        " ").Replace(
                        "\n",
                        " "));
            }
        }

        File.WriteAllText(
            ReportPath,
            builder.ToString(),
            new UTF8Encoding(true));

        Debug.Log(
            "[0922 Phase 1] Baseline report written: " +
            Path.GetFullPath(ReportPath));
    }

    private static string Escape(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }
}
#endif
