#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;
using Block = UnityEditor.VFX.Block;

namespace ProjectAbyss.Editor.VFXAI
{
    internal sealed class AbyssVFXBuildResult
    {
        public VisualEffectAsset asset;
        public string assetPath;
        public string prefabPath;
        public string seedPath;
        public readonly List<string> warnings = new();
        public int systemCount;
    }

    internal static class AbyssVFXGraphBuilder17
    {
        private const float ContextGapY = 360f;
        private const float SystemGapX = 620f;

        internal static AbyssVFXBuildResult Build(
            AbyssVFXRecipe recipe,
            string outputFolder,
            VisualEffectAsset seedAsset,
            bool overwrite,
            bool createPrefab)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            AbyssVFXRecipeUtility.Normalize(recipe);
            IReadOnlyList<string> errors = AbyssVFXRecipeUtility.Validate(recipe, out IReadOnlyList<string> validationWarnings);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));

            string folder = NormalizeFolder(outputFolder);
            string assetPath = $"{folder}/{AbyssVFXRecipeUtility.SanitizeAssetName(recipe.name)}.vfx";
            VisualEffectAsset asset = AbyssVFXAssetFactory.CreateOrLoad(assetPath, seedAsset, overwrite, out assetPath, out string seedPath);

            var resource = asset.GetResource();
            if (resource == null)
                throw new InvalidOperationException("VisualEffectAsset.GetResource()가 null입니다.");

            VFXGraph graph = resource.GetOrCreateGraph();
            if (graph == null)
                throw new InvalidOperationException("VFX Graph 내부 모델을 생성하지 못했습니다.");

            Undo.RegisterCompleteObjectUndo(resource, "Build Project Abyss AI VFX Graph");
            graph.RemoveAllChildren();

            AbyssVFXBuildResult result = new()
            {
                asset = asset,
                assetPath = assetPath,
                seedPath = seedPath,
                systemCount = recipe.systems.Count
            };
            result.warnings.AddRange(validationWarnings);

            for (int i = 0; i < recipe.systems.Count; i++)
                BuildSystem(graph, recipe.systems[i], i, result.warnings);

            EditorUtility.SetDirty(graph);
            EditorUtility.SetDirty(resource);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(resource), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (createPrefab)
                result.prefabPath = CreatePrefab(asset, folder, Path.GetFileNameWithoutExtension(assetPath));

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void BuildSystem(VFXGraph graph, AbyssVFXSystemRecipe system, int systemIndex, List<string> warnings)
        {
            float x = systemIndex * SystemGapX;
            string labelPrefix = $"{systemIndex + 1:00}_{system.name}";

            VFXBasicSpawner spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            spawner.label = labelPrefix + "_Spawn";
            spawner.position = new Vector2(x, 0f);
            AddSpawnerBlock(spawner, system);

            VFXBasicInitialize initialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            initialize.label = labelPrefix + "_Initialize";
            initialize.position = new Vector2(x, ContextGapY);
            initialize.SetSettingValue("capacity", system.capacity);
            AddInitializeBlocks(initialize, system);

            VFXBasicUpdate update = ScriptableObject.CreateInstance<VFXBasicUpdate>();
            update.label = labelPrefix + "_Update";
            update.position = new Vector2(x, ContextGapY * 2f);
            AddUpdateBlocks(update, system, warnings);

            VFXPlanarPrimitiveOutput output = ScriptableObject.CreateInstance<VFXPlanarPrimitiveOutput>();
            output.label = labelPrefix + "_Output";
            output.position = new Vector2(x, ContextGapY * 3f);
            ConfigureOutput(output, system, warnings);

            spawner.LinkTo(initialize);
            initialize.LinkTo(update);
            update.LinkTo(output);

            graph.AddChild(spawner);
            graph.AddChild(initialize);
            graph.AddChild(update);
            graph.AddChild(output);
        }

        private static void AddSpawnerBlock(VFXBasicSpawner spawner, AbyssVFXSystemRecipe system)
        {
            if (string.Equals(system.spawnMode, "Rate", StringComparison.OrdinalIgnoreCase))
            {
                VFXSpawnerConstantRate rate = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
                AssignRequired(rate, 0, system.spawnRate, "Spawn Rate");
                spawner.AddChild(rate);
                return;
            }

            VFXSpawnerBurst burst = ScriptableObject.CreateInstance<VFXSpawnerBurst>();
            AssignRequired(burst, 0, system.spawnCount, "Burst Count");
            spawner.AddChild(burst);
        }

        private static void AddInitializeBlocks(VFXBasicInitialize initialize, AbyssVFXSystemRecipe system)
        {
            AddFloatAttribute(initialize, VFXAttribute.Lifetime, system.lifetime.min, system.lifetime.max);
            AddFloatAttribute(initialize, VFXAttribute.Size, system.size.min, system.size.max);
            AddColorAttribute(initialize, system.startColor.ToColor());

            Vector3 extents = string.Equals(system.positionMode, "Point", StringComparison.OrdinalIgnoreCase)
                ? Vector3.zero
                : Vector3.Max(Vector3.zero, system.spawnBoxExtents.ToVector3());
            AddVectorAttribute(initialize, VFXAttribute.Position, -extents, extents);

            (Vector3 minVelocity, Vector3 maxVelocity) = CalculateVelocityRange(system);
            AddVectorAttribute(initialize, VFXAttribute.Velocity, minVelocity, maxVelocity);
        }

        private static void AddUpdateBlocks(VFXBasicUpdate update, AbyssVFXSystemRecipe system, List<string> warnings)
        {
            Vector3 gravityValue = system.gravity.ToVector3();
            if (gravityValue.sqrMagnitude > 0.0000001f)
            {
                Block.Gravity gravity = ScriptableObject.CreateInstance<Block.Gravity>();
                AssignRequired(gravity, 0, gravityValue, "Gravity");
                update.AddChild(gravity);
            }

            if (system.drag > 0.0001f && !TryAddDragBlock(update, system.drag))
                warnings.Add($"{system.name}: VFX Graph 17 내부 Drag Block 타입을 찾지 못해 Drag를 생략했습니다.");
        }

        private static bool TryAddDragBlock(VFXBasicUpdate update, float dragValue)
        {
            // VFX Graph 내부 타입명은 릴리스별로 Drag/LinearDrag 사이에서 달라질 수 있어
            // 컴파일 타임 의존 대신 현재 Editor assembly에서 후보를 찾는다.
            string[] candidates =
            {
                "UnityEditor.VFX.Block.Drag",
                "UnityEditor.VFX.Block.LinearDrag"
            };

            foreach (string typeName in candidates)
            {
                Type type = typeof(VFXBasicUpdate).Assembly.GetType(typeName, false);
                if (type == null)
                    continue;

                ScriptableObject instance = ScriptableObject.CreateInstance(type);
                if (instance is not VFXModel model)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }

                if (!AbyssVFXInternalUtility.TryAssignInputSlotByIndex(model, 0, dragValue))
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }

                update.AddChild(model);
                return true;
            }

            return false;
        }

        private static void ConfigureOutput(VFXPlanarPrimitiveOutput output, AbyssVFXSystemRecipe system, List<string> warnings)
        {
            string requestedBlend = system.blendMode switch
            {
                "Premultiplied" => "AlphaPremultiplied",
                _ => system.blendMode
            };
            if (!AbyssVFXInternalUtility.TrySetEnumSetting(output, "blendMode", requestedBlend, out string blendWarning))
                warnings.Add($"{system.name}: {blendWarning} 기본 Blend Mode를 사용합니다.");

            if (!string.IsNullOrWhiteSpace(system.textureAssetPath))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(system.textureAssetPath);
                if (texture == null)
                {
                    warnings.Add($"{system.name}: Texture를 찾지 못했습니다: {system.textureAssetPath}");
                }
                else if (!AbyssVFXInternalUtility.TryAssignInputSlotByName(output, "mainTexture", texture))
                {
                    warnings.Add($"{system.name}: Output의 mainTexture 슬롯 자동 연결에 실패했습니다. 그래프에서 수동 지정하세요.");
                }
            }
        }

        private static void AddFloatAttribute(VFXBasicInitialize initialize, VFXAttribute attribute, float min, float max)
        {
            Block.SetAttribute block = ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute.name);
            bool random = !Mathf.Approximately(min, max);
            if (random)
                block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, $"{attribute.name} A");
            if (random)
                AssignRequired(block, 1, max, $"{attribute.name} B");
            initialize.AddChild(block);
        }

        private static void AddVectorAttribute(VFXBasicInitialize initialize, VFXAttribute attribute, Vector3 min, Vector3 max)
        {
            Block.SetAttribute block = ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute.name);
            bool random = min != max;
            if (random)
                block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, $"{attribute.name} A");
            if (random)
                AssignRequired(block, 1, max, $"{attribute.name} B");
            initialize.AddChild(block);
        }

        private static void AddColorAttribute(VFXBasicInitialize initialize, Color color)
        {
            // VFX Graph의 particle color 표준 Attribute는 RGB float3이며 Alpha는 별도 float Attribute다.
            Block.SetAttribute colorBlock = ScriptableObject.CreateInstance<Block.SetAttribute>();
            colorBlock.SetSettingValue("attribute", VFXAttribute.Color.name);
            AssignRequired(colorBlock, 0, new Vector3(color.r, color.g, color.b), "color RGB");
            initialize.AddChild(colorBlock);

            Block.SetAttribute alphaBlock = ScriptableObject.CreateInstance<Block.SetAttribute>();
            alphaBlock.SetSettingValue("attribute", VFXAttribute.Alpha.name);
            AssignRequired(alphaBlock, 0, color.a, "alpha");
            initialize.AddChild(alphaBlock);
        }

        private static void AssignRequired(VFXModel model, int slotIndex, object value, string purpose)
        {
            if (AbyssVFXInternalUtility.TryAssignInputSlotByIndex(model, slotIndex, value))
                return;

            string slot = AbyssVFXInternalUtility.DescribeInputSlot(model, slotIndex);
            throw new InvalidOperationException(
                $"{purpose} 값을 VFX Slot에 기록하지 못했습니다. " +
                $"Model={model.GetType().FullName}, Slot={slot}, Input={value?.GetType().FullName ?? "null"}");
        }

        private static (Vector3 min, Vector3 max) CalculateVelocityRange(AbyssVFXSystemRecipe system)
        {
            if (string.Equals(system.velocityMode, "None", StringComparison.OrdinalIgnoreCase))
                return (Vector3.zero, Vector3.zero);

            float minSpeed = system.speed.min;
            float maxSpeed = system.speed.max;
            if (string.Equals(system.velocityMode, "Radial", StringComparison.OrdinalIgnoreCase))
            {
                Vector3 max = Vector3.one * maxSpeed;
                return (-max, max);
            }

            Vector3 direction = system.direction.ToVector3();
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector3.up;
            direction.Normalize();
            Vector3 spread = Vector3.one * (system.spread * maxSpeed);
            Vector3 a = direction * minSpeed - spread;
            Vector3 b = direction * maxSpeed + spread;
            return (Vector3.Min(a, b), Vector3.Max(a, b));
        }

        private static string CreatePrefab(VisualEffectAsset asset, string folder, string baseName)
        {
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.prefab");
            GameObject gameObject = new(baseName);
            try
            {
                VisualEffect visualEffect = gameObject.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = asset;
                PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static string NormalizeFolder(string folder)
        {
            folder = (folder ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(folder))
                folder = "Assets/VFX/Generated/AI";
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                folder = "Assets/" + folder.TrimStart('/');
            AbyssVFXAssetFactory.EnsureAssetFolder(folder);
            return folder;
        }
    }
}
#endif
