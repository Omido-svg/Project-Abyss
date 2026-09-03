using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public abstract class WeaponAttackProfile : ScriptableObject
    {
        [SerializeField] private string attackId = "primary";
        [SerializeField] private string damageChannel = "default";
        [SerializeField, Min(0f)] private float power = 1f;
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private WeaponQueryTriggerInteraction triggerInteraction = WeaponQueryTriggerInteraction.Ignore;

        public string AttackId => attackId;
        public string DamageChannel => damageChannel;
        public float Power => power;
        public float Cooldown => cooldown;
        public LayerMask HitMask => hitMask;
        public QueryTriggerInteraction TriggerInteraction => WeaponPhysicsUtility.ToUnity(triggerInteraction);
        public abstract WeaponAttackKind Kind { get; }
    }
}
