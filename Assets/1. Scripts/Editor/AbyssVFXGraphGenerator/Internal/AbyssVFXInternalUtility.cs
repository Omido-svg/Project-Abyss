#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXInternalUtility
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags DeclaredInstanceFlags = InstanceFlags | BindingFlags.DeclaredOnly;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type[] cachedEditorTypes;

        internal static IReadOnlyList<Type> GetVFXEditorTypes()
        {
            if (cachedEditorTypes != null) return cachedEditorTypes;
            try { cachedEditorTypes = typeof(VFXGraph).Assembly.GetTypes().Where(t => t != null).ToArray(); }
            catch (ReflectionTypeLoadException ex) { cachedEditorTypes = ex.Types.Where(t => t != null).ToArray(); }
            return cachedEditorTypes;
        }

        internal static Type FindModelType(string requested, Func<Type, bool> extraFilter = null)
        {
            if (string.IsNullOrWhiteSpace(requested)) return null;
            string token = requested.Trim();
            Type exact = GetVFXEditorTypes().FirstOrDefault(t =>
                !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) &&
                (string.Equals(t.FullName, token, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, token, StringComparison.OrdinalIgnoreCase)) &&
                (extraFilter == null || extraFilter(t)));
            if (exact != null) return exact;

            string[] words = token.Split(new[] { '.', '/', ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return GetVFXEditorTypes()
                .Where(t => !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) && (extraFilter == null || extraFilter(t)))
                .Select(t => new { Type = t, Score = ScoreType(t, words) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Type.FullName, StringComparer.Ordinal)
                .Select(x => x.Type)
                .FirstOrDefault();
        }

        internal static VFXModel CreateModel(string requested, Func<Type, bool> filter, out string resolvedType)
        {
            resolvedType = string.Empty;
            Type type = FindModelType(requested, filter);
            if (type == null) return null;
            try
            {
                if (ScriptableObject.CreateInstance(type) is VFXModel model)
                {
                    resolvedType = type.FullName;
                    return model;
                }
            }
            catch { }
            return null;
        }

        internal static bool TrySetSetting(VFXModel model, string settingName, object sourceValue, out string warning)
        {
            warning = string.Empty;
            if (model == null || string.IsNullOrWhiteSpace(settingName))
            {
                warning = "Model 또는 setting name이 없습니다.";
                return false;
            }

            // VFXModel의 setting은 반드시 같은 이름의 C# field/property로 노출된다는 보장이 없다.
            // 먼저 공식 내부 setting API가 알려 주는 현재 값 타입을 사용하고, 실패했을 때만 reflection으로 후퇴한다.
            try
            {
                object currentSetting = model.GetSettingValue(settingName);
                Type settingType = currentSetting?.GetType();
                if (TryConvertValue(sourceValue, settingType, currentSetting, out object settingValue))
                {
                    model.SetSettingValue(settingName, settingValue);
                    return true;
                }
            }
            catch
            {
                // 등록된 setting이 아니거나 현재 값이 null인 경우 아래 reflection 경로에서 타입을 찾는다.
            }

            FieldInfo field = FindField(model.GetType(), settingName);
            PropertyInfo property = FindProperty(model.GetType(), settingName);
            Type expected = field?.FieldType ?? property?.PropertyType;
            object current = null;
            try
            {
                current = field?.GetValue(model) ?? (property?.CanRead == true ? property.GetValue(model) : null);
            }
            catch
            {
                // current 값 조회 실패는 타입 기반 변환에 영향을 주지 않는다.
            }

            if (expected == null)
            {
                warning = $"{model.GetType().Name}.{settingName} setting을 찾지 못했습니다.";
                return false;
            }
            if (!TryConvertValue(sourceValue, expected, current, out object converted))
            {
                warning = $"{settingName}: {sourceValue?.GetType().Name ?? "null"} -> {expected.Name} 변환 실패";
                return false;
            }

            try
            {
                model.SetSettingValue(settingName, converted);
                return true;
            }
            catch
            {
                try
                {
                    if (field != null) field.SetValue(model, converted);
                    else if (property?.CanWrite == true) property.SetValue(model, converted);
                    else
                    {
                        warning = settingName + "은 읽기 전용입니다.";
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    warning = settingName + " 설정 실패: " + Unwrap(ex).Message;
                    return false;
                }
            }
        }

        internal static bool TrySetEnumSetting(VFXModel model, string settingName, string enumName, out string warning) => TrySetSetting(model, settingName, enumName, out warning);
        internal static bool TrySetObjectSetting(VFXModel model, string settingName, UnityEngine.Object value, out string warning) => TrySetSetting(model, settingName, value, out warning);

        internal static bool TryAssignInputSlotByName(VFXModel model, string slotName, object value)
        {
            VFXSlot slot = FindSlot(model, true, slotName, -1);
            return slot != null && TryAssignSlotValue(slot, value);
        }

        internal static bool TryAssignInputSlotByIndex(VFXModel model, int index, object value)
        {
            VFXSlot slot = FindSlot(model, true, null, index);
            if (slot != null && TryAssignSlotValue(slot, value)) return true;
            return TryAssignInputLeafByIndex(model, index, value);
        }

        internal static bool TryAssignInputLeafByIndex(VFXModel model, int index, object value)
        {
            IReadOnlyList<VFXSlot> leaves = GetFlattenedInputSlots(model)
                .Where(slot => !HasChildSlots(slot))
                .ToList();
            return index >= 0 && index < leaves.Count && TryAssignSlotValue(leaves[index], value);
        }

        internal static VFXSlot FindInputSlot(VFXModel model, string name, int index = -1) => FindSlot(model, true, name, index);
        internal static VFXSlot FindOutputSlot(VFXModel model, string name = null, int index = 0) => FindSlot(model, false, name, index);

        internal static bool TryLink(VFXSlot output, VFXSlot input, out string error)
        {
            error = string.Empty;
            if (output == null || input == null)
            {
                error = "output/input slot이 null입니다.";
                return false;
            }

            foreach ((VFXSlot owner, VFXSlot other) in new[] { (output, input), (input, output) })
            {
                foreach (string methodName in new[] { "Link", "LinkTo" })
                {
                    IEnumerable<MethodInfo> methods = owner.GetType().GetMethods(InstanceFlags)
                        .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        .Where(m =>
                        {
                            ParameterInfo[] parameters = m.GetParameters();
                            if (parameters.Length == 0 || !parameters[0].ParameterType.IsAssignableFrom(other.GetType())) return false;
                            return parameters.Skip(1).All(p => p.IsOptional || p.ParameterType == typeof(bool));
                        })
                        .OrderBy(m => m.GetParameters().Length);

                    foreach (MethodInfo method in methods)
                    {
                        try
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            object[] args = new object[parameters.Length];
                            args[0] = other;
                            for (int i = 1; i < parameters.Length; i++)
                                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : true;

                            object result = method.Invoke(owner, args);
                            if (method.ReturnType == typeof(bool) && result is bool success && !success) continue;
                            if (IsSlotLinked(output) || IsSlotLinked(input) || method.ReturnType != typeof(bool)) return true;
                        }
                        catch (Exception ex)
                        {
                            error = Unwrap(ex).Message;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(error))
                error = $"VFXSlot Link API를 찾지 못했습니다: {output.name} -> {input.name}";
            return false;
        }

        internal static string DescribeInputSlot(VFXModel model, int index)
        {
            VFXSlot slot = FindInputSlot(model, null, index);
            if (slot == null) return $"index {index} (unresolved)";
            Type type = SafeGetSlotValue(slot)?.GetType() ?? TryGetSlotDeclaredType(slot);
            return $"{slot.name} ({type?.FullName ?? "unknown"})";
        }

        internal static IReadOnlyList<VFXSlot> GetInputSlots(VFXModel model) => GetSlots(model, true).ToList();
        internal static IReadOnlyList<VFXSlot> GetOutputSlots(VFXModel model) => GetSlots(model, false).ToList();
        internal static IReadOnlyList<VFXSlot> GetFlattenedInputSlots(VFXModel model) => GetSlots(model, true).SelectMany(FlattenSlot).ToList();

        internal static bool HasChildSlots(VFXSlot slot)
        {
            if (slot == null) return false;
            object raw = FindProperty(slot.GetType(), "children")?.GetValue(slot) ?? FindField(slot.GetType(), "children")?.GetValue(slot);
            return raw is IEnumerable enumerable && enumerable.Cast<object>().Any(item => item is VFXSlot);
        }

        internal static string DescribeInputSlotsDetailed(VFXModel model)
        {
            if (model == null) return "InputSlots=<null model>";
            IReadOnlyList<VFXSlot> roots = GetInputSlots(model);
            if (roots.Count == 0) return "InputSlots=<none>";
            List<string> descriptions = new();
            for (int i = 0; i < roots.Count; i++)
                DescribeSlotRecursive(roots[i], $"[{i}]", descriptions);
            return "InputSlots=" + string.Join(", ", descriptions);
        }

        private static void DescribeSlotRecursive(VFXSlot slot, string path, List<string> descriptions)
        {
            Type type = SafeGetSlotValue(slot)?.GetType() ?? TryGetSlotDeclaredType(slot);
            descriptions.Add($"{path} {slot.name}<{type?.FullName ?? "unknown"}>");
            object raw = FindProperty(slot.GetType(), "children")?.GetValue(slot) ?? FindField(slot.GetType(), "children")?.GetValue(slot);
            if (raw is not IEnumerable enumerable) return;
            int childIndex = 0;
            foreach (object child in enumerable)
            {
                if (child is VFXSlot childSlot)
                    DescribeSlotRecursive(childSlot, path + $".{childIndex++}", descriptions);
            }
        }

        internal static bool IsSlotLinked(VFXSlot slot)
        {
            if (slot == null) return false;
            foreach (string name in new[] { "linkedSlots", "m_LinkedSlots" })
            {
                object value = FindProperty(slot.GetType(), name)?.GetValue(slot) ?? FindField(slot.GetType(), name)?.GetValue(slot);
                if (value is IEnumerable enumerable) return enumerable.Cast<object>().Any();
            }
            foreach (string name in new[] { "HasLink", "hasLink", "IsLinked", "isLinked" })
            {
                object value = FindProperty(slot.GetType(), name)?.GetValue(slot) ?? FindField(slot.GetType(), name)?.GetValue(slot);
                if (value is bool linked) return linked;
            }
            return false;
        }

        internal static IEnumerable<VFXModel> EnumerateModels(VFXModel root)
        {
            if (root == null) yield break;
            yield return root;
            object children = FindProperty(root.GetType(), "children")?.GetValue(root) ?? FindField(root.GetType(), "m_Children")?.GetValue(root);
            if (children is not IEnumerable enumerable) yield break;
            foreach (object child in enumerable)
                if (child is VFXModel model)
                    foreach (VFXModel nested in EnumerateModels(model)) yield return nested;
        }

        internal static bool TryLoadObject(string assetPath, string subAssetName, out UnityEngine.Object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (main != null && (string.IsNullOrWhiteSpace(subAssetName) || main.name == subAssetName)) { value = main; return true; }
            value = AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault(x => x != null && (string.IsNullOrWhiteSpace(subAssetName) || x.name == subAssetName));
            return value != null;
        }

        internal static object ConvertSettingValue(AbyssVFXSettingRecipe setting)
        {
            if (setting == null) return null;
            return setting.valueType switch
            {
                "Float" => setting.floatValue,
                "Int" => setting.intValue,
                "UInt" => setting.uintValue,
                "Bool" => setting.boolValue,
                "Vector3" => setting.vectorValue.ToVector3(),
                "Color" => setting.colorValue.ToColor(),
                "Object" => TryLoadObject(setting.assetPath, setting.subAssetName, out UnityEngine.Object obj) ? obj : null,
                _ => setting.stringValue
            };
        }

        private static VFXSlot FindSlot(VFXModel model, bool input, string name, int index)
        {
            if (model == null) return null;
            List<VFXSlot> slots = GetSlots(model, input).ToList();
            if (!string.IsNullOrWhiteSpace(name))
            {
                VFXSlot exact = slots.SelectMany(FlattenSlot).FirstOrDefault(s => string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
                string normalized = NormalizeName(name);
                VFXSlot loose = slots.SelectMany(FlattenSlot).FirstOrDefault(s => NormalizeName(s.name) == normalized);
                if (loose != null) return loose;
            }
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        private static IEnumerable<VFXSlot> GetSlots(VFXModel model, bool input)
        {
            string propertyName = input ? "inputSlots" : "outputSlots";
            object raw = FindProperty(model.GetType(), propertyName)?.GetValue(model) ?? FindField(model.GetType(), propertyName)?.GetValue(model);
            if (raw is IEnumerable enumerable)
                foreach (object item in enumerable) if (item is VFXSlot slot) yield return slot;
        }

        private static IEnumerable<VFXSlot> FlattenSlot(VFXSlot slot)
        {
            yield return slot;
            object raw = FindProperty(slot.GetType(), "children")?.GetValue(slot) ?? FindField(slot.GetType(), "children")?.GetValue(slot);
            if (raw is IEnumerable enumerable)
                foreach (object child in enumerable)
                    if (child is VFXSlot childSlot)
                        foreach (VFXSlot nested in FlattenSlot(childSlot)) yield return nested;
        }

        internal static bool TryAssignSlotValue(VFXSlot slot, object sourceValue)
        {
            try
            {
                object current = SafeGetSlotValue(slot);
                Type expected = current?.GetType() ?? TryGetSlotDeclaredType(slot);
                if (!TryConvertValue(sourceValue, expected, current, out object converted)) return false;
                slot.value = converted;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Abyss VFX] Slot 값 설정 실패: {slot?.name}\n{Unwrap(ex).Message}");
                return false;
            }
        }

        private static object SafeGetSlotValue(VFXSlot slot) { try { return slot.value; } catch { return null; } }

        private static Type TryGetSlotDeclaredType(VFXSlot slot)
        {
            object property = FindProperty(slot.GetType(), "property")?.GetValue(slot) ?? FindField(slot.GetType(), "m_Property")?.GetValue(slot);
            if (property == null) return null;
            return FindProperty(property.GetType(), "type")?.GetValue(property) as Type ?? FindField(property.GetType(), "type")?.GetValue(property) as Type;
        }

        private static bool TryConvertValue(object source, Type expected, object current, out object converted)
        {
            converted = source;
            if (expected == null) return true;
            if (source == null) { converted = expected.IsValueType ? Activator.CreateInstance(expected) : null; return !expected.IsValueType || converted != null; }
            if (expected.IsInstanceOfType(source)) return true;
            if (expected.IsEnum)
            {
                try { converted = source is string text ? Enum.Parse(expected, text, true) : Enum.ToObject(expected, source); return true; } catch { return false; }
            }
            if (expected == typeof(Vector3) && TryVector3(source, out Vector3 v3)) { converted = v3; return true; }
            if (expected == typeof(Vector4))
            {
                if (source is Color c) { converted = new Vector4(c.r, c.g, c.b, c.a); return true; }
                if (TryVector3(source, out Vector3 v)) { converted = new Vector4(v.x, v.y, v.z, 0f); return true; }
            }
            if (expected == typeof(Color))
            {
                if (source is Vector4 v4) { converted = new Color(v4.x, v4.y, v4.z, v4.w); return true; }
                if (TryVector3(source, out Vector3 v)) { converted = new Color(v.x, v.y, v.z, 1f); return true; }
            }
            string fullName = expected.FullName ?? expected.Name;
            if ((fullName == "UnityEditor.VFX.Position" || fullName == "UnityEditor.VFX.Vector" || fullName.EndsWith(".DirectionType")) && TryVector3(source, out Vector3 spatial))
                return TryCreateSpatial(expected, current, spatial, out converted);
            try { converted = Convert.ChangeType(source, expected); return true; } catch { return false; }
        }

        private static bool TryCreateSpatial(Type type, object current, Vector3 value, out object result)
        {
            result = current ?? Activator.CreateInstance(type);
            foreach (string name in new[] { "position", "vector", "direction", "value", "m_Position", "m_Vector", "m_Direction", "m_Value" })
            {
                FieldInfo field = FindField(type, name);
                if (field?.FieldType == typeof(Vector3)) { field.SetValue(result, value); return true; }
                PropertyInfo property = FindProperty(type, name);
                if (property?.CanWrite == true && property.PropertyType == typeof(Vector3)) { property.SetValue(result, value); return true; }
            }
            ConstructorInfo ctor = type.GetConstructor(InstanceFlags, null, new[] { typeof(Vector3) }, null);
            if (ctor != null) { result = ctor.Invoke(new object[] { value }); return true; }
            return false;
        }

        private static bool TryVector3(object source, out Vector3 value)
        {
            switch (source)
            {
                case Vector3 v3: value = v3; return true;
                case Vector4 v4: value = new Vector3(v4.x, v4.y, v4.z); return true;
                case Color c: value = new Vector3(c.r, c.g, c.b); return true;
                default: value = default; return false;
            }
        }

        private static int ScoreType(Type type, string[] words)
        {
            string full = NormalizeName(type.FullName ?? type.Name);
            int score = 0;
            foreach (string word in words)
            {
                string normalized = NormalizeName(word);
                if (full == normalized) score += 1000;
                else if (NormalizeName(type.Name) == normalized) score += 500;
                else if (full.Contains(normalized)) score += 40;
                else return 0;
            }
            if (type.Namespace?.Contains("UnityEditor.VFX", StringComparison.Ordinal) == true) score += 10;
            return score;
        }

        private static string NormalizeName(string value) => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        private static Exception Unwrap(Exception ex) => ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;

        internal static FieldInfo FindField(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrEmpty(fieldName)) return null;

            // GetField/GetProperty에 상속 멤버를 한 번에 포함시키면 VFX Graph 내부 타입의
            // new/override 멤버 때문에 AmbiguousMatchException이 발생할 수 있다.
            // 각 선언 타입을 Derived -> Base 순서로 직접 순회해 가장 가까운 선언을 선택한다.
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current
                    .GetFields(DeclaredInstanceFlags)
                    .FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.Ordinal));
                if (field != null) return field;
            }
            return null;
        }

        internal static PropertyInfo FindProperty(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrEmpty(propertyName)) return null;

            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current
                    .GetProperties(DeclaredInstanceFlags)
                    .Where(candidate => string.Equals(candidate.Name, propertyName, StringComparison.Ordinal))
                    // 호출부는 모두 인수 없는 일반 프로퍼티를 기대한다. 인덱서는 제외해야
                    // GetValue(instance) 호출 시 TargetParameterCountException도 방지할 수 있다.
                    .Where(candidate => candidate.GetIndexParameters().Length == 0)
                    .OrderByDescending(candidate => candidate.GetMethod != null)
                    .ThenByDescending(candidate => candidate.SetMethod != null)
                    .FirstOrDefault();
                if (property != null) return property;
            }
            return null;
        }

        internal static MethodInfo FindMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(methodName)) return null;
            parameterTypes ??= Type.EmptyTypes;

            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current
                    .GetMethods(DeclaredInstanceFlags)
                    .Where(candidate => string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    .FirstOrDefault(candidate => ParametersMatch(candidate.GetParameters(), parameterTypes));
                if (method != null) return method;
            }
            return null;
        }

        private static bool ParametersMatch(ParameterInfo[] parameters, Type[] parameterTypes)
        {
            if (parameters.Length != parameterTypes.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i]) return false;
            }
            return true;
        }
    }
}
#endif
