using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public readonly struct WeaponProjectileSpawnData
    {
        public WeaponInstance SourceWeapon { get; }
        public GameObject Owner { get; }
        public string AttackId { get; }
        public string DamageChannel { get; }
        public float Power { get; }
        public Vector3 Velocity { get; }
        public Vector3 Gravity { get; }
        public float Lifetime { get; }
        public float Radius { get; }
        public LayerMask HitMask { get; }
        public QueryTriggerInteraction TriggerInteraction { get; }

        public WeaponProjectileSpawnData(WeaponInstance sourceWeapon, GameObject owner, string attackId, string damageChannel, float power, Vector3 velocity, Vector3 gravity, float lifetime, float radius, LayerMask hitMask, QueryTriggerInteraction triggerInteraction)
        {
            SourceWeapon = sourceWeapon; Owner = owner; AttackId = attackId; DamageChannel = damageChannel; Power = power;
            Velocity = velocity; Gravity = gravity; Lifetime = lifetime; Radius = radius; HitMask = hitMask; TriggerInteraction = triggerInteraction;
        }
    }

    [DisallowMultipleComponent]
    public class WeaponProjectile : MonoBehaviour
    {
        public event Action<WeaponHitInfo> Hit;
        public Vector3 Velocity { get; private set; }
        public bool IsInitialized { get; private set; }

        private WeaponProjectileSpawnData data;
        private float remainingLife;
        private Vector3 previousPosition;
        private int sequence;

        public virtual void Initialize(WeaponProjectileSpawnData spawnData)
        {
            data = spawnData;
            Velocity = spawnData.Velocity;
            remainingLife = spawnData.Lifetime;
            previousPosition = transform.position;
            IsInitialized = true;
        }

        protected virtual void FixedUpdate()
        {
            if (!IsInitialized) return;
            float dt = Time.fixedDeltaTime;
            Velocity += data.Gravity * dt;
            Vector3 next = transform.position + Velocity * dt;
            Vector3 delta = next - previousPosition;
            float distance = delta.magnitude;

            if (distance > 0.000001f)
            {
                RaycastHit[] hits = data.Radius > 0f
                    ? Physics.SphereCastAll(previousPosition, data.Radius, delta / distance, distance, data.HitMask, data.TriggerInteraction)
                    : Physics.RaycastAll(previousPosition, delta / distance, distance, data.HitMask, data.TriggerInteraction);
                Array.Sort(hits, static (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit raw = hits[i];
                    if (WeaponHitQueryUtility.IsSelf(raw.collider, data.SourceWeapon, data.Owner)) continue;
                    if (!WeaponHitQueryUtility.PassesOwnerFilters(raw.collider, data.SourceWeapon, data.Owner)) continue;
                    transform.position = raw.point;
                    WeaponHitInfo hit = new(WeaponAttackKind.Projectile, data.AttackId, data.DamageChannel, data.Power, data.SourceWeapon, data.Owner, raw.collider, raw.point, raw.normal, Velocity.normalized, raw.distance, sequence++);
                    Hit?.Invoke(hit);
                    WeaponDamageUtility.Deliver(in hit);
                    OnImpact(in hit);
                    return;
                }
            }

            transform.position = next;
            if (Velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(Velocity.normalized);
            previousPosition = next;
            remainingLife -= dt;
            if (remainingLife <= 0f) OnExpired();
        }

        protected virtual void OnImpact(in WeaponHitInfo hit) => Destroy(gameObject);
        protected virtual void OnExpired() => Destroy(gameObject);
    }
}
