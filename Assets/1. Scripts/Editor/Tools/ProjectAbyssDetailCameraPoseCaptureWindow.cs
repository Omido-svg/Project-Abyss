#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public sealed class ProjectAbyssDetailCameraPoseCaptureWindow : EditorWindow
{
    private const string WindowMenuPath =
        "Tools/Project Abyss/Camera Authoring/Detail Camera Pose Capture";

    private const string SessionKey =
        "ProjectAbyss.DetailCameraPoseCapture.v413";

    private const string AutoApplyKey =
        "ProjectAbyss.DetailCameraPoseCapture.AutoApply.v413";

    [Serializable]
    private sealed class CaptureState
    {
        public string TargetGlobalObjectId;
        public string TargetScenePath;
        public string TargetHierarchyPath;

        public Vector3 CapturedWorldPosition;
        public Quaternion CapturedWorldRotation;

        public string CapturedCameraName;
        public float CapturedFieldOfView;
        public bool CapturedOrthographic;
        public float CapturedOrthographicSize;

        public bool HasRegisteredTarget;
        public bool HasPendingPose;
    }

    private CaptureState state;
    private Transform displayedTarget;
    private Vector2 scrollPosition;

    static ProjectAbyssDetailCameraPoseCaptureWindow()
    {
        EditorApplication.playModeStateChanged -=
            HandlePlayModeStateChanged;

        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
    }

    [MenuItem(WindowMenuPath)]
    private static void OpenWindow()
    {
        ProjectAbyssDetailCameraPoseCaptureWindow window =
            GetWindow<ProjectAbyssDetailCameraPoseCaptureWindow>();

        window.titleContent =
            new GUIContent("Detail Camera Capture");

        window.minSize =
            new Vector2(460f, 560f);

        window.Show();
    }

    private void OnEnable()
    {
        LoadState();
        ResolveDisplayedTarget();
    }

    private void OnFocus()
    {
        LoadState();
        ResolveDisplayedTarget();
        Repaint();
    }

    private void OnGUI()
    {
        LoadStateIfNeeded();

        scrollPosition =
            EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawTargetSection();
        DrawCaptureSection();
        DrawStatusSection();
        DrawExplanation();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField(
            "Project Abyss DetailCameraPoint 캡처",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Scene View의 Ctrl + Shift + F를 사용하지 않고, " +
            "Cinemachine이 최종 계산한 실제 Game View/Main Camera의 " +
            "월드 위치와 회전을 DetailCameraPoint에 저장합니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField(
            "1. 저장 대상 등록",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "등록된 DetailCameraPoint",
                displayedTarget,
                typeof(Transform),
                true);
        }

        if (state.HasRegisteredTarget)
        {
            EditorGUILayout.LabelField(
                "Scene",
                string.IsNullOrWhiteSpace(state.TargetScenePath)
                    ? "(알 수 없음)"
                    : state.TargetScenePath);

            EditorGUILayout.LabelField(
                "Hierarchy",
                string.IsNullOrWhiteSpace(state.TargetHierarchyPath)
                    ? "(알 수 없음)"
                    : state.TargetHierarchyPath);
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button(
                    "현재 선택한 DetailCameraPoint 등록",
                    GUILayout.Height(30f)))
            {
                RegisterSelectedTarget();
            }
        }

        EditorGUILayout.Space(10f);
    }

    private void DrawCaptureSection()
    {
        EditorGUILayout.LabelField(
            "2. Game View 구도 캡처",
            EditorStyles.boldLabel);

        bool autoApply =
            EditorPrefs.GetBool(
                AutoApplyKey,
                true);

        bool nextAutoApply =
            EditorGUILayout.ToggleLeft(
                "Play Mode 종료 후 자동 적용",
                autoApply);

        if (nextAutoApply != autoApply)
        {
            EditorPrefs.SetBool(
                AutoApplyKey,
                nextAutoApply);
        }

        if (!EditorApplication.isPlaying)
        {
            using (new EditorGUI.DisabledScope(!state.HasRegisteredTarget))
            {
                if (GUILayout.Button(
                        "Play Mode 시작",
                        GUILayout.Height(32f)))
                {
                    EditorApplication.isPlaying =
                        true;
                }
            }

            using (new EditorGUI.DisabledScope(!state.HasPendingPose))
            {
                if (GUILayout.Button(
                        "보류 중인 캡처를 지금 적용",
                        GUILayout.Height(28f)))
                {
                    ApplyPendingPose(
                        showDialog: true);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Game View에서 원하는 구도가 나올 때까지 " +
                "CM_Overview 또는 Cinemachine 설정을 조정한 뒤 " +
                "아래 버튼을 누르세요. " +
                "CM_Overview의 Play Mode 변경은 종료 시 원래대로 돌아가고, " +
                "최종 Main Camera 포즈만 DetailCameraPoint에 저장됩니다.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(!state.HasRegisteredTarget))
            {
                if (GUILayout.Button(
                        "현재 Game Camera 캡처 후 Play Mode 종료",
                        GUILayout.Height(42f)))
                {
                    CaptureLiveGameCameraAndExit();
                }
            }
        }

        EditorGUILayout.Space(10f);
    }

    private void DrawStatusSection()
    {
        EditorGUILayout.LabelField(
            "상태",
            EditorStyles.boldLabel);

        string targetState =
            state.HasRegisteredTarget
                ? "등록 완료"
                : "미등록";

        string captureState =
            state.HasPendingPose
                ? "적용 대기 중"
                : "대기 없음";

        EditorGUILayout.LabelField(
            "Target",
            targetState);

        EditorGUILayout.LabelField(
            "Captured Pose",
            captureState);

        if (state.HasPendingPose)
        {
            EditorGUILayout.LabelField(
                "Camera",
                state.CapturedCameraName);

            EditorGUILayout.Vector3Field(
                "World Position",
                state.CapturedWorldPosition);

            Vector3 euler =
                state.CapturedWorldRotation.eulerAngles;

            EditorGUILayout.Vector3Field(
                "World Rotation",
                euler);
        }

        using (new EditorGUI.DisabledScope(
                   !state.HasRegisteredTarget &&
                   !state.HasPendingPose))
        {
            if (GUILayout.Button("등록/캡처 정보 초기화"))
            {
                ClearState();
            }
        }

        EditorGUILayout.Space(10f);
    }

    private void DrawExplanation()
    {
        EditorGUILayout.LabelField(
            "권장 작업 순서",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "1. Edit Mode에서 Enemy의 DetailCameraPoint를 선택합니다.\n" +
            "2. '현재 선택한 DetailCameraPoint 등록'을 누릅니다.\n" +
            "3. Play Mode를 시작합니다.\n" +
            "4. CM_Overview를 움직이거나 Cinemachine 설정을 조절해 " +
            "Game View에 원하는 Enemy 상세 구도를 만듭니다.\n" +
            "5. 카메라 블렌드와 Damping이 완전히 멈출 때까지 기다립니다.\n" +
            "6. '현재 Game Camera 캡처 후 Play Mode 종료'를 누릅니다.\n" +
            "7. Edit Mode로 돌아오면 포즈가 자동 적용됩니다.\n" +
            "8. Scene을 저장합니다.\n\n" +
            "Inspector의 Local Position/Rotation 숫자가 복잡해지는 것은 정상입니다. " +
            "이 Tool은 SetPositionAndRotation으로 월드 포즈를 기록하고, " +
            "Unity가 부모 기준 로컬 값으로 정확하게 환산합니다.",
            MessageType.None);
    }

    private void RegisterSelectedTarget()
    {
        Transform selected =
            Selection.activeTransform;

        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Detail Camera Capture",
                "Hierarchy에서 DetailCameraPoint를 먼저 선택하세요.",
                "확인");
            return;
        }

        if (!selected.gameObject.scene.IsValid())
        {
            EditorUtility.DisplayDialog(
                "Detail Camera Capture",
                "Project 에셋이 아니라 Scene 안의 DetailCameraPoint를 선택해야 합니다.",
                "확인");
            return;
        }

        if (!selected.name.Contains(
                "DetailCameraPoint",
                StringComparison.OrdinalIgnoreCase))
        {
            bool continueAnyway =
                EditorUtility.DisplayDialog(
                    "선택 확인",
                    $"선택한 오브젝트 이름이 DetailCameraPoint가 아닙니다.\n\n" +
                    $"Selected: {GetHierarchyPath(selected)}\n\n" +
                    "그래도 이 Transform을 대상으로 등록할까요?",
                    "등록",
                    "취소");

            if (!continueAnyway)
                return;
        }

        GlobalObjectId globalId =
            GlobalObjectId.GetGlobalObjectIdSlow(
                selected);

        state.TargetGlobalObjectId =
            globalId.ToString();

        state.TargetScenePath =
            selected.gameObject.scene.path;

        state.TargetHierarchyPath =
            GetHierarchyPath(selected);

        state.HasRegisteredTarget =
            true;

        state.HasPendingPose =
            false;

        displayedTarget =
            selected;

        SaveState();

        ShowNotification(
            new GUIContent(
                "DetailCameraPoint 등록 완료"));

        Debug.Log(
            "[DetailCameraCapture][TARGET_REGISTERED] " +
            $"Target={state.TargetHierarchyPath}, " +
            $"Scene={state.TargetScenePath}, " +
            $"GlobalObjectId={state.TargetGlobalObjectId}",
            selected);
    }

    private void CaptureLiveGameCameraAndExit()
    {
        if (!EditorApplication.isPlaying)
            return;

        if (!state.HasRegisteredTarget)
        {
            EditorUtility.DisplayDialog(
                "Detail Camera Capture",
                "Edit Mode에서 DetailCameraPoint를 먼저 등록해야 합니다.",
                "확인");
            return;
        }

        Camera camera =
            ResolveActiveGameCamera();

        if (camera == null)
        {
            EditorUtility.DisplayDialog(
                "Detail Camera Capture",
                "활성 Game Camera를 찾지 못했습니다.",
                "확인");
            return;
        }

        Transform cameraTransform =
            camera.transform;

        state.CapturedWorldPosition =
            cameraTransform.position;

        state.CapturedWorldRotation =
            cameraTransform.rotation;

        state.CapturedCameraName =
            camera.name;

        state.CapturedFieldOfView =
            camera.fieldOfView;

        state.CapturedOrthographic =
            camera.orthographic;

        state.CapturedOrthographicSize =
            camera.orthographicSize;

        state.HasPendingPose =
            true;

        SaveState();

        Debug.Log(
            "[DetailCameraCapture][GAME_CAMERA_CAPTURED] " +
            $"Camera={camera.name}, " +
            $"WorldPosition={cameraTransform.position:F4}, " +
            $"WorldRotation={cameraTransform.eulerAngles:F3}, " +
            $"Target={state.TargetHierarchyPath}",
            camera);

        EditorApplication.isPlaying =
            false;
    }

    private static Camera ResolveActiveGameCamera()
    {
        Camera main =
            Camera.main;

        if (main != null &&
            main.isActiveAndEnabled)
        {
            return main;
        }

        Camera[] cameras =
            Resources.FindObjectsOfTypeAll<Camera>();

        return cameras
            .Where(
                camera =>
                    camera != null &&
                    camera.gameObject.scene.IsValid() &&
                    camera.isActiveAndEnabled &&
                    camera.targetTexture == null)
            .OrderByDescending(
                camera => camera.depth)
            .FirstOrDefault();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange change)
    {
        if (change !=
            PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        if (!EditorPrefs.GetBool(
                AutoApplyKey,
                true))
        {
            return;
        }

        EditorApplication.delayCall +=
            ApplyPendingAfterPlayMode;
    }

    private static void ApplyPendingAfterPlayMode()
    {
        CaptureState state =
            LoadStateStatic();

        if (state == null ||
            !state.HasRegisteredTarget ||
            !state.HasPendingPose)
        {
            return;
        }

        ApplyPendingPoseStatic(
            state,
            showDialog: false);
    }

    private void ApplyPendingPose(
        bool showDialog)
    {
        LoadState();

        bool success =
            ApplyPendingPoseStatic(
                state,
                showDialog);

        if (!success)
            return;

        LoadState();
        ResolveDisplayedTarget();
        Repaint();
    }

    private static bool ApplyPendingPoseStatic(
        CaptureState state,
        bool showDialog)
    {
        Transform target =
            ResolveTargetTransform(
                state);

        if (target == null)
        {
            string message =
                "등록된 DetailCameraPoint를 현재 Edit Mode Scene에서 찾지 못했습니다.\n\n" +
                $"Scene: {state.TargetScenePath}\n" +
                $"Hierarchy: {state.TargetHierarchyPath}\n\n" +
                "해당 Scene을 연 뒤 Window에서 '보류 중인 캡처를 지금 적용'을 누르세요.";

            Debug.LogError(
                "[DetailCameraCapture][APPLY_FAILED] " +
                message.Replace(
                    "\n",
                    " | "));

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Detail Camera Capture",
                    message,
                    "확인");
            }

            return false;
        }

        Undo.RecordObject(
            target,
            "Apply Detail Camera Game View Pose");

        target.SetPositionAndRotation(
            state.CapturedWorldPosition,
            state.CapturedWorldRotation);

        EditorUtility.SetDirty(
            target);

        Scene scene =
            target.gameObject.scene;

        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                scene);
        }

        Selection.activeTransform =
            target;

        state.HasPendingPose =
            false;

        SaveStateStatic(
            state);

        Vector3 parentScale =
            target.parent != null
                ? target.parent.lossyScale
                : Vector3.one;

        bool suspiciousScale =
            Mathf.Abs(parentScale.x - parentScale.y) > 0.001f ||
            Mathf.Abs(parentScale.y - parentScale.z) > 0.001f ||
            parentScale.x <= 0f ||
            parentScale.y <= 0f ||
            parentScale.z <= 0f;

        Debug.Log(
            "[DetailCameraCapture][POSE_APPLIED] " +
            $"Target={GetHierarchyPath(target)}, " +
            $"WorldPosition={target.position:F4}, " +
            $"WorldRotation={target.eulerAngles:F3}, " +
            $"LocalPosition={target.localPosition:F4}, " +
            $"LocalRotation={target.localEulerAngles:F3}",
            target);

        if (suspiciousScale)
        {
            Debug.LogWarning(
                "[DetailCameraCapture][PARENT_SCALE_WARNING] " +
                $"Parent={GetHierarchyPath(target.parent)}, " +
                $"LossyScale={parentScale:F4}. " +
                "CameraPoints 부모 계층은 가능하면 양수 균일 스케일을 사용하세요.",
                target);
        }

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Detail Camera Capture",
                "Game View의 최종 Main Camera 월드 포즈를 " +
                "DetailCameraPoint에 적용했습니다.\n\n" +
                "Scene을 저장하세요.",
                "확인");
        }

        ProjectAbyssDetailCameraPoseCaptureWindow window =
            Resources
                .FindObjectsOfTypeAll<
                    ProjectAbyssDetailCameraPoseCaptureWindow>()
                .FirstOrDefault();

        window?.ShowNotification(
            new GUIContent(
                "DetailCameraPoint 적용 완료"));

        return true;
    }

    private static Transform ResolveTargetTransform(
        CaptureState state)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(
                state.TargetGlobalObjectId))
        {
            return null;
        }

        if (GlobalObjectId.TryParse(
                state.TargetGlobalObjectId,
                out GlobalObjectId globalId))
        {
            UnityEngine.Object resolved =
                GlobalObjectId
                    .GlobalObjectIdentifierToObjectSlow(
                        globalId);

            if (resolved is Transform transform)
                return transform;

            if (resolved is GameObject gameObject)
                return gameObject.transform;

            if (resolved is Component component)
                return component.transform;
        }

        return FindBySceneAndHierarchyPath(
            state.TargetScenePath,
            state.TargetHierarchyPath);
    }

    private static Transform FindBySceneAndHierarchyPath(
        string scenePath,
        string hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(
                hierarchyPath))
        {
            return null;
        }

        Scene targetScene =
            default;

        for (int index = 0;
             index < SceneManager.sceneCount;
             index++)
        {
            Scene scene =
                SceneManager.GetSceneAt(index);

            if (scene.path ==
                scenePath)
            {
                targetScene =
                    scene;
                break;
            }
        }

        if (!targetScene.IsValid() ||
            !targetScene.isLoaded)
        {
            return null;
        }

        string[] names =
            hierarchyPath.Split(
                '/');

        if (names.Length == 0)
            return null;

        GameObject root =
            targetScene
                .GetRootGameObjects()
                .FirstOrDefault(
                    candidate =>
                        candidate.name ==
                        names[0]);

        if (root == null)
            return null;

        Transform current =
            root.transform;

        for (int index = 1;
             index < names.Length;
             index++)
        {
            Transform next =
                null;

            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                Transform child =
                    current.GetChild(
                        childIndex);

                if (child.name !=
                    names[index])
                {
                    continue;
                }

                next =
                    child;
                break;
            }

            if (next == null)
                return null;

            current =
                next;
        }

        return current;
    }

    private void ResolveDisplayedTarget()
    {
        displayedTarget =
            ResolveTargetTransform(
                state);
    }

    private void ClearState()
    {
        state =
            new CaptureState();

        displayedTarget =
            null;

        SaveState();

        ShowNotification(
            new GUIContent(
                "캡처 정보 초기화"));
    }

    private void LoadStateIfNeeded()
    {
        if (state == null)
            LoadState();
    }

    private void LoadState()
    {
        state =
            LoadStateStatic() ??
            new CaptureState();
    }

    private static CaptureState LoadStateStatic()
    {
        string json =
            SessionState.GetString(
                SessionKey,
                string.Empty);

        if (string.IsNullOrWhiteSpace(
                json))
        {
            return new CaptureState();
        }

        try
        {
            return JsonUtility.FromJson<CaptureState>(
                       json) ??
                   new CaptureState();
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);

            return new CaptureState();
        }
    }

    private void SaveState()
    {
        SaveStateStatic(
            state);
    }

    private static void SaveStateStatic(
        CaptureState state)
    {
        string json =
            JsonUtility.ToJson(
                state ?? new CaptureState());

        SessionState.SetString(
            SessionKey,
            json);
    }

    private static string GetHierarchyPath(
        Transform transform)
    {
        if (transform == null)
            return "(null)";

        string path =
            transform.name;

        Transform current =
            transform.parent;

        while (current != null)
        {
            path =
                current.name +
                "/" +
                path;

            current =
                current.parent;
        }

        return path;
    }
}
#endif
