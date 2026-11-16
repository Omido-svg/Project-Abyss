#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ContentCatalogAudit
{
    public const string ManifestPath =
        "Assets/2. Data/Progression/PhaseE/PhaseEContentManifest.asset";
    public const string EnemyCatalogPath =
        "Assets/2. Data/Progression/PhaseE/PhaseEEnemyContentCatalog.asset";
    public const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";
    public const string CanonicalEmotionRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";
    public const string RunItemCatalogPath =
        "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset";


    public static void AuditRollTextures(ContentDefinitionManifest manifest)
    {
        manifest.RollTextureScanned = 0;
        manifest.RollTexturePassed = 0;
        manifest.RollTexturePending = 0;
        manifest.RollTextureViolationCount = 0;
        manifest.RollTextureIssues ??= new List<string>();
        manifest.RollTextureIssues.Clear();

        string[] guids = AssetDatabase.FindAssets(
            "t:SkillDefinition",
            new[]
            {
                "Assets/2. Data/Characters/Design2026",
                "Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills"
            });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null ||
                (definition.ActionType != ActionType.NormalAttack &&
                 definition.ActionType != ActionType.Duel) ||
                definition.ResolverType != SkillResolverType.Dice ||
                definition.Rolls == null ||
                definition.Rolls.Count == 0)
            {
                continue;
            }

            manifest.RollTextureScanned++;

            int basePower = PowerFormulaService.ResolveBasePower(definition);
            if (basePower <= 0)
            {
                manifest.RollTexturePending++;
                manifest.RollTextureIssues.Add(
                    $"PENDING {path}: BasePower를 확정할 수 없음.");
                continue;
            }

            double sum = 0d;
            bool valid = true;
            for (int i = 0; i < definition.Rolls.Count; i++)
            {
                SkillRollData roll = definition.Rolls[i];
                if (roll == null)
                {
                    valid = false;
                    break;
                }

                int min = roll.GetDiceFinalMinPower(basePower);
                int max = roll.GetDiceFinalMaxPower(basePower);
                sum += (min + max) * 0.5d;
            }

            if (!valid)
            {
                manifest.RollTexturePending++;
                manifest.RollTextureIssues.Add(
                    $"PENDING {path}: null roll.");
                continue;
            }

            double actualMean = sum / definition.Rolls.Count;
            double expectedMean = basePower + 4.5d;

            if (Math.Abs(actualMean - expectedMean) <= 0.001d)
            {
                manifest.RollTexturePassed++;
            }
            else
            {
                // 0916(3) §4.3은 위치별 평균의 평균 = 기본위력 + 4.5를 정본 불변식으로 둔다.
                // TEMP_BALANCE_V1 표식으로 canonical 위반을 PASS 처리하지 않는다.
                manifest.RollTextureViolationCount++;
                manifest.RollTextureIssues.Add(
                    $"VIOLATION {path}: mean={actualMean:0.###}, expected={expectedMean:0.###}.");
            }
        }
    }
}
#endif
