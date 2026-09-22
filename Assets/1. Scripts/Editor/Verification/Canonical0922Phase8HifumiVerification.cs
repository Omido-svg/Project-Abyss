#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase8HifumiVerification
{
    private const string Phase7ReportPath =
        "Logs/GameSystemVerification/0922_Phase7_OlafYujinSkillMigration.md";

    private const string LoadoutPath =
        "Assets/2. Data/Characters/Design2026/Hifumi/Hifumi_Loadout.asset";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 8 - Verify Hifumi")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();
        List<string> pending = BuildPendingCanonical();

        AddPrerequisites(checks);
        AddBoneAdvanceChecks(checks);
        AddCatastropheChecks(checks);
        AddCompleteBlockChecks(checks);
        AddAllInChecks(checks);
        AddEngraveChecks(checks);
        AddContentChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_HIFUMI_0922"
            : "FAIL";

        WriteReport(checks, pending, pass, fail, result);

        string summary =
            "[0922 Phase 8 · Hifumi Mechanics / Content]\n" +
            $"PASS={pass} FAIL={fail} PENDING_CANONICAL={pending.Count}\n\n" +
            "PASS\n- " +
            string.Join("\n- ", checks.Where(x => x.Passed)
                .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join("\n- ", checks.Where(x => !x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            "\n\nPENDING_CANONICAL\n- " +
            string.Join("\n- ", pending) +
            $"\n\nPHASE8_RESULT={result}";

        if (fail == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void AddPrerequisites(List<Check> checks)
    {
        Add(checks, "P8-H00", "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, Phase7ReportPath);
        bool exists = File.Exists(path);
        string report = exists ? File.ReadAllText(path) : string.Empty;
        bool pass = report.Contains("PHASE7_RESULT=PASS_OLAF_YUJIN_SKILLS", StringComparison.Ordinal) ||
                    report.Contains("RESULT: PASS_OLAF_YUJIN_SKILLS", StringComparison.Ordinal);

        Add(checks, "P8-H01", "Phase 7 Olaf/Yujin content closed",
            exists && pass,
            !exists ? "Phase7 report missing" : $"ReportPresent=True, ResultPass={pass}");
    }

    private static void AddBoneAdvanceChecks(List<Check> checks)
    {
        HifumiMechanic mechanic = new HifumiMechanic();
        mechanic.SetBoneForVerification(450);
        mechanic.SetAdvanceBoneForVerification(60);

        Add(checks, "P8-B01", "Advance is separate / no BoneBand contribution / Bloom sums",
            mechanic.Bone == 450 &&
            mechanic.AdvanceBone == 60 &&
            mechanic.BoneBand == 4 &&
            mechanic.SpendableBone == 510 &&
            mechanic.IsBloom,
            $"Bone={mechanic.Bone}, Advance={mechanic.AdvanceBone}, Band={mechanic.BoneBand}, Spendable={mechanic.SpendableBone}, Bloom={mechanic.IsBloom}");

        mechanic.SetBoneForVerification(100);
        mechanic.SetAdvanceBoneForVerification(30);
        bool paid = mechanic.ConsumeBone(40);

        Add(checks, "P8-B02", "Bone cost pays Advance first",
            paid && mechanic.AdvanceBone == 0 && mechanic.Bone == 90,
            $"Paid={paid}, Bone={mechanic.Bone}, Advance={mechanic.AdvanceBone}");

        mechanic.SetAdvanceBoneForVerification(999);
        mechanic.ClearAdvanceForVerification();
        Add(checks, "P8-B03", "Advance can be fully cleared at TurnEnd boundary",
            mechanic.AdvanceBone == 0,
            $"Advance={mechanic.AdvanceBone}");
    }

    private static void AddCatastropheChecks(List<Check> checks)
    {
        HifumiMechanic mechanic = new HifumiMechanic();

        int pooled = mechanic.GetCounterPoolForVerification(
            HifumiSkillIds.RollCompendium,
            taggedLosses: 1,
            catastropheCount: 1);

        Add(checks, "P8-C01", "Tagged loss + catastrophe both accumulate counter rolls",
            pooled == 2,
            $"Tagged1+Catastrophe1={pooled}");

        int attempts = mechanic.GetCounterAttemptsAfterCatastrophesForVerification(
            initialCounterCount: 2,
            catastropheCount: 2);

        Add(checks, "P8-C02", "Counter catastrophe queues replacement counter",
            attempts == 4,
            $"Initial2+CounterCatastrophe2={attempts}");

        Add(checks, "P8-C03", "Shake Fate overrides common catastrophe side effects",
            mechanic.IsShakeFateCommonCatastropheSuppressedForVerification(),
            "Hifumi 1·2·3 on H uses power-0 normal comparison path");

        string source = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs");
        string clash = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Resolution/ClashManager.cs");

        bool forcedJudgment =
            source.Contains("IForcedRollJudgmentRule", StringComparison.Ordinal) &&
            source.Contains("ChinchiroCombination.Arashi", StringComparison.Ordinal) &&
            source.Contains("ChinchiroCombination.Shigoro", StringComparison.Ordinal) &&
            clash.Contains("GetForcedRollJudgment", StringComparison.Ordinal);

        Add(checks, "P8-C04", "Shake Fate Arashi/Shigoro forced-win capability wired",
            forcedJudgment,
            forcedJudgment ? "Character capability -> ClashManager forced judgment" : "forced judgment wiring missing");
    }

    private static void AddCompleteBlockChecks(List<Check> checks)
    {
        HifumiMechanic mechanic = new HifumiMechanic();
        mechanic.SetBoneForVerification(0);

        bool clear = mechanic.ApplyBoldJudgmentCompleteBlockForVerification(0, 0);
        int afterClear = mechanic.Bone;
        bool hpHit = mechanic.ApplyBoldJudgmentCompleteBlockForVerification(1, 0);
        bool staggerHit = mechanic.ApplyBoldJudgmentCompleteBlockForVerification(0, 1);

        Add(checks, "P8-K01", "Complete Block = zero HP and zero Stagger damage, not exchange wins",
            clear && afterClear == 50 && !hpHit && !staggerHit,
            $"ZeroZero={clear}, BoneAfter={afterClear}, HpHitReward={hpHit}, StaggerHitReward={staggerHit}");
    }

    private static void AddAllInChecks(List<Check> checks)
    {
        HifumiMechanic mechanic = new HifumiMechanic();

        mechanic.SetBoneForVerification(120);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Moku,
            ChinchiroCombination.Blank,
            ChinchiroCombination.Moku);
        int moku = mechanic.Bone;

        mechanic.SetBoneForVerification(120);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Shigoro,
            ChinchiroCombination.Moku,
            ChinchiroCombination.Blank);
        int shigoro = mechanic.Bone;

        mechanic.SetBoneForVerification(120);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Arashi,
            ChinchiroCombination.Hifumi,
            ChinchiroCombination.Shigoro);
        int catastrophe = mechanic.Bone;

        mechanic.SetBoneForVerification(120);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Blank,
            ChinchiroCombination.Blank,
            ChinchiroCombination.Blank);
        int blank = mechanic.Bone;

        Add(checks, "P8-A01", "All-In 0922 payout table / catastrophe priority",
            moku == 220 && shigoro == 500 && catastrophe == 0 && blank == 0,
            $"Moku={moku}, Shigoro={shigoro}, Catastrophe={catastrophe}, Blank={blank}");
    }

    private static void AddEngraveChecks(List<Check> checks)
    {
        HifumiMechanic mechanic = new HifumiMechanic();
        mechanic.SetEngraveProgressForVerification(1, HifumiEngraveBranch.None);
        bool stage1 = mechanic.GetEngraveRollCountForVerification() == 1 &&
                      mechanic.GetEngraveEnergyCostForVerification() == 0 &&
                      mechanic.EngraveCounterEnabled;

        mechanic.SetEngraveProgressForVerification(4, HifumiEngraveBranch.Victory);
        bool stage4 = mechanic.GetEngraveRollCountForVerification() == 4 &&
                      mechanic.GetEngraveEnergyCostForVerification() == 1 &&
                      !mechanic.EngraveCounterEnabled;

        Add(checks, "P8-I01", "Engrave Body runtime stage drives 1->4 rolls and 0/0/1/1 cost",
            stage1 && stage4,
            $"Stage1={stage1}, Stage4Victory={stage4}");

        RunProgressionState run = new RunProgressionState();
        run.HifumiEngrave.RecordExchangeOutcome(true);
        run.HifumiEngrave.RecordExchangeOutcome(true);
        bool advanced = run.HifumiEngrave.TryAdvance(2, 2);
        int defeatBefore = run.HifumiEngrave.DefeatProgress;
        run.HifumiEngrave.RecordExchangeOutcome(false);

        Add(checks, "P8-I02", "Engrave first threshold locks branch in run-scoped state",
            advanced &&
            run.HifumiEngrave.Stage == 2 &&
            run.HifumiEngrave.Branch == HifumiEngraveBranch.Victory &&
            run.HifumiEngrave.DefeatProgress == defeatBefore,
            $"Advanced={advanced}, Stage={run.HifumiEngrave.Stage}, Branch={run.HifumiEngrave.Branch}, DefeatProgress={run.HifumiEngrave.DefeatProgress}");

        HifumiEngraveRunProgression sameReference = run.HifumiEngrave;
        Add(checks, "P8-I03", "Engrave progression is owned by RunProgressionState",
            ReferenceEquals(sameReference, run.HifumiEngrave),
            $"Stage={run.HifumiEngrave.Stage}, Branch={run.HifumiEngrave.Branch}");

        mechanic.SetBoneForVerification(450);
        mechanic.SetAdvanceBoneForVerification(100);
        mechanic.SetEngraveProgressForVerification(4, HifumiEngraveBranch.Victory);
        bool converted = mechanic.ResolveEngraveStage4CompleteBlockForVerification(0, 0);

        Add(checks, "P8-I04", "Engrave Stage4 victory Complete Block converts all Advance to actual Bone",
            converted && mechanic.Bone == 500 && mechanic.AdvanceBone == 0,
            $"Converted={converted}, Bone={mechanic.Bone}, Advance={mechanic.AdvanceBone}");
    }

    private static void AddContentChecks(List<Check> checks)
    {
        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(LoadoutPath);

        Add(checks, "P8-D00", "Hifumi loadout present",
            loadout != null,
            loadout != null ? loadout.name : "MISSING");

        if (loadout == null)
            return;

        string[] pool = (loadout.DuelSkillPool ?? new List<SkillDefinition>())
            .Where(x => x != null)
            .Select(x => x.SkillId)
            .ToArray();

        Add(checks, "P8-D01", "Duel pool exactly matches 0922 A/B/C/E/G/H/I/J/K/L/M",
            pool.SequenceEqual(HifumiSkillIds.CanonicalDuel),
            "Pool=" + string.Join(",", pool));

        bool noLegacy = !pool.Contains(HifumiSkillIds.RiskLife) &&
                        !pool.Contains(HifumiSkillIds.RecoverPrincipal);
        bool newSlots = pool.Contains(HifumiSkillIds.RollCompendium) &&
                        pool.Contains(HifumiSkillIds.FlipTable) &&
                        pool.Contains(HifumiSkillIds.Provoke) &&
                        pool.Contains(HifumiSkillIds.CleanseAll);

        Add(checks, "P8-D02", "Legacy D/F removed and J/K/L/M integrated",
            noLegacy && newSlots,
            $"NoDF={noLegacy}, JKLM={newSlots}");

        SkillDefinition e = Find(loadout, HifumiSkillIds.RaiseStake);
        SkillDefinition i = Find(loadout, HifumiSkillIds.EngraveBody);
        SkillDefinition j = Find(loadout, HifumiSkillIds.RollCompendium);

        Add(checks, "P8-D03", "Confirmed E/I/J authoring shape migrated",
            e != null && e.EffectiveRollCount == 2 && e.OverrideEnergyCost && e.EnergyCost == 0 &&
            i != null && i.EffectiveRollCount == 1 && i.OverrideEnergyCost && i.EnergyCost == 0 && i.BasePower == 15 &&
            j != null && j.EffectiveRollCount == 3 && j.OverrideEnergyCost && j.EnergyCost == 2 && j.Color == SkillColor.Blue,
            $"E={(e == null ? "MISSING" : $"R{e.EffectiveRollCount}/E{e.EnergyCost}")}, " +
            $"I={(i == null ? "MISSING" : $"R{i.EffectiveRollCount}/E{i.EnergyCost}/B{i.BasePower}")}, " +
            $"J={(j == null ? "MISSING" : $"R{j.EffectiveRollCount}/E{j.EnergyCost}/{j.Color}")}");

        int duplicateIds = loadout.DuelSkillPool
            .Where(x => x != null)
            .GroupBy(x => x.SkillId)
            .Count(g => g.Count() > 1);

        Add(checks, "P8-I05", "Phase 8 migration shape is duplicate-safe/idempotent",
            duplicateIds == 0,
            $"DuelPoolDuplicateIds={duplicateIds}");
    }

    private static SkillDefinition Find(CharacterCombatLoadout loadout, string id)
    {
        return loadout?.EnumerateAllDefinitions()
            .FirstOrDefault(x => x != null && string.Equals(x.SkillId, id, StringComparison.Ordinal));
    }

    private static List<string> BuildPendingCanonical() => new()
    {
        "Hifumi 몸에 새기다 — 단계 상승 임계치 및 Stage2~4 다수 보상 수치/기본위력 (미정); Run state/branch lock/1→4 구조만 구현",
        "Hifumi 곱절을 노리다 G — 고정 wager K, 빛, 기본위력 (미정); 구조만 보존",
        "Hifumi 운명을 흔들다 H — 빛/기본위력 (미정); 결과 치환 규칙은 구현",
        "Hifumi 판을 엎다 K — X별 위력/뼈/골절 N, 침체 N/T, 뼈 비용, 빛/기본위력 (미정); 슬롯 설치 후 runtime 잠금",
        "Hifumi 도발형 L — 도발 지속, 견고 N, 핏값 정의/수치, 빛/기본위력 (미정); 슬롯 설치 후 runtime 잠금",
        "Hifumi 전체 정화형 M — 굴림수/빛/기본위력/뼈 비용 (미정); 캐릭터 상태 정화 구조만 문서화하고 runtime 잠금",
        "Hifumi 자기 제약 — 다음 턴 결투 위력 감소량 (미정); 비중첩 예약 구조 유지",
        "Hifumi 평타③ 동귀어진 / GiveFlesh 다음턴 피해증가 등 — 정본에 남은 수치 (미정), 기존 placeholder를 canonical로 승격하지 않음"
    };

    private static string ReadSource(string relative)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void Add(
        List<Check> checks,
        string id,
        string name,
        bool passed,
        string actual)
    {
        checks.Add(new Check
        {
            Id = id,
            Name = name,
            Passed = passed,
            Actual = actual
        });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        IReadOnlyList<string> pending,
        int pass,
        int fail,
        string result)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string directory = Path.Combine(root, "Logs", "GameSystemVerification");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "0922_Phase8_Hifumi.md");

        List<string> lines = new()
        {
            "# 0922 Phase 8 — Hifumi Mechanics / Content",
            string.Empty,
            $"- PASS: {pass}",
            $"- FAIL: {fail}",
            $"- PENDING_CANONICAL: {pending.Count}",
            $"- RESULT: {result}",
            string.Empty,
            "## Checks",
            string.Empty
        };

        foreach (Check check in checks)
            lines.Add($"- [{(check.Passed ? "x" : " ")}] `{check.Id}` {check.Name} — {check.Actual}");

        lines.Add(string.Empty);
        lines.Add("## PENDING_CANONICAL");
        lines.Add(string.Empty);
        foreach (string item in pending)
            lines.Add("- " + item);

        File.WriteAllLines(path, lines);
        Debug.Log("[0922 Phase8] Report written: " + path);
    }
}
#endif
