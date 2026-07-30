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
        internal static string ToJson(AbyssVFXRecipe recipe, bool pretty = true)
        {
            return JsonUtility.ToJson(recipe, pretty);
        }

        internal static bool TryFromJson(string json, out AbyssVFXRecipe recipe, out string error)
        {
            recipe = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Recipe JSON이 비어 있습니다.";
                return false;
            }

            try
            {
                recipe = JsonUtility.FromJson<AbyssVFXRecipe>(json);
                if (recipe == null)
                {
                    error = "JSON을 Recipe로 변환하지 못했습니다.";
                    return false;
                }

                recipe.systems ??= new List<AbyssVFXSystemRecipe>();
                Normalize(recipe);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static void Normalize(AbyssVFXRecipe recipe)
        {
            recipe.schemaVersion = "2.0";
            recipe.buildMode = NormalizeChoice(recipe.buildMode, "Structured", "Structured", "CloneTemplate");
            recipe.templateAssetPath ??= string.Empty;
            recipe.requiredCapabilities ??= new List<string>();
            recipe.sceneObjects ??= new List<AbyssVFXSceneObjectRecipe>();
            recipe.exposedOverrides ??= new List<AbyssVFXExposedOverrideRecipe>();
            recipe.name = SanitizeAssetName(string.IsNullOrWhiteSpace(recipe.name) ? "GeneratedVFX" : recipe.name);
            recipe.systems ??= new List<AbyssVFXSystemRecipe>();

            foreach (AbyssVFXSystemRecipe system in recipe.systems.Where(x => x != null))
            {
                system.name = string.IsNullOrWhiteSpace(system.name) ? "Particles" : system.name.Trim();
                system.spawnMode = NormalizeChoice(system.spawnMode, "Burst", "Burst", "Rate");
                system.positionMode = NormalizeChoice(system.positionMode, "Box", "Point", "Box");
                system.velocityMode = NormalizeChoice(system.velocityMode, "Radial", "None", "Radial", "Directional");
                system.blendMode = NormalizeChoice(system.blendMode, "Additive", "Alpha", "Additive", "Premultiplied");
                system.outputMode = NormalizeChoice(system.outputMode, "Quad", "Quad", "Mesh", "TemplateOnly");
                system.textureAssetPath ??= string.Empty;
                system.meshAssetPath ??= string.Empty;
                system.meshSubAssetName ??= string.Empty;
                system.materialAssetPath ??= string.Empty;
                system.shaderGraphAssetPath ??= string.Empty;
                system.orientationMode ??= "FaceCameraPlane";
                system.capabilities ??= new List<string>();
                system.flipbookColumns = Mathf.Max(1, system.flipbookColumns);
                system.flipbookRows = Mathf.Max(1, system.flipbookRows);
                if (system.capacity < 1u) system.capacity = 1u;
                else if (system.capacity > 1_000_000u) system.capacity = 1_000_000u;
                system.spawnCount = Mathf.Max(0f, system.spawnCount);
                system.spawnRate = Mathf.Max(0f, system.spawnRate);
                system.lifetime.Normalize();
                system.size.Normalize();
                system.speed.Normalize();
                system.lifetime.min = Mathf.Max(0.001f, system.lifetime.min);
                system.lifetime.max = Mathf.Max(system.lifetime.min, system.lifetime.max);
                system.size.min = Mathf.Max(0.0001f, system.size.min);
                system.size.max = Mathf.Max(system.size.min, system.size.max);
                system.speed.min = Mathf.Max(0f, system.speed.min);
                system.speed.max = Mathf.Max(system.speed.min, system.speed.max);
                system.spread = Mathf.Max(0f, system.spread);
                system.drag = Mathf.Max(0f, system.drag);
            }

            foreach (AbyssVFXSceneObjectRecipe sceneObject in recipe.sceneObjects.Where(x => x != null))
            {
                sceneObject.name = string.IsNullOrWhiteSpace(sceneObject.name) ? "Mesh Object" : sceneObject.name.Trim();
                sceneObject.meshAssetPath ??= string.Empty;
                sceneObject.meshSubAssetName ??= string.Empty;
                sceneObject.materialAssetPath ??= string.Empty;
                Vector3 scale = sceneObject.localScale.ToVector3();
                if (scale == Vector3.zero)
                    sceneObject.localScale = new AbyssVector3(1f, 1f, 1f);
            }

            foreach (AbyssVFXExposedOverrideRecipe exposed in recipe.exposedOverrides.Where(x => x != null))
            {
                exposed.propertyName ??= string.Empty;
                exposed.valueType = NormalizeChoice(
                    exposed.valueType,
                    "Float",
                    "Float", "Int", "Bool", "Vector3", "Vector4", "Color", "Texture", "Mesh");
                exposed.assetPath ??= string.Empty;
                exposed.subAssetName ??= string.Empty;
            }
        }

        internal static IReadOnlyList<string> Validate(AbyssVFXRecipe recipe, out IReadOnlyList<string> warnings)
        {
            List<string> errors = new();
            List<string> warningList = new();
            warnings = warningList;

            if (recipe == null)
            {
                errors.Add("Recipe가 없습니다.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(recipe.name))
                errors.Add("Recipe name이 비어 있습니다.");

            bool cloneTemplate = string.Equals(
                recipe.buildMode,
                "CloneTemplate",
                StringComparison.OrdinalIgnoreCase);

            if (cloneTemplate && string.IsNullOrWhiteSpace(recipe.templateAssetPath))
                errors.Add("CloneTemplate 모드는 templateAssetPath가 필요합니다.");

            if (cloneTemplate && !recipe.preserveTemplateGraph &&
                (recipe.systems == null || recipe.systems.Count == 0))
            {
                errors.Add(
                    "CloneTemplate에서 Preserve Full Graph를 끄면 새로 만들 System이 하나 이상 필요합니다.");
            }

            if (!cloneTemplate && (recipe.systems == null || recipe.systems.Count == 0))
                errors.Add("Structured 모드는 Particle System이 하나 이상 필요합니다.");
            else if (recipe.systems != null && recipe.systems.Count > 12)
                errors.Add("한 Recipe에는 최대 12개의 System만 허용합니다.");

            if (recipe.systems == null)
                return errors;

            for (int i = 0; i < recipe.systems.Count; i++)
            {
                AbyssVFXSystemRecipe system = recipe.systems[i];
                string prefix = $"System {i + 1}";
                if (system == null)
                {
                    errors.Add($"{prefix}: null 항목입니다.");
                    continue;
                }

                if (system.capacity == 0)
                    errors.Add($"{prefix}: capacity는 1 이상이어야 합니다.");
                if (system.spawnMode == "Burst" && system.spawnCount <= 0f)
                    errors.Add($"{prefix}: Burst spawnCount는 0보다 커야 합니다.");
                if (system.spawnMode == "Rate" && system.spawnRate <= 0f)
                    errors.Add($"{prefix}: Rate spawnRate는 0보다 커야 합니다.");
                if (system.lifetime.min <= 0f || system.lifetime.max <= 0f)
                    errors.Add($"{prefix}: lifetime은 0보다 커야 합니다.");
                if (system.size.min <= 0f || system.size.max <= 0f)
                    errors.Add($"{prefix}: size는 0보다 커야 합니다.");
                if (system.spawnMode == "Burst" && system.spawnCount > system.capacity)
                    warningList.Add($"{prefix}: spawnCount가 capacity보다 큽니다. 일부 파티클이 잘릴 수 있습니다.");
                if (system.capacity > 100_000)
                    warningList.Add($"{prefix}: capacity가 100,000을 초과합니다. GPU 비용을 확인하세요.");
                if (!string.IsNullOrEmpty(system.textureAssetPath) && !system.textureAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    warningList.Add($"{prefix}: textureAssetPath는 Assets/ 경로여야 자동 연결됩니다.");
                if (system.outputMode == "Mesh" && string.IsNullOrWhiteSpace(system.meshAssetPath))
                    errors.Add($"{prefix}: Mesh Output은 meshAssetPath가 필요합니다.");
                if (!string.IsNullOrEmpty(system.meshAssetPath) && !system.meshAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    warningList.Add($"{prefix}: meshAssetPath는 Assets/ 경로여야 자동 연결됩니다.");
                if ((system.capabilities?.Count ?? 0) > 0 && !cloneTemplate)
                {
                    string advanced = string.Join(", ", system.capabilities);
                    warningList.Add($"{prefix}: 고급 기능 [{advanced}]은 Reference/Template Graph 복제를 권장합니다.");
                }
            }

            if (recipe.sceneObjects != null)
            {
                for (int i = 0; i < recipe.sceneObjects.Count; i++)
                {
                    AbyssVFXSceneObjectRecipe sceneObject =
                        recipe.sceneObjects[i];

                    if (sceneObject == null)
                        continue;

                    string prefix = $"Scene Object {i + 1}";
                    if (string.IsNullOrWhiteSpace(
                            sceneObject.meshAssetPath))
                    {
                        errors.Add(
                            $"{prefix}: meshAssetPath가 필요합니다.");
                    }
                    else if (!sceneObject.meshAssetPath.StartsWith(
                                 "Assets/",
                                 StringComparison.Ordinal))
                    {
                        warningList.Add(
                            $"{prefix}: meshAssetPath는 Assets/ 경로여야 자동 연결됩니다.");
                    }

                    if (!string.IsNullOrWhiteSpace(
                            sceneObject.materialAssetPath) &&
                        !sceneObject.materialAssetPath.StartsWith(
                            "Assets/",
                            StringComparison.Ordinal))
                    {
                        warningList.Add(
                            $"{prefix}: materialAssetPath는 Assets/ 경로여야 자동 연결됩니다.");
                    }
                }
            }

            if (recipe.exposedOverrides != null)
            {
                for (int i = 0; i < recipe.exposedOverrides.Count; i++)
                {
                    AbyssVFXExposedOverrideRecipe entry =
                        recipe.exposedOverrides[i];

                    if (entry == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(entry.propertyName))
                    {
                        warningList.Add(
                            $"Exposed Override {i + 1}: propertyName이 비어 있어 무시됩니다.");
                    }

                    if ((entry.valueType == "Texture" ||
                         entry.valueType == "Mesh") &&
                        string.IsNullOrWhiteSpace(entry.assetPath))
                    {
                        warningList.Add(
                            $"Exposed Override {i + 1}: {entry.valueType} assetPath가 비어 있습니다.");
                    }
                }
            }

            return errors;
        }

        internal static string BuildAIRequest(string userDescription, AbyssVFXRecipe currentRecipe)
        {
            StringBuilder builder = new();
            builder.AppendLine("Unity 6000.0.56f1 / URP 17.0.4 / Visual Effect Graph 17.0.4용 Project Abyss VFX Recipe 2.0 JSON을 생성하라.");
            builder.AppendLine("응답은 설명이나 Markdown 없이 JSON 객체 하나만 출력한다.");
            builder.AppendLine("Structured 모드는 기본 Spawn/Initialize/Update/Quad 또는 Mesh Output과 복합 Prefab Mesh Object를 생성한다.");
            builder.AppendLine("Strip, GPU Event, Trigger Event, Decal, SDF, Sample Mesh, Sample Skinned Mesh, Collision, Multiple Outputs, Shader Graph, Flipbook/TexIndex/Pivot 같은 고급 그래프는 CloneTemplate 모드를 사용하여 기존 .vfx의 전체 그래프를 보존한다.");
            builder.AppendLine("3D Mesh는 meshAssetPath 또는 sceneObjects[].meshAssetPath로 사용자가 제공한다. 일반 MeshRenderer 재질은 materialAssetPath, VFX Mesh Output의 외형은 VFX 호환 Shader Graph/Texture 경로로 지정한다.");
            builder.AppendLine("지원 Capability 예: Contexts, MultipleOutputs, Bounds, FaceCamera, FixedAxis, OrientAdvanced, Rotation, AngularVelocity, TexIndex, Flipbook, FlipbookBlend, Pivot, SampleMesh, SampleTexture2D, SDF, SampleSkinnedMesh, Collision, CollisionEvent, Decal, Strip, MultiStrip, GPUEvent.");
            builder.AppendLine();
            builder.AppendLine("요청 효과:");
            builder.AppendLine(string.IsNullOrWhiteSpace(userDescription) ? "현재 Recipe를 더 완성도 있게 조정" : userDescription.Trim());
            builder.AppendLine();
            builder.AppendLine("현재 Recipe 또는 시작 예시:");
            builder.AppendLine(ToJson(currentRecipe, true));
            return builder.ToString();
        }

        internal static string SanitizeAssetName(string value)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string result = new(value.Where(c => !invalid.Contains(c) && c != '/').ToArray());
            return string.IsNullOrWhiteSpace(result) ? "GeneratedVFX" : result.Trim();
        }

        private static string NormalizeChoice(string value, string fallback, params string[] choices)
        {
            foreach (string choice in choices)
            {
                if (string.Equals(value, choice, StringComparison.OrdinalIgnoreCase))
                    return choice;
            }
            return fallback;
        }
    }
}
#endif