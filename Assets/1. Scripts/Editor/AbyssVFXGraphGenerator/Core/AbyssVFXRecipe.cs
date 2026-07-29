#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    [Serializable]
    internal sealed class AbyssVFXRecipe
    {
        public string schemaVersion = "1.0";
        public string name = "GeneratedVFX";
        [TextArea(2, 5)] public string description = string.Empty;
        public List<AbyssVFXSystemRecipe> systems = new();
    }

    [Serializable]
    internal sealed class AbyssVFXSystemRecipe
    {
        public string name = "Particles";
        public string spawnMode = "Burst"; // Burst | Rate
        public uint capacity = 1024;
        public float spawnCount = 64f;
        public float spawnRate = 24f;

        public AbyssFloatRange lifetime = new(0.35f, 0.8f);
        public AbyssFloatRange size = new(0.05f, 0.2f);

        public string positionMode = "Box"; // Point | Box
        public AbyssVector3 spawnBoxExtents = new(0.05f, 0.05f, 0.05f);

        public string velocityMode = "Radial"; // Radial | Directional | None
        public AbyssVector3 direction = new(0f, 1f, 0f);
        public AbyssFloatRange speed = new(2f, 7f);
        public float spread = 0.35f;

        public AbyssVector3 gravity = new(0f, -3f, 0f);
        public float drag = 0.5f;

        public AbyssColor startColor = new(0.8f, 0.05f, 0.02f, 1f);
        public string blendMode = "Additive"; // Alpha | Additive | Premultiplied
        public string textureAssetPath = string.Empty;
        public bool orientToVelocity = false;
    }

    [Serializable]
    internal struct AbyssFloatRange
    {
        public float min;
        public float max;

        public AbyssFloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public void Normalize()
        {
            if (min > max)
                (min, max) = (max, min);
        }
    }

    [Serializable]
    internal struct AbyssVector3
    {
        public float x;
        public float y;
        public float z;

        public AbyssVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public readonly Vector3 ToVector3() => new(x, y, z);
        public static AbyssVector3 From(Vector3 value) => new(value.x, value.y, value.z);
    }

    [Serializable]
    internal struct AbyssColor
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public AbyssColor(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public readonly Color ToColor() => new(r, g, b, a);
        public static AbyssColor From(Color value) => new(value.r, value.g, value.b, value.a);
    }
}
#endif
