#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXOperatorRegistry
    {
        private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Add"] = new[] { "UnityEditor.VFX.Operator.Add", "Add" },
            ["Subtract"] = new[] { "UnityEditor.VFX.Operator.Subtract", "Subtract" },
            ["Multiply"] = new[] { "UnityEditor.VFX.Operator.Multiply", "Multiply" },
            ["Divide"] = new[] { "UnityEditor.VFX.Operator.Divide", "Divide" },
            ["Power"] = new[] { "UnityEditor.VFX.Operator.Power", "Power" },
            ["Sin"] = new[] { "UnityEditor.VFX.Operator.Sine", "UnityEditor.VFX.Operator.Sin", "Sine", "Sin" },
            ["Cos"] = new[] { "UnityEditor.VFX.Operator.Cosine", "UnityEditor.VFX.Operator.Cos", "Cosine", "Cos" },
            ["Abs"] = new[] { "UnityEditor.VFX.Operator.Absolute", "UnityEditor.VFX.Operator.Abs", "Absolute", "Abs" },
            ["Normalize"] = new[] { "UnityEditor.VFX.Operator.Normalize", "Normalize" },
            ["Length"] = new[] { "UnityEditor.VFX.Operator.Length", "Length" },
            ["Clamp"] = new[] { "UnityEditor.VFX.Operator.Clamp", "Clamp" },
            ["Lerp"] = new[] { "UnityEditor.VFX.Operator.Lerp", "UnityEditor.VFX.Operator.LinearInterpolate", "Lerp" },
            ["Remap"] = new[] { "UnityEditor.VFX.Operator.Remap", "Remap" },
            ["RandomFloat"] = new[] { "UnityEditor.VFX.Operator.Random", "Random" },
            ["RandomVector3"] = new[] { "UnityEditor.VFX.Operator.Random", "Random" },
            ["Time"] = new[] { "UnityEditor.VFX.Operator.BuiltInParameter", "UnityEditor.VFX.VFXBuiltInParameter", "BuiltInParameter" },
            ["DeltaTime"] = new[] { "UnityEditor.VFX.Operator.BuiltInParameter", "UnityEditor.VFX.VFXBuiltInParameter", "BuiltInParameter" },
            ["Attribute"] = new[] { "UnityEditor.VFX.Operator.AttributeParameter", "UnityEditor.VFX.VFXAttributeParameter", "AttributeParameter" },
            ["Noise3D"] = new[] { "UnityEditor.VFX.Operator.Noise", "UnityEditor.VFX.Operator.ValueNoise", "UnityEditor.VFX.Operator.PerlinNoise", "Noise" },
            ["CurlNoise3D"] = new[] { "UnityEditor.VFX.Operator.CurlNoise", "CurlNoise" }
        };

        internal static VFXModel Create(AbyssVFXExpressionRecipe expression, out string resolvedType)
        {
            resolvedType = string.Empty;
            if (expression == null) return null;
            IEnumerable<string> names = expression.kind == "Operator"
                ? new[] { expression.operatorType }
                : Aliases.TryGetValue(expression.kind, out string[] aliases) ? aliases : Array.Empty<string>();

            foreach (string name in names.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                VFXModel model = AbyssVFXInternalUtility.CreateModel(
                    name,
                    type => type.Namespace?.Contains("VFX", StringComparison.OrdinalIgnoreCase) == true &&
                            (type.Namespace?.Contains("Operator", StringComparison.OrdinalIgnoreCase) == true ||
                             type.Name.Contains("Parameter", StringComparison.OrdinalIgnoreCase)),
                    out resolvedType);
                if (model != null) return model;
            }
            return null;
        }

        internal static IReadOnlyDictionary<string, string[]> GetAliases() => Aliases;
    }
}
#endif
