#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigColliderTools
    {
        public static GameObject Duplicate(SecondaryRigCollider source)
        {
            if (source == null)
                return null;

            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = GetUniqueSiblingName(source.transform.parent, source.gameObject.name + "_Copy");
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Secondary Rig Collider");
            Selection.activeGameObject = copy;
            MarkControllerDirty(copy);
            return copy;
        }

        public static GameObject MirrorLeftRight(SecondaryRigCollider source, out string message)
        {
            if (source == null)
            {
                message = "No Secondary Rig Collider selected.";
                return null;
            }

            SecondaryRigController controller = source.GetComponentInParent<SecondaryRigController>();
            if (controller == null)
            {
                message = "Mirror needs a SecondaryRigController above the collider.";
                return null;
            }

            Transform mirrorRoot = controller.SimulationRoot != null
                ? controller.SimulationRoot
                : controller.transform;

            SecondaryRigColliderFollower sourceFollower = source.GetComponent<SecondaryRigColliderFollower>();
            Transform sourceFollow = sourceFollower != null ? sourceFollower.FollowTarget : null;
            Transform mirroredFollow = FindMirroredFollowTarget(controller, sourceFollow);

            Vector3 sphereCenter = default;
            Vector3 capsuleA = default;
            Vector3 capsuleB = default;
            float sourceWorldRadius;

            if (source is SecondaryRigSphereCollider sourceSphere)
            {
                sphereCenter = MirrorPoint(mirrorRoot, sourceSphere.WorldCenter);
                sourceWorldRadius = sourceSphere.WorldRadius;
            }
            else if (source is SecondaryRigCapsuleCollider sourceCapsule)
            {
                capsuleA = MirrorPoint(mirrorRoot, sourceCapsule.WorldPointA);
                capsuleB = MirrorPoint(mirrorRoot, sourceCapsule.WorldPointB);
                sourceWorldRadius = sourceCapsule.WorldRadius;
            }
            else
            {
                message = "Unsupported collider type.";
                return null;
            }

            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = GetUniqueSiblingName(source.transform.parent, SwapSideName(source.gameObject.name));
            Undo.RegisterCreatedObjectUndo(copy, "Mirror Secondary Rig Collider");

            SecondaryRigColliderFollower copyFollower = copy.GetComponent<SecondaryRigColliderFollower>();
            if (copyFollower != null && mirroredFollow != null)
            {
                copyFollower.FollowTarget = mirroredFollow;
                copyFollower.SnapAndClearOffsets();
            }
            else
            {
                if (copyFollower != null)
                    copyFollower.FollowTarget = null;

                Vector3 mirroredHolder = MirrorPoint(mirrorRoot, source.transform.position);
                copy.transform.position = mirroredHolder;
                copy.transform.rotation = MirrorRotation(mirrorRoot, source.transform.rotation);
            }

            SecondaryRigSphereCollider copySphere = copy.GetComponent<SecondaryRigSphereCollider>();
            if (copySphere != null)
            {
                copySphere.LocalCenter = copy.transform.InverseTransformPoint(sphereCenter);
                copySphere.Radius = WorldToLocalRadius(copy.transform, sourceWorldRadius, copySphere.Margin);
            }

            SecondaryRigCapsuleCollider copyCapsule = copy.GetComponent<SecondaryRigCapsuleCollider>();
            if (copyCapsule != null)
            {
                copyCapsule.LocalPointA = copy.transform.InverseTransformPoint(capsuleA);
                copyCapsule.LocalPointB = copy.transform.InverseTransformPoint(capsuleB);
                copyCapsule.Radius = WorldToLocalRadius(copy.transform, sourceWorldRadius, copyCapsule.Margin);
            }

            EditorUtility.SetDirty(copy);
            Selection.activeGameObject = copy;
            MarkControllerDirty(copy);

            message = mirroredFollow != null
                ? $"Mirrored collider created and Follow Target mapped to '{mirroredFollow.name}'."
                : "Mirrored collider created geometrically. No opposite Follow Target was found, so verify the follower manually.";
            return copy;
        }

        public static Transform FindMirroredFollowTarget(SecondaryRigController controller, Transform source)
        {
            if (controller == null || source == null)
                return null;

            Animator animator = controller.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                HumanBodyBones? sourceBone = FindHumanBone(animator, source);
                if (sourceBone.HasValue && TryGetOppositeHumanBone(sourceBone.Value, out HumanBodyBones opposite))
                    return animator.GetBoneTransform(opposite);
            }

            string oppositeName = SwapSideName(source.name);
            if (!string.Equals(oppositeName, source.name, StringComparison.Ordinal))
            {
                Transform[] transforms = controller.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (string.Equals(transforms[i].name, oppositeName, StringComparison.Ordinal))
                        return transforms[i];
                }
            }

            return null;
        }

        private static HumanBodyBones? FindHumanBone(Animator animator, Transform transform)
        {
            for (int boneIndex = 0; boneIndex < (int)HumanBodyBones.LastBone; boneIndex++)
            {
                HumanBodyBones bone = (HumanBodyBones)boneIndex;
                if (animator.GetBoneTransform(bone) == transform)
                    return bone;
            }
            return null;
        }

        private static bool TryGetOppositeHumanBone(HumanBodyBones source, out HumanBodyBones opposite)
        {
            switch (source)
            {
                case HumanBodyBones.LeftUpperLeg: opposite = HumanBodyBones.RightUpperLeg; return true;
                case HumanBodyBones.RightUpperLeg: opposite = HumanBodyBones.LeftUpperLeg; return true;
                case HumanBodyBones.LeftLowerLeg: opposite = HumanBodyBones.RightLowerLeg; return true;
                case HumanBodyBones.RightLowerLeg: opposite = HumanBodyBones.LeftLowerLeg; return true;
                case HumanBodyBones.LeftFoot: opposite = HumanBodyBones.RightFoot; return true;
                case HumanBodyBones.RightFoot: opposite = HumanBodyBones.LeftFoot; return true;
                case HumanBodyBones.LeftToes: opposite = HumanBodyBones.RightToes; return true;
                case HumanBodyBones.RightToes: opposite = HumanBodyBones.LeftToes; return true;
                case HumanBodyBones.LeftShoulder: opposite = HumanBodyBones.RightShoulder; return true;
                case HumanBodyBones.RightShoulder: opposite = HumanBodyBones.LeftShoulder; return true;
                case HumanBodyBones.LeftUpperArm: opposite = HumanBodyBones.RightUpperArm; return true;
                case HumanBodyBones.RightUpperArm: opposite = HumanBodyBones.LeftUpperArm; return true;
                case HumanBodyBones.LeftLowerArm: opposite = HumanBodyBones.RightLowerArm; return true;
                case HumanBodyBones.RightLowerArm: opposite = HumanBodyBones.LeftLowerArm; return true;
                case HumanBodyBones.LeftHand: opposite = HumanBodyBones.RightHand; return true;
                case HumanBodyBones.RightHand: opposite = HumanBodyBones.LeftHand; return true;
                case HumanBodyBones.LeftEye: opposite = HumanBodyBones.RightEye; return true;
                case HumanBodyBones.RightEye: opposite = HumanBodyBones.LeftEye; return true;
                default:
                    opposite = source;
                    return false;
            }
        }

        private static Vector3 MirrorPoint(Transform root, Vector3 worldPoint)
        {
            Vector3 local = root.InverseTransformPoint(worldPoint);
            local.x = -local.x;
            return root.TransformPoint(local);
        }

        private static Quaternion MirrorRotation(Transform root, Quaternion worldRotation)
        {
            Vector3 forward = root.InverseTransformDirection(worldRotation * Vector3.forward);
            Vector3 up = root.InverseTransformDirection(worldRotation * Vector3.up);
            forward.x = -forward.x;
            up.x = -up.x;
            if (forward.sqrMagnitude <= 0.000001f || up.sqrMagnitude <= 0.000001f)
                return worldRotation;

            Quaternion localMirrored = Quaternion.LookRotation(forward.normalized, up.normalized);
            return root.rotation * localMirrored;
        }

        private static string SwapSideName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "MirroredCollider";

            string[] leftTokens = { "Left", "left", "LEFT", "_L_", ".L", "_L", "L_" };
            string[] rightTokens = { "Right", "right", "RIGHT", "_R_", ".R", "_R", "R_" };

            for (int i = 0; i < leftTokens.Length; i++)
            {
                if (name.Contains(leftTokens[i]))
                    return name.Replace(leftTokens[i], rightTokens[i]);
                if (name.Contains(rightTokens[i]))
                    return name.Replace(rightTokens[i], leftTokens[i]);
            }

            return name + "_Mirrored";
        }

        private static string GetUniqueSiblingName(Transform parent, string desired)
        {
            if (parent == null)
                return desired;

            bool Exists(string candidate)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (string.Equals(parent.GetChild(i).name, candidate, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }

            if (!Exists(desired))
                return desired;

            for (int index = 1; index < 1000; index++)
            {
                string candidate = $"{desired}_{index:00}";
                if (!Exists(candidate))
                    return candidate;
            }

            return desired + "_Copy";
        }

        private static float WorldToLocalRadius(Transform transform, float worldRadius, float margin)
        {
            float scale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
            float withoutMargin = Mathf.Max(0f, worldRadius - margin);
            return scale > 0.0001f ? withoutMargin / scale : withoutMargin;
        }

        private static void MarkControllerDirty(GameObject gameObject)
        {
            SecondaryRigController controller = gameObject.GetComponentInParent<SecondaryRigController>();
            if (controller == null)
                return;

            controller.RefreshColliders();
            EditorUtility.SetDirty(controller);
            if (PrefabUtility.IsPartOfPrefabInstance(controller))
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
    }
}
#endif
