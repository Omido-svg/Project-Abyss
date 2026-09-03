using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    public abstract class SecondaryRigCollider : MonoBehaviour
    {
        [SerializeField] private SecondaryCollisionLayer layer = SecondaryCollisionLayer.Body;
        [SerializeField, Min(0f)] private float margin;
        [SerializeField] private bool collisionEnabled = true;

        public SecondaryCollisionLayer Layer => layer;
        public float Margin => Mathf.Max(0f, margin);
        public bool CollisionEnabled => collisionEnabled && isActiveAndEnabled;

        public abstract bool ProjectOutside(ref Vector3 worldPoint, float particleRadius);

        protected static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
