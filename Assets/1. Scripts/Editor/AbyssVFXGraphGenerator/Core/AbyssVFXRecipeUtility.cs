#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXRecipeUtility
    {
        private static readonly HashSet<string> ExpressionKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            "LiteralFloat", "LiteralVector3", "LiteralColor", "RandomFloat", "RandomVector3",
            "Add", "Subtract", "Multiply", "Divide", "Power", "Sin", "Cos", "Abs", "Normalize",
            "Length", "Clamp", "Lerp", "Remap", "Time", "DeltaTime", "Attribute", "Noise3D",
            "CurlNoise3D", "Operator"
        };

        internal static AbyssVFXRecipe CreateBlankRecipe()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Procedural3D_Test",
                description = "3D Mesh Output + 수학/난수/Noise 노드 검증용",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "TorusParticles",
                        proceduralMesh = "Torus",
                        orientationMode = "OrientToVelocity",
                        orientToVelocity = true,
                        turbulenceEnabled = true,
                        velocityExpression = Multiply(
                            Normalize(RandomVector3(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f))),
                            RandomFloat(2f, 7f)),
                        sizeExpression = Add(
                            Literal(0.07f),
                            Multiply(
                                new AbyssVFXExpressionRecipe { kind = "Sin", inputs = new List<AbyssVFXExpressionRecipe> { Time() } },
                                Literal(0.025f)))
                    }
                }
            };
        }

        internal static string ToJson(AbyssVFXRecipe recipe, bool pretty = true)
            => AbyssVFXJsonCodec.ToJson(recipe, pretty);

        internal static bool TryFromJson(string json, out AbyssVFXRecipe recipe, out string error)
        {
            recipe = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json)) { error = "Recipe JSON이 비어 있습니다."; return false; }
            if (!AbyssVFXStrictJson.ValidateContract(json, out List<string> contractErrors))
            {
                error = string.Join("\n", contractErrors);
                return false;
            }

            try
            {
                recipe = AbyssVFXJsonCodec.FromJson<AbyssVFXRecipe>(json);
                if (recipe == null) { error = "JSON을 Recipe로 변환하지 못했습니다."; return false; }
                MigrateAndNormalize(recipe);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static void MigrateAndNormalize(AbyssVFXRecipe recipe)
        {
            bool legacyV3 = string.Equals(recipe.schemaVersion, "3.0", StringComparison.OrdinalIgnoreCase);
            bool legacyV4 = string.Equals(recipe.schemaVersion, "4.0", StringComparison.OrdinalIgnoreCase);
            bool legacyV41 = string.Equals(recipe.schemaVersion, "4.1", StringComparison.OrdinalIgnoreCase);
            bool legacyV42 = string.Equals(recipe.schemaVersion, "4.2", StringComparison.OrdinalIgnoreCase);
            recipe.schemaVersion = "4.3";
            recipe.buildMode = "NodeGraph3D";
            recipe.name = SanitizeAssetName(string.IsNullOrWhiteSpace(recipe.name) ? "VFX_Generated_3D" : recipe.name);
            recipe.description ??= string.Empty;
            recipe.requiredCapabilities ??= new List<string>();
            if (legacyV3 || legacyV4 || legacyV41 || legacyV42)
            {
                AddUnique(recipe.requiredCapabilities, "MeshOutput");
                AddUnique(recipe.requiredCapabilities, "NodeExpressions");
                AddUnique(recipe.requiredCapabilities, "Procedural3D");
                AddUnique(recipe.requiredCapabilities, "TextureFree");
            }
            recipe.systems ??= new List<AbyssVFXSystemRecipe>();
            recipe.surfaceAura ??= new AbyssVFXSurfaceAuraRecipe();
            recipe.surfaceAura.shaderFamily = Choice(
                recipe.surfaceAura.shaderFamily,
                "SkinnedFlameAura",
                "SkinnedFlameAura",
                "ShinCurtainAura");
            recipe.surfaceAura.rendererMode = Choice(
                recipe.surfaceAura.rendererMode,
                recipe.surfaceAura.shaderFamily == "ShinCurtainAura" ? "CameraBackCurtain" : "BackSilhouette",
                "BackSilhouette",
                "CameraBackCurtain");
            if (recipe.surfaceAura.shaderFamily == "ShinCurtainAura")
                recipe.surfaceAura.rendererMode = "CameraBackCurtain";
            else if (recipe.surfaceAura.shaderFamily == "SkinnedFlameAura")
                recipe.surfaceAura.rendererMode = "BackSilhouette";
            string defaultAuraShader = recipe.surfaceAura.shaderFamily == "ShinCurtainAura"
                ? "Assets/VFX/Shaders/AbyssShinCurtainAura.shader"
                : "Assets/VFX/Shaders/AbyssSkinnedFlameAura.shader";
            recipe.surfaceAura.shaderAssetPath = string.IsNullOrWhiteSpace(recipe.surfaceAura.shaderAssetPath)
                ? defaultAuraShader
                : recipe.surfaceAura.shaderAssetPath.Replace('\\', '/');
            if (legacyV41 && string.Equals(
                    recipe.surfaceAura.shaderAssetPath,
                    "Assets/VFX/Shaders/AbyssShinSurfaceAura.shader",
                    StringComparison.OrdinalIgnoreCase))
            {
                recipe.surfaceAura.shaderAssetPath = "Assets/VFX/Shaders/AbyssSkinnedFlameAura.shader";
            }

            // v4.1의 단일 값은 v4.2의 Core/Flame 계층으로 이관한다.
            if (legacyV41)
            {
                recipe.surfaceAura.coreEmission = Mathf.Max(0f, recipe.surfaceAura.emission * 0.55f);
                recipe.surfaceAura.flameEmission = Mathf.Max(recipe.surfaceAura.coreEmission, recipe.surfaceAura.emission * 1.45f);
                recipe.surfaceAura.coreOpacity = Mathf.Clamp01(recipe.surfaceAura.opacity * 0.52f);
                recipe.surfaceAura.flameOpacity = Mathf.Clamp01(recipe.surfaceAura.opacity);
                recipe.surfaceAura.coreFresnelPower = Mathf.Clamp(recipe.surfaceAura.fresnelPower * 1.35f, 0.25f, 8f);
                recipe.surfaceAura.flameFresnelPower = Mathf.Clamp(recipe.surfaceAura.fresnelPower * 0.72f, 0.25f, 8f);
                recipe.surfaceAura.innerWidth = Mathf.Max(recipe.surfaceAura.innerWidth, 0.018f);
                recipe.surfaceAura.outerWidth = Mathf.Max(recipe.surfaceAura.outerWidth * 2.2f, 0.10f);
            }

            recipe.surfaceAura.excludedNameContains ??= new List<string>();
            recipe.surfaceAura.excludedNameContains = recipe.surfaceAura.excludedNameContains
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            recipe.surfaceAura.coreEmission = Mathf.Clamp(recipe.surfaceAura.coreEmission, 0f, 20f);
            recipe.surfaceAura.flameEmission = Mathf.Clamp(recipe.surfaceAura.flameEmission, 0f, 24f);
            recipe.surfaceAura.coreOpacity = Mathf.Clamp01(recipe.surfaceAura.coreOpacity);
            recipe.surfaceAura.flameOpacity = Mathf.Clamp01(recipe.surfaceAura.flameOpacity);
            recipe.surfaceAura.coreFresnelPower = Mathf.Clamp(recipe.surfaceAura.coreFresnelPower, 0.25f, 8f);
            recipe.surfaceAura.flameFresnelPower = Mathf.Clamp(recipe.surfaceAura.flameFresnelPower, 0.25f, 8f);
            recipe.surfaceAura.innerWidth = Mathf.Clamp(recipe.surfaceAura.innerWidth, 0f, 0.12f);
            recipe.surfaceAura.outerWidth = Mathf.Clamp(recipe.surfaceAura.outerWidth, 0f, 0.35f);
            recipe.surfaceAura.rearOffset = Mathf.Clamp(recipe.surfaceAura.rearOffset, 0f, 0.25f);
            recipe.surfaceAura.flameLift = Mathf.Clamp(recipe.surfaceAura.flameLift, 0f, 0.6f);
            recipe.surfaceAura.lateralWobble = Mathf.Clamp(recipe.surfaceAura.lateralWobble, 0f, 0.25f);
            recipe.surfaceAura.noiseScale = Mathf.Clamp(recipe.surfaceAura.noiseScale, 0.1f, 20f);
            recipe.surfaceAura.detailNoiseScale = Mathf.Clamp(recipe.surfaceAura.detailNoiseScale, 0.1f, 30f);
            recipe.surfaceAura.noiseSpeed = Mathf.Clamp(recipe.surfaceAura.noiseSpeed, -5f, 5f);
            recipe.surfaceAura.flameStretch = Mathf.Clamp(recipe.surfaceAura.flameStretch, 0.1f, 8f);
            recipe.surfaceAura.cutoff = Mathf.Clamp01(recipe.surfaceAura.cutoff);
            recipe.surfaceAura.edgeSoftness = Mathf.Clamp(recipe.surfaceAura.edgeSoftness, 0.001f, 0.35f);
            recipe.surfaceAura.rearBias = Mathf.Clamp01(recipe.surfaceAura.rearBias);
            recipe.surfaceAura.pulseSpeed = Mathf.Clamp(recipe.surfaceAura.pulseSpeed, 0f, 8f);
            recipe.surfaceAura.pulseAmount = Mathf.Clamp01(recipe.surfaceAura.pulseAmount);
            recipe.surfaceAura.boundsPadding = Mathf.Clamp(recipe.surfaceAura.boundsPadding, 0.02f, 2f);
            recipe.surfaceAura.curtainLayerCount = Mathf.Clamp(recipe.surfaceAura.curtainLayerCount, 1, 6);
            recipe.surfaceAura.curtainWidthMultiplier = Mathf.Clamp(recipe.surfaceAura.curtainWidthMultiplier, 0.5f, 3f);
            recipe.surfaceAura.curtainHeightMultiplier = Mathf.Clamp(recipe.surfaceAura.curtainHeightMultiplier, 0.5f, 3f);
            recipe.surfaceAura.curtainRearDistance = Mathf.Clamp(recipe.surfaceAura.curtainRearDistance, 0f, 2f);
            recipe.surfaceAura.curtainVerticalOffset = Mathf.Clamp(recipe.surfaceAura.curtainVerticalOffset, -0.75f, 0.75f);
            recipe.surfaceAura.curtainCurveDepth = Mathf.Clamp(recipe.surfaceAura.curtainCurveDepth, 0f, 1f);
            recipe.surfaceAura.curtainLayerDepthStep = Mathf.Clamp(recipe.surfaceAura.curtainLayerDepthStep, 0f, 0.5f);
            recipe.surfaceAura.curtainLayerScaleStep = Mathf.Clamp(recipe.surfaceAura.curtainLayerScaleStep, 0f, 0.3f);
            recipe.surfaceAura.curtainColumns = Mathf.Clamp(recipe.surfaceAura.curtainColumns, 4, 96);
            recipe.surfaceAura.curtainRows = Mathf.Clamp(recipe.surfaceAura.curtainRows, 4, 96);
            recipe.surfaceAura.lobeSeparation = Mathf.Clamp(recipe.surfaceAura.lobeSeparation, 0f, 0.9f);
            recipe.surfaceAura.lobeWidth = Mathf.Clamp(recipe.surfaceAura.lobeWidth, 0.05f, 1.2f);
            recipe.surfaceAura.tipVariation = Mathf.Clamp(recipe.surfaceAura.tipVariation, 0f, 0.6f);
            recipe.surfaceAura.sideFade = Mathf.Clamp(recipe.surfaceAura.sideFade, 0.01f, 0.8f);
            recipe.surfaceAura.bottomFade = Mathf.Clamp(recipe.surfaceAura.bottomFade, 0.001f, 0.5f);
            recipe.surfaceAura.depthWobble = Mathf.Clamp(recipe.surfaceAura.depthWobble, 0f, 0.25f);
            recipe.exposedOverrides ??= new List<AbyssVFXExposedOverrideRecipe>();

            foreach (AbyssVFXSystemRecipe system in recipe.systems.Where(x => x != null))
            {
                system.name = string.IsNullOrWhiteSpace(system.name) ? "Particles" : system.name.Trim();
                system.spawnMode = Choice(system.spawnMode, "Burst", "Burst", "Rate");
                system.positionMode = Choice(system.positionMode, "Box", "Point", "Box");
                system.velocityMode = Choice(system.velocityMode, "Radial", "None", "Radial", "Directional");
                system.blendMode = Choice(system.blendMode, "Additive", "Alpha", "Additive", "Premultiplied");
                system.outputMode = Choice(system.outputMode, "Mesh", "Mesh", "Quad", "TemplateOnly");
                system.proceduralMesh = Choice(system.proceduralMesh, "Sphere", "Asset", "Sphere", "Cube", "Cylinder", "Capsule", "Torus", "ArcBlade", "Shard");
                system.orientationMode = Choice(system.orientationMode, "FixedAxis", "FixedAxis", "OrientToVelocity", "FaceCameraPlane");
                system.orientToVelocity |= system.orientationMode == "OrientToVelocity";
                system.meshAssetPath ??= string.Empty;
                system.meshSubAssetName ??= string.Empty;
                system.shaderGraphAssetPath ??= string.Empty;
                system.textureAssetPath ??= string.Empty;
                system.materialAssetPath ??= string.Empty;
                system.capabilities ??= new List<string>();
                system.customBlocks ??= new List<AbyssVFXCustomBlockRecipe>();
                system.capacity = (uint)Mathf.Clamp((int)system.capacity, 1, 1_000_000);
                system.spawnCount = Mathf.Max(0f, system.spawnCount);
                system.spawnRate = Mathf.Max(0f, system.spawnRate);
                NormalizePositiveRange(ref system.lifetime, 0.001f);
                NormalizePositiveRange(ref system.size, 0.0001f);
                system.speed.Normalize();
                system.speed.min = Mathf.Max(0f, system.speed.min);
                system.speed.max = Mathf.Max(system.speed.min, system.speed.max);
                system.spread = Mathf.Clamp(system.spread, 0f, 1f);
                system.drag = Mathf.Max(0f, system.drag);
                system.turbulenceIntensity = Mathf.Max(0f, system.turbulenceIntensity);
                system.turbulenceFrequency = Mathf.Max(0.0001f, system.turbulenceFrequency);
                system.turbulenceDrag = Mathf.Max(0f, system.turbulenceDrag);
                system.turbulenceOctaves = Mathf.Clamp(system.turbulenceOctaves, 1, 8);
                system.turbulenceRoughness = Mathf.Clamp01(system.turbulenceRoughness);
                system.turbulenceLacunarity = Mathf.Max(1f, system.turbulenceLacunarity);
                system.torusMajorRadius = Mathf.Max(0.001f, system.torusMajorRadius);
                system.torusMinorRadius = Mathf.Clamp(system.torusMinorRadius, 0.001f, system.torusMajorRadius);
                system.torusRadialSegments = Mathf.Clamp(system.torusRadialSegments, 3, 256);
                system.torusTubeSegments = Mathf.Clamp(system.torusTubeSegments, 3, 128);
                system.flipbookColumns = Mathf.Max(1, system.flipbookColumns);
                system.flipbookRows = Mathf.Max(1, system.flipbookRows);
                NormalizeExpression(system.lifetimeExpression);
                NormalizeExpression(system.sizeExpression);
                NormalizeExpression(system.positionExpression);
                NormalizeExpression(system.velocityExpression);
                NormalizeExpression(system.colorExpression);
                NormalizeExpression(system.alphaExpression);
                NormalizeExpression(system.angleExpression);
                NormalizeExpression(system.updateVelocityExpression);
                NormalizeExpression(system.updatePositionExpression);
                foreach (AbyssVFXCustomBlockRecipe block in system.customBlocks.Where(x => x != null))
                {
                    block.context = Choice(block.context, "Update", "Initialize", "Update", "Output");
                    block.blockType ??= string.Empty;
                    block.label ??= string.Empty;
                    block.settings ??= new List<AbyssVFXSettingRecipe>();
                    block.inputs ??= new List<AbyssVFXBlockInputRecipe>();
                    foreach (AbyssVFXBlockInputRecipe input in block.inputs.Where(x => x != null)) NormalizeExpression(input.expression);
                }
            }
        }

        internal static IReadOnlyList<string> Validate(AbyssVFXRecipe recipe, out IReadOnlyList<string> warnings)
        {
            List<string> errors = new();
            List<string> warningList = new();
            warnings = warningList;
            if (recipe == null) { errors.Add("Recipe가 없습니다."); return errors; }
            if (recipe.schemaVersion != "4.3") errors.Add("schemaVersion은 4.3이어야 합니다.");
            if (recipe.buildMode != "NodeGraph3D") errors.Add("buildMode는 NodeGraph3D여야 합니다.");
            if (string.IsNullOrWhiteSpace(recipe.name)) errors.Add("name이 비어 있습니다.");
            RequireCapability(recipe.requiredCapabilities, "MeshOutput", errors);
            RequireCapability(recipe.requiredCapabilities, "NodeExpressions", errors);
            RequireCapability(recipe.requiredCapabilities, "Procedural3D", errors);
            RequireCapability(recipe.requiredCapabilities, "TextureFree", errors);
            if (recipe.systems == null || recipe.systems.Count == 0) errors.Add("System이 하나 이상 필요합니다.");
            if (recipe.systems?.Count > 24) errors.Add("System은 최대 24개입니다.");
            if (recipe.exposedOverrides != null && recipe.exposedOverrides.Count > 0)
                errors.Add("v4.3은 죽은 Prefab override를 허용하지 않습니다. Graph Parameter 노드가 구현되기 전 exposedOverrides는 비워 두세요.");

            if (recipe.surfaceAura?.enabled == true)
            {
                bool skinnedMode = string.Equals(recipe.surfaceAura.shaderFamily, "SkinnedFlameAura", StringComparison.Ordinal) &&
                                   string.Equals(recipe.surfaceAura.rendererMode, "BackSilhouette", StringComparison.Ordinal);
                bool curtainMode = string.Equals(recipe.surfaceAura.shaderFamily, "ShinCurtainAura", StringComparison.Ordinal) &&
                                   string.Equals(recipe.surfaceAura.rendererMode, "CameraBackCurtain", StringComparison.Ordinal);
                if (!skinnedMode && !curtainMode)
                    errors.Add("surfaceAura shaderFamily/rendererMode 조합은 SkinnedFlameAura+BackSilhouette 또는 ShinCurtainAura+CameraBackCurtain만 지원합니다.");
                if (string.IsNullOrWhiteSpace(recipe.surfaceAura.shaderAssetPath))
                    errors.Add("surfaceAura.shaderAssetPath가 비어 있습니다.");
                else if (!recipe.surfaceAura.shaderAssetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    errors.Add("surfaceAura.shaderAssetPath는 일반 URP .shader 에셋이어야 합니다.");

                if (skinnedMode)
                {
                    if (recipe.surfaceAura.outerWidth <= recipe.surfaceAura.innerWidth)
                        errors.Add("surfaceAura.outerWidth는 innerWidth보다 커야 뒤쪽 Flame Silhouette가 형성됩니다.");
                    if (recipe.surfaceAura.rearOffset <= 0.0001f)
                        warningList.Add("surfaceAura.rearOffset이 0입니다. 오라가 캐릭터 표면 코팅처럼 보일 수 있습니다.");
                    if (recipe.surfaceAura.boundsPadding < recipe.surfaceAura.outerWidth + recipe.surfaceAura.flameLift)
                        warningList.Add("surfaceAura.boundsPadding이 외곽 변형 폭보다 작아 카메라 각도에 따라 오라가 컬링될 수 있습니다.");
                }

                if (curtainMode)
                {
                    if (recipe.surfaceAura.curtainWidthMultiplier <= 1f)
                        warningList.Add("신 Curtain 폭이 캐릭터 폭과 같거나 작습니다. 레퍼런스처럼 양옆으로 넓게 보이려면 1.25 이상을 권장합니다.");
                    if (recipe.surfaceAura.curtainHeightMultiplier <= 1f)
                        warningList.Add("신 Curtain 높이가 캐릭터 높이와 같거나 작습니다. 머리 위 Flame Tip을 위해 1.25 이상을 권장합니다.");
                    if (recipe.surfaceAura.curtainRearDistance <= 0.0001f)
                        warningList.Add("curtainRearDistance가 0입니다. 오라가 캐릭터와 Z-fighting할 수 있습니다.");
                    if (recipe.surfaceAura.curtainColumns < 8 || recipe.surfaceAura.curtainRows < 8)
                        warningList.Add("Curtain mesh subdivision이 낮아 HLSL Vertex Wobble이 각져 보일 수 있습니다.");
                }

                if (recipe.surfaceAura.coreEmission < recipe.surfaceAura.flameEmission)
                    warningList.Add("레퍼런스의 밝은 세로 Core를 강조하려면 coreEmission이 flameEmission 이상인 편이 좋습니다.");
            }

            if (recipe.systems == null) return errors;
            for (int i = 0; i < recipe.systems.Count; i++)
            {
                AbyssVFXSystemRecipe s = recipe.systems[i];
                string p = $"System {i + 1}";
                if (s == null) { errors.Add(p + ": null입니다."); continue; }
                if (s.outputMode != "Mesh") errors.Add(p + ": outputMode는 Mesh만 허용합니다.");
                if (!string.IsNullOrWhiteSpace(s.textureAssetPath) || s.useFlipbook || s.flipbookBlend || s.flipbookColumns > 1 || s.flipbookRows > 1)
                    errors.Add(p + ": Texture/Flipbook/2D Sprite 출력은 금지됩니다.");
                if (s.orientationMode == "FaceCameraPlane") errors.Add(p + ": Billboard 방향은 금지됩니다.");
                if (!string.IsNullOrWhiteSpace(s.materialAssetPath)) errors.Add(p + ": 일반 Material은 금지됩니다. VFX Shader Graph만 사용하세요.");
                if (s.proceduralMesh == "Asset" && string.IsNullOrWhiteSpace(s.meshAssetPath)) errors.Add(p + ": Asset Mesh 경로가 필요합니다.");
                if (s.spawnMode == "Burst" && s.spawnCount <= 0f) errors.Add(p + ": spawnCount는 0보다 커야 합니다.");
                if (s.spawnMode == "Rate" && s.spawnRate <= 0f) errors.Add(p + ": spawnRate는 0보다 커야 합니다.");
                if (s.spawnCount > s.capacity) warningList.Add(p + ": spawnCount가 capacity보다 큽니다.");
                ValidateExpression(s.lifetimeExpression, p + ".lifetimeExpression", errors, 0);
                ValidateExpression(s.sizeExpression, p + ".sizeExpression", errors, 0);
                ValidateExpression(s.positionExpression, p + ".positionExpression", errors, 0);
                ValidateExpression(s.velocityExpression, p + ".velocityExpression", errors, 0);
                ValidateExpression(s.colorExpression, p + ".colorExpression", errors, 0);
                ValidateExpression(s.alphaExpression, p + ".alphaExpression", errors, 0);
                ValidateExpression(s.angleExpression, p + ".angleExpression", errors, 0);
                ValidateExpression(s.updateVelocityExpression, p + ".updateVelocityExpression", errors, 0);
                ValidateExpression(s.updatePositionExpression, p + ".updatePositionExpression", errors, 0);
                foreach (AbyssVFXCustomBlockRecipe block in s.customBlocks ?? new List<AbyssVFXCustomBlockRecipe>())
                {
                    if (block == null) continue;
                    if (string.IsNullOrWhiteSpace(block.blockType)) errors.Add(p + ": customBlock.blockType이 비어 있습니다.");
                    foreach (AbyssVFXBlockInputRecipe input in block.inputs ?? new List<AbyssVFXBlockInputRecipe>())
                        if (input != null) ValidateExpression(input.expression, p + ".customBlock.input", errors, 0);
                }
            }
            return errors;
        }

        internal static string BuildAIRequest(string prompt, AbyssVFXRecipe currentRecipe)
        {
            StringBuilder b = new();
            b.AppendLine("Project Abyss VFX Recipe schemaVersion 4.3 JSON만 반환하세요.");
            b.AppendLine("정책: buildMode=NodeGraph3D, outputMode=Mesh, Texture/Flipbook/Billboard 금지.");
            b.AppendLine("지속형 황금 오라는 surfaceAura의 ShinCurtainAura+CameraBackCurtain을 우선 사용하고, VFX systems는 소량의 불씨 같은 보조 계층에만 사용하세요.");
            b.AppendLine("수식은 expression kind(Add/Multiply/Sin/Cos/Normalize/RandomFloat/RandomVector3/Noise3D/CurlNoise3D 등)로 구성하세요.");
            b.AppendLine("효과 설명:"); b.AppendLine(prompt ?? string.Empty);
            b.AppendLine("현재 Recipe:"); b.AppendLine(ToJson(currentRecipe ?? CreateBlankRecipe(), true));
            return b.ToString();
        }

        internal static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "VFX_Generated_3D";
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim().Replace(' ', '_');
        }

        internal static AbyssVFXExpressionRecipe Literal(float value) => new() { kind = "LiteralFloat", valueType = "Float", floatValue = value };
        internal static AbyssVFXExpressionRecipe RandomFloat(float min, float max) => new() { kind = "RandomFloat", valueType = "Float", floatRange = new AbyssFloatRange(min, max) };
        internal static AbyssVFXExpressionRecipe RandomVector3(Vector3 min, Vector3 max) => new() { kind = "RandomVector3", valueType = "Vector3", vectorMin = AbyssVector3.From(min), vectorMax = AbyssVector3.From(max) };
        internal static AbyssVFXExpressionRecipe Normalize(AbyssVFXExpressionRecipe input) => Unary("Normalize", input, "Vector3");
        internal static AbyssVFXExpressionRecipe Time() => new() { kind = "Time", valueType = "Float" };
        internal static AbyssVFXExpressionRecipe Add(AbyssVFXExpressionRecipe a, AbyssVFXExpressionRecipe b) => Binary("Add", a, b);
        internal static AbyssVFXExpressionRecipe Multiply(AbyssVFXExpressionRecipe a, AbyssVFXExpressionRecipe b) => Binary("Multiply", a, b);

        private static AbyssVFXExpressionRecipe Unary(string kind, AbyssVFXExpressionRecipe input, string type) => new() { kind = kind, valueType = type, inputs = new List<AbyssVFXExpressionRecipe> { input } };
        private static AbyssVFXExpressionRecipe Binary(string kind, AbyssVFXExpressionRecipe a, AbyssVFXExpressionRecipe b) => new() { kind = kind, inputs = new List<AbyssVFXExpressionRecipe> { a, b } };

        private static void NormalizeExpression(AbyssVFXExpressionRecipe expression)
        {
            if (expression == null) return;
            expression.kind = ExpressionKinds.FirstOrDefault(k => string.Equals(k, expression.kind, StringComparison.OrdinalIgnoreCase)) ?? expression.kind?.Trim() ?? string.Empty;
            expression.valueType = Choice(expression.valueType, "Float", "Float", "Vector2", "Vector3", "Vector4", "Color");
            expression.attributeName ??= string.Empty;
            expression.operatorType ??= string.Empty;
            expression.label ??= string.Empty;
            expression.settings ??= new List<AbyssVFXSettingRecipe>();
            expression.inputs ??= new List<AbyssVFXExpressionRecipe>();
            expression.floatRange.Normalize();
            foreach (AbyssVFXExpressionRecipe input in expression.inputs) NormalizeExpression(input);
        }

        private static void ValidateExpression(AbyssVFXExpressionRecipe expression, string path, List<string> errors, int depth)
        {
            if (expression == null) return;
            if (depth > 32) { errors.Add(path + ": 식 깊이는 최대 32입니다."); return; }
            if (!ExpressionKinds.Contains(expression.kind)) errors.Add(path + $": 지원하지 않는 kind '{expression.kind}'.");
            if (expression.kind == "Operator" && string.IsNullOrWhiteSpace(expression.operatorType)) errors.Add(path + ": Operator에는 operatorType이 필요합니다.");
            if (expression.kind == "Attribute" && string.IsNullOrWhiteSpace(expression.attributeName)) errors.Add(path + ": Attribute에는 attributeName이 필요합니다.");
            int expected = expression.kind switch
            {
                "Add" or "Subtract" or "Multiply" or "Divide" or "Power" => 2,
                "Sin" or "Cos" or "Abs" or "Normalize" or "Length" or "Noise3D" or "CurlNoise3D" => 1,
                "Clamp" => 3,
                "Lerp" => 3,
                "Remap" => 5,
                _ => -1
            };
            if (expected >= 0 && (expression.inputs?.Count ?? 0) != expected) errors.Add(path + $": {expression.kind} 입력은 {expected}개여야 합니다.");
            for (int i = 0; i < (expression.inputs?.Count ?? 0); i++) ValidateExpression(expression.inputs[i], $"{path}.inputs[{i}]", errors, depth + 1);
        }

        private static void NormalizePositiveRange(ref AbyssFloatRange range, float minimum)
        {
            range.Normalize(); range.min = Mathf.Max(minimum, range.min); range.max = Mathf.Max(range.min, range.max);
        }
        private static string Choice(string value, string fallback, params string[] choices) => choices.FirstOrDefault(choice => string.Equals(value, choice, StringComparison.OrdinalIgnoreCase)) ?? fallback;
        private static void AddUnique(List<string> list, string value) { if (!list.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))) list.Add(value); }
        private static void RequireCapability(List<string> list, string value, List<string> errors) { if (list == null || !list.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))) errors.Add($"requiredCapabilities에 {value}가 필요합니다."); }
    }
}
#endif
