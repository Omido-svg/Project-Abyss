#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXLegacyCleanupTool
    {
        private const string MenuPath =
            "Tools/Project Abyss/VFX AI/One-Time Cleanup Legacy VFX Generator";

        private static readonly string[] ProtectedFiles =
        {
            "AbyssVFXJsonCodec.cs",
            "AbyssVFXRecipe.cs",
            "AbyssVFXRecipeUtility.cs",
            "AbyssVFXStrictJson.cs",
            "AbyssVFXDiagnostics.cs",
            "AbyssVFXLegacyCleanupTool.cs",
            "AbyssParticleMaterialFactory.cs",
            "AbyssParticleMeshFactory.cs",
            "AbyssParticleSystemBuilder.cs",
            "AbyssVFXGeneratorWindow.cs"
        };

        private static readonly string[] ObsoleteTypeTokens =
        {
            "AbyssVFXExpressionRecipe",
            "AbyssVFXSystemRecipe",
            "AbyssVFXSurfaceAuraRecipe",
            "AbyssVFXSettingRecipe"
        };

        private static readonly string[] LegacyClassNames =
        {
            "AbyssVFXGraphBuilder17",
            "AbyssVFXExpressionCompiler",
            "AbyssVFXGraphAudit",
            "AbyssVFXInternalUtility",
            "AbyssVFXMeshAssetFactory",
            "AbyssVFXOperatorRegistry",
            "AbyssVFXSurfaceAuraAssetFactory",
            "AbyssVFXVersionGuard",
            "AbyssVFXAssetFactory",
            "AbyssMeshOutputSupportGenerator",
            "AbyssVFXGraphOnlyMigrationWindow",
            "AbyssVFXV4Migration",
            "AbyssVFXPresets",
            "AbyssProcedural3DGenerator",
            "AbyssProcedural3DGeneratorMenu",
            "AbyssUnifiedQuickMenus",
            "AbyssUnifiedVFXGeneratorWindow",
            "AbyssMeshOutputMetadata",
            "AbyssProcedural3DController",
            "AbyssProcedural3DRecipe",
            "AbyssProceduralMeshFactory",
            "AbyssShinCurtainAuraController",
            "AbyssShinSurfaceAuraController",
            "AbyssSkinnedAuraController"
        };

        private static readonly string[] SearchRoots =
        {
            "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator",
            "Assets/1. Scripts/Runtime/AbyssVFXGraphGenerator",
            "Assets/1. Scripts/Runtime/VFX"
        };

        [MenuItem(MenuPath, false, 1900)]
        private static void Cleanup()
        {
            List<string> candidates = FindCandidates();
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Legacy VFX Cleanup",
                    "제거할 구형 VFX Generator 스크립트가 없습니다.",
                    "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Legacy VFX Generator 제거",
                    $"구형 스크립트 {candidates.Count}개를 백업한 뒤 제거합니다.\n\n" +
                    "기존 .vfx/.prefab/.mat 자산과 런타임 VfxGraphEffectPlayer는 삭제하지 않습니다.",
                    "백업 후 제거",
                    "취소"))
            {
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("프로젝트 루트를 찾을 수 없습니다.");
            string backupRoot = Path.Combine(
                projectRoot,
                "ProjectAbyss_LegacyVFX_Cleanup_Backup_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Directory.CreateDirectory(backupRoot);
            List<string> deleted = new List<string>();
            List<string> failed = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string assetPath in candidates)
                {
                    try
                    {
                        Backup(projectRoot, backupRoot, assetPath);
                        if (AssetDatabase.DeleteAsset(assetPath))
                            deleted.Add(assetPath);
                        else
                            failed.Add(assetPath);
                    }
                    catch (Exception exception)
                    {
                        failed.Add(assetPath + " :: " + exception.Message);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            string report =
                $"삭제: {deleted.Count}개\n실패: {failed.Count}개\n백업: {backupRoot}";
            if (failed.Count > 0)
                report += "\n\n" + string.Join("\n", failed.Take(15));

            if (failed.Count == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog("Legacy VFX Cleanup 완료", report, "확인");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCleanup()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        private static List<string> FindCandidates()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in SearchRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileName(path);
                    if (ProtectedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    string absolute = Path.GetFullPath(path);
                    if (!File.Exists(absolute))
                        continue;

                    string text = File.ReadAllText(absolute);
                    if (IsLegacy(text))
                        result.Add(path);
                }
            }

            // 중복 BuildResult는 Particle System Builder 외 파일을 제거합니다.
            foreach (string root in SearchRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("/AbyssParticleSystemBuilder.cs", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string absolute = Path.GetFullPath(path);
                    if (!File.Exists(absolute))
                        continue;
                    string text = File.ReadAllText(absolute);
                    if (Regex.IsMatch(text, @"\bclass\s+AbyssVFXBuildResult\b"))
                        result.Add(path);
                }
            }

            return result.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsLegacy(string text)
        {
            foreach (string token in ObsoleteTypeTokens)
            {
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            foreach (string className in LegacyClassNames)
            {
                if (Regex.IsMatch(
                        text,
                        @"\b(class|struct|enum|interface)\s+" + Regex.Escape(className) + @"\b"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Backup(string projectRoot, string backupRoot, string assetPath)
        {
            string source = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
                return;

            string target = Path.Combine(
                backupRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, target, true);

            string metaSource = source + ".meta";
            if (File.Exists(metaSource))
                File.Copy(metaSource, target + ".meta", true);
        }
    }
}
#endif
