#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킬 내부/스킬별 Effect SO를 공유 Template + SkillEffectEntry Override 구조로 변환합니다.
/// 기존 Sub-Asset/Legacy List는 롤백을 위해 삭제하지 않습니다.
/// </summary>
public static class SkillEffectTemplateMigration
{
    private const string ApplyMenu =
        "Tools/Project Abyss/Migration/Migrate Skill Effects to Shared Templates";

    private const string ValidateMenu =
        "Tools/Project Abyss/Migration/Validate Shared Skill Effect Templates";

    private const string TemplateRoot =
        "Assets/2. Data/BattleEffects/Templates";

    [MenuItem(ApplyMenu, false, 2200)]
    public static void Apply()
    {
        MigrationStats stats = new();
        EnsureFolder(TemplateRoot);

        Dictionary<string, SkillEffectDefinition> templates =
            LoadExistingTemplates();

        string[] guids = AssetDatabase.FindAssets("t:SkillDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
                continue;

            stats.SkillsScanned++;
            bool changed = MigrateSkill(skill, templates, stats);
            if (!changed)
                continue;

            stats.SkillsUpdated++;
            EditorUtility.SetDirty(skill);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(stats.BuildReport("SKILL EFFECT TEMPLATE MIGRATION"));
        EditorUtility.DisplayDialog(
            "Skill Effect Migration",
            stats.BuildReport("SKILL EFFECT TEMPLATE MIGRATION"),
            "확인");
    }

    [MenuItem(ValidateMenu, false, 2201)]
    public static void Validate()
    {
        MigrationStats stats = new();
        string[] guids = AssetDatabase.FindAssets("t:SkillDefinition");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
                continue;

            stats.SkillsScanned++;
            ValidateEntries(skill.EffectEntries, path, "Effects", stats);

            if (skill.Rolls != null)
            {
                for (int i = 0; i < skill.Rolls.Count; i++)
                {
                    SkillRollData roll = skill.Rolls[i];
                    if (roll == null)
                        continue;

                    ValidateEntries(
                        roll.OnWinEffectEntries,
                        path,
                        $"Rolls[{i}].OnWin",
                        stats);
                    ValidateEntries(
                        roll.OnLoseEffectEntries,
                        path,
                        $"Rolls[{i}].OnLose",
                        stats);
                }
            }

            ValidateEntries(
                skill.MultiRollPenalty?.EffectEntries,
                path,
                "MultiRollPenalty",
                stats);
        }

        Debug.Log(stats.BuildReport("SHARED SKILL EFFECT VALIDATION"));
        EditorUtility.DisplayDialog(
            "Skill Effect Validation",
            stats.BuildReport("SHARED SKILL EFFECT VALIDATION"),
            "확인");
    }

    private static bool MigrateSkill(
        SkillDefinition skill,
        IDictionary<string, SkillEffectDefinition> templates,
        MigrationStats stats)
    {
        bool changed = false;

        skill.EffectEntries ??= new List<SkillEffectEntry>();
        changed |= MigrateList(
            skill.EffectEntries,
            skill.Effects,
            templates,
            stats);

        if (skill.Rolls != null)
        {
            foreach (SkillRollData roll in skill.Rolls)
            {
                if (roll == null)
                    continue;

                roll.OnWinEffectEntries ??= new List<SkillEffectEntry>();
                roll.OnLoseEffectEntries ??= new List<SkillEffectEntry>();

                changed |= MigrateList(
                    roll.OnWinEffectEntries,
                    roll.OnWinEffects,
                    templates,
                    stats);
                changed |= MigrateList(
                    roll.OnLoseEffectEntries,
                    roll.OnLoseEffects,
                    templates,
                    stats);
            }
        }

        if (skill.MultiRollPenalty != null)
        {
            skill.MultiRollPenalty.EffectEntries ??=
                new List<SkillEffectEntry>();

            changed |= MigrateList(
                skill.MultiRollPenalty.EffectEntries,
                skill.MultiRollPenalty.Effects,
                templates,
                stats);
        }

        return changed;
    }

