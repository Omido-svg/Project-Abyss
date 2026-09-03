using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public readonly struct WeaponAttackRequest
    {
        public Vector3? Origin { get; }
        public Vector3? Direction { get; }
        public Vector3? TargetPoint { get; }
        public GameObject InstigatorOverride { get; }
        public string RuntimeAttackId { get; }

        public WeaponAttackRequest(Vector3? origin = null, Vector3? direction = null, Vector3? targetPoint = null, GameObject instigatorOverride = null, string runtimeAttackId = null)
        {
            Origin = origin;
            Direction = direction;
            TargetPoint = targetPoint;
            InstigatorOverride = instigatorOverride;
            RuntimeAttackId = runtimeAttackId;
        }
    }
}
