#if UNITY_EDITOR

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleAnalysisSetupTool
{
    private const string MenuPath =
        "Tools/Project Abyss/Diagnostics/" +
        "Setup Dynamic Analysis & AI Buttons";

    [MenuItem(MenuPath, priority = 2300)]
    public static void SetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Battle Analysis Setup",
                "먼저 저장된 전투 씬을 여세요.",
                "확인");
            return;
        }

        BattleManager battleManager =
            Object.FindFirstObjectByType<BattleManager>(
                FindObjectsInactive.Include);

        Canvas canvas = FindDebugCanvas();

        if (battleManager == null || canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Battle Analysis Setup",
                "BattleManager 또는 DebugUI Canvas를 찾지 못했습니다.",
                "확인");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(
            battleManager.gameObject,
            "Setup Battle Analysis");

        BattleDynamicAnalysisRecorder recorder =
            battleManager.GetComponent<
                BattleDynamicAnalysisRecorder>();

        if (recorder == null)
        {
            recorder = Undo.AddComponent<
                BattleDynamicAnalysisRecorder>(
                    battleManager.gameObject);
        }

        recorder.Configure(battleManager);
        EditorUtility.SetDirty(recorder);

        BattleBatchSimulationRunner runner =
            Object.FindFirstObjectByType<
                BattleBatchSimulationRunner>(
                    FindObjectsInactive.Include);

        if (runner == null)
        {
            GameObject runtimeRoot = new GameObject(
                "BattleAnalysisRuntime");

            Undo.RegisterCreatedObjectUndo(
                runtimeRoot,
                "Create Battle Analysis Runtime");

            runner = runtimeRoot.AddComponent<
                BattleBatchSimulationRunner>();
        }

        runner.Configure(
            winRuns: 100,
            damageRuns: 50,
            maxTurns: 50);

        EditorUtility.SetDirty(runner);

        BattleAnalysisDebugPanel panel =
            CreateOrConfigurePanel(canvas.transform);

        BattleAnalysisPanelToggle panelToggle =
            CreateOrConfigurePanelToggle(
                canvas,
                panel);

        EnsureSceneInBuildSettings(scene.path);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panelToggle.gameObject;

        EditorUtility.DisplayDialog(
            "Battle Analysis Setup",
            "설정 완료\n\n" +
            "1. BattleManager에 JSONL 동적 분석 Recorder 추가\n" +
            "2. DebugUI 왼쪽 아래에 분석 패널과 토글 버튼 추가\n" +
            "3. F8 단축키로 패널 표시/숨김 지원\n" +
            "4. 현재 씬을 Build Settings에 추가\n\n" +
            "패널은 기본적으로 숨김 상태이며, 토글 버튼 또는 F8로 열 수 있습니다.",
            "확인");
    }

    private static Canvas FindDebugCanvas()
    {
        GameObject debugUi = GameObject.Find("DebugUI");

        if (debugUi != null)
        {
            Canvas direct = debugUi.GetComponent<Canvas>();
            if (direct != null)
                return direct;
        }

        foreach (Canvas canvas in
                 Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (canvas != null &&
                canvas.name.Contains("Debug"))
            {
                return canvas;
            }
        }

        return null;
    }

    private static BattleAnalysisDebugPanel
        CreateOrConfigurePanel(Transform parent)
    {
        Transform existing = parent.Find(
            "BattleAnalysisPanel");

        GameObject panelObject;

        if (existing != null)
        {
            panelObject = existing.gameObject;
        }
        else
        {
            panelObject = new GameObject(
                "BattleAnalysisPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            Undo.RegisterCreatedObjectUndo(
                panelObject,
                "Create Battle Analysis Panel");

            panelObject.transform.SetParent(
                parent,
                false);
        }

        RectTransform rect =
            panelObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 72f);
        rect.sizeDelta = new Vector2(390f, 290f);

        CanvasGroup panelCanvasGroup =
            panelObject.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup =
                Undo.AddComponent<CanvasGroup>(
                    panelObject);
        }

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.78f);

        VerticalLayoutGroup layout =
            panelObject.GetComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            panelObject.GetComponent<ContentSizeFitter>();

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText(
            panelObject.transform,
            "Title",
            "BATTLE ANALYSIS (TEMP)",
            22f,
            32f,
            FontStyles.Bold);

        TMP_Text status = CreateText(
            panelObject.transform,
            "StatusText",
            "동적 로그 및 AI 배치 분석 대기 중",
            16f,
            105f,
            FontStyles.Normal);

        Button winRate = CreateButton(
            panelObject.transform,
            "WinRateButton",
            "평균 AI 승률 분석 (100회)");

        Button damage = CreateButton(
            panelObject.transform,
            "DamageButton",
            "평균 AI 피해량 분석 (50회)");

        Button stop = CreateButton(
            panelObject.transform,
            "StopButton",
            "분석 중단");

        BattleAnalysisDebugPanel panel =
            panelObject.GetComponent<
                BattleAnalysisDebugPanel>();

        if (panel == null)
        {
            panel = Undo.AddComponent<
                BattleAnalysisDebugPanel>(
                    panelObject);
        }

        panel.Configure(
            winRate,
            damage,
            stop,
            status);

        EditorUtility.SetDirty(title);
        EditorUtility.SetDirty(status);
        EditorUtility.SetDirty(panel);
        return panel;
    }

    private static BattleAnalysisPanelToggle
        CreateOrConfigurePanelToggle(
            Canvas canvas,
            BattleAnalysisDebugPanel panel)
    {
        if (canvas == null || panel == null)
            return null;

        GameObject panelObject = panel.gameObject;
        panelObject.SetActive(true);

        CanvasGroup panelCanvasGroup =
            panelObject.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup =
                Undo.AddComponent<CanvasGroup>(
                    panelObject);
        }

        Button toggleButton =
            CreateToggleButton(
                canvas.transform,
                out TMP_Text toggleLabel);

        BattleAnalysisPanelToggle controller =
            canvas.GetComponent<BattleAnalysisPanelToggle>();

        if (controller == null)
        {
            controller =
                Undo.AddComponent<BattleAnalysisPanelToggle>(
                    canvas.gameObject);
        }

        controller.Configure(
            panelObject,
            panelCanvasGroup,
            toggleButton,
            toggleLabel,
            hiddenAtStart: true);

        controller.SetPanelVisible(false);

        EditorUtility.SetDirty(panelCanvasGroup);
        EditorUtility.SetDirty(toggleButton);
        EditorUtility.SetDirty(toggleLabel);
        EditorUtility.SetDirty(controller);

        return controller;
    }

    private static Button CreateToggleButton(
        Transform parent,
        out TMP_Text label)
    {
        const string objectName =
            "BattleAnalysisToggleButton";

        Transform existing = parent.Find(objectName);
        GameObject gameObject = existing != null
            ? existing.gameObject
            : new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "Create Battle Analysis Toggle Button");

            gameObject.transform.SetParent(
                parent,
                false);
        }

        RectTransform rect =
            gameObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 20f);
        rect.sizeDelta = new Vector2(220f, 42f);

        Image image = gameObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

        Button button = gameObject.GetComponent<Button>();
        button.targetGraphic = image;

        Transform existingLabel =
            gameObject.transform.Find("Label");

        GameObject labelObject = existingLabel != null
            ? existingLabel.gameObject
            : new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        if (existingLabel == null)
        {
            Undo.RegisterCreatedObjectUndo(
                labelObject,
                "Create Battle Analysis Toggle Label");

            labelObject.transform.SetParent(
                gameObject.transform,
                false);
        }

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "분석 패널 열기 [F8]";
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRect =
            label.GetComponent<RectTransform>();

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);

        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        float preferredHeight,
        FontStyles style)
    {
        Transform existing = parent.Find(name);
        GameObject gameObject = existing != null
            ? existing.gameObject
            : new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "Create " + name);
            gameObject.transform.SetParent(parent, false);
        }

        TextMeshProUGUI label =
            gameObject.GetComponent<TextMeshProUGUI>();

        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.color = Color.white;

        LayoutElement element =
            gameObject.GetComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;

        return label;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string labelText)
    {
        Transform existing = parent.Find(name);
        GameObject gameObject = existing != null
            ? existing.gameObject
            : new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "Create " + name);
            gameObject.transform.SetParent(parent, false);
        }

        Image image = gameObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.24f, 0.30f, 1f);

        Button button = gameObject.GetComponent<Button>();
        button.targetGraphic = image;

        LayoutElement element =
            gameObject.GetComponent<LayoutElement>();
        element.preferredHeight = 42f;

        TMP_Text label = CreateText(
            gameObject.transform,
            "Label",
            labelText,
            16f,
            42f,
            FontStyles.Bold);

        RectTransform labelRect =
            label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);

        return button;
    }

    private static void EnsureSceneInBuildSettings(
        string scenePath)
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == scenePath)
            {
                scene.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(
            new EditorBuildSettingsScene(
                scenePath,
                true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

#endif
