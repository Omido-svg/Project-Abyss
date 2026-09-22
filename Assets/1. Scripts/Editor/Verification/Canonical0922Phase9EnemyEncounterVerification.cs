#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase9EnemyEncounterVerification
{
    private const string Phase8ReportPath =
        "Logs/GameSystemVerification/0922_Phase8_Hifumi.md";

    private const string EncounterCatalogPath =
        "Assets/Resources/Canonical0922/EnemyEncounterCatalog.asset";
    private const string NormalEnemyLoadoutPath =
        "Assets/2. Data/Characters/Enemies/NormalEnemy/Loadout/NormalEnemy Combat Loadout.asset";

    private static readonly string[] NormalEnemyDataPaths =
    {
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 1.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 2.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 3.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 4.asset"
    };

    private const string BossBundlePath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Bundle.asset";
    private const string BossDataPath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Data.asset";
    private const string BossPhasePath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Phase01.asset";
    private const string BossAPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_A_Duel.asset";
    private const string BossBPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Duel.asset";
    private const string BossBFallbackPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Fallback.asset";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 9 - Verify Enemy Encounter")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();
        List<string> pending = BuildPendingCanonical();

        AddPrerequisites(checks);
        AddEncounterChecks(checks);
        AddRoleAndQuotaChecks(checks);
        AddCoverChecks(checks);
        AddEliteBossChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_ENEMY_ENCOUNTER_0922"
            : "FAIL";

        WriteReport(checks, pending, pass, fail, result);

        string summary =
            "[0922 Phase 9 · Enemy / Encounter / Boss]\n" +
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
            $"\n\nPHASE9_RESULT={result}";

        if (fail == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void AddPrerequisites(List<Check> checks)
    {
        Add(checks, "P9-H00", "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, Phase8ReportPath);
        bool exists = File.Exists(path);
        string report = exists ? File.ReadAllText(path) : string.Empty;
        bool passed = report.Contains("PHASE8_RESULT=PASS_HIFUMI_0922", StringComparison.Ordinal) ||
                      report.Contains("RESULT: PASS_HIFUMI_0922", StringComparison.Ordinal);

        Add(checks, "P9-H01", "Phase 8 Hifumi closed",
            exists && passed,
            !exists ? "Phase8 report missing" : $"ReportPresent=True, ResultPass={passed}");
    }

    private static void AddEncounterChecks(List<Check> checks)
    {
        Canonical0922EnemyEncounterCatalog catalog =
            AssetDatabase.LoadAssetAtPath<Canonical0922EnemyEncounterCatalog>(
                EncounterCatalogPath);

        Add(checks, "P9-E00", "Canonical normal encounter catalog present",
            catalog != null,
            catalog != null ? catalog.name : "MISSING");

        if (catalog == null)
            return;

        List<Canonical0922EnemyEncounterEntry> entries =
            catalog.NormalEncounters?.Where(x => x != null).ToList() ?? new();
        List<Canonical0922EnemyEncounterEntry> three =
            entries.Where(x => x.EnemyCount == 3).ToList();
        List<Canonical0922EnemyEncounterEntry> four =
            entries.Where(x => x.EnemyCount == 4).ToList();

        float total = entries.Sum(x => x.OverallProbability);
        float total3 = three.Sum(x => x.OverallProbability);
        float total4 = four.Sum(x => x.OverallProbability);

        Add(checks, "P9-E01", "3/4 enemy encounter weights = 25/75 with 20 compositions",
            entries.Count == 20 &&
            three.Count == 10 && four.Count == 10 &&
            Near(catalog.ThreeEnemyProbability, 0.25f) &&
            Near(catalog.FourEnemyProbability, 0.75f) &&
            Near(total, 1f) && Near(total3, 0.25f) && Near(total4, 0.75f) &&
            three.All(x => Near(x.OverallProbability, 0.025f)) &&
            four.All(x => Near(x.OverallProbability, 0.075f)),
            $"Entries={entries.Count}, 3={three.Count}/{total3:0.###}, 4={four.Count}/{total4:0.###}, Total={total:0.###}");

        bool exactCompositions = VerifyExactCompositions(entries);
        Add(checks, "P9-E02", "Canonical 10 + 10 role compositions are exact",
            exactCompositions,
            exactCompositions ? "20/20 exact" : "composition mismatch");

        bool hpEconomy =
            Canonical0922EnemyEncounterRules.ThreeEnemyHp == 135 &&
            Canonical0922EnemyEncounterRules.FourEnemyHp == 101 &&
            Canonical0922EnemyEncounterRules.ThreeEnemyHp * 3 == 405 &&
            Canonical0922EnemyEncounterRules.FourEnemyHp * 4 == 404 &&
            Canonical0922EnemyEncounterRules.NormalEnemyRollCount == 3 &&
            Canonical0922EnemyEncounterRules.NormalEnemyStaggerMax == 100;

        Add(checks, "P9-E03", "Normal enemy HP pool / 1-slot 3-roll / stagger contract",
            hpEconomy,
            "3x135=405, 4x101=404, Rolls=3, Stagger=100");

        bool speciesCounts =
            catalog.AttackerSpeciesCount == 3 &&
            catalog.DuelistSpeciesCount == 3 &&
            catalog.BufferSpeciesCount == 2 &&
            catalog.TankSpeciesCount == 2 &&
            catalog.DebufferSpeciesCount == 2;
        Add(checks, "P9-E03B", "Stage1 normal roster role counts = 3/3/2/2/2",
            speciesCounts,
            $"Attacker={catalog.AttackerSpeciesCount}, Duelist={catalog.DuelistSpeciesCount}, Buffer={catalog.BufferSpeciesCount}, Tank={catalog.TankSpeciesCount}, Debuffer={catalog.DebufferSpeciesCount}");

        bool threshold =
            Canonical0922EnemyEncounterRules.RollEnemyCount(0.10f) == 3 &&
            Canonical0922EnemyEncounterRules.RollEnemyCount(0.249f) == 3 &&
            Canonical0922EnemyEncounterRules.RollEnemyCount(0.25f) == 4 &&
            Canonical0922EnemyEncounterRules.RollEnemyCount(0.90f) == 4;

        string switcher = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/BattleFlow/Test/BattleTestScenarioSwitcher.cs");
        bool runtimeWired =
            switcher.Contains("Canonical0922EnemyEncounterRules.RollEnemyCount", StringComparison.Ordinal) &&
            switcher.Contains("AddRepeated(result, normalEnemyPrefab, normalCount)", StringComparison.Ordinal);

        Add(checks, "P9-E04", "Normal battle runtime rolls 3/4 encounter size from canonical weights",
            threshold && runtimeWired,
            $"Thresholds={threshold}, ScenarioWired={runtimeWired}");

        CharacterCombatLoadout normalLoadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                NormalEnemyLoadoutPath);
        List<SkillDefinition> normalCombatSkills = new();
        if (normalLoadout?.NormalSkillPool != null)
            normalCombatSkills.AddRange(normalLoadout.NormalSkillPool.Where(x => x != null));
        if (normalLoadout?.DuelSkillPool != null)
            normalCombatSkills.AddRange(normalLoadout.DuelSkillPool.Where(x => x != null));
        normalCombatSkills = normalCombatSkills.Distinct().ToList();
        bool d8 = normalCombatSkills.Count > 0 && normalCombatSkills.All(skill =>
            skill.ResolverType == SkillResolverType.Dice &&
            skill.EffectiveRollCount == 3 &&
            skill.Rolls != null &&
            skill.Rolls.Count == 3 &&
            skill.Rolls.All(roll => roll != null &&
                roll.RngSource == RollRngSource.Dice &&
                roll.MinPower == 1 && roll.MaxPower == 8));

        Add(checks, "P9-E05", "Normal enemy RNG source is uniform D8 on all 3 rolls",
            d8,
            $"CombatSkills={normalCombatSkills.Count}, D8={d8}");

        List<CharacterData> normalData = NormalEnemyDataPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<CharacterData>(path))
            .Where(x => x != null)
            .ToList();
        bool normalResources = normalData.Count == NormalEnemyDataPaths.Length &&
            normalData.All(x =>
                x.maxEnergy == 3 &&
                x.EnableStaggerGauge &&
                x.OverrideMaxStaggerGauge &&
                x.GetEffectiveMaxStaggerGauge(null) == 100);
        Add(checks, "P9-E06", "Normal enemy energy/stagger baseline = Light3 / Stagger100",
            normalResources,
            $"Assets={normalData.Count}/{NormalEnemyDataPaths.Length}, Baseline={normalResources}");
    }

    private static void AddRoleAndQuotaChecks(List<Check> checks)
    {
        bool roleWeights =
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Attacker, true), 0.50f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Attacker, true), 0.50f) &&
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Duelist, true), 0.25f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Duelist, true), 0.75f) &&
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Debuffer, true), 0.50f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Debuffer, true), 0.50f) &&
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Buffer, true), 0f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Buffer, true), 0f) &&
            Near(Canonical0922EnemyEncounterRules.GetPreparationWeight(EnemyRole0922.Buffer, true), 1f) &&
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Buffer, false), 0.75f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Buffer, false), 0.25f) &&
            Near(Canonical0922EnemyEncounterRules.GetNormalWeight(EnemyRole0922.Tank, true), 0.50f) &&
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Tank, true), 0.50f);

        Add(checks, "P9-R01", "Role action tendencies match 0922 table",
            roleWeights,
            roleWeights ? "Attacker50/50 Duelist25/75 Debuffer50/50 BufferPrep100 Tank50/50" : "role weight mismatch");

        bool colors =
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Attacker, true, 0.99f) == SkillColor.Red &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Duelist, true, 0.99f) == SkillColor.Red &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Debuffer, true, 0.50f) == SkillColor.Blue &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Debuffer, true, 0.90f) == SkillColor.Red &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Buffer, true, 0.20f) == SkillColor.Unset &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Buffer, false, 0.20f) == SkillColor.Red &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Buffer, false, 0.80f) == SkillColor.Blue &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Tank, true, 0.20f) == SkillColor.Red &&
            Canonical0922EnemyEncounterRules.RollPreferredColor(EnemyRole0922.Tank, true, 0.80f) == SkillColor.Blue;

        Add(checks, "P9-R02", "Role color tendencies match 0922 table",
            colors,
            colors ? "RED / BLUE75-25 / mixed50-50" : "role color mismatch");

        bool dealerSplit =
            Canonical0922EnemyEncounterRules.ResolveDealerSlot(0.10f) == EnemyRole0922.Attacker &&
            Canonical0922EnemyEncounterRules.ResolveDealerSlot(0.90f) == EnemyRole0922.Duelist;
        Add(checks, "P9-R03", "Dealer slot resolves Attacker/Duelist 50/50",
            dealerSplit,
            $"0.10={Canonical0922EnemyEncounterRules.ResolveDealerSlot(0.10f)}, 0.90={Canonical0922EnemyEncounterRules.ResolveDealerSlot(0.90f)}");

        bool quota =
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.10f) == 1 &&
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.249f) == 1 &&
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.25f) == 2 &&
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.749f) == 2 &&
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.75f) == 3 &&
            Canonical0922EnemyEncounterRules.RollDuelQuota(0.99f) == 3;

        string coordinator = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs");
        bool weightedGlobal =
            coordinator.Contains("WeightedPickWithoutReplacement", StringComparison.Ordinal) &&
            coordinator.Contains("GetDuelWeight", StringComparison.Ordinal) &&
            coordinator.Contains("requestedQuota", StringComparison.Ordinal);
        bool bufferExcluded =
            Near(Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Buffer, true), 0f);

        Add(checks, "P9-D01", "Encounter-wide Duel quota = 1/2/3 at 25/50/25",
            quota && weightedGlobal,
            $"Thresholds={quota}, GlobalWeightedAllocator={weightedGlobal}");

        Add(checks, "P9-D02", "Buffer with living ally is excluded from Duel candidates",
            bufferExcluded,
            $"BufferDuelWeight={Canonical0922EnemyEncounterRules.GetDuelWeight(EnemyRole0922.Buffer, true):0.##}");
    }

    private static void AddCoverChecks(List<Check> checks)
    {
        bool priority =
            Canonical0922EnemyEncounterRules.GetCoverPriority(0.50f, EnemyRole0922.Attacker) == 0 &&
            Canonical0922EnemyEncounterRules.GetCoverPriority(0.51f, EnemyRole0922.Buffer) == 1 &&
            Canonical0922EnemyEncounterRules.GetCoverPriority(0.90f, EnemyRole0922.Attacker) == 2;

        Add(checks, "P9-C01", "Tank cover target priority = <=50% HP -> Buffer -> other",
            priority,
            priority ? "LowHP=0, Buffer=1, Other=2" : "priority mismatch");

        string clash = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Resolution/ClashManager.cs");
        string cover = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs");

        int directGate = clash.IndexOf("if (!cameFromClash)", StringComparison.Ordinal);
        int redirect = clash.IndexOf("EnemyCover0922Resolver.TryRedirectOneSided", StringComparison.Ordinal);
        int oneSideDamage = clash.IndexOf("DamagePowerResolver.ResolveOneSided", StringComparison.Ordinal);

        bool semantics =
            directGate >= 0 &&
            redirect > directGate &&
            oneSideDamage > redirect &&
            clash.Contains("0922_PHASE9_ONE_SIDED_COVER_REDIRECT", StringComparison.Ordinal) &&
            cover.Contains("action.Slot.TargetCharacter = tank", StringComparison.Ordinal) &&
            cover.Contains("mechanic.CoverTarget != originalTarget", StringComparison.Ordinal);

        Add(checks, "P9-C02", "Tank cover redirects only direct one-sided attacks before damage",
            semantics,
            semantics ? "!cameFromClash gate -> retarget -> one-sided damage" : "cover wiring/order missing");
    }

    private static void AddEliteBossChecks(List<Check> checks)
    {
        EnemyPostureSettings posture = new EnemyPostureSettings();
        string postureSource = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EnemyPostureMechanic.cs");

        bool postureDefaults =
            posture.MinimumTurns == 2 && posture.MaximumTurns == 3 &&
            posture.NormalAttackSlotLimit == 3 &&
            posture.CrouchingAttackSlotLimit == 2 &&
            posture.OffensiveAttackSlotLimit == 4;
        bool sequence =
            postureSource.Contains("EnemyPosture.Normal => EnemyPosture.Crouching", StringComparison.Ordinal) &&
            postureSource.Contains("EnemyPosture.Crouching => EnemyPosture.Offensive", StringComparison.Ordinal) &&
            postureSource.Contains("_ => EnemyPosture.Normal", StringComparison.Ordinal);

        Add(checks, "P9-B01", "Elite/Boss posture rotates Normal3 -> Crouching2 -> Offensive4 every 2-3 turns",
            postureDefaults && sequence,
            $"Slots={posture.NormalAttackSlotLimit}/{posture.CrouchingAttackSlotLimit}/{posture.OffensiveAttackSlotLimit}, Turns={posture.MinimumTurns}-{posture.MaximumTurns}, Sequence={sequence}");

        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BossBundlePath);
        bool fiveParts = false;
        string partActual = "Bundle=MISSING";
        if (bundle != null)
        {
            SerializedObject so = new SerializedObject(bundle);
            SerializedProperty parts = so.FindProperty("eliteBodyPartDefinitions");
            int count = parts?.arraySize ?? 0;
            bool hp150 = count == 5;
            if (parts != null && parts.isArray)
            {
                for (int i = 0; i < parts.arraySize; i++)
                {
                    SerializedProperty hp = parts.GetArrayElementAtIndex(i)
                        ?.FindPropertyRelative("MaxPartHP");
                    hp150 &= hp != null && Near(hp.floatValue, 150f);
                }
            }
            fiveParts = count == 5 && hp150;
            partActual = $"Parts={count}, Each150={hp150}";
        }

        ScaledBodyPartTargetModel bossScale = new ScaledBodyPartTargetModel(2f);
        ScaledBodyPartTargetModel eliteScale = new ScaledBodyPartTargetModel(1.5f);
        bool scale =
            bossScale.ScaleForVerification(750) == 1500 &&
            eliteScale.ScaleForVerification(500) == 750;
        string eliteSource = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EliteEnemy.cs");
        bool runtimeScaleWired =
            eliteSource.Contains("new ScaledBodyPartTargetModel(multiplier)", StringComparison.Ordinal) &&
            eliteSource.Contains("CombatantTier.Boss => 2.0f", StringComparison.Ordinal) &&
            eliteSource.Contains("CombatantTier.EliteEnemy => 1.5f", StringComparison.Ordinal);

        Add(checks, "P9-B02", "5-part boss and tier-scaled Whole HP are wired",
            fiveParts && scale && runtimeScaleWired,
            $"{partActual}, Boss750x2={bossScale.ScaleForVerification(750)}, Elite500x1.5={eliteScale.ScaleForVerification(500)}, Runtime={runtimeScaleWired}");

        SkillDefinition a = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossAPath);
        SkillDefinition b = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossBPath);
        SkillDefinition fallback = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossBFallbackPath);

        bool skills =
            IsBossSkill(a, ActionType.Duel, 13, 3, 1) &&
            IsBossSkill(b, ActionType.Duel, 14, 2, 1) &&
            IsBossSkill(fallback, ActionType.NormalAttack, 14, 2, 0) &&
            a != null && !a.CanBreakPart && a.BreakMode == PartBreakMode.None &&
            b != null && !b.CanBreakPart && b.BreakMode == PartBreakMode.None;

        Add(checks, "P9-B03", "Stage1 Boss A/B and B light-shortage fallback match 0922",
            skills,
            $"A={BossSkillActual(a)}, B={BossSkillActual(b)}, Fallback={BossSkillActual(fallback)}");

        BossPhaseData phase = AssetDatabase.LoadAssetAtPath<BossPhaseData>(BossPhasePath);
        CharacterData bossData = AssetDatabase.LoadAssetAtPath<CharacterData>(BossDataPath);
        bool aab = phase?.SlotConfigs != null && phase.SlotConfigs.Count == 3 &&
                   phase.SlotConfigs[0]?.FixedSkill == a &&
                   phase.SlotConfigs[1]?.FixedSkill == a &&
                   phase.SlotConfigs[2]?.FixedSkill == b &&
                   phase.SlotConfigs[2]?.InsufficientEnergyFallbackSkill == fallback &&
                   phase.SlotConfigs.All(x => x != null && x.TargetingPolicy == AITargetingPolicy.RandomValid);
        bool budget = bossData != null && bossData.maxEnergy == 2 &&
                      bossData.GetEffectiveMaxStaggerGauge(null) == 400;

        Add(checks, "P9-B04", "Stage1 Boss pattern A·A·B / random target / light2 / stagger400",
            aab && budget,
            $"AAB={aab}, MaxEnergy={(bossData != null ? bossData.maxEnergy : -1)}, Stagger={(bossData != null ? bossData.GetEffectiveMaxStaggerGauge(null) : -1)}");
    }

    private static bool IsBossSkill(
        SkillDefinition skill,
        ActionType actionType,
        int power,
        int rolls,
        int energy)
    {
        if (skill == null ||
            skill.ActionType != actionType ||
            skill.BasePower != power ||
            skill.EffectiveRollCount != rolls ||
            !skill.OverrideEnergyCost ||
            skill.EnergyCost != energy ||
            skill.Rolls == null ||
            skill.Rolls.Count != rolls)
        {
            return false;
        }

        return skill.Rolls.All(x => x != null && x.MinPower == power && x.MaxPower == power);
    }

    private static string BossSkillActual(SkillDefinition skill) =>
        skill == null
            ? "MISSING"
            : $"{skill.SkillId}:{skill.ActionType}/P{skill.BasePower}/R{skill.EffectiveRollCount}/E{skill.EnergyCost}/Break{skill.CanBreakPart}";

    private static bool VerifyExactCompositions(
        IReadOnlyList<Canonical0922EnemyEncounterEntry> entries)
    {
        Dictionary<string, EnemyRole0922[]> expected = new()
        {
            ["4-01"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer },
            ["4-02"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer, EnemyRole0922.Buffer },
            ["4-03"] = new[] { EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Buffer },
            ["4-04"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Debuffer, EnemyRole0922.Buffer },
            ["4-05"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot },
            ["4-06"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer },
            ["4-07"] = new[] { EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer },
            ["4-08"] = new[] { EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer },
            ["4-09"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot },
            ["4-10"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer },
            ["3-01"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot },
            ["3-02"] = new[] { EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer },
            ["3-03"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Buffer },
            ["3-04"] = new[] { EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer },
            ["3-05"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer },
            ["3-06"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer },
            ["3-07"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Debuffer },
            ["3-08"] = new[] { EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer },
            ["3-09"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer },
            ["3-10"] = new[] { EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot }
        };

        if (entries == null || entries.Count != expected.Count)
            return false;

        foreach (Canonical0922EnemyEncounterEntry entry in entries)
        {
            if (entry == null ||
                !expected.TryGetValue(entry.Id ?? string.Empty, out EnemyRole0922[] roles) ||
                entry.Roles == null ||
                !entry.Roles.SequenceEqual(roles))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> BuildPendingCanonical() => new()
    {
        "Tank 엄호 — 두 탱커가 같은 아군을 동시에 엄호할 수 있는지, 지속/해제 세부는 (미정); Phase 9는 단일 active cover의 일방타격 redirect 계약만 고정",
        "Duel quota — 결투 후보가 목표 1/2/3보다 적을 때의 처리 방식 (미정); 현재 가용 후보까지만 배정하고 PENDING 로그",
        "1스테이지 일반 몹 12종 — 역할별 종수만 확정, 이름·개별 스킬·수치는 (미정); 공통 role policy만 구현",
        "일반/엘리트 적 조준 정책 — 1스테이지 Boss의 random 외에는 (미정)",
        "엘리트 6종·중간보스 2종·메인보스 2종의 실제 부위 구성/절대 HP/스킬은 (미정); 5부위·WholeHP 배수·자세 계약만 구현",
        "Stage1 Boss A/B 색 — (미정), migration이 기존 Color 값을 canonical로 확정하지 않음",
        "Stage1 Boss 별도 파괴 스킬 — (미정), A/B에는 파괴 권한을 주지 않음",
        "적 디버프 스킬과 공세 자세 변형 — (미정), 임의 효과를 생성하지 않음"
    };

    private static bool Near(float a, float b) => Mathf.Abs(a - b) <= 0.0001f;

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
        string path = Path.Combine(directory, "0922_Phase9_EnemyEncounter.md");

        List<string> lines = new()
        {
            "# 0922 Phase 9 — Enemy / Encounter / Boss",
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
        Debug.Log("[0922 Phase9] Report written: " + path);
    }
}
#endif
