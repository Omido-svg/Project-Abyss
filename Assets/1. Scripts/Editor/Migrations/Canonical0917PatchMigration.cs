#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Abyss 0917 overlay patch.
/// Base target: Default-Battle-Test (0917 overlay; emotion hotfix rechecked on c8d3a02a).
/// - Yujin O/P assets + loadout
/// - Olaf 0917 card textures as data, repay-all B<=-30 +4
/// - Hifumi Goldan Bloom counter x2 source patch
/// - legacy 0916 C44 hardcode re-application disabled
/// - Phase E Yujin count contract 14->16
/// - RunFlow registry EmotionAugmentCatalog repair
/// - canonical 7-emotion × 3-tier × 3-card roster normalization
///
/// 미정값은 플레이 가능한 SkillDefinition으로 발명하지 않는다.
/// </summary>
public static class Canonical0917PatchMigration
{
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";

    private const string CanonicalRoot =
        "Assets/2. Data/Progression/Canonical0917";

    private const string YujinRoot =
        CanonicalRoot + "/Yujin";

    private const string OlafTextureJsonPath =
        CanonicalRoot + "/OlafTexturePatch.json";

    private const string RegistryPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";

    private const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    [Serializable]
    private sealed class OlafTexturePatchFile
    {
        public string revision;
        public string authorityPolicy;
        public OlafTexturePatchEntry[] skills;
    }

    [Serializable]
    private sealed class OlafTexturePatchEntry
    {
        public string skillId;
        public string name;
        public string state;
        public int[] min;
        public int[] max;
    }