    private static bool MigrateList(
        List<SkillEffectEntry> entries,
        IReadOnlyList<SkillEffectDefinition> legacy,
        IDictionary<string, SkillEffectDefinition> templates,
        MigrationStats stats)
    {
        bool changed = false;

        // 이미 Entry로 옮겨진 항목도 Embedded/Skill 전용 Effect라면 공유 Template로 승격합니다.
        for (int i = 0; i < entries.Count; i++)
        {
            SkillEffectEntry entry = entries[i];
            if (entry?.Definition == null)
                continue;

            if (IsSharedTemplate(entry.Definition))
            {
                stats.SharedReferences++;
                continue;
            }

            SkillEffectOverrides overrides =
                CloneOverrides(entry.Overrides);
            SkillEffectDefinition template =
                ResolveTemplate(
                    entry.Definition,
                    overrides,
                    templates,
                    stats);

            entry.Definition = template;
            entry.Overrides = overrides;
            changed = true;
            stats.EntryReferencesUpdated++;
        }

        // Legacy 목록은 새 Entry가 비어 있을 때만 가져옵니다.
        // 이미 Entry가 존재하면 이중 실행을 피합니다.
        if (entries.Any(entry => entry?.Definition != null) ||
            legacy == null)
        {
            return changed;
        }

        foreach (SkillEffectDefinition effect in legacy)
        {
            if (effect == null)
                continue;

            SkillEffectOverrides overrides = new();
            SkillEffectDefinition template =
                ResolveTemplate(
                    effect,
                    overrides,
                    templates,
                    stats);

            entries.Add(new SkillEffectEntry
            {
                Definition = template,
                Overrides = overrides
            });
            stats.LegacyEntriesMigrated++;
            changed = true;
        }

        return changed;
    }

    private static SkillEffectDefinition ResolveTemplate(
        SkillEffectDefinition source,
        SkillEffectOverrides overrides,
        IDictionary<string, SkillEffectDefinition> templates,
        MigrationStats stats)
    {
        SkillEffectDefinition normalized =
            UnityEngine.Object.Instantiate(source);
        normalized.name = source.GetType().Name + "_Template";

        CaptureAndNormalizeParameters(
            source,
            normalized,
            overrides);

        string key = BuildTemplateKey(normalized);
        if (templates.TryGetValue(key, out SkillEffectDefinition existing) &&
            existing != null)
        {
            UnityEngine.Object.DestroyImmediate(normalized);
            stats.TemplatesReused++;
            return existing;
        }

        string typeName = Sanitize(source.GetType().Name);
        string semanticName = GetSemanticName(source);
        string hash = ComputeShortHash(key);
        string fileName = string.IsNullOrWhiteSpace(semanticName)
            ? $"{typeName}_{hash}.asset"
            : $"{typeName}_{Sanitize(semanticName)}_{hash}.asset";
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{TemplateRoot}/{fileName}");

        AssetDatabase.CreateAsset(normalized, path);
        templates[key] = normalized;
        stats.TemplatesCreated++;
        return normalized;
    }

