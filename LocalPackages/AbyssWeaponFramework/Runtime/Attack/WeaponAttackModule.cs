using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public abstract class WeaponAttackModule : WeaponModule
    {
        [SerializeField] private WeaponAttackProfile profile;
        [SerializeField] private bool enabledForAttack = true;
        [SerializeField] private bool allowWhenNotEquipped;

        public event Action<WeaponAttackModule> AttackStarted;
        public event Action<WeaponAttackModule> AttackCompleted;
        public event Action<WeaponHitInfo> HitProduced;

        public WeaponAttackProfile Profile => profile;
        public string AttackId => profile != null ? profile.AttackId : string.Empty;
        public bool EnabledForAttack { get => enabledForAttack; set => enabledForAttack = value; }
        public abstract WeaponAttackKind Kind { get; }
        public float CooldownRemaining => profile == null ? 0f : Mathf.Max(0f, nextAllowedTime - Time.time);

        private float nextAllowedTime;

        public bool TryAttack(in WeaponAttackRequest request)
        {
            // A disabled component or inactive GameObject must never be executable through
            // a cached WeaponAttackController reference.
            if (!isActiveAndEnabled || !enabledForAttack || profile == null ||
                Time.time < nextAllowedTime || !CanAttack(in request))
            {
                return false;
            }
            AttackStarted?.Invoke(this);
            bool result = ExecuteAttack(in request);
            if (result) nextAllowedTime = Time.time + profile.Cooldown;
            if (result && CompletesImmediately) AttackCompleted?.Invoke(this);
            return result;
        }

        protected virtual bool CanAttack(in WeaponAttackRequest request) => IsEquipped || allowWhenNotEquipped;
        protected virtual bool CompletesImmediately => true;
        protected abstract bool ExecuteAttack(in WeaponAttackRequest request);

        protected GameObject ResolveOwner(in WeaponAttackRequest request)
            => request.InstigatorOverride != null ? request.InstigatorOverride : RuntimeContext?.OwnerObject;

        protected void PublishHit(in WeaponHitInfo hit, bool deliverToReceiver = true)
        {
            HitProduced?.Invoke(hit);
            if (deliverToReceiver) WeaponDamageUtility.Deliver(in hit);
        }

        protected void CompleteDeferredAttack() => AttackCompleted?.Invoke(this);
    }
}