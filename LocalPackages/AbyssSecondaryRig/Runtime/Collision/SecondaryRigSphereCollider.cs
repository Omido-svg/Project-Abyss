using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [AddComponentMenu("Project Abyss/Secondary Rig/Sphere Collider")]
    public sealed class SecondaryRigSphereCollider : SecondaryRigCollider
    {
        [SerializeField] private Vector3 localCenter;
        [SerializeField, Min(0f)] private float radius = 0.1f;

        public Vector3 LocalCenter
        {
            get => localCenter;
            set => localCenter = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public Vector3 WorldCenter => transform.TransformPoint(localCenter);
        public float WorldRadius => radius * MaxAbsComponent(transform.lossyScale) + Margin;

        public override bool ProjectOutside(ref Vector3 worldPoint, float particleRadius)
        {
            if (!CollisionEnabled)
                return false;

            Vector3 center = WorldCenter;
            float minimumDistance = WorldRadius + Mathf.Max(0f, particleRadius);
            Vector3 delta = worldPoint - center;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance >= minimumDistance * minimumDistance)
                return false;

            if (sqrDistance <= 0.00000001f)
            {
                Vector3 fallback = transform.up.sqrMagnitude > 0.001f ? transform.up : Vector3.up;
                worldPoint = center + fallback.normalized * minimumDistance;
                return true;
            }

            worldPoint = center + delta * (minimumDistance / Mathf.Sqrt(sqrDistance));
            return true;
        }

    }
}
