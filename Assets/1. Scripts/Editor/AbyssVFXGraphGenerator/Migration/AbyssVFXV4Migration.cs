#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXV4Migration
    {
        private static readonly string[] RecipePaths =
        {
            "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator/Examples/migration_VFX_Abyss_BloodHit_Burst_v4.json",
            "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator/Examples/migration_VFX_Abyss_BurnTick_Burst_v4.json",
            "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator/Examples/migration_VFX_Abyss_Bleeding_Status_v4.json",
            "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator/Examples/migration_VFX_Abyss_DarkRed_SwordWave_v4.json"
        };

        internal static string RebuildKnownLegacyAssets(string outputFolder, VisualEffectAsset seedAsset, bool createPrefab)
        {
            List<string> results = new();
            foreach (string path in RecipePaths)
            {
                TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                    ?? throw new InvalidOperationException("Migration Recipe를 찾지 못했습니다: " + path);
                if (!AbyssVFXRecipeUtility.TryFromJson(json.text, out AbyssVFXRecipe recipe, out string parseError))
                    throw new InvalidOperationException(path + " 파싱 실패: " + parseError);
                AbyssVFXBuildResult result = AbyssVFXGraphBuilder17.Build(recipe, outputFolder, seedAsset, true, createPrefab);
                results.Add(result.assetPath + " (systems=" + result.systemCount + ")");
            }
            AssetDatabase.Refresh();
            return string.Join("\n", results);
        }
    }
}
#endif
