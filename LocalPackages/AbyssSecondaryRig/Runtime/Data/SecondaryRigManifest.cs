using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [Serializable]
    public sealed class SecondaryRigManifest
    {
        public string schema;
        public string armature;
        public string note;
        public List<SecondaryRigManifestPart> parts = new();

        public static SecondaryRigManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Secondary rig manifest JSON is empty.", nameof(json));

            SecondaryRigManifest manifest = JsonUtility.FromJson<SecondaryRigManifest>(json);
            if (manifest == null)
                throw new InvalidOperationException("Unity JsonUtility could not parse the secondary rig manifest.");

            manifest.parts ??= new List<SecondaryRigManifestPart>();
            for (int i = 0; i < manifest.parts.Count; i++)
                manifest.parts[i]?.Sanitize();

            return manifest;
        }
    }

    [Serializable]
    public sealed class SecondaryRigManifestPart
    {
        public string @object;
        public string partType;
        public string parentBone;
        public string preset;

        // v3 authoring metadata. Empty on legacy v2 files.
        public string workflow;
        public string boneStructure;

        public List<string> targetRoots = new();
        public List<string> secondaryRoots = new();
        public List<string> secondaryBones = new();

        // v2/v0.7 compatibility aliases.
        public List<string> springRoots = new();
        public List<string> springBones = new();

        public List<SecondaryRigManifestChain> chains = new();
        public SecondaryRigManifestSpringDefaults springDefaults = new();
        public List<SecondaryRigManifestRegion> regions = new();

        internal void Sanitize()
        {
            targetRoots ??= new List<string>();
            secondaryRoots ??= new List<string>();
            secondaryBones ??= new List<string>();
            springRoots ??= new List<string>();
            springBones ??= new List<string>();
            chains ??= new List<SecondaryRigManifestChain>();
            regions ??= new List<SecondaryRigManifestRegion>();
            springDefaults ??= new SecondaryRigManifestSpringDefaults();

            for (int i = 0; i < chains.Count; i++)
                chains[i]?.Sanitize();
            for (int i = 0; i < regions.Count; i++)
                regions[i]?.Sanitize();
        }
    }

    [Serializable]
    public sealed class SecondaryRigManifestRegion
    {
        public string group;
        public string parentBone;
        public string preset;
        public string workflow;
        public string boneStructure;

        public List<string> targetRoots = new();
        public List<string> secondaryRoots = new();
        public List<string> secondaryBones = new();
        public List<string> springRoots = new();
        public List<string> springBones = new();

        public List<SecondaryRigManifestChain> chains = new();
        public SecondaryRigManifestSpringDefaults springDefaults = new();

        internal void Sanitize()
        {
            targetRoots ??= new List<string>();
            secondaryRoots ??= new List<string>();
            secondaryBones ??= new List<string>();
            springRoots ??= new List<string>();
            springBones ??= new List<string>();
            chains ??= new List<SecondaryRigManifestChain>();
            springDefaults ??= new SecondaryRigManifestSpringDefaults();

            for (int i = 0; i < chains.Count; i++)
                chains[i]?.Sanitize();
        }
    }

    [Serializable]
    public sealed class SecondaryRigManifestChain
    {
        public int chainIndex;
        public string boneStructure;

        public string targetRoot;
        public string secondaryRoot;
        public string springRoot;

        public List<string> targetBones = new();
        public List<string> secondaryBones = new();
        public List<string> springBones = new();

        internal void Sanitize()
        {
            targetBones ??= new List<string>();
            secondaryBones ??= new List<string>();
            springBones ??= new List<string>();
        }
    }

    [Serializable]
    public sealed class SecondaryRigManifestSpringDefaults
    {
        public float stiffness = 0.35f;
        public float damping = 0.75f;
        public float gravity = 0.2f;
        public float maxAngle = 50f;

        public SecondaryRigManifestSpringDefaults Clone()
        {
            return new SecondaryRigManifestSpringDefaults
            {
                stiffness = stiffness,
                damping = damping,
                gravity = gravity,
                maxAngle = maxAngle
            };
        }
    }
}
