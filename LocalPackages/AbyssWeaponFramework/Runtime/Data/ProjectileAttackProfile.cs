using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [CreateAssetMenu(fileName = "ProjectileAttack", menuName = "Abyss Weapon Framework/Attack/Projectile Profile")]
    public sealed class ProjectileAttackProfile : WeaponAttackProfile
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0f)] private float speed = 30f;
        [SerializeField] private Vector3 gravity = new(0f, -9.81f, 0f);
        [SerializeField, Min(0.01f)] private float lifetime = 10f;
        [SerializeField, Min(0f)] private float collisionRadius = 0.02f;
        [SerializeField] private bool inheritOwnerVelocity;

        public override WeaponAttackKind Kind => WeaponAttackKind.Projectile;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float Speed => speed;
        public Vector3 Gravity => gravity;
        public float Lifetime => lifetime;
        public float CollisionRadius => collisionRadius;
        public bool InheritOwnerVelocity => inheritOwnerVelocity;
    }
}
