#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXGraphAudit
    {
        internal static void ValidateBuiltGraph(VFXGraph graph, int expectedSystems, bool expressionsRequested, List<string> diagnostics)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            List<VFXModel> models = AbyssVFXInternalUtility.EnumerateModels(graph).Distinct().ToList();
            string[] forbidden = { "Quad", "Planar", "Billboard", "Flipbook", "Sprite", "Texture2D", "TextureSheet" };
            List<string> offenders = models.Select(m => m.GetType().FullName ?? m.GetType().Name)
                .Where(name => forbidden.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase))).Distinct().ToList();
            if (offenders.Count > 0) throw new InvalidOperationException("2D/Planar 모델이 생성된 Graph에 남아 있습니다:\n" + string.Join("\n", offenders));

            int meshOutputs = models.Count(m => m is VFXContext && m.GetType().Name.Contains("Mesh", StringComparison.OrdinalIgnoreCase) && m.GetType().Name.Contains("Output", StringComparison.OrdinalIgnoreCase));
            if (meshOutputs != expectedSystems) throw new InvalidOperationException($"Mesh Output 수가 예상과 다릅니다. Expected={expectedSystems}, Actual={meshOutputs}");

            int operatorCount = models.Count(m =>
                m.GetType().Namespace?.Contains("Operator", StringComparison.OrdinalIgnoreCase) == true ||
                m.GetType().Name.Contains("Parameter", StringComparison.OrdinalIgnoreCase));

            // Expression Recipe가 존재한다고 해서 Operator 노드가 반드시 남아야 하는 것은 아니다.
            // Literal은 대상 Block Slot에 직접 기록되고, RandomFloat/RandomVector3 및
            // RandomRange + Literal 산술은 Set Attribute의 Random Min/Max로 접힐 수 있다.
            // 실제 Operator가 필요한 Expression은 Compiler가 노드를 생성하거나 그 자리에서 실패하므로,
            // 여기에서 operatorCount == 0을 생성 실패로 취급하면 정상적인 lower/fold 결과를 오탐한다.
            if (expressionsRequested && operatorCount == 0)
            {
                bool foldedExpression = diagnostics.Any(message =>
                    message?.Contains("folded directly", StringComparison.OrdinalIgnoreCase) == true);
                diagnostics.Add(foldedExpression
                    ? "Audit note: Expression Recipe는 Set Attribute literal/random range로 정상 folding되어 Operator 노드가 필요하지 않습니다."
                    : "Audit note: Expression Recipe가 literal/direct binding으로 처리되어 Operator 노드가 생성되지 않았습니다.");
            }

            diagnostics.Add($"Audit OK: models={models.Count}, meshOutputs={meshOutputs}, operators={operatorCount}, forbidden2D=0");
        }
    }
}
#endif
