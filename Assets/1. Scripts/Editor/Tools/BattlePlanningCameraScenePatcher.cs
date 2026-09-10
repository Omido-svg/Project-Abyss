#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Planning Overview 자유 카메라 + Tab 모드 표시 UI를 Scene에 한 번에 구성한다.
/// 여러 번 실행해도 중복 GameObject/Component를 만들지 않는 Repair 방식이다.
/// </summary>
public static class BattlePlanningCameraScenePatcher
{
    private const string MenuRoot =
        "Tools/Project Abyss/Battle/Planning Overview Camera/";

    private const string PresentationRootName = "[30] PRESENTATION";
    private const string ControllerObjectName =
        "Battle Planning Camera Controller";
    private const string BoundsObjectName = "Planning Camera Bounds";

    private const string PersistentTopLayerName = "PersistentTopLayer";
    private const string IndicatorObjectName = "PlanningModeIndicator";
    private const string ModeTrackName = "ModeTrack";
    private const string HighlightName = "Highlight";
    private const string CameraLabelName = "CameraModeLabel";
    private const string SlashLabelName = "ModeSlash";
    private const string AssignmentLabelName = "AssignmentModeLabel";
    private const string TabHintName = "TabHint";

    private static readonly Vector3 DefaultBoundsPosition =
        new(0.5f, 4.5f, 0.25f);

    private static readonly Vector3 DefaultBoundsSize =
        new(16f, 7f, 12f);

    private static readonly Vector2 CameraHighlightPosition =
        new(-38f, 0f);
    private static readonly Vector2 AssignmentHighlightPosition =
        new(38f, 0f);

