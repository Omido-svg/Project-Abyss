using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class HitscanWeaponModule : WeaponAttackModule
    {
        [SerializeField] private WeaponPoint originPoint;
        [SerializeField] private bool useMuzzlePointWhenOriginMissing = true;

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

            for (int pellet = 0; pellet < profile.Pellets; pellet++)
            {
                Vector3 direction = ApplySpread(baseRay.direction, profile.SpreadDegrees);
                RaycastHit[] hits = profile.SphereRadius > 0f
                    ? Physics.SphereCastAll(baseRay.origin, profile.SphereRadius, direction, profile.Range, profile.HitMask, profile.TriggerInteraction)
                    : Physics.RaycastAll(baseRay.origin, direction, profile.Range, profile.HitMask, profile.TriggerInteraction);

                Array.Sort(hits, static (a, b) => a.distance.CompareTo(b.distance));
                int penetrations = 0;
                for (int i = 0; i < hits.Length && penetrations < profile.MaxPenetrations; i++)
                {
                    RaycastHit raw = hits[i];
                    if (WeaponHitQueryUtility.IsSelf(raw.collider, weapon, owner)) continue;
                    if (!WeaponHitQueryUtility.PassesOwnerFilters(raw.collider, weapon, owner)) continue;

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
