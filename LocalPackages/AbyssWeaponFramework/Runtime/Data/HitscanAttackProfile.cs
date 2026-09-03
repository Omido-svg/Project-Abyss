using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [CreateAssetMenu(fileName = "HitscanAttack", menuName = "Abyss Weapon Framework/Attack/Hitscan Profile")]
    public sealed class HitscanAttackProfile : WeaponAttackProfile
    {
        [SerializeField, Min(0.01f)] private float range = 100f;
        [SerializeField, Min(1)] private int pellets = 1;
        [SerializeField, Min(0f)] private float spreadDegrees;
        [SerializeField, Min(0f)] private float sphereRadius;
        [SerializeField, Min(1)] private int maxPenetrations = 1;
        [SerializeField] private bool stopOnFirstDamageReceiver = true;

        public override WeaponAttackKind Kind => WeaponAttackKind.Hitscan;
        public float Range => range;
        public int Pellets => Mathf.Max(1, pellets);
        public float SpreadDegrees => spreadDegrees;
        public float SphereRadius => sphereRadius;
        public int MaxPenetrations => Mathf.Max(1, maxPenetrations);
        public bool StopOnFirstDamageReceiver => stopOnFirstDamageReceiver;
    }
}
