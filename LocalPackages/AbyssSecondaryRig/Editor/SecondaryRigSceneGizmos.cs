#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigSceneGizmos
    {
        private const string ShowAllKey = "ProjectAbyss.SecondaryRig.ShowAllColliderGizmos";
        private const string ShowLabelsKey = "ProjectAbyss.SecondaryRig.ShowColliderLabels";

        public static bool ShowAll
        {
            get => EditorPrefs.GetBool(ShowAllKey, true);
            set => EditorPrefs.SetBool(ShowAllKey, value);
        }

        public static bool ShowLabels
        {
            get => EditorPrefs.GetBool(ShowLabelsKey, false);
            set => EditorPrefs.SetBool(ShowLabelsKey, value);
        }

        [MenuItem("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show All Colliders", false, 2300)]
        private static void ToggleShowAll()
        {
            ShowAll = !ShowAll;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show All Colliders", true)]
        private static bool ValidateShowAll()
        {
            Menu.SetChecked("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show All Colliders", ShowAll);
            return true;
        }

        [MenuItem("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show Labels", false, 2301)]
        private static void ToggleLabels()
        {
            ShowLabels = !ShowLabels;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show Labels", true)]
        private static bool ValidateLabels()
        {
            Menu.SetChecked("Tools/Project Abyss/Secondary Rig/Scene Gizmos/Show Labels", ShowLabels);
            return true;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.NotInSelectionHierarchy)]
        private static void DrawSphere(SecondaryRigSphereCollider collider, GizmoType gizmoType)
        {
            if (!ShowAll || collider == null || !collider.gameObject.activeInHierarchy)
                return;

            Color previous = Handles.color;
            Handles.color = collider.CollisionEnabled
                ? new Color(0.35f, 0.8f, 1f, 0.38f)
                : new Color(0.5f, 0.5f, 0.5f, 0.25f);
            SecondaryRigColliderSceneDrawing.DrawSphereOutline(
                collider.WorldCenter,
                collider.WorldRadius,
                collider.transform.rotation);
            DrawLabel(collider, collider.WorldCenter);
            Handles.color = previous;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.NotInSelectionHierarchy)]
        private static void DrawCapsule(SecondaryRigCapsuleCollider collider, GizmoType gizmoType)
        {
            if (!ShowAll || collider == null || !collider.gameObject.activeInHierarchy)
                return;

            Color previous = Handles.color;
            Handles.color = collider.CollisionEnabled
                ? new Color(0.35f, 0.8f, 1f, 0.38f)
                : new Color(0.5f, 0.5f, 0.5f, 0.25f);
            SecondaryRigColliderSceneDrawing.DrawCapsuleOutline(
                collider.WorldPointA,
                collider.WorldPointB,
                collider.WorldRadius,
                collider.transform);
            DrawLabel(collider, (collider.WorldPointA + collider.WorldPointB) * 0.5f);
            Handles.color = previous;
        }

        private static void DrawLabel(SecondaryRigCollider collider, Vector3 position)
        {
            if (!ShowLabels)
                return;

            Handles.Label(position, collider.gameObject.name, EditorStyles.miniLabel);
        }
    }
}
#endif
