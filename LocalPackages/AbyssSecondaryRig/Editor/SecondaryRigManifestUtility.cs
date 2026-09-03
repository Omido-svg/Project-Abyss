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
        public SecondaryRigManifestSpringDefaults Defaults;
        public SecondaryRigManifestChain Chain;
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
                            if (!IsUsable(chain))
                                continue;

                            regionRoots.Add(chain.springRoot ?? string.Empty);
                            candidates.Add(new NormalizedManifestChain
                            {
                                PartName = part.@object,
                                RegionName = region.group,
                                PresetName = string.IsNullOrWhiteSpace(region.preset) ? part.preset : region.preset,
                                ParentBone = string.IsNullOrWhiteSpace(region.parentBone) ? part.parentBone : region.parentBone,
                                Defaults = region.springDefaults ?? part.springDefaults ?? new SecondaryRigManifestSpringDefaults(),
                                Chain = chain
                            });
                        }
                    }
                }

                if (part.chains == null)
                    continue;

                for (int chainIndex = 0; chainIndex < part.chains.Count; chainIndex++)
                {
                    SecondaryRigManifestChain chain = part.chains[chainIndex];
                    if (!IsUsable(chain))
                        continue;

                    if (regionRoots.Contains(chain.springRoot ?? string.Empty))
                        continue;

                    candidates.Add(new NormalizedManifestChain
                    {
                        PartName = part.@object,
                        RegionName = string.Empty,
                        PresetName = part.preset,
                        ParentBone = part.parentBone,
                        Defaults = part.springDefaults ?? new SecondaryRigManifestSpringDefaults(),
                        Chain = chain
                    });
                }
            }

            // Multi-region authoring can leave legacy/top-level entries with the same spring root.
            // One spring transform must only be simulated once. Prefer the longest chain because
            // it contains the most recently approved guide when legacy and updated data overlap.
            Dictionary<string, NormalizedManifestChain> bestByRoot = new(StringComparer.Ordinal);
            List<string> insertionOrder = new();

            for (int i = 0; i < candidates.Count; i++)
            {
                NormalizedManifestChain candidate = candidates[i];
                string root = candidate.Chain.springRoot ?? string.Empty;

                if (!bestByRoot.TryGetValue(root, out NormalizedManifestChain existing))
                {
                    bestByRoot.Add(root, candidate);
                    insertionOrder.Add(root);
                    continue;
                }

                duplicateCount++;
                int existingCount = existing.Chain.springBones?.Count ?? 0;
                int candidateCount = candidate.Chain.springBones?.Count ?? 0;

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
            Dictionary<string, int> byPart = new(StringComparer.Ordinal);

            for (int i = 0; i < normalized.Count; i++)
            {
                NormalizedManifestChain chain = normalized[i];
                byPart.TryGetValue(chain.PartName ?? string.Empty, out int count);
                byPart[chain.PartName ?? string.Empty] = count + 1;

                foreach (string name in chain.Chain.targetBones.Concat(chain.Chain.springBones))
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
                $"Duplicate/legacy chain records ignored: {duplicates}",
                $"Missing bone references: {missing}",
                $"Ambiguous transform names: {ambiguous}"
            };

            foreach (KeyValuePair<string, int> pair in byPart)
                lines.Add($"- {pair.Key}: {pair.Value} chain(s)");

            return string.Join("\n", lines);
        }

        private static bool IsUsable(SecondaryRigManifestChain chain)
        {
            return chain != null &&
                   !string.IsNullOrWhiteSpace(chain.springRoot) &&
                   chain.targetBones != null &&
                   chain.springBones != null &&
                   chain.targetBones.Count > 0 &&
                   chain.targetBones.Count == chain.springBones.Count;
        }
    }
}
#endif
