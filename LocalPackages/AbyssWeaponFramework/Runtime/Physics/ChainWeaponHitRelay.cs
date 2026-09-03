using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    /// <summary>Optional collision-based relay for physical chain links.</summary>
    [DisallowMultipleComponent]
    public sealed class ChainWeaponHitRelay : MonoBehaviour
    {
        [SerializeField] private WeaponInstance sourceWeapon;
        [SerializeField] private string attackId = "chain";
        [SerializeField] private string damageChannel = "default";
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 1f;
        [SerializeField, Min(0f)] private float powerScale = 1f;
        [SerializeField, Min(0f)] private float cooldownPerCollider = 0.1f;
        private readonly System.Collections.Generic.Dictionary<Collider, float> lastHit = new();

        private void Reset() => sourceWeapon = GetComponentInParent<WeaponInstance>();

        private void OnCollisionEnter(Collision collision)
        {
            if (sourceWeapon == null || collision.collider == null) return;
            GameObject owner = sourceWeapon.RuntimeContext?.OwnerObject;
            if (WeaponHitQueryUtility.IsSelf(collision.collider, sourceWeapon, owner)) return;
            if (!WeaponHitQueryUtility.PassesOwnerFilters(collision.collider, sourceWeapon, owner)) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minimumImpactSpeed) return;
            if (lastHit.TryGetValue(collision.collider, out float time) && Time.time - time < cooldownPerCollider) return;
            lastHit[collision.collider] = Time.time;
            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            Vector3 point = collision.contactCount > 0 ? contact.point : collision.collider.ClosestPoint(transform.position);
            Vector3 normal = collision.contactCount > 0 ? contact.normal : -collision.relativeVelocity.normalized;
            WeaponHitInfo hit = new(WeaponAttackKind.Melee, attackId, damageChannel, speed * powerScale, sourceWeapon, owner, collision.collider, point, normal, collision.relativeVelocity.normalized, 0f);
            WeaponDamageUtility.Deliver(in hit);
        }
    }
}
