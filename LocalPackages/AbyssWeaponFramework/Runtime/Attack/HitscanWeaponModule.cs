using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class HitscanWeaponModule : WeaponAttackModule
    {
        [SerializeField] private WeaponPoint originPoint;
        [SerializeField] private bool useMuzzlePointWhenOriginMissing = true;

        private RaycastHit[] hitBuffer = new RaycastHit[32];
        private readonly List<MonoBehaviour> ownerBehaviours = new();

        public override WeaponAttackKind Kind => WeaponAttackKind.Hitscan;

        protected override bool CanAttack(in WeaponAttackRequest request)
            => base.CanAttack(in request) && Profile is HitscanAttackProfile;

        protected override bool ExecuteAttack(in WeaponAttackRequest request)
        {
            if (Profile is not HitscanAttackProfile profile) return false;
            WeaponInstance weapon = GetComponentInParent<WeaponInstance>();
            Transform origin = ResolveOrigin(weapon);
            Ray baseRay = WeaponAimUtility.ResolveAim(this, in request, origin);
            GameObject owner = ResolveOwner(in request);
            int sequence = 0;
            bool any = false;
            WeaponHitQueryUtility.RefreshOwnerBehaviours(owner, ownerBehaviours);

            for (int pellet = 0; pellet < profile.Pellets; pellet++)
            {
                Vector3 direction = ApplySpread(baseRay.direction, profile.SpreadDegrees);
                int hitCount = WeaponPhysicsQueryUtility.CastSorted(
                    baseRay.origin,
                    profile.SphereRadius,
                    direction,
                    profile.Range,
                    profile.HitMask,
                    profile.TriggerInteraction,
                    ref hitBuffer);

                int penetrations = 0;
                for (int i = 0; i < hitCount && penetrations < profile.MaxPenetrations; i++)
                {
                    RaycastHit raw = hitBuffer[i];
                    if (WeaponHitQueryUtility.IsSelf(raw.collider, weapon, owner)) continue;
                    if (!WeaponHitQueryUtility.PassesOwnerFilters(raw.collider, weapon, ownerBehaviours)) continue;

                    WeaponHitInfo hit = new(
                        WeaponAttackKind.Hitscan,
                        string.IsNullOrWhiteSpace(request.RuntimeAttackId) ? profile.AttackId : request.RuntimeAttackId,
                        profile.DamageChannel,
                        profile.Power,
                        weapon,
                        owner,
                        raw.collider,
                        raw.point,
                        raw.normal,
                        direction,
                        raw.distance,
                        sequence++);

                    bool receiver = WeaponHitQueryUtility.HasReceiver(raw.collider);
                    PublishHit(in hit);
                    any = true;
                    penetrations++;
                    if (profile.StopOnFirstDamageReceiver && receiver) break;
                }
            }
            return any || profile.Pellets > 0;
        }

        private Transform ResolveOrigin(WeaponInstance weapon)
        {
            if (originPoint != null) return originPoint.transform;
            if (useMuzzlePointWhenOriginMissing && weapon != null)
            {
                WeaponPoint point = weapon.FindPoint(WeaponPointType.Muzzle) ?? weapon.FindPoint(WeaponPointType.ProjectileOrigin);
                if (point != null) return point.transform;
            }
            return transform;
        }

        private static Vector3 ApplySpread(Vector3 direction, float degrees)
        {
            if (degrees <= 0f) return direction.normalized;
            Quaternion baseRotation = Quaternion.LookRotation(direction.normalized);
            Vector2 circle = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (baseRotation * new Vector3(circle.x, circle.y, 1f)).normalized;
        }
    }
}