#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProjectAbyss.Editor.VFXAI
{
    /// <summary>
    /// VFX Recipe 전용 JSON Codec.
    /// Unity JsonUtility는 AbyssVFXExpressionRecipe.inputs 같은 재귀형 클래스를
    /// 실제 값이 null이어도 타입 구조 기준으로 계속 펼쳐 serialization depth limit을 발생시킨다.
    /// 이 Codec은 일반 JSON 트리를 직접 읽고/쓰므로 그 제한을 사용하지 않는다.
    /// </summary>
    internal static class AbyssVFXJsonCodec
    {
        private const int MaxDepth = 128;

        internal static string ToJson<T>(T value, bool pretty = true)
        {
            StringBuilder builder = new(8192);
            HashSet<object> recursionStack = new(ReferenceComparer.Instance);
            WriteValue(builder, value, pretty, 0, recursionStack);
            return builder.ToString();
        }

        internal static T FromJson<T>(string json)
        {
            object document = AbyssVFXStrictJson.ParseDocument(json);
            object converted = ConvertValue(document, typeof(T), "$", 0);
            return converted == null ? default : (T)converted;
        }

        private static object ConvertValue(object raw, Type targetType, string path, int depth)
        {
            if (depth > MaxDepth)
                throw new FormatException($"{path}: JSON 중첩 깊이는 최대 {MaxDepth}입니다.");

            Type nullable = Nullable.GetUnderlyingType(targetType);
            if (nullable != null)
            {
                if (raw == null) return null;
                targetType = nullable;
            }

            if (raw == null)
            {
                if (!targetType.IsValueType) return null;
                return Activator.CreateInstance(targetType);
            }

            if (targetType == typeof(string))
            {
                if (raw is string text) return text;
                throw TypeMismatch(path, "string", raw);
            }

            if (targetType == typeof(bool))
            {
                if (raw is bool boolean) return boolean;
                throw TypeMismatch(path, "bool", raw);
            }

            if (targetType.IsEnum)
            {
                if (raw is string enumText)
                    return Enum.Parse(targetType, enumText, true);
                if (IsNumber(raw))
                    return Enum.ToObject(targetType, Convert.ToInt64(raw, CultureInfo.InvariantCulture));
                throw TypeMismatch(path, "enum", raw);
            }

            if (IsNumericType(targetType))
            {
                if (!IsNumber(raw)) throw TypeMismatch(path, targetType.Name, raw);
                return ConvertNumber(raw, targetType, path);
            }

            if (targetType.IsArray)
            {
                if (raw is not IList rawArray) throw TypeMismatch(path, "array", raw);
                Type elementType = targetType.GetElementType()
                    ?? throw new InvalidOperationException($"{path}: 배열 요소 타입을 확인할 수 없습니다.");
                Array array = Array.CreateInstance(elementType, rawArray.Count);
                for (int i = 0; i < rawArray.Count; i++)
                    array.SetValue(ConvertValue(rawArray[i], elementType, $"{path}[{i}]", depth + 1), i);
                return array;
            }

            if (TryGetListElementType(targetType, out Type listElementType))
            {
                if (raw is not IList rawList) throw TypeMismatch(path, "array", raw);
                IList list = CreateList(targetType, listElementType, path);
                for (int i = 0; i < rawList.Count; i++)
                    list.Add(ConvertValue(rawList[i], listElementType, $"{path}[{i}]", depth + 1));
                return list;
            }

            if (raw is not Dictionary<string, object> objectMap)
                throw TypeMismatch(path, "object", raw);

            object instance;
            try
            {
                instance = Activator.CreateInstance(targetType, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{path}: {targetType.FullName} 인스턴스를 만들 수 없습니다.", ex);
            }

            foreach (FieldInfo field in GetSerializableFields(targetType))
            {
                if (!objectMap.TryGetValue(field.Name, out object fieldRaw))
                    continue; // 필드 initializer/constructor 기본값 유지

                object fieldValue = ConvertValue(fieldRaw, field.FieldType, path + "." + field.Name, depth + 1);
                field.SetValue(instance, fieldValue);
            }

            return instance;
        }

        private static void WriteValue(
            StringBuilder builder,
            object value,
            bool pretty,
            int depth,
            HashSet<object> recursionStack)
        {
            if (depth > MaxDepth)
                throw new InvalidOperationException($"JSON 중첩 깊이는 최대 {MaxDepth}입니다.");

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            Type type = value.GetType();
            if (value is string text)
            {
                WriteString(builder, text);
                return;
            }

            if (value is char character)
            {
                WriteString(builder, character.ToString());
                return;
            }

            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }

            if (type.IsEnum)
            {
                WriteString(builder, value.ToString());
                return;
            }

            if (IsNumericType(type))
            {
                WriteNumber(builder, value, type);
                return;
            }

            bool trackReference = !type.IsValueType;
            if (trackReference && !recursionStack.Add(value))
                throw new InvalidOperationException($"JSON으로 직렬화할 수 없는 실제 순환 참조가 발견되었습니다: {type.FullName}");

            try
            {
                if (value is IEnumerable enumerable)
                {
                    WriteArray(builder, enumerable, pretty, depth, recursionStack);
                    return;
                }

                WriteObject(builder, value, type, pretty, depth, recursionStack);
            }
            finally
            {
                if (trackReference) recursionStack.Remove(value);
            }
        }

        private static void WriteArray(
            StringBuilder builder,
            IEnumerable enumerable,
            bool pretty,
            int depth,
            HashSet<object> recursionStack)
        {
            builder.Append('[');
            bool first = true;
            foreach (object item in enumerable)
            {
                if (!first) builder.Append(',');
                if (pretty)
                {
                    builder.AppendLine();
                    AppendIndent(builder, depth + 1);
                }
                WriteValue(builder, item, pretty, depth + 1, recursionStack);
                first = false;
            }

            if (!first && pretty)
            {
                builder.AppendLine();
                AppendIndent(builder, depth);
            }
            builder.Append(']');
        }

        private static void WriteObject(
            StringBuilder builder,
            object value,
            Type type,
            bool pretty,
            int depth,
            HashSet<object> recursionStack)
        {
            FieldInfo[] fields = GetSerializableFields(type);
            builder.Append('{');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) builder.Append(',');
                if (pretty)
                {
                    builder.AppendLine();
                    AppendIndent(builder, depth + 1);
                }

                FieldInfo field = fields[i];
                WriteString(builder, field.Name);
                builder.Append(pretty ? ": " : ":");
                WriteValue(builder, field.GetValue(value), pretty, depth + 1, recursionStack);
            }

            if (fields.Length > 0 && pretty)
            {
                builder.AppendLine();
                AppendIndent(builder, depth);
            }
            builder.Append('}');
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return type
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsStatic && !field.IsNotSerialized)
                .OrderBy(field => field.MetadataToken)
                .ToArray();
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (!type.IsGenericType) return false;
            Type generic = type.GetGenericTypeDefinition();
            if (generic != typeof(List<>) && generic != typeof(IList<>) && generic != typeof(IEnumerable<>) && generic != typeof(ICollection<>))
                return false;
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        private static IList CreateList(Type targetType, Type elementType, string path)
        {
            Type concreteType = targetType.IsInterface || targetType.IsAbstract
                ? typeof(List<>).MakeGenericType(elementType)
                : targetType;
            try
            {
                return (IList)Activator.CreateInstance(concreteType, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{path}: List 타입 {concreteType.FullName}을 만들 수 없습니다.", ex);
            }
        }

        private static object ConvertNumber(object raw, Type targetType, string path)
        {
            try
            {
                if (targetType == typeof(byte)) return checked(Convert.ToByte(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(sbyte)) return checked(Convert.ToSByte(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(short)) return checked(Convert.ToInt16(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(ushort)) return checked(Convert.ToUInt16(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(int)) return checked(Convert.ToInt32(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(uint)) return checked(Convert.ToUInt32(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(long)) return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(ulong)) return checked(Convert.ToUInt64(raw, CultureInfo.InvariantCulture));
                if (targetType == typeof(float)) return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(double)) return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                if (targetType == typeof(decimal)) return Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
            {
                throw new FormatException($"{path}: 숫자 '{raw}'를 {targetType.Name}(으)로 변환할 수 없습니다.", ex);
            }

            throw new NotSupportedException($"{path}: 지원하지 않는 숫자 타입 {targetType.FullName}");
        }

        private static void WriteNumber(StringBuilder builder, object value, Type type)
        {
            if (type == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new InvalidOperationException("JSON은 NaN 또는 Infinity float를 지원하지 않습니다.");
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (type == typeof(double))
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException("JSON은 NaN 또는 Infinity double을 지원하지 않습니다.");
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (type == typeof(decimal))
            {
                builder.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        private static void AppendIndent(StringBuilder builder, int depth)
            => builder.Append(' ', depth * 2);

        private static bool IsNumber(object value)
            => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

        private static bool IsNumericType(Type type)
            => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
               || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
               || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        private static FormatException TypeMismatch(string path, string expected, object actual)
            => new($"{path}: {expected} 값이 필요하지만 {actual?.GetType().Name ?? "null"}이 입력되었습니다.");

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
