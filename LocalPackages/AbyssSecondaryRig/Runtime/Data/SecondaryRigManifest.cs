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
        public List<SecondaryRigManifestPart> parts = new();

        public static SecondaryRigManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Secondary rig manifest JSON is empty.", nameof(json));

            SecondaryRigManifest manifest = JsonUtility.FromJson<SecondaryRigManifest>(json);
            if (manifest == null)
                throw new InvalidOperationException("Unity JsonUtility could not parse the secondary rig manifest.");

            manifest.parts ??= new List<SecondaryRigManifestPart>();
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
        public List<string> targetRoots = new();
        public List<string> springRoots = new();
        public List<SecondaryRigManifestChain> chains = new();
        public SecondaryRigManifestSpringDefaults springDefaults = new();
        public List<SecondaryRigManifestRegion> regions = new();
    }

    [Serializable]
    public sealed class SecondaryRigManifestRegion
    {
        public string group;
        public string parentBone;
        public string preset;
        public List<string> targetRoots = new();
        public List<string> springRoots = new();
        public List<SecondaryRigManifestChain> chains = new();
        public SecondaryRigManifestSpringDefaults springDefaults = new();
    }

    [Serializable]
    public sealed class SecondaryRigManifestChain
    {
        public int chainIndex;
        public string targetRoot;
        public string springRoot;
        public List<string> targetBones = new();
        public List<string> springBones = new();
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
