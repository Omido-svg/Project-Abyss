using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class ChainWeaponPhysicsModule : WeaponModule
    {
        [SerializeField] private Transform chainRoot;
        [SerializeField] private Rigidbody anchorBody;
        [SerializeField] private bool simulateWhenEquipped = true;
        [SerializeField] private bool simulateWhenStowed;
        [SerializeField] private bool resetVelocitiesOnRemount = true;
        [SerializeField] private bool includeInactive = true;

        private Rigidbody[] bodies = Array.Empty<Rigidbody>();

        private void Awake() => Refresh();
        private void OnValidate() { if (!Application.isPlaying) Refresh(); }

        public void Refresh()
        {
            Transform root = chainRoot != null ? chainRoot : transform;
            bodies = root.GetComponentsInChildren<Rigidbody>(includeInactive);
            if (anchorBody == null && bodies.Length > 0) anchorBody = bodies[0];
        }

        public override void OnWeaponEquipped(WeaponEquipContext context)
        {
            base.OnWeaponEquipped(context);
            ApplyState(context.Handle, simulateWhenEquipped);
        }

        public override void OnWeaponRemounted(WeaponEquipContext context)
        {
            base.OnWeaponRemounted(context);
            bool stowed = context.Handle?.MountBinding?.Grip?.Role == WeaponGripRole.Stow;
            ApplyState(context.Handle, stowed ? simulateWhenStowed : simulateWhenEquipped);
        }

        public override void OnWeaponUnequipped(WeaponEquipContext context)
        {
            SetSimulation(false);
            base.OnWeaponUnequipped(context);
        }

        public void SetSimulation(bool enabled)
        {
            if (bodies == null || bodies.Length == 0) Refresh();
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null) continue;
                bool anchor = body == anchorBody;
                body.isKinematic = anchor || !enabled;
                if (!enabled || resetVelocitiesOnRemount)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private void ApplyState(WeaponEquipHandle handle, bool enabled)
        {
            if (resetVelocitiesOnRemount && bodies != null)
                for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; }
            SetSimulation(enabled);
        }
    }
}
