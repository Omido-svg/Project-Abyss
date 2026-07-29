#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using Block = UnityEditor.VFX.Block;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXDiagnostics
    {
        [MenuItem("Tools/Project Abyss/VFX AI/Run Compatibility Diagnostics", priority = 100)]
        internal static void RunFromMenu()
        {
            string report = BuildReport();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log(report);
            EditorUtility.DisplayDialog("Project Abyss VFX AI Diagnostics", report + "\n\n보고서는 클립보드에도 복사되었습니다.", "확인");
        }

        internal static string BuildReport()
        {
            AbyssVFXEnvironmentStatus status = AbyssVFXVersionGuard.GetStatus();
            StringBuilder sb = new();
            sb.AppendLine("=== Project Abyss AI VFX Graph Generator Diagnostics ===");
            sb.AppendLine($"Unity: {status.unityVersion} (expected {AbyssVFXVersionGuard.ExpectedUnity})");
            sb.AppendLine($"VFX Graph: {status.vfxVersion} (expected {AbyssVFXVersionGuard.ExpectedVFX})");
            sb.AppendLine($"URP: {status.urpVersion} (expected {AbyssVFXVersionGuard.ExpectedURP})");
            sb.AppendLine($"URP Active: {status.isURPActive}");
            sb.AppendLine($"Linear Color Space: {status.isLinearColorSpace}");
            sb.AppendLine($"Compute Shader Support: {status.supportsCompute}");
            sb.AppendLine();
            AppendType<VFXGraph>(sb);
            AppendType<VFXBasicSpawner>(sb);
            AppendType<VFXSpawnerBurst>(sb);
            AppendType<VFXSpawnerConstantRate>(sb);
            AppendType<VFXBasicInitialize>(sb);
            AppendType<VFXBasicUpdate>(sb);
            AppendType<VFXPlanarPrimitiveOutput>(sb);
            AppendType<Block.SetAttribute>(sb);
            AppendType<Block.Gravity>(sb);
            AppendType<Block.Drag>(sb);
            sb.AppendLine();
            sb.AppendLine($"Seed candidate: {AbyssVFXAssetFactory.FindBestSeedAssetPath(string.Empty) ?? "NONE"}");
            sb.AppendLine($"Result: {(status.CoreCompatible ? "CORE COMPATIBLE" : "CHECK WARNINGS")}");
            return sb.ToString();
        }

        private static void AppendType<T>(StringBuilder sb)
        {
            Type type = typeof(T);
            sb.AppendLine($"Type OK: {type.FullName} [{type.Assembly.GetName().Name}]");
        }
    }
}
#endif
