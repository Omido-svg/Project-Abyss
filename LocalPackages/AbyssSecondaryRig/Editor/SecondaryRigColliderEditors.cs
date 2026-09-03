#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigColliderSceneDrawing
    {
        public static void DrawSphereOutline(Vector3 center, float radius, Quaternion rotation)
        {
            if (radius <= 0f)
                return;

            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            Handles.DrawWireDisc(center, right, radius);
            Handles.DrawWireDisc(center, up, radius);
            Handles.DrawWireDisc(center, forward, radius);
        }

        public static void DrawCapsuleOutline(Vector3 a, Vector3 b, float radius, Transform reference)
        {
            if (radius <= 0f)
                return;

            Vector3 axisVector = b - a;
            Vector3 axis = axisVector.sqrMagnitude > 0.0000001f
                ? axisVector.normalized
                : reference.up.normalized;

            Vector3 sideA = Vector3.Cross(axis, reference.forward);
            if (sideA.sqrMagnitude <= 0.000001f)
                sideA = Vector3.Cross(axis, reference.right);
            if (sideA.sqrMagnitude <= 0.000001f)
                sideA = Vector3.Cross(axis, Vector3.up);
            if (sideA.sqrMagnitude <= 0.000001f)
                sideA = Vector3.right;
            sideA.Normalize();

            Vector3 sideB = Vector3.Cross(axis, sideA).normalized;

            Handles.DrawLine(a + sideA * radius, b + sideA * radius);
            Handles.DrawLine(a - sideA * radius, b - sideA * radius);
            Handles.DrawLine(a + sideB * radius, b + sideB * radius);
            Handles.DrawLine(a - sideB * radius, b - sideB * radius);

            DrawHemisphere(a, -axis, sideA, radius);
            DrawHemisphere(a, -axis, sideB, radius);
            DrawHemisphere(b, axis, sideA, radius);
            DrawHemisphere(b, axis, sideB, radius);
        }

        public static float RadiusSlider(Vector3 center, Vector3 axis, float worldRadius)
        {
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Vector3.right;
            axis.Normalize();

            Vector3 handlePosition = center + axis * Mathf.Max(0.0001f, worldRadius);
            float handleSize = HandleUtility.GetHandleSize(handlePosition) * 0.075f;
            Handles.DrawLine(center, handlePosition);

            Vector3 newHandlePosition = Handles.Slider(
                handlePosition,
                axis,
                handleSize,
                Handles.CubeHandleCap,
                0f);

            return Mathf.Max(0.0001f, Vector3.Dot(newHandlePosition - center, axis));
        }

        private static void DrawHemisphere(Vector3 center, Vector3 outward, Vector3 side, float radius)
        {
            Vector3 normal = Vector3.Cross(side, outward);
            if (normal.sqrMagnitude <= 0.000001f)
                return;

            Handles.DrawWireArc(center, normal.normalized, side.normalized, 180f, radius);
        }
    }

    internal static class SecondaryRigColliderInspectorTools
    {
        public static void Draw(SecondaryRigCollider collider)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Production Tools", EditorStyles.boldLabel);

            SecondaryRigColliderFollower follower = collider.GetComponent<SecondaryRigColliderFollower>();
            if (follower != null)
            {
                using (new EditorGUI.DisabledScope(follower.FollowTarget == null))
                {
                    if (GUILayout.Button("Select Follow Target"))
                    {
                        Selection.activeObject = follower.FollowTarget;
                        EditorGUIUtility.PingObject(follower.FollowTarget);
                    }
                }
            }

            if (GUILayout.Button("Auto Fit To Weighted Skinned Mesh"))
            {
                if (SecondaryRigColliderAutoFitUtility.TryAutoFit(collider, out string message))
                    Debug.Log($"[SecondaryRig] {message}", collider);
                else
                    EditorUtility.DisplayDialog("Secondary Rig Auto Fit", message, "OK");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate"))
                SecondaryRigColliderTools.Duplicate(collider);

            if (GUILayout.Button("Mirror L/R"))
            {
                SecondaryRigColliderTools.MirrorLeftRight(collider, out string message);
                Debug.Log($"[SecondaryRig] {message}", collider);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Auto Fit samples vertices influenced by this collider's Follow Target. " +
                "Mirror L/R mirrors geometry around the character root X=0 plane and attempts to map the opposite Humanoid bone.",
                MessageType.None);
        }
    }

    [CustomEditor(typeof(SecondaryRigSphereCollider))]
    public sealed class SecondaryRigSphereColliderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SecondaryRigColliderInspectorTools.Draw((SecondaryRigSphereCollider)target);
        }

        private void OnSceneGUI()
        {
            SecondaryRigSphereCollider collider = (SecondaryRigSphereCollider)target;
            Transform transform = collider.transform;

            Vector3 worldCenter = transform.TransformPoint(collider.LocalCenter);
            float worldRadius = collider.WorldRadius;

            Color previousColor = Handles.color;
            Handles.color = collider.CollisionEnabled ? Handles.selectedColor : Color.gray;
            SecondaryRigColliderSceneDrawing.DrawSphereOutline(worldCenter, worldRadius, transform.rotation);
            Handles.Label(worldCenter, collider.gameObject.name, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, transform.rotation);
            float newWorldRadius = SecondaryRigColliderSceneDrawing.RadiusSlider(
                newWorldCenter,
                transform.right,
                worldRadius);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Edit Secondary Sphere Collider");
                collider.LocalCenter = transform.InverseTransformPoint(newWorldCenter);
                collider.Radius = ToLocalRadius(transform, newWorldRadius, collider.Margin);
                EditorUtility.SetDirty(collider);
            }

            Handles.color = previousColor;
        }

        private static float ToLocalRadius(Transform transform, float worldRadius, float margin)
        {
            float scale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));

            float radiusWithoutMargin = Mathf.Max(0f, worldRadius - margin);
            return scale > 0.0001f ? radiusWithoutMargin / scale : radiusWithoutMargin;
        }
    }

    [CustomEditor(typeof(SecondaryRigCapsuleCollider))]
    public sealed class SecondaryRigCapsuleColliderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SecondaryRigColliderInspectorTools.Draw((SecondaryRigCapsuleCollider)target);
        }

        private void OnSceneGUI()
        {
            SecondaryRigCapsuleCollider collider = (SecondaryRigCapsuleCollider)target;
            Transform transform = collider.transform;

            Vector3 worldA = transform.TransformPoint(collider.LocalPointA);
            Vector3 worldB = transform.TransformPoint(collider.LocalPointB);
            float worldRadius = collider.WorldRadius;

            Color previousColor = Handles.color;
            Handles.color = collider.CollisionEnabled ? Handles.selectedColor : Color.gray;
            SecondaryRigColliderSceneDrawing.DrawCapsuleOutline(worldA, worldB, worldRadius, transform);
            Handles.Label((worldA + worldB) * 0.5f, collider.gameObject.name, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldA = Handles.PositionHandle(worldA, transform.rotation);
            Vector3 newWorldB = Handles.PositionHandle(worldB, transform.rotation);

            Vector3 midpoint = (newWorldA + newWorldB) * 0.5f;
            Vector3 capsuleAxis = newWorldB - newWorldA;
            Vector3 radiusAxis = Vector3.Cross(
                capsuleAxis.sqrMagnitude > 0.000001f ? capsuleAxis.normalized : transform.up,
                transform.forward);
            if (radiusAxis.sqrMagnitude <= 0.000001f)
                radiusAxis = transform.right;

            float newWorldRadius = SecondaryRigColliderSceneDrawing.RadiusSlider(
                midpoint,
                radiusAxis,
                worldRadius);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Edit Secondary Capsule Collider");
                collider.LocalPointA = transform.InverseTransformPoint(newWorldA);
                collider.LocalPointB = transform.InverseTransformPoint(newWorldB);
                collider.Radius = ToLocalRadius(transform, newWorldRadius, collider.Margin);
                EditorUtility.SetDirty(collider);
            }

            Handles.color = previousColor;
        }

        private static float ToLocalRadius(Transform transform, float worldRadius, float margin)
        {
            float scale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));

            float radiusWithoutMargin = Mathf.Max(0f, worldRadius - margin);
            return scale > 0.0001f ? radiusWithoutMargin / scale : radiusWithoutMargin;
        }
    }
}
#endif
