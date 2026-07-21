#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillCameraDefinition))]
public sealed class SkillCameraDefinitionEditor :
    Editor
{
    private SerializedProperty shots;
    private SerializedProperty impactPulses;
    private SerializedProperty returnToOverview;
    private SerializedProperty overrideReturnBlend;
    private SerializedProperty returnBlendStyle;
    private SerializedProperty returnBlendTime;

    private bool showShots = true;
    private bool showImpactPulses = true;
    private bool showReturn = true;

    private void OnEnable()
    {
        shots =
            serializedObject.FindProperty(
                "Shots");

        impactPulses =
            serializedObject.FindProperty(
                "ImpactPulses");

        returnToOverview =
            serializedObject.FindProperty(
                "ReturnToOverviewAfterAction");

        overrideReturnBlend =
            serializedObject.FindProperty(
                "OverrideReturnBrainBlend");

        returnBlendStyle =
            serializedObject.FindProperty(
                "ReturnBlendStyle");

        returnBlendTime =
            serializedObject.FindProperty(
                "ReturnBlendTime");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        DrawShotTimeline();
        DrawImpactPulseSection();
        DrawReturnSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.HelpBox(
            "Shot은 카메라의 위치·회전과 구도 전환을 담당합니다.\n" +
            "Impact Zoom Pulse는 현재 Shot을 유지한 채 Lens FOV만 매우 짧게 " +
            "줌인/줌아웃하여 합 굴림과 타격감을 강화합니다.",
            MessageType.Info);
    }

    private void DrawShotTimeline()
    {
        showShots =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                showShots,
                "1. Camera Shot Timeline");

        if (showShots &&
            shots != null)
        {
            EditorGUILayout.PropertyField(
                shots,
                new GUIContent(
                    "Shots",
                    "CameraPoint, Shot Timing, Blend, Duration을 순서대로 설정합니다."),
                true);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawImpactPulseSection()
    {
        showImpactPulses =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                showImpactPulses,
                "2. Impact Zoom Pulses");

        if (!showImpactPulses)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
            return;
        }

        EditorGUILayout.HelpBox(
            "위에서부터 조건을 검사하며 처음 일치한 Pulse 하나가 재생됩니다.\n" +
            "FOV Delta가 음수이면 줌인, 양수이면 줌아웃입니다. " +
            "CameraPoint의 위치·회전은 변경하지 않습니다.",
            MessageType.None);

        DrawPresetButtons();

        if (impactPulses == null)
        {
            EditorGUILayout.HelpBox(
                "ImpactPulses 직렬화 필드를 찾지 못했습니다.",
                MessageType.Error);

            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        for (int i = 0;
             i < impactPulses.arraySize;
             i++)
        {
            SerializedProperty pulse =
                impactPulses.GetArrayElementAtIndex(i);

            DrawImpactPulseCard(
                pulse,
                i);
        }

        EditorGUILayout.Space(2f);

        if (GUILayout.Button(
                "+ 빈 Impact Pulse 추가",
                GUILayout.Height(24f)))
        {
            AddPreset(
                ImpactPreset.Empty);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawPresetButtons()
    {
        EditorGUILayout.LabelField(
            "빠른 프리셋",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("합 굴림"))
        {
            AddPreset(
                ImpactPreset.ClashRoll);
        }

        if (GUILayout.Button("타격"))
        {
            AddPreset(
                ImpactPreset.Hit);
        }

        if (GUILayout.Button("강한 타격"))
        {
            AddPreset(
                ImpactPreset.HeavyHit);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("합 최종 결과"))
        {
            AddPreset(
                ImpactPreset.ClashFinal);
        }

        if (GUILayout.Button("줌아웃 반동"))
        {
            AddPreset(
                ImpactPreset.ZoomOutKick);
        }

        if (GUILayout.Button("액션 시작"))
        {
            AddPreset(
                ImpactPreset.ActionStart);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);
    }

    private void DrawImpactPulseCard(
        SerializedProperty pulse,
        int index)
    {
        if (pulse == null)
            return;

        SerializedProperty displayName =
            pulse.FindPropertyRelative(
                "DisplayName");

        SerializedProperty timing =
            pulse.FindPropertyRelative(
                "Timing");

        string label =
            BuildPulseLabel(
                displayName,
                timing,
                index);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();

        pulse.isExpanded =
            EditorGUILayout.Foldout(
                pulse.isExpanded,
                label,
                true);

        GUI.enabled = index > 0;

        if (GUILayout.Button("▲", GUILayout.Width(26f)))
        {
            impactPulses.MoveArrayElement(
                index,
                index - 1);

            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        GUI.enabled =
            index < impactPulses.arraySize - 1;

        if (GUILayout.Button("▼", GUILayout.Width(26f)))
        {
            impactPulses.MoveArrayElement(
                index,
                index + 1);

            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        GUI.enabled = true;

        if (GUILayout.Button("삭제", GUILayout.Width(42f)))
        {
            impactPulses.DeleteArrayElementAtIndex(
                index);

            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();

        if (pulse.isExpanded)
        {
            EditorGUI.indentLevel++;

            DrawIdentity(
                pulse);

            DrawFilters(
                pulse);

            DrawZoom(
                pulse);

            DrawShake(
                pulse);

            DrawPreview(
                pulse);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawIdentity(
        SerializedProperty pulse)
    {
        DrawRelative(
            pulse,
            "DisplayName",
            "이름");

        DrawRelative(
            pulse,
            "Enabled",
            "사용");

        DrawRelative(
            pulse,
            "Timing",
            "재생 시점");
    }

    private static void DrawFilters(
        SerializedProperty pulse)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "조건 필터",
            EditorStyles.boldLabel);

        DrawRelative(
            pulse,
            "OnlyDuringClash",
            "합에서만");

        DrawRelative(
            pulse,
            "ExcludeOneSided",
            "일방 공격 제외");

        DrawRelative(
            pulse,
            "RequirePositiveDamage",
            "피해 1 이상");

        DrawRelative(
            pulse,
            "RequireCritical",
            "치명타만");

        DrawRelative(
            pulse,
            "RequirePartBreak",
            "부위 파괴만");

        DrawRelative(
            pulse,
            "RequireKill",
            "처치만");

        SerializedProperty useHit =
            pulse.FindPropertyRelative(
                "UseHitIndexFilter");

        if (useHit != null)
        {
            EditorGUILayout.PropertyField(
                useHit,
                new GUIContent(
                    "HitFrame 번호 제한"));

            if (useHit.boolValue)
            {
                DrawRelative(
                    pulse,
                    "HitIndex",
                    "Hit Index");
            }
        }

        SerializedProperty useExchange =
            pulse.FindPropertyRelative(
                "UseExchangeIndexFilter");

        if (useExchange != null)
        {
            EditorGUILayout.PropertyField(
                useExchange,
                new GUIContent(
                    "합 교환 번호 제한"));

            if (useExchange.boolValue)
            {
                DrawRelative(
                    pulse,
                    "ExchangeIndex",
                    "Exchange Index");
            }
        }
    }

    private static void DrawZoom(
        SerializedProperty pulse)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "FOV 줌 펄스",
            EditorStyles.boldLabel);

        DrawRelative(
            pulse,
            "FieldOfViewDelta",
            "FOV Delta");

        DrawRelative(
            pulse,
            "StartDelay",
            "시작 지연");

        DrawRelative(
            pulse,
            "ZoomInDuration",
            "목표 FOV 이동 시간");

        DrawRelative(
            pulse,
            "HoldDuration",
            "정점 유지 시간");

        DrawRelative(
            pulse,
            "ZoomOutDuration",
            "원래 FOV 복귀 시간");

        DrawRelative(
            pulse,
            "ZoomInCurve",
            "줌 진입 Curve");

        DrawRelative(
            pulse,
            "ZoomOutCurve",
            "줌 복귀 Curve");

        DrawRelative(
            pulse,
            "UseUnscaledTime",
            "Unscaled Time 사용");
    }

    private static void DrawShake(
        SerializedProperty pulse)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "Cinemachine Impulse",
            EditorStyles.boldLabel);

        SerializedProperty useShake =
            pulse.FindPropertyRelative(
                "UseShake");

        if (useShake == null)
            return;

        EditorGUILayout.PropertyField(
            useShake,
            new GUIContent(
                "Zoom과 함께 Shake"));

        if (!useShake.boolValue)
            return;

        DrawRelative(
            pulse,
            "ShakeTiming",
            "Shake 시점");

        DrawRelative(
            pulse,
            "Shake",
            "Shake 설정",
            includeChildren: true);
    }

    private void DrawPreview(
        SerializedProperty pulse)
    {
        EditorGUILayout.Space(3f);

        GUI.enabled =
            EditorApplication.isPlaying;

        if (GUILayout.Button(
                "Play Mode에서 이 Pulse 미리보기"))
        {
            serializedObject.ApplyModifiedProperties();

            BattleCameraDirector director =
                Object.FindFirstObjectByType<
                    BattleCameraDirector>();

            SkillCameraDefinition definition =
                target as SkillCameraDefinition;

            int index =
                FindElementIndex(
                    pulse);

            if (director == null ||
                definition?.ImpactPulses == null ||
                index < 0 ||
                index >= definition.ImpactPulses.Count)
            {
                Debug.LogWarning(
                    "[SkillCameraDefinitionEditor] " +
                    "BattleCameraDirector 또는 Pulse를 찾지 못했습니다.");

                return;
            }

            director.StartImpactPulse(
                definition.ImpactPulses[index]);
        }

        GUI.enabled = true;
    }

    private void DrawReturnSection()
    {
        showReturn =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                showReturn,
                "3. Action End / Overview Return");

        if (showReturn)
        {
            if (returnToOverview != null)
            {
                EditorGUILayout.PropertyField(
                    returnToOverview,
                    new GUIContent(
                        "액션 후 Overview 복귀"));
            }

            if (overrideReturnBlend != null)
            {
                EditorGUILayout.PropertyField(
                    overrideReturnBlend,
                    new GUIContent(
                        "복귀 Blend Override"));

                if (overrideReturnBlend.boolValue)
                {
                    if (returnBlendStyle != null)
                    {
                        EditorGUILayout.PropertyField(
                            returnBlendStyle,
                            new GUIContent(
                                "복귀 Blend Style"));
                    }

                    if (returnBlendTime != null)
                    {
                        EditorGUILayout.PropertyField(
                            returnBlendTime,
                            new GUIContent(
                                "복귀 Blend Time"));
                    }
                }
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void AddPreset(
        ImpactPreset preset)
    {
        if (impactPulses == null)
            return;

        serializedObject.Update();

        int index =
            impactPulses.arraySize;

        impactPulses.arraySize =
            index + 1;

        SerializedProperty pulse =
            impactPulses.GetArrayElementAtIndex(
                index);

        ResetPulse(
            pulse);

        ConfigurePreset(
            pulse,
            preset);

        pulse.isExpanded = true;

        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(
            target);
    }

    private static void ResetPulse(
        SerializedProperty pulse)
    {
        SetString(
            pulse,
            "DisplayName",
            "Impact Zoom");

        SetBool(
            pulse,
            "Enabled",
            true);

        SetEnum(
            pulse,
            "Timing",
            SkillCameraImpactTiming.OnHitFrame);

        SetBool(pulse, "OnlyDuringClash", false);
        SetBool(pulse, "ExcludeOneSided", false);
        SetBool(pulse, "RequirePositiveDamage", true);
        SetBool(pulse, "RequireCritical", false);
        SetBool(pulse, "RequirePartBreak", false);
        SetBool(pulse, "RequireKill", false);
        SetBool(pulse, "UseHitIndexFilter", false);
        SetInt(pulse, "HitIndex", 0);
        SetBool(pulse, "UseExchangeIndexFilter", false);
        SetInt(pulse, "ExchangeIndex", 0);

        SetFloat(pulse, "FieldOfViewDelta", -7f);
        SetFloat(pulse, "StartDelay", 0f);
        SetFloat(pulse, "ZoomInDuration", 0.035f);
        SetFloat(pulse, "HoldDuration", 0.015f);
        SetFloat(pulse, "ZoomOutDuration", 0.09f);

        SetCurve(
            pulse,
            "ZoomInCurve",
            AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f));

        SetCurve(
            pulse,
            "ZoomOutCurve",
            AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f));

        SetBool(pulse, "UseUnscaledTime", true);
        SetBool(pulse, "UseShake", false);

        SetEnum(
            pulse,
            "ShakeTiming",
            SkillCameraImpactShakeTiming.OnZoomPeak);

        SerializedProperty shake =
            pulse.FindPropertyRelative(
                "Shake");

        if (shake != null)
        {
            SetBool(shake, "UseImpulse", true);
            SetFloat(shake, "ImpulseForce", 1f);
        }
    }

    private static void ConfigurePreset(
        SerializedProperty pulse,
        ImpactPreset preset)
    {
        switch (preset)
        {
            case ImpactPreset.ClashRoll:
                SetString(pulse, "DisplayName", "합 굴림 충돌");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnClashRoll);
                SetBool(pulse, "OnlyDuringClash", true);
                SetBool(pulse, "RequirePositiveDamage", false);
                SetFloat(pulse, "FieldOfViewDelta", -4f);
                SetFloat(pulse, "ZoomInDuration", 0.035f);
                SetFloat(pulse, "HoldDuration", 0.01f);
                SetFloat(pulse, "ZoomOutDuration", 0.075f);
                break;

            case ImpactPreset.Hit:
                SetString(pulse, "DisplayName", "타격 줌");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnHitFrame);
                SetFloat(pulse, "FieldOfViewDelta", -7f);
                SetFloat(pulse, "ZoomInDuration", 0.025f);
                SetFloat(pulse, "HoldDuration", 0.015f);
                SetFloat(pulse, "ZoomOutDuration", 0.09f);
                SetBool(pulse, "UseShake", true);
                SetImpulse(pulse, 0.8f);
                break;

            case ImpactPreset.HeavyHit:
                SetString(pulse, "DisplayName", "강한 타격 / 파괴");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnHitFrame);
                SetBool(pulse, "RequirePartBreak", true);
                SetFloat(pulse, "FieldOfViewDelta", -11f);
                SetFloat(pulse, "ZoomInDuration", 0.02f);
                SetFloat(pulse, "HoldDuration", 0.025f);
                SetFloat(pulse, "ZoomOutDuration", 0.13f);
                SetBool(pulse, "UseShake", true);
                SetImpulse(pulse, 1.5f);
                break;

            case ImpactPreset.ClashFinal:
                SetString(pulse, "DisplayName", "합 최종 결과");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnClashFinalResult);
                SetBool(pulse, "OnlyDuringClash", true);
                SetBool(pulse, "RequirePositiveDamage", false);
                SetFloat(pulse, "FieldOfViewDelta", -6f);
                SetFloat(pulse, "ZoomInDuration", 0.04f);
                SetFloat(pulse, "HoldDuration", 0.03f);
                SetFloat(pulse, "ZoomOutDuration", 0.12f);
                break;

            case ImpactPreset.ZoomOutKick:
                SetString(pulse, "DisplayName", "줌아웃 반동");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnHitFrame);
                SetFloat(pulse, "FieldOfViewDelta", 5f);
                SetFloat(pulse, "ZoomInDuration", 0.025f);
                SetFloat(pulse, "HoldDuration", 0.01f);
                SetFloat(pulse, "ZoomOutDuration", 0.08f);
                break;

            case ImpactPreset.ActionStart:
                SetString(pulse, "DisplayName", "액션 시작 강조");
                SetEnum(
                    pulse,
                    "Timing",
                    SkillCameraImpactTiming.OnActionStart);
                SetBool(pulse, "RequirePositiveDamage", false);
                SetFloat(pulse, "FieldOfViewDelta", -3f);
                SetFloat(pulse, "ZoomInDuration", 0.06f);
                SetFloat(pulse, "HoldDuration", 0.02f);
                SetFloat(pulse, "ZoomOutDuration", 0.12f);
                break;

            case ImpactPreset.Empty:
            default:
                break;
        }
    }

    private static string BuildPulseLabel(
        SerializedProperty name,
        SerializedProperty timing,
        int index)
    {
        string displayName =
            name != null &&
            !string.IsNullOrWhiteSpace(
                name.stringValue)
                ? name.stringValue
                : $"Pulse #{index + 1}";

        string timingName =
            timing != null
                ? timing.enumDisplayNames[
                    Mathf.Clamp(
                        timing.enumValueIndex,
                        0,
                        timing.enumDisplayNames.Length - 1)]
                : "Unknown";

        return
            $"{index + 1}. {displayName} [{timingName}]";
    }

    private static int FindElementIndex(
        SerializedProperty element)
    {
        if (element == null)
            return -1;

        string path =
            element.propertyPath;

        int open =
            path.LastIndexOf('[');

        int close =
            path.LastIndexOf(']');

        if (open < 0 ||
            close <= open)
        {
            return -1;
        }

        string number =
            path.Substring(
                open + 1,
                close - open - 1);

        return int.TryParse(
            number,
            out int index)
                ? index
                : -1;
    }

    private static void DrawRelative(
        SerializedProperty root,
        string name,
        string label,
        bool includeChildren = false)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(
                name);

        if (property == null)
            return;

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label),
            includeChildren);
    }

    private static void SetBool(
        SerializedProperty root,
        string name,
        bool value)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
            property.boolValue = value;
    }

    private static void SetInt(
        SerializedProperty root,
        string name,
        int value)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
            property.intValue = value;
    }

    private static void SetFloat(
        SerializedProperty root,
        string name,
        float value)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
            property.floatValue = value;
    }

    private static void SetString(
        SerializedProperty root,
        string name,
        string value)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
            property.stringValue = value;
    }

    private static void SetEnum<T>(
        SerializedProperty root,
        string name,
        T value)
        where T : System.Enum
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
        {
            property.enumValueIndex =
                System.Convert.ToInt32(
                    value);
        }
    }

    private static void SetCurve(
        SerializedProperty root,
        string name,
        AnimationCurve value)
    {
        SerializedProperty property =
            root?.FindPropertyRelative(name);

        if (property != null)
            property.animationCurveValue = value;
    }

    private static void SetImpulse(
        SerializedProperty pulse,
        float force)
    {
        SerializedProperty shake =
            pulse?.FindPropertyRelative(
                "Shake");

        if (shake == null)
            return;

        SetBool(shake, "UseImpulse", true);
        SetFloat(shake, "ImpulseForce", force);
    }

    private enum ImpactPreset
    {
        Empty,
        ClashRoll,
        Hit,
        HeavyHit,
        ClashFinal,
        ZoomOutKick,
        ActionStart
    }
}
#endif
