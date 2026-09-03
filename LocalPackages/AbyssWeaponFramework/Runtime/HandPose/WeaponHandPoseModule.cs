using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponHandPoseModule : WeaponModule
    {
        [SerializeField] private HandPoseDefinition primaryPose;
        [SerializeField] private HandPoseDefinition supportPose;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [SerializeField] private int priority = 100;

        private WeaponHandPoseController controller;

        public override void OnWeaponEquipped(WeaponEquipContext context)
        {
            base.OnWeaponEquipped(context);
            Apply(context);
        }

        public override void OnWeaponRemounted(WeaponEquipContext context)
        {
            base.OnWeaponRemounted(context);
            Clear();
            Apply(context);
        }

        public override void OnWeaponUnequipped(WeaponEquipContext context)
        {
            Clear();
            base.OnWeaponUnequipped(context);
        }

        private void Apply(WeaponEquipContext context)
        {
            if (context.Controller == null || context.Handle == null) return;
            controller = context.Controller.GetComponent<WeaponHandPoseController>();
            if (controller == null) controller = context.Controller.GetComponentInChildren<WeaponHandPoseController>(true);
            if (controller == null) return;

            for (int i = 0; i < context.Handle.Bindings.Count; i++)
            {
                ResolvedWeaponGripBinding binding = context.Handle.Bindings[i];
                if (binding?.Grip == null || binding.Socket == null || binding.Socket.Hand == WeaponHand.Any) continue;
                HandPoseDefinition pose = binding.Grip.Role == WeaponGripRole.Support ? supportPose : primaryPose;
                if (pose != null) controller.SetPose(this, binding.Socket.Hand, pose, weight, priority);
            }
        }

        private void Clear()
        {
            if (controller != null) controller.RemovePose(this);
            controller = null;
        }
    }
}
