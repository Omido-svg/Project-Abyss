#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase4TimingDeferredVerification
{
    private const string Phase3ReportPath =
        "Logs/GameSystemVerification/0922_Phase3_CalculationTurnEnd.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 4 - Verify Timing and Deferred")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisites(checks);
        AddDeferredRuntimeChecks(checks);
        AddOrderingChecks(checks);
        AddPlanningChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;

        string result =
            fail == 0
                ? "PASS_TIMING_DEFERRED"
                : "FAIL";

        WriteReport(
            checks,
            pass,
            fail,
            result);

        string summary =
            "[0922 Phase 4 · Timing / Deferred / Reservation]\n" +
            $"PASS={pass} FAIL={fail}\n\n" +
            "PASS\n- " +
            string.Join(
                "\n- ",
                checks.Where(x => x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join(
                    "\n- ",
                    checks.Where(x => !x.Passed)
                        .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            $"\n\nPHASE4_RESULT={result}";

        if (fail == 0)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static void AddPrerequisites(
        List<Check> checks)
    {
        Add(
            checks,
            "P4-H00",
            "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                Phase3ReportPath);

        bool exists =
            File.Exists(path);

        string report =
            exists
                ? File.ReadAllText(path)
                : string.Empty;

        bool pass =
            report.Contains(
                "RESULT: PASS_CALCULATION_TURNEND",
                StringComparison.Ordinal) ||
            report.Contains(
                "PHASE3_RESULT=PASS_CALCULATION_TURNEND",
                StringComparison.Ordinal);

        Add(
            checks,
            "P4-H01",
            "Phase 3 calculation/TurnEnd closed",
            exists && pass,
            !exists
                ? "Phase3 report missing"
                : $"ReportPresent=True, ResultPass={pass}");
    }

    private static void AddDeferredRuntimeChecks(
        List<Check> checks)
    {
        GameObject go = null;

        try
        {
            go =
                new GameObject(
                    "__0922_PHASE4_DEFERRED_PROBE__");

            NormalEnemy owner =
                go.AddComponent<NormalEnemy>();

            CharacterStatusController controller =
                new CharacterStatusController(
                    owner);

            controller.AddStatus(
                new DeferredStatusEffect(
                    StatusEffectId.Strength,
                    2,
                    1),
                owner);

            controller.AddStatus(
                new DeferredStatusEffect(
                    StatusEffectId.Strength,
                    3,
                    1),
                owner);

            DeferredStatusEffect[] numericQueued =
                controller.CharacterStatuses
                    .OfType<DeferredStatusEffect>()
                    .Where(x =>
                        x.DeferredStatusId ==
                        StatusEffectId.Strength)
                    .ToArray();

            Add(
                checks,
                "P4-D01",
                "Deferred Numeric reservations remain independent",
                numericQueued.Length == 2 &&
                numericQueued.Any(x => x.Stack == 2) &&
                numericQueued.Any(x => x.Stack == 3),
                "QueuedStrength=" +
                string.Join(
                    ", ",
                    numericQueued.Select(
                        x => $"{x.Stack}·{x.PendingDuration}")));

            controller.AddStatus(
                new DeferredStatusEffect(
                    StatusEffectId.Pain,
                    1,
                    5),
                owner);

            controller.AddStatus(
                new DeferredStatusEffect(
                    StatusEffectId.Pain,
                    1,
                    2),
                owner);

            DeferredStatusEffect[] painQueued =
                controller.CharacterStatuses
                    .OfType<DeferredStatusEffect>()
                    .Where(x =>
                        x.DeferredStatusId ==
                        StatusEffectId.Pain)
                    .ToArray();

            Add(
                checks,
                "P4-D02",
                "Deferred Presence reservation max-refreshes",
                painQueued.Length == 1 &&
                painQueued[0].PendingDuration == 5,
                painQueued.Length == 0
                    ? "DeferredPain missing"
                    : $"Count={painQueued.Length}, T={painQueued[0].PendingDuration}");

            controller.AddStatus(
                new DeferredStatusEffect(
                    StatusEffectId.Swift,
                    4,
                    1),
                owner);

            controller.OnTurnStart();

            StrengthStatus[] materializedStrength =
                controller.CharacterStatuses
                    .OfType<StrengthStatus>()
                    .ToArray();

            SwiftStatus swift =
                controller.CharacterStatuses
                    .OfType<SwiftStatus>()
                    .FirstOrDefault();

            Add(
                checks,
                "P4-D03",
                "Deferred Numeric entries materialize independently at TurnStart",
                materializedStrength.Length == 2 &&
                materializedStrength.Sum(x => x.Stack) == 5,
                $"StrengthCount={materializedStrength.Length}, Total={materializedStrength.Sum(x => x.Stack)}");

            int swiftBonus =
                swift?.GetSpeedMaximumIncrease(
                    null) ??
                0;

            Add(
                checks,
                "P4-D04",
                "Reserved Swift materializes before speed phase",
                swift != null &&
                swift.Stack == 4 &&
                swiftBonus == 4,
                $"Swift={(swift == null ? 0 : swift.Stack)}, SpeedMaxBonus={swiftBonus}");

            SwiftStatus immediate =
                new SwiftStatus(
                    2,
                    1);

            immediate.DecreaseDuration();

            Add(
                checks,
                "P4-D05",
                "Immediate T1 Swift follows normal current-turn decay",
                immediate.Duration == 0 &&
                immediate.IsExpired,
                $"Duration={immediate.Duration}, Expired={immediate.IsExpired}");
        }
        catch (Exception exception)
        {
            Add(
                checks,
                "P4-DXX",
                "Deferred runtime probe completed without exception",
                false,
                exception.GetType().Name +
                ": " +
                exception.Message);
        }
        finally
        {
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void AddOrderingChecks(
        List<Check> checks)
    {
        string turnManager =
            ReadSource(
                "Assets/1. Scripts/Runtime/Systems/BattleFlow/TurnManager.cs");

        int characterStart =
            turnManager.IndexOf(
                "RunCharacterTurnStart();",
                StringComparison.Ordinal);

        int eventStart =
            turnManager.IndexOf(
                ".RaiseTurnStart(CurrentTurn);",
                StringComparison.Ordinal);

        int speed =
            turnManager.IndexOf(
                "speedManager?.RollAllSpeed();",
                StringComparison.Ordinal);

        Add(
            checks,
            "P4-T01",
            "TurnStart order = queued status -> queued mark/event -> speed",
            characterStart >= 0 &&
            eventStart > characterStart &&
            speed > eventStart,
            $"CharacterStart={characterStart}, TurnStartEvent={eventStart}, Speed={speed}");

        string yujin =
            ReadSource(
                "Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs");

        bool queuedMark =
            yujin.Contains(
                "QueueMarkForNextTurn",
                StringComparison.Ordinal) &&
            yujin.Contains(
                "MaterializePendingMarks();",
                StringComparison.Ordinal) &&
            yujin.Contains(
                "AddMark(",
                StringComparison.Ordinal);

        Add(
            checks,
            "P4-T02",
            "Delayed Mark has TurnStart materialization path",
            queuedMark,
            queuedMark
                ? "Queue -> MaterializePendingMarks -> AddMark canonical path"
                : "Delayed Mark path missing");
    }

    private static void AddPlanningChecks(
        List<Check> checks)
    {
        List<int> rollbackOrder = new();
        PlanningUndoJournal journal = new();

        journal.Record(
            () =>
                rollbackOrder.Add(1));

        journal.Record(
            () =>
                rollbackOrder.Add(2));

        bool rolled =
            journal.RollbackAll(
                out string reason);

        Add(
            checks,
            "P4-U01",
            "Planning undo journal rolls back in reverse commit order",
            rolled &&
            rollbackOrder.SequenceEqual(
                new[] { 2, 1 }) &&
            journal.Count == 0,
            $"Rolled={rolled}, Order={string.Join(",", rollbackOrder)}, Remaining={journal.Count}, Reason={reason}");

        string addStatus =
            ReadSource(
                "Assets/1. Scripts/Runtime/Skills/Effects/Common/AddBodyPartStatusEffect.cs");

        bool exact =
            addStatus.Contains(
                "[0922_PHASE4_EXACT_STATUS_UNDO]",
                StringComparison.Ordinal) &&
            addStatus.Contains(
                "RestoreStateForPlanning",
                StringComparison.Ordinal) &&
            addStatus.Contains(
                "RemoveStatus(",
                StringComparison.Ordinal);

        Add(
            checks,
            "P4-U02",
            "Planning status undo records exact merge/insert rollback",
            exact,
            exact
                ? "Exact status rollback wiring detected"
                : "Exact status rollback wiring missing");
    }

    private static string ReadSource(
        string relative)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                relative.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        return File.Exists(path)
            ? File.ReadAllText(path)
            : string.Empty;
    }

    private static void Add(
        List<Check> checks,
        string id,
        string name,
        bool passed,
        string actual)
    {
        checks.Add(
            new Check
            {
                Id = id,
                Name = name,
                Passed = passed,
                Actual = actual
            });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        int pass,
        int fail,
        string result)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string directory =
            Path.Combine(
                root,
                "Logs",
                "GameSystemVerification");

        Directory.CreateDirectory(
            directory);

        string path =
            Path.Combine(
                directory,
                "0922_Phase4_TimingDeferred.md");

        List<string> lines = new()
        {
            "# 0922 Phase 4 — Timing / Deferred / Reservation",
            string.Empty,
            $"- PASS: {pass}",
            $"- FAIL: {fail}",
            $"- RESULT: {result}",
            string.Empty,
            "## Checks",
            string.Empty
        };

        foreach (Check check in checks)
        {
            lines.Add(
                $"- [{(check.Passed ? "x" : " ")}] " +
                $"`{check.Id}` {check.Name} — {check.Actual}");
        }

        File.WriteAllLines(
            path,
            lines);

        Debug.Log(
            "[0922 Phase 4] Report written: " +
            path);
    }
}
#endif
