#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleLiveDebugInspector))]
public sealed class BattleLiveDebugInspectorEditor : Editor
{
    private bool showScenarioSettings;
    private bool showPerformanceSettings;
    private bool showAnalysisSettings = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BattleLiveDebugInspector hub =
            (BattleLiveDebugInspector)target;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "PROJECT ABYSS / LIVE DEBUG CONTROLS",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Game View 디버그 패널을 대체하는 Inspector 전용 제어판입니다. " +
            "Play Mode에서도 이 오브젝트를 선택한 채 실시간으로 조절할 수 있습니다.",
            MessageType.Info);

        DrawReferenceStatus(hub);
        EditorGUILayout.Space(6f);

        DrawScenarioSection(hub.TestScenarioSwitcher);
        EditorGUILayout.Space(8f);
        DrawPerformanceSection(hub.PerformanceDiagnostics);
        EditorGUILayout.Space(8f);
        DrawAnalysisSection(hub.BatchSimulationRunner);

        serializedObject.ApplyModifiedProperties();
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private void DrawReferenceStatus(BattleLiveDebugInspector hub)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Test Encounter",
                hub.TestScenarioSwitcher,
                typeof(BattleTestScenarioSwitcher),
                true);
            EditorGUILayout.ObjectField(
                "Performance",
                hub.PerformanceDiagnostics,
                typeof(BattlePerformanceDiagnostics),
                true);
            EditorGUILayout.ObjectField(
                "Dynamic Analysis",
                hub.BatchSimulationRunner,
                typeof(BattleBatchSimulationRunner),
                true);
        }
    }

    private void DrawScenarioSection(BattleTestScenarioSwitcher switcher)
    {
        EditorGUILayout.LabelField(
            "1. 테스트 전투",
            EditorStyles.boldLabel);

        if (switcher == null)
        {
            EditorGUILayout.HelpBox(
                "BattleTestScenarioSwitcher 참조가 없습니다. " +
                "Hierarchy Patch Tool을 다시 실행하세요.",
                MessageType.Error);
            return;
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField(
                "현재 플레이어",
                switcher.SelectedPlayer.ToString());
            EditorGUILayout.LabelField(
                "현재 전투",
                switcher.SelectedEncounter.ToString());
            EditorGUILayout.LabelField(
                "현재 감정",
                switcher.SelectedEmotion.ToString());

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("플레이어");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Olaf"))
                    switcher.SelectOlaf();
                if (GUILayout.Button("Yujin"))
                    switcher.SelectYujin();
                if (GUILayout.Button("Hifumi"))
                    switcher.SelectHifumi();
            }

            EditorGUILayout.LabelField("전투 구성");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("일반 3"))
                    switcher.SelectNormalBattle();
                if (GUILayout.Button("정예1 + 일반2"))
                    switcher.SelectMixedBattle();
                if (GUILayout.Button("보스 1"))
                    switcher.SelectBossBattle();
            }

            EmotionType emotion =
                (EmotionType)EditorGUILayout.EnumPopup(
                    "감정",
                    switcher.SelectedEmotion);

            if (emotion != switcher.SelectedEmotion)
                switcher.SelectEmotion(emotion);

            EditorGUILayout.Space(2f);
            if (GUILayout.Button("저장된 선택 Reset"))
                switcher.ResetSavedSelection();

            EditorGUILayout.HelpBox(
                "Play Mode에서 플레이어/전투/감정을 바꾸면 기존 안전 정책대로 " +
                "현재 Scene을 다시 불러 Roster를 깨끗하게 재구성합니다.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Play Mode 전에는 아래 기본값을 수정하고, Play Mode에서 위 버튼으로 실시간 전환하세요.",
                MessageType.None);
        }

        showScenarioSettings =
            EditorGUILayout.Foldout(
                showScenarioSettings,
                "테스트 전투 설정",
                true);

        if (showScenarioSettings)
        {
            SerializedObject so = new(switcher);
            so.Update();
            DrawPropertyIfPresent(so, "defaultPlayer");
            DrawPropertyIfPresent(so, "defaultEncounter");
            DrawPropertyIfPresent(so, "rememberSelection");
            DrawPropertyIfPresent(so, "configureEmotionForTest");
            DrawPropertyIfPresent(so, "defaultEmotion");
            DrawPropertyIfPresent(so, "emotionAugmentCatalog");
            so.ApplyModifiedProperties();
        }
    }

    private void DrawPerformanceSection(BattlePerformanceDiagnostics diagnostics)
    {
        EditorGUILayout.LabelField(
            "2. Performance",
            EditorStyles.boldLabel);

        if (diagnostics == null)
        {
            EditorGUILayout.HelpBox(
                "BattlePerformanceDiagnostics 참조가 없습니다. " +
                "Hierarchy Patch Tool을 다시 실행하세요.",
                MessageType.Error);
            return;
        }

        if (Application.isPlaying)
        {
            string live = diagnostics.LiveSummary;
            EditorGUILayout.HelpBox(
                string.IsNullOrWhiteSpace(live)
                    ? "첫 Performance Sample을 기다리는 중..."
                    : live,
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        diagnostics.CaptureEnabled
                            ? "Capture: ON"
                            : "Capture: OFF"))
                {
                    diagnostics.ToggleCapture();
                }

                if (GUILayout.Button(
                        $"Export CSV ({diagnostics.CapturedSampleCount})"))
                {
                    diagnostics.ExportCsv();
                }
            }

            EditorGUILayout.LabelField("A/B Isolation");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        diagnostics.OutlinesSuppressed
                            ? "Outline: OFF"
                            : "Outline: ON"))
                {
                    diagnostics.ToggleOutlineIsolation();
                }

                if (GUILayout.Button(
                        diagnostics.DebugUiSuppressed
                            ? "Debug UI: OFF"
                            : "Debug UI: ON"))
                {
                    diagnostics.ToggleDebugUiIsolation();
                }

                if (GUILayout.Button(
                        diagnostics.WorldCanvasesSuppressed
                            ? "World Canvas: OFF"
                            : "World Canvas: ON"))
                {
                    diagnostics.ToggleWorldCanvasIsolation();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "실시간 FPS / CPU / GPU / GC / DrawCall 값은 Play Mode에서 표시됩니다.",
                MessageType.None);
        }

        showPerformanceSettings =
            EditorGUILayout.Foldout(
                showPerformanceSettings,
                "Performance 설정",
                true);

        if (showPerformanceSettings)
        {
            SerializedObject so = new(diagnostics);
            so.Update();
            DrawPropertyIfPresent(so, "battleManager");
            DrawPropertyIfPresent(so, "cameraDirector");
            DrawPropertyIfPresent(so, "targetCamera");
            DrawPropertyIfPresent(so, "sampleInterval");
            DrawPropertyIfPresent(so, "maxCapturedSamples");
            DrawPropertyIfPresent(so, "lowFpsThreshold");
            DrawPropertyIfPresent(so, "lowFpsWarningDelay");
            DrawPropertyIfPresent(so, "lowFpsWarningWarmupFrames");
            DrawPropertyIfPresent(so, "maximumTrustedFrameDeltaSeconds");
            DrawPropertyIfPresent(so, "ignoreWarningsDuringBatchSimulation");
            DrawPropertyIfPresent(so, "ignoreWarningsWhenApplicationUnfocused");
            DrawPropertyIfPresent(so, "ignoreWarningsWhileTimeScaleIsZero");
            so.ApplyModifiedProperties();
        }
    }

    private void DrawAnalysisSection(BattleBatchSimulationRunner runner)
    {
        EditorGUILayout.LabelField(
            "3. 동적 분석 / Batch Simulation",
            EditorStyles.boldLabel);

        if (runner == null)
        {
            EditorGUILayout.HelpBox(
                "BattleBatchSimulationRunner 참조가 없습니다. " +
                "Hierarchy Patch Tool을 다시 실행하세요.",
                MessageType.Error);
            return;
        }

        showAnalysisSettings =
            EditorGUILayout.Foldout(
                showAnalysisSettings,
                "분석 설정",
                true);

        if (showAnalysisSettings)
        {
            SerializedObject so = new(runner);
            so.Update();
            DrawPropertyIfPresent(so, "winRateRunCount");
            DrawPropertyIfPresent(so, "damageRunCount");
            DrawPropertyIfPresent(so, "maximumTurnsPerBattle");
            DrawPropertyIfPresent(so, "suppressInfoLogs");
            DrawPropertyIfPresent(so, "reloadCleanSceneAfterBatch");
            DrawPropertyIfPresent(so, "recordDetailedTraceDuringBatch");
            DrawPropertyIfPresent(so, "writeProgressAfterEveryRun");
            DrawPropertyIfPresent(so, "openOutputFolderWhenComplete");
            DrawPropertyIfPresent(so, "sceneLoadTimeoutSeconds");
            DrawPropertyIfPresent(so, "battleReadyTimeoutSeconds");
            DrawPropertyIfPresent(so, "noProgressTimeoutSeconds");
            so.ApplyModifiedProperties();
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(runner.IsRunning))
                {
                    if (GUILayout.Button("승률 분석 실행"))
                        runner.RunWinRateAnalysis();

                    if (GUILayout.Button("피해량 분석 실행"))
                        runner.RunDamageAnalysis();
                }

                using (new EditorGUI.DisabledScope(!runner.IsRunning))
                {
                    if (GUILayout.Button("STOP", GUILayout.Width(70f)))
                        runner.StopAnalysis();
                }
            }

            if (GUILayout.Button("분석 출력 폴더 열기"))
                runner.OpenOutputFolder();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Batch 분석 실행 버튼은 Play Mode에서 활성화됩니다.",
                MessageType.None);
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "상태",
            runner.IsRunning ? "RUNNING" : "IDLE");

        if (runner.LastSummary != null)
        {
            EditorGUILayout.HelpBox(
                runner.LastSummary.ToDisplayString(),
                MessageType.None);

            if (!string.IsNullOrWhiteSpace(runner.LastOutputDirectory))
                EditorGUILayout.SelectableLabel(
                    runner.LastOutputDirectory,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
        else
        {
            EditorGUILayout.HelpBox(
                "아직 실행된 Batch 분석이 없습니다.\n출력 루트: " +
                runner.OutputRootDirectory,
                MessageType.None);
        }
    }

    private static void DrawPropertyIfPresent(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property != null)
            EditorGUILayout.PropertyField(property, true);
    }
}
#endif
