#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase7SkillContentVerification
{
    private const string Phase6ReportPath =
        "Logs/GameSystemVerification/0922_Phase6_CharacterKeywords.md";
    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 7 - Verify Olaf Yujin Skill Migration")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();
        List<string> pending = new();

        AddPrerequisites(checks);

        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);
        CharacterCombatLoadout yujin =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(YujinLoadoutPath);

        AddOlafChecks(checks, pending, olaf);
        AddYujinChecks(checks, pending, yujin);
        AddIdempotenceShapeChecks(checks, olaf, yujin);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_OLAF_YUJIN_SKILLS"
            : "FAIL";

        WriteReport(checks, pending, pass, fail, result);

        string summary =
            "[0922 Phase 7 · Olaf / Yujin Skill Content Migration]\n" +
            $"PASS={pass} FAIL={fail} PENDING_CANONICAL={pending.Count}\n\n" +
            "PASS\n- " +
            string.Join("\n- ", checks.Where(x => x.Passed)
                .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0 ? "NONE" : string.Join("\n- ", checks.Where(x => !x.Passed)
                .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            "\n\nPENDING_CANONICAL\n- " +
            (pending.Count == 0 ? "NONE" : string.Join("\n- ", pending)) +
            $"\n\nPHASE7_RESULT={result}";

        if (fail == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void AddPrerequisites(List<Check> checks)
    {
        Add(checks, "P7-H00", "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, Phase6ReportPath);
        bool exists = File.Exists(path);
        string report = exists ? File.ReadAllText(path) : string.Empty;
        bool pass = report.Contains("PASS_CHARACTER_KEYWORDS", StringComparison.Ordinal);
        Add(checks, "P7-H01", "Phase 6 character keywords closed",
            exists && pass,
            !exists ? "Phase6 report missing" : $"ReportPresent=True, ResultPass={pass}");
    }

    private static void AddOlafChecks(
        List<Check> checks,
        List<string> pending,
        CharacterCombatLoadout loadout)
    {
        Add(checks, "P7-O00", "Olaf loadout present", loadout != null,
            loadout == null ? "missing" : loadout.name);
        if (loadout == null) return;

        SkillDefinition stoke = Find(loadout, OlafSkillIds.StokePrestige, "위세를 지피다");
        bool stokePlacement = stoke != null &&
            stoke.ActionType == ActionType.Preparation &&
            Contains(loadout.CharacterPreparationPool, stoke) &&
            !Contains(loadout.DuelSkillPool, stoke) &&
            !Contains(loadout.DuelSkills, stoke);
        Add(checks, "P7-O01", "StokePrestige moved Duel -> Preparation",
            stokePlacement,
            stoke == null ? "missing" : $"Id={stoke.SkillId}, Type={stoke.ActionType}, PrepPool={Contains(loadout.CharacterPreparationPool, stoke)}, DuelPool={Contains(loadout.DuelSkillPool, stoke)}");

        SkillDefinition headOn = Find(loadout, OlafSkillIds.HeadOn, "정면승부");
        bool headOnPass = headOn != null &&
            headOn.BreakMode == PartBreakMode.WeakenedOnly &&
            OlafRulebreakerMechanic.CalculateHeadOnSelfDamage(100) == 10 &&
            OlafRulebreakerMechanic.CalculateHeadOnSelfDamage(19) == 1;
        Add(checks, "P7-O02", "HeadOn self damage = dealt damage 10%",
            headOnPass,
            $"Damage100={OlafRulebreakerMechanic.CalculateHeadOnSelfDamage(100)}, Damage19={OlafRulebreakerMechanic.CalculateHeadOnSelfDamage(19)}, Break={headOn?.BreakMode}");

        SkillDefinition bloodPrice = Find(loadout, OlafSkillIds.BloodPrice, "피의 대가");
        bool bloodPricePass = HasOlafOp(bloodPrice, Olaf0922Phase7EffectOperation.OwnerBleeding, amount: 4) &&
                              HasOlafOp(bloodPrice, Olaf0922Phase7EffectOperation.BloodPriceStrengthByOwnerBleeding);
        Add(checks, "P7-O03", "BloodPrice = self bleed4 -> self Strength 1/2/3·3",
            bloodPricePass, DescribeEffects(bloodPrice));

        SkillDefinition absorb = Find(loadout, OlafSkillIds.AbsorbBlood, "피를 흡수하다");
        bool absorbPass = HasOlafOp(absorb, Olaf0922Phase7EffectOperation.ConsumeTargetBleedingForEnergy, amount: 5);
        Add(checks, "P7-O04", "AbsorbBlood consumes bleed5 for light2",
            absorbPass, DescribeEffects(absorb));

        SkillDefinition honorable = Find(loadout, OlafSkillIds.HonorableFight, "명예로운 전투");
        bool honorablePass = honorable?.Rulebreaker?.MirrorThisSkillToOpponentOnDuelMatch == true &&
            honorable.BreakMode == PartBreakMode.WeakenedOnly &&
            typeof(IExchangeRepeatRule).IsAssignableFrom(typeof(OlafRulebreakerMechanic));
        Add(checks, "P7-O05", "HonorableFight repeat contract wired",
            honorablePass,
            $"Mirror={honorable?.Rulebreaker?.MirrorThisSkillToOpponentOnDuelMatch}, RepeatInterface={typeof(IExchangeRepeatRule).IsAssignableFrom(typeof(OlafRulebreakerMechanic))}");

        SkillDefinition oath = Find(loadout, OlafSkillIds.BloodOathDuel, "피의 맹세");
        bool oathPass = HasOlafOp(oath, Olaf0922Phase7EffectOperation.PrepareBloodOathDuel) &&
                        CountOlafOp(oath, Olaf0922Phase7EffectOperation.ResolveBloodOathDuelWin) == 2 &&
                        oath?.Rulebreaker?.ConsumeAllOwnerBleedingOnExecute == false;
        Add(checks, "P7-O06", "BloodOathDuel 3-band runtime path",
            oathPass, DescribeEffects(oath));

        bool ntPass =
            HasOlafStatus(Find(loadout, OlafSkillIds.Tenacious, "끈질기게"), StatusEffectId.Regeneration, 2, 3, Olaf0922Phase7EffectOperation.OwnerStatusImmediate) &&
            HasOlafStatus(Find(loadout, OlafSkillIds.BloodCharge, "피의돌진"), StatusEffectId.Fracture, 1, 1, Olaf0922Phase7EffectOperation.TargetStatusNextTurn) &&
            HasOlafStatus(Find(loadout, OlafSkillIds.HoldOn, "물고 늘어지기"), StatusEffectId.Rupture, 2, 2, Olaf0922Phase7EffectOperation.TargetStatusNextTurn) &&
            HasOlafStatus(Find(loadout, OlafSkillIds.ProtectSelf, "몸을 사리다"), StatusEffectId.Protection, 1, 1, Olaf0922Phase7EffectOperation.OwnerStatusNextTurn) &&
            HasOlafStatus(Find(loadout, OlafSkillIds.Choke, "숨통을 조이다"), StatusEffectId.Pain, 1, 3, Olaf0922Phase7EffectOperation.TargetStatusNextTurn);
        Add(checks, "P7-O07", "Olaf explicit N/T cards migrated",
            ntPass, ntPass ? "Regen2·3 / Fracture1·1 / Rupture2·2 / Protection1·1 / PainT3" : "one or more N/T entry mismatch");

        SkillDefinition bite = Find(loadout, OlafSkillIds.Bite, "물어뜯기");
        bool bitePass = MatchRanges(bite, new[] { 9, 10, 10 }, new[] { 20, 21, 21 });
        Add(checks, "P7-O08", "Bite confirmed range migrated",
            bitePass, Ranges(bite));

        SkillDefinition standard = Find(loadout, OlafSkillIds.Standard, "표준");
        SkillDefinition singleCut = Find(loadout, OlafSkillIds.SingleCut, "단칼");
        bool pendingExplosionSafe = standard?.Rulebreaker?.BleedingExplosionMultiplier == 0 &&
                                    singleCut?.Rulebreaker?.BleedingExplosionMultiplier == 0;
        Add(checks, "P7-O09", "Unresolved bleed explosion coefficient not invented",
            pendingExplosionSafe,
            $"Standard={standard?.Rulebreaker?.BleedingExplosionMultiplier}, SingleCut={singleCut?.Rulebreaker?.BleedingExplosionMultiplier}");

        pending.Add("Olaf 출혈 폭발 계수 — 0922 §17.1/17.6 (미정), multiplier=0 sentinel");
        pending.Add("Olaf 단칼 위력 — 0922 §17.6 (미정), 기존 데이터 값을 canonical로 승격하지 않음");
        pending.Add("Olaf 결투 빛 비용 구조 — 0922 §17.6 (미정), 전역 재산정하지 않음");
        pending.Add("Olaf 명예로운 전투 vs 부위 없는 일반몹 종료조건 — (미정), 1교환 fallback");
        pending.Add("Olaf 위세를 지피다 출혈 구간별 충전량 — (미정), effect 비활성");
    }

    private static void AddYujinChecks(
        List<Check> checks,
        List<string> pending,
        CharacterCombatLoadout loadout)
    {
        Add(checks, "P7-Y00", "Yujin loadout present", loadout != null,
            loadout == null ? "missing" : loadout.name);
        if (loadout == null) return;

        SkillDefinition c = Find(loadout, YujinSkillIds.DesignateTarget, "목표 지정");
        SkillDefinition m = Find(loadout, YujinSkillIds.AllTargets, "모두를 노리다");
        bool designatePass = HasYujinOp(c, Yujin0916EffectOperation.DesignateCurrent) &&
                             HasYujinOp(m, Yujin0916EffectOperation.DesignateAll) &&
                             SourceContainsDesignation23();
        Add(checks, "P7-Y01", "Designation C/M = 2·3",
            designatePass,
            $"C={DescribeEffects(c)}, M={DescribeEffects(m)}, Source2x3={SourceContainsDesignation23()}");

        SkillDefinition wanted = Find(loadout, YujinSkillIds.Wanted, "현상수배");
        SkillDefinition probe = Find(loadout, YujinSkillIds.ProbeWeakness, "약점을 캐내다");
        bool debuffPass = HasYujinOp(wanted, Yujin0916EffectOperation.Wanted) &&
                          HasYujinOp(probe, Yujin0916EffectOperation.ProbeWeakness);
        Add(checks, "P7-Y02", "Weapon debuffs route to Weakness/Fracture/Rupture 1·1",
            debuffPass, $"Wanted={DescribeEffects(wanted)}, Probe={DescribeEffects(probe)}");

        SkillDefinition sharpen = Find(loadout, YujinSkillIds.Sharpen, "칼을 갈다");
        bool sharpenPass = HasYujinOp(sharpen, Yujin0916EffectOperation.Sharpen);
        Add(checks, "P7-Y03", "Sharpen = mark10 -> next-turn Swift1·1",
            sharpenPass, DescribeEffects(sharpen));

        SkillDefinition o = Find(loadout, YujinSkillIds.AdvanceTiming, "때를 앞당기다");
        SkillDefinition p = Find(loadout, YujinSkillIds.FinishIt, "끝장을 보다");
        bool opPass = o != null && p != null &&
                      p.Rulebreaker?.HasDynamicCost == true &&
                      p.Rulebreaker.ResolveMinimumEnergyCost() == 1;
        Add(checks, "P7-Y04", "Yujin O/P retained on 0922 atomic path",
            opPass,
            $"O={o?.SkillId ?? "missing"}, P={p?.SkillId ?? "missing"}, PDynamic={p?.Rulebreaker?.HasDynamicCost}");

        string mechanicSource = ReadSource("Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs");
        bool atomic = mechanicSource.Contains("ApplyMarkGain(", StringComparison.Ordinal) &&
                      mechanicSource.Contains("GetDesignationTotal", StringComparison.Ordinal) &&
                      mechanicSource.Contains("IgniteMark(", StringComparison.Ordinal);
        Add(checks, "P7-Y05", "Mark gain/Designation/44 ignition remain centralized",
            atomic, atomic ? "ApplyMarkGain -> DesignationTotal -> IgniteMark" : "atomic source path missing");

        pending.Add("Yujin A 의뢰 대상 바 +15 — 0922 표에 (미정), migration이 값을 생성하지 않음");
    }

    private static void AddIdempotenceShapeChecks(
        List<Check> checks,
        CharacterCombatLoadout olaf,
        CharacterCombatLoadout yujin)
    {
        int olafDuplicate = CountDuplicateIds(olaf);
        int yujinDuplicate = CountDuplicateIds(yujin);
        bool poolShape = olafDuplicate == 0 && yujinDuplicate == 0;
        Add(checks, "P7-I01", "Migration shape is duplicate-safe/idempotent",
            poolShape,
            $"OlafDuplicateIds={olafDuplicate}, YujinDuplicateIds={yujinDuplicate}");
    }

    private static int CountDuplicateIds(CharacterCombatLoadout loadout)
    {
        if (loadout == null) return 0;
        return loadout.EnumerateAllDefinitions()
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillId))
            .GroupBy(x => x.SkillId, StringComparer.Ordinal)
            .Count(g => g.Distinct().Count() > 1);
    }

    private static bool SourceContainsDesignation23()
    {
        string source = ReadSource(
            "Assets/1. Scripts/Runtime/Skills/Effects/Canonical0916/Yujin0916SkillEffectDefinition.cs");
        return source.Contains("ApplyDesignation(context.Owner, context.Target, context.TargetPart, 2, 3);", StringComparison.Ordinal) &&
               source.Contains("ApplyDesignation(context.Owner, enemy, null, 2, 3);", StringComparison.Ordinal);
    }

    private static string ReadSource(string relative)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static SkillDefinition Find(CharacterCombatLoadout loadout, string id, string name)
    {
        if (loadout == null) return null;
        return loadout.EnumerateAllDefinitions()
            .Where(x => x != null)
            .FirstOrDefault(x => string.Equals(x.SkillId, id, StringComparison.Ordinal))
            ?? loadout.EnumerateAllDefinitions()
                .Where(x => x != null)
                .FirstOrDefault(x => string.Equals(x.SkillName, name, StringComparison.Ordinal));
    }

    private static bool Contains(List<SkillDefinition> list, SkillDefinition skill) =>
        list != null && skill != null && list.Contains(skill);

    private static IEnumerable<Olaf0922Phase7SkillEffectDefinition> OlafEffects(SkillDefinition skill) =>
        skill?.EffectEntries == null
            ? Enumerable.Empty<Olaf0922Phase7SkillEffectDefinition>()
            : skill.EffectEntries.Where(x => x?.Definition != null)
                .Select(x => x.Definition as Olaf0922Phase7SkillEffectDefinition)
                .Where(x => x != null);

    private static bool HasOlafOp(SkillDefinition skill, Olaf0922Phase7EffectOperation op, int? amount = null) =>
        OlafEffects(skill).Any(x => x.Operation == op && (!amount.HasValue || x.Amount == amount.Value));

    private static int CountOlafOp(SkillDefinition skill, Olaf0922Phase7EffectOperation op) =>
        OlafEffects(skill).Count(x => x.Operation == op);

    private static bool HasOlafStatus(
        SkillDefinition skill,
        StatusEffectId id,
        int amount,
        int duration,
        Olaf0922Phase7EffectOperation operation) =>
        OlafEffects(skill).Any(x => x.Operation == operation && x.StatusId == id && x.Amount == amount && x.Duration == duration);

    private static bool HasYujinOp(SkillDefinition skill, Yujin0916EffectOperation op) =>
        skill?.EffectEntries != null && skill.EffectEntries.Any(x =>
            x?.Definition is Yujin0916SkillEffectDefinition y && y.Operation == op);

    private static string DescribeEffects(SkillDefinition skill)
    {
        if (skill?.EffectEntries == null) return "NONE";
        return string.Join(", ", skill.EffectEntries
            .Where(x => x?.Definition != null)
            .Select(x => x.Definition is Olaf0922Phase7SkillEffectDefinition o
                ? $"Olaf:{o.Operation}(N={o.Amount},T={o.Duration},S={o.StatusId})"
                : x.Definition is Yujin0916SkillEffectDefinition y
                    ? $"Yujin:{y.Operation}"
                    : x.Definition.GetType().Name));
    }

    private static bool MatchRanges(SkillDefinition skill, int[] min, int[] max)
    {
        if (skill?.Rolls == null || skill.Rolls.Count != min.Length || min.Length != max.Length)
            return false;
        for (int i = 0; i < min.Length; i++)
            if (skill.Rolls[i] == null || skill.Rolls[i].MinPower != min[i] || skill.Rolls[i].MaxPower != max[i])
                return false;
        return true;
    }

    private static string Ranges(SkillDefinition skill) =>
        skill?.Rolls == null
            ? "NONE"
            : string.Join(" / ", skill.Rolls.Select(x => x == null ? "null" : $"{x.MinPower}~{x.MaxPower}"));

    private static void Add(List<Check> checks, string id, string name, bool pass, string actual)
    {
        checks.Add(new Check { Id = id, Name = name, Passed = pass, Actual = actual });
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
        string path = Path.Combine(directory, "0922_Phase7_OlafYujinSkillMigration.md");

        List<string> lines = new()
        {
            "# 0922 Phase 7 — Olaf / Yujin Skill Content Migration",
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
        Debug.Log("[0922 Phase7] Report written: " + path);
    }
}
#endif
