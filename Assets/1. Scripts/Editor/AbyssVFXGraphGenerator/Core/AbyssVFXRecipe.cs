#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    [Serializable]
    internal sealed class AbyssVFXRecipe
    {
        public string schemaVersion = "4.3";
        public string name = "VFX_Generated_3D";
        [TextArea(2, 6)] public string description = string.Empty;
        public string buildMode = "NodeGraph3D";
        public List<string> requiredCapabilities = new()
        {
            "MeshOutput",
            "NodeExpressions",
            "Procedural3D",
            "TextureFree"
        };
        public List<AbyssVFXSystemRecipe> systems = new();
        public AbyssVFXSurfaceAuraRecipe surfaceAura = new();
        public List<AbyssVFXExposedOverrideRecipe> exposedOverrides = new();
    }


    [Serializable]
    internal sealed class AbyssVFXSurfaceAuraRecipe
    {
        public bool enabled;
        public string shaderFamily = "SkinnedFlameAura";
        public string shaderAssetPath = "Assets/VFX/Shaders/AbyssSkinnedFlameAura.shader";
        public string rendererMode = "BackSilhouette";
        public bool autoBindToParent = true;
        public bool includeInactiveRenderers = true;
        public bool previewInEditMode = true;
        public List<string> excludedNameContains = new()
        {
            "Weapon", "Sword", "Blade", "Gun", "Rifle", "Bow"
        };

        [Header("Texture-free procedural HLSL palette")]
        public AbyssColor baseColor = new(1f, 0.16f, 0.01f, 1f);
        public AbyssColor edgeColor = new(1f, 0.72f, 0.08f, 1f);
        public AbyssColor hotColor = new(1f, 0.96f, 0.62f, 1f);

        [Header("Back silhouette core")]
        public float coreEmission = 4.2f;
        public float coreOpacity = 0.42f;
        public float coreFresnelPower = 2.8f;
        public float innerWidth = 0.025f;

        [Header("Outer rising flame shell")]
        public float flameEmission = 8.2f;
        public float flameOpacity = 0.82f;
        public float flameFresnelPower = 1.35f;
        public float outerWidth = 0.14f;
        public float rearOffset = 0.055f;
        public float flameLift = 0.18f;
        public float lateralWobble = 0.045f;

        [Header("Procedural flow")]
        public float noiseScale = 1.85f;
        public float detailNoiseScale = 4.6f;
        public float noiseSpeed = 0.95f;
        public float flameStretch = 2.4f;
        public float cutoff = 0.52f;
        public float edgeSoftness = 0.075f;
        public float rearBias = 0.82f;
        public float pulseSpeed = 1.15f;
        public float pulseAmount = 0.12f;
        public float boundsPadding = 0.42f;

        [Header("Camera-backed procedural Shin curtain (v4.3)")]
        public bool followCamera = true;
        public int curtainLayerCount = 3;
        public float curtainWidthMultiplier = 1.48f;
        public float curtainHeightMultiplier = 1.46f;
        public float curtainRearDistance = 0.14f;
        public float curtainVerticalOffset = 0.08f;
        public float curtainCurveDepth = 0.12f;
        public float curtainLayerDepthStep = 0.035f;
        public float curtainLayerScaleStep = 0.055f;
        public int curtainColumns = 32;
        public int curtainRows = 24;

        [Header("Reference-matched broad flame columns")]
        public float lobeSeparation = 0.38f;
        public float lobeWidth = 0.48f;
        public float tipVariation = 0.20f;
        public float sideFade = 0.18f;
        public float bottomFade = 0.07f;
        public float depthWobble = 0.028f;

        [Header("v4.1 compatibility; v4.2+ recipes should use core/flame fields")]
        [HideInInspector] public float emission = 5.4f;
        [HideInInspector] public float opacity = 0.74f;
        [HideInInspector] public float fresnelPower = 2.05f;
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
        public string velocityMode = "Radial"; // None | Radial | Directional
        public AbyssVector3 direction = new(0f, 1f, 0f);
        public AbyssFloatRange speed = new(2f, 7f);
        public float spread = 0.35f;
        public AbyssVector3 gravity = new(0f, -3f, 0f);
        public float drag = 0.5f;

        [Header("Optional built-in 3D force block")]
        public bool turbulenceEnabled;
        public float turbulenceIntensity = 1f;
        public float turbulenceFrequency = 1f;
        public float turbulenceDrag = 0.2f;
        public int turbulenceOctaves = 3;
        public float turbulenceRoughness = 0.5f;
        public float turbulenceLacunarity = 2f;

        public AbyssColor startColor = new(0.8f, 0.05f, 0.02f, 1f);
        public string blendMode = "Additive"; // Alpha | Additive | Premultiplied

        [Header("3D Mesh Output")]
        public string outputMode = "Mesh";
        public string proceduralMesh = "Sphere"; // Asset | Sphere | Cube | Cylinder | Capsule | Torus | ArcBlade | Shard
        public string meshAssetPath = string.Empty;
        public string meshSubAssetName = string.Empty;
        public float torusMajorRadius = 0.5f;
        public float torusMinorRadius = 0.08f;
        public int torusRadialSegments = 48;
        public int torusTubeSegments = 12;
        public string shaderGraphAssetPath = string.Empty;
        public string orientationMode = "FixedAxis"; // FixedAxis | OrientToVelocity
        public bool orientToVelocity;

        [Header("Expression overrides")]
        public AbyssVFXExpressionRecipe lifetimeExpression;
        public AbyssVFXExpressionRecipe sizeExpression;
        public AbyssVFXExpressionRecipe positionExpression;
        public AbyssVFXExpressionRecipe velocityExpression;
        public AbyssVFXExpressionRecipe colorExpression;
        public AbyssVFXExpressionRecipe alphaExpression;
        public AbyssVFXExpressionRecipe angleExpression;
        public AbyssVFXExpressionRecipe updateVelocityExpression;
        public AbyssVFXExpressionRecipe updatePositionExpression;
        public List<AbyssVFXCustomBlockRecipe> customBlocks = new();

        [Header("Compatibility fields; validation rejects non-empty 2D data")]
        [HideInInspector] public string textureAssetPath = string.Empty;
        [HideInInspector] public string materialAssetPath = string.Empty;
        [HideInInspector] public bool useFlipbook;
        [HideInInspector] public int flipbookColumns = 1;
        [HideInInspector] public int flipbookRows = 1;
        [HideInInspector] public bool flipbookBlend;
        public List<string> capabilities = new();
    }

    [Serializable]
    internal sealed class AbyssVFXExpressionRecipe
    {
        [Tooltip("LiteralFloat, LiteralVector3, LiteralColor, RandomFloat, RandomVector3, Add, Subtract, Multiply, Divide, Power, Sin, Cos, Abs, Normalize, Length, Clamp, Lerp, Remap, Time, DeltaTime, Attribute, Noise3D, CurlNoise3D, Operator")]
        public string kind = "LiteralFloat";
        public string valueType = "Float"; // Float | Vector2 | Vector3 | Vector4 | Color
        public float floatValue;
        public AbyssVector3 vectorValue = new(0f, 0f, 0f);
        public AbyssColor colorValue = new(1f, 1f, 1f, 1f);
        public AbyssFloatRange floatRange = new(0f, 1f);
        public AbyssVector3 vectorMin = new(-1f, -1f, -1f);
        public AbyssVector3 vectorMax = new(1f, 1f, 1f);
        public string attributeName = "position";
        public string operatorType = string.Empty;
        public string label = string.Empty;
        public List<AbyssVFXSettingRecipe> settings = new();
        public List<AbyssVFXExpressionRecipe> inputs = new();
    }

    [Serializable]
    internal sealed class AbyssVFXCustomBlockRecipe
    {
        public string context = "Update"; // Initialize | Update | Output
        public string blockType = string.Empty;
        public string label = string.Empty;
        public bool required = true;
        public List<AbyssVFXSettingRecipe> settings = new();
        public List<AbyssVFXBlockInputRecipe> inputs = new();
    }

    [Serializable]
    internal sealed class AbyssVFXBlockInputRecipe
    {
        public string slotName = string.Empty;
        public int slotIndex = -1;
        public AbyssVFXExpressionRecipe expression;
    }

    [Serializable]
    internal sealed class AbyssVFXSettingRecipe
    {
        public string name = string.Empty;
        public string valueType = "String"; // String | Float | Int | UInt | Bool | Vector3 | Color | Enum | Object
        public string stringValue = string.Empty;
        public float floatValue;
        public int intValue;
        public uint uintValue;
        public bool boolValue;
        public AbyssVector3 vectorValue = new(0f, 0f, 0f);
        public AbyssColor colorValue = new(1f, 1f, 1f, 1f);
        public string assetPath = string.Empty;
        public string subAssetName = string.Empty;
    }

    [Serializable]
    internal sealed class AbyssVFXExposedOverrideRecipe
    {
        public string propertyName = string.Empty;
        public string valueType = "Float"; // Float | Int | UInt | Bool | Vector3 | Vector4 | Color | Mesh
        public float floatValue;
        public int intValue;
        public uint uintValue;
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
        public AbyssFloatRange(float min, float max) { this.min = min; this.max = max; }
        public void Normalize() { if (min > max) (min, max) = (max, min); }
    }

    [Serializable]
    internal struct AbyssVector3
    {
        public float x;
        public float y;
        public float z;
        public AbyssVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
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
        public AbyssColor(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public readonly Color ToColor() => new(r, g, b, a);
        public static AbyssColor From(Color value) => new(value.r, value.g, value.b, value.a);
    }
}
#endif
