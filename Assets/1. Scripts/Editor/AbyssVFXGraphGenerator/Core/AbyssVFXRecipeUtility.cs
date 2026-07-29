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
            recipe.schemaVersion = string.IsNullOrWhiteSpace(recipe.schemaVersion) ? "1.0" : recipe.schemaVersion.Trim();
            recipe.name = SanitizeAssetName(string.IsNullOrWhiteSpace(recipe.name) ? "GeneratedVFX" : recipe.name);
            recipe.systems ??= new List<AbyssVFXSystemRecipe>();

            foreach (AbyssVFXSystemRecipe system in recipe.systems.Where(x => x != null))
            {
                system.name = string.IsNullOrWhiteSpace(system.name) ? "Particles" : system.name.Trim();
                system.spawnMode = NormalizeChoice(system.spawnMode, "Burst", "Burst", "Rate");
                system.positionMode = NormalizeChoice(system.positionMode, "Box", "Point", "Box");
                system.velocityMode = NormalizeChoice(system.velocityMode, "Radial", "None", "Radial", "Directional");
                system.blendMode = NormalizeChoice(system.blendMode, "Additive", "Alpha", "Additive", "Premultiplied");
                system.textureAssetPath ??= string.Empty;
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

            if (recipe.systems == null || recipe.systems.Count == 0)
                errors.Add("Particle System이 하나 이상 필요합니다.");
            else if (recipe.systems.Count > 12)
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
            }

            return errors;
        }

        internal static string BuildAIRequest(string userDescription, AbyssVFXRecipe currentRecipe)
        {
            StringBuilder builder = new();
            builder.AppendLine("Unity 6000.0.56f1 / URP 17.0.4 / Visual Effect Graph 17.0.4용 VFX Recipe JSON을 생성하라.");
            builder.AppendLine("응답은 설명이나 Markdown 없이 JSON 객체 하나만 출력한다.");
            builder.AppendLine("지원 범위: Spawn(Burst/Rate), Point/Box 위치, Radial/Directional 속도, Lifetime, Size, Gravity, Drag, 단색, Alpha/Additive/Premultiplied Quad Output.");
            builder.AppendLine("schemaVersion은 1.0, systems는 1~12개로 제한한다.");
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
