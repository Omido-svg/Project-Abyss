using System;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [Serializable]
    public sealed class SecondaryRigChainBinding
    {
        public bool Enabled = true;
        public string PartName;
        public string RegionName;
        public string PresetName;

        [Tooltip("DUAL = explicit TGT + SEC/SPR bones. SINGLE = SEC-only with a virtual target reconstructed from the stored rest-local chain pose.")]
        public string BoneStructure = "DUAL";

        // DUAL mode.
        public Transform TargetRoot;
        public Transform[] TargetBones = Array.Empty<Transform>();

        // Both DUAL and SINGLE mode.
        public Transform SpringRoot;
        public Transform[] SpringBones = Array.Empty<Transform>();

        // SINGLE mode driver. Normally the animated body bone that is the parent of SpringRoot.
        public Transform DriverParent;

        // SINGLE mode reference pose captured by the Setup Wizard. This prevents a runtime
        // Rebuild from accidentally treating a previously simulated pose as the new target pose.
        public Vector3[] ReferenceLocalPositions = Array.Empty<Vector3>();
        public Quaternion[] ReferenceLocalRotations = Array.Empty<Quaternion>();
        public Vector3[] ReferenceLocalScales = Array.Empty<Vector3>();

        public SecondaryRigManifestSpringDefaults ManifestDefaults = new();

        [Tooltip("Enable only when this chain needs settings that differ from the shared preset library.")]
        public bool UseCustomSettings;
        public SecondaryRigSettings CustomSettings = new();

        public bool UsesVirtualTarget
        {
            get
            {
                if (string.Equals(BoneStructure, "SINGLE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(BoneStructure, "SEC_ONLY", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(BoneStructure, "DUAL", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Backward/fallback inference for old serialized bindings.
                return TargetBones == null || TargetBones.Length == 0;
            }
        }

        public Transform EffectiveDriverParent
        {
            get
            {
                if (DriverParent != null)
                    return DriverParent;
                return SpringRoot != null ? SpringRoot.parent : null;
            }
        }

        public string StableKey
        {
            get
            {
                string root = SpringRoot != null ? SpringRoot.name : "<missing>";
                string mode = UsesVirtualTarget ? "SINGLE" : "DUAL";
                return $"{PartName}|{RegionName}|{root}|{mode}";
            }
        }

        public SecondaryRigSettings ResolveSettings(SecondaryRigPresetLibrary library)
        {
            if (UseCustomSettings && CustomSettings != null)
            {
                SecondaryRigSettings custom = CustomSettings.Clone();
                custom.Sanitize();
                return custom;
            }

            if (library != null)
                return library.Resolve(PresetName, ManifestDefaults);

            return SecondaryRigPresetLibrary.CreateFallbackSettings(PresetName, ManifestDefaults);
        }

        public bool HasUsableBones()
        {
            if (!Enabled || SpringRoot == null)
                return false;

            if (SpringBones == null || SpringBones.Length == 0)
                return false;

            for (int i = 0; i < SpringBones.Length; i++)
            {
                if (SpringBones[i] == null)
                    return false;
            }

            if (UsesVirtualTarget)
                return EffectiveDriverParent != null;

            if (TargetRoot == null || TargetBones == null || TargetBones.Length == 0)
                return false;

            if (TargetBones.Length != SpringBones.Length)
                return false;

            for (int i = 0; i < TargetBones.Length; i++)
            {
                if (TargetBones[i] == null)
                    return false;
            }

            return true;
        }

        public bool HasStoredReferencePose()
        {
            int count = SpringBones?.Length ?? 0;
            return count > 0 &&
                   ReferenceLocalPositions != null && ReferenceLocalPositions.Length == count &&
                   ReferenceLocalRotations != null && ReferenceLocalRotations.Length == count &&
                   ReferenceLocalScales != null && ReferenceLocalScales.Length == count;
        }
    }
}
