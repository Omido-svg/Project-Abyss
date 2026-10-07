#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal sealed class NormalizedManifestChain
    {
        public string PartName;
        public string RegionName;
        public string PresetName;
        public string ParentBone;
        public string BoneStructure;
        public SecondaryRigManifestSpringDefaults Defaults;
        public SecondaryRigManifestChain Chain;
        public string SpringRootName;
        public List<string> TargetBoneNames = new();
        public List<string> SpringBoneNames = new();

        public bool UsesVirtualTarget =>
            string.Equals(BoneStructure, "SINGLE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(BoneStructure, "SEC_ONLY", StringComparison.OrdinalIgnoreCase);
    }

    internal static class SecondaryRigManifestUtility
    {
        public static List<NormalizedManifestChain> Normalize(
            SecondaryRigManifest manifest,
            out int duplicateCount)
        {
            duplicateCount = 0;
            List<NormalizedManifestChain> candidates = new();

            if (manifest?.parts == null)
                return candidates;

            for (int partIndex = 0; partIndex < manifest.parts.Count; partIndex++)
            {
                SecondaryRigManifestPart part = manifest.parts[partIndex];
                if (part == null || !string.Equals(part.partType, "DYNAMIC", StringComparison.OrdinalIgnoreCase))
                    continue;

                // The Setup Wizard is itself the runtime import boundary.
                // v0.8.1 Blender records may keep an old OFFLINE_BAKE value when the artist
                // changes the UI to Runtime after the region was already built. The exporter
                // can therefore produce a valid runtime JSON whose per-region workflow metadata
                // is stale. Do not discard otherwise-valid DYNAMIC chains here.
                //
                // The source workflow is still reported by Analyze so stale authoring metadata
                // is visible, but explicit JSON import wins.
                HashSet<string> regionRoots = new(StringComparer.Ordinal);

                if (part.regions != null)
                {
                    for (int regionIndex = 0; regionIndex < part.regions.Count; regionIndex++)
                    {
                        SecondaryRigManifestRegion region = part.regions[regionIndex];
                        if (region?.chains == null)
                            continue;

                        for (int chainIndex = 0; chainIndex < region.chains.Count; chainIndex++)
                        {
                            SecondaryRigManifestChain chain = region.chains[chainIndex];
                            NormalizedManifestChain normalized = BuildNormalized(part, region, chain);
                            if (!IsUsable(normalized))
                                continue;

                            regionRoots.Add(normalized.SpringRootName);
                            candidates.Add(normalized);
                        }
                    }
                }

                if (part.chains == null)
                    continue;

                for (int chainIndex = 0; chainIndex < part.chains.Count; chainIndex++)
                {
                    SecondaryRigManifestChain chain = part.chains[chainIndex];
                    NormalizedManifestChain normalized = BuildNormalized(part, null, chain);
                    if (!IsUsable(normalized))
                        continue;

                    if (regionRoots.Contains(normalized.SpringRootName))
                        continue;

                    candidates.Add(normalized);
                }
            }

            // Multi-region authoring can leave legacy/top-level entries with the same secondary root.
            // One SEC/SPR transform must only be simulated once. Prefer the longest chain.
            Dictionary<string, NormalizedManifestChain> bestByRoot = new(StringComparer.Ordinal);
            List<string> insertionOrder = new();

            for (int i = 0; i < candidates.Count; i++)
            {
                NormalizedManifestChain candidate = candidates[i];
                string root = candidate.SpringRootName ?? string.Empty;

                if (!bestByRoot.TryGetValue(root, out NormalizedManifestChain existing))
                {
                    bestByRoot.Add(root, candidate);
                    insertionOrder.Add(root);
                    continue;
                }

                duplicateCount++;
                int existingCount = existing.SpringBoneNames?.Count ?? 0;
                int candidateCount = candidate.SpringBoneNames?.Count ?? 0;

                if (candidateCount > existingCount)
                    bestByRoot[root] = candidate;
            }

            List<NormalizedManifestChain> result = new();
            for (int i = 0; i < insertionOrder.Count; i++)
            {
                if (bestByRoot.TryGetValue(insertionOrder[i], out NormalizedManifestChain chain))
                    result.Add(chain);
            }

            return result;
        }

        public static Dictionary<string, List<Transform>> BuildNameLookup(Transform root)
        {
            Dictionary<string, List<Transform>> lookup = new(StringComparer.Ordinal);
            if (root == null)
                return lookup;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (!lookup.TryGetValue(transform.name, out List<Transform> list))
                {
                    list = new List<Transform>();
                    lookup.Add(transform.name, list);
                }

                list.Add(transform);
            }

            return lookup;
        }

        public static Transform ResolveUnique(
            Dictionary<string, List<Transform>> lookup,
            string name,
            out bool ambiguous)
        {
            ambiguous = false;
            if (string.IsNullOrWhiteSpace(name) || !lookup.TryGetValue(name, out List<Transform> list) || list.Count == 0)
                return null;

            ambiguous = list.Count > 1;
            return list[0];
        }

        public static string BuildAnalysisSummary(
            GameObject root,
            SecondaryRigManifest manifest)
        {
            if (root == null)
                return "Character Root is not assigned.";
            if (manifest == null)
                return "Manifest is not assigned or could not be parsed.";

            List<NormalizedManifestChain> normalized = Normalize(manifest, out int duplicates);
            Dictionary<string, List<Transform>> lookup = BuildNameLookup(root.transform);
            int missing = 0;
            int ambiguous = 0;
            int singleCount = 0;
            int dualCount = 0;
            int offlineMetadataParts = 0;
            int runtimeMetadataParts = 0;
            Dictionary<string, int> byPart = new(StringComparer.Ordinal);

            if (manifest.parts != null)
            {
                for (int partIndex = 0; partIndex < manifest.parts.Count; partIndex++)
                {
                    SecondaryRigManifestPart part = manifest.parts[partIndex];
                    if (part == null || !string.Equals(part.partType, "DYNAMIC", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IsRuntimeWorkflow(part.workflow)) runtimeMetadataParts++;
                    else offlineMetadataParts++;
                }
            }

            for (int i = 0; i < normalized.Count; i++)
            {
                NormalizedManifestChain chain = normalized[i];
                byPart.TryGetValue(chain.PartName ?? string.Empty, out int count);
                byPart[chain.PartName ?? string.Empty] = count + 1;

                if (chain.UsesVirtualTarget) singleCount++;
                else dualCount++;

                IEnumerable<string> names = chain.SpringBoneNames;
                if (!chain.UsesVirtualTarget)
                    names = chain.TargetBoneNames.Concat(chain.SpringBoneNames);

                if (chain.UsesVirtualTarget && !string.IsNullOrWhiteSpace(chain.ParentBone))
                    names = names.Concat(new[] { chain.ParentBone });

                foreach (string name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
                {
                    Transform resolved = ResolveUnique(lookup, name, out bool isAmbiguous);
                    if (resolved == null)
                        missing++;
                    else if (isAmbiguous)
                        ambiguous++;
                }
            }

            List<string> lines = new()
            {
                $"Schema: {manifest.schema}",
                $"Dynamic chains after de-duplication: {normalized.Count}",
                $"- SEC-only / virtual target: {singleCount}",
                $"- TGT+SEC / explicit target: {dualCount}",
                $"Source workflow metadata: Runtime/legacy={runtimeMetadataParts}, Offline={offlineMetadataParts}",
                offlineMetadataParts > 0
                    ? "NOTE: Offline workflow metadata is accepted because assigning this JSON to the Runtime Setup Wizard is treated as explicit runtime intent."
                    : "Source workflow metadata: OK",
                $"Duplicate/legacy chain records ignored: {duplicates}",
                $"Missing bone references: {missing}",
                $"Ambiguous transform names: {ambiguous}"
            };

            foreach (KeyValuePair<string, int> pair in byPart)
                lines.Add($"- {pair.Key}: {pair.Value} chain(s)");

            return string.Join("\n", lines);
        }

        private static NormalizedManifestChain BuildNormalized(
            SecondaryRigManifestPart part,
            SecondaryRigManifestRegion region,
            SecondaryRigManifestChain chain)
        {
            if (chain == null)
                return null;

            List<string> springBones = PreferNonEmpty(chain.secondaryBones, chain.springBones);
            string springRoot = FirstNonEmpty(chain.secondaryRoot, chain.springRoot);
            if (string.IsNullOrWhiteSpace(springRoot) && springBones.Count > 0)
                springRoot = springBones[0];

            List<string> targetBones = chain.targetBones ?? new List<string>();
            string structure = FirstNonEmpty(
                chain.boneStructure,
                region?.boneStructure,
                part?.boneStructure);

            if (string.IsNullOrWhiteSpace(structure))
                structure = targetBones.Count > 0 ? "DUAL" : "SINGLE";

            structure = structure.Equals("DUAL", StringComparison.OrdinalIgnoreCase)
                ? "DUAL"
                : "SINGLE";

            return new NormalizedManifestChain
            {
                PartName = part?.@object,
                RegionName = region?.group ?? string.Empty,
                PresetName = !string.IsNullOrWhiteSpace(region?.preset) ? region.preset : part?.preset,
                ParentBone = !string.IsNullOrWhiteSpace(region?.parentBone) ? region.parentBone : part?.parentBone,
                BoneStructure = structure,
                Defaults = region?.springDefaults ?? part?.springDefaults ?? new SecondaryRigManifestSpringDefaults(),
                Chain = chain,
                SpringRootName = springRoot,
                TargetBoneNames = targetBones,
                SpringBoneNames = springBones
            };
        }

        private static bool IsUsable(NormalizedManifestChain chain)
        {
            if (chain == null || string.IsNullOrWhiteSpace(chain.SpringRootName))
                return false;

            if (chain.SpringBoneNames == null || chain.SpringBoneNames.Count == 0)
                return false;

            if (chain.UsesVirtualTarget)
                return true;

            return chain.TargetBoneNames != null &&
                   chain.TargetBoneNames.Count > 0 &&
                   chain.TargetBoneNames.Count == chain.SpringBoneNames.Count;
        }

        private static bool IsRuntimeWorkflow(string workflow)
        {
            if (string.IsNullOrWhiteSpace(workflow))
                return true; // v2 manifests had no workflow field.

            return !workflow.Equals("OFFLINE_BAKE", StringComparison.OrdinalIgnoreCase) &&
                   !workflow.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> PreferNonEmpty(List<string> primary, List<string> fallback)
        {
            if (primary != null && primary.Count > 0)
                return primary;
            return fallback ?? new List<string>();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }
    }
}
#endif
