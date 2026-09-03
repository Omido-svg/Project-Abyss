using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [CreateAssetMenu(
        menuName = "Project Abyss/Secondary Rig/Preset Library",
        fileName = "SecondaryRigPresetLibrary")]
    public sealed class SecondaryRigPresetLibrary : ScriptableObject
    {
        public const int CurrentDataVersion = 2;

        [SerializeField, HideInInspector] private int dataVersion = CurrentDataVersion;
        [SerializeField] private SecondaryRigPresetEntry[] presets = Array.Empty<SecondaryRigPresetEntry>();

        public int DataVersion => dataVersion;
        public IReadOnlyList<SecondaryRigPresetEntry> Presets => presets ?? Array.Empty<SecondaryRigPresetEntry>();
        public int PresetCount => presets?.Length ?? 0;

        public SecondaryRigSettings Resolve(
            string presetName,
            SecondaryRigManifestSpringDefaults manifestDefaults)
        {
            SecondaryRigSettings result = CreateFallbackSettings(presetName, manifestDefaults);
            SecondaryRigPresetEntry match = Find(presetName);
            if (match == null || match.Settings == null)
                return result;

            SecondaryRigSettings preset = match.Settings;
            if (match.OverrideManifestSpring)
            {
                result.Stiffness = preset.Stiffness;
                result.Damping = preset.Damping;
                result.Gravity = preset.Gravity;
                result.MaxAngle = preset.MaxAngle;
            }

            result.ParticleRadius = preset.ParticleRadius;
            result.EnableCollision = preset.EnableCollision;
            result.Layer = preset.Layer;
            result.CollidesWith = preset.CollidesWith;
            result.EnableSelfCollision = preset.EnableSelfCollision;
            result.CollideWithinSamePart = preset.CollideWithinSamePart;
            result.EnableCrossChainConstraint = preset.EnableCrossChainConstraint;
            result.CrossChainStiffness = preset.CrossChainStiffness;
            result.Sanitize();
            return result;
        }

        public static SecondaryRigSettings CreateFallbackSettings(
            string presetName,
            SecondaryRigManifestSpringDefaults manifestDefaults)
        {
            SecondaryRigSettings result = new();
            result.CopySpringFrom(manifestDefaults);
            ApplyFallbackCollisionPreset(presetName, result);
            result.Sanitize();
            return result;
        }

        public SecondaryRigPresetEntry Find(string presetName)
        {
            if (presets == null)
                return null;

            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] != null && presets[i].Matches(presetName))
                    return presets[i];
            }

            return null;
        }

        public bool Contains(string presetName) => Find(presetName) != null;

        public bool AddMissingRecommendedPresets()
        {
            List<SecondaryRigPresetEntry> values = new(presets ?? Array.Empty<SecondaryRigPresetEntry>());
            bool changed = false;

            SecondaryRigPresetEntry[] recommended = CreateRecommendedDefaults();
            for (int i = 0; i < recommended.Length; i++)
            {
                if (FindInList(values, recommended[i].PresetName) != null)
                    continue;

                values.Add(recommended[i]);
                changed = true;
            }

            if (dataVersion != CurrentDataVersion)
            {
                dataVersion = CurrentDataVersion;
                changed = true;
            }

            if (changed)
                presets = values.ToArray();

            return changed;
        }

        public bool TryDuplicatePreset(string sourcePresetName, string newPresetName)
        {
            if (string.IsNullOrWhiteSpace(newPresetName) || Contains(newPresetName))
                return false;

            SecondaryRigPresetEntry source = Find(sourcePresetName);
            if (source == null)
                return false;

            List<SecondaryRigPresetEntry> values = new(presets ?? Array.Empty<SecondaryRigPresetEntry>())
            {
                source.Clone(newPresetName)
            };
            presets = values.ToArray();
            dataVersion = CurrentDataVersion;
            return true;
        }

        public void ResetToRecommendedDefaults()
        {
            presets = CreateRecommendedDefaults();
            dataVersion = CurrentDataVersion;
        }

        public string ValidateLibrary()
        {
            List<string> issues = new();
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            if (presets == null || presets.Length == 0)
                issues.Add("Preset library is empty.");

            if (presets != null)
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    SecondaryRigPresetEntry entry = presets[i];
                    if (entry == null)
                    {
                        issues.Add($"Preset [{i}] is null.");
                        continue;
                    }

                    string name = entry.PresetName?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        issues.Add($"Preset [{i}] has an empty name.");
                        continue;
                    }

                    if (!names.Add(name))
                        issues.Add($"Duplicate preset name: {name}");

                    if (entry.Settings == null)
                        issues.Add($"Preset '{name}' has no Settings object.");
                }
            }

            return issues.Count == 0 ? "Preset Library: OK" : string.Join("\n", issues);
        }

        private static SecondaryRigPresetEntry FindInList(List<SecondaryRigPresetEntry> values, string name)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != null && values[i].Matches(name))
                    return values[i];
            }
            return null;
        }

        private static SecondaryRigPresetEntry[] CreateRecommendedDefaults()
        {
            return new[]
            {
                MakeHairPreset(),
                MakeLongHairPreset(),
                MakeShortHairPreset(),
                MakeLongCoatPreset(),
                MakeSkirtPreset(),
                MakeCapePreset(),
                MakeRibbonPreset(),
                MakeAccessoryPreset()
            };
        }

        private static SecondaryRigPresetEntry MakeHairPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "HAIR",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.32f,
                    Damping = 0.76f,
                    Gravity = 0.16f,
                    MaxAngle = 48f,
                    ParticleRadius = 0.014f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Hair,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = false,
                    CrossChainStiffness = 0f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeLongHairPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "LONG_HAIR",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.25f,
                    Damping = 0.73f,
                    Gravity = 0.2f,
                    MaxAngle = 60f,
                    ParticleRadius = 0.016f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Hair,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = false,
                    CrossChainStiffness = 0f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeShortHairPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "SHORT_HAIR",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.55f,
                    Damping = 0.82f,
                    Gravity = 0.08f,
                    MaxAngle = 28f,
                    ParticleRadius = 0.01f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Hair,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = false,
                    CrossChainStiffness = 0f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeLongCoatPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "LONG_COAT",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.34f,
                    Damping = 0.74f,
                    Gravity = 0.26f,
                    MaxAngle = 50f,
                    ParticleRadius = 0.02f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Cloth,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = true,
                    CrossChainStiffness = 0.3f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeSkirtPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "SKIRT",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.38f,
                    Damping = 0.76f,
                    Gravity = 0.3f,
                    MaxAngle = 55f,
                    ParticleRadius = 0.02f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Cloth,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = true,
                    CrossChainStiffness = 0.35f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeCapePreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "CAPE",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.28f,
                    Damping = 0.72f,
                    Gravity = 0.22f,
                    MaxAngle = 65f,
                    ParticleRadius = 0.018f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Cloth,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = true,
                    CrossChainStiffness = 0.25f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeRibbonPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "RIBBON",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.3f,
                    Damping = 0.72f,
                    Gravity = 0.12f,
                    MaxAngle = 70f,
                    ParticleRadius = 0.009f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Accessory,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = false,
                    CrossChainStiffness = 0f
                }
            };
        }

        private static SecondaryRigPresetEntry MakeAccessoryPreset()
        {
            return new SecondaryRigPresetEntry
            {
                PresetName = "ACCESSORY",
                OverrideManifestSpring = true,
                Settings = new SecondaryRigSettings
                {
                    Stiffness = 0.5f,
                    Damping = 0.8f,
                    Gravity = 0.18f,
                    MaxAngle = 45f,
                    ParticleRadius = 0.01f,
                    EnableCollision = true,
                    Layer = SecondaryCollisionLayer.Accessory,
                    CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth,
                    EnableSelfCollision = false,
                    CollideWithinSamePart = false,
                    EnableCrossChainConstraint = false,
                    CrossChainStiffness = 0f
                }
            };
        }

        private static void ApplyFallbackCollisionPreset(string presetName, SecondaryRigSettings settings)
        {
            string normalized = (presetName ?? string.Empty).Trim().ToUpperInvariant();

            if (normalized.Contains("COAT") || normalized.Contains("CAPE") || normalized.Contains("SKIRT") || normalized.Contains("CLOTH"))
            {
                settings.Layer = SecondaryCollisionLayer.Cloth;
                settings.CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth;
                settings.ParticleRadius = 0.02f;
                settings.EnableCrossChainConstraint = true;
                settings.CrossChainStiffness = 0.3f;
                return;
            }

            if (normalized.Contains("HAIR"))
            {
                settings.Layer = SecondaryCollisionLayer.Hair;
                settings.CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Cloth;
                settings.ParticleRadius = 0.014f;
                return;
            }

            settings.Layer = SecondaryCollisionLayer.Accessory;
            settings.CollidesWith = SecondaryCollisionLayer.Body | SecondaryCollisionLayer.Hair | SecondaryCollisionLayer.Cloth;
            settings.ParticleRadius = 0.01f;
        }
    }
}
