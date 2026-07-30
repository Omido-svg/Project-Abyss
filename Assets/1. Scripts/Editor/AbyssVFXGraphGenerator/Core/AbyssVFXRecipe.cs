#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    [Serializable]
    internal sealed class AbyssVFXRecipe
    {
        public string schemaVersion = "2.0";
        public string name = "GeneratedVFX";
        [TextArea(2, 5)] public string description = string.Empty;

        [Tooltip("Structured는 Recipe로 그래프를 생성합니다. CloneTemplate은 기존 .vfx의 모든 Context/Block/Operator/GPU Event/Strip을 보존하여 복제합니다.")]
        public string buildMode = "Structured"; // Structured | CloneTemplate
        public string templateAssetPath = string.Empty;
        public bool preserveTemplateGraph = true;

        [Tooltip("AI가 반드시 고려할 VFX Graph 기능 태그입니다.")]
        public List<string> requiredCapabilities = new();

        public List<AbyssVFXSystemRecipe> systems = new();

        [Tooltip("석상처럼 단일 3D 오브젝트가 필요한 합성 VFX용입니다. 생성 Prefab의 자식 MeshRenderer로 구성됩니다.")]
        public List<AbyssVFXSceneObjectRecipe> sceneObjects = new();

        [Tooltip("생성된 VisualEffect Prefab에 기록할 Exposed Property 기본값입니다.")]
        public List<AbyssVFXExposedOverrideRecipe> exposedOverrides = new();
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

        [Header("Output Assets")]
        public string outputMode = "Quad"; // Quad | Mesh | TemplateOnly
        public string textureAssetPath = string.Empty;
        public string meshAssetPath = string.Empty;
        public string meshSubAssetName = string.Empty;
        public string materialAssetPath = string.Empty;
        public string shaderGraphAssetPath = string.Empty;

        [Header("Flipbook / Orientation Metadata")]
        public bool useFlipbook;
        public int flipbookColumns = 1;
        public int flipbookRows = 1;
        public bool flipbookBlend;
        public string orientationMode = "FaceCameraPlane";
        public bool orientToVelocity;

        [Header("Advanced Feature Contract")]
        [Tooltip("Strip, GPUEvent, Decal, SDF, SampleMesh, SampleSkinnedMesh 등 Template Graph에서 보존해야 하는 기능 태그입니다.")]
        public List<string> capabilities = new();
    }

    [Serializable]
    internal sealed class AbyssVFXSceneObjectRecipe
    {
        public string name = "Mesh Object";
        public string meshAssetPath = string.Empty;
        public string meshSubAssetName = string.Empty;
        public string materialAssetPath = string.Empty;
        public AbyssVector3 localPosition = new(0f, 0f, 0f);
        public AbyssVector3 localEulerAngles = new(0f, 0f, 0f);
        public AbyssVector3 localScale = new(1f, 1f, 1f);
        public bool castShadows = true;
        public bool receiveShadows = true;
    }

    [Serializable]
    internal sealed class AbyssVFXExposedOverrideRecipe
    {
        public string propertyName = string.Empty;
        public string valueType = "Float"; // Float | Int | Bool | Vector3 | Vector4 | Color | Texture | Mesh
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public AbyssVector3 vectorValue = new(0f, 0f, 0f);
        public AbyssColor colorValue = new(1f, 1f, 1f, 1f);
        public string assetPath = string.Empty;
        public string subAssetName = string.Empty;
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
