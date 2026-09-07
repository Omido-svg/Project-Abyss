#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillEffectEntry에서 현재 Template이 실제로 사용하는 Override만 표시합니다.
/// </summary>
[CustomPropertyDrawer(typeof(SkillEffectEntry))]
public sealed class SkillEffectEntryDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return line;

        SerializedProperty definition =
            property.FindPropertyRelative("Definition");

        const int schedulingRows = 2;

        return line +
               Gap +
               schedulingRows * (line + Gap) +
               GetRows(definition?.objectReferenceValue).Count *
               (line + Gap);
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        Rect header = new(
            position.x,
            position.y,
            position.width,
            line);

        SerializedProperty definition =
            property.FindPropertyRelative("Definition");
        SerializedProperty overrides =
            property.FindPropertyRelative("Overrides");
        SerializedProperty overrideTiming =
            property.FindPropertyRelative("OverrideTiming");
        SerializedProperty timing =
            property.FindPropertyRelative("Timing");
        SerializedProperty restrictToRoll =
            property.FindPropertyRelative("RestrictToRoll");
        SerializedProperty rollNumber =
            property.FindPropertyRelative("RollNumber");

        Rect foldout = new(
            header.x,
            header.y,
            16f,
            header.height);
        property.isExpanded = EditorGUI.Foldout(
            foldout,
            property.isExpanded,
            GUIContent.none,
            true);

        Rect objectRect = new(
            header.x + 18f,
            header.y,
            header.width - 18f,
            header.height);

        EditorGUI.PropertyField(
            objectRect,
            definition,
            new GUIContent(label.text));

        if (!property.isExpanded || overrides == null)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = header.yMax + Gap;

        DrawSchedulingRow(
            new Rect(position.x, y, position.width, line),
            overrideTiming,
            timing,
            "Detailed Timing");
        y += line + Gap;

        DrawSchedulingRow(
            new Rect(position.x, y, position.width, line),
            restrictToRoll,
            rollNumber,
            "Roll Number");
        y += line + Gap;

        foreach (OverrideRow row in GetRows(
                     definition?.objectReferenceValue))
        {
            DrawOverrideRow(
                new Rect(position.x, y, position.width, line),
                overrides,
                row);
            y += line + Gap;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }


    private static void DrawSchedulingRow(
        Rect rect,
        SerializedProperty toggle,
        SerializedProperty value,
        string label)
    {
        if (toggle == null || value == null)
            return;

        Rect toggleRect = new(
            rect.x,
            rect.y,
            18f,
            rect.height);

        toggle.boolValue = EditorGUI.Toggle(
            toggleRect,
            toggle.boolValue);

        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            Rect valueRect = new(
                rect.x + 20f,
                rect.y,
                rect.width - 20f,
                rect.height);

            EditorGUI.PropertyField(
                valueRect,
                value,
                new GUIContent(label));
        }
    }

    private static void DrawOverrideRow(
        Rect rect,
        SerializedProperty overrides,
        OverrideRow row)
    {
        SerializedProperty toggle =
            overrides.FindPropertyRelative(row.Toggle);
        SerializedProperty value =
            overrides.FindPropertyRelative(row.Value);

        if (toggle == null || value == null)
            return;

        Rect toggleRect = new(
            rect.x,
            rect.y,
            18f,
            rect.height);
        toggle.boolValue = EditorGUI.Toggle(
            toggleRect,
            toggle.boolValue);

        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            Rect valueRect = new(
                rect.x + 20f,
                rect.y,
                rect.width - 20f,
                rect.height);

            EditorGUI.PropertyField(
                valueRect,
                value,
                new GUIContent(row.Label));
        }
    }

    private static List<OverrideRow> GetRows(
        UnityEngine.Object definition)
    {
        if (definition is AddBodyPartStatusEffect)
        {
            return Rows(
                ("OverrideStack", "Stack", "Stack"),
                ("OverrideDuration", "Duration", "Duration"));
        }

        if (definition is OlafNormalBleedEffect)
        {
            return Rows(
                ("OverrideStack", "Stack", "Stack"),
                ("OverrideDuration", "Duration", "Duration"));
        }

        if (definition is ApplyStatusIfConditionEffect)
        {
            return Rows(
                ("OverrideStack", "Stack", "Stack"),
                ("OverrideDuration", "Duration", "Duration"),
                ("OverrideForceCharacterStatus", "ForceCharacterStatus", "Force Character Status"));
        }

        if (definition is GainPrestigeEffect)
        {
            return Rows(
                ("OverrideAmount", "Amount", "Amount"),
                ("OverrideGiveToSelectedTarget", "GiveToSelectedTarget", "Give To Selected Target"));
        }

        if (definition is GainCustomResourceEffect)
        {
            return Rows(
                ("OverrideResourceKey", "ResourceKey", "Resource Key"),
                ("OverrideAmount", "Amount", "Amount"),
                ("OverrideMaximum", "Maximum", "Maximum"),
                ("OverrideGiveToSelectedTarget", "GiveToSelectedTarget", "Give To Selected Target"));
        }

        if (definition is ModifyResourceEffect)
        {
            return Rows(
                ("OverrideResourceKey", "ResourceKey", "Resource Key"),
                ("OverrideAmount", "Amount", "Amount"),
                ("OverrideMinimum", "Minimum", "Minimum"),
                ("OverrideMaximum", "Maximum", "Maximum"));
        }

        if (definition is ConditionalDamageEffect ||
            definition is ApplyRolledPartDamageEffect)
        {
            return Rows(
                ("OverrideFlatValue", "FlatValue", "Flat Value"),
                ("OverrideMultiplier", "Multiplier", "Multiplier"));
        }

        return Rows(
            ("OverrideStack", "Stack", "Stack"),
            ("OverrideDuration", "Duration", "Duration"),
            ("OverrideAmount", "Amount", "Amount"),
            ("OverrideMinimum", "Minimum", "Minimum"),
            ("OverrideMaximum", "Maximum", "Maximum"),
            ("OverrideFlatValue", "FlatValue", "Flat Value"),
            ("OverrideMultiplier", "Multiplier", "Multiplier"),
            ("OverrideResourceKey", "ResourceKey", "Resource Key"),
            ("OverrideForceCharacterStatus", "ForceCharacterStatus", "Force Character Status"),
            ("OverrideGiveToSelectedTarget", "GiveToSelectedTarget", "Give To Selected Target"));
    }

    private static List<OverrideRow> Rows(
        params (string Toggle, string Value, string Label)[] values)
    {
        List<OverrideRow> result = new(values.Length);
        foreach ((string toggle, string value, string label) in values)
        {
            result.Add(new OverrideRow(
                toggle,
                value,
                label));
        }

        return result;
    }

    private readonly struct OverrideRow
    {
        public readonly string Toggle;
        public readonly string Value;
        public readonly string Label;

        public OverrideRow(
            string toggle,
            string value,
            string label)
        {
            Toggle = toggle;
            Value = value;
            Label = label;
        }
    }
}
#endif