#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0917 정본 대비 구현 현황에서 "확정 규칙인데 실제 누락"으로 판정된 3건만 수정한다.
/// 기획 미정/TEMP 항목은 건드리지 않는다.
///
/// 1) 히후미 C 무모한 베팅: 뼈<70 -> 다음 턴 받는 HP 피해 +3/회
/// 2) 견고/무장해제: 흐트러짐 피해 -N/+N
/// 3) 히후미 공용 도사림: 웅크리기/노려보기 후보 풀 연결
/// </summary>
public static class Canonical0917ConfirmedGapMigration
{
    private const string HifumiMechanicPath =
        "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs";

    private const string StatusEffectPath =
        "Assets/1. Scripts/Runtime/Status/StatusEffect.cs";

    private const string CommonStatusesPath =
        "Assets/1. Scripts/Runtime/Status/Common/CommonCombatStatuses.cs";

    private const string StatusFactoryPath =
        "Assets/1. Scripts/Runtime/Skills/Effects/Common/StatusEffectFactory.cs";

    private const string StaggerGaugePath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/StaggerGaugeMechanic.cs";

    private const string HifumiLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Hifumi/Hifumi_Loadout.asset";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";

    [MenuItem("Game System Verification/0917 Spec Patch/Apply Confirmed Missing Rules")]
    public static void ApplyFromMenu()
    {
        List<string> errors = new();
        List<string> changed = new();

        TryPatchHifumiMechanic(errors, changed);
        TryPatchStatusEffectBase(errors, changed);
        TryPatchCommonStatuses(errors, changed);
        TryPatchStatusFactory(errors, changed);
        TryPatchStaggerGauge(errors, changed);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[0917 Confirmed Missing Rules Patch] FAIL\n- " +
                string.Join("\n- ", errors));
            return;
        }

        TryRepairHifumiCommonPreparationPool(errors, changed);
        if (errors.Count > 0)
        {
            Debug.LogError(
                "[0917 Confirmed Missing Rules Patch] SOURCE PATCHED, DATA FAIL\n- " +
                string.Join("\n- ", errors) +
                "\nSource changes were written successfully. Fix the data error and rerun this menu.");
            AssetDatabase.Refresh();
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[0917 Confirmed Missing Rules Patch] PASS\n" +
            "Patched/verified:\n- " +
            string.Join("\n- ", changed) +
            "\n\nUnity may recompile because runtime source files changed. " +
            "After compilation completes, run: " +
            "Game System Verification > 0917 Spec Patch > Verify Confirmed Missing Rules");
    }

    private static void TryPatchHifumiMechanic(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(HifumiMechanicPath, out string source, errors))
            return;

        string patched = Normalize(source);
        int replacements = 0;

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "    private bool engraveBoneActive;\n" +
            "    private bool nextTurnSpeedPenalty;\n" +
            "    private bool trickActive;",
            "    private bool engraveBoneActive;\n" +
            "    private bool nextTurnSpeedPenalty;\n" +
            "\n" +
            "    // [0917_CONFIRMED_GAP:HIFUMI_C_FIELDS]\n" +
            "    // 무모한 베팅(C): 뼈<70 사용 1회당 다음 턴 받는 HP 피해 +3.\n" +
            "    // 같은 턴 여러 번 발동하면 공통 상태 문법처럼 합산한다.\n" +
            "    private int recklessBetIncomingDamagePenaltyQueued;\n" +
            "    private int recklessBetIncomingDamagePenaltyActive;\n" +
            "\n" +
            "    private bool trickActive;",
            "[0917_CONFIRMED_GAP:HIFUMI_C_FIELDS]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "        nextTurnSpeedPenalty = false;\n" +
            "        trickActive = false;",
            "        nextTurnSpeedPenalty = false;\n" +
            "        recklessBetIncomingDamagePenaltyQueued = 0;\n" +
            "        recklessBetIncomingDamagePenaltyActive = 0;\n" +
            "        trickActive = false; // [0917_CONFIRMED_GAP:HIFUMI_C_RESET]",
            "[0917_CONFIRMED_GAP:HIFUMI_C_RESET]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "        int result = damage;\n" +
            "\n" +
            "        // 0916 Poker Face: incoming HP damage -4 per hit/exchange.\n" +
            "        if (pokerFaceActive && context.Action != null)\n" +
            "            result = Mathf.Max(0, result - 4);\n" +
            "\n" +
            "        return result;",
            "        // [0917_CONFIRMED_GAP:HIFUMI_C_DAMAGE]\n" +
            "        // 두 효과는 같은 flat HP damage 축이므로 먼저 대수합한 뒤 한 번만 clamp한다.\n" +
            "        // 무모한 베팅의 +3은 '교환당' 규칙이므로 Action 기반 피해에만 적용한다.\n" +
            "        int flatModifier = 0;\n" +
            "        if (context.Action != null)\n" +
            "        {\n" +
            "            if (pokerFaceActive)\n" +
            "                flatModifier -= 4;\n" +
            "\n" +
            "            flatModifier +=\n" +
            "                Mathf.Max(0, recklessBetIncomingDamagePenaltyActive);\n" +
            "        }\n" +
            "\n" +
            "        return Mathf.Max(0, damage + flatModifier);",
            "[0917_CONFIRMED_GAP:HIFUMI_C_DAMAGE]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "        catastropheCounterBonusByAction.Clear();\n" +
            "        processedCatastrophes.Clear();\n" +
            "\n" +
            "        ActivateQueuedGovernor();",
            "        catastropheCounterBonusByAction.Clear();\n" +
            "        processedCatastrophes.Clear();\n" +
            "\n" +
            "        // [0917_CONFIRMED_GAP:HIFUMI_C_TURN_START]\n" +
            "        recklessBetIncomingDamagePenaltyActive =\n" +
            "            Mathf.Max(0, recklessBetIncomingDamagePenaltyQueued);\n" +
            "        recklessBetIncomingDamagePenaltyQueued = 0;\n" +
            "\n" +
            "        ActivateQueuedGovernor();",
            "[0917_CONFIRMED_GAP:HIFUMI_C_TURN_START]",
            errors,
            ref replacements,
            occurrenceMustBeUnique: true);

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "        forceTrickFailure = false;\n" +
            "        governorActivePenalty = 0;\n" +
            "        catastropheCounterBonusByAction.Clear();",
            "        forceTrickFailure = false;\n" +
            "        recklessBetIncomingDamagePenaltyActive = 0; // [0917_CONFIRMED_GAP:HIFUMI_C_TURN_END]\n" +
            "        governorActivePenalty = 0;\n" +
            "        catastropheCounterBonusByAction.Clear();",
            "[0917_CONFIRMED_GAP:HIFUMI_C_TURN_END]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            HifumiMechanicPath,
            "            else\n" +
            "            {\n" +
            "                AddBone(100);\n" +
            "                QueueNextTurnRupture();\n" +
            "            }\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    private void OnDamageResolved",
            "            else\n" +
            "            {\n" +
            "                AddBone(100);\n" +
            "\n" +
            "                // [0917_CONFIRMED_GAP:HIFUMI_C_QUEUE]\n" +
            "                // 0917 확정: 다음 턴 받는 피해 +3/교환.\n" +
            "                recklessBetIncomingDamagePenaltyQueued += 3;\n" +
            "            }\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    private void OnDamageResolved",
            "[0917_CONFIRMED_GAP:HIFUMI_C_QUEUE]",
            errors,
            ref replacements);

        WriteIfChanged(HifumiMechanicPath, source, patched, replacements, errors, changed);
    }

    private static void TryPatchStatusEffectBase(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(StatusEffectPath, out string source, errors))
            return;

        string patched = Normalize(source);
        int replacements = 0;

        ReplaceRequired(
            ref patched,
            StatusEffectPath,
            "    public virtual float ModifyDamageTaken(BattleAction action, float damage)\n" +
            "    {\n" +
            "        return damage;\n" +
            "    }\n" +
            "\n" +
            "    public virtual int ModifyHealing(int amount)",
            "    public virtual float ModifyDamageTaken(BattleAction action, float damage)\n" +
            "    {\n" +
            "        return damage;\n" +
            "    }\n" +
            "\n" +
            "    // [0917_CONFIRMED_GAP:STAGGER_STATUS_API]\n" +
            "    // 흐트러짐 내성 적용 뒤 더해지는 flat 보정. 반대 효과는 합산 후 상쇄한다.\n" +
            "    public virtual int GetStaggerDamageTakenFlatModifier(BattleAction action)\n" +
            "    {\n" +
            "        return 0;\n" +
            "    }\n" +
            "\n" +
            "    public virtual int ModifyHealing(int amount)",
            "[0917_CONFIRMED_GAP:STAGGER_STATUS_API]",
            errors,
            ref replacements);

        WriteIfChanged(StatusEffectPath, source, patched, replacements, errors, changed);
    }

    private static void TryPatchCommonStatuses(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(CommonStatusesPath, out string source, errors))
            return;

        string patched = Normalize(source);
        int replacements = 0;

        ReplaceRequired(
            ref patched,
            CommonStatusesPath,
            "// 0915 C-27: 견고/무장해제의 실제 효과는 (미정). 타입은 구 에셋 호환용으로만 남기며\n" +
            "// 신규 Factory authoring에서는 생성하지 않는다.\n" +
            "public sealed class SturdyStatus : OneTurnCommonStatus, ICommonRollShiftStatus\n" +
            "{\n" +
            "    public SturdyStatus(int stack = 1) : base(\"견고\", stack, 3) { }\n" +
            "    public int GetRollShift(BattleAction action) => 0;\n" +
            "}\n" +
            "\n" +
            "public sealed class DisarmStatus : OneTurnCommonStatus, ICommonRollShiftStatus\n" +
            "{\n" +
            "    public DisarmStatus(int stack = 1) : base(\"무장해제\", stack, 4) { }\n" +
            "    public int GetRollShift(BattleAction action) => 0;\n" +
            "}",
            "// [0917_CONFIRMED_GAP:STURDY_DISARM]\n" +
            "// 0917 확정: 견고/무장해제는 굴림 위력이 아니라 '받는 흐트러짐 피해' flat 축이다.\n" +
            "public sealed class SturdyStatus : OneTurnCommonStatus\n" +
            "{\n" +
            "    public SturdyStatus(int stack = 1) : base(\"견고\", stack, 3) { }\n" +
            "\n" +
            "    public override int GetStaggerDamageTakenFlatModifier(BattleAction action) =>\n" +
            "        -Stack;\n" +
            "}\n" +
            "\n" +
            "public sealed class DisarmStatus : OneTurnCommonStatus\n" +
            "{\n" +
            "    public DisarmStatus(int stack = 1) : base(\"무장해제\", stack, 4) { }\n" +
            "\n" +
            "    public override int GetStaggerDamageTakenFlatModifier(BattleAction action) =>\n" +
            "        Stack;\n" +
            "}",
            "[0917_CONFIRMED_GAP:STURDY_DISARM]",
            errors,
            ref replacements);

        WriteIfChanged(CommonStatusesPath, source, patched, replacements, errors, changed);
    }

    private static void TryPatchStatusFactory(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(StatusFactoryPath, out string source, errors))
            return;

        string patched = Normalize(source);
        int replacements = 0;

        ReplaceRequired(
            ref patched,
            StatusFactoryPath,
            "            // 0915 C-27: (미정) 키워드는 신규 Runtime 효과를 만들지 않는다.\n" +
            "            StatusEffectId.Sturdy => null,\n" +
            "            StatusEffectId.Disarm => null,",
            "            // [0917_CONFIRMED_GAP:STURDY_DISARM_FACTORY]\n" +
            "            StatusEffectId.Sturdy =>\n" +
            "                new SturdyStatus(safeStack),\n" +
            "            StatusEffectId.Disarm =>\n" +
            "                new DisarmStatus(safeStack),",
            "[0917_CONFIRMED_GAP:STURDY_DISARM_FACTORY]",
            errors,
            ref replacements);

        WriteIfChanged(StatusFactoryPath, source, patched, replacements, errors, changed);
    }

    private static void TryPatchStaggerGauge(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(StaggerGaugePath, out string source, errors))
            return;

        string patched = Normalize(source);
        int replacements = 0;

        ReplaceRequired(
            ref patched,
            StaggerGaugePath,
            "        float multiplier = owner.Data?.StaggerResistances?.GetMultiplier(physicalType) ?? 1f;\n" +
            "        int staggerDamage = Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));\n" +
            "        if (staggerDamage <= 0)\n" +
            "            return;",
            "        float multiplier = owner.Data?.StaggerResistances?.GetMultiplier(physicalType) ?? 1f;\n" +
            "        int staggerDamage = Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));\n" +
            "\n" +
            "        // [0917_CONFIRMED_GAP:STAGGER_RED_PATH]\n" +
            "        // 내성 적용 후 견고/무장해제 flat 보정을 적용한다.\n" +
            "        staggerDamage = ApplyTargetStaggerDamageFlatModifiers(\n" +
            "            action,\n" +
            "            owner,\n" +
            "            action.TargetPart,\n" +
            "            staggerDamage);\n" +
            "\n" +
            "        if (staggerDamage <= 0)\n" +
            "            return;",
            "[0917_CONFIRMED_GAP:STAGGER_RED_PATH]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            StaggerGaugePath,
            "        PhysicalDamageType type = PhysicalDamageResolver.Resolve(action);\n" +
            "        float multiplier = target.Data?.StaggerResistances?.GetMultiplier(type) ?? 1f;\n" +
            "        return Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));",
            "        PhysicalDamageType type = PhysicalDamageResolver.Resolve(action);\n" +
            "        float multiplier = target.Data?.StaggerResistances?.GetMultiplier(type) ?? 1f;\n" +
            "        int staggerDamage = Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));\n" +
            "\n" +
            "        // [0917_CONFIRMED_GAP:STAGGER_BLUE_PATH]\n" +
            "        return ApplyTargetStaggerDamageFlatModifiers(\n" +
            "            action,\n" +
            "            target,\n" +
            "            action.TargetPart,\n" +
            "            staggerDamage);",
            "[0917_CONFIRMED_GAP:STAGGER_BLUE_PATH]",
            errors,
            ref replacements);

        ReplaceRequired(
            ref patched,
            StaggerGaugePath,
            "    private static int ResolveAdditionalStaggerDamage(\n" +
            "        BattleAction action,\n" +
            "        Character target)",
            "    // [0917_CONFIRMED_GAP:STAGGER_FLAT_HELPER]\n" +
            "    private static int ApplyTargetStaggerDamageFlatModifiers(\n" +
            "        BattleAction action,\n" +
            "        Character target,\n" +
            "        BodyPart targetPart,\n" +
            "        int damage)\n" +
            "    {\n" +
            "        if (target == null || damage <= 0)\n" +
            "            return Mathf.Max(0, damage);\n" +
            "\n" +
            "        int flatModifier = 0;\n" +
            "\n" +
            "        if (target.StatusEffects != null)\n" +
            "        {\n" +
            "            foreach (StatusEffect effect in target.StatusEffects)\n" +
            "            {\n" +
            "                if (effect != null)\n" +
            "                {\n" +
            "                    flatModifier +=\n" +
            "                        effect.GetStaggerDamageTakenFlatModifier(action);\n" +
            "                }\n" +
            "            }\n" +
            "        }\n" +
            "\n" +
            "        if (targetPart != null &&\n" +
            "            targetPart.Owner == target &&\n" +
            "            targetPart.StatusEffects != null)\n" +
            "        {\n" +
            "            foreach (StatusEffect effect in targetPart.StatusEffects)\n" +
            "            {\n" +
            "                if (effect != null)\n" +
            "                {\n" +
            "                    flatModifier +=\n" +
            "                        effect.GetStaggerDamageTakenFlatModifier(action);\n" +
            "                }\n" +
            "            }\n" +
            "        }\n" +
            "\n" +
            "        return Mathf.Max(0, damage + flatModifier);\n" +
            "    }\n" +
            "\n" +
            "    private static int ResolveAdditionalStaggerDamage(\n" +
            "        BattleAction action,\n" +
            "        Character target)",
            "[0917_CONFIRMED_GAP:STAGGER_FLAT_HELPER]",
            errors,
            ref replacements);

        WriteIfChanged(StaggerGaugePath, source, patched, replacements, errors, changed);
    }

    private static void TryRepairHifumiCommonPreparationPool(
        List<string> errors,
        List<string> changed)
    {
        CharacterCombatLoadout hifumi =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(HifumiLoadoutPath);

        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);

        if (hifumi == null)
        {
            errors.Add("Hifumi loadout missing: " + HifumiLoadoutPath);
            return;
        }

        if (olaf == null ||
            olaf.CommonPreparationPool == null ||
            olaf.CommonPreparationPool.Count != 2 ||
            olaf.CommonPreparationPool[0] == null ||
            olaf.CommonPreparationPool[1] == null)
        {
            errors.Add("Canonical common preparation source is invalid in Olaf loadout (expected exactly 2).");
            return;
        }

        hifumi.CommonPreparationPool ??= new List<SkillDefinition>();

        bool alreadyCorrect =
            hifumi.CommonPreparationPool.Count == 2 &&
            hifumi.CommonPreparationPool[0] == olaf.CommonPreparationPool[0] &&
            hifumi.CommonPreparationPool[1] == olaf.CommonPreparationPool[1];

        if (!alreadyCorrect)
        {
            hifumi.CommonPreparationPool.Clear();
            hifumi.CommonPreparationPool.Add(olaf.CommonPreparationPool[0]);
            hifumi.CommonPreparationPool.Add(olaf.CommonPreparationPool[1]);
            EditorUtility.SetDirty(hifumi);
            changed.Add("Hifumi CommonPreparationPool <- common crouch/glare (2)");
        }
        else
        {
            changed.Add("Hifumi CommonPreparationPool already correct (2)");
        }
    }

    private static bool TryRead(
        string path,
        out string source,
        List<string> errors)
    {
        source = string.Empty;
        if (!File.Exists(path))
        {
            errors.Add("Source file missing: " + path);
            return false;
        }

        source = File.ReadAllText(path);
        return true;
    }

    private static string Normalize(string source) =>
        (source ?? string.Empty).Replace("\r\n", "\n");

    private static void ReplaceRequired(
        ref string source,
        string path,
        string oldText,
        string newText,
        string marker,
        List<string> errors,
        ref int replacements,
        bool occurrenceMustBeUnique = false)
    {
        if (source.Contains(marker, StringComparison.Ordinal))
            return;

        int first = source.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0)
        {
            errors.Add($"Patch anchor not found: {path} / {marker}");
            return;
        }

        if (occurrenceMustBeUnique &&
            source.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
        {
            errors.Add($"Patch anchor is ambiguous: {path} / {marker}");
            return;
        }

        source =
            source.Substring(0, first) +
            newText +
            source.Substring(first + oldText.Length);
        replacements++;
    }

    private static void WriteIfChanged(
        string path,
        string original,
        string patched,
        int replacements,
        List<string> errors,
        List<string> changed)
    {
        if (errors.Count > 0)
            return;

        string normalizedOriginal = Normalize(original);
        if (string.Equals(normalizedOriginal, patched, StringComparison.Ordinal))
        {
            changed.Add(Path.GetFileName(path) + " already patched");
            return;
        }

        if (replacements <= 0)
        {
            errors.Add("No replacement was made for: " + path);
            return;
        }

        File.WriteAllText(path, patched);
        changed.Add(Path.GetFileName(path) + $" source patched ({replacements})");
    }
}
#endif
