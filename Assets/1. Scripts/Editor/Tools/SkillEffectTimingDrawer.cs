#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 내부/구형 SkillEffectTiming을 신규 Authoring에서 숨기고
/// 정본 트리거만 노출한다. 기존 에셋이 구형 값을 가지고 있으면
/// 현재 값은 보존한 채 경고 항목으로 표시한다.
/// </summary>
[CustomPropertyDrawer(typeof(SkillEffectTiming))]
public sealed class SkillEffectTimingDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        DrawPopup(
            position,
            property,
            label,
            SkillEffectTimingCatalog.AuthoringTimings);
        EditorGUI.EndProperty();
    }

    public static void DrawPopup(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        IReadOnlyList<SkillEffectTiming> allowedTimings)
    {
        SkillEffectTiming current =
            (SkillEffectTiming)property.intValue;

        IReadOnlyList<SkillEffectTiming> canonical =
            allowedTimings ?? SkillEffectTimingCatalog.AuthoringTimings;

        List<SkillEffectTiming> values =
            new List<SkillEffectTiming>(canonical.Count + 1);
        List<string> labels =
            new List<string>(canonical.Count + 1);

        int currentIndex = -1;
        bool currentAllowed = Contains(canonical, current);

        if (!currentAllowed)
        {
            values.Add(current);
            labels.Add(
                $"{SkillEffectTimingCatalog.GetDisplayName(current)}  ⚠");
            currentIndex = 0;
        }

        for (int i = 0; i < canonical.Count; i++)
        {
            SkillEffectTiming timing = canonical[i];
            if (timing == current)
                currentIndex = values.Count;

            values.Add(timing);
            labels.Add(
                SkillEffectTimingCatalog.GetDisplayName(timing));
        }

        if (currentIndex < 0)
            currentIndex = 0;

        GUIContent popupLabel = new GUIContent(
            label.text,
            SkillEffectTimingCatalog.GetSemantics(current));

        // Unity 6's EditorGUI.Popup overloads do not provide the
        // (Rect, GUIContent, int, string[]) combination used previously.
        // Draw the label separately, then use the stable
        // (Rect, int, string[]) overload for the popup itself.
        Rect popupRect = EditorGUI.PrefixLabel(position, popupLabel);
        int selected = EditorGUI.Popup(
            popupRect,
            currentIndex,
            labels.ToArray());

        if (selected >= 0 && selected < values.Count)
            property.intValue = (int)values[selected];
    }

    private static bool Contains(
        IReadOnlyList<SkillEffectTiming> values,
        SkillEffectTiming timing)
    {
        if (values == null)
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == timing)
                return true;
        }

        return false;
    }
}
#endif
