#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game View에 존재하던 테스트 전투 / Performance / 동적 분석 UI를 제거하고
/// [90] DEBUG & ANALYSIS 아래의 Inspector 전용 Live Debug Controls로 이관한다.
/// 여러 번 실행해도 중복 오브젝트를 만들지 않는 Repair 방식이다.
/// </summary>
public static class BattleDebugInspectorScenePatcher
{
    private const string MenuRoot =
        "Tools/Project Abyss/Diagnostics/Inspector Debug Controls/";

    private const string DebugRootName = "[90] DEBUG & ANALYSIS";
    private const string RunnerObjectName = "Battle Analysis Runtime";
    private const string PerformanceObjectName = "Battle Performance Diagnostics";

    private static readonly string[] LegacyAnalysisUiObjectNames =
    {
        "BattleAnalysisPanel",
        "BattleAnalysisToggleButton",
        "BattleAnalysisPersistentOverlay"
    };

    [MenuItem(MenuRoot + "Apply / Repair + Remove GameView Debug UI", false, 1200)]
    private static void ApplyOrRepair()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Inspector Debug Controls",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Inspector Debug Controls",
                "활성 Scene을 찾지 못했습니다.",
                "확인");
            return;
        }

        BattleManager battleManager = FindSceneComponent<BattleManager>(scene);
        BattleTestScenarioSwitcher switcher =
            FindSceneComponent<BattleTestScenarioSwitcher>(scene);
        BattleCameraDirector cameraDirector =
            FindSceneComponent<BattleCameraDirector>(scene);
        Camera mainCamera = FindSceneComponent<Camera>(scene);

        if (switcher == null)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Inspector Debug Controls",
                "BattleTestScenarioSwitcher를 찾지 못했습니다.\n" +
                "먼저 Test Encounter Scene Setup을 복구한 뒤 다시 실행하세요.",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(
            "Convert Project Abyss Runtime Debug UI To Inspector");

        GameObject debugRoot = ResolveDebugRoot(scene);

        BattleBatchSimulationRunner runner =
            FindSceneComponent<BattleBatchSimulationRunner>(scene);
        if (runner == null)
        {
            GameObject runnerObject = new(RunnerObjectName);
            SceneManager.MoveGameObjectToScene(runnerObject, scene);
            Undo.RegisterCreatedObjectUndo(
                runnerObject,
                "Create Battle Analysis Runtime");
            Undo.SetTransformParent(
                runnerObject.transform,
                debugRoot.transform,
                "Parent Battle Analysis Runtime");
            ResetLocalTransform(runnerObject.transform);
            runner = Undo.AddComponent<BattleBatchSimulationRunner>(runnerObject);
        }
        else
        {
            EnsureDirectChild(runner.transform, debugRoot.transform);
        }

        BattlePerformanceDiagnostics performance =
            FindSceneComponent<BattlePerformanceDiagnostics>(scene);
        if (performance == null)
        {
            GameObject performanceObject = new(PerformanceObjectName);
            SceneManager.MoveGameObjectToScene(performanceObject, scene);
            Undo.RegisterCreatedObjectUndo(
                performanceObject,
                "Create Battle Performance Diagnostics");
            Undo.SetTransformParent(
                performanceObject.transform,
                debugRoot.transform,
                "Parent Battle Performance Diagnostics");
            ResetLocalTransform(performanceObject.transform);
            performance =
                Undo.AddComponent<BattlePerformanceDiagnostics>(performanceObject);
        }
        else
        {
            EnsureDirectChild(performance.transform, debugRoot.transform);
        }

        ConfigureScenarioForInspectorOnly(switcher);
        ConfigurePerformanceForInspectorOnly(
            performance,
            battleManager,
            cameraDirector,
            mainCamera);

        int removedToggleComponents = RemoveLegacyAnalysisToggleComponents(scene);
        int removedPanelComponents = RemoveLegacyAnalysisPanelComponents(scene);
        int removedLegacyObjects = RemoveLegacyAnalysisUiObjects(scene);

        // Hub는 Battle Analysis Runtime과 같은 GameObject에 둔다.
        // Runner가 DontDestroyOnLoad로 살아남으므로 Batch 중 Scene reload가 반복돼도
        // Inspector 제어판과 Selection이 함께 유지된다.
        BattleLiveDebugInspector hub =
            runner.GetComponent<BattleLiveDebugInspector>();

        if (hub == null)
            hub = Undo.AddComponent<BattleLiveDebugInspector>(runner.gameObject);

        Undo.RecordObject(hub, "Wire Battle Live Debug Inspector");
        hub.AssignReferences(switcher, performance, runner);

        EditorUtility.SetDirty(switcher);
        EditorUtility.SetDirty(performance);
        EditorUtility.SetDirty(runner);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);

        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = hub.gameObject;
        EditorGUIUtility.PingObject(hub.gameObject);

        Debug.Log(
            "[Inspector Debug Controls] Apply/Repair 완료. " +
            $"RemovedToggleComponents={removedToggleComponents}, " +
            $"RemovedPanelComponents={removedPanelComponents}, " +
            $"RemovedLegacyObjects={removedLegacyObjects}. " +
            "Game View Debug UI는 비활성화되고 [90] DEBUG & ANALYSIS의 " +
            "Battle Live Debug Inspector에서 제어합니다.",
            hub);

        EditorUtility.DisplayDialog(
            "Project Abyss - Inspector Debug Controls",
            "변환 완료.\n\n" +
            "Game View에서 제거/비활성화:\n" +
            "- Project Abyss 테스트 전투 IMGUI Panel\n" +
            "- Show Performance / Performance Overlay\n" +
            "- 분석 열기(F8) / 동적 분석 Runtime Canvas\n\n" +
            "새 제어 위치:\n" +
            $"{DebugRootName}/{RunnerObjectName}의 Battle Live Debug Inspector 컴포넌트\n\n" +
            "현재 해당 오브젝트를 선택해 두었습니다. Scene을 저장하세요.",
            "확인");
    }

    [MenuItem(MenuRoot + "Validate Current Scene", false, 1201)]
    private static void ValidateCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Inspector Debug Controls Validate] 활성 Scene이 없습니다.");
            return;
        }

        int errors = 0;
        StringBuilder report = new();

        BattleLiveDebugInspector hub =
            FindSceneComponent<BattleLiveDebugInspector>(scene);
        BattleTestScenarioSwitcher switcher =
            FindSceneComponent<BattleTestScenarioSwitcher>(scene);
        BattlePerformanceDiagnostics performance =
            FindSceneComponent<BattlePerformanceDiagnostics>(scene);
        BattleBatchSimulationRunner runner =
            FindSceneComponent<BattleBatchSimulationRunner>(scene);

        errors += Require(hub, nameof(BattleLiveDebugInspector), report);
        errors += Require(switcher, nameof(BattleTestScenarioSwitcher), report);
        errors += Require(performance, nameof(BattlePerformanceDiagnostics), report);
        errors += Require(runner, nameof(BattleBatchSimulationRunner), report);

        if (hub != null)
        {
            if (runner != null && hub.gameObject != runner.gameObject)
            {
                errors++;
                report.AppendLine("- BattleLiveDebugInspector가 Battle Analysis Runtime GameObject에 있지 않음");
            }

            if (hub.TestScenarioSwitcher != switcher)
            {
                errors++;
                report.AppendLine("- Hub.TestScenarioSwitcher 참조 불일치");
            }
            if (hub.PerformanceDiagnostics != performance)
            {
                errors++;
                report.AppendLine("- Hub.PerformanceDiagnostics 참조 불일치");
            }
            if (hub.BatchSimulationRunner != runner)
            {
                errors++;
                report.AppendLine("- Hub.BatchSimulationRunner 참조 불일치");
            }
        }

        if (switcher != null)
        {
            SerializedObject so = new(switcher);
            SerializedProperty p = so.FindProperty("showRuntimePanel");
            if (p != null && p.boolValue)
            {
                errors++;
                report.AppendLine("- BattleTestScenarioSwitcher.showRuntimePanel=true");
            }
        }

        if (performance != null)
        {
            SerializedObject so = new(performance);
            errors += RequireFalse(so, "startOverlayVisible", report);
            errors += RequireFalse(so, "showOverlay", report);
            errors += RequireFalse(so, "showPersistentToggleButton", report);
        }

        BattleAnalysisPanelToggle[] toggles =
            FindSceneComponents<BattleAnalysisPanelToggle>(scene);
        if (toggles.Length > 0)
        {
            errors += toggles.Length;
            report.AppendLine(
                $"- BattleAnalysisPanelToggle가 Scene에 {toggles.Length}개 남아 있음");
        }

        BattleAnalysisDebugPanel[] panels =
            FindSceneComponents<BattleAnalysisDebugPanel>(scene);
        if (panels.Length > 0)
        {
            errors += panels.Length;
            report.AppendLine(
                $"- BattleAnalysisDebugPanel이 Scene에 {panels.Length}개 남아 있음");
        }

        if (errors == 0)
        {
            Debug.Log(
                "[Inspector Debug Controls Validate] OK - " +
                "세 가지 Debug UI가 Inspector 제어 방식으로 정상 이관되었습니다.",
                hub);
        }
        else
        {
            Debug.LogError(
                "[Inspector Debug Controls Validate] FAILED\n" + report);
        }

        EditorUtility.DisplayDialog(
            "Project Abyss - Inspector Debug Controls",
            errors == 0
                ? "검증 완료: Game View Debug UI가 제거되고 Inspector 제어가 정상 구성되어 있습니다."
                : $"검증 실패: {errors}개 문제. Console을 확인한 뒤 Apply / Repair를 다시 실행하세요.",
            "확인");
    }

    private static void ConfigureScenarioForInspectorOnly(
        BattleTestScenarioSwitcher switcher)
    {
        Undo.RecordObject(switcher, "Disable Test Encounter Runtime Panel");
        SerializedObject so = new(switcher);
        so.Update();
        SetBool(so, "showRuntimePanel", false);
        so.ApplyModifiedProperties();
    }

    private static void ConfigurePerformanceForInspectorOnly(
        BattlePerformanceDiagnostics performance,
        BattleManager battleManager,
        BattleCameraDirector cameraDirector,
        Camera targetCamera)
    {
        Undo.RecordObject(performance, "Disable Performance Runtime Overlay");
        SerializedObject so = new(performance);
        so.Update();
        SetBool(so, "startOverlayVisible", false);
        SetBool(so, "showOverlay", false);
        SetBool(so, "showPersistentToggleButton", false);
        SetObject(so, "battleManager", battleManager);
        SetObject(so, "cameraDirector", cameraDirector);
        SetObject(so, "targetCamera", targetCamera);
        so.ApplyModifiedProperties();
    }

    private static int RemoveLegacyAnalysisToggleComponents(Scene scene)
    {
        int removed = 0;
        foreach (BattleAnalysisPanelToggle toggle in
                 FindSceneComponents<BattleAnalysisPanelToggle>(scene))
        {
            if (toggle == null)
                continue;

            Undo.DestroyObjectImmediate(toggle);
            removed++;
        }
        return removed;
    }

    private static int RemoveLegacyAnalysisPanelComponents(Scene scene)
    {
        int removed = 0;
        foreach (BattleAnalysisDebugPanel panel in
                 FindSceneComponents<BattleAnalysisDebugPanel>(scene))
        {
            if (panel == null)
                continue;

            Undo.DestroyObjectImmediate(panel);
            removed++;
        }
        return removed;
    }

    private static int RemoveLegacyAnalysisUiObjects(Scene scene)
    {
        int removed = 0;
        HashSet<GameObject> targets = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null)
                    continue;

                foreach (string legacyName in LegacyAnalysisUiObjectNames)
                {
                    if (transform.name == legacyName)
                    {
                        targets.Add(transform.gameObject);
                        break;
                    }
                }
            }
        }

        foreach (GameObject target in targets)
        {
            if (target == null)
                continue;

            Undo.DestroyObjectImmediate(target);
            removed++;
        }

        return removed;
    }

    private static GameObject ResolveDebugRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == DebugRootName)
                return root;
        }

        GameObject created = new(DebugRootName);
        SceneManager.MoveGameObjectToScene(created, scene);
        Undo.RegisterCreatedObjectUndo(created, "Create Debug & Analysis Root");
        return created;
    }

    private static void EnsureDirectChild(Transform child, Transform parent)
    {
        if (child == null || parent == null || child.parent == parent)
            return;

        Undo.SetTransformParent(
            child,
            parent,
            $"Move {child.name} under {DebugRootName}");
    }

    private static void ResetLocalTransform(Transform target)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, "Reset Debug Object Transform");
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] found = FindSceneComponents<T>(scene);
        return found.Length > 0 ? found[0] : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new();
        T[] all = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T component in all)
        {
            if (component != null && component.gameObject.scene == scene)
                result.Add(component);
        }

        return result.ToArray();
    }

    private static int Require(
        Object value,
        string label,
        StringBuilder report)
    {
        if (value != null)
            return 0;

        report.AppendLine("- Missing: " + label);
        return 1;
    }

    private static int RequireFalse(
        SerializedObject so,
        string propertyName,
        StringBuilder report)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.boolValue)
            return 0;

        report.AppendLine($"- {so.targetObject.name}.{propertyName}=true");
        return 1;
    }

    private static void SetBool(
        SerializedObject so,
        string propertyName,
        bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetObject(
        SerializedObject so,
        string propertyName,
        Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && value != null)
            property.objectReferenceValue = value;
    }
}
#endif
