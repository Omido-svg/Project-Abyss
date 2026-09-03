using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponAnimationModule : WeaponModule
    {
        [SerializeField] private string equipTrigger;
        [SerializeField] private string unequipTrigger;
        [SerializeField] private string remountTrigger;
        [SerializeField] private string attackTriggerPrefix = "Weapon_";
        private WeaponAnimatorBridge bridge;
        private WeaponAttackModule[] attacks;

        private void Awake() => attacks = GetComponentsInChildren<WeaponAttackModule>(true);

        public override void OnWeaponEquipped(WeaponEquipContext context)
        {
            base.OnWeaponEquipped(context);
            ResolveBridge(context);
            SetAttackSubscriptions(true);
            bridge?.SetTrigger(equipTrigger);
        }

        public override void OnWeaponRemounted(WeaponEquipContext context)
        {
            base.OnWeaponRemounted(context);
            ResolveBridge(context);
            bridge?.SetTrigger(remountTrigger);
        }

        public override void OnWeaponUnequipped(WeaponEquipContext context)
        {
            bridge?.SetTrigger(unequipTrigger);
            SetAttackSubscriptions(false);
            bridge = null;
            base.OnWeaponUnequipped(context);
        }

        private void ResolveBridge(WeaponEquipContext context)
        {
            GameObject owner = context.OwnerObject;
            if (owner == null) return;
            bridge = owner.GetComponentInChildren<WeaponAnimatorBridge>(true);
            if (bridge == null && owner.GetComponent<Animator>() != null) bridge = owner.AddComponent<WeaponAnimatorBridge>();
        }

        private void SetAttackSubscriptions(bool subscribe)
        {
            attacks ??= GetComponentsInChildren<WeaponAttackModule>(true);
            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] == null) continue;
                if (subscribe) attacks[i].AttackStarted += OnAttackStarted;
                else attacks[i].AttackStarted -= OnAttackStarted;
            }
        }

        private void OnAttackStarted(WeaponAttackModule attack)
        {
            if (bridge == null || string.IsNullOrWhiteSpace(attackTriggerPrefix)) return;
            bridge.SetTrigger(attackTriggerPrefix + attack.AttackId);
        }
    }
}
