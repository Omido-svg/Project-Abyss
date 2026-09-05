using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class ProjectileWeaponModule : WeaponAttackModule
    {
        [SerializeField] private WeaponPoint originPoint;

        public override WeaponAttackKind Kind => WeaponAttackKind.Projectile;

        protected override bool CanAttack(in WeaponAttackRequest request)
            => base.CanAttack(in request) && Profile is ProjectileAttackProfile profile && profile.ProjectilePrefab != null;

        protected override bool ExecuteAttack(in WeaponAttackRequest request)
        {
            if (Profile is not ProjectileAttackProfile profile || profile.ProjectilePrefab == null) return false;
            WeaponInstance weapon = GetComponentInParent<WeaponInstance>();
            Transform origin = ResolveOrigin(weapon);
            Ray aim = WeaponAimUtility.ResolveAim(this, in request, origin);
            GameObject owner = ResolveOwner(in request);

            GameObject clone = Instantiate(profile.ProjectilePrefab, aim.origin, Quaternion.LookRotation(aim.direction));
            WeaponProjectile projectile = clone.GetComponent<WeaponProjectile>();
            if (projectile == null) projectile = clone.AddComponent<WeaponProjectile>();

            Vector3 inheritedVelocity = Vector3.zero;
            if (profile.InheritOwnerVelocity && owner != null)
            {
                Rigidbody body = owner.GetComponentInParent<Rigidbody>();
                if (body != null) inheritedVelocity = body.linearVelocity;
            }

            // WeaponProjectile owns damage delivery. Forward the same hit to the common
            // WeaponAttackModule event path so effects/listeners observe projectile hits too.
            projectile.Hit += OnProjectileHit;

            projectile.Initialize(new WeaponProjectileSpawnData(
                weapon,
                owner,
                string.IsNullOrWhiteSpace(request.RuntimeAttackId) ? profile.AttackId : request.RuntimeAttackId,
                profile.DamageChannel,
                profile.Power,
                aim.direction * profile.Speed + inheritedVelocity,
                profile.Gravity,
                profile.Lifetime,
                profile.CollisionRadius,
                profile.HitMask,
                profile.TriggerInteraction));
            return true;
        }

        private void OnProjectileHit(WeaponHitInfo hit)
        {
            // Damage is already delivered by WeaponProjectile.FixedUpdate().
            PublishHit(in hit, deliverToReceiver: false);
        }

        private Transform ResolveOrigin(WeaponInstance weapon)
        {
            if (originPoint != null) return originPoint.transform;
            if (weapon != null)
            {
                WeaponPoint point = weapon.FindPoint(WeaponPointType.ProjectileOrigin) ?? weapon.FindPoint(WeaponPointType.Muzzle);
                if (point != null) return point.transform;
            }
            return transform;
        }
    }
}