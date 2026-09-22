#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 상태 authoring inspector.
/// Canonical Presence는 N 필드를 숨기고 T/∞만 노출하며,
/// Bespoke는 generic Add Status authoring을 사용하지 못하도록 명시한다.
/// Legacy schema는 기존 직렬화 필드를 그대로 노출한다.
/// </summary>
public abstract class StatusEffectAuthoringEditorBase : Editor
{
    protected abstract bool HasApplicationTiming { get; }
    protected abstract bool HasForceCharacterStatus { get; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "StatusEffectId",
            "AuthoringSchema",
            "Stack",
            "Duration",
            "InfiniteDuration",
            "RegenerationHealAmount",
            "RegenerationChannel",
            "ApplicationTiming",
            "ForceCharacterStatus");

        SerializedProperty statusId =
            serializedObject.FindProperty("StatusEffectId");
        SerializedProperty schema =
            serializedObject.FindProperty("AuthoringSchema");
        SerializedProperty stack =
            serializedObject.FindProperty("Stack");
        SerializedProperty duration =
            serializedObject.FindProperty("Duration");
        SerializedProperty infinite =
            serializedObject.FindProperty("InfiniteDuration");
        SerializedProperty regenerationHeal =
            serializedObject.FindProperty("RegenerationHealAmount");
        SerializedProperty regenerationChannel =
            serializedObject.FindProperty("RegenerationChannel");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Status Authoring",
            EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(statusId);
        EditorGUILayout.PropertyField(schema);

        StatusEffectId id =
            (StatusEffectId)statusId.intValue;
        StatusEffectAuthoringSchema authoringSchema =
            (StatusEffectAuthoringSchema)schema.intValue;
        StatusEffectStorageKind kind =
            StatusEffectFactory.GetStorageKind(id);

        if (authoringSchema == StatusEffectAuthoringSchema.Legacy)
        {
            EditorGUILayout.HelpBox(
                "Legacy schema: 기존 asset의 직렬화 의미를 보존합니다. 0922 N/T/∞ 규칙은 자동 활성화되지 않습니다.",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                stack,
                new GUIContent("Stack"));
            EditorGUILayout.PropertyField(
                duration,
                new GUIContent("Duration"));

            if (id == StatusEffectId.Regeneration)
            {
                EditorGUILayout.PropertyField(regenerationHeal);
                EditorGUILayout.PropertyField(regenerationChannel);
            }
        }
        else if (kind == StatusEffectStorageKind.Bespoke)
        {
            EditorGUILayout.HelpBox(
                $"{id} is Bespoke. Canonical0922 generic Add Status authoring is disabled. Use its dedicated effect/mechanic.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField(
                "Storage Kind",
                kind.ToString());

            if (kind == StatusEffectStorageKind.NumericTimed)
            {
                EditorGUILayout.PropertyField(
                    stack,
                    new GUIContent("Value (N)"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "PresenceTimed: gameplay N이 없습니다. 동일 상태 재부여는 효과를 중첩하지 않고 Duration=max(current,new)만 적용합니다.",
                    MessageType.None);
            }

            EditorGUILayout.PropertyField(
                infinite,
                new GUIContent("Infinite Duration (∞)"));

            using (new EditorGUI.DisabledScope(infinite.boolValue))
            {
                EditorGUILayout.PropertyField(
                    duration,
                    new GUIContent("Duration (T)"));
            }

            if (id == StatusEffectId.Regeneration)
            {
                EditorGUILayout.HelpBox(
                    "Canonical0922 Regeneration: N만큼 HP와 Stagger를 둘 다 회복합니다. legacy channel/heal fields는 사용하지 않습니다.",
                    MessageType.None);
            }
        }

        if (HasApplicationTiming)
        {
            SerializedProperty timing =
                serializedObject.FindProperty("ApplicationTiming");
            if (timing != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(timing);
            }
        }

        if (HasForceCharacterStatus)
        {
            SerializedProperty force =
                serializedObject.FindProperty("ForceCharacterStatus");
            if (force != null)
                EditorGUILayout.PropertyField(force);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(AddBodyPartStatusEffect))]
public sealed class AddBodyPartStatusEffectEditor : StatusEffectAuthoringEditorBase
{
    protected override bool HasApplicationTiming => true;
    protected override bool HasForceCharacterStatus => false;
}

[CustomEditor(typeof(ApplyStatusIfConditionEffect))]
public sealed class ApplyStatusIfConditionEffectEditor : StatusEffectAuthoringEditorBase
{
    protected override bool HasApplicationTiming => false;
    protected override bool HasForceCharacterStatus => true;
}
#endif
