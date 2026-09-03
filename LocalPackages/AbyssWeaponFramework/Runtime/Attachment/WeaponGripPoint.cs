using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponGripPoint : MonoBehaviour
    {
        [SerializeField] private string gripId = "Grip_Main";
        [SerializeField] private WeaponGripRole role = WeaponGripRole.Primary;
        [SerializeField] private bool requiredForAutoEquip = true;

        [Header("Socket Requirements")]
        [SerializeField] private WeaponSocketType requiredSocketType = WeaponSocketType.Hand;
        [SerializeField] private WeaponHand preferredHand = WeaponHand.Any;
        [SerializeField] private string requiredSocketId;
        [SerializeField] private string[] requiredSocketTags = Array.Empty<string>();

        [Header("Humanoid IK")]
        [Tooltip("Support grip 등에 사용. Primary grip은 보통 무기가 손에 붙기 때문에 꺼 둡니다.")]
        [SerializeField] private bool applyHumanoidIK;
        [SerializeField, Range(0f, 1f)] private float ikPositionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float ikRotationWeight = 1f;

        public string GripId => gripId;
        public WeaponGripRole Role => role;
        public bool RequiredForAutoEquip => requiredForAutoEquip;
        public WeaponSocketType RequiredSocketType => requiredSocketType;
        public WeaponHand PreferredHand => preferredHand;
        public string RequiredSocketId => requiredSocketId;
        public bool ApplyHumanoidIK => applyHumanoidIK;
        public float IKPositionWeight => ikPositionWeight;
        public float IKRotationWeight => ikRotationWeight;

        public bool IsCompatible(WeaponSocket socket)
        {
            if (socket == null)
                return false;

            if (!string.IsNullOrWhiteSpace(requiredSocketId) &&
                !string.Equals(requiredSocketId, socket.SocketId, StringComparison.Ordinal))
            {
                return false;
            }

            if (requiredSocketType != WeaponSocketType.Any &&
                socket.SocketType != requiredSocketType)
            {
                return false;
            }

            if (preferredHand != WeaponHand.Any &&
                socket.Hand != WeaponHand.Any &&
                socket.Hand != preferredHand)
            {
                return false;
            }

            if (requiredSocketTags != null)
            {
                for (int index = 0; index < requiredSocketTags.Length; index++)
                {
                    string tag = requiredSocketTags[index];
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;

                    if (!socket.HasTag(tag))
                        return false;
                }
            }

            return true;
        }

        public int GetCompatibilityScore(WeaponSocket socket)
        {
            if (!IsCompatible(socket))
                return int.MinValue;

            int score = 0;

            if (!string.IsNullOrWhiteSpace(requiredSocketId) &&
                string.Equals(requiredSocketId, socket.SocketId, StringComparison.Ordinal))
            {
                score += 1000;
            }

            if (preferredHand != WeaponHand.Any && socket.Hand == preferredHand)
                score += 200;

            if (requiredSocketType != WeaponSocketType.Any && socket.SocketType == requiredSocketType)
                score += 100;

            if (role == WeaponGripRole.Primary && socket.SocketType == WeaponSocketType.Hand)
                score += 25;

            return score;
        }

        private void OnDrawGizmosSelected()
        {
            const float size = 0.03f;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size * 2.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newGripId,
            WeaponGripRole newRole,
            bool newRequiredForAutoEquip,
            WeaponSocketType newRequiredSocketType,
            WeaponHand newPreferredHand,
            bool newApplyHumanoidIK)
        {
            gripId = newGripId;
            role = newRole;
            requiredForAutoEquip = newRequiredForAutoEquip;
            requiredSocketType = newRequiredSocketType;
            preferredHand = newPreferredHand;
            applyHumanoidIK = newApplyHumanoidIK;
        }
#endif
    }
}
