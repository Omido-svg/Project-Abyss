#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXStrictJson
    {
        // JsonUtility는 재귀형 Recipe를 타입 깊이로 펼쳐 Unity 직렬화 깊이 제한을 넘긴다.
        // 전용 Codec이 동일한 엄격 Parser를 재사용할 수 있도록 파싱 진입점만 노출한다.
        internal static object ParseDocument(string json) => Parser.Parse(json);

        // Recipe 클래스와 엄격 JSON 화이트리스트가 서로 어긋나지 않도록
        // 공개 직렬화 필드 이름을 직접 사용한다. 새 필드를 Recipe에 추가한 뒤
        // StrictJson의 수동 문자열 목록을 갱신하지 않아 전체 필드가 거부되는 문제를 방지한다.
        private static readonly HashSet<string> RootKeys = FieldsOf<AbyssVFXRecipe>();
        private static readonly HashSet<string> SystemKeys = FieldsOf<AbyssVFXSystemRecipe>();
        private static readonly HashSet<string> SurfaceAuraKeys = FieldsOf<AbyssVFXSurfaceAuraRecipe>();
        private static readonly HashSet<string> ExpressionKeys = FieldsOf<AbyssVFXExpressionRecipe>();
        private static readonly HashSet<string> BlockKeys = FieldsOf<AbyssVFXCustomBlockRecipe>();
        private static readonly HashSet<string> BlockInputKeys = FieldsOf<AbyssVFXBlockInputRecipe>();
        private static readonly HashSet<string> SettingKeys = FieldsOf<AbyssVFXSettingRecipe>();
        private static readonly HashSet<string> OverrideKeys = FieldsOf<AbyssVFXExposedOverrideRecipe>();
        private static readonly HashSet<string> RangeKeys = FieldsOf<AbyssFloatRange>();
        private static readonly HashSet<string> VectorKeys = FieldsOf<AbyssVector3>();
        private static readonly HashSet<string> ColorKeys = FieldsOf<AbyssColor>();

        internal static bool ValidateContract(string json, out List<string> errors)
        {
            errors = new List<string>();
            object root;
            try
            {
                root = Parser.Parse(json);
            }
            catch (Exception ex)
            {
                errors.Add("JSON 문법 오류: " + ex.Message);
                return false;
            }

            if (root is not Dictionary<string, object> obj)
            {
                errors.Add("루트 JSON은 object여야 합니다.");
                return false;
            }

            ValidateObjectKeys(obj, RootKeys, "$", errors);
            RequireString(obj, "schemaVersion", "$", errors);
            RequireString(obj, "name", "$", errors);
            RequireString(obj, "buildMode", "$", errors);
            ValidateStringArray(obj, "requiredCapabilities", "$.requiredCapabilities", errors);

            if (!TryArray(obj, "systems", out List<object> systems))
                errors.Add("$.systems는 array여야 합니다.");
            else
                for (int i = 0; i < systems.Count; i++) ValidateSystem(systems[i], $"$.systems[{i}]", errors);

            if (obj.TryGetValue("surfaceAura", out object surfaceAuraRaw) && surfaceAuraRaw != null)
                ValidateSurfaceAura(surfaceAuraRaw, "$.surfaceAura", errors);

            if (obj.TryGetValue("exposedOverrides", out object overridesRaw))
            {
                if (overridesRaw is not List<object> overrides)
                    errors.Add("$.exposedOverrides는 array여야 합니다.");
                else
                    for (int i = 0; i < overrides.Count; i++) ValidateSimpleObject(overrides[i], OverrideKeys, $"$.exposedOverrides[{i}]", errors);
            }

            return errors.Count == 0;
        }

        private static void ValidateSurfaceAura(object raw, string path, List<string> errors)
        {
            if (raw is not Dictionary<string, object> obj)
            {
                errors.Add(path + "는 object여야 합니다.");
                return;
            }

            ValidateObjectKeys(obj, SurfaceAuraKeys, path, errors);
            ValidateOptionalObject(obj, "baseColor", ColorKeys, path, errors);
            ValidateOptionalObject(obj, "edgeColor", ColorKeys, path, errors);
            ValidateOptionalObject(obj, "hotColor", ColorKeys, path, errors);
            ValidateStringArray(obj, "excludedNameContains", path + ".excludedNameContains", errors);
        }

        private static void ValidateSystem(object raw, string path, List<string> errors)
        {
            if (raw is not Dictionary<string, object> obj)
            {
                errors.Add(path + "는 object여야 합니다.");
                return;
            }
            ValidateObjectKeys(obj, SystemKeys, path, errors);
            foreach (string key in new[] { "lifetime", "size", "speed" }) ValidateOptionalObject(obj, key, RangeKeys, path, errors);
            foreach (string key in new[] { "spawnBoxExtents", "direction", "gravity" }) ValidateOptionalObject(obj, key, VectorKeys, path, errors);
            ValidateOptionalObject(obj, "startColor", ColorKeys, path, errors);
            foreach (string key in new[] { "lifetimeExpression", "sizeExpression", "positionExpression", "velocityExpression", "colorExpression", "alphaExpression", "angleExpression", "updateVelocityExpression", "updatePositionExpression" })
                if (obj.TryGetValue(key, out object expr) && expr != null) ValidateExpression(expr, path + "." + key, errors);

            if (obj.TryGetValue("customBlocks", out object blocksRaw))
            {
                if (blocksRaw is not List<object> blocks) errors.Add(path + ".customBlocks는 array여야 합니다.");
                else for (int i = 0; i < blocks.Count; i++) ValidateBlock(blocks[i], $"{path}.customBlocks[{i}]", errors);
            }
            ValidateStringArray(obj, "capabilities", path + ".capabilities", errors);
        }

        private static void ValidateExpression(object raw, string path, List<string> errors)
        {
            if (raw is not Dictionary<string, object> obj)
            {
                errors.Add(path + "는 object여야 합니다.");
                return;
            }
            ValidateObjectKeys(obj, ExpressionKeys, path, errors);
            ValidateOptionalObject(obj, "vectorValue", VectorKeys, path, errors);
            ValidateOptionalObject(obj, "colorValue", ColorKeys, path, errors);
            ValidateOptionalObject(obj, "floatRange", RangeKeys, path, errors);
            ValidateOptionalObject(obj, "vectorMin", VectorKeys, path, errors);
            ValidateOptionalObject(obj, "vectorMax", VectorKeys, path, errors);
            ValidateSettings(obj, "settings", path, errors);
            if (obj.TryGetValue("inputs", out object inputsRaw))
            {
                if (inputsRaw is not List<object> inputs) errors.Add(path + ".inputs는 array여야 합니다.");
                else for (int i = 0; i < inputs.Count; i++) ValidateExpression(inputs[i], $"{path}.inputs[{i}]", errors);
            }
        }

        private static void ValidateBlock(object raw, string path, List<string> errors)
        {
            if (raw is not Dictionary<string, object> obj)
            {
                errors.Add(path + "는 object여야 합니다.");
                return;
            }
            ValidateObjectKeys(obj, BlockKeys, path, errors);
            ValidateSettings(obj, "settings", path, errors);
            if (obj.TryGetValue("inputs", out object inputsRaw))
            {
                if (inputsRaw is not List<object> inputs) errors.Add(path + ".inputs는 array여야 합니다.");
                else
                {
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        string inputPath = $"{path}.inputs[{i}]";
                        if (inputs[i] is not Dictionary<string, object> inputObj)
                        {
                            errors.Add(inputPath + "는 object여야 합니다.");
                            continue;
                        }
                        ValidateObjectKeys(inputObj, BlockInputKeys, inputPath, errors);
                        if (inputObj.TryGetValue("expression", out object expr) && expr != null) ValidateExpression(expr, inputPath + ".expression", errors);
                    }
                }
            }
        }

        private static void ValidateSettings(Dictionary<string, object> owner, string key, string path, List<string> errors)
        {
            if (!owner.TryGetValue(key, out object raw)) return;
            if (raw is not List<object> list) { errors.Add(path + "." + key + "는 array여야 합니다."); return; }
            for (int i = 0; i < list.Count; i++) ValidateSimpleObject(list[i], SettingKeys, $"{path}.{key}[{i}]", errors);
        }

        private static void ValidateSimpleObject(object raw, HashSet<string> allowed, string path, List<string> errors)
        {
            if (raw is not Dictionary<string, object> obj) { errors.Add(path + "는 object여야 합니다."); return; }
            ValidateObjectKeys(obj, allowed, path, errors);
            ValidateOptionalObject(obj, "vectorValue", VectorKeys, path, errors);
            ValidateOptionalObject(obj, "colorValue", ColorKeys, path, errors);
        }

        private static void ValidateOptionalObject(Dictionary<string, object> owner, string key, HashSet<string> allowed, string path, List<string> errors)
        {
            if (!owner.TryGetValue(key, out object raw) || raw == null) return;
            if (raw is not Dictionary<string, object> obj) { errors.Add(path + "." + key + "는 object여야 합니다."); return; }
            ValidateObjectKeys(obj, allowed, path + "." + key, errors);
        }

        private static void ValidateObjectKeys(Dictionary<string, object> obj, HashSet<string> allowed, string path, List<string> errors)
        {
            foreach (string key in obj.Keys.Where(key => !allowed.Contains(key)))
                errors.Add($"{path}.{key}: 알려지지 않은 필드입니다. 오타 또는 구형 schema 필드인지 확인하세요.");
        }

        private static void RequireString(Dictionary<string, object> obj, string key, string path, List<string> errors)
        {
            if (!obj.TryGetValue(key, out object value) || value is not string) errors.Add(path + "." + key + "는 string 필수 필드입니다.");
        }

        private static void ValidateStringArray(Dictionary<string, object> obj, string key, string path, List<string> errors)
        {
            if (!obj.TryGetValue(key, out object raw)) return;
            if (raw is not List<object> list || list.Any(item => item is not string)) errors.Add(path + "는 string array여야 합니다.");
        }

        private static bool TryArray(Dictionary<string, object> obj, string key, out List<object> list)
        {
            list = null;
            return obj.TryGetValue(key, out object raw) && (list = raw as List<object>) != null;
        }

        private static HashSet<string> FieldsOf<T>()
        {
            return new HashSet<string>(
                typeof(T)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => field.Name),
                StringComparer.Ordinal);
        }

        private sealed class Parser : IDisposable
        {
            private readonly StringReader reader;
            private Parser(string json) { reader = new StringReader(json ?? string.Empty); }
            internal static object Parse(string json) { using Parser parser = new(json); object value = parser.ParseValue(); parser.SkipWhitespace(); if (parser.Peek() != -1) throw new FormatException("JSON 끝 뒤에 불필요한 문자가 있습니다."); return value; }
            public void Dispose() => reader.Dispose();

            private object ParseValue()
            {
                SkipWhitespace();
                int c = Peek();
                return c switch
                {
                    '{' => ParseObject(),
                    '[' => ParseArray(),
                    '"' => ParseString(),
                    't' => ParseLiteral("true", true),
                    'f' => ParseLiteral("false", false),
                    'n' => ParseLiteral("null", null),
                    _ when c == '-' || char.IsDigit((char)c) => ParseNumber(),
                    _ => throw new FormatException($"예상하지 못한 문자 '{(char)c}'.")
                };
            }

            private Dictionary<string, object> ParseObject()
            {
                ReadExpected('{');
                Dictionary<string, object> result = new(StringComparer.Ordinal);
                SkipWhitespace();
                if (Peek() == '}') { reader.Read(); return result; }
                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    ReadExpected(':');
                    object value = ParseValue();
                    if (!result.TryAdd(key, value)) throw new FormatException("중복 key: " + key);
                    SkipWhitespace();
                    int c = reader.Read();
                    if (c == '}') return result;
                    if (c != ',') throw new FormatException("object 항목 사이에 ','가 필요합니다.");
                }
            }

            private List<object> ParseArray()
            {
                ReadExpected('[');
                List<object> result = new();
                SkipWhitespace();
                if (Peek() == ']') { reader.Read(); return result; }
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    int c = reader.Read();
                    if (c == ']') return result;
                    if (c != ',') throw new FormatException("array 항목 사이에 ','가 필요합니다.");
                }
            }

            private string ParseString()
            {
                ReadExpected('"');
                StringBuilder sb = new();
                while (true)
                {
                    int raw = reader.Read();
                    if (raw < 0) throw new EndOfStreamException("문자열이 닫히지 않았습니다.");
                    char c = (char)raw;
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    int esc = reader.Read();
                    if (esc < 0) throw new EndOfStreamException("escape가 끝나지 않았습니다.");
                    switch ((char)esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': sb.Append((char)int.Parse(ReadChars(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); break;
                        default: throw new FormatException("지원하지 않는 escape: \\" + (char)esc);
                    }
                }
            }

            private object ParseNumber()
            {
                StringBuilder sb = new();
                while (true)
                {
                    int c = Peek();
                    if (c < 0 || "0123456789+-.eE".IndexOf((char)c) < 0) break;
                    sb.Append((char)reader.Read());
                }
                string text = sb.ToString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return integer;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
                throw new FormatException("잘못된 숫자: " + text);
            }

            private object ParseLiteral(string text, object value)
            {
                foreach (char expected in text) if (reader.Read() != expected) throw new FormatException("잘못된 literal: " + text);
                return value;
            }

            private string ReadChars(int count)
            {
                char[] chars = new char[count];
                if (reader.Read(chars, 0, count) != count) throw new EndOfStreamException();
                return new string(chars);
            }

            private void ReadExpected(char expected)
            {
                SkipWhitespace();
                int actual = reader.Read();
                if (actual != expected) throw new FormatException($"'{expected}'가 필요하지만 '{(char)actual}'가 발견되었습니다.");
            }

            private void SkipWhitespace() { while (Peek() >= 0 && char.IsWhiteSpace((char)Peek())) reader.Read(); }
            private int Peek() => reader.Peek();
        }
    }
}
#endif
