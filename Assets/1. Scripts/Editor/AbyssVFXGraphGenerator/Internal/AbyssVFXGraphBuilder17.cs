#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public readonly List<string> diagnostics = new();
        public int systemCount;
    }

    internal static class AbyssVFXGraphBuilder17
    {
        private const float ContextGapY = 360f;
        private const float SystemGapX = 760f;

        internal static AbyssVFXBuildResult Build(AbyssVFXRecipe recipe, string outputFolder, VisualEffectAsset seedAsset, bool overwrite, bool createPrefab)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            AbyssVFXRecipeUtility.MigrateAndNormalize(recipe);
            IReadOnlyList<string> errors = AbyssVFXRecipeUtility.Validate(recipe, out IReadOnlyList<string> validationWarnings);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

            string folder = NormalizeFolder(outputFolder);
            string requestedPath = $"{folder}/{AbyssVFXRecipeUtility.SanitizeAssetName(recipe.name)}.vfx";
            string existingAbsolute = ToAbsolutePath(requestedPath);
            byte[] backup = overwrite && File.Exists(existingAbsolute) ? File.ReadAllBytes(existingAbsolute) : null;

            VisualEffectAsset asset = null;
            string actualPath = requestedPath;
            AbyssVFXMeshAssetTransaction meshTransaction = new();
            try
            {
                asset = AbyssVFXAssetFactory.CreateOrLoad(requestedPath, seedAsset, overwrite, out actualPath, out string seedPath);
                var resource = asset.GetResource() ?? throw new InvalidOperationException("VisualEffectAsset.GetResource()가 null입니다.");
                VFXGraph graph = resource.GetOrCreateGraph() ?? throw new InvalidOperationException("VFXGraph를 생성하지 못했습니다.");
                AbyssVFXBuildResult result = new()
                {
                    asset = asset,
                    assetPath = actualPath,
                    seedPath = seedPath,
                    systemCount = recipe.systems.Count
                };
                result.warnings.AddRange(validationWarnings);

                Undo.RegisterCompleteObjectUndo(resource, "Build Project Abyss NodeGraph3D VFX");
                AbyssVFXMeshAssetFactory.Prepare(recipe, folder, overwrite, result.diagnostics, meshTransaction);
                graph.RemoveAllChildren();
                for (int i = 0; i < recipe.systems.Count; i++) BuildSystem(graph, recipe.systems[i], i, result);

                EditorUtility.SetDirty(graph);
                EditorUtility.SetDirty(resource);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(actualPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                bool expressionsRequested = recipe.systems.Any(HasExpressions);
                AbyssVFXGraphAudit.ValidateBuiltGraph(graph, recipe.systems.Count, expressionsRequested, result.diagnostics);
                ApplyGeneratedLabels(asset);
                if (createPrefab) result.prefabPath = CreatePrefab(asset, folder, Path.GetFileNameWithoutExtension(actualPath), recipe, result.diagnostics);
                AssetDatabase.SaveAssets();
                meshTransaction.Commit();
                return result;
            }
            catch
            {
                meshTransaction.Rollback();
                if (backup != null)
                {
                    File.WriteAllBytes(existingAbsolute, backup);
                    AssetDatabase.ImportAsset(requestedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                else if (!string.IsNullOrWhiteSpace(actualPath) && AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(actualPath) != null)
                {
                    AssetDatabase.DeleteAsset(actualPath);
                }
                throw;
            }
        }

        private static void BuildSystem(VFXGraph graph, AbyssVFXSystemRecipe system, int systemIndex, AbyssVFXBuildResult result)
        {
            float x = systemIndex * SystemGapX;
            string prefix = $"{systemIndex + 1:00}_{system.name}";
            VFXBasicSpawner spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            spawner.label = prefix + "_Spawn";
            spawner.position = new Vector2(x, 0f);
            AddSpawnerBlock(spawner, system);

            VFXBasicInitialize initialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            initialize.label = prefix + "_Initialize";
            initialize.position = new Vector2(x, ContextGapY);
            initialize.SetSettingValue("capacity", system.capacity);

            VFXBasicUpdate update = ScriptableObject.CreateInstance<VFXBasicUpdate>();
            update.label = prefix + "_Update";
            update.position = new Vector2(x, ContextGapY * 2f);

            VFXContext output = CreateMeshOutput(system);
            output.label = prefix + "_MeshOutput";
            output.position = new Vector2(x, ContextGapY * 3f);

            spawner.LinkTo(initialize);
            initialize.LinkTo(update);
            update.LinkTo(output);
            graph.AddChild(spawner);
            graph.AddChild(initialize);
            graph.AddChild(update);
            graph.AddChild(output);

            AbyssVFXExpressionCompiler initCompiler = new(graph, x, ContextGapY, result.diagnostics);
            AbyssVFXExpressionCompiler updateCompiler = new(graph, x, ContextGapY * 2f, result.diagnostics);
            AddInitializeBlocks(initialize, system, initCompiler);
            AddUpdateBlocks(update, system, updateCompiler);
            ConfigureOutput(output, system, result);
            AddCustomBlocks(initialize, update, output, system, graph, x, result);
        }

        private static void AddSpawnerBlock(VFXBasicSpawner spawner, AbyssVFXSystemRecipe system)
        {
            if (system.spawnMode == "Rate")
            {
                VFXSpawnerConstantRate rate = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
                AssignRequired(rate, 0, system.spawnRate, "Spawn Rate");
                spawner.AddChild(rate);
            }
            else
            {
                VFXSpawnerBurst burst = ScriptableObject.CreateInstance<VFXSpawnerBurst>();
                AssignRequired(burst, 0, system.spawnCount, "Burst Count");
                spawner.AddChild(burst);
            }
        }

        private static void AddInitializeBlocks(VFXBasicInitialize initialize, AbyssVFXSystemRecipe system, AbyssVFXExpressionCompiler compiler)
        {
            Block.SetAttribute lifetime = AddFloatAttribute(
                initialize,
                VFXAttribute.Lifetime.name,
                system.lifetime.min,
                system.lifetimeExpression == null ? system.lifetime.max : system.lifetime.min);
            compiler.BindRequired(system.lifetimeExpression, lifetime, null, 0, system.name + ".lifetime");

            Block.SetAttribute size = AddFloatAttribute(
                initialize,
                VFXAttribute.Size.name,
                system.size.min,
                system.sizeExpression == null ? system.size.max : system.size.min);
            compiler.BindRequired(system.sizeExpression, size, null, 0, system.name + ".size");

            Block.SetAttribute color = AddVectorAttribute(initialize, VFXAttribute.Color.name, new Vector3(system.startColor.r, system.startColor.g, system.startColor.b), new Vector3(system.startColor.r, system.startColor.g, system.startColor.b));
            compiler.BindRequired(system.colorExpression, color, null, 0, system.name + ".color");

            Block.SetAttribute alpha = AddFloatAttribute(initialize, VFXAttribute.Alpha.name, system.startColor.a, system.startColor.a);
            compiler.BindRequired(system.alphaExpression, alpha, null, 0, system.name + ".alpha");

            Vector3 extents = system.positionMode == "Point" ? Vector3.zero : Vector3.Max(Vector3.zero, system.spawnBoxExtents.ToVector3());
            Block.SetAttribute position = AddVectorAttribute(
                initialize,
                VFXAttribute.Position.name,
                system.positionExpression == null ? -extents : Vector3.zero,
                system.positionExpression == null ? extents : Vector3.zero);
            compiler.BindRequired(system.positionExpression, position, null, 0, system.name + ".position");

            Block.SetAttribute velocity = AddVectorAttribute(initialize, VFXAttribute.Velocity.name, Vector3.zero, Vector3.zero);
            AbyssVFXExpressionRecipe velocityExpression = system.velocityExpression ?? BuildDefaultVelocityExpression(system);
            compiler.BindRequired(velocityExpression, velocity, null, 0, system.name + ".velocity");

            if (system.angleExpression != null)
            {
                Block.SetAttribute angle = AddFloatAttribute(initialize, "angleX", 0f, 0f);
                compiler.BindRequired(system.angleExpression, angle, null, 0, system.name + ".angleX");
            }
        }

        private static void AddUpdateBlocks(VFXBasicUpdate update, AbyssVFXSystemRecipe system, AbyssVFXExpressionCompiler compiler)
        {
            Vector3 gravityValue = system.gravity.ToVector3();
            if (gravityValue.sqrMagnitude > 0.0000001f)
            {
                Block.Gravity gravity = ScriptableObject.CreateInstance<Block.Gravity>();
                AssignRequired(gravity, 0, gravityValue, system.name + " Gravity");
                update.AddChild(gravity);
            }

            if (system.drag > 0.0001f)
            {
                Block.Drag drag = ScriptableObject.CreateInstance<Block.Drag>();
                AssignRequired(drag, 0, system.drag, system.name + " Drag");
                update.AddChild(drag);
            }

            if (system.turbulenceEnabled) AddRequiredTurbulence(update, system);
            if (system.updateVelocityExpression != null)
            {
                Block.SetAttribute velocity = AddVectorAttribute(update, VFXAttribute.Velocity.name, Vector3.zero, Vector3.zero);
                compiler.BindRequired(system.updateVelocityExpression, velocity, null, 0, system.name + ".updateVelocity");
            }
            if (system.updatePositionExpression != null)
            {
                Block.SetAttribute position = AddVectorAttribute(update, VFXAttribute.Position.name, Vector3.zero, Vector3.zero);
                compiler.BindRequired(system.updatePositionExpression, position, null, 0, system.name + ".updatePosition");
            }
        }

        private static VFXContext CreateMeshOutput(AbyssVFXSystemRecipe system)
        {
            Type[] candidates = AbyssVFXInternalUtility.GetVFXEditorTypes()
                .Where(t => !t.IsAbstract && typeof(VFXContext).IsAssignableFrom(t) && t.Name.Contains("Mesh", StringComparison.OrdinalIgnoreCase) && t.Name.Contains("Output", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.Name.Contains("URP", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(t => t.Name.Contains("Particle", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            List<string> failures = new();
            foreach (Type type in candidates)
            {
                try
                {
                    if (ScriptableObject.CreateInstance(type) is VFXContext context)
                        return context;
                }
                catch (Exception ex)
                {
                    failures.Add(type.FullName + ": " + ex.Message);
                }
            }
            throw new InvalidOperationException(system.name + ": 호환되는 Mesh Output Context를 찾지 못했습니다.\n" + string.Join("\n", failures));
        }

        private static void ConfigureOutput(VFXContext output, AbyssVFXSystemRecipe system, AbyssVFXBuildResult result)
        {
            string blend = system.blendMode == "Premultiplied" ? "AlphaPremultiplied" : system.blendMode;
            if (!AbyssVFXInternalUtility.TrySetEnumSetting(output, "blendMode", blend, out string blendWarning)) result.warnings.Add(system.name + ": " + blendWarning);

            Mesh mesh = LoadMesh(system.meshAssetPath, system.meshSubAssetName) ?? throw new InvalidOperationException(system.name + ": Mesh를 로드하지 못했습니다: " + system.meshAssetPath);
            bool meshAssigned = AbyssVFXInternalUtility.TryAssignInputSlotByName(output, "mesh", mesh) || AbyssVFXInternalUtility.TryAssignInputSlotByName(output, "Mesh", mesh) || AbyssVFXInternalUtility.TrySetObjectSetting(output, "mesh", mesh, out _) || AbyssVFXInternalUtility.TrySetObjectSetting(output, "m_Mesh", mesh, out _);
            if (!meshAssigned) throw new InvalidOperationException(system.name + ": Mesh Output에 Mesh를 연결하지 못했습니다.");

            if (!string.IsNullOrWhiteSpace(system.shaderGraphAssetPath))
            {
                ValidateShaderGraphIsTextureFree(system.shaderGraphAssetPath, system.name);
                UnityEngine.Object shaderGraph = AssetDatabase.LoadAllAssetsAtPath(system.shaderGraphAssetPath).FirstOrDefault(x => x != null)
                    ?? throw new InvalidOperationException(system.name + ": Shader Graph를 찾지 못했습니다: " + system.shaderGraphAssetPath);
                bool assigned = false;
                foreach (string setting in new[] { "shaderGraph", "shaderGraphAsset", "m_ShaderGraph" }) assigned |= AbyssVFXInternalUtility.TrySetObjectSetting(output, setting, shaderGraph, out _);
                if (!assigned) throw new InvalidOperationException(system.name + ": VFX Shader Graph를 Mesh Output에 연결하지 못했습니다.");
            }

            if (system.orientToVelocity || system.orientationMode == "OrientToVelocity") AddRequiredOrientation(output, system.name);
        }

        private static void AddRequiredOrientation(VFXContext output, string systemName)
        {
            IEnumerable<Type> candidates = AbyssVFXInternalUtility.GetVFXEditorTypes()
                .Where(t => !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) && (t.Namespace?.Contains(".Block", StringComparison.OrdinalIgnoreCase) ?? false) && t.Name.Contains("Orient", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.Name.Contains("Velocity", StringComparison.OrdinalIgnoreCase));
            List<string> failures = new();
            foreach (Type type in candidates)
            {
                VFXModel block = null;
                try
                {
                    block = ScriptableObject.CreateInstance(type) as VFXModel;
                    if (block == null) continue;
                    bool configured = type.Name.Contains("Velocity", StringComparison.OrdinalIgnoreCase);
                    foreach (string setting in new[] { "mode", "orientation", "orientationMode", "m_Mode" })
                        configured |= AbyssVFXInternalUtility.TrySetEnumSetting(block, setting, "AlongVelocity", out _);
                    if (!configured)
                    {
                        failures.Add(type.FullName + ": velocity orientation 설정 없음");
                        UnityEngine.Object.DestroyImmediate(block);
                        continue;
                    }
                    output.AddChild(block);
                    return;
                }
                catch (Exception ex)
                {
                    failures.Add(type.FullName + ": " + ex.Message);
                    if (block != null && block.GetParent() == null) UnityEngine.Object.DestroyImmediate(block);
                }
            }
            throw new InvalidOperationException(systemName + ": Orient To Velocity Block을 생성하지 못했습니다.\n" + string.Join("\n", failures));
        }

        private static void AddRequiredTurbulence(VFXBasicUpdate update, AbyssVFXSystemRecipe system)
        {
            IEnumerable<Type> candidates = AbyssVFXInternalUtility.GetVFXEditorTypes()
                .Where(t => !t.IsAbstract && typeof(VFXModel).IsAssignableFrom(t) && (t.Namespace?.Contains(".Block", StringComparison.OrdinalIgnoreCase) ?? false) && t.Name.Contains("Turbulence", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.Name.Contains("Curl", StringComparison.OrdinalIgnoreCase));
            List<string> failures = new();
            foreach (Type type in candidates)
            {
                VFXModel block = null;
                try
                {
                    block = ScriptableObject.CreateInstance(type) as VFXModel;
                    if (block == null) continue;
                    bool intensity = AssignAny(block, system.turbulenceIntensity, "Intensity", "Power", "Amplitude");
                    bool frequency = AssignAny(block, system.turbulenceFrequency, "Frequency", "Scale");
                    AssignAny(block, system.turbulenceDrag, "Drag");
                    AssignAny(block, system.turbulenceOctaves, "Octaves");
                    AssignAny(block, system.turbulenceRoughness, "Roughness");
                    AssignAny(block, system.turbulenceLacunarity, "Lacunarity");
                    if (!intensity || !frequency)
                    {
                        failures.Add(type.FullName + $": intensity={intensity}, frequency={frequency}");
                        UnityEngine.Object.DestroyImmediate(block);
                        continue;
                    }
                    update.AddChild(block);
                    return;
                }
                catch (Exception ex)
                {
                    failures.Add(type.FullName + ": " + ex.Message);
                    if (block != null && block.GetParent() == null) UnityEngine.Object.DestroyImmediate(block);
                }
            }
            throw new InvalidOperationException(system.name + ": Turbulence를 요청했지만 필수 슬롯을 가진 3D Turbulence Block을 만들지 못했습니다.\n" + string.Join("\n", failures));
        }

        private static void AddCustomBlocks(VFXBasicInitialize initialize, VFXBasicUpdate update, VFXContext output, AbyssVFXSystemRecipe system, VFXGraph graph, float x, AbyssVFXBuildResult result)
        {
            int index = 0;
            foreach (AbyssVFXCustomBlockRecipe recipe in system.customBlocks ?? new List<AbyssVFXCustomBlockRecipe>())
            {
                if (recipe == null) continue;
                VFXModel block = AbyssVFXInternalUtility.CreateModel(recipe.blockType, type => type.Namespace?.Contains(".Block", StringComparison.OrdinalIgnoreCase) == true, out string resolved);
                if (block == null)
                {
                    if (recipe.required) throw new InvalidOperationException(system.name + ": Custom Block을 찾지 못했습니다: " + recipe.blockType);
                    result.warnings.Add(system.name + ": optional custom block 생략: " + recipe.blockType);
                    continue;
                }
                // VFXModel.label은 공개되지 않고 name도 읽기 전용이다.
                // recipe.label은 Recipe/진단용 메타데이터로만 유지한다.
                foreach (AbyssVFXSettingRecipe setting in recipe.settings ?? new List<AbyssVFXSettingRecipe>())
                    if (!AbyssVFXInternalUtility.TrySetSetting(block, setting.name, AbyssVFXInternalUtility.ConvertSettingValue(setting), out string warning))
                        throw new InvalidOperationException(system.name + ": Custom Block setting 실패: " + warning);
                VFXContext context = recipe.context switch { "Initialize" => initialize, "Output" => output, _ => update };
                context.AddChild(block);
                AbyssVFXExpressionCompiler compiler = new(graph, x - 60f, context.position.y + index * 120f, result.diagnostics);
                foreach (AbyssVFXBlockInputRecipe input in recipe.inputs ?? new List<AbyssVFXBlockInputRecipe>())
                    if (input?.expression != null) compiler.BindRequired(input.expression, block, input.slotName, input.slotIndex, system.name + ".customBlock." + resolved);
                index++;
            }
        }

        private static Block.SetAttribute AddFloatAttribute(VFXContext context, string attribute, float min, float max)
        {
            Block.SetAttribute block = ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute);
            bool random = !Mathf.Approximately(min, max);
            if (random) block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, attribute + " A");
            if (random) AssignRequired(block, 1, max, attribute + " B");
            context.AddChild(block);
            return block;
        }

        private static Block.SetAttribute AddVectorAttribute(VFXContext context, string attribute, Vector3 min, Vector3 max)
        {
            Block.SetAttribute block = ScriptableObject.CreateInstance<Block.SetAttribute>();
            block.SetSettingValue("attribute", attribute);
            bool random = min != max;
            if (random) block.SetSettingValue("Random", Block.RandomMode.PerComponent);
            AssignRequired(block, 0, min, attribute + " A");
            if (random) AssignRequired(block, 1, max, attribute + " B");
            context.AddChild(block);
            return block;
        }

        private static AbyssVFXExpressionRecipe BuildDefaultVelocityExpression(AbyssVFXSystemRecipe system)
        {
            if (system.velocityMode == "None") return new AbyssVFXExpressionRecipe { kind = "LiteralVector3", valueType = "Vector3", vectorValue = new AbyssVector3(0f, 0f, 0f) };
            AbyssVFXExpressionRecipe speed = AbyssVFXRecipeUtility.RandomFloat(system.speed.min, system.speed.max);
            if (system.velocityMode == "Radial")
                return AbyssVFXRecipeUtility.Multiply(AbyssVFXRecipeUtility.Normalize(AbyssVFXRecipeUtility.RandomVector3(-Vector3.one, Vector3.one)), speed);
            Vector3 direction = system.direction.ToVector3().sqrMagnitude < 0.000001f ? Vector3.up : system.direction.ToVector3().normalized;
            AbyssVFXExpressionRecipe directionLiteral = new() { kind = "LiteralVector3", valueType = "Vector3", vectorValue = AbyssVector3.From(direction) };
            AbyssVFXExpressionRecipe jitter = AbyssVFXRecipeUtility.RandomVector3(Vector3.one * -system.spread, Vector3.one * system.spread);
            return AbyssVFXRecipeUtility.Multiply(AbyssVFXRecipeUtility.Normalize(AbyssVFXRecipeUtility.Add(directionLiteral, jitter)), speed);
        }

        private static bool AssignAny(VFXModel model, object value, params string[] names)
        {
            foreach (string name in names) if (AbyssVFXInternalUtility.TryAssignInputSlotByName(model, name, value)) return true;
            return false;
        }

        private static void AssignRequired(VFXModel model, int slotIndex, object value, string purpose)
        {
            if (!AbyssVFXInternalUtility.TryAssignInputSlotByIndex(model, slotIndex, value))
                throw new InvalidOperationException($"{purpose}: VFX Slot 기록 실패. Model={model.GetType().FullName}, Slot={AbyssVFXInternalUtility.DescribeInputSlot(model, slotIndex)}");
        }

        private static Mesh LoadMesh(string assetPath, string subAssetName)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return null;
            Mesh main = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (main != null && (string.IsNullOrWhiteSpace(subAssetName) || main.name == subAssetName)) return main;
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>().FirstOrDefault(mesh => string.IsNullOrWhiteSpace(subAssetName) || mesh.name == subAssetName);
        }

        private static void ValidateShaderGraphIsTextureFree(string shaderGraphPath, string systemName)
        {
            string normalized = (shaderGraphPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(systemName + ": shaderGraphAssetPath는 .shadergraph여야 합니다: " + shaderGraphPath);

            string[] textureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr", ".bmp", ".tif", ".tiff" };
            foreach (string dependency in AssetDatabase.GetDependencies(normalized, true))
            {
                if (string.Equals(dependency, normalized, StringComparison.OrdinalIgnoreCase)) continue;
                string extension = Path.GetExtension(dependency);
                if (textureExtensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)) ||
                    typeof(Texture).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(dependency)))
                {
                    throw new InvalidOperationException(
                        systemName + ": TextureFree 정책 위반. Shader Graph가 Texture 자산을 참조합니다: " + dependency);
                }
            }
        }

        private static string CreatePrefab(VisualEffectAsset asset, string folder, string baseName, AbyssVFXRecipe recipe, List<string> diagnostics)
        {
            string path = $"{folder}/{baseName}.prefab";
            GameObject root = new(baseName);
            try
            {
                VisualEffect visualEffect = root.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = asset;
                AbyssVFXSurfaceAuraAssetFactory.ConfigurePrefab(root, recipe.surfaceAura, folder, baseName, diagnostics);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ApplyGeneratedLabels(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ApplyGeneratedLabels(UnityEngine.Object asset)
        {
            if (asset == null) return;
            List<string> labels = new(AssetDatabase.GetLabels(asset));
            if (!labels.Contains("ProjectAbyss")) labels.Add("ProjectAbyss");
            if (!labels.Contains("AbyssGeneratedVFX")) labels.Add("AbyssGeneratedVFX");
            if (!labels.Contains("AbyssNodeGraph3D")) labels.Add("AbyssNodeGraph3D");
            AssetDatabase.SetLabels(asset, labels.ToArray());
        }

        private static bool HasExpressions(AbyssVFXSystemRecipe s) => s != null && (s.lifetimeExpression != null || s.sizeExpression != null || s.positionExpression != null || s.velocityExpression != null || s.colorExpression != null || s.alphaExpression != null || s.angleExpression != null || s.updateVelocityExpression != null || s.updatePositionExpression != null || (s.customBlocks?.Any(b => b?.inputs?.Any(i => i?.expression != null) == true) ?? false) || s.velocityMode != "None");

        private static string NormalizeFolder(string folder)
        {
            folder = (folder ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(folder)) folder = "Assets/VFX/Generated/AI";
            if (!folder.StartsWith("Assets", StringComparison.Ordinal)) folder = "Assets/" + folder.TrimStart('/');
            AbyssVFXAssetFactory.EnsureAssetFolder(folder);
            return folder;
        }

        private static string ToAbsolutePath(string assetPath) => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }
}
#endif
