using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidWeaponIK : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private WeaponMountController mountController;
        [SerializeField] private bool applyPosition = true;
        [SerializeField] private bool applyRotation = true;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            mountController = GetComponent<WeaponMountController>();
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (mountController == null)
                mountController = GetComponent<WeaponMountController>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || mountController == null || !animator.isHuman)
                return;

            ResetGoal(AvatarIKGoal.LeftHand);
            ResetGoal(AvatarIKGoal.RightHand);

            IReadOnlyList<WeaponEquipHandle> handles = mountController.Equipped;
            for (int handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                WeaponEquipHandle handle = handles[handleIndex];
                if (handle == null)
                    continue;

                IReadOnlyList<ResolvedWeaponGripBinding> bindings = handle.Bindings;
                for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    ResolvedWeaponGripBinding binding = bindings[bindingIndex];
                    WeaponGripPoint grip = binding?.Grip;
                    WeaponSocket socket = binding?.Socket;

                    if (grip == null || socket == null || !grip.ApplyHumanoidIK)
                        continue;

                    if (!TryGetHandGoal(socket.Hand, out AvatarIKGoal goal, out HumanBodyBones boneId))
                        continue;

                    Transform handBone = animator.GetBoneTransform(boneId);
                    Transform socketMount = socket.MountTransform;
                    if (handBone == null || socketMount == null)
                        continue;

                    ResolveDesiredHandPose(
                        handBone,
                        socketMount,
                        grip.transform,
                        out Vector3 desiredPosition,
                        out Quaternion desiredRotation);

                    float positionWeight = applyPosition ? grip.IKPositionWeight : 0f;
                    float rotationWeight = applyRotation ? grip.IKRotationWeight : 0f;

                    animator.SetIKPositionWeight(goal, positionWeight);
                    animator.SetIKRotationWeight(goal, rotationWeight);

                    if (positionWeight > 0f)
                        animator.SetIKPosition(goal, desiredPosition);

                    if (rotationWeight > 0f)
                        animator.SetIKRotation(goal, desiredRotation);
                }
            }
        }

        private void ResetGoal(AvatarIKGoal goal)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
        }

        private static bool TryGetHandGoal(
            WeaponHand hand,
            out AvatarIKGoal goal,
            out HumanBodyBones bone)
        {
            switch (hand)
            {
                case WeaponHand.Left:
                    goal = AvatarIKGoal.LeftHand;
                    bone = HumanBodyBones.LeftHand;
                    return true;

                case WeaponHand.Right:
                    goal = AvatarIKGoal.RightHand;
                    bone = HumanBodyBones.RightHand;
                    return true;

                default:
                    goal = default;
                    bone = default;
                    return false;
            }
        }

        private static void ResolveDesiredHandPose(
            Transform handBone,
            Transform socketMount,
            Transform desiredSocketPose,
            out Vector3 desiredHandPosition,
            out Quaternion desiredHandRotation)
        {
            Quaternion deltaRotation =
                desiredSocketPose.rotation * Quaternion.Inverse(socketMount.rotation);

            desiredHandRotation = deltaRotation * handBone.rotation;

            Vector3 handToSocket = socketMount.position - handBone.position;
            Vector3 rotatedOffset = deltaRotation * handToSocket;
            desiredHandPosition = desiredSocketPose.position - rotatedOffset;
        }
    }
}
