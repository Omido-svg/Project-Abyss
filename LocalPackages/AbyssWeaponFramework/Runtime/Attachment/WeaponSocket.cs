using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponSocket : MonoBehaviour
    {
        [SerializeField] private string socketId = "RightHand";
        [SerializeField] private WeaponSocketType socketType = WeaponSocketType.Hand;
        [SerializeField] private WeaponHand hand = WeaponHand.Right;
        [SerializeField] private Transform mountTransform;
        [SerializeField] private bool allowAutoSelection = true;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private bool drawGizmo = true;
        [SerializeField, Min(0.001f)] private float gizmoSize = 0.035f;

        private WeaponEquipHandle occupant;

        public string SocketId => socketId;
        public WeaponSocketType SocketType => socketType;
        public WeaponHand Hand => hand;
        public Transform MountTransform => mountTransform != null ? mountTransform : transform;
        public bool AllowAutoSelection => allowAutoSelection;
        public bool IsOccupied => occupant != null;
        public WeaponEquipHandle Occupant => occupant;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
                return false;

            for (int index = 0; index < tags.Length; index++)
            {
                if (string.Equals(tags[index], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal bool TryOccupy(WeaponEquipHandle handle)
        {
            if (handle == null)
                return false;

            if (occupant != null && occupant != handle)
                return false;

            occupant = handle;
            return true;
        }

        internal void Release(WeaponEquipHandle handle)
        {
            if (occupant == handle)
                occupant = null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newSocketId,
            WeaponSocketType newSocketType,
            WeaponHand newHand)
        {
            socketId = newSocketId;
            socketType = newSocketType;
            hand = newHand;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            Transform target = MountTransform;
            float size = Mathf.Max(0.001f, gizmoSize);

            Gizmos.matrix = Matrix4x4.TRS(target.position, target.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size * 2f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
