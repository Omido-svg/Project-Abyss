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
        public Transform TargetRoot;
        public Transform SpringRoot;
        public Transform[] TargetBones = Array.Empty<Transform>();
        public Transform[] SpringBones = Array.Empty<Transform>();
        public SecondaryRigManifestSpringDefaults ManifestDefaults = new();

        [Tooltip("Enable only when this chain needs settings that differ from the shared preset library.")]
        public bool UseCustomSettings;
        public SecondaryRigSettings CustomSettings = new();

        public string StableKey
        {
            get
            {
                string root = SpringRoot != null ? SpringRoot.name : "<missing>";
                return $"{PartName}|{RegionName}|{root}";
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
            if (!Enabled || TargetRoot == null || SpringRoot == null)
                return false;

            if (TargetBones == null || SpringBones == null)
                return false;

            if (TargetBones.Length == 0 || TargetBones.Length != SpringBones.Length)
                return false;

            for (int i = 0; i < TargetBones.Length; i++)
            {
                if (TargetBones[i] == null || SpringBones[i] == null)
                    return false;
            }

            return true;
        }
    }
}
