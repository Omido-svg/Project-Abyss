#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectAbyssEditModeCameraPointShortcutV416
{
    private const string ShortcutId =
        "Project Abyss/Camera Authoring/" +
        "Copy CM_Overview Pose To Selected Camera Point";

    private const string MenuPath =
        "Tools/Project Abyss/Camera Authoring/" +
        "Copy CM_Overview Pose To Selected Camera Point";

    private const string CinemachineCameraTypeName =
        "Unity.Cinemachine.CinemachineCamera";

    [Shortcut(
        ShortcutId,
        KeyCode.F,
        ShortcutModifiers.Control |
        ShortcutModifiers.Shift)]
    private static void CopyByShortcut()
    {
        CopyOverviewPoseToSelectedPoint();
    }

    [MenuItem(MenuPath)]
    private static void CopyByMenu()
    {
        CopyOverviewPoseToSelectedPoint();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCopyByMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void CopyOverviewPoseToSelectedPoint()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Notify(
                "Edit Mode에서만 사용할 수 있습니다.");
            return;
        }

        Transform targetPoint =
            Selection.activeTransform;

        if (targetPoint == null)
        {
            EditorUtility.DisplayDialog(
                "Camera Point 저장",
                "Hierarchy에서 CameraPoints 아래의 저장 대상 포인트를 선택하세요.",
                "확인");
            return;
        }

        Transform cameraPointsRoot =
            FindCameraPointsAncestor(
                targetPoint);

        if (cameraPointsRoot == null)
        {
            bool continueAnyway =
                EditorUtility.DisplayDialog(
                    "Camera Point 확인",
                    "선택 오브젝트가 이름이 'CameraPoints'인 부모 아래에 없습니다.\n\n" +
                    $"Selected: {GetHierarchyPath(targetPoint)}\n\n" +
                    "그래도 CM_Overview의 월드 위치와 회전을 복사할까요?",
                    "복사",
                    "취소");

            if (!continueAnyway)
                return;
        }

        Component overview =
            FindCmOverview();

        if (overview == null)
        {
            EditorUtility.DisplayDialog(
                "Camera Point 저장",
                "현재 열린 Scene에서 Unity.Cinemachine.CinemachineCamera를 가진 " +
                "CM_Overview 오브젝트를 찾지 못했습니다.",
                "확인");
            return;
        }

        Transform source =
            overview.transform;

        Vector3 worldPosition =
            source.position;

        Quaternion worldRotation =
            source.rotation;

        Undo.RecordObject(
            targetPoint,
            "Copy CM_Overview Pose To Camera Point");

        targetPoint.SetPositionAndRotation(
            worldPosition,
            worldRotation);

        EditorUtility.SetDirty(
            targetPoint);

        Scene targetScene =
            targetPoint.gameObject.scene;

        if (targetScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                targetScene);
        }

        Debug.Log(
            "[EditModeCameraPoint][POSE_COPIED] " +
            $"Source={GetHierarchyPath(source)}, " +
            $"Target={GetHierarchyPath(targetPoint)}, " +
            $"WorldPosition={worldPosition:F4}, " +
            $"WorldRotation={worldRotation.eulerAngles:F3}, " +
            $"LocalPosition={targetPoint.localPosition:F4}, " +
            $"LocalRotation={targetPoint.localEulerAngles:F3}",
            targetPoint);

        WarnForSuspiciousParentScale(
            targetPoint);

        Notify(
            $"{targetPoint.name} ← CM_Overview 저장 완료");
    }

    private static Component FindCmOverview()
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        Component exact =
            behaviours
                .Where(
                    behaviour =>
                        IsSceneBehaviourOfType(
                            behaviour,
                            CinemachineCameraTypeName))
                .FirstOrDefault(
                    behaviour =>
                        string.Equals(
                            behaviour.name,
                            "CM_Overview",
                            StringComparison.Ordinal));

        if (exact != null)
            return exact;

        Component partialName =
            behaviours
                .Where(
                    behaviour =>
                        IsSceneBehaviourOfType(
                            behaviour,
                            CinemachineCameraTypeName))
                .FirstOrDefault(
                    behaviour =>
                        behaviour.name.Contains(
                            "Overview",
                            StringComparison.OrdinalIgnoreCase));

        if (partialName != null)
            return partialName;

        Component[] candidates =
            behaviours
                .Where(
                    behaviour =>
                        IsSceneBehaviourOfType(
                            behaviour,
                            CinemachineCameraTypeName))
                .Cast<Component>()
                .ToArray();

        if (candidates.Length == 1)
            return candidates[0];

        return null;
    }

    private static bool IsSceneBehaviourOfType(
        MonoBehaviour behaviour,
        string fullTypeName)
    {
        return behaviour != null &&
               behaviour.gameObject.scene.IsValid() &&
               behaviour.GetType().FullName ==
               fullTypeName;
    }

    private static Transform FindCameraPointsAncestor(
        Transform target)
    {
        Transform current =
            target?.parent;

        while (current != null)
        {
            if (string.Equals(
                    current.name,
                    "CameraPoints",
                    StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            current =
                current.parent;
        }

        return null;
    }

    private static void WarnForSuspiciousParentScale(
        Transform target)
    {
        Transform current =
            target?.parent;

        while (current != null)
        {
            Vector3 scale =
                current.localScale;

            bool nonUniform =
                Mathf.Abs(scale.x - scale.y) >
                0.001f ||
                Mathf.Abs(scale.y - scale.z) >
                0.001f;

            bool nonPositive =
                scale.x <= 0f ||
                scale.y <= 0f ||
                scale.z <= 0f;

            if (nonUniform ||
                nonPositive)
            {
                Debug.LogWarning(
                    "[EditModeCameraPoint][PARENT_SCALE_WARNING] " +
                    $"Target={GetHierarchyPath(target)}, " +
                    $"Parent={GetHierarchyPath(current)}, " +
                    $"LocalScale={scale:F4}. " +
                    "월드 포즈는 복사됐지만 비균일 또는 음수 Scale 때문에 " +
                    "Inspector의 로컬 회전이 직관적이지 않을 수 있습니다.",
                    target);
                return;
            }

            current =
                current.parent;
        }
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

    private static void Notify(
        string message)
    {
        GUIContent content =
            new(
                message);

        EditorWindow focusedWindow =
            EditorWindow.focusedWindow;

        if (focusedWindow != null)
        {
            focusedWindow.ShowNotification(
                content);
            return;
        }

        SceneView.lastActiveSceneView
            ?.ShowNotification(
                content);
    }
}
#endif
