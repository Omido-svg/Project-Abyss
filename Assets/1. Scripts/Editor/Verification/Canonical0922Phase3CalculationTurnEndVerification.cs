#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 Phase 3 — Calculation / TurnEnd Aggregation gate.
/// Phase 2의 독립 저장 Entry를 실제 정본 수식으로 집계하는지 검증한다.
/// </summary>
public static class Canonical0922Phase3CalculationTurnEndVerification
{
    private const string Phase2ReportPath =
        "Logs/GameSystemVerification/0922_Phase2_CoreStatusStorage.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 3 - Verify Calculation and TurnEnd")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisiteChecks(checks);
        AddPureCalculationChecks(checks);
        AddRuntimeContractChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;

        string summary =
            "[0922 Phase 3 · Calculation / TurnEnd Aggregation]\n" +
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
            "\n\n" +
            (fail == 0
                ? "PHASE3_RESULT=PASS_CALCULATION_TURNEND"
                : "PHASE3_RESULT=FAIL");

        WriteReport(checks, pass, fail);

        if (fail > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static void AddPrerequisiteChecks(List<Check> checks)
    {
        Add(
            checks,
            "P3-H00",
            "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string projectRoot =
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        string reportPath =
            Path.Combine(projectRoot, Phase2ReportPath);

        bool exists = File.Exists(reportPath);
        string report =
            exists
                ? File.ReadAllText(reportPath)
                : string.Empty;

        bool phase2Pass =
            report.Contains(
                "RESULT: PASS_CORE_STORAGE",
                StringComparison.Ordinal) ||
            report.Contains(
                "PHASE2_RESULT=PASS_CORE_STORAGE",
                StringComparison.Ordinal);

        Add(
            checks,
            "P3-H01",
            "Phase 2 core storage closed",
            exists && phase2Pass,
            !exists
                ? "Phase2 report missing"
                : $"ReportPresent=True, ResultPass={phase2Pass}");
    }

    private static void AddPureCalculationChecks(List<Check> checks)
    {
        StatusEffect[] rollStatuses =
        {
            new StrengthStatus(3, 3),
            new StrengthStatus(2, 1),
            new WeaknessStatus(4, 2),
            new OlafFearStatus(3),
            new OlafFearStatus(2)
        };

        int strength =
            CommonStatusAlgebra.GetNumericTotal<StrengthStatus>(
                rollStatuses);

        int weakness =
            CommonStatusAlgebra.GetNumericTotal<WeaknessStatus>(
                rollStatuses);

        int fear =
            CommonStatusAlgebra.GetFearRollPenalty(
                rollStatuses);

        Add(
            checks,
            "P3-R01",
            "Roll axis = StrengthTotal - WeaknessTotal, Fear additional -1",
            strength == 5 &&
            weakness == 4 &&
            fear == 1 &&
            (strength - weakness - fear) == 0,
            $"Strength={strength}, Weakness={weakness}, Fear={fear}, Net={strength - weakness - fear}");

        StatusEffect[] fractureStatuses =
        {
            new FractureStatus(2, 3),
            new FractureStatus(3, 1)
        };

        int fracture =
            CommonStatusAlgebra.GetNumericTotal<FractureStatus>(
                fractureStatuses);

        Add(
            checks,
            "P3-R02",
            "Fracture maximum reduction aggregates",
            fracture == 5,
            $"FractureTotal={fracture}");

        StatusEffect[] hpStatuses =
        {
            new ProtectionStatus(5, 3),
            new RuptureStatus(5, 1)
        };

        int hpFlat =
            CommonStatusAlgebra.GetHpDamageFlatModifier(
                hpStatuses,
                null);

        int hpResult =
            Mathf.Max(0, 2 + hpFlat);

        Add(
            checks,
            "P3-D01",
            "HP flat axis is order-independent",
            hpFlat == 0 && hpResult == 2,
            $"RawAfterResistance=2, Flat={hpFlat}, Result={hpResult}");

        StatusEffect[] staggerStatuses =
        {
            new SturdyStatus(5, 3),
            new DisarmStatus(3, 2)
        };

        int staggerFlat =
            CommonStatusAlgebra.GetStaggerDamageFlatModifier(
                staggerStatuses,
                null);

        Add(
            checks,
            "P3-D02",
            "Stagger flat axis = DisarmTotal - SturdyTotal",
            staggerFlat == -2,
            $"Disarm=3, Sturdy=5, Flat={staggerFlat}");

        StatusEffect[] prestigeStatuses =
        {
            new HeatStatus(3, 2),
            new HeatStatus(2, 1),
            new StagnationStatus(1, 3)
        };

        int prestigeDelta =
            CommonStatusAlgebra.GetTurnEndPrestigeDelta(
                prestigeStatuses);

        Add(
            checks,
            "P3-T01",
            "TurnEnd prestige = HeatTotal - StagnationTotal",
            prestigeDelta == 4,
            $"Heat=5, Stagnation=1, Delta={prestigeDelta}");

        StatusEffect[] regenStatuses =
        {
            new RegenerationStatus(
                3,
                4,
                RegenerationRecoveryChannel.HitPoints),
            new RegenerationStatus(
                2,
                3,
                RegenerationRecoveryChannel.Stagger)
        };

        int regen =
            CommonStatusAlgebra.GetRegenerationTotal(
                regenStatuses);

        Add(
            checks,
            "P3-T02",
            "Regeneration aggregates one N regardless of legacy channel",
            regen == 7,
            $"RegenTotal={regen}");

        StatusEffect[] painStatuses =
        {
            new PainStatus(3),
            new PainStatus(2)
        };

        int heal9 =
            CommonStatusAlgebra.ApplyHealingModifiers(
                painStatuses,
                9);

        int heal1 =
            CommonStatusAlgebra.ApplyHealingModifiers(
                painStatuses,
                1);

        Add(
            checks,
            "P3-T03",
            "Pain halves healing once, floor, minimum 1",
            heal9 == 4 && heal1 == 1,
            $"Heal9={heal9}, Heal1={heal1}");

        StagnationStatus stagnation =
            new StagnationStatus(8, 4);

        Add(
            checks,
            "P3-T04",
            "Stagnation is NumericTimed",
            stagnation.StorageKind == StatusEffectStorageKind.NumericTimed &&
            stagnation.NumericValue == 8 &&
            stagnation.Duration == 4,
            $"N={stagnation.NumericValue}, T={stagnation.Duration}");
    }

    private static void AddRuntimeContractChecks(List<Check> checks)
    {
        var heatOnApply =
            typeof(HeatStatus).GetMethod(
                nameof(StatusEffect.OnApply),
                Type.EmptyTypes);

        var regenTurnEnd =
            typeof(RegenerationStatus).GetMethod(
                nameof(StatusEffect.OnTurnEnd),
                new[] { typeof(StatusEffectTickContext) });

        Add(
            checks,
            "P3-C01",
            "Heat and Regeneration no longer execute per-entry TurnEnd gameplay",
            heatOnApply?.DeclaringType == typeof(StatusEffect) &&
            regenTurnEnd?.DeclaringType == typeof(StatusEffect),
            $"HeatOnApplyOwner={heatOnApply?.DeclaringType?.Name}, RegenTurnEndOwner={regenTurnEnd?.DeclaringType?.Name}");

        string controller =
            ReadProjectSource(
                "Assets/1. Scripts/Runtime/Characters/CharacterBase/CharacterStatusController.cs");

        int aggregateIndex =
            controller.IndexOf(
                "ApplyCanonicalTurnEndAggregates();",
                StringComparison.Ordinal);

        int tickIndex =
            controller.IndexOf(
                "TickCharacterStatuses(",
                aggregateIndex >= 0 ? aggregateIndex : 0,
                StringComparison.Ordinal);

        Add(
            checks,
            "P3-C02",
            "TurnEnd aggregate runs before duration tick",
            aggregateIndex >= 0 &&
            tickIndex > aggregateIndex,
            $"AggregateIndex={aggregateIndex}, TickIndex={tickIndex}");

        string damage =
            ReadProjectSource(
                "Assets/1. Scripts/Runtime/Battle/Damage/DamagePipeline.cs");

        Add(
            checks,
            "P3-C03",
            "DamagePipeline uses aggregate HP flat axis",
            damage.Contains(
                "CommonStatusAlgebra.GetHpDamageFlatModifier",
                StringComparison.Ordinal),
            "Aggregate HP flat helper wired");

        string stagger =
            ReadProjectSource(
                "Assets/1. Scripts/Runtime/Characters/CharacterBase/StaggerGaugeMechanic.cs");

        Add(
            checks,
            "P3-C04",
            "Stagger healing passes through common healing modifier",
            stagger.Contains(
                "owner?.ModifyHealingAmount(amount)",
                StringComparison.Ordinal),
            "Stagger Recover -> Character.ModifyHealingAmount");

        string character =
            ReadProjectSource(
                "Assets/1. Scripts/Runtime/Characters/CharacterBase/Character.cs");

        Add(
            checks,
            "P3-C05",
            "Fear is handled as a fixed presence penalty outside Numeric shift",
            character.Contains(
                "CommonStatusAlgebra.GetFearRollPenalty",
                StringComparison.Ordinal) &&
            character.Contains(
                "commonShift - fearPenalty",
                StringComparison.Ordinal),
            "Fear penalty wired separately from Strength/Weakness");

        string resources =
            ReadProjectSource(
                "Assets/1. Scripts/Runtime/Characters/CharacterBase/CharacterResourceController.cs");

        Add(
            checks,
            "P3-C06",
            "Signed prestige adjustment exists for Heat/Stagnation net delta",
            resources.Contains(
                "public int AdjustPrestige(int delta)",
                StringComparison.Ordinal),
            "Signed prestige adjustment wired");
    }

    private static string ReadProjectSource(string relativePath)
    {
        string root =
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        string path =
            Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

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
        int fail)
    {
        string projectRoot =
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        string directory =
            Path.Combine(
                projectRoot,
                "Logs",
                "GameSystemVerification");

        Directory.CreateDirectory(directory);

        string path =
            Path.Combine(
                directory,
                "0922_Phase3_CalculationTurnEnd.md");

        List<string> lines = new()
        {
            "# 0922 Phase 3 — Calculation / TurnEnd Aggregation",
            string.Empty,
            $"- PASS: {pass}",
            $"- FAIL: {fail}",
            $"- RESULT: {(fail == 0 ? "PASS_CALCULATION_TURNEND" : "FAIL")}",
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

        File.WriteAllLines(path, lines);

        Debug.Log(
            "[0922 Phase 3] Report written: " + path);
    }
}
#endif
