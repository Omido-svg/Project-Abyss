using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [CreateAssetMenu(fileName = "MeleeAttack", menuName = "Abyss Weapon Framework/Attack/Melee Profile")]
    public sealed class MeleeAttackProfile : WeaponAttackProfile
    {
        [SerializeField, Min(0.001f)] private float radius = 0.035f;
        [SerializeField] private bool allowMultipleTargets = true;
        [SerializeField] private bool allowRepeatedHitPerWindow;
        [SerializeField, Min(1)] private int maxTargetsPerStep = 32;

        public override WeaponAttackKind Kind => WeaponAttackKind.Melee;
        public float Radius => radius;
        public bool AllowMultipleTargets => allowMultipleTargets;
        public bool AllowRepeatedHitPerWindow => allowRepeatedHitPerWindow;
        public int MaxTargetsPerStep => Mathf.Max(1, maxTargetsPerStep);
    }
}
