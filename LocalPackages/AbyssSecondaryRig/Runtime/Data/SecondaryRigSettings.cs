using System;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    public enum SecondaryRigUpdateMode
    {
        LateUpdate = 0,
        Manual = 1
    }

    [Flags]
    public enum SecondaryCollisionLayer
    {
        None = 0,
        Body = 1 << 0,
        Hair = 1 << 1,
        Cloth = 1 << 2,
        Accessory = 1 << 3,
        Custom1 = 1 << 4,
        Custom2 = 1 << 5,
        All = ~0
    }

    [Serializable]
    public sealed class SecondaryRigSettings
    {
        [Header("Spring")]
        [Range(0f, 1f)] public float Stiffness = 0.35f;
        [Range(0f, 1f)] public float Damping = 0.75f;
        [Min(0f)] public float Gravity = 0.2f;
        [Range(0f, 180f)] public float MaxAngle = 50f;

        [Header("Collision")]
        [Min(0f)] public float ParticleRadius = 0.015f;
        public bool EnableCollision = true;
        public SecondaryCollisionLayer Layer = SecondaryCollisionLayer.Hair;
        public SecondaryCollisionLayer CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Cloth;
        public bool EnableSelfCollision;
        public bool CollideWithinSamePart;

        [Header("Wide Surface Constraint")]
        public bool EnableCrossChainConstraint;
        [Range(0f, 1f)] public float CrossChainStiffness = 0.35f;

        public SecondaryRigSettings Clone()
        {
            return new SecondaryRigSettings
            {
                Stiffness = Stiffness,
                Damping = Damping,
                Gravity = Gravity,
                MaxAngle = MaxAngle,
                ParticleRadius = ParticleRadius,
                EnableCollision = EnableCollision,
                Layer = Layer,
                CollidesWith = CollidesWith,
                EnableSelfCollision = EnableSelfCollision,
                CollideWithinSamePart = CollideWithinSamePart,
                EnableCrossChainConstraint = EnableCrossChainConstraint,
                CrossChainStiffness = CrossChainStiffness
            };
        }

        public void CopySpringFrom(SecondaryRigManifestSpringDefaults defaults)
        {
            if (defaults == null)
                return;

            Stiffness = defaults.stiffness;
            Damping = defaults.damping;
            Gravity = defaults.gravity;
            MaxAngle = defaults.maxAngle;
        }

        public void Sanitize()
        {
            Stiffness = Mathf.Clamp01(Stiffness);
            Damping = Mathf.Clamp01(Damping);
            Gravity = Mathf.Max(0f, Gravity);
            MaxAngle = Mathf.Clamp(MaxAngle, 0f, 180f);
            ParticleRadius = Mathf.Max(0f, ParticleRadius);
            CrossChainStiffness = Mathf.Clamp01(CrossChainStiffness);
        }
    }

    [Serializable]
    public sealed class SecondaryRigPresetEntry
    {
        public string PresetName = "HAIR";
        public bool OverrideManifestSpring = true;
        public SecondaryRigSettings Settings = new();

        public bool Matches(string presetName)
        {
            return string.Equals(PresetName?.Trim(), presetName?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public SecondaryRigPresetEntry Clone(string newName = null)
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = string.IsNullOrWhiteSpace(newName) ? PresetName : newName.Trim(),
                OverrideManifestSpring = OverrideManifestSpring,
                Settings = Settings?.Clone() ?? new SecondaryRigSettings()
            };
        }
    }
}
