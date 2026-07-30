#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.Rendering;
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
        public bool clonedTemplate;
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
            IReadOnlyList<string> errors =
                AbyssVFXRecipeUtility.Validate(
                    recipe,
                    out IReadOnlyList<string> validationWarnings);

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));

            string folder = NormalizeFolder(outputFolder);
            string assetPath =
                $"{folder}/{AbyssVFXRecipeUtility.SanitizeAssetName(recipe.name)}.vfx";

            bool cloneTemplate = string.Equals(
                recipe.buildMode,
                "CloneTemplate",
                StringComparison.OrdinalIgnoreCase);

            VisualEffectAsset templateAsset = cloneTemplate
                ? AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                    recipe.templateAssetPath)
                : null;

            if (cloneTemplate && templateAsset == null)
            {
                throw new InvalidOperationException(
                    $"Template VFX Asset을 찾지 못했습니다: {recipe.templateAssetPath}");
            }

            if (cloneTemplate && string.Equals(
                    AssetDatabase.GetAssetPath(templateAsset),
                    assetPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Template 원본과 출력 .vfx 경로가 같습니다. " +
                    "원본 보호를 위해 다른 Effect Name 또는 Output Folder를 사용하세요.");
            }

            VisualEffectAsset effectiveSeed = templateAsset ?? seedAsset;

            // Template을 다시 적용할 때 기존 생성 Graph가 남지 않도록 대상만 지웁니다.
            if (cloneTemplate && overwrite &&
                AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath) != null &&
                !string.Equals(
                    AssetDatabase.GetAssetPath(templateAsset),
                    assetPath,
                    StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            VisualEffectAsset asset = AbyssVFXAssetFactory.CreateOrLoad(
                assetPath,
                effectiveSeed,
                overwrite,
                out assetPath,
                out string seedPath);

            var resource = asset.GetResource();
            if (resource == null)
                throw new InvalidOperationException("VisualEffectAsset.GetResource()가 null입니다.");

            VFXGraph graph = resource.GetOrCreateGraph();
            if (graph == null)
                throw new InvalidOperationException("VFX Graph 내부 모델을 생성하지 못했습니다.");

            AbyssVFXBuildResult result = new()
            {
                asset = asset,
                assetPath = assetPath,
                seedPath = seedPath,
                systemCount = cloneTemplate ? 0 : recipe.systems.Count,
                clonedTemplate = cloneTemplate
            };
            result.warnings.AddRange(validationWarnings);

            if (!cloneTemplate || !recipe.preserveTemplateGraph)
            {
                Undo.RegisterCompleteObjectUndo(
                    resource,
                    "Build Project Abyss AI VFX Graph");

                graph.RemoveAllChildren();

                for (int i = 0; i < recipe.systems.Count; i++)
                    BuildSystem(graph, recipe.systems[i], i, result.warnings);

                EditorUtility.SetDirty(graph);
                EditorUtility.SetDirty(resource);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(resource),
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }
            else
            {
                result.warnings.Add(
                    "Template Clone 모드: 원본 Graph의 Context, Block, Operator, Strip, GPU Event, Decal, SDF 및 연결을 그대로 보존했습니다.");
            }

            ApplyGeneratedLabels(asset);

            if (createPrefab)
            {
                result.prefabPath = CreatePrefab(
                    asset,
                    folder,
                    Path.GetFileNameWithoutExtension(assetPath),
                    recipe,
                    result.warnings);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void BuildSystem(
            VFXGraph graph,
            AbyssVFXSystemRecipe system,
            int systemIndex,
            List<string> warnings)
        {
            float x = systemIndex * SystemGapX;
            string labelPrefix = $"{systemIndex + 1:00}_{system.name}";

            VFXBasicSpawner spawner =
                ScriptableObject.CreateInstance<VFXBasicSpawner>();
            spawner.label = labelPrefix + "_Spawn";
            spawner.position = new Vector2(x, 0f);
            AddSpawnerBlock(spawner, system);

            VFXBasicInitialize initialize =
                ScriptableObject.CreateInstance<VFXBasicInitialize>();
            initialize.label = labelPrefix + "_Initialize";
            initialize.position = new Vector2(x, ContextGapY);
            initialize.SetSettingValue("capacity", system.capacity);
            AddInitializeBlocks(initialize, system);

            VFXBasicUpdate update =
                ScriptableObject.CreateInstance<VFXBasicUpdate>();
            update.label = labelPrefix + "_Update";
            update.position = new Vector2(x, ContextGapY * 2f);
            AddUpdateBlocks(update, system, warnings);

            VFXContext output = CreateOutputContext(system, warnings);
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

        private static VFXContext CreateOutputContext(
            AbyssVFXSystemRecipe system,
            List<string> warnings)
        {
            if (string.Equals(
                    system.outputMode,
                    "Mesh",
                    StringComparison.OrdinalIgnoreCase))
            {
                string[] candidates =
                {
                    "UnityEditor.VFX.VFXMeshOutput",
                    "UnityEditor.VFX.VFXStaticMeshOutput",
                    "UnityEditor.VFX.VFXURPLitMeshOutput"
                };

                foreach (string typeName in candidates)
                {
                    Type type = typeof(VFXBasicUpdate).Assembly.GetType(
                        typeName,
                        false);

                    if (type == null ||
                        !typeof(VFXContext).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    try
                    {
                        if (ScriptableObject.CreateInstance(type)
                            is VFXContext context)
                        {
                            return context;
                        }
                    }
                    catch (Exception exception)
                    {
                        warnings.Add(
                            $"{system.name}: {typeName} 생성 실패: {exception.Message}");
                    }
                }

                warnings.Add(
                    $"{system.name}: VFX Graph 17 Mesh Output 내부 타입을 찾지 못해 Quad Output으로 대체했습니다. " +
                    "정확한 Mesh/Shader Graph 기반 그래프는 CloneTemplate 모드를 사용하세요.");
            }

            return ScriptableObject.CreateInstance<VFXPlanarPrimitiveOutput>();
        }

        private static void AddSpawnerBlock(
            VFXBasicSpawner spawner,
            AbyssVFXSystemRecipe system)
        {
            if (string.Equals(
                    system.spawnMode,
                    "Rate",
                    StringComparison.OrdinalIgnoreCase))
            {
                VFXSpawnerConstantRate rate =
                    ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
                AssignRequired(rate, 0, system.spawnRate, "Spawn Rate");
                spawner.AddChild(rate);
                return;
            }

            VFXSpawnerBurst burst =
                ScriptableObject.CreateInstance<VFXSpawnerBurst>();
            AssignRequired(burst, 0, system.spawnCount, "Burst Count");
            spawner.AddChild(burst);
        }

        private static void AddInitializeBlocks(
            VFXBasicInitialize initialize,
            AbyssVFXSystemRecipe system)
        {
            AddFloatAttribute(
                initialize,
                VFXAttribute.Lifetime,
                system.lifetime.min,
                system.lifetime.max);
            AddFloatAttribute(
                initialize,
                VFXAttribute.Size,
                system.size.min,
                system.size.max);
            AddColorAttribute(initialize, system.startColor.ToColor());

            Vector3 extents = string.Equals(
                    system.positionMode,
                    "Point",
                    StringComparison.OrdinalIgnoreCase)
                ? Vector3.zero
                : Vector3.Max(
                    Vector3.zero,
                    system.spawnBoxExtents.ToVector3());

            AddVectorAttribute(
                initialize,
                VFXAttribute.Position,
                -extents,
                extents);

            (Vector3 minVelocity, Vector3 maxVelocity) =
                CalculateVelocityRange(system);
            AddVectorAttribute(
                initialize,
                VFXAttribute.Velocity,
                minVelocity,
                maxVelocity);
        }

        private static void AddUpdateBlocks(
            VFXBasicUpdate update,
            AbyssVFXSystemRecipe system,
            List<string> warnings)
        {
            Vector3 gravityValue = system.gravity.ToVector3();
            if (gravityValue.sqrMagnitude > 0.0000001f)
            {
                Block.Gravity gravity =
                    ScriptableObject.CreateInstance<Block.Gravity>();
                AssignRequired(gravity, 0, gravityValue, "Gravity");
                update.AddChild(gravity);
            }

            if (system.drag > 0.0001f &&
                !TryAddDragBlock(update, system.drag))
            {
                warnings.Add(
                    $"{system.name}: VFX Graph 17 내부 Drag Block 타입을 찾지 못해 Drag를 생략했습니다.");
            }
        }

        private static bool TryAddDragBlock(
            VFXBasicUpdate update,
            float dragValue)
        {
            string[] candidates =
            {
                "UnityEditor.VFX.Block.Drag",
                "UnityEditor.VFX.Block.LinearDrag"
            };

            foreach (string typeName in candidates)
            {
                Type type = typeof(VFXBasicUpdate).Assembly.GetType(
                    typeName,
                    false);
                if (type == null)
                    continue;

                ScriptableObject instance =
                    ScriptableObject.CreateInstance(type);
                if (instance is not VFXModel model)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }

                if (!AbyssVFXInternalUtility.TryAssignInputSlotByIndex(
                        model,
                        0,
                        dragValue))
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }

                update.AddChild(model);
                return true;
            }

            return false;
        }

        private static void ConfigureOutput(
            VFXContext output,
            AbyssVFXSystemRecipe system,
            List<string> warnings)
        {
            string requestedBlend = system.blendMode switch
            {
                "Premultiplied" => "AlphaPremultiplied",
                _ => system.blendMode
            };

            if (!AbyssVFXInternalUtility.TrySetEnumSetting(
                    output,
                    "blendMode",
                    requestedBlend,
                    out string blendWarning))
            {
                warnings.Add(
                    $"{system.name}: {blendWarning} 기본 Blend Mode를 사용합니다.");
            }

            if (!string.IsNullOrWhiteSpace(system.textureAssetPath))
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                    system.textureAssetPath);

                if (texture == null)
                {
                    warnings.Add(
                        $"{system.name}: Texture를 찾지 못했습니다: {system.textureAssetPath}");
                }
                else if (!TryAssignAnyInput(
                             output,
                             texture,
                             "mainTexture",
                             "baseColorMap",
                             "texture"))
                {
                    warnings.Add(
                        $"{system.name}: Output Texture 슬롯 자동 연결에 실패했습니다. Graph 또는 VFX Shader Graph에서 지정하세요.");
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    system.shaderGraphAssetPath))
            {
                UnityEngine.Object[] shaderGraphAssets =
                    AssetDatabase.LoadAllAssetsAtPath(
                        system.shaderGraphAssetPath);

                if (shaderGraphAssets == null ||
                    shaderGraphAssets.Length == 0)
                {
                    warnings.Add(
                        $"{system.name}: Shader Graph를 찾지 못했습니다: " +
                        system.shaderGraphAssetPath);
                }
                else
                {
                    bool assigned = false;
                    string lastWarning = string.Empty;

                    foreach (UnityEngine.Object shaderGraph in
                             shaderGraphAssets.Where(asset => asset != null))
                    {
                        foreach (string settingName in new[]
                                 {
                                     "shaderGraph",
                                     "shaderGraphAsset",
                                     "m_ShaderGraph"
                                 })
                        {
                            if (AbyssVFXInternalUtility.TrySetObjectSetting(
                                    output,
                                    settingName,
                                    shaderGraph,
                                    out lastWarning))
                            {
                                assigned = true;
                                break;
                            }
                        }

                        if (assigned)
                            break;
                    }

                    if (!assigned)
                    {
                        warnings.Add(
                            $"{system.name}: VFX Shader Graph 자동 연결에 실패했습니다. " +
                            $"{lastWarning} CloneTemplate 또는 Graph Inspector에서 직접 지정하세요.");
                    }
                }
            }

            if (string.Equals(
                    system.outputMode,
                    "Mesh",
                    StringComparison.OrdinalIgnoreCase))
            {
                Mesh mesh = LoadMesh(
                    system.meshAssetPath,
                    system.meshSubAssetName);

                if (mesh == null)
                {
                    warnings.Add(
                        $"{system.name}: Mesh를 찾지 못했습니다: {system.meshAssetPath} / {system.meshSubAssetName}");
                }
                else if (!TryAssignAnyInput(
                             output,
                             mesh,
                             "mesh",
                             "Mesh"))
                {
                    warnings.Add(
                        $"{system.name}: Mesh Output 슬롯 자동 연결에 실패했습니다. CloneTemplate 또는 Graph에서 직접 지정하세요.");
                }

                if (!string.IsNullOrWhiteSpace(system.materialAssetPath))
                {
                    warnings.Add(
                        $"{system.name}: VFX Mesh Output은 일반 MeshRenderer Material을 직접 사용하지 않습니다. " +
                        "VFX 호환 Shader Graph와 Texture 속성을 사용하거나 sceneObjects에 Material을 지정하세요.");
                }
            }
        }

        private static bool TryAssignAnyInput(
            VFXModel model,
            UnityEngine.Object value,
            params string[] names)
        {
            foreach (string name in names)
            {
                if (AbyssVFXInternalUtility.TryAssignInputSlotByName(
                        model,
                        name,
                        value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFloatAttribute(
            VFXBasicInitialize initialize,
            VFXAttribute attribute,
            float min,
            float max)
        {
            Block.SetAttribute block =
                ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute.name);
            bool random = !Mathf.Approximately(min, max);
            if (random)
                block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, $"{attribute.name} A");
            if (random)
                AssignRequired(block, 1, max, $"{attribute.name} B");
            initialize.AddChild(block);
        }

        private static void AddVectorAttribute(
            VFXBasicInitialize initialize,
            VFXAttribute attribute,
            Vector3 min,
            Vector3 max)
        {
            Block.SetAttribute block =
                ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute.name);
            bool random = min != max;
            if (random)
                block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, $"{attribute.name} A");
            if (random)
                AssignRequired(block, 1, max, $"{attribute.name} B");
            initialize.AddChild(block);
        }

        private static void AddColorAttribute(
            VFXBasicInitialize initialize,
            Color color)
        {
            Block.SetAttribute colorBlock =
                ScriptableObject.CreateInstance<Block.SetAttribute>();
            colorBlock.SetSettingValue("attribute", VFXAttribute.Color.name);
            AssignRequired(
                colorBlock,
                0,
                new Vector3(color.r, color.g, color.b),
                "color RGB");
            initialize.AddChild(colorBlock);

            Block.SetAttribute alphaBlock =
                ScriptableObject.CreateInstance<Block.SetAttribute>();
            alphaBlock.SetSettingValue("attribute", VFXAttribute.Alpha.name);
            AssignRequired(alphaBlock, 0, color.a, "alpha");
            initialize.AddChild(alphaBlock);
        }

        private static void AssignRequired(
            VFXModel model,
            int slotIndex,
            object value,
            string purpose)
        {
            if (AbyssVFXInternalUtility.TryAssignInputSlotByIndex(
                    model,
                    slotIndex,
                    value))
            {
                return;
            }

            string slot =
                AbyssVFXInternalUtility.DescribeInputSlot(
                    model,
                    slotIndex);

            throw new InvalidOperationException(
                $"{purpose} 값을 VFX Slot에 기록하지 못했습니다. " +
                $"Model={model.GetType().FullName}, Slot={slot}, " +
                $"Input={value?.GetType().FullName ?? "null"}");
        }

        private static (Vector3 min, Vector3 max) CalculateVelocityRange(
            AbyssVFXSystemRecipe system)
        {
            if (string.Equals(
                    system.velocityMode,
                    "None",
                    StringComparison.OrdinalIgnoreCase))
            {
                return (Vector3.zero, Vector3.zero);
            }

            float minSpeed = system.speed.min;
            float maxSpeed = system.speed.max;
            if (string.Equals(
                    system.velocityMode,
                    "Radial",
                    StringComparison.OrdinalIgnoreCase))
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

        private static string CreatePrefab(
            VisualEffectAsset asset,
            string folder,
            string baseName,
            AbyssVFXRecipe recipe,
            List<string> warnings)
        {
            string prefabPath = $"{folder}/{baseName}.prefab";
            GameObject root = new(baseName);

            try
            {
                VisualEffect visualEffect = root.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = asset;
                ApplyExposedOverrides(
                    visualEffect,
                    recipe.exposedOverrides,
                    warnings);

                if (recipe.sceneObjects != null)
                {
                    foreach (AbyssVFXSceneObjectRecipe objectRecipe
                             in recipe.sceneObjects.Where(x => x != null))
                    {
                        CreateSceneObject(root.transform, objectRecipe, warnings);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                ApplyGeneratedLabels(prefab);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateSceneObject(
            UnityEngine.Transform parent,
            AbyssVFXSceneObjectRecipe recipe,
            List<string> warnings)
        {
            Mesh mesh = LoadMesh(
                recipe.meshAssetPath,
                recipe.meshSubAssetName);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    recipe.materialAssetPath);

            if (mesh == null)
            {
                warnings.Add(
                    $"Scene Object '{recipe.name}': Mesh를 찾지 못했습니다: " +
                    $"{recipe.meshAssetPath} / {recipe.meshSubAssetName}");
                return;
            }

            GameObject child = new(
                string.IsNullOrWhiteSpace(recipe.name)
                    ? mesh.name
                    : recipe.name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = recipe.localPosition.ToVector3();
            child.transform.localEulerAngles =
                recipe.localEulerAngles.ToVector3();
            child.transform.localScale = recipe.localScale.ToVector3();

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = recipe.castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = recipe.receiveShadows;

            if (material == null &&
                !string.IsNullOrWhiteSpace(recipe.materialAssetPath))
            {
                warnings.Add(
                    $"Scene Object '{recipe.name}': Material을 찾지 못했습니다: {recipe.materialAssetPath}");
            }
        }

        private static void ApplyExposedOverrides(
            VisualEffect visualEffect,
            IReadOnlyList<AbyssVFXExposedOverrideRecipe> overrides,
            List<string> warnings)
        {
            if (visualEffect == null || overrides == null)
                return;

            foreach (AbyssVFXExposedOverrideRecipe entry in overrides)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.propertyName))
                {
                    continue;
                }

                try
                {
                    switch (entry.valueType)
                    {
                        case "Int":
                            visualEffect.SetInt(entry.propertyName, entry.intValue);
                            break;
                        case "Bool":
                            visualEffect.SetBool(entry.propertyName, entry.boolValue);
                            break;
                        case "Vector3":
                            visualEffect.SetVector3(
                                entry.propertyName,
                                entry.vectorValue.ToVector3());
                            break;
                        case "Vector4":
                        case "Color":
                            Color color = entry.colorValue.ToColor();
                            visualEffect.SetVector4(
                                entry.propertyName,
                                new Vector4(color.r, color.g, color.b, color.a));
                            break;
                        case "Texture":
                            visualEffect.SetTexture(
                                entry.propertyName,
                                AssetDatabase.LoadAssetAtPath<Texture>(entry.assetPath));
                            break;
                        case "Mesh":
                            visualEffect.SetMesh(
                                entry.propertyName,
                                LoadMesh(entry.assetPath, entry.subAssetName));
                            break;
                        default:
                            visualEffect.SetFloat(
                                entry.propertyName,
                                entry.floatValue);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    warnings.Add(
                        $"Exposed Property '{entry.propertyName}' 적용 실패: {exception.Message}");
                }
            }
        }

        private static Mesh LoadMesh(
            string assetPath,
            string subAssetName)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            Mesh main = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (main != null &&
                (string.IsNullOrWhiteSpace(subAssetName) ||
                 string.Equals(main.name, subAssetName, StringComparison.Ordinal)))
            {
                return main;
            }

            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Mesh>()
                .FirstOrDefault(mesh =>
                    string.IsNullOrWhiteSpace(subAssetName) ||
                    string.Equals(
                        mesh.name,
                        subAssetName,
                        StringComparison.Ordinal));
        }

        private static void ApplyGeneratedLabels(
            UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            List<string> labels = new(AssetDatabase.GetLabels(asset));

            if (!labels.Contains("ProjectAbyss"))
                labels.Add("ProjectAbyss");

            if (!labels.Contains("AbyssGeneratedVFX"))
                labels.Add("AbyssGeneratedVFX");

            AssetDatabase.SetLabels(asset, labels.ToArray());
        }

        private static string NormalizeFolder(string folder)
        {
            folder = (folder ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimEnd('/');
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
