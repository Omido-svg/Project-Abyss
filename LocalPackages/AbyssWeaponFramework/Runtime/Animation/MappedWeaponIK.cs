using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class MappedWeaponIK : MonoBehaviour
    {
        [SerializeField] private WeaponSkeletonMap skeletonMap;
        [SerializeField] private WeaponMountController mountController;
        [SerializeField, Range(1, 8)] private int iterations = 3;
        [SerializeField] private bool applyRotation = true;

        private void Reset()
        {
            skeletonMap = GetComponent<WeaponSkeletonMap>();
            mountController = GetComponent<WeaponMountController>();
        }

        private void LateUpdate()
        {
            if (skeletonMap == null || mountController == null || skeletonMap.IsHumanoid) return;
            IReadOnlyList<WeaponEquipHandle> handles = mountController.Equipped;
            for (int h = 0; h < handles.Count; h++)
            {
                WeaponEquipHandle handle = handles[h];
                if (handle == null) continue;
                for (int b = 0; b < handle.Bindings.Count; b++)
                {
                    ResolvedWeaponGripBinding binding = handle.Bindings[b];
                    if (binding?.Grip == null || binding.Socket == null || !binding.Grip.ApplyHumanoidIK) continue;
                    if (binding.Socket.Hand == WeaponHand.Any) continue;
                    Solve(binding);
                }
            }
        }

        private void Solve(ResolvedWeaponGripBinding binding)
        {
            WeaponHand hand = binding.Socket.Hand;
            WeaponBoneId upperId = hand == WeaponHand.Left ? WeaponBoneId.LeftUpperArm : WeaponBoneId.RightUpperArm;
            WeaponBoneId lowerId = hand == WeaponHand.Left ? WeaponBoneId.LeftLowerArm : WeaponBoneId.RightLowerArm;
            WeaponBoneId handId = hand == WeaponHand.Left ? WeaponBoneId.LeftHand : WeaponBoneId.RightHand;
            Transform upper = skeletonMap.GetBone(upperId);
            Transform lower = skeletonMap.GetBone(lowerId);
            Transform tip = skeletonMap.GetBone(handId);
            if (upper == null || lower == null || tip == null) return;

            Transform socketMount = binding.Socket.MountTransform;
            Transform target = binding.Grip.transform;
            ResolveDesiredHandPose(tip, socketMount, target, out Vector3 targetPos, out Quaternion targetRot);
            float w = binding.Grip.IKPositionWeight;

            for (int i = 0; i < iterations; i++)
            {
                RotateJointTowards(lower, tip.position, targetPos, w);
                RotateJointTowards(upper, tip.position, targetPos, w);
            }
            if (applyRotation) tip.rotation = Quaternion.Slerp(tip.rotation, targetRot, binding.Grip.IKRotationWeight);
        }

        private static void RotateJointTowards(Transform joint, Vector3 tip, Vector3 target, float weight)
        {
            Vector3 from = tip - joint.position;
            Vector3 to = target - joint.position;
            if (from.sqrMagnitude < 0.000001f || to.sqrMagnitude < 0.000001f) return;
            Quaternion delta = Quaternion.FromToRotation(from, to);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * joint.rotation;
        }

        private static void ResolveDesiredHandPose(Transform handBone, Transform socketMount, Transform desiredSocketPose, out Vector3 desiredHandPosition, out Quaternion desiredHandRotation)
        {
            Quaternion deltaRotation = desiredSocketPose.rotation * Quaternion.Inverse(socketMount.rotation);
            desiredHandRotation = deltaRotation * handBone.rotation;
            Vector3 handToSocket = socketMount.position - handBone.position;
            desiredHandPosition = desiredSocketPose.position - deltaRotation * handToSocket;
        }
    }
}