    [MenuItem("Game System Verification/0917 Spec Patch/Apply 0917 Patch")]
    public static void ApplyFromMenu()
    {
        int sourcePatches = ApplySourcePatches();
        int olafPatched = ApplyOlafData();
        int yujinPatched = ApplyYujinOP();
        bool emotionRosterOk =
            Canonical0917EmotionAugmentMigration.ApplyCanonicalRoster(
                out string emotionRosterReport);
        bool registryFixed = RepairRunFlowRegistry();

        UpdateManifest();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[0917 Spec Patch] APPLY complete. " +
            $"SourcePatches={sourcePatches}, OlafData={olafPatched}, " +
            $"YujinAssets={yujinPatched}, EmotionRosterOk={emotionRosterOk}, " +
            $"RegistryFixed={registryFixed}. " +
            (emotionRosterOk ? string.Empty : "EmotionRoster: " + emotionRosterReport + ". ") +
            "Source patch가 있었다면 Unity 재컴파일 완료 후 " +
            "'Game System Verification > 0917 Spec Patch > Verify 0917 Patch'를 실행하세요.");
    }

    private static int ApplySourcePatches()
    {
        int changed = 0;

        changed += PatchHifumiGoldanBloomCounter();
        changed += DisableLegacy0916OlafC44Hardcode();
        changed += PatchPhaseEYujin0917Counts();
        changed += PatchPhaseETempBalanceYujinPersistence();

        return changed;
    }

    private static int PatchHifumiGoldanBloomCounter()
    {
        const string path =
            "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs";

        if (!File.Exists(path))
        {
            Debug.LogError("[0917 Patch] HifumiMechanic.cs missing.");
            return 0;
        }

        string text = File.ReadAllText(path);
        string before = text;

        const string previewOld =
@"        return new HifumiCounterPreview(
            true,
            lostExchanges,
            baseCounterPower,";

        const string previewNew =
@"        int previewCounterCount =
            goldan && bloom
                ? lostExchanges * 2
                : lostExchanges;

        return new HifumiCounterPreview(
            true,
            previewCounterCount,
            baseCounterPower,";

        if (text.Contains(previewOld))
            text = text.Replace(previewOld, previewNew);

        const string liveOld =
@"        int counterCount =
            Mathf.Max(0, taggedLosses) +
            Mathf.Max(0, catastropheBonus);

        if (counterCount <= 0)
            return;";

        const string liveNew =
@"        int counterCount =
            Mathf.Max(0, taggedLosses) +
            Mathf.Max(0, catastropheBonus);

        // 0917 §19 Goldan: Bloom(뼈 500) 상태의 골단 반격 풀은 x2.
        // tagged loss + catastrophe bonus로 완성된 '이번 합의 반격 풀'을 두 배로 만든다.
        if (myAction.Skill?.Definition?.SkillId == HifumiSkillIds.Goldan &&
            IsBloom)
        {
            counterCount *= 2;
        }

        if (counterCount <= 0)
            return;";

        if (text.Contains(liveOld))
            text = text.Replace(liveOld, liveNew);

        if (text == before)
        {
            bool already =
                text.Contains("previewCounterCount") &&
                text.Contains("counterCount *= 2");

            if (!already)
                Debug.LogWarning("[0917 Patch] Hifumi Goldan source token not found; manual review required.");
            return 0;
        }

        File.WriteAllText(path, text);
        Debug.Log("[0917 Patch] Hifumi Goldan Bloom counter x2 source patched.");
        return 1;
    }

    private static int DisableLegacy0916OlafC44Hardcode()
    {
        const string path =
            "Assets/1. Scripts/Editor/Migrations/Canonical0916ClosureMigration.cs";

        if (!File.Exists(path))
            return 0;

        string text = File.ReadAllText(path);
        string before = text;

        const string callOld =
            "int olafC44TexturesNormalized = ApplyOlafC44CanonicalTextures(olaf);";

        const string callNew =
            "int olafC44TexturesNormalized = 0; // 0917: legacy 15-card C44 hardcode disabled; card data owns provisional/confirmed values.";

        if (text.Contains(callOld))
            text = text.Replace(callOld, callNew);

        const string checkOld =
@"        if (HasOlafC44Mismatch(olaf))
            return true;";

        const string checkNew =
@"        // 0917: legacy C44 exact-array check is intentionally disabled.
        // Specific card data and the 0917 verifier own the known C44-vs-card authority conflict.";

        if (text.Contains(checkOld))
            text = text.Replace(checkOld, checkNew);

        if (text == before)
            return 0;

        File.WriteAllText(path, text);
        Debug.Log("[0917 Patch] Canonical0916Closure legacy Olaf C44 re-apply disabled.");
        return 1;
    }

    private static int PatchPhaseEYujin0917Counts()
    {
        const string path =
            "Assets/1. Scripts/Editor/Verification/PhaseEContentVerificationModule.cs";

        if (!File.Exists(path))
            return 0;

        string text = File.ReadAllText(path);
        string before = text;

        text = text.Replace(
            "최신 0916 평타9/결투14/도사림/위세3 + Runtime 연결",
            "최신 0917 평타9/결투16/도사림/위세3 + Runtime 연결");

        text = text.Replace(
            "normal == 9 && duel == 14 && preparation == 9 && prestige == 3",
            "normal == 9 && duel == 16 && preparation == 9 && prestige == 3");

        text = text.Replace(
            "D={duel}/14",
            "D={duel}/16");

        text = text.Replace(
            "Yujin 0916 canonical closure PASS",
            "Yujin 0917 canonical closure PASS");

        text = text.Replace(
            "Yujin 0916 canonical closure mismatch",
            "Yujin 0917 canonical closure mismatch");

        // 0917 card-vs-C44 conflict is an explicit design PENDING, not a runtime regression.
        const string c44FailOld =
@"        if(m.RollTextureViolationCount>0)
            return GameSystemVerificationProbeResult.Fail(
                $""Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations={m.RollTextureViolationCount}"", details);";

        const string c44PendingNew =
@"        bool known0917AuthorityPending =
            m.TempBalanceNotes != null &&
            m.TempBalanceNotes.Any(note =>
                !string.IsNullOrWhiteSpace(note) &&
                note.Contains(""0917 C44 authority pending""));

        if(m.RollTextureViolationCount>0 && known0917AuthorityPending)
            return GameSystemVerificationProbeResult.Pending(
                $""0917 authority pending / Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations={m.RollTextureViolationCount}"", details);

        if(m.RollTextureViolationCount>0)
            return GameSystemVerificationProbeResult.Fail(
                $""Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations={m.RollTextureViolationCount}"", details);";

        if (text.Contains(c44FailOld))
            text = text.Replace(c44FailOld, c44PendingNew);

        if (text == before)
            return 0;

        File.WriteAllText(path, text);
        Debug.Log("[0917 Patch] Phase E Yujin count contract updated to Duel16/Total37.");
        return 1;
    }


    private static int PatchPhaseETempBalanceYujinPersistence()
    {
        const string path =
            "Assets/1. Scripts/Editor/Migrations/PhaseETempBalanceMigration.cs";

        if (!File.Exists(path))
            return 0;

        string text = File.ReadAllText(path);
        string before = text;

        text = text.Replace(
            "// Y-06 Yujin 9 / 14 / 9 / 3",
            "// Y-06 Yujin 9 / 16 / 9 / 3 (0917 O/P appended from Canonical0917 assets)");

        const string applyOld =
@"        ApplyLoadout(loadout, normals, duels, commonPreparations, preps.Skip(2).ToList(), prestiges);
        ApplyYujinStarterPreparationLoadout(loadout, preps);
        manifest.YujinPoolCount = normals.Count + duels.Count + preps.Count + prestiges.Count;
        return normals.Count == 9 && duels.Count == 14 && preps.Count == 9 && prestiges.Count == 3 &&
               ValidateTempUpgradeProfiles(normals.Concat(duels).Concat(preps).Concat(prestiges));";

        const string applyNew =
@"        // 0917 O/P are canonical assets owned by Canonical0917PatchMigration.
        // Re-running Phase E must not shrink the Yujin duel pool back to 14.
        SkillDefinition yujinO0917 =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                ""Assets/2. Data/Progression/Canonical0917/Yujin/Skills/Yujin_D_15.asset"");
        SkillDefinition yujinP0917 =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                ""Assets/2. Data/Progression/Canonical0917/Yujin/Skills/Yujin_D_16.asset"");

        if (yujinO0917 != null)
            duels.Add(yujinO0917);
        if (yujinP0917 != null)
            duels.Add(yujinP0917);

        ApplyLoadout(loadout, normals, duels, commonPreparations, preps.Skip(2).ToList(), prestiges);
        ApplyYujinStarterPreparationLoadout(loadout, preps);
        manifest.YujinPoolCount = normals.Count + duels.Count + preps.Count + prestiges.Count;
        return normals.Count == 9 && duels.Count == 16 && preps.Count == 9 && prestiges.Count == 3 &&
               ValidateTempUpgradeProfiles(normals.Concat(duels).Concat(preps).Concat(prestiges));";

        if (text.Contains(applyOld))
            text = text.Replace(applyOld, applyNew);

        if (text == before)
            return 0;

        File.WriteAllText(path, text);
        Debug.Log("[0917 Patch] PhaseETempBalanceMigration preserves Yujin O/P on re-run.");
        return 1;
    }

    private static int ApplyOlafData()
    {
        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                OlafLoadoutPath);

        if (loadout == null)
        {
            Debug.LogError("[0917 Patch] Olaf loadout missing.");
            return 0;
        }

        TextAsset jsonAsset =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                OlafTextureJsonPath);

        if (jsonAsset == null)
        {
            Debug.LogError("[0917 Patch] OlafTexturePatch.json missing.");
            return 0;
        }

        OlafTexturePatchFile patch =
            JsonUtility.FromJson<OlafTexturePatchFile>(
                jsonAsset.text);

        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillId))
                .GroupBy(x => x.SkillId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int changed = 0;

        foreach (OlafTexturePatchEntry entry in patch?.skills ??
                 Array.Empty<OlafTexturePatchEntry>())
        {
            if (!byId.TryGetValue(entry.skillId, out SkillDefinition skill) ||
                skill?.Rolls == null ||
                entry.min == null ||
                entry.max == null ||
                entry.min.Length != entry.max.Length ||
                skill.Rolls.Count != entry.min.Length)
            {
                Debug.LogWarning(
                    $"[0917 Patch] Olaf texture skipped: {entry?.name} ({entry?.skillId})");
                continue;
            }

            for (int i = 0; i < entry.min.Length; i++)
            {
                SkillRollData roll = skill.Rolls[i];
                if (roll == null)
                    continue;

                int minDelta =
                    UpgradeRollDelta(
                        skill,
                        i,
                        min: true);

                int maxDelta =
                    UpgradeRollDelta(
                        skill,
                        i,
                        min: false);

                roll.MinPower =
                    Mathf.Max(
                        1,
                        entry.min[i] - minDelta);

                roll.MaxPower =
                    Mathf.Max(
                        roll.MinPower,
                        entry.max[i] - maxDelta);
            }

            string marker =
                entry.state != null &&
                entry.state.StartsWith("confirmed", StringComparison.OrdinalIgnoreCase)
                    ? "[0917_CONFIRMED_CARD]"
                    : entry.state != null &&
                      entry.state.Contains("intentional-formula-deviation")
                        ? "[0917_INTENTIONAL_FORMULA_DEVIATION]"
                        : "[0917_TEMP_DATA]";

            string description =
                skill.Description ?? string.Empty;

            description = description
                .Replace("[CANONICAL_0916_C44]", string.Empty)
                .Replace("[0917_CONFIRMED_CARD]", string.Empty)
                .Replace("[0917_INTENTIONAL_FORMULA_DEVIATION]", string.Empty)
                .Replace("[0917_TEMP_DATA]", string.Empty)
                .Trim();

            skill.Description =
                $"{marker} 0917 source={entry.state}. " +
                "미정/잠정 범위는 Runtime 상수가 아니라 SkillDefinition 데이터가 소유한다.\n" +
                description;

            EditorUtility.SetDirty(skill);
            changed++;
        }

        SkillDefinition repay =
            byId.TryGetValue(
                OlafSkillIds.PaybackAll,
                out SkillDefinition repaySkill)
                ? repaySkill
                : null;

        if (repay != null)
        {
            repay.Rulebreaker ??=
                new SkillRulebreakerSettings();

            repay.Rulebreaker.Enabled = true;
            repay.Rulebreaker.ApplyCurrentDisadvantageOrWorsePowerBonus = true;
            repay.Rulebreaker.CurrentDisadvantageOrWorsePowerBonus = 4;

            // 구 0916 LastStand +6 설정이 남아 있으면 중복되지 않게 끈다.
            repay.Rulebreaker.ApplyCurrentLastStandPowerBonus = false;
            repay.Rulebreaker.CurrentLastStandPowerBonus = 0;

            EditorUtility.SetDirty(repay);
        }

        return changed;
    }

    private static int UpgradeRollDelta(
        SkillDefinition skill,
        int rollIndex,
        bool min)
    {
        if (skill?.UpgradeProfile == null)
            return 0;

        int total = 0;
        SkillUpgradeStep[] steps =
        {
            skill.UpgradeProfile.Upgrade1,
            skill.UpgradeProfile.Upgrade2
        };

        foreach (SkillUpgradeStep step in steps)
        {
            if (step == null)
                continue;

            total +=
                min
                    ? step.AllRollMinPowerDelta
                    : step.AllRollMaxPowerDelta;

            if (step.PerRollPowerDelta != null &&
                rollIndex >= 0 &&
                rollIndex < step.PerRollPowerDelta.Count)
            {
                total += step.PerRollPowerDelta[rollIndex];
            }
        }

        return total;
    }

    private static int ApplyYujinOP()
    {
        EnsureFolder(YujinRoot + "/Skills");
        EnsureFolder(YujinRoot + "/Effects");
        EnsureFolder(YujinRoot + "/Upgrades");

        Yujin0917SkillEffectDefinition oEffect =
            EnsureAsset<Yujin0917SkillEffectDefinition>(
                YujinRoot + "/Effects/Yujin_O_ForceIgnition.asset");

        oEffect.Operation =
            Yujin0917EffectOperation.ForceTargetMarkIgnition;
        EditorUtility.SetDirty(oEffect);

        Yujin0917SkillEffectDefinition pEffect =
            EnsureAsset<Yujin0917SkillEffectDefinition>(
                YujinRoot + "/Effects/Yujin_P_ConsumeSensePower.asset");

        pEffect.Operation =
            Yujin0917EffectOperation.ConsumeAllSenseForPower;
        EditorUtility.SetDirty(pEffect);

        SkillDefinition o =
            EnsureAsset<SkillDefinition>(
                YujinRoot + "/Skills/Yujin_D_15.asset");

        ConfigureYujinDuel(
            o,
            YujinSkillIds.AdvanceTiming,
            "때를 앞당기다",
            2,
            oEffect,
            "0917 확정: RED/1굴림/빛2. 계획 commit에서 살수의 감4 소비. 사용시 조준 부위 표식을 44 임계까지 밀어 즉시 발화; K/L rider 포함.");

        SkillDefinition p =
            EnsureAsset<SkillDefinition>(
                YujinRoot + "/Skills/Yujin_D_16.asset");

        ConfigureYujinDuel(
            p,
            YujinSkillIds.FinishIt,
            "끝장을 보다",
            4,
            pEffect,
            "0917 확정: RED/1굴림. 실제 비용 4→3→2→1(전투 누적), 사용시 감 전량 소비→감1당 위력+2. 코인 위력의 빛 계수는 실제 비용과 무관하게 빛2 고정.");

        p.Rulebreaker ??=
            new SkillRulebreakerSettings();

        p.Rulebreaker.Enabled = true;
        p.Rulebreaker.EnergyCostReductionPerCommittedUse = 1;
        p.Rulebreaker.MinimumEnergyCost = 1;
        EditorUtility.SetDirty(p);

        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                YujinLoadoutPath);

        if (loadout == null)
        {
            Debug.LogError("[0917 Patch] Yujin loadout missing.");
            return 0;
        }

        loadout.DuelSkillPool ??=
            new List<SkillDefinition>();

        UpsertBySkillId(
            loadout.DuelSkillPool,
            o);

        UpsertBySkillId(
            loadout.DuelSkillPool,
            p);

        EditorUtility.SetDirty(loadout);

        return 2;
    }

    private static void ConfigureYujinDuel(
        SkillDefinition skill,
        string id,
        string name,
        int energy,
        Yujin0917SkillEffectDefinition effect,
        string description)
    {
        SetSkillId(skill, id);
        skill.SkillName = name;
        skill.ActionType = ActionType.Duel;
        skill.Color = SkillColor.Red;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, energy);
        skill.BasePowerRule = SkillBasePowerRule.FixedBase;
        skill.BasePower = 11;
        skill.ResolverType = SkillResolverType.Coin;
        skill.ExchangeRollCount = 1;
        skill.Rolls ??= new List<SkillRollData>();
        skill.Rolls.Clear();
        skill.CanBreakPart = false;
        skill.BreakMode = PartBreakMode.None;
        skill.Description = description;

        // 새 전용 Timeline authoring 전까지 기존 Yujin Duel의 검증된 presentation을 fallback으로 사용.
        UnityEngine.Object fallbackVisual =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/2. Data/Characters/Design2026/Yujin/Skills/Cutscenes/Yujin_Inscription/Yujin_Inscription_Visual.asset");
        if (skill.PresentationAsset == null && fallbackVisual != null)
            skill.SetPresentationAsset(fallbackVisual);

        skill.EffectEntries ??=
            new List<SkillEffectEntry>();

        skill.EffectEntries.Clear();

        skill.EffectEntries.Add(
            new SkillEffectEntry
            {
                Definition = effect,
                UpgradeKey = "0917_core",
                OverrideTiming = true,
                Timing = SkillEffectTiming.OnExecute,
                Conditions =
                    new List<SkillEffectCondition>()
            });

        skill.Effects ??=
            new List<SkillEffectDefinition>();
        skill.Effects.Clear();

        SkillUpgradeProfile profile =
            EnsureAsset<SkillUpgradeProfile>(
                YujinRoot +
                "/Upgrades/" +
                Path.GetFileNameWithoutExtension(
                    AssetDatabase.GetAssetPath(skill)) +
                "_Upgrade.asset");

        profile.BaseCostOverride = 0;
        profile.Upgrade1 ??= new SkillUpgradeStep();
        profile.Upgrade2 ??= new SkillUpgradeStep();

        ResetUpgradeStep(profile.Upgrade1);
        ResetUpgradeStep(profile.Upgrade2);

        profile.Upgrade1.DesignNote =
            "[0917] 신규 카드 강화 수치가 별도 확정되지 않아 delta=0.";
        profile.Upgrade2.DesignNote =
            "[0917] 2회차 강화 가격/상세 delta는 Unset.";

        // 2회차 비용 0 = Unset.
        profile.Upgrade2.CostOverride = 0;
        skill.UpgradeProfile = profile;

        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(skill);
    }

    private static void ResetUpgradeStep(
        SkillUpgradeStep step)
    {
        if (step == null)
            return;

        step.CostOverride = 0;
        step.BasePowerDelta = 0;
        step.AllRollMinPowerDelta = 0;
        step.AllRollMaxPowerDelta = 0;
        step.EnergyCostDelta = 0;
        step.PrestigeCostDelta = 0;
        step.CustomResourceCostDelta = 0;

        step.PerRollPowerDelta ??=
            new List<int>();
        step.PerRollPowerDelta.Clear();

        step.EffectPayloads ??=
            new List<SkillUpgradeEffectPayload>();
        step.EffectPayloads.Clear();
    }

    private static void UpsertBySkillId(
        List<SkillDefinition> list,
        SkillDefinition skill)
    {
        if (list == null || skill == null)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            SkillDefinition current = list[i];

            if (current == null)
                continue;

            if (string.Equals(
                    current.SkillId,
                    skill.SkillId,
                    StringComparison.Ordinal))
            {
                list[i] = skill;
                return;
            }
        }

        list.Add(skill);
    }

    private static bool RepairRunFlowRegistry()
    {
        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryPath);

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                EmotionCatalogPath);

        if (registry == null || catalog == null)
            return false;

        if (registry.EmotionAugmentCatalog == catalog)
            return false;

        registry.EmotionAugmentCatalog = catalog;
        EditorUtility.SetDirty(registry);
        return true;
    }

    private static void UpdateManifest()
    {
        PhaseEContentManifest manifest =
            AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(
                PhaseEContentMigration.ManifestPath);

        if (manifest == null)
            return;

        manifest.YujinPoolCount = 37;
        manifest.YujinSkillPoolComplete = true;

        manifest.TempBalanceNotes ??=
            new List<string>();

        manifest.TempBalanceNotes.RemoveAll(note =>
            !string.IsNullOrWhiteSpace(note) &&
            (note.StartsWith("0916 Closure C44:", StringComparison.Ordinal) ||
             note.StartsWith("0917 Patch:", StringComparison.Ordinal)));

        manifest.TempBalanceNotes.Add(
            "0917 Patch: Yujin Duel O/P 추가 → Duel16 / canonical total37. " +
            "P cost 4→1 누적, power-energy=2 고정.");

        manifest.TempBalanceNotes.Add(
            "0917 Patch: Hifumi Goldan Bloom counter pool x2 runtime source patch.");

        manifest.TempBalanceNotes.Add(
            "0917 C44 authority pending: Bite 확정 카드 범위와 §4.3 공식이 내부 충돌. " +
            "15-card legacy C# hardcode는 비활성화하고 0917 카드 표 값을 SkillDefinition data로 유지.");

        EditorUtility.SetDirty(manifest);
    }

    private static void SetSkillId(
        SkillDefinition definition,
        string id)
    {
        SerializedObject serialized =
            new SerializedObject(
                definition);

        SerializedProperty property =
            serialized.FindProperty(
                "skillId");

        if (property != null)
            property.stringValue = id;

        SerializedProperty schema =
            serialized.FindProperty(
                "phaseASchemaVersion");

        if (schema != null)
        {
            schema.intValue =
                SkillDefinition
                    .CurrentPhaseASchemaVersion;
        }

        serialized
            .ApplyModifiedPropertiesWithoutUndo();
    }

    private static T EnsureAsset<T>(
        string path)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase
                .LoadAssetAtPath<T>(
                    path);

        if (asset != null)
            return asset;

        UnityEngine.Object existing =
            AssetDatabase
                .LoadMainAssetAtPath(
                    path);

        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        string directory =
            path.Substring(
                0,
                path.LastIndexOf('/'));

        EnsureFolder(directory);

        asset =
            ScriptableObject
                .CreateInstance<T>();

        asset.name =
            Path.GetFileNameWithoutExtension(
                path);

        AssetDatabase.CreateAsset(
            asset,
            path);

        return asset;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts =
            path.Split('/');

        string current =
            parts[0];

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current = next;
        }
    }
}
#endif
