using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [ExecuteAlways]
    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class SecondaryRigColliderFollower : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 localPositionOffset;
        [SerializeField] private Vector3 localEulerOffset;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        public Vector3 LocalPositionOffset
        {
            get => localPositionOffset;
            set => localPositionOffset = value;
        }

        public Vector3 LocalEulerOffset
        {
            get => localEulerOffset;
            set => localEulerOffset = value;
        }

        private void OnEnable()
        {
            UpdateFollow();
        }

        private void LateUpdate()
        {
            UpdateFollow();
        }

        private void UpdateFollow()
        {
            if (followTarget == null)
                return;

            transform.position = followTarget.TransformPoint(localPositionOffset);
            transform.rotation = followTarget.rotation * Quaternion.Euler(localEulerOffset);
        }

        public void SnapAndClearOffsets()
        {
            if (followTarget == null)
                return;

            localPositionOffset = Vector3.zero;
            localEulerOffset = Vector3.zero;
            transform.SetPositionAndRotation(followTarget.position, followTarget.rotation);
        }

        public void SetWorldPoseAsOffsets(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (followTarget == null)
            {
                transform.SetPositionAndRotation(worldPosition, worldRotation);
                return;
            }

            localPositionOffset = followTarget.InverseTransformPoint(worldPosition);
            Quaternion relative = Quaternion.Inverse(followTarget.rotation) * worldRotation;
            localEulerOffset = relative.eulerAngles;
            UpdateFollow();
        }
    }
}