    private static void CaptureAndNormalizeParameters(
        SkillEffectDefinition source,
        SkillEffectDefinition normalized,
        SkillEffectOverrides overrides)
    {
        SerializedObject sourceSo = new(source);
        SerializedObject templateSo = new(normalized);

        if (source is AddBodyPartStatusEffect)
        {
            CaptureInt(sourceSo, overrides, "Stack", nameof(overrides.OverrideStack), value => overrides.Stack = value);
            CaptureInt(sourceSo, overrides, "Duration", nameof(overrides.OverrideDuration), value => overrides.Duration = value);
            SetInt(templateSo, "Stack", 1);
            SetInt(templateSo, "Duration", 1);
        }
        else if (source is OlafNormalBleedEffect)
        {
            CaptureInt(sourceSo, overrides, "stack", nameof(overrides.OverrideStack), value => overrides.Stack = value);
            CaptureInt(sourceSo, overrides, "duration", nameof(overrides.OverrideDuration), value => overrides.Duration = value);
            SetInt(templateSo, "stack", 1);
            SetInt(templateSo, "duration", 1);
        }
        else if (source is ApplyStatusIfConditionEffect)
        {
            CaptureInt(sourceSo, overrides, "Stack", nameof(overrides.OverrideStack), value => overrides.Stack = value);
            CaptureInt(sourceSo, overrides, "Duration", nameof(overrides.OverrideDuration), value => overrides.Duration = value);
            CaptureBool(sourceSo, overrides, "ForceCharacterStatus", nameof(overrides.OverrideForceCharacterStatus), value => overrides.ForceCharacterStatus = value);
            SetInt(templateSo, "Stack", 1);
            SetInt(templateSo, "Duration", 1);
            SetBool(templateSo, "ForceCharacterStatus", false);
        }
        else if (source is GainPrestigeEffect)
        {
            CaptureInt(sourceSo, overrides, "Amount", nameof(overrides.OverrideAmount), value => overrides.Amount = value);
            CaptureBool(sourceSo, overrides, "GiveToSelectedTarget", nameof(overrides.OverrideGiveToSelectedTarget), value => overrides.GiveToSelectedTarget = value);
            SetInt(templateSo, "Amount", 1);
            SetBool(templateSo, "GiveToSelectedTarget", false);
        }
        else if (source is GainCustomResourceEffect)
        {
            CaptureString(sourceSo, overrides, "ResourceKey", nameof(overrides.OverrideResourceKey), value => overrides.ResourceKey = value);
            CaptureInt(sourceSo, overrides, "Amount", nameof(overrides.OverrideAmount), value => overrides.Amount = value);
            CaptureInt(sourceSo, overrides, "Maximum", nameof(overrides.OverrideMaximum), value => overrides.Maximum = value);
            CaptureBool(sourceSo, overrides, "GiveToSelectedTarget", nameof(overrides.OverrideGiveToSelectedTarget), value => overrides.GiveToSelectedTarget = value);
            SetString(templateSo, "ResourceKey", "Custom");
            SetInt(templateSo, "Amount", 1);
            SetInt(templateSo, "Maximum", 999);
            SetBool(templateSo, "GiveToSelectedTarget", false);
        }
        else if (source is ModifyResourceEffect)
        {
            CaptureString(sourceSo, overrides, "CustomResourceKey", nameof(overrides.OverrideResourceKey), value => overrides.ResourceKey = value);
            CaptureInt(sourceSo, overrides, "Amount", nameof(overrides.OverrideAmount), value => overrides.Amount = value);
            CaptureInt(sourceSo, overrides, "Minimum", nameof(overrides.OverrideMinimum), value => overrides.Minimum = value);
            CaptureInt(sourceSo, overrides, "Maximum", nameof(overrides.OverrideMaximum), value => overrides.Maximum = value);
            SetString(templateSo, "CustomResourceKey", string.Empty);
            SetInt(templateSo, "Amount", 1);
            SetInt(templateSo, "Minimum", 0);
            SetInt(templateSo, "Maximum", 999);
        }
        else if (source is ConditionalDamageEffect)
        {
            CaptureInt(sourceSo, overrides, "flatDamage", nameof(overrides.OverrideFlatValue), value => overrides.FlatValue = value);
            CaptureFloat(sourceSo, overrides, "multiplier", nameof(overrides.OverrideMultiplier), value => overrides.Multiplier = value);
            SetInt(templateSo, "flatDamage", 1);
            SetFloat(templateSo, "multiplier", 1f);
        }
        else if (source is ApplyRolledPartDamageEffect)
        {
            CaptureFloat(sourceSo, overrides, "powerMultiplier", nameof(overrides.OverrideMultiplier), value => overrides.Multiplier = value);
            CaptureInt(sourceSo, overrides, "flatBonus", nameof(overrides.OverrideFlatValue), value => overrides.FlatValue = value);
            SetFloat(templateSo, "powerMultiplier", 1f);
            SetInt(templateSo, "flatBonus", 0);
        }

        templateSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CaptureInt(
        SerializedObject source,
        SkillEffectOverrides overrides,
        string propertyName,
        string overrideFlag,
        Action<int> setter)
    {
        if (GetOverrideFlag(overrides, overrideFlag))
            return;

        SerializedProperty property = source.FindProperty(propertyName);
        if (property == null)
            return;

        setter(property.intValue);
        SetOverrideFlag(overrides, overrideFlag, true);
    }

    private static void CaptureFloat(
        SerializedObject source,
        SkillEffectOverrides overrides,
        string propertyName,
        string overrideFlag,
        Action<float> setter)
    {
        if (GetOverrideFlag(overrides, overrideFlag))
            return;

        SerializedProperty property = source.FindProperty(propertyName);
        if (property == null)
            return;

        setter(property.floatValue);
        SetOverrideFlag(overrides, overrideFlag, true);
    }

    private static void CaptureBool(
        SerializedObject source,
        SkillEffectOverrides overrides,
        string propertyName,
        string overrideFlag,
        Action<bool> setter)
    {
        if (GetOverrideFlag(overrides, overrideFlag))
            return;

        SerializedProperty property = source.FindProperty(propertyName);
        if (property == null)
            return;

        setter(property.boolValue);
        SetOverrideFlag(overrides, overrideFlag, true);
    }

    private static void CaptureString(
        SerializedObject source,
        SkillEffectOverrides overrides,
        string propertyName,
        string overrideFlag,
        Action<string> setter)
    {
        if (GetOverrideFlag(overrides, overrideFlag))
            return;

        SerializedProperty property = source.FindProperty(propertyName);
        if (property == null)
            return;

        setter(property.stringValue);
        SetOverrideFlag(overrides, overrideFlag, true);
    }

    private static bool GetOverrideFlag(
        SkillEffectOverrides overrides,
        string fieldName)
    {
        return (bool)(typeof(SkillEffectOverrides)
            .GetField(fieldName)
            ?.GetValue(overrides) ?? false);
    }

    private static void SetOverrideFlag(
        SkillEffectOverrides overrides,
        string fieldName,
        bool value)
    {
        typeof(SkillEffectOverrides)
            .GetField(fieldName)
            ?.SetValue(overrides, value);
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
            property.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
            property.stringValue = value;
    }

    private static SkillEffectOverrides CloneOverrides(
        SkillEffectOverrides source)
    {
        if (source == null)
            return new SkillEffectOverrides();

        return JsonUtility.FromJson<SkillEffectOverrides>(
                   JsonUtility.ToJson(source)) ??
               new SkillEffectOverrides();
    }

    private static Dictionary<string, SkillEffectDefinition>
        LoadExistingTemplates()
    {
        Dictionary<string, SkillEffectDefinition> result = new();

        foreach (string guid in AssetDatabase.FindAssets(
                     string.Empty,
                     new[] { TemplateRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillEffectDefinition effect =
                AssetDatabase.LoadAssetAtPath<SkillEffectDefinition>(path);
            if (effect == null)
                continue;

            result[BuildTemplateKey(effect)] = effect;
        }

        return result;
    }

    private static string BuildTemplateKey(
        SkillEffectDefinition effect)
    {
        StringBuilder builder = new();
        builder.AppendLine(
            effect.GetType().AssemblyQualifiedName);

        SerializedObject serialized = new(effect);
        SerializedProperty iterator =
            serialized.GetIterator();

        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyPath.StartsWith(
                    "m_Name",
                    StringComparison.Ordinal) ||
                iterator.propertyPath.StartsWith(
                    "m_ObjectHideFlags",
                    StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append(iterator.propertyPath);
            builder.Append('=');
            AppendPropertyValue(builder, iterator);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendPropertyValue(
        StringBuilder builder,
        SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                builder.Append(property.longValue);
                break;
            case SerializedPropertyType.Boolean:
                builder.Append(property.boolValue);
                break;
            case SerializedPropertyType.Float:
                builder.Append(
                    property.doubleValue.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                break;
            case SerializedPropertyType.String:
                builder.Append(property.stringValue);
                break;
            case SerializedPropertyType.Color:
            {
                Color value = property.colorValue;
                AppendFloats(
                    builder,
                    value.r, value.g, value.b, value.a);
                break;
            }
            case SerializedPropertyType.Enum:
                builder.Append(property.enumValueIndex);
                break;
            case SerializedPropertyType.Vector2:
            {
                Vector2 value = property.vector2Value;
                AppendFloats(builder, value.x, value.y);
                break;
            }
            case SerializedPropertyType.Vector3:
            {
                Vector3 value = property.vector3Value;
                AppendFloats(builder, value.x, value.y, value.z);
                break;
            }
            case SerializedPropertyType.Vector4:
            {
                Vector4 value = property.vector4Value;
                AppendFloats(builder, value.x, value.y, value.z, value.w);
                break;
            }
            case SerializedPropertyType.Rect:
            {
                Rect value = property.rectValue;
                AppendFloats(builder, value.x, value.y, value.width, value.height);
                break;
            }
            case SerializedPropertyType.Bounds:
            {
                Bounds value = property.boundsValue;
                AppendFloats(
                    builder,
                    value.center.x, value.center.y, value.center.z,
                    value.size.x, value.size.y, value.size.z);
                break;
            }
            case SerializedPropertyType.Quaternion:
            {
                Quaternion value = property.quaternionValue;
                AppendFloats(builder, value.x, value.y, value.z, value.w);
                break;
            }
            case SerializedPropertyType.ObjectReference:
            {
                UnityEngine.Object referenced =
                    property.objectReferenceValue;

                if (referenced == null)
                {
                    builder.Append("null");
                }
                else if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                             referenced,
                             out string guid,
                             out long localId))
                {
                    builder.Append(guid);
                    builder.Append(':');
                    builder.Append(localId);
                }
                else
                {
                    builder.Append(referenced.GetType().FullName);
                    builder.Append(':');
                    builder.Append(referenced.name);
                }

                break;
            }
            case SerializedPropertyType.AnimationCurve:
                builder.Append(
                    JsonUtility.ToJson(
                        new CurveWrapper
                        {
                            curve = property.animationCurveValue
                        }));
                break;
            default:
                builder.Append(property.propertyType);
                break;
        }
    }

    private static void AppendFloats(
        StringBuilder builder,
        params float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                builder.Append(',');

            builder.Append(
                values[i].ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }
    }

    private static string GetSemanticName(
        SkillEffectDefinition effect)
    {
        return effect switch
        {
            AddBodyPartStatusEffect status =>
                status.StatusEffectId.ToString(),
            ApplyStatusIfConditionEffect status =>
                status.StatusEffectId.ToString(),
            ModifyResourceEffect resource =>
                resource.ResourceType.ToString(),
            _ => string.Empty
        };
    }

    private static bool IsSharedTemplate(
        SkillEffectDefinition definition)
    {
        string path = AssetDatabase.GetAssetPath(definition);
        return !string.IsNullOrWhiteSpace(path) &&
               path.StartsWith(
                   TemplateRoot + "/",
                   StringComparison.Ordinal);
    }

    private static void ValidateEntries(
        IReadOnlyList<SkillEffectEntry> entries,
        string ownerPath,
        string field,
        MigrationStats stats)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            SkillEffectEntry entry = entries[i];
            stats.EntriesValidated++;

            if (entry?.Definition == null)
            {
                stats.Errors++;
                Debug.LogError(
                    $"[Skill Effect Validation] null Definition: " +
                    $"{ownerPath} / {field}[{i}]");
                continue;
            }

            if (!IsSharedTemplate(entry.Definition))
            {
                stats.Warnings++;
                Debug.LogWarning(
                    $"[Skill Effect Validation] 공유 Template 폴더 밖의 Effect: " +
                    $"{ownerPath} / {field}[{i}] -> " +
                    AssetDatabase.GetAssetPath(entry.Definition));
            }
        }
    }

    private static string ComputeShortHash(string text)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(
            Encoding.UTF8.GetBytes(text ?? string.Empty));
        return BitConverter.ToString(bytes)
            .Replace("-", string.Empty)
            .Substring(0, 10)
            .ToLowerInvariant();
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Trim();
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];

        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    [Serializable]
    private sealed class CurveWrapper
    {
        public AnimationCurve curve;
    }

    private sealed class MigrationStats
    {
        public int SkillsScanned;
        public int SkillsUpdated;
        public int LegacyEntriesMigrated;
        public int EntryReferencesUpdated;
        public int TemplatesCreated;
        public int TemplatesReused;
        public int SharedReferences;
        public int EntriesValidated;
        public int Warnings;
        public int Errors;

        public string BuildReport(string title)
        {
            return
                $"=== {title} ===\n" +
                $"Skills Scanned: {SkillsScanned}\n" +
                $"Skills Updated: {SkillsUpdated}\n" +
                $"Legacy Entries Migrated: {LegacyEntriesMigrated}\n" +
                $"Entry References Updated: {EntryReferencesUpdated}\n" +
                $"Templates Created: {TemplatesCreated}\n" +
                $"Templates Reused: {TemplatesReused}\n" +
                $"Existing Shared References: {SharedReferences}\n" +
                $"Entries Validated: {EntriesValidated}\n" +
                $"Warnings: {Warnings}\n" +
                $"Errors: {Errors}\n" +
                $"RESULT: {(Errors == 0 ? "READY" : "INCOMPLETE")}";
        }
    }
}
#endif
