#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectAbyss.Editor.VFXAI
{
    /// <summary>
    /// Creates common HLSL aura materials and connects the matching runtime bridge
    /// without a compile-time dependency on Assembly-CSharp.
    /// </summary>
    internal static class AbyssVFXSurfaceAuraAssetFactory
    {
        private const string SkinnedShaderPath = "Assets/VFX/Shaders/AbyssSkinnedFlameAura.shader";
        private const string CurtainShaderPath = "Assets/VFX/Shaders/AbyssShinCurtainAura.shader";
        private const string SkinnedControllerTypeName = "ProjectAbyss.VFX.AbyssSkinnedAuraController";
        private const string CurtainControllerTypeName = "ProjectAbyss.VFX.AbyssShinCurtainAuraController";
        private const string LegacyControllerTypeName = "ProjectAbyss.VFX.AbyssShinSurfaceAuraController";

        internal static void ConfigurePrefab(
            GameObject prefabRoot,
            AbyssVFXSurfaceAuraRecipe recipe,
            string outputFolder,
            string baseName,
            List<string> diagnostics)
        {
            if (prefabRoot == null)
                throw new ArgumentNullException(nameof(prefabRoot));
            if (recipe == null || !recipe.enabled)
                return;

            bool curtainMode = string.Equals(recipe.shaderFamily, "ShinCurtainAura", StringComparison.Ordinal) &&
                               string.Equals(recipe.rendererMode, "CameraBackCurtain", StringComparison.Ordinal);
            bool skinnedMode = string.Equals(recipe.shaderFamily, "SkinnedFlameAura", StringComparison.Ordinal) &&
                               string.Equals(recipe.rendererMode, "BackSilhouette", StringComparison.Ordinal);
            if (!curtainMode && !skinnedMode)
            {
                throw new InvalidOperationException(
                    "지원하지 않는 Surface Aura 조합입니다: " +
                    recipe.shaderFamily + " + " + recipe.rendererMode);
            }

            RemoveConflictingControllers(prefabRoot, curtainMode ? CurtainControllerTypeName : SkinnedControllerTypeName);
            if (curtainMode)
                ConfigureCurtain(prefabRoot, recipe, outputFolder, baseName, diagnostics);
            else
                ConfigureSkinned(prefabRoot, recipe, outputFolder, baseName, diagnostics);
        }

        private static void ConfigureCurtain(
            GameObject prefabRoot,
            AbyssVFXSurfaceAuraRecipe recipe,
            string outputFolder,
            string baseName,
            List<string> diagnostics)
        {
            Shader shader = LoadShader(recipe.shaderAssetPath, CurtainShaderPath, "Project Abyss/VFX/Shin Curtain Aura");
            if (shader == null)
                throw new InvalidOperationException("공용 Shin Curtain HLSL Shader를 찾지 못했습니다: " + recipe.shaderAssetPath);

            string curtainPath = $"{outputFolder}/{baseName}_ShinCurtain.mat";
            string corePath = $"{outputFolder}/{baseName}_ShinCurtainCore.mat";
            Material curtainMaterial = LoadOrCreateMaterial(curtainPath, baseName + "_ShinCurtain", shader);
            Material coreMaterial = LoadOrCreateMaterial(corePath, baseName + "_ShinCurtainCore", shader);

            ConfigureCurtainMaterial(curtainMaterial, recipe, false);
            ConfigureCurtainMaterial(coreMaterial, recipe, true);
            FinalizeMaterial(curtainMaterial, "AbyssShinCurtain");
            FinalizeMaterial(coreMaterial, "AbyssShinCurtain");

            Type controllerType = ResolveRequiredType(CurtainControllerTypeName);
            Component controller = prefabRoot.GetComponent(controllerType) ?? prefabRoot.AddComponent(controllerType);
            SerializedObject serialized = new(controller);
            serialized.Update();
            SetObjectReference(serialized, "curtainMaterial", curtainMaterial);
            SetObjectReference(serialized, "coreMaterial", coreMaterial);
            SetBool(serialized, "autoBindToParent", recipe.autoBindToParent);
            SetBool(serialized, "includeInactiveRenderers", recipe.includeInactiveRenderers);
            SetBool(serialized, "previewInEditMode", false);
            SetBool(serialized, "followCamera", recipe.followCamera);
            SetInt(serialized, "curtainLayerCount", recipe.curtainLayerCount);
            SetFloat(serialized, "widthMultiplier", recipe.curtainWidthMultiplier);
            SetFloat(serialized, "heightMultiplier", recipe.curtainHeightMultiplier);
            SetFloat(serialized, "rearDistance", recipe.curtainRearDistance);
            SetFloat(serialized, "verticalOffset", recipe.curtainVerticalOffset);
            SetFloat(serialized, "curveDepth", recipe.curtainCurveDepth);
            SetFloat(serialized, "layerDepthStep", recipe.curtainLayerDepthStep);
            SetFloat(serialized, "layerScaleStep", recipe.curtainLayerScaleStep);
            SetInt(serialized, "meshColumns", recipe.curtainColumns);
            SetInt(serialized, "meshRows", recipe.curtainRows);
            SetFloat(serialized, "boundsPadding", recipe.boundsPadding);
            SetStringArray(serialized, "excludedNameContains", recipe.excludedNameContains);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            if (recipe.previewInEditMode)
                diagnostics?.Add("Edit Mode Aura preview는 Unity persistent-object 안전을 위해 비활성화됐습니다. Play Mode에서 확인하세요.");

            diagnostics?.Add(
                $"Reference Shin curtain configured: controller={controllerType.FullName}, " +
                $"shader={AssetDatabase.GetAssetPath(shader)}, curtain={curtainPath}, core={corePath}, " +
                $"layers={recipe.curtainLayerCount}, geometry={recipe.curtainColumns}x{recipe.curtainRows}");
        }

        private static void ConfigureCurtainMaterial(
            Material material,
            AbyssVFXSurfaceAuraRecipe recipe,
            bool core)
        {
            SetColor(material, "_BaseColor", recipe.baseColor.ToColor());
            SetColor(material, "_EdgeColor", recipe.edgeColor.ToColor());
            SetColor(material, "_HotColor", recipe.hotColor.ToColor());
            SetFloat(material, "_NoiseScale", recipe.noiseScale);
            SetFloat(material, "_DetailNoiseScale", recipe.detailNoiseScale);
            SetFloat(material, "_NoiseSpeed", recipe.noiseSpeed);
            SetFloat(material, "_FlameStretch", recipe.flameStretch);
            SetFloat(material, "_PulseSpeed", recipe.pulseSpeed);
            SetFloat(material, "_PulseAmount", recipe.pulseAmount);
            SetFloat(material, "_LateralWobble", recipe.lateralWobble);
            SetFloat(material, "_DepthWobble", recipe.depthWobble);
            SetFloat(material, "_LobeSeparation", recipe.lobeSeparation);
            SetFloat(material, "_LobeWidth", core ? recipe.lobeWidth * 0.72f : recipe.lobeWidth);
            SetFloat(material, "_TipVariation", recipe.tipVariation);
            SetFloat(material, "_SideFade", recipe.sideFade);
            SetFloat(material, "_BottomFade", recipe.bottomFade);
            SetFloat(material, "_LayerMode", core ? 1f : 0f);
            SetFloat(material, "_Emission", core ? recipe.coreEmission : recipe.flameEmission);
            SetFloat(material, "_Opacity", core ? recipe.coreOpacity : recipe.flameOpacity);
            SetFloat(material, "_Cutoff", core
                ? Mathf.Clamp01(recipe.cutoff + 0.07f)
                : recipe.cutoff);
            SetFloat(material, "_EdgeSoftness", core
                ? Mathf.Max(0.001f, recipe.edgeSoftness * 0.72f)
                : recipe.edgeSoftness);
            material.renderQueue = (int)RenderQueue.Transparent + (core ? 5 : 3);
        }

        private static void ConfigureSkinned(
            GameObject prefabRoot,
            AbyssVFXSurfaceAuraRecipe recipe,
            string outputFolder,
            string baseName,
            List<string> diagnostics)
        {
            Shader shader = LoadShader(recipe.shaderAssetPath, SkinnedShaderPath, "Project Abyss/VFX/Skinned Flame Aura");
            if (shader == null)
                throw new InvalidOperationException("공용 Skinned Flame Aura Shader를 찾지 못했습니다: " + recipe.shaderAssetPath);

            string corePath = $"{outputFolder}/{baseName}_AuraCore.mat";
            string flamePath = $"{outputFolder}/{baseName}_AuraFlame.mat";
            Material coreMaterial = LoadOrCreateMaterial(corePath, baseName + "_AuraCore", shader);
            Material flameMaterial = LoadOrCreateMaterial(flamePath, baseName + "_AuraFlame", shader);
            ConfigureSkinnedMaterial(coreMaterial, recipe, true);
            ConfigureSkinnedMaterial(flameMaterial, recipe, false);
            FinalizeMaterial(coreMaterial, "AbyssCommonHlslAura");
            FinalizeMaterial(flameMaterial, "AbyssCommonHlslAura");

            Type controllerType = ResolveRequiredType(SkinnedControllerTypeName);
            Component controller = prefabRoot.GetComponent(controllerType) ?? prefabRoot.AddComponent(controllerType);
            SerializedObject serialized = new(controller);
            serialized.Update();
            SetObjectReference(serialized, "coreAuraMaterial", coreMaterial);
            SetObjectReference(serialized, "flameAuraMaterial", flameMaterial);
            SetBool(serialized, "autoBindToParent", recipe.autoBindToParent);
            SetBool(serialized, "includeInactiveRenderers", recipe.includeInactiveRenderers);
            SetBool(serialized, "previewInEditMode", false);
            SetFloat(serialized, "boundsPadding", recipe.boundsPadding);
            SetStringArray(serialized, "excludedNameContains", recipe.excludedNameContains);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            if (recipe.previewInEditMode)
                diagnostics?.Add("Edit Mode Aura preview는 Unity persistent-object 안전을 위해 비활성화됐습니다. Play Mode에서 확인하세요.");

            diagnostics?.Add($"Legacy skinned HLSL aura configured: controller={controllerType.FullName}");
        }

        private static void ConfigureSkinnedMaterial(Material material, AbyssVFXSurfaceAuraRecipe recipe, bool core)
        {
            SetColor(material, "_BaseColor", recipe.baseColor.ToColor());
            SetColor(material, "_EdgeColor", recipe.edgeColor.ToColor());
            SetColor(material, "_HotColor", recipe.hotColor.ToColor());
            SetFloat(material, "_NoiseScale", recipe.noiseScale);
            SetFloat(material, "_DetailNoiseScale", recipe.detailNoiseScale);
            SetFloat(material, "_NoiseSpeed", recipe.noiseSpeed);
            SetFloat(material, "_FlameStretch", recipe.flameStretch);
            SetFloat(material, "_RearBias", recipe.rearBias);
            SetFloat(material, "_PulseSpeed", recipe.pulseSpeed);
            SetFloat(material, "_PulseAmount", recipe.pulseAmount);
            SetFloat(material, "_LayerMode", core ? 0f : 1f);
            SetFloat(material, "_Emission", core ? recipe.coreEmission : recipe.flameEmission);
            SetFloat(material, "_Opacity", core ? recipe.coreOpacity : recipe.flameOpacity);
            SetFloat(material, "_FresnelPower", core ? recipe.coreFresnelPower : recipe.flameFresnelPower);
            SetFloat(material, "_ShellWidth", core ? recipe.innerWidth : recipe.outerWidth);
            SetFloat(material, "_RearOffset", core ? recipe.rearOffset * 0.45f : recipe.rearOffset);
            SetFloat(material, "_FlameLift", core ? recipe.flameLift * 0.18f : recipe.flameLift);
            SetFloat(material, "_LateralWobble", core ? recipe.lateralWobble * 0.25f : recipe.lateralWobble);
            SetFloat(material, "_Cutoff", core ? Mathf.Clamp01(recipe.cutoff - 0.18f) : recipe.cutoff);
            SetFloat(material, "_EdgeSoftness", core ? Mathf.Max(0.001f, recipe.edgeSoftness * 1.45f) : recipe.edgeSoftness);
            SetFloat(material, "_Cull", (float)CullMode.Front);
            material.renderQueue = (int)RenderQueue.Transparent + (core ? 8 : 4);
        }

        private static void RemoveConflictingControllers(GameObject root, string keepTypeName)
        {
            string[] candidates = { SkinnedControllerTypeName, CurtainControllerTypeName, LegacyControllerTypeName };
            foreach (string typeName in candidates)
            {
                if (string.Equals(typeName, keepTypeName, StringComparison.Ordinal))
                    continue;
                Type type = TryResolveType(typeName);
                if (type == null)
                    continue;
                Component component = root.GetComponent(type);
                if (component != null)
                    UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static Type ResolveRequiredType(string fullName)
        {
            Type type = TryResolveType(fullName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "런타임 Aura Controller를 찾지 못했습니다: " + fullName +
                    ". Runtime/VFX 스크립트가 존재하고 컴파일됐는지 확인하세요.");
            }
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                throw new InvalidOperationException(fullName + " 타입은 MonoBehaviour가 아닙니다.");
            return type;
        }

        private static Type TryResolveType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type candidate = assembly.GetType(fullName, false, false);
                    if (candidate != null)
                        return candidate;
                }
                catch
                {
                    // Ignore dynamic or partially loaded editor assemblies.
                }
            }
            return null;
        }

        private static Shader LoadShader(string configuredPath, string fallbackPath, string shaderName)
        {
            string path = string.IsNullOrWhiteSpace(configuredPath)
                ? fallbackPath
                : configuredPath.Replace('\\', '/');
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            return shader != null ? shader : Shader.Find(shaderName);
        }

        private static Material LoadOrCreateMaterial(string path, string name, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
                material.name = name;
            }
            return material;
        }

        private static void FinalizeMaterial(Material material, string label)
        {
            EditorUtility.SetDirty(material);
            List<string> labels = new(AssetDatabase.GetLabels(material));
            if (!labels.Contains("ProjectAbyss")) labels.Add("ProjectAbyss");
            if (!labels.Contains("AbyssGeneratedVFX")) labels.Add("AbyssGeneratedVFX");
            if (!labels.Contains(label)) labels.Add(label);
            AssetDatabase.SetLabels(material, labels.ToArray());
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().FullName +
                    "에 직렬화 필드 '" + name + "'가 없습니다. Runtime Controller와 Editor Factory 버전이 다릅니다.");
            }
            return property;
        }

        private static void SetObjectReference(SerializedObject serialized, string name, UnityEngine.Object value)
            => RequireProperty(serialized, name).objectReferenceValue = value;

        private static void SetBool(SerializedObject serialized, string name, bool value)
            => RequireProperty(serialized, name).boolValue = value;

        private static void SetFloat(SerializedObject serialized, string name, float value)
            => RequireProperty(serialized, name).floatValue = value;

        private static void SetInt(SerializedObject serialized, string name, int value)
            => RequireProperty(serialized, name).intValue = value;

        private static void SetStringArray(SerializedObject serialized, string name, IEnumerable<string> values)
        {
            SerializedProperty property = RequireProperty(serialized, name);
            List<string> clean = (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            property.arraySize = clean.Count;
            for (int i = 0; i < clean.Count; i++)
                property.GetArrayElementAtIndex(i).stringValue = clean[i];
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
                material.SetFloat(name, value);
        }

        private static void SetColor(Material material, string name, Color value)
        {
            if (material.HasProperty(name))
                material.SetColor(name, value);
        }
    }
}
#endif
