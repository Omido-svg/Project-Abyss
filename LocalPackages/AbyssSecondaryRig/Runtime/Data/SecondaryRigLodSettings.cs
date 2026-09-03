using System;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    public enum SecondaryRigLodLevel
    {
        Full = 0,
        Reduced = 1,
        Minimal = 2,
        Disabled = 3
    }

    [Serializable]
    public sealed class SecondaryRigLodSettings
    {
        [Tooltip("Enable distance based simulation quality scaling.")]
        public bool Enabled = true;

        [Tooltip("Optional camera override. If empty, Camera.main is used.")]
        public Camera CameraOverride;

        [Tooltip("Optional transform used for distance measurement. If empty, Simulation Root is used.")]
        public Transform DistanceReference;

        [Header("Distances")]
        [Min(0f)] public float FullDistance = 10f;
        [Min(0f)] public float ReducedDistance = 25f;
        [Min(0f)] public float DisableDistance = 40f;

        [Header("Reduced")]
        [Range(1, 8)] public int ReducedSubsteps = 1;
        [Range(1, 8)] public int ReducedConstraintIterations = 1;
        public bool ReducedBodyCollision = true;
        public bool ReducedSecondaryCollision;

        [Header("Minimal")]
        [Range(1, 8)] public int MinimalSubsteps = 1;
        [Range(1, 8)] public int MinimalConstraintIterations = 1;
        public bool MinimalBodyCollision;
        public bool MinimalSecondaryCollision;

        [Header("Disabled")]
        [Tooltip("Snap SPR particles to their TGT pose when entering Disabled LOD.")]
        public bool ResetWhenDisabled = true;

        public void Sanitize()
        {
            FullDistance = Mathf.Max(0f, FullDistance);
            ReducedDistance = Mathf.Max(FullDistance, ReducedDistance);
            DisableDistance = Mathf.Max(ReducedDistance, DisableDistance);
            ReducedSubsteps = Mathf.Clamp(ReducedSubsteps, 1, 8);
            ReducedConstraintIterations = Mathf.Clamp(ReducedConstraintIterations, 1, 8);
            MinimalSubsteps = Mathf.Clamp(MinimalSubsteps, 1, 8);
            MinimalConstraintIterations = Mathf.Clamp(MinimalConstraintIterations, 1, 8);
        }
    }
}
