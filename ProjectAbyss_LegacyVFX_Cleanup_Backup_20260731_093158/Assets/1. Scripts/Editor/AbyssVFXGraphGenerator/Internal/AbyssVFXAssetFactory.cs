#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.VFX;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXAssetFactory
    {
        internal static VisualEffectAsset CreateOrLoad(
            string requestedPath,
            VisualEffectAsset seedAsset,
            bool overwrite,
            out string actualPath,
            out string seedPath)
        {
            actualPath = NormalizeAssetPath(requestedPath);
            seedPath = string.Empty;
            EnsureAssetFolder(Path.GetDirectoryName(actualPath)?.Replace('\\', '/'));

            VisualEffectAsset existing = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(actualPath);
            if (existing != null)
            {
                if (overwrite)
                    return existing;

                actualPath = AssetDatabase.GenerateUniqueAssetPath(actualPath);
            }

            string sourcePath = seedAsset != null ? AssetDatabase.GetAssetPath(seedAsset) : FindBestSeedAssetPath(actualPath);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new InvalidOperationException(
                    "새 .vfx 에셋을 만들 Seed를 찾지 못했습니다. Project 창에서 빈 Visual Effect Graph 하나를 만든 뒤 Seed VFX Asset에 지정하세요.");
            }

            seedPath = sourcePath;
            if (!AssetDatabase.CopyAsset(sourcePath, actualPath))
                throw new InvalidOperationException($"VFX Seed 복사에 실패했습니다. Source={sourcePath}, Target={actualPath}");

            AssetDatabase.ImportAsset(actualPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            VisualEffectAsset created = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(actualPath);
            if (created == null)
                throw new InvalidOperationException($"복사된 VFX Asset을 로드하지 못했습니다: {actualPath}");

            return created;
        }

        internal static string FindBestSeedAssetPath(string excludedPath)
        {
            List<(string path, int score)> candidates = new();
            foreach (string guid in AssetDatabase.FindAssets("t:VisualEffectAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || string.Equals(path, excludedPath, StringComparison.Ordinal))
                    continue;
                if (path.Contains("/Generated/", StringComparison.OrdinalIgnoreCase))
                    continue;

                int score = 0;
                string lower = path.ToLowerInvariant();
                if (lower.Contains("empty")) score += 100;
                if (lower.Contains("default")) score += 80;
                if (lower.Contains("simple")) score += 50;
                if (lower.StartsWith("packages/com.unity.visualeffectgraph")) score += 40;
                if (lower.Contains("learning templates")) score += 20;
                if (lower.StartsWith("assets/")) score += 10;
                score -= path.Length / 20;
                candidates.Add((path, score));
            }

            return candidates.OrderByDescending(x => x.score).Select(x => x.path).FirstOrDefault();
        }

        internal static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || folder == "Assets")
                return;
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException("출력 폴더는 Assets 아래여야 합니다.", nameof(folder));
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            path = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(path))
                path = "Assets/VFX/Generated/VFX_Generated_AI.vfx";
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                path = "Assets/" + path.TrimStart('/');
            if (!path.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                path += ".vfx";
            return path;
        }
    }
}
#endif
