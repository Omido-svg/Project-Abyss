#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleDebugTuner))]
public sealed class BattleDebugTunerEditor : Editor
{
    private SerializedProperty battleManager;
    private SerializedProperty battleUIManager;

    private SerializedProperty overrideMomentum;
    private SerializedProperty momentum;

    private SerializedProperty playerTuning;
    private SerializedProperty enemyTunings;
    private SerializedProperty statusOperations;

    private SerializedProperty applyOnStart;
    private SerializedProperty applyStatusOperationsOnStart;
    private SerializedProperty autoCaptureProfilesWhenEmpty;
    private SerializedProperty liveApply;
    private SerializedProperty liveApplyInterval;
    private SerializedProperty preventApplyDuringResolution;
    private SerializedProperty refreshUIAfterApply;
    private SerializedProperty refreshCharacterViewAfterApply;
    private SerializedProperty logApplySummary;

    private SerializedProperty configureLogger;
    private SerializedProperty enabledLogCategories;
    private SerializedProperty minimumLogLevel;
    private SerializedProperty includeRealtimeInLog;
    private SerializedProperty includeFrameInLog;
    private SerializedProperty captureRecentLogs;
    private SerializedProperty recentLogCapacity;
    private SerializedProperty clearRecentLogsOnStart;

    private bool showReferences;
    private bool showAdditionalSettings;
    private bool showStatusOperations;
    private bool showApplySettings;
    private bool showLoggerSettings;
    private bool showEnemyProfiles = true;

    private GUIStyle sectionStyle;

    private void OnEnable()
    {
        battleManager =
            Find("battleManager");

        battleUIManager =
            Find("battleUIManager");

        overrideMomentum =
            Find("overrideMomentum");

        momentum =
            Find("momentum");

        playerTuning =
            Find("playerTuning");

        enemyTunings =
            Find("enemyTunings");

        statusOperations =
            Find("statusOperations");

        applyOnStart =
            Find("applyOnStart");

        applyStatusOperationsOnStart =
            Find("applyStatusOperationsOnStart");

        autoCaptureProfilesWhenEmpty =
            Find("autoCaptureProfilesWhenEmpty");

        liveApply =
            Find("liveApply");

        liveApplyInterval =
            Find("liveApplyInterval");

        preventApplyDuringResolution =
            Find("preventApplyDuringResolution");

        refreshUIAfterApply =
            Find("refreshUIAfterApply");

        refreshCharacterViewAfterApply =
            Find("refreshCharacterViewAfterApply");

        logApplySummary =
            Find("logApplySummary");

        configureLogger =
            Find("configureLogger");

        enabledLogCategories =
            Find("enabledLogCategories");

        minimumLogLevel =
            Find("minimumLogLevel");

        includeRealtimeInLog =
            Find("includeRealtimeInLog");

        includeFrameInLog =
            Find("includeFrameInLog");

        captureRecentLogs =
            Find("captureRecentLogs");

        recentLogCapacity =
            Find("recentLogCapacity");

        clearRecentLogsOnStart =
            Find("clearRecentLogsOnStart");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EnsureStyles();

        BattleDebugTuner tuner =
            (BattleDebugTuner)target;

        DrawRuntimeState(tuner);
        DrawActionButtons(tuner);

        EditorGUILayout.Space(6f);

        DrawSectionTitle(
            "MAIN BALANCE");

        DrawMomentum();

        EditorGUILayout.Space(4f);

        DrawCharacterProfile(
            playerTuning,
            "PLAYER",
            true,
            -1);

        EditorGUILayout.Space(4f);

        showEnemyProfiles =
            EditorGUILayout.Foldout(
                showEnemyProfiles,
                $"ENEMIES ({enemyTunings.arraySize})",
                true);

        if (showEnemyProfiles)
        {
            EditorGUI.indentLevel++;

            for (int i = 0;
                 i < enemyTunings.arraySize;
                 i++)
            {
                SerializedProperty profile =
                    enemyTunings
                        .GetArrayElementAtIndex(i);

                DrawCharacterProfile(
                    profile,
                    $"ENEMY {i}",
                    false,
                    i);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Add Enemy Profile"))
                {
                    int index =
                        enemyTunings.arraySize;

                    enemyTunings
                        .InsertArrayElementAtIndex(index);

                    SerializedProperty added =
                        enemyTunings
                            .GetArrayElementAtIndex(index);

                    added
                        .FindPropertyRelative("enemyIndex")
                        .intValue =
                            index;
                }

                using (new EditorGUI.DisabledScope(
                           enemyTunings.arraySize == 0))
                {
                    if (GUILayout.Button(
                            "Remove Last"))
                    {
                        enemyTunings
                            .DeleteArrayElementAtIndex(
                                enemyTunings.arraySize - 1);
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8f);

        showAdditionalSettings =
            EditorGUILayout.Foldout(
                showAdditionalSettings,
                "ADDITIONAL SETTINGS",
                true);

        if (showAdditionalSettings)
        {
            EditorGUI.indentLevel++;

            DrawAdditionalSettings();

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRuntimeState(
        BattleDebugTuner tuner)
    {
        MessageType type =
            tuner.IsRuntimeReady
                ? MessageType.Info
                : MessageType.Warning;

        string message =
            tuner.IsRuntimeReady
                ? "Runtime Ready — 설정을 캡처하거나 즉시 적용할 수 있습니다."
                : Application.isPlaying
                    ? "BattleContext 초기화를 기다리는 중입니다."
                    : "Edit Mode에서는 값을 편집할 수 있지만, 캡처와 적용은 Play Mode에서 실행됩니다.";

        EditorGUILayout.HelpBox(
            message,
            type);
    }

    private void DrawActionButtons(
        BattleDebugTuner tuner)
    {
        using (new EditorGUI.DisabledScope(
                   !Application.isPlaying ||
                   !tuner.IsRuntimeReady))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Capture Current",
                        GUILayout.Height(28f)))
                {
                    serializedObject
                        .ApplyModifiedProperties();

                    tuner.CaptureCurrentBattleState();

                    EditorUtility.SetDirty(
                        tuner);

                    serializedObject.Update();
                }

                if (GUILayout.Button(
                        "Apply Balance",
                        GUILayout.Height(28f)))
                {
                    serializedObject
                        .ApplyModifiedProperties();

                    tuner.ApplyBalanceNow();

                    serializedObject.Update();
                }

                if (GUILayout.Button(
                        "Reroll Speed",
                        GUILayout.Height(28f)))
                {
                    serializedObject
                        .ApplyModifiedProperties();

                    tuner.RerollAllSpeeds();

                    serializedObject.Update();
                }
            }
        }
    }

    private void DrawMomentum()
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "GLOBAL MOMENTUM",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                overrideMomentum,
                new GUIContent("Override"));

