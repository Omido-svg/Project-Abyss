using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public readonly struct WeaponHitInfo
    {
        public WeaponAttackKind AttackKind { get; }
        public string AttackId { get; }
        public string DamageChannel { get; }
        public float Power { get; }
        public WeaponInstance SourceWeapon { get; }
        public WeaponDefinition WeaponDefinition { get; }
        public GameObject SourceOwner { get; }
        public Collider HitCollider { get; }
        public Rigidbody HitRigidbody { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector3 Direction { get; }
        public float Distance { get; }
        public int Sequence { get; }

        public WeaponHitInfo(
            WeaponAttackKind attackKind, string attackId, string damageChannel, float power,
            WeaponInstance sourceWeapon, GameObject sourceOwner, Collider hitCollider,
            Vector3 point, Vector3 normal, Vector3 direction, float distance, int sequence = 0)
        {
            AttackKind = attackKind;
            AttackId = attackId;
            DamageChannel = damageChannel;
            Power = power;
            SourceWeapon = sourceWeapon;
            WeaponDefinition = sourceWeapon != null ? sourceWeapon.Definition : null;
            SourceOwner = sourceOwner;
            HitCollider = hitCollider;
            HitRigidbody = hitCollider != null ? hitCollider.attachedRigidbody : null;
            Point = point;
            Normal = normal;
            Direction = direction;
            Distance = distance;
            Sequence = sequence;
        }
    }
}