    [MenuItem(MenuRoot + "Apply / Repair Camera + Mode UI", false, 1100)]
    private static void ApplyOrRepair()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Planning Camera",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Planning Camera",
                "활성 Scene을 찾지 못했습니다.",
                "확인");
            return;
        }

        BattleManager battleManager =
            FindSceneComponent<BattleManager>(scene);
        BattleCinemachineRig cameraRig =
            FindSceneComponent<BattleCinemachineRig>(scene);
        BattleScreenModeController screenMode =
            FindSceneComponent<BattleScreenModeController>(scene);

        StringBuilder missing = new();
        AppendMissing(missing, battleManager, nameof(BattleManager));
        AppendMissing(missing, cameraRig, nameof(BattleCinemachineRig));
        AppendMissing(
            missing,
            screenMode,
            nameof(BattleScreenModeController));

        if (cameraRig != null && cameraRig.OverviewCamera == null)
        {
            if (missing.Length > 0)
                missing.Append(", ");
            missing.Append("BattleCinemachineRig.OverviewCamera");
        }

        if (missing.Length > 0)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss - Planning Camera",
                "현재 Scene의 필수 기존 구성요소를 찾지 못해서 패치를 중단했습니다.\n\n" +
                missing,
                "확인");
            Debug.LogError(
                "[Planning Camera Authoring] Missing existing scene dependencies: " +
                missing);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(
            "Apply Project Abyss Planning Camera + Mode UI");

        GameObject presentationRoot =
            ResolvePresentationRoot(scene, cameraRig);

        bool controllerCreated;
        BattlePlanningCameraController controller =
            GetOrCreateSceneComponent<BattlePlanningCameraController>(
                scene,
                presentationRoot.transform,
                ControllerObjectName,
                out controllerCreated);

        bool boundsCreated;
        BattlePlanningCameraBounds bounds =
            GetOrCreateSceneComponent<BattlePlanningCameraBounds>(
                scene,
                presentationRoot.transform,
                BoundsObjectName,
                out boundsCreated);

        EnsureDirectChild(
            controller.transform,
            presentationRoot.transform,
            ControllerObjectName);
        EnsureDirectChild(
            bounds.transform,
            presentationRoot.transform,
            BoundsObjectName);

        if (controllerCreated)
            ResetLocalTransform(controller.transform);

        if (boundsCreated)
        {
            Undo.RecordObject(bounds.transform, "Set Planning Camera Bounds Pose");
            bounds.transform.position = DefaultBoundsPosition;
            bounds.transform.rotation = Quaternion.identity;
            bounds.transform.localScale = Vector3.one;

            Undo.RecordObject(bounds, "Set Planning Camera Bounds Size");
            bounds.Center = Vector3.zero;
            bounds.Size = DefaultBoundsSize;
        }

        CanvasGroup uiCanvasGroup =
            screenMode.GetComponent<CanvasGroup>();
        bool canvasGroupCreated = uiCanvasGroup == null;

        if (uiCanvasGroup == null)
            uiCanvasGroup = Undo.AddComponent<CanvasGroup>(screenMode.gameObject);

        if (canvasGroupCreated)
        {
            Undo.RecordObject(uiCanvasGroup, "Initialize Battle UI CanvasGroup");
            uiCanvasGroup.alpha = 1f;
            uiCanvasGroup.interactable = true;
            uiCanvasGroup.blocksRaycasts = true;
            uiCanvasGroup.ignoreParentGroups = false;
        }

        Undo.RecordObject(controller, "Wire Planning Camera References");
        controller.EditorAssignReferences(
            battleManager,
            cameraRig,
            bounds,
            screenMode,
            uiCanvasGroup);

        Transform persistentTopLayer =
            ResolvePersistentTopLayer(screenMode);

        BattlePlanningModeIndicatorUI indicator =
            EnsureModeIndicator(
                persistentTopLayer,
                controller,
                out bool indicatorCreated);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(bounds);
        EditorUtility.SetDirty(uiCanvasGroup);
        EditorUtility.SetDirty(screenMode);
        EditorUtility.SetDirty(indicator);
        EditorSceneManager.MarkSceneDirty(scene);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = indicator.gameObject;
        EditorGUIUtility.PingObject(indicator.gameObject);

        Debug.Log(
            "[Planning Camera Authoring] Apply/Repair 완료. " +
            $"ControllerCreated={controllerCreated}, " +
            $"BoundsCreated={boundsCreated}, " +
            $"CanvasGroupCreated={canvasGroupCreated}, " +
            $"ModeIndicatorCreated={indicatorCreated}. " +
            "Scene을 저장하세요.",
            indicator);

        EditorUtility.DisplayDialog(
            "Project Abyss - Planning Camera",
            "Planning 자유 카메라 + Tab 모드 UI 패치가 완료되었습니다.\n\n" +
            $"{PresentationRootName}\n" +
            $"  ├─ {ControllerObjectName}\n" +
            $"  └─ {BoundsObjectName}\n\n" +
            "AbyssBattleUI\n" +
            $"  └─ {PersistentTopLayerName}\n" +
            $"      └─ {IndicatorObjectName}  [TAB 전환 / 지정]\n\n" +
            "Tab으로 모드가 바뀔 때 하이라이트가 슬라이드하며 텍스트가 팝됩니다.\n" +
            "Resolution/Cutscene 중에는 자동으로 숨습니다.\n\n" +
            "Scene을 저장한 뒤 Play Mode에서 테스트하세요.",
            "확인");
    }

    // 이전 v2 메뉴명을 눌러도 새 패치 전체가 적용되게 호환 메뉴를 남긴다.
    [MenuItem(MenuRoot + "Apply / Repair Hierarchy + References", false, 1102)]
    private static void ApplyOrRepairLegacyMenu()
    {
        ApplyOrRepair();
    }

    [MenuItem(MenuRoot + "Validate Current Scene", false, 1101)]
    private static void ValidateCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError(
                "[Planning Camera Validate] 활성 Scene을 찾지 못했습니다.");
            return;
        }

        int errors = 0;

        BattleManager battleManager =
            FindSceneComponent<BattleManager>(scene);
        BattleCinemachineRig cameraRig =
            FindSceneComponent<BattleCinemachineRig>(scene);
        BattleScreenModeController screenMode =
            FindSceneComponent<BattleScreenModeController>(scene);
        BattlePlanningCameraController controller =
            FindSceneComponent<BattlePlanningCameraController>(scene);
        BattlePlanningCameraBounds bounds =
            FindSceneComponent<BattlePlanningCameraBounds>(scene);
        BattlePlanningModeIndicatorUI indicator =
            FindSceneComponent<BattlePlanningModeIndicatorUI>(scene);

        errors += Require(battleManager, nameof(BattleManager));
        errors += Require(cameraRig, nameof(BattleCinemachineRig));
        errors += Require(
            screenMode,
            nameof(BattleScreenModeController));
        errors += Require(
            controller,
            nameof(BattlePlanningCameraController));
        errors += Require(
            bounds,
            nameof(BattlePlanningCameraBounds));
        errors += Require(
            indicator,
            nameof(BattlePlanningModeIndicatorUI));

        CanvasGroup canvasGroup =
            screenMode != null
                ? screenMode.GetComponent<CanvasGroup>()
                : null;
        errors += Require(canvasGroup, "AbyssBattleUI CanvasGroup");

        if (cameraRig != null && cameraRig.OverviewCamera == null)
        {
            errors++;
            Debug.LogError(
                "[Planning Camera Validate] Missing: " +
                "BattleCinemachineRig.OverviewCamera",
                cameraRig);
        }

        if (controller != null)
        {
            SerializedObject serialized = new(controller);
            serialized.Update();

            errors += ValidateSerializedReference(
                serialized,
                "battleManager",
                battleManager);
            errors += ValidateSerializedReference(
                serialized,
                "cameraRig",
                cameraRig);
            errors += ValidateSerializedReference(
                serialized,
                "movementBounds",
                bounds);
            errors += ValidateSerializedReference(
                serialized,
                "screenModeController",
                screenMode);
            errors += ValidateSerializedReference(
                serialized,
                "battleUiCanvasGroup",
                canvasGroup);
        }

        if (indicator != null)
        {
            SerializedObject serialized = new(indicator);
            serialized.Update();

            errors += ValidateSerializedReference(
                serialized,
                "controller",
                controller);
            errors += RequireSerializedReference(
                serialized,
                "canvasGroup",
                "PlanningModeIndicator CanvasGroup");
            errors += RequireSerializedReference(
                serialized,
                "highlightRect",
                "PlanningModeIndicator Highlight");
            errors += RequireSerializedReference(
                serialized,
                "highlightImage",
                "PlanningModeIndicator Highlight Image");
            errors += RequireSerializedReference(
                serialized,
                "cameraModeText",
                "전환 Text");
            errors += RequireSerializedReference(
                serialized,
                "assignmentModeText",
                "지정 Text");
            errors += RequireSerializedReference(
                serialized,
                "tabHintText",
                "TAB Text");

            CanvasGroup indicatorCanvas =
                indicator.GetComponent<CanvasGroup>();
            if (indicatorCanvas != null &&
                (indicatorCanvas.interactable || indicatorCanvas.blocksRaycasts))
            {
                errors++;
                Debug.LogError(
                    "[Planning Camera Validate] PlanningModeIndicator는 " +
                    "입력을 가로채면 안 됩니다. interactable=false, blocksRaycasts=false가 필요합니다.",
                    indicator);
            }
        }

        GameObject presentationRoot =
            FindTopLevelObject(scene, PresentationRootName);

        if (controller != null && presentationRoot != null &&
            controller.transform.parent != presentationRoot.transform)
        {
            errors++;
            Debug.LogError(
                $"[Planning Camera Validate] {ControllerObjectName}가 " +
                $"{PresentationRootName} 직속 Child가 아닙니다.",
                controller);
        }

        if (bounds != null && presentationRoot != null &&
            bounds.transform.parent != presentationRoot.transform)
        {
            errors++;
            Debug.LogError(
                $"[Planning Camera Validate] {BoundsObjectName}가 " +
                $"{PresentationRootName} 직속 Child가 아닙니다.",
                bounds);
        }

        if (indicator != null && screenMode != null)
        {
            Transform persistentTop =
                screenMode.transform.Find(PersistentTopLayerName);

            if (persistentTop != null &&
                indicator.transform.parent != persistentTop)
            {
                errors++;
                Debug.LogError(
                    $"[Planning Camera Validate] {IndicatorObjectName}는 " +
                    $"{PersistentTopLayerName} 직속 Child여야 합니다.",
                    indicator);
            }
        }

        if (errors == 0)
        {
            Debug.Log(
                "[Planning Camera Validate] OK - Camera, Bounds, Tab Mode UI, " +
                "serialized references가 모두 정상입니다.",
                indicator != null ? indicator : controller);
        }
        else
        {
            Debug.LogError(
                $"[Planning Camera Validate] FAILED - 오류 {errors}개. " +
                "Apply / Repair Camera + Mode UI 메뉴를 다시 실행하세요.");
        }

        EditorUtility.DisplayDialog(
            "Project Abyss - Planning Camera",
            errors == 0
                ? "검증 완료: Planning Camera + 전환/지정 UI 구성이 정상입니다."
                : $"검증 실패: {errors}개 항목을 확인하세요. Console에 상세 내용이 있습니다.",
            "확인");
    }

    private static BattlePlanningModeIndicatorUI EnsureModeIndicator(
        Transform parent,
        BattlePlanningCameraController controller,
        out bool created)
    {
        created = false;

        RectTransform root =
            GetOrCreateRectChild(parent, IndicatorObjectName, ref created);
        ConfigureRect(
            root,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(232f, 36f),
            new Vector2(0f, -78f));

        Image rootImage = EnsureComponent<Image>(root.gameObject, ref created);
        Undo.RecordObject(rootImage, "Style Planning Mode Indicator");
        rootImage.color = new Color(0.02f, 0.055f, 0.085f, 0.92f);
        rootImage.raycastTarget = false;

        CanvasGroup rootCanvas =
            EnsureComponent<CanvasGroup>(root.gameObject, ref created);
        Undo.RecordObject(rootCanvas, "Configure Planning Mode Indicator CanvasGroup");
        rootCanvas.interactable = false;
        rootCanvas.blocksRaycasts = false;
        rootCanvas.ignoreParentGroups = false;
        if (!Application.isPlaying)
            rootCanvas.alpha = 1f;

        BattlePlanningModeIndicatorUI indicator =
            EnsureComponent<BattlePlanningModeIndicatorUI>(
                root.gameObject,
                ref created);

        UnityEngine.Object referenceFont = FindReferenceFont(parent);

        RectTransform tabHintRect =
            GetOrCreateRectChild(root, TabHintName, ref created);
        ConfigureRect(
            tabHintRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(48f, 30f),
            new Vector2(26f, 0f));

        Component tabHint =
            EnsureTextMeshProUGUI(tabHintRect.gameObject, ref created);
        ConfigureText(
            tabHint,
            referenceFont,
            "TAB",
            12f,
            new Color(0.66f, 0.72f, 0.78f, 1f),
            bold: true);

        RectTransform track =
            GetOrCreateRectChild(root, ModeTrackName, ref created);
        ConfigureRect(
            track,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(174f, 30f),
            new Vector2(-89f, 0f));

        Image trackImage = EnsureComponent<Image>(track.gameObject, ref created);
        Undo.RecordObject(trackImage, "Style Planning Mode Track");
        trackImage.color = new Color(0.035f, 0.095f, 0.14f, 0.98f);
        trackImage.raycastTarget = false;

        RectTransform highlight =
            GetOrCreateRectChild(track, HighlightName, ref created);
        ConfigureRect(
            highlight,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(70f, 24f),
            AssignmentHighlightPosition);

        Image highlightImage =
            EnsureComponent<Image>(highlight.gameObject, ref created);
        Undo.RecordObject(highlightImage, "Style Planning Mode Highlight");
        highlightImage.color = new Color(0.90f, 0.68f, 0.18f, 0.94f);
        highlightImage.raycastTarget = false;

        RectTransform cameraLabelRect =
            GetOrCreateRectChild(track, CameraLabelName, ref created);
        ConfigureRect(
            cameraLabelRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(70f, 28f),
            CameraHighlightPosition);

        Component cameraLabel =
            EnsureTextMeshProUGUI(
                cameraLabelRect.gameObject,
                ref created);
        ConfigureText(
            cameraLabel,
            referenceFont,
            "전환",
            16f,
            new Color(0.68f, 0.72f, 0.76f, 1f),
            bold: true);

        RectTransform slashLabelRect =
            GetOrCreateRectChild(track, SlashLabelName, ref created);
        ConfigureRect(
            slashLabelRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(18f, 28f),
            Vector2.zero);

        Component slashLabel =
            EnsureTextMeshProUGUI(
                slashLabelRect.gameObject,
                ref created);
        ConfigureText(
            slashLabel,
            referenceFont,
            "/",
            13f,
            new Color(0.48f, 0.54f, 0.60f, 1f),
            bold: false);

        RectTransform assignmentLabelRect =
            GetOrCreateRectChild(track, AssignmentLabelName, ref created);
        ConfigureRect(
            assignmentLabelRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(70f, 28f),
            AssignmentHighlightPosition);

        Component assignmentLabel =
            EnsureTextMeshProUGUI(
                assignmentLabelRect.gameObject,
                ref created);
        ConfigureText(
            assignmentLabel,
            referenceFont,
            "지정",
            16f,
            Color.white,
            bold: true);

        // Highlight가 항상 텍스트 뒤에 렌더링되도록 첫 Child로 둔다.
        if (highlight.GetSiblingIndex() != 0)
        {
            Undo.RecordObject(highlight, "Reorder Planning Mode Highlight");
            highlight.SetSiblingIndex(0);
        }

        WireIndicatorReferences(
            indicator,
            controller,
            rootCanvas,
            highlight,
            highlightImage,
            cameraLabel,
            assignmentLabel,
            tabHint);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(rootImage);
        EditorUtility.SetDirty(rootCanvas);
        EditorUtility.SetDirty(indicator);
        EditorUtility.SetDirty(tabHint);
        EditorUtility.SetDirty(trackImage);
        EditorUtility.SetDirty(highlightImage);
        EditorUtility.SetDirty(cameraLabel);
        EditorUtility.SetDirty(slashLabel);
        EditorUtility.SetDirty(assignmentLabel);

        return indicator;
    }

    private static Transform ResolvePersistentTopLayer(
        BattleScreenModeController screenMode)
    {
        Transform existing =
            screenMode.transform.Find(PersistentTopLayerName);
        if (existing != null)
            return existing;

        // 현재 프로젝트에는 이미 존재하지만, 누락된 Scene에서도 Tool이 완주할 수 있게 복구한다.
        GameObject created = new(PersistentTopLayerName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(
            created,
            $"Create {PersistentTopLayerName}");
        Undo.SetTransformParent(
            created.transform,
            screenMode.transform,
            $"Parent {PersistentTopLayerName}");

        RectTransform rect = (RectTransform)created.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Debug.LogWarning(
            $"[Planning Camera Authoring] {PersistentTopLayerName}가 없어 새로 만들었습니다. " +
            "BattleScreenModeController.keepVisibleInAllModes 구성도 확인하세요.",
            created);

        return created.transform;
    }

    /// <summary>
    /// Editor assembly가 Unity.TextMeshPro assembly를 직접 참조하지 않아도
    /// TMP 기반 프로젝트 UI를 생성할 수 있도록 Type/Reflection으로 접근한다.
    /// 이 프로젝트의 Editor asmdef 경계에서 TMPro 직접 using을 넣으면 CS0246이
    /// 발생하므로 authoring tool은 의도적으로 compile-time TMP dependency를 갖지 않는다.
    /// </summary>
    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, throwOnError: false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static Type RequireTextMeshProUGUIType()
    {
        Type type = ResolveType("TMPro.TextMeshProUGUI");
        if (type != null && typeof(Component).IsAssignableFrom(type))
            return type;

        throw new InvalidOperationException(
            "TextMeshProUGUI type을 찾지 못했습니다. " +
            "현재 프로젝트의 TextMeshPro/UGUI 패키지 설치 상태를 확인하세요.");
    }

    private static Component EnsureTextMeshProUGUI(
        GameObject host,
        ref bool anyCreated)
    {
        Type textType = RequireTextMeshProUGUIType();
        Component existing = host.GetComponent(textType);
        if (existing != null)
            return existing;

        anyCreated = true;
        return Undo.AddComponent(host, textType);
    }

    private static UnityEngine.Object FindReferenceFont(Transform searchRoot)
    {
        Type tmpTextType = ResolveType("TMPro.TMP_Text");

        if (searchRoot != null && tmpTextType != null)
        {
            Component[] components =
                searchRoot.GetComponentsInChildren<Component>(true);

            PropertyInfo fontProperty = tmpTextType.GetProperty("font");
            foreach (Component component in components)
            {
                if (component == null || !tmpTextType.IsInstanceOfType(component))
                    continue;

                UnityEngine.Object font =
                    fontProperty?.GetValue(component) as UnityEngine.Object;
                if (font != null)
                    return font;
            }
        }

        Type settingsType = ResolveType("TMPro.TMP_Settings");
        PropertyInfo defaultFontProperty = settingsType?.GetProperty(
            "defaultFontAsset",
            BindingFlags.Public | BindingFlags.Static);

        return defaultFontProperty?.GetValue(null) as UnityEngine.Object;
    }

    private static void WireIndicatorReferences(
        BattlePlanningModeIndicatorUI indicator,
        BattlePlanningCameraController controller,
        CanvasGroup rootCanvas,
        RectTransform highlight,
        Image highlightImage,
        Component cameraLabel,
        Component assignmentLabel,
        Component tabHint)
    {
        Undo.RecordObject(indicator, "Wire Planning Mode Indicator");

        SerializedObject serialized = new(indicator);
        serialized.FindProperty("controller").objectReferenceValue = controller;
        serialized.FindProperty("canvasGroup").objectReferenceValue = rootCanvas;
        serialized.FindProperty("highlightRect").objectReferenceValue = highlight;
        serialized.FindProperty("highlightImage").objectReferenceValue = highlightImage;
        serialized.FindProperty("cameraModeText").objectReferenceValue = cameraLabel;
        serialized.FindProperty("assignmentModeText").objectReferenceValue = assignmentLabel;
        serialized.FindProperty("tabHintText").objectReferenceValue = tabHint;
        serialized.FindProperty("cameraHighlightPosition").vector2Value =
            CameraHighlightPosition;
        serialized.FindProperty("assignmentHighlightPosition").vector2Value =
            AssignmentHighlightPosition;
        serialized.ApplyModifiedProperties();
    }

    private static RectTransform GetOrCreateRectChild(
        Transform parent,
        string objectName,
        ref bool anyCreated)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;
            if (existingRect != null)
                return existingRect;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject created = new(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");

        if (parent != null)
        {
            Undo.SetTransformParent(
                created.transform,
                parent,
                $"Parent {objectName}");
        }

        RectTransform rect = (RectTransform)created.transform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        anyCreated = true;
        return rect;
    }

    private static T EnsureComponent<T>(
        GameObject host,
        ref bool anyCreated)
        where T : Component
    {
        T existing = host.GetComponent<T>();
        if (existing != null)
            return existing;

        anyCreated = true;
        return Undo.AddComponent<T>(host);
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        Undo.RecordObject(rect, $"Layout {rect.name}");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureText(
        Component text,
        UnityEngine.Object font,
        string value,
        float fontSize,
        Color color,
        bool bold)
    {
        if (text == null)
            return;

        Undo.RecordObject(text, $"Style {text.name}");

        Type type = text.GetType();
        SetProperty(type, text, "text", value);

        if (font != null)
            SetProperty(type, text, "font", font);

        SetProperty(type, text, "fontSize", fontSize);
        SetEnumProperty(type, text, "fontStyle", bold ? "Bold" : "Normal");
        SetEnumProperty(type, text, "alignment", "Center");
        SetProperty(type, text, "enableAutoSizing", false);

        if (text is Graphic graphic)
        {
            graphic.color = color;
            graphic.raycastTarget = false;
        }
    }

    private static void SetProperty(
        Type type,
        object target,
        string propertyName,
        object value)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (property != null && property.CanWrite)
            property.SetValue(target, value);
    }

    private static void SetEnumProperty(
        Type type,
        object target,
        string propertyName,
        string enumName)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
            return;

        object enumValue = Enum.Parse(
            property.PropertyType,
            enumName,
            ignoreCase: true);
        property.SetValue(target, enumValue);
    }

    private static GameObject ResolvePresentationRoot(
        Scene scene,
        BattleCinemachineRig cameraRig)
    {
        GameObject existing =
            FindTopLevelObject(scene, PresentationRootName);
        if (existing != null)
            return existing;

        if (cameraRig != null &&
            cameraRig.transform.parent != null &&
            cameraRig.transform.parent.gameObject.scene == scene)
        {
            GameObject candidate = cameraRig.transform.parent.gameObject;
            if (candidate.name == PresentationRootName)
                return candidate;
        }

        GameObject created = new(PresentationRootName);
        SceneManager.MoveGameObjectToScene(created, scene);
        Undo.RegisterCreatedObjectUndo(
            created,
            $"Create {PresentationRootName}");
        return created;
    }

    private static T GetOrCreateSceneComponent<T>(
        Scene scene,
        Transform desiredParent,
        string objectName,
        out bool created)
        where T : Component
    {
        T component = FindSceneComponent<T>(scene);
        if (component != null)
        {
            created = false;
            return component;
        }

        Transform existingChild =
            desiredParent != null
                ? desiredParent.Find(objectName)
                : null;

        GameObject host;
        bool hostCreated;

        if (existingChild != null)
        {
            host = existingChild.gameObject;
            hostCreated = false;
        }
        else
        {
            host = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(host, scene);
            Undo.RegisterCreatedObjectUndo(
                host,
                $"Create {objectName}");

            if (desiredParent != null)
            {
                Undo.SetTransformParent(
                    host.transform,
                    desiredParent,
                    $"Parent {objectName}");
            }

            hostCreated = true;
        }

        T added = host.GetComponent<T>();
        bool componentAdded = added == null;
        if (componentAdded)
            added = Undo.AddComponent<T>(host);

        created = hostCreated || componentAdded;
        return added;
    }

    private static void EnsureDirectChild(
        Transform child,
        Transform desiredParent,
        string undoName)
    {
        if (child == null || desiredParent == null ||
            child.parent == desiredParent)
        {
            return;
        }

        Undo.SetTransformParent(
            child,
            desiredParent,
            $"Move {undoName}");
    }

    private static void ResetLocalTransform(Transform target)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, "Reset Planning Camera Controller Transform");
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindTopLevelObject(
        Scene scene,
        string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        return null;
    }

    private static void AppendMissing(
        StringBuilder builder,
        UnityEngine.Object value,
        string label)
    {
        if (value != null)
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(label);
    }

    private static int Require(UnityEngine.Object value, string label)
    {
        if (value != null)
            return 0;

        Debug.LogError(
            $"[Planning Camera Validate] Missing: {label}");
        return 1;
    }

    private static int ValidateSerializedReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object expected)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogError(
                $"[Planning Camera Validate] Serialized field를 찾지 못했습니다: {propertyName}");
            return 1;
        }

        if (property.objectReferenceValue == expected && expected != null)
            return 0;

        string expectedName =
            expected != null ? expected.name : "NULL";
        string actualName =
            property.objectReferenceValue != null
                ? property.objectReferenceValue.name
                : "NULL";

        Debug.LogError(
            $"[Planning Camera Validate] Reference mismatch: {propertyName} " +
            $"expected={expectedName}, actual={actualName}");
        return 1;
    }

    private static int RequireSerializedReference(
        SerializedObject serialized,
        string propertyName,
        string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError(
                $"[Planning Camera Validate] Serialized field를 찾지 못했습니다: {propertyName}");
            return 1;
        }

        if (property.objectReferenceValue != null)
            return 0;

        Debug.LogError(
            $"[Planning Camera Validate] Missing reference: {label}");
        return 1;
    }
}
#endif
