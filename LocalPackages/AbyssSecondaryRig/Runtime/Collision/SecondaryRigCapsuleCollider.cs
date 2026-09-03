using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [AddComponentMenu("Project Abyss/Secondary Rig/Capsule Collider")]
    public sealed class SecondaryRigCapsuleCollider : SecondaryRigCollider
    {
        [SerializeField] private Vector3 localPointA;
        [SerializeField] private Vector3 localPointB = new(0f, 0.2f, 0f);
        [SerializeField, Min(0f)] private float radius = 0.08f;

        public Vector3 LocalPointA
        {
            get => localPointA;
            set => localPointA = value;
        }

        public Vector3 LocalPointB
        {
            get => localPointB;
            set => localPointB = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public Vector3 WorldPointA => transform.TransformPoint(localPointA);
        public Vector3 WorldPointB => transform.TransformPoint(localPointB);
        public float WorldRadius => radius * MaxAbsComponent(transform.lossyScale) + Margin;

        public override bool ProjectOutside(ref Vector3 worldPoint, float particleRadius)
        {
            if (!CollisionEnabled)
                return false;

            Vector3 a = WorldPointA;
            Vector3 b = WorldPointB;
            Vector3 ab = b - a;
            float abLengthSqr = ab.sqrMagnitude;
            float t = 0f;

            if (abLengthSqr > 0.00000001f)
                t = Mathf.Clamp01(Vector3.Dot(worldPoint - a, ab) / abLengthSqr);

            Vector3 closest = a + ab * t;
            float minimumDistance = WorldRadius + Mathf.Max(0f, particleRadius);
            Vector3 delta = worldPoint - closest;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance >= minimumDistance * minimumDistance)
                return false;

            if (sqrDistance <= 0.00000001f)
            {
                Vector3 axis = abLengthSqr > 0.00000001f ? ab.normalized : transform.up;
                Vector3 fallback = Vector3.Cross(axis, transform.right);
                if (fallback.sqrMagnitude <= 0.000001f)
                    fallback = Vector3.Cross(axis, Vector3.forward);
                if (fallback.sqrMagnitude <= 0.000001f)
                    fallback = Vector3.up;

                worldPoint = closest + fallback.normalized * minimumDistance;
                return true;
            }

            worldPoint = closest + delta * (minimumDistance / Mathf.Sqrt(sqrDistance));
            return true;
        }

    }
}
