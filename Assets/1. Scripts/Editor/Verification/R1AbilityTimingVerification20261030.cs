#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-10-30 R1 canonical trigger/duel gate regression checks.
/// Reuses the G0 temporary-battle probes without altering production content.
/// Run from Play Mode with an initialized BattleManager.
/// </summary>
public static class R1AbilityTimingVerification20261030
{
    [Serializable]
    private sealed class Root { public List<Scenario> Scenarios; }

    [Serializable]
    private sealed class Scenario
    {
        public string Id;
        public int AttackWeight;
        public int SecondaryHitCount;
        public int DamageEventCount;
        public int OneSidedHitCount;
        public List<Probe> ProbeCounts;
    }

    [Serializable]
    private sealed class Probe
    {
        public string Label;
        public int FirstOwnerCount;
        public int SecondOwnerCount;
    }

    [Serializable]
    private sealed class Report
    {
        public int Pass;
        public int Fail;
        public string[] Failures;
        public string GeneratedAtUtc;
    }

    [MenuItem("Game System Verification/R1 - Canonical Trigger Gate (Play Mode)")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("R1 검증은 초기화된 전투가 있는 Play Mode에서 실행해야 합니다.");

        // Existing test fixture isolates its temporary actors and destroys probes.
        G0AbilityAuditReport20261029.RunGoldenBaselineFromMenu();

        string root = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(root, "G0_Ability_Audit_20261029");
        string file = Path.Combine(dir, "G0_GOLDEN_BASELINE.json");
        Root baseline = JsonUtility.FromJson<Root>(File.ReadAllText(file));
        if (baseline?.Scenarios == null)
            throw new InvalidOperationException("G0 Golden data is unavailable.");

        int pass = 0;
        List<string> failures = new List<string>();

        void Check(string label, bool valid, string actual)
        {
            if (valid)
            {
                pass++;
                Debug.Log("[R1] PASS: " + label + " | " + actual);
            }
            else
            {
                failures.Add(label + " | " + actual);
                Debug.LogError("[R1] FAIL: " + label + " | " + actual);
            }
        }

        Scenario Find(string id) => baseline.Scenarios.FirstOrDefault(s => s.Id == id);
        int Count(string id, string label, bool first = true)
        {
            Probe p = Find(id)?.ProbeCounts?.FirstOrDefault(x => x.Label == label);
            return p == null ? -1 : first ? p.FirstOwnerCount : p.SecondOwnerCount;
        }

        Check("R1-T01 3-roll OnUse once per roll",
            Count("G0-G01", "OnExecute") == 3,
            "count=" + Count("G0-G01", "OnExecute"));

        Check("R1-T02 1-roll OnUse once",
            Count("G0-G02", "OnExecute") == 1,
            "count=" + Count("G0-G02", "OnExecute"));

        Check("R1-T03 duel vs duel roll-use",
            Count("G0-G03", "OnExecute") == 3 &&
            Count("G0-G03", "OnExecute", false) == 3,
            "first/second=" + Count("G0-G03", "OnExecute") + "/" + Count("G0-G03", "OnExecute", false));

        Check("R1-T04 duel vs normal full card effect gate",
            Count("G0-G04", "OnExecute") == 0 &&
            Count("G0-G04", "OnClashEnd") == 0 &&
            Count("G0-G04", "AfterDamage") == 0 &&
            Count("G0-G04", "OnExecute", false) == 1,
            "duel use/end/damage=" +
            Count("G0-G04", "OnExecute") + "/" +
            Count("G0-G04", "OnClashEnd") + "/" +
            Count("G0-G04", "AfterDamage"));

        Check("R1-T05 unilateral normal hit has no exchange victory",
            Count("G0-G05", "OnExecute") == 3 &&
            Count("G0-G05", "OnExchangeWin") == 0 &&
            Count("G0-G05", "OnClashEnd") == 0,
            "use/win/end=" + Count("G0-G05", "OnExecute") + "/" +
            Count("G0-G05", "OnExchangeWin") + "/" + Count("G0-G05", "OnClashEnd"));

        Check("R1-T06 remaining one-sided rolls use but never win",
            Count("G0-G06", "OnExecute") == 3 &&
            Count("G0-G06", "OnExchangeWin") == 1,
            "use/win=" + Count("G0-G06", "OnExecute") + "/" + Count("G0-G06", "OnExchangeWin"));

        Check("R1-T07 additional damage does not double-fire source AfterDamage",
            Count("G0-G07", "AfterDamage") == 1 &&
            Find("G0-G07")?.DamageEventCount == 3,
            "afterDamage=" + Count("G0-G07", "AfterDamage") +
            ";events=" + (Find("G0-G07")?.DamageEventCount ?? -1));

        Check("R1-T08 DuelMatched removed from NEW authoring, legacy enum stable",
            !SkillEffectTimingCatalog.IsAuthoringTiming(SkillEffectTiming.OnDuelMatched) &&
            (int)SkillEffectTiming.OnDuelMatched == 31 &&
            Count("G0-G03", "OnDuelMatched") == 3,
            "legacy-value=31; old probe=" + Count("G0-G03", "OnDuelMatched"));

        Check("R1-T09 new card phase enums do not reuse legacy IDs",
            (int)SkillEffectTiming.OnAttackResolutionStarting == 35 &&
            (int)SkillEffectTiming.OnPreparationActivated == 36 &&
            SkillEffectTimingCatalog.IsAuthoringTiming(SkillEffectTiming.OnAttackResolutionStarting) &&
            SkillEffectTimingCatalog.IsAuthoringTiming(SkillEffectTiming.OnPreparationActivated),
            "new ids=35/36");

        Check("R1-T10 old weighted hit 3 still functions",
            Find("G0-W03")?.SecondaryHitCount == 2 &&
            Find("G0-W03")?.AttackWeight == 3,
            "secondary=" + (Find("G0-W03")?.SecondaryHitCount ?? -1));

        Report report = new Report
        {
            Pass = pass,
            Fail = failures.Count,
            Failures = failures.ToArray(),
            GeneratedAtUtc = DateTime.UtcNow.ToString("o")
        };
        File.WriteAllText(Path.Combine(dir, "R1_TRIGGER_GATE_VERIFY.json"), JsonUtility.ToJson(report, true));
        Debug.Log("[R1] RESULT PASS=" + pass + " FAIL=" + failures.Count);
        if (failures.Count > 0)
            throw new InvalidOperationException("R1 checks failed: " + string.Join(" | ", failures));
    }
}
#endif
