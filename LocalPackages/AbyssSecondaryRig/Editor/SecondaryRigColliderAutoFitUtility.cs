#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigColliderAutoFitUtility
    {
        private const float MinimumBoneWeight = 0.12f;
        private const float Padding = 1.04f;

        public static int AutoFitAll(Transform colliderRoot, out int failed)
        {
            failed = 0;
            if (colliderRoot == null)
                return 0;

            SecondaryRigCollider[] colliders = colliderRoot.GetComponentsInChildren<SecondaryRigCollider>(true);
            int fitted = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (TryAutoFit(colliders[i], out _))
                    fitted++;
                else
                    failed++;
            }
            return fitted;
        }

        public static bool TryAutoFit(SecondaryRigCollider collider, out string message)
        {
            if (collider == null)
            {
                message = "Collider is null.";
                return false;
            }

            SecondaryRigController controller = collider.GetComponentInParent<SecondaryRigController>();
            GameObject characterRoot = controller != null ? controller.gameObject : FindCharacterRoot(collider.gameObject);
            SecondaryRigColliderFollower follower = collider.GetComponent<SecondaryRigColliderFollower>();
            Transform followBone = follower != null ? follower.FollowTarget : collider.transform.parent;

            if (characterRoot == null || followBone == null)
            {
                message = "Auto Fit needs a character root and a Follow Target (or parent bone).";
                return false;
            }

            List<Vector3> points = CollectWeightedWorldPoints(characterRoot, followBone);
            if (points.Count < 4)
            {
                message = $"Not enough skinned vertices were influenced by '{followBone.name}'. Found {points.Count}. " +
                          "Keep the manually generated proxy or choose a different Follow Target.";
                return false;
            }

            Undo.RecordObject(collider, "Auto Fit Secondary Rig Collider");

            if (collider is SecondaryRigSphereCollider sphere)
            {
                FitSphere(sphere, points);
            }
            else if (collider is SecondaryRigCapsuleCollider capsule)
            {
                FitCapsule(capsule, points, followBone);
            }
            else
            {
                message = "Unsupported collider type.";
                return false;
            }

            EditorUtility.SetDirty(collider);
            message = $"Auto Fit complete using {points.Count} weighted skinned vertices from '{followBone.name}'.";
            return true;
        }

        private static GameObject FindCharacterRoot(GameObject source)
        {
            Transform current = source != null ? source.transform : null;
            while (current != null)
            {
                if (current.GetComponentInChildren<Animator>(true) != null)
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        private static List<Vector3> CollectWeightedWorldPoints(GameObject characterRoot, Transform followBone)
        {
            List<Vector3> points = new();
            SkinnedMeshRenderer[] renderers = characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Mesh shared = renderer.sharedMesh;
                Transform[] bones = renderer.bones;
                if (shared == null || bones == null || bones.Length == 0)
                    continue;

                // Secondary meshes often still contain Head/Pelvis base weights. Including them
                // would make body proxies expand around hair/coat geometry, so only base-body
                // skinned meshes are sampled here.
                if (ContainsSecondaryBones(bones))
                    continue;

                int targetBoneIndex = -1;
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (bones[boneIndex] == followBone)
                    {
                        targetBoneIndex = boneIndex;
                        break;
                    }
                }

                if (targetBoneIndex < 0)
                    continue;

                BoneWeight[] weights = shared.boneWeights;
                if (weights == null || weights.Length != shared.vertexCount)
                    continue;

                Mesh baked = new Mesh { name = "SecondaryRig_AutoFit_Temp" };
                baked.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    renderer.BakeMesh(baked);
                    Vector3[] vertices = baked.vertices;
                    int count = Mathf.Min(vertices.Length, weights.Length);
                    for (int vertexIndex = 0; vertexIndex < count; vertexIndex++)
                    {
                        if (GetBoneWeight(weights[vertexIndex], targetBoneIndex) < MinimumBoneWeight)
                            continue;

                        points.Add(renderer.transform.TransformPoint(vertices[vertexIndex]));
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            return points;
        }

        private static bool ContainsSecondaryBones(Transform[] bones)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                    continue;

                string name = bone.name;
                if (name.Contains("_SPR_") || name.Contains("_TGT_"))
                    return true;
            }
            return false;
        }

        private static float GetBoneWeight(BoneWeight weight, int boneIndex)
        {
            float result = 0f;
            if (weight.boneIndex0 == boneIndex) result += weight.weight0;
            if (weight.boneIndex1 == boneIndex) result += weight.weight1;
            if (weight.boneIndex2 == boneIndex) result += weight.weight2;
            if (weight.boneIndex3 == boneIndex) result += weight.weight3;
            return result;
        }

        private static void FitSphere(SecondaryRigSphereCollider collider, List<Vector3> points)
        {
            Vector3 center = Average(points);
            float radius = 0f;
            for (int i = 0; i < points.Count; i++)
                radius = Mathf.Max(radius, Vector3.Distance(center, points[i]));

            radius *= Padding;
            collider.LocalCenter = collider.transform.InverseTransformPoint(center);
            collider.Radius = WorldToLocalRadius(collider.transform, radius, collider.Margin);
        }

        private static void FitCapsule(
            SecondaryRigCapsuleCollider collider,
            List<Vector3> points,
            Transform followBone)
        {
            Vector3 currentAxis = collider.WorldPointB - collider.WorldPointA;
            Vector3 axis = currentAxis.sqrMagnitude > 0.000001f
                ? currentAxis.normalized
                : FindBoneAxis(followBone);

            Vector3 center = Average(points);
            float minProjection = float.PositiveInfinity;
            float maxProjection = float.NegativeInfinity;
            float radialRadius = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 relative = points[i] - center;
                float projection = Vector3.Dot(relative, axis);
                minProjection = Mathf.Min(minProjection, projection);
                maxProjection = Mathf.Max(maxProjection, projection);
                Vector3 radial = relative - axis * projection;
                radialRadius = Mathf.Max(radialRadius, radial.magnitude);
            }

            radialRadius *= Padding;
            float segmentHalf = Mathf.Max(0f, (maxProjection - minProjection) * 0.5f - radialRadius * 0.5f);
            float segmentCenterProjection = (minProjection + maxProjection) * 0.5f;
            Vector3 segmentCenter = center + axis * segmentCenterProjection;
            Vector3 a = segmentCenter - axis * segmentHalf;
            Vector3 b = segmentCenter + axis * segmentHalf;

            collider.LocalPointA = collider.transform.InverseTransformPoint(a);
            collider.LocalPointB = collider.transform.InverseTransformPoint(b);
            collider.Radius = WorldToLocalRadius(collider.transform, radialRadius, collider.Margin);
        }

        private static Vector3 FindBoneAxis(Transform bone)
        {
            if (bone != null && bone.childCount > 0)
            {
                Transform bestChild = bone.GetChild(0);
                float bestDistance = Vector3.Distance(bone.position, bestChild.position);
                for (int i = 1; i < bone.childCount; i++)
                {
                    Transform child = bone.GetChild(i);
                    float distance = Vector3.Distance(bone.position, child.position);
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        bestChild = child;
                    }
                }

                Vector3 direction = bestChild.position - bone.position;
                if (direction.sqrMagnitude > 0.000001f)
                    return direction.normalized;
            }

            return bone != null ? bone.up : Vector3.up;
        }

        private static Vector3 Average(List<Vector3> points)
        {
            Vector3 result = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                result += points[i];
            return result / Mathf.Max(1, points.Count);
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
    }
}
#endif
