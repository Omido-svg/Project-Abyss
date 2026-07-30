#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using Block = UnityEditor.VFX.Block;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal sealed class AbyssVFXExpressionCompiler
    {
        private readonly VFXGraph graph;
        private readonly float baseX;
        private readonly float baseY;
        private readonly List<string> diagnostics;
        private int nodeIndex;

        internal AbyssVFXExpressionCompiler(VFXGraph graph, float baseX, float baseY, List<string> diagnostics)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            this.baseX = baseX;
            this.baseY = baseY;
            this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        internal void BindRequired(AbyssVFXExpressionRecipe expression, VFXModel target, string targetSlotName, int targetSlotIndex, string purpose)
        {
            if (expression == null) return;

            // Set Attribute 블록에 바로 표현할 수 있는 난수 범위는 Random Number Operator를 만들지 않는다.
            // Unity의 Random Number Operator는 float 전용이므로 Vector3 범위를 억지로 넣는 것은 잘못이다.
            if (TryBindRandomRangeDirectly(expression, target, purpose)) return;

            VFXSlot targetSlot = AbyssVFXInternalUtility.FindInputSlot(target, targetSlotName, targetSlotIndex);
            if (targetSlot == null)
                throw new InvalidOperationException($"{purpose}: target slot을 찾지 못했습니다. Model={target.GetType().FullName}, Name={targetSlotName}, Index={targetSlotIndex}");

            CompiledValue value = Compile(expression, purpose);
            if (value.IsLiteral)
            {
                if (!AssignLiteral(target, targetSlotName, targetSlotIndex, value.Literal))
                    throw new InvalidOperationException($"{purpose}: literal 값을 target slot에 기록하지 못했습니다.");
                return;
            }

            if (!AbyssVFXInternalUtility.TryLink(value.Output, targetSlot, out string error))
                throw new InvalidOperationException($"{purpose}: operator 연결 실패. {error}");
        }

        private CompiledValue Compile(AbyssVFXExpressionRecipe expression, string path)
        {
            if (expression == null) throw new InvalidOperationException(path + ": null expression");
            switch (expression.kind)
            {
                case "LiteralFloat": return CompiledValue.FromLiteral(expression.floatValue);
                case "LiteralVector3": return CompiledValue.FromLiteral(expression.vectorValue.ToVector3());
                case "LiteralColor": return CompiledValue.FromLiteral(expression.colorValue.ToColor());
                case "RandomFloat": return CompileRandomFloat(expression.floatRange.min, expression.floatRange.max, path);
                case "RandomVector3": return CompileRandomVector3(expression.vectorMin.ToVector3(), expression.vectorMax.ToVector3(), path);
            }

            VFXModel model = AbyssVFXOperatorRegistry.Create(expression, out string resolvedType);
            if (model == null)
                throw new InvalidOperationException($"{path}: '{expression.kind}' Operator 타입을 VFX Graph 17 assembly에서 찾지 못했습니다. Diagnostics의 Operator Registry를 확인하세요.");

            AttachModel(model);
            ApplySettings(model, expression.settings, path);
            ConfigureSpecialModel(model, expression, path);

            for (int i = 0; i < (expression.inputs?.Count ?? 0); i++)
            {
                CompiledValue child = Compile(expression.inputs[i], $"{path}.inputs[{i}]");
                string preferredName = PreferredInputName(expression.kind, i);
                VFXSlot input = AbyssVFXInternalUtility.FindInputSlot(model, preferredName, i);
                if (input == null)
                    throw new InvalidOperationException($"{path}: {resolvedType} input slot {i} ({preferredName})를 찾지 못했습니다. {AbyssVFXInternalUtility.DescribeInputSlotsDetailed(model)}");

                if (child.IsLiteral)
                {
                    bool assigned = !string.IsNullOrWhiteSpace(preferredName) && AbyssVFXInternalUtility.TryAssignInputSlotByName(model, preferredName, child.Literal);
                    if (!assigned) assigned = AbyssVFXInternalUtility.TryAssignInputSlotByIndex(model, i, child.Literal);
                    if (!assigned)
                        throw new InvalidOperationException($"{path}: {resolvedType} input {i}에 literal을 기록하지 못했습니다. ValueType={child.Literal?.GetType().FullName ?? "null"}. {AbyssVFXInternalUtility.DescribeInputSlotsDetailed(model)}");
                }
                else if (!AbyssVFXInternalUtility.TryLink(child.Output, input, out string error))
                {
                    throw new InvalidOperationException($"{path}: input {i} 연결 실패. {error}");
                }
            }

            VFXSlot output = AbyssVFXInternalUtility.FindOutputSlot(model, null, 0);
            if (output == null) throw new InvalidOperationException($"{path}: {resolvedType} output slot을 찾지 못했습니다.");
            diagnostics.Add($"Expression {path}: {expression.kind} -> {resolvedType}");
            return CompiledValue.FromOutput(output);
        }

        private CompiledValue CompileRandomFloat(float min, float max, string path)
        {
            VFXModel model = AbyssVFXInternalUtility.CreateModel(
                "UnityEditor.VFX.Operator.Random",
                type => type.Namespace?.Contains(".Operator", StringComparison.OrdinalIgnoreCase) == true,
                out string resolvedType)
                ?? AbyssVFXInternalUtility.CreateModel(
                    "Random",
                    type => type.Namespace?.Contains(".Operator", StringComparison.OrdinalIgnoreCase) == true,
                    out resolvedType);

            if (model == null)
                throw new InvalidOperationException(path + ": float Random Number Operator를 찾지 못했습니다.");

            // Random Number는 float 전용이다. m_Type/m_OperandType 같은 동적 타입 설정을 시도하지 않는다.
            AttachModel(model);
            AssignRandomScalarRange(model, min, max, path);

            VFXSlot output = AbyssVFXInternalUtility.FindOutputSlot(model, null, 0);
            if (output == null)
                throw new InvalidOperationException($"{path}: {resolvedType} output slot을 찾지 못했습니다.");

            diagnostics.Add($"Expression {path}: RandomFloat -> {resolvedType}");
            return CompiledValue.FromOutput(output);
        }

        private CompiledValue CompileRandomVector3(Vector3 min, Vector3 max, string path)
        {
            // VFX Graph의 Random Number Operator는 float만 출력한다.
            // Vector3 난수는 X/Y/Z 세 개의 scalar Random과 Append Vector 두 개로 구성한다.
            CompiledValue x = CompileRandomFloat(min.x, max.x, path + ".x");
            CompiledValue y = CompileRandomFloat(min.y, max.y, path + ".y");
            CompiledValue z = CompileRandomFloat(min.z, max.z, path + ".z");

            VFXSlot xy = CreateAppendVector(x.Output, y.Output, path + ".appendXY");
            VFXSlot xyz = CreateAppendVector(xy, z.Output, path + ".appendXYZ");
            diagnostics.Add($"Expression {path}: RandomVector3 -> 3x RandomFloat + 2x AppendVector");
            return CompiledValue.FromOutput(xyz);
        }

        private VFXSlot CreateAppendVector(VFXSlot first, VFXSlot second, string path)
        {
            VFXModel append = AbyssVFXInternalUtility.CreateModel(
                "UnityEditor.VFX.Operator.AppendVector",
                type => type.Namespace?.Contains(".Operator", StringComparison.OrdinalIgnoreCase) == true,
                out string resolvedType)
                ?? AbyssVFXInternalUtility.CreateModel(
                    "AppendVector",
                    type => type.Namespace?.Contains(".Operator", StringComparison.OrdinalIgnoreCase) == true,
                    out resolvedType);

            if (append == null)
                throw new InvalidOperationException(path + ": Append Vector Operator를 찾지 못했습니다.");

            AttachModel(append);
            IReadOnlyList<VFXSlot> inputs = AbyssVFXInternalUtility.GetInputSlots(append);
            if (inputs.Count < 2)
            {
                IReadOnlyList<VFXSlot> flattened = AbyssVFXInternalUtility.GetFlattenedInputSlots(append);
                inputs = flattened.Where(slot => !AbyssVFXInternalUtility.HasChildSlots(slot)).Take(2).ToList();
            }
            if (inputs.Count < 2)
                throw new InvalidOperationException($"{path}: {resolvedType}에 두 개의 입력 슬롯이 없습니다. {AbyssVFXInternalUtility.DescribeInputSlotsDetailed(append)}");

            if (!AbyssVFXInternalUtility.TryLink(first, inputs[0], out string firstError))
                throw new InvalidOperationException($"{path}: 첫 번째 Append Vector 입력 연결 실패. {firstError}");
            if (!AbyssVFXInternalUtility.TryLink(second, inputs[1], out string secondError))
                throw new InvalidOperationException($"{path}: 두 번째 Append Vector 입력 연결 실패. {secondError}");

            VFXSlot output = AbyssVFXInternalUtility.FindOutputSlot(append, null, 0);
            if (output == null) throw new InvalidOperationException(path + ": Append Vector output slot을 찾지 못했습니다.");
            return output;
        }

        private void AttachModel(VFXModel model)
        {
            // VFXModel.label은 공개되지 않고 VFXModel.name도 읽기 전용이다.
            model.position = new Vector2(baseX - 360f - (nodeIndex % 4) * 230f, baseY + (nodeIndex / 4) * 170f);
            nodeIndex++;
            graph.AddChild(model);
        }

        private static void AssignRandomScalarRange(VFXModel model, float min, float max, string path)
        {
            bool minAssigned = AbyssVFXInternalUtility.TryAssignInputSlotByName(model, "Min", min)
                               || AbyssVFXInternalUtility.TryAssignInputSlotByName(model, "A", min)
                               || AbyssVFXInternalUtility.TryAssignInputLeafByIndex(model, 0, min);
            bool maxAssigned = AbyssVFXInternalUtility.TryAssignInputSlotByName(model, "Max", max)
                               || AbyssVFXInternalUtility.TryAssignInputSlotByName(model, "B", max)
                               || AbyssVFXInternalUtility.TryAssignInputLeafByIndex(model, 1, max);

            if (!minAssigned || !maxAssigned)
            {
                throw new InvalidOperationException(
                    $"{path}: Random Number의 Min/Max float 슬롯 설정 실패. " +
                    $"MinAssigned={minAssigned}, MaxAssigned={maxAssigned}. " +
                    AbyssVFXInternalUtility.DescribeInputSlotsDetailed(model));
            }
        }

        private bool TryBindRandomRangeDirectly(AbyssVFXExpressionRecipe expression, VFXModel target, string purpose)
        {
            if (!TryEvaluateRandomRange(expression, out RangeValue range)) return false;

            // 현재 직접 범위 바인딩은 Random 설정을 가진 Set Attribute 계열에만 적용한다.
            if (!AbyssVFXInternalUtility.TrySetSetting(target, "Random", Block.RandomMode.PerComponent, out _))
                return false;

            bool minAssigned = AbyssVFXInternalUtility.TryAssignInputSlotByIndex(target, 0, range.Min);
            bool maxAssigned = AbyssVFXInternalUtility.TryAssignInputSlotByIndex(target, 1, range.Max);
            if (!minAssigned || !maxAssigned)
            {
                throw new InvalidOperationException(
                    $"{purpose}: Set Attribute Random 범위 기록 실패. " +
                    $"MinAssigned={minAssigned}, MaxAssigned={maxAssigned}, " +
                    $"MinType={range.Min?.GetType().FullName ?? "null"}, MaxType={range.Max?.GetType().FullName ?? "null"}. " +
                    AbyssVFXInternalUtility.DescribeInputSlotsDetailed(target));
            }

            diagnostics.Add($"Expression {purpose}: random range folded directly into {target.GetType().FullName}");
            return true;
        }

        private static bool TryEvaluateRandomRange(AbyssVFXExpressionRecipe expression, out RangeValue range)
        {
            range = default;
            if (expression == null) return false;

            if (expression.kind == "RandomFloat")
            {
                range = new RangeValue(expression.floatRange.min, expression.floatRange.max);
                return true;
            }
            if (expression.kind == "RandomVector3")
            {
                range = new RangeValue(expression.vectorMin.ToVector3(), expression.vectorMax.ToVector3());
                return true;
            }

            List<AbyssVFXExpressionRecipe> inputs = expression.inputs;
            if (inputs == null || inputs.Count != 2) return false;

            if (expression.kind == "Add")
            {
                if (TryEvaluateRandomRange(inputs[0], out RangeValue leftRange) && TryGetLiteral(inputs[1], out object rightLiteral))
                    return TryOffsetRange(leftRange, rightLiteral, false, out range);
                if (TryGetLiteral(inputs[0], out object leftLiteral) && TryEvaluateRandomRange(inputs[1], out RangeValue rightRange))
                    return TryOffsetRange(rightRange, leftLiteral, false, out range);
            }
            else if (expression.kind == "Subtract")
            {
                if (TryEvaluateRandomRange(inputs[0], out RangeValue leftRange) && TryGetLiteral(inputs[1], out object rightLiteral))
                    return TryOffsetRange(leftRange, rightLiteral, true, out range);
                if (TryGetLiteral(inputs[0], out object leftLiteral) && TryEvaluateRandomRange(inputs[1], out RangeValue rightRange))
                    return TryLiteralMinusRange(leftLiteral, rightRange, out range);
            }
            else if (expression.kind == "Multiply")
            {
                if (TryEvaluateRandomRange(inputs[0], out RangeValue leftRange) && TryGetLiteral(inputs[1], out object rightLiteral))
                    return TryScaleRange(leftRange, rightLiteral, out range);
                if (TryGetLiteral(inputs[0], out object leftLiteral) && TryEvaluateRandomRange(inputs[1], out RangeValue rightRange))
                    return TryScaleRange(rightRange, leftLiteral, out range);
            }

            return false;
        }

        private static bool TryGetLiteral(AbyssVFXExpressionRecipe expression, out object value)
        {
            value = null;
            if (expression == null) return false;
            if (expression.kind == "LiteralFloat") { value = expression.floatValue; return true; }
            if (expression.kind == "LiteralVector3") { value = expression.vectorValue.ToVector3(); return true; }
            return false;
        }

        private static bool TryOffsetRange(RangeValue source, object literal, bool subtract, out RangeValue result)
        {
            result = default;
            if (source.Min is float minFloat && source.Max is float maxFloat && TryAsFloat(literal, out float scalar))
            {
                result = subtract
                    ? new RangeValue(minFloat - scalar, maxFloat - scalar)
                    : new RangeValue(minFloat + scalar, maxFloat + scalar);
                return true;
            }
            if (source.Min is Vector3 minVector && source.Max is Vector3 maxVector && TryAsVector3(literal, out Vector3 vector))
            {
                result = subtract
                    ? new RangeValue(minVector - vector, maxVector - vector)
                    : new RangeValue(minVector + vector, maxVector + vector);
                return true;
            }
            return false;
        }

        private static bool TryLiteralMinusRange(object literal, RangeValue source, out RangeValue result)
        {
            result = default;
            if (source.Min is float minFloat && source.Max is float maxFloat && TryAsFloat(literal, out float scalar))
            {
                result = new RangeValue(scalar - maxFloat, scalar - minFloat);
                return true;
            }
            if (source.Min is Vector3 minVector && source.Max is Vector3 maxVector && TryAsVector3(literal, out Vector3 vector))
            {
                result = new RangeValue(vector - maxVector, vector - minVector);
                return true;
            }
            return false;
        }

        private static bool TryScaleRange(RangeValue source, object literal, out RangeValue result)
        {
            result = default;
            if (source.Min is float minFloat && source.Max is float maxFloat && TryAsFloat(literal, out float scalar))
            {
                float a = minFloat * scalar;
                float b = maxFloat * scalar;
                result = new RangeValue(Mathf.Min(a, b), Mathf.Max(a, b));
                return true;
            }
            if (source.Min is Vector3 minVector && source.Max is Vector3 maxVector)
            {
                Vector3 scale;
                if (TryAsFloat(literal, out float scalarValue)) scale = Vector3.one * scalarValue;
                else if (!TryAsVector3(literal, out scale)) return false;

                Vector3 a = Vector3.Scale(minVector, scale);
                Vector3 b = Vector3.Scale(maxVector, scale);
                result = new RangeValue(Vector3.Min(a, b), Vector3.Max(a, b));
                return true;
            }
            return false;
        }

        private static bool TryAsFloat(object value, out float result)
        {
            if (value is float f) { result = f; return true; }
            result = default;
            return false;
        }

        private static bool TryAsVector3(object value, out Vector3 result)
        {
            if (value is Vector3 vector) { result = vector; return true; }
            if (value is float scalar) { result = Vector3.one * scalar; return true; }
            result = default;
            return false;
        }

        private static string PreferredInputName(string kind, int index)
        {
            return kind switch
            {
                "Noise3D" or "CurlNoise3D" when index == 0 => "Coordinate",
                "Sin" or "Cos" or "Abs" or "Normalize" or "Length" when index == 0 => "Input",
                "Clamp" when index == 0 => "Input",
                "Clamp" when index == 1 => "Min",
                "Clamp" when index == 2 => "Max",
                "Lerp" when index == 0 => "A",
                "Lerp" when index == 1 => "B",
                "Lerp" when index == 2 => "T",
                "Remap" when index == 0 => "Input",
                "Remap" when index == 1 => "Old Min",
                "Remap" when index == 2 => "Old Max",
                "Remap" when index == 3 => "New Min",
                "Remap" when index == 4 => "New Max",
                _ when index == 0 => "A",
                _ when index == 1 => "B",
                _ => null
            };
        }

        private static void ConfigureSpecialModel(VFXModel model, AbyssVFXExpressionRecipe expression, string path)
        {
            if (expression.kind is "Time" or "DeltaTime")
            {
                string enumValue = expression.kind == "Time" ? "TotalTime" : "DeltaTime";
                bool success = false;
                foreach (string name in new[] { "m_BuiltInParameter", "builtInParameter", "parameter", "m_Parameter" })
                    success |= AbyssVFXInternalUtility.TrySetEnumSetting(model, name, enumValue, out _);
                if (!success) throw new InvalidOperationException(path + ": BuiltIn Parameter 종류를 설정하지 못했습니다.");
            }
            else if (expression.kind == "Attribute")
            {
                bool success = false;
                foreach (string name in new[] { "attribute", "m_Attribute", "attributeName", "m_AttributeName" })
                    success |= AbyssVFXInternalUtility.TrySetSetting(model, name, expression.attributeName, out _);
                if (!success) throw new InvalidOperationException(path + ": Attribute 이름을 설정하지 못했습니다: " + expression.attributeName);
            }
            else if (expression.kind is "Noise3D" or "CurlNoise3D")
            {
                foreach (string name in new[] { "dimensions", "dimension", "m_Dimensions" })
                    AbyssVFXInternalUtility.TrySetEnumSetting(model, name, "Three", out _);
            }
        }

        private static void ApplySettings(VFXModel model, List<AbyssVFXSettingRecipe> settings, string path)
        {
            foreach (AbyssVFXSettingRecipe setting in settings ?? new List<AbyssVFXSettingRecipe>())
            {
                if (setting == null || string.IsNullOrWhiteSpace(setting.name)) continue;
                object value = AbyssVFXInternalUtility.ConvertSettingValue(setting);
                if (setting.name.StartsWith("input:", StringComparison.OrdinalIgnoreCase))
                {
                    string slotName = setting.name.Substring("input:".Length);
                    if (!AbyssVFXInternalUtility.TryAssignInputSlotByName(model, slotName, value))
                        throw new InvalidOperationException($"{path}: input slot '{slotName}' 설정 실패.");
                }
                else if (!AbyssVFXInternalUtility.TrySetSetting(model, setting.name, value, out string warning))
                {
                    throw new InvalidOperationException($"{path}: setting '{setting.name}' 설정 실패. {warning}");
                }
            }
        }

        private static bool AssignLiteral(VFXModel model, string name, int index, object value)
        {
            if (!string.IsNullOrWhiteSpace(name) && AbyssVFXInternalUtility.TryAssignInputSlotByName(model, name, value)) return true;
            return index >= 0 && AbyssVFXInternalUtility.TryAssignInputSlotByIndex(model, index, value);
        }

        private readonly struct RangeValue
        {
            internal readonly object Min;
            internal readonly object Max;
            internal RangeValue(object min, object max) { Min = min; Max = max; }
        }

        private readonly struct CompiledValue
        {
            internal readonly object Literal;
            internal readonly VFXSlot Output;
            internal bool IsLiteral => Output == null;
            private CompiledValue(object literal, VFXSlot output) { Literal = literal; Output = output; }
            internal static CompiledValue FromLiteral(object value) => new(value, null);
            internal static CompiledValue FromOutput(VFXSlot slot) => new(null, slot);
        }
    }
}
#endif
