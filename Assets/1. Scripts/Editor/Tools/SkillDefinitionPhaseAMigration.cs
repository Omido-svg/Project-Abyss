using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SkillDefinitionPhaseAMigration
{
    private const string MenuRoot =
        "Tools/Project Abyss/0915 Skill Definition Migration/";

    private sealed class Audit
    {
        public int Total;
        public int SchemaCurrent;
        public int SchemaPending;
        public int Migrated;

        public int ColorExplicit;
        public int ColorLegacyDerived;
        public int ColorDeferredTbd;
        public int ColorDeferredContent;
        public int ColorNotApplicable;

        public int PowerCanonical;
        public int PowerYujin;
        public int PowerHifumi;
        public int PowerExplicitException;
        public int PowerDeferredLegacy;
        public int PowerNotApplicable;

        public int ManualReview;
        public readonly List<string> Details = new();
    }

    [MenuItem(MenuRoot + "Audit Phase A Skill Definitions")]
    public static void AuditPhaseASkillDefinitions()
    {
        Audit audit = Run(migrate: false);
        Finish(audit, "Audit only");
    }

    [MenuItem(MenuRoot + "Migrate Safe Phase A Values")]
    public static void MigrateSafePhaseAValues()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "0915 Phase A SkillDefinition Migration",
            "Rules 2026-09에서 확정됐거나 기계적으로 안전한 값만 저장합니다.\n\n" +
            "• 유진 공격 스킬: 기본위력 11 고정 + 빛×2 계약\n" +
            "• 히후미: 현재 BasePower가 곡선+빛×2+1과 정확히 맞는 카드만 +1 규칙으로 이관\n" +
            "• 그 외: 기존 BasePower가 0915 공통식과 정확히 맞을 때만 CanonicalCurve\n" +
            "• 공식과 다른 기존 값은 LegacyExplicit로 보존 — Phase A가 임의 변경하지 않음\n" +
            "• 색: 명시 RED/BLUE 보존. 히후미는 정본 미정이라 Unset 유지. " +
            "그 외는 구 Roll이 전부 같은 종류일 때만 RED/BLUE로 이관\n\n" +
            "미정/근사/캐릭터 전체 Core Data 이관은 후속 Phase 대상입니다.\n" +
            "Git working tree가 깨끗한 상태에서 실행하세요.",
            "Migrate",
            "Cancel");

        if (!confirmed)
            return;

        Audit audit = Run(migrate: true);
        AssetDatabase.SaveAssets();
        Finish(audit, "Safe migration");
    }

    private static Audit Run(bool migrate)
    {
        Audit audit = new Audit();
        string[] guids = AssetDatabase.FindAssets("t:SkillDefinition");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null)
            {
                audit.ManualReview++;
                audit.Details.Add($"MANUAL | load failed | {path}");
                continue;
            }

            audit.Total++;

            if (definition.IsPhaseASchemaMigrated)
            {
                audit.SchemaCurrent++;
                ClassifyColor(definition, path, audit);
                ClassifyPower(definition, path, audit);
                continue;
            }

            audit.SchemaPending++;

            if (!migrate)
            {
                ClassifyPendingColor(definition, path, audit);
                ClassifyPendingPower(definition, path, audit);
                continue;
            }

            Undo.RecordObject(
                definition,
                "Migrate 0915 Phase A SkillDefinition");

            MigrateColor(definition, path, audit);
            MigratePower(definition, path, audit);
            definition.MarkPhaseASchemaMigrated();
            EditorUtility.SetDirty(definition);
            audit.Migrated++;
        }

        return audit;
    }

    private static void MigrateColor(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            audit.ColorNotApplicable++;
            return;
        }

        if (definition.Color != SkillColor.Unset)
        {
            audit.ColorExplicit++;
            return;
        }

        if (IsHifumi(path))
        {
            // 0915 정본에서 히후미 RED/BLUE는 아직 미정이다.
            audit.ColorDeferredTbd++;
            audit.Details.Add(
                $"DEFERRED(TBD COLOR) | {path} | {definition.SkillName}");
            return;
        }

        if (TryInferUniformLegacyColor(
                definition,
                out SkillColor inferred))
        {
            definition.Color = inferred;
            audit.ColorLegacyDerived++;
            audit.Details.Add(
                $"MIGRATE COLOR | {inferred} | {path} | {definition.SkillName}");
            return;
        }

        // 혼합/빈 Roll은 카드 스펙 없이 색을 추측하지 않는다.
        audit.ColorDeferredContent++;
        audit.Details.Add(
            $"DEFERRED(CONTENT COLOR) | {path} | {definition.SkillName}");
    }

    private static void MigratePower(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            // 비전투 도사림/위세에서 새 enum의 기본값 0(CanonicalCurve)이
            // 의도된 authoring으로 오해되지 않게 명시적 Legacy로 잠근다.
            if (definition.BasePowerRule == SkillBasePowerRule.CanonicalCurve &&
                definition.BasePowerFlatAdjustment == 0)
            {
                definition.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
            }

            audit.PowerNotApplicable++;
            return;
        }

        // schema v1 이전에 non-default rule이 이미 들어 있다면 사용자의 명시 authoring으로 본다.
        if (definition.BasePowerRule != SkillBasePowerRule.CanonicalCurve ||
            definition.BasePowerFlatAdjustment != 0)
        {
            ClassifyPower(definition, path, audit);
            return;
        }

        if (IsYujin(path))
        {
            // 정본 §4.2/§4.4: 유진 기본위력은 11 고정, 빛 보너스는 별도 +2/빛.
            definition.BasePowerRule = SkillBasePowerRule.FixedBase;
            definition.BasePower = 11;
            definition.BasePowerFlatAdjustment = 0;
            audit.PowerYujin++;
            audit.Details.Add(
                $"MIGRATE POWER | Yujin FixedBase=11 | {path} | {definition.SkillName}");
            return;
        }

        if (!PowerFormulaService.TryGetCurveValue(
                definition.EffectiveRollCount,
                out int curve))
        {
            definition.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
            definition.BasePowerFlatAdjustment = 0;
            audit.PowerDeferredLegacy++;
            audit.Details.Add(
                $"DEFERRED(POWER 6-8 ROLLS) | Base={definition.BasePower} | {path} | {definition.SkillName}");
            return;
        }

        int energyBonus =
            PowerFormulaService.GetEffectiveEnergyCost(definition) *
            PowerFormulaService.EnergyPowerPerPoint;

        if (IsHifumi(path))
        {
            int expected = curve + energyBonus + 1;

            if (definition.BasePower == expected)
            {
                definition.BasePowerRule =
                    SkillBasePowerRule.CanonicalPlusFlat;
                definition.BasePowerFlatAdjustment = 1;
                audit.PowerHifumi++;
                audit.Details.Add(
                    $"MIGRATE POWER | Hifumi Canonical+1={expected} | {path} | {definition.SkillName}");
            }
            else
            {
                // 골단/푼돈 걸기처럼 별도 규칙 또는 재계산 대기인 카드는 현재 값을 보존한다.
                definition.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
                definition.BasePowerFlatAdjustment = 0;
                audit.PowerDeferredLegacy++;
                audit.Details.Add(
                    $"DEFERRED(HIFUMI POWER) | Base={definition.BasePower}, ExpectedCommon+1={expected} | {path} | {definition.SkillName}");
            }

            return;
        }

        int expectedCanonical = curve + energyBonus;

        if (definition.BasePower == expectedCanonical)
        {
            definition.BasePowerRule = SkillBasePowerRule.CanonicalCurve;
            definition.BasePowerFlatAdjustment = 0;
            audit.PowerCanonical++;
            audit.Details.Add(
                $"MIGRATE POWER | Canonical={expectedCanonical} | {path} | {definition.SkillName}");
        }
        else
        {
            // 카드별 곡선 할인/캐릭터 예외/구 Core Data를 임의로 덮지 않는다.
            definition.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
            definition.BasePowerFlatAdjustment = 0;
            audit.PowerDeferredLegacy++;
            audit.Details.Add(
                $"DEFERRED(POWER) | Base={definition.BasePower}, Canonical={expectedCanonical} | {path} | {definition.SkillName}");
        }
    }

    private static void ClassifyPendingColor(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            audit.ColorNotApplicable++;
            return;
        }

        if (definition.Color != SkillColor.Unset)
        {
            audit.ColorExplicit++;
            return;
        }

        if (IsHifumi(path))
        {
            audit.ColorDeferredTbd++;
            return;
        }

        if (TryInferUniformLegacyColor(definition, out _))
            audit.ColorLegacyDerived++;
        else
            audit.ColorDeferredContent++;
    }

    private static void ClassifyPendingPower(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            audit.PowerNotApplicable++;
            return;
        }

        if (IsYujin(path))
        {
            audit.PowerYujin++;
            return;
        }

        if (definition.BasePowerRule != SkillBasePowerRule.CanonicalCurve ||
            definition.BasePowerFlatAdjustment != 0)
        {
            ClassifyPower(definition, path, audit);
            return;
        }

        if (!PowerFormulaService.TryGetCurveValue(
                definition.EffectiveRollCount,
                out int curve))
        {
            audit.PowerDeferredLegacy++;
            return;
        }

        int expected =
            curve +
            PowerFormulaService.GetEffectiveEnergyCost(definition) *
            PowerFormulaService.EnergyPowerPerPoint;

        if (IsHifumi(path))
        {
            if (definition.BasePower == expected + 1)
                audit.PowerHifumi++;
            else
                audit.PowerDeferredLegacy++;
            return;
        }

        if (definition.BasePower == expected)
            audit.PowerCanonical++;
        else
            audit.PowerDeferredLegacy++;
    }

    private static void ClassifyColor(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            audit.ColorNotApplicable++;
            return;
        }

        if (definition.Color != SkillColor.Unset)
        {
            audit.ColorExplicit++;
            return;
        }

        if (IsHifumi(path))
        {
            audit.ColorDeferredTbd++;
            return;
        }

        if (TryInferUniformLegacyColor(definition, out _))
        {
            // v1 완료 후에도 자동 이관 가능한 색이 Unset이면 실제 누락이다.
            audit.ManualReview++;
            audit.Details.Add(
                $"MANUAL | safe color inference not persisted | {path} | {definition.SkillName}");
            return;
        }

        audit.ColorDeferredContent++;
    }

    private static void ClassifyPower(
        SkillDefinition definition,
        string path,
        Audit audit)
    {
        if (!IsCombatRelevant(definition))
        {
            audit.PowerNotApplicable++;
            return;
        }

        if (IsYujin(path))
        {
            if (definition.BasePowerRule == SkillBasePowerRule.FixedBase &&
                definition.BasePower == 11)
            {
                audit.PowerYujin++;
            }
            else if (definition.IsPhaseASchemaMigrated)
            {
                audit.ManualReview++;
                audit.Details.Add(
                    $"MANUAL | Yujin power must be FixedBase 11 | {path} | {definition.SkillName}");
            }
            else
            {
                audit.PowerYujin++;
            }

            return;
        }

        switch (definition.BasePowerRule)
        {
            case SkillBasePowerRule.CanonicalCurve:
                audit.PowerCanonical++;
                break;

            case SkillBasePowerRule.CanonicalPlusFlat:
                if (IsHifumi(path) &&
                    definition.BasePowerFlatAdjustment == 1)
                {
                    audit.PowerHifumi++;
                }
                else
                {
                    audit.PowerExplicitException++;
                }
                break;

            case SkillBasePowerRule.FixedBase:
                audit.PowerExplicitException++;
                break;

            case SkillBasePowerRule.LegacyExplicit:
                audit.PowerDeferredLegacy++;
                break;

            default:
                audit.ManualReview++;
                audit.Details.Add(
                    $"MANUAL | unknown power rule | {path} | {definition.SkillName}");
                break;
        }
    }

    private static bool TryInferUniformLegacyColor(
        SkillDefinition definition,
        out SkillColor color)
    {
        color = SkillColor.Unset;

        if (definition?.Rolls == null ||
            definition.Rolls.Count == 0)
        {
            return false;
        }

        bool? stagger = null;

        foreach (SkillRollData roll in definition.Rolls)
        {
            if (roll == null)
                return false;

            bool isStagger =
                roll.Type == CombatRollType.Stagger;

            if (!stagger.HasValue)
            {
                stagger = isStagger;
                continue;
            }

            if (stagger.Value != isStagger)
                return false;
        }

        if (!stagger.HasValue)
            return false;

        color = stagger.Value
            ? SkillColor.Blue
            : SkillColor.Red;

        return true;
    }

    private static bool IsCombatRelevant(
        SkillDefinition definition)
    {
        if (definition == null)
            return false;

        return definition.ActionType == ActionType.NormalAttack ||
               definition.ActionType == ActionType.Duel ||
               definition.ActionType == ActionType.Prestige &&
               definition.ResolvePrestigeInCombat;
    }

    private static bool IsYujin(string path) =>
        ContainsPath(path, "/Design2026/Yujin/");

    private static bool IsHifumi(string path) =>
        ContainsPath(path, "/Design2026/Hifumi/");

    private static bool ContainsPath(
        string path,
        string segment)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               path.IndexOf(
                   segment,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Finish(
        Audit audit,
        string operation)
    {
        foreach (string detail in audit.Details)
            Debug.Log("[Phase A SkillDefinition Migration] " + detail);

        int schemaRemaining =
            Mathf.Max(
                0,
                audit.Total -
                (audit.SchemaCurrent + audit.Migrated));

        string message =
            "[Phase A SkillDefinition Contract — Rules 2026-09]\n" +
            $"Operation: {operation}\n" +
            $"SkillDefinition 총계: {audit.Total}\n" +
            $"Schema 이미 완료: {audit.SchemaCurrent}\n" +
            $"이번 실행에서 이관: {audit.Migrated}\n" +
            $"Schema 남음: {schemaRemaining}\n\n" +
            "[Color]\n" +
            $"명시 RED/BLUE: {audit.ColorExplicit}\n" +
            $"구 Roll에서 안전 이관/이관 가능: {audit.ColorLegacyDerived}\n" +
            $"정본 미정(Hifumi): {audit.ColorDeferredTbd}\n" +
            $"콘텐츠 이관 대기: {audit.ColorDeferredContent}\n" +
            $"비전투/해당 없음: {audit.ColorNotApplicable}\n\n" +
            "[Power]\n" +
            $"CanonicalCurve: {audit.PowerCanonical}\n" +
            $"Yujin FixedBase 11: {audit.PowerYujin}\n" +
            $"Hifumi Canonical+1: {audit.PowerHifumi}\n" +
            $"명시적 예외: {audit.PowerExplicitException}\n" +
            $"LegacyExplicit 보존/후속 이관: {audit.PowerDeferredLegacy}\n" +
            $"비전투/해당 없음: {audit.PowerNotApplicable}\n\n" +
            $"수동 검토 필요: {audit.ManualReview}\n\n" +
            "Phase A 완료 기준: Schema 남음 0 + 수동 검토 0.\n" +
            "정본 (미정)과 후속 O/Y/H/Enemy Core Data 항목은 " +
            "Unset/LegacyExplicit로 명시 보존되므로 Phase A 완료를 막지 않습니다.";

        bool clean =
            schemaRemaining == 0 &&
            audit.ManualReview == 0;

        if (clean)
            Debug.Log(message + "\n\nPhase A SkillDefinition contract clean.");
        else
            Debug.LogWarning(message);

        EditorUtility.DisplayDialog(
            "0915 Phase A SkillDefinition Contract",
            message +
            (clean
                ? "\n\nPhase A SkillDefinition contract clean."
                : "\n\nSchema/Manual 항목을 확인하세요."),
            "OK");
    }
}