            if (overrideMomentum.boolValue)
            {
                EditorGUILayout.PropertyField(
                    momentum,
                    new GUIContent("Momentum"));
            }
        }
    }

    private void DrawCharacterProfile(
        SerializedProperty profile,
        string fallbackTitle,
        bool isPlayer,
        int listIndex)
    {
        if (profile == null)
            return;

        SerializedProperty enabled =
            profile.FindPropertyRelative(
                "enabled");

        SerializedProperty characterName =
            profile.FindPropertyRelative(
                "characterName");

        SerializedProperty enemyIndex =
            profile.FindPropertyRelative(
                "enemyIndex");

        SerializedProperty core =
            profile.FindPropertyRelative(
                "core");

        SerializedProperty partSettings =
            profile.FindPropertyRelative(
                "partSettings");

        SerializedProperty additional =
            profile.FindPropertyRelative(
                "additionalSettings");

        string title =
            string.IsNullOrWhiteSpace(
                characterName.stringValue)
                ? fallbackTitle
                : characterName.stringValue;

        profile.isExpanded =
            EditorGUILayout.Foldout(
                profile.isExpanded,
                title,
                true);

        if (!profile.isExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(
                enabled,
                new GUIContent("Enable Profile"));

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    characterName,
                    new GUIContent("Runtime Character"));
            }

            if (!isPlayer)
            {
                EditorGUILayout.PropertyField(
                    enemyIndex,
                    new GUIContent("Enemy Index"));
            }

            EditorGUILayout.Space(3f);

            DrawCoreBalance(
                core);

            EditorGUILayout.Space(3f);

            partSettings.isExpanded =
                EditorGUILayout.Foldout(
                    partSettings.isExpanded,
                    "Part HP / State / Rolled Speed",
                    true);

            if (partSettings.isExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(
                    partSettings,
                    GUIContent.none,
                    true);

                EditorGUI.indentLevel--;
            }

            additional.isExpanded =
                EditorGUILayout.Foldout(
                    additional.isExpanded,
                    "Character Additional Settings",
                    true);

            if (additional.isExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(
                    additional,
                    GUIContent.none,
                    true);

                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawCoreBalance(
        SerializedProperty core)
    {
        if (core == null)
            return;

        EditorGUILayout.LabelField(
            "CORE BALANCE",
            EditorStyles.boldLabel);

        DrawOverrideGroup(
            core,
            "overrideHP",
            "HP / Max HP",
            "currentHP",
            "maxHP",
            "preserveCurrentHpRatioWhenMaxChanges");

        DrawOverrideGroup(
            core,
            "overrideBlock",
            "Block",
            "currentBlock");

        DrawOverrideGroup(
            core,
            "overridePrestige",
            "Prestige",
            "currentPrestige",
            "maxPrestige");

        DrawOverrideGroup(
            core,
            "overrideSpeedRange",
            "Speed Range",
            "minSpeed",
            "maxSpeed");

        DrawOverrideGroup(
            core,
            "overrideOffense",
            "Offense",
            "flatDamageBonus",
            "damageMultiplier");

        DrawOverrideGroup(
            core,
            "overrideDefense",
            "Defense / Penetration",
            "defense",
            "defensePenetrationRate");

        DrawOverrideGroup(
            core,
            "overridePrestigeGain",
            "Prestige Gain",
            "prestigeGainMultiplier");
    }

    private void DrawOverrideGroup(
        SerializedProperty parent,
        string toggleName,
        string label,
        params string[] valueNames)
    {
        SerializedProperty toggle =
            parent.FindPropertyRelative(
                toggleName);

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(
                toggle,
                new GUIContent(label));

            if (!toggle.boolValue)
                return;

            EditorGUI.indentLevel++;

            foreach (string valueName
                     in valueNames)
            {
                SerializedProperty value =
                    parent.FindPropertyRelative(
                        valueName);

                if (value != null)
                {
                    EditorGUILayout.PropertyField(
                        value);
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawAdditionalSettings()
    {
        showStatusOperations =
            EditorGUILayout.Foldout(
                showStatusOperations,
                "One Shot Status Operations",
                true);

        if (showStatusOperations)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                statusOperations,
                true);

            BattleDebugTuner tuner =
                (BattleDebugTuner)target;

            using (new EditorGUI.DisabledScope(
                       !Application.isPlaying ||
                       !tuner.IsRuntimeReady))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            "Apply Status Operations"))
                    {
                        serializedObject
                            .ApplyModifiedProperties();

                        tuner.ApplyStatusOperationsNow();

                        serializedObject.Update();
                    }

                    if (GUILayout.Button(
                            "Remove Configured Types"))
                    {
                        serializedObject
                            .ApplyModifiedProperties();

                        tuner.RemoveConfiguredStatusTypesNow();

                        serializedObject.Update();
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        showApplySettings =
            EditorGUILayout.Foldout(
                showApplySettings,
                "Apply / Refresh",
                true);

        if (showApplySettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                applyOnStart);

            EditorGUILayout.PropertyField(
                applyStatusOperationsOnStart);

            EditorGUILayout.PropertyField(
                autoCaptureProfilesWhenEmpty);

            EditorGUILayout.PropertyField(
                liveApply);

            if (liveApply.boolValue)
            {
                EditorGUILayout.PropertyField(
                    liveApplyInterval);
            }

            EditorGUILayout.PropertyField(
                preventApplyDuringResolution);

            EditorGUILayout.PropertyField(
                refreshUIAfterApply);

            EditorGUILayout.PropertyField(
                refreshCharacterViewAfterApply);

            EditorGUILayout.PropertyField(
                logApplySummary);

            EditorGUI.indentLevel--;
        }

        showLoggerSettings =
            EditorGUILayout.Foldout(
                showLoggerSettings,
                "Logger",
                true);

        if (showLoggerSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                configureLogger);

            if (configureLogger.boolValue)
            {
                EditorGUILayout.PropertyField(
                    enabledLogCategories);

                EditorGUILayout.PropertyField(
                    minimumLogLevel);

                EditorGUILayout.PropertyField(
                    includeRealtimeInLog);

                EditorGUILayout.PropertyField(
                    includeFrameInLog);

                EditorGUILayout.PropertyField(
                    captureRecentLogs);

                if (captureRecentLogs.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        recentLogCapacity);
                }

                EditorGUILayout.PropertyField(
                    clearRecentLogsOnStart);
            }

            EditorGUI.indentLevel--;
        }

        showReferences =
            EditorGUILayout.Foldout(
                showReferences,
                "Scene References",
                true);

        if (showReferences)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                battleManager);

            EditorGUILayout.PropertyField(
                battleUIManager);

            EditorGUI.indentLevel--;
        }
    }

    private void DrawSectionTitle(
        string title)
    {
        EditorGUILayout.LabelField(
            title,
            sectionStyle);
    }

    private SerializedProperty Find(
        string propertyName)
    {
        return serializedObject
            .FindProperty(
                propertyName);
    }

    private void EnsureStyles()
    {
        if (sectionStyle != null)
            return;

        sectionStyle =
            new GUIStyle(
                EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment =
                    TextAnchor.MiddleLeft
            };
    }
}

#endif
