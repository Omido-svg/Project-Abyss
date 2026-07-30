#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXDiagnostics
    {
        internal static void RunFromMenu()
        {
            string report = BuildReport();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log(report);
            EditorUtility.DisplayDialog("Project Abyss VFX NodeGraph3D Diagnostics", report + "\n\n보고서는 클립보드에도 복사되었습니다.", "확인");
        }

        internal static string BuildReport()
        {
            AbyssVFXEnvironmentStatus status = AbyssVFXVersionGuard.GetStatus();
            StringBuilder sb = new();
            sb.AppendLine("=== Project Abyss VFX NodeGraph3D Diagnostics ===");
            sb.AppendLine($"Unity: {status.unityVersion} (expected {AbyssVFXVersionGuard.ExpectedUnity})");
            sb.AppendLine($"VFX Graph: {status.vfxVersion} (expected {AbyssVFXVersionGuard.ExpectedVFX})");
            sb.AppendLine($"URP: {status.urpVersion} (expected {AbyssVFXVersionGuard.ExpectedURP})");
            sb.AppendLine($"URP Active: {status.isURPActive}");
            sb.AppendLine($"Linear Color Space: {status.isLinearColorSpace}");
            sb.AppendLine($"Compute Shader Support: {status.supportsCompute}");
            sb.AppendLine();

            Type[] meshOutputs = AbyssVFXInternalUtility.GetVFXEditorTypes()
                .Where(t => !t.IsAbstract && typeof(VFXContext).IsAssignableFrom(t) && t.Name.Contains("Mesh", StringComparison.OrdinalIgnoreCase) && t.Name.Contains("Output", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
            sb.AppendLine("Mesh Output candidates:");
            foreach (Type type in meshOutputs) sb.AppendLine("  OK  " + type.FullName);
            if (meshOutputs.Length == 0) sb.AppendLine("  MISSING");

            sb.AppendLine();
            sb.AppendLine("Expression operator registry:");
            foreach (var pair in AbyssVFXOperatorRegistry.GetAliases().OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                Type resolved = pair.Value.Select(alias => AbyssVFXInternalUtility.FindModelType(alias,
                    t => t.Namespace?.Contains("Operator", StringComparison.OrdinalIgnoreCase) == true || t.Name.Contains("Parameter", StringComparison.OrdinalIgnoreCase))).FirstOrDefault(t => t != null);
                sb.AppendLine($"  {(resolved != null ? "OK" : "MISSING"),-7} {pair.Key,-16} -> {resolved?.FullName ?? string.Join(" | ", pair.Value)}");
            }

            Type turbulence = AbyssVFXInternalUtility.GetVFXEditorTypes().FirstOrDefault(t => !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) && t.Namespace?.Contains(".Block", StringComparison.OrdinalIgnoreCase) == true && t.Name.Contains("Turbulence", StringComparison.OrdinalIgnoreCase));
            Type orient = AbyssVFXInternalUtility.GetVFXEditorTypes().FirstOrDefault(t => !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) && t.Namespace?.Contains(".Block", StringComparison.OrdinalIgnoreCase) == true && t.Name.Contains("Orient", StringComparison.OrdinalIgnoreCase));
            sb.AppendLine();
            sb.AppendLine("Required 3D blocks:");
            sb.AppendLine("  Turbulence: " + (turbulence?.FullName ?? "MISSING"));
            sb.AppendLine("  Orientation: " + (orient?.FullName ?? "MISSING"));
            sb.AppendLine("  Seed candidate: " + (AbyssVFXAssetFactory.FindBestSeedAssetPath(string.Empty) ?? "NONE"));
            sb.AppendLine();
            bool operatorsReady = AbyssVFXOperatorRegistry.GetAliases().Where(x => x.Key is "Add" or "Multiply" or "Normalize" or "RandomFloat" or "RandomVector3")
                .All(pair => pair.Value.Any(alias => AbyssVFXInternalUtility.FindModelType(alias,
                    t => t.Namespace?.Contains("Operator", StringComparison.OrdinalIgnoreCase) == true) != null));
            sb.AppendLine("Result: " + (status.CoreCompatible && meshOutputs.Length > 0 && operatorsReady ? "NODEGRAPH3D CORE COMPATIBLE" : "CHECK MISSING TYPES"));
            return sb.ToString();
        }
    }
}
#endif
