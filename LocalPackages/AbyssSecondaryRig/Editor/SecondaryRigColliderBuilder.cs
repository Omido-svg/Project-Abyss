#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigColliderBuilder
    {
        private const string GeneratedRootName = "SecondaryRigColliders";

        public static int CreateRecommendedHumanoidColliders(GameObject characterRoot, bool replaceExisting)
        {
            if (characterRoot == null)
                return 0;

            Animator animator = characterRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException("A valid Humanoid Animator is required for automatic collider generation.");

            Transform existingRoot = characterRoot.transform.Find(GeneratedRootName);
            if (replaceExisting && existingRoot != null)
                UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);

            Transform root = existingRoot;
            if (root == null)
            {
                GameObject rootObject = new(GeneratedRootName);
                rootObject.transform.SetParent(characterRoot.transform, false);
                root = rootObject.transform;
            }

            float height = EstimateCharacterHeight(characterRoot);
            int created = 0;

            created += EnsureHeadSphere(animator, root, height);
            created += EnsureTorsoCapsule(animator, root, height);
            created += EnsureHipsCapsule(animator, root, height);
            created += EnsureLimbCapsule(animator, root, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, "LeftThigh", height * 0.045f);
            created += EnsureLimbCapsule(animator, root, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "RightThigh", height * 0.045f);
            created += EnsureShoulderSphere(animator, root, HumanBodyBones.LeftUpperArm, "LeftShoulder", height * 0.045f);
            created += EnsureShoulderSphere(animator, root, HumanBodyBones.RightUpperArm, "RightShoulder", height * 0.045f);

            return created;
        }

        private static int EnsureHeadSphere(Animator animator, Transform root, float height)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                return 0;

            GameObject holder = GetOrCreateHolder(root, "Head", head);
            SecondaryRigSphereCollider collider = holder.GetComponent<SecondaryRigSphereCollider>();
            if (collider != null)
                return 0;

            collider = holder.AddComponent<SecondaryRigSphereCollider>();
            Vector3 up = GetCharacterUp(animator);
            Vector3 centerWorld = head.position + up * (height * 0.035f);
            collider.LocalCenter = holder.transform.InverseTransformPoint(centerWorld);
            collider.Radius = height * 0.055f;
            return 1;
        }

        private static int EnsureTorsoCapsule(Animator animator, Transform root, float height)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                              animator.GetBoneTransform(HumanBodyBones.Chest) ??
                              animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ??
                             animator.GetBoneTransform(HumanBodyBones.Head);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (chest == null || neck == null || spine == null)
                return 0;

            GameObject holder = GetOrCreateHolder(root, "Torso", chest);
            SecondaryRigCapsuleCollider collider = holder.GetComponent<SecondaryRigCapsuleCollider>();
            if (collider != null)
                return 0;

            collider = holder.AddComponent<SecondaryRigCapsuleCollider>();
            collider.LocalPointA = holder.transform.InverseTransformPoint(spine.position);
            collider.LocalPointB = holder.transform.InverseTransformPoint(neck.position);
            collider.Radius = height * 0.075f;
            return 1;
        }

        private static int EnsureHipsCapsule(Animator animator, Transform root, float height)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (hips == null || spine == null)
                return 0;

            Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Vector3 bottom = hips.position;
            if (leftUpperLeg != null && rightUpperLeg != null)
                bottom = (leftUpperLeg.position + rightUpperLeg.position) * 0.5f;

            GameObject holder = GetOrCreateHolder(root, "Hips", hips);
            SecondaryRigCapsuleCollider collider = holder.GetComponent<SecondaryRigCapsuleCollider>();
            if (collider != null)
                return 0;

            collider = holder.AddComponent<SecondaryRigCapsuleCollider>();
            collider.LocalPointA = holder.transform.InverseTransformPoint(bottom);
            collider.LocalPointB = holder.transform.InverseTransformPoint(spine.position);
            collider.Radius = height * 0.075f;
            return 1;
        }

        private static int EnsureLimbCapsule(
            Animator animator,
            Transform root,
            HumanBodyBones upperBone,
            HumanBodyBones lowerBone,
            string name,
            float radius)
        {
            Transform upper = animator.GetBoneTransform(upperBone);
            Transform lower = animator.GetBoneTransform(lowerBone);
            if (upper == null || lower == null)
                return 0;

            GameObject holder = GetOrCreateHolder(root, name, upper);
            SecondaryRigCapsuleCollider collider = holder.GetComponent<SecondaryRigCapsuleCollider>();
            if (collider != null)
                return 0;

            collider = holder.AddComponent<SecondaryRigCapsuleCollider>();
            collider.LocalPointA = Vector3.zero;
            collider.LocalPointB = holder.transform.InverseTransformPoint(lower.position);
            collider.Radius = radius;
            return 1;
        }

        private static int EnsureShoulderSphere(
            Animator animator,
            Transform root,
            HumanBodyBones upperArmBone,
            string name,
            float radius)
        {
            Transform upperArm = animator.GetBoneTransform(upperArmBone);
            if (upperArm == null)
                return 0;

            GameObject holder = GetOrCreateHolder(root, name, upperArm);
            SecondaryRigSphereCollider collider = holder.GetComponent<SecondaryRigSphereCollider>();
            if (collider != null)
                return 0;

            collider = holder.AddComponent<SecondaryRigSphereCollider>();
            collider.LocalCenter = Vector3.zero;
            collider.Radius = radius;
            return 1;
        }

        private static GameObject GetOrCreateHolder(Transform root, string name, Transform followBone)
        {
            Transform existing = root.Find(name);
            GameObject holder;

            if (existing != null)
            {
                holder = existing.gameObject;
            }
            else
            {
                holder = new GameObject(name);
                holder.transform.SetParent(root, false);
            }

            SecondaryRigColliderFollower follower = holder.GetComponent<SecondaryRigColliderFollower>();
            if (follower == null)
                follower = holder.AddComponent<SecondaryRigColliderFollower>();

            follower.FollowTarget = followBone;
            holder.transform.position = followBone.position;
            holder.transform.rotation = followBone.rotation;
            holder.transform.localScale = Vector3.one;
            return holder;
        }

        private static float EstimateCharacterHeight(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds && bounds.size.y > 0.1f)
                return Mathf.Clamp(bounds.size.y, 0.5f, 4f);

            return 1.7f;
        }

        private static Vector3 GetCharacterUp(Animator animator)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips != null && head != null)
            {
                Vector3 direction = head.position - hips.position;
                if (direction.sqrMagnitude > 0.001f)
                    return direction.normalized;
            }

            return animator.transform.up;
        }
    }
}
#endif
