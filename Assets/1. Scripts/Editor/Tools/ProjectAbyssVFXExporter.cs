#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public static class ProjectAbyssVFXExporter
{
    private const string VfxRoot =
        "Assets/VFX";

    private const string RecipeRoot =
        "Assets/1. Scripts/Editor/AbyssVFXGraphGenerator";

    private const string OutputFolderName =
        "AllDataTXT/VFX";

    private const string CatalogFileName =
        "Project_Abyss_VFX_Catalog_For_AI.json";

    private const string TextBundleFileName =
        "Project_Abyss_VFX_Text_Assets_For_AI.txt";

    private const string MenuPath =
        "Tools/Project Abyss/Export VFX Context for AI";

    private const long MaximumTextAssetBytes =
        4L * 1024L * 1024L;

    private static readonly HashSet<string> TextExtensions =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".vfx",
            ".vfxoperator",
            ".vfxblock",
            ".shadergraph",
            ".shadersubgraph",
            ".shader",
            ".hlsl",
            ".compute",
            ".json",
            ".prefab",
            ".asset",
            ".mat",
            ".txt",
            ".md",
            ".asmref",
            ".asmdef"
        };

    private static readonly HashSet<string> ReverseReferenceExtensions =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".prefab",
            ".unity",
            ".asset",
            ".playable",
            ".controller",
            ".overridecontroller",
            ".mat",
            ".vfx",
            ".shadergraph",
            ".shadersubgraph"
        };

    [MenuItem(MenuPath, priority = 2002)]
    public static void ExportVFXContextForAI()
    {
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        string outputDirectory =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    OutputFolderName));

        string catalogPath =
            Path.Combine(
                outputDirectory,
                CatalogFileName);

        string textBundlePath =
            Path.Combine(
                outputDirectory,
                TextBundleFileName);

        string catalogTemporaryPath =
            catalogPath + ".tmp";

        string textTemporaryPath =
            textBundlePath + ".tmp";

        try
        {
            Directory.CreateDirectory(
                outputDirectory);

            VfxExportReport report =
                BuildReport(
                    projectRoot);

            string catalogJson =
                JsonUtility.ToJson(
                    report,
                    true);

            string textBundle =
                BuildTextBundle(
                    projectRoot,
                    report);

            WriteAtomic(
                catalogTemporaryPath,
                catalogPath,
                catalogJson);

            WriteAtomic(
                textTemporaryPath,
                textBundlePath,
                textBundle);

            string normalizedCatalogPath =
                NormalizePath(
                    catalogPath);

            string normalizedTextPath =
                NormalizePath(
                    textBundlePath);

            Debug.Log(
                "[ProjectAbyssVFXExporter] Export complete\n" +
                $"Assets      : {report.summary.assetCount}\n" +
                $"Graphs      : {report.summary.graphCount}\n" +
                $"Prefabs     : {report.summary.prefabCount}\n" +
                $"Recipes     : {report.summary.recipeCount}\n" +
                $"References  : {report.summary.reverseReferenceCount}\n" +
                $"Errors      : {report.errors.Count}\n" +
                $"Catalog     : {normalizedCatalogPath}\n" +
                $"Text Bundle : {normalizedTextPath}");

            EditorUtility.DisplayDialog(
                "VFX Context Export Complete",
                $"VFX AI 컨텍스트 내보내기가 완료되었습니다.\n\n" +
                $"VFX Assets: {report.summary.assetCount}\n" +
                $"VFX Graphs: {report.summary.graphCount}\n" +
                $"VFX Prefabs: {report.summary.prefabCount}\n" +
                $"Recipes: {report.summary.recipeCount}\n" +
                $"Reverse References: {report.summary.reverseReferenceCount}\n" +
                $"Errors: {report.errors.Count}\n\n" +
                $"{normalizedCatalogPath}\n" +
                $"{normalizedTextPath}",
                "확인");

            EditorUtility.RevealInFinder(
                catalogPath);
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfExists(
                catalogTemporaryPath);

            DeleteFileIfExists(
                textTemporaryPath);

            Debug.LogWarning(
                "[ProjectAbyssVFXExporter] Export cancelled.");
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(
                catalogTemporaryPath);

            DeleteFileIfExists(
                textTemporaryPath);

            Debug.LogException(
                exception);

            EditorUtility.DisplayDialog(
                "VFX Context Export Failed",
                $"VFX 컨텍스트 내보내기에 실패했습니다.\n\n{exception.Message}",
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static VfxExportReport BuildReport(
        string projectRoot)
    {
        VfxExportReport report =
            new VfxExportReport
            {
                formatVersion = "1.0",
                purpose = "AI_VFX_INVENTORY_REUSE_AND_INTEGRATION",
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                generatedAtLocal = DateTimeOffset.Now.ToString("O"),
                unityVersion = Application.unityVersion,
                sourceRoot = VfxRoot,
                recipeRoot = RecipeRoot,
                catalogNotes = new List<string>
                {
                    "assets contains every non-folder asset currently found under Assets/VFX.",
                    "graphs contains exposed properties, events, and dependencies for each VisualEffectAsset.",
                    "prefabs contains VisualEffect component bindings and serialized override data.",
                    "recipes contains JSON recipes found under the VFX generator folder and Assets/VFX.",
                    "reverseReferences lists project assets outside Assets/VFX that directly depend on VFX assets.",
                    "The companion text bundle contains readable source/YAML for graph, prefab, material, shader, and JSON assets."
                }
            };

        List<string> vfxAssetPaths =
            CollectAssetPathsUnderRoot(
                VfxRoot);

        List<string> recipePaths =
            CollectRecipePaths();

        HashSet<string> targetPaths =
            new HashSet<string>(
                vfxAssetPaths,
                StringComparer.Ordinal);

        for (int index = 0;
             index < vfxAssetPaths.Count;
             index++)
        {
            string assetPath =
                vfxAssetPaths[index];

            ShowProgress(
                "VFX 컨텍스트 수집",
                $"Asset [{index + 1}/{vfxAssetPaths.Count}] {assetPath}",
                0.45f * SafeRatio(index, vfxAssetPaths.Count));

            try
            {
                VfxAssetRecord assetRecord =
                    BuildAssetRecord(
                        projectRoot,
                        assetPath);

                report.assets.Add(
                    assetRecord);

                Type mainType =
                    AssetDatabase.GetMainAssetTypeAtPath(
                        assetPath);

                if (mainType != null &&
                    typeof(VisualEffectAsset).IsAssignableFrom(mainType))
                {
                    VfxGraphRecord graphRecord =
                        BuildGraphRecord(
                            assetPath);

                    report.graphs.Add(
                        graphRecord);
                }

                if (string.Equals(
                        Path.GetExtension(assetPath),
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    VfxPrefabRecord prefabRecord =
                        BuildPrefabRecord(
                            assetPath);

                    if (prefabRecord.visualEffects.Count > 0)
                    {
                        report.prefabs.Add(
                            prefabRecord);
                    }
                }
            }
            catch (Exception exception)
            {
                report.errors.Add(
                    $"{assetPath} | {exception.GetType().Name}: {exception.Message}");
            }
        }

        for (int index = 0;
             index < recipePaths.Count;
             index++)
        {
            string recipePath =
                recipePaths[index];

            ShowProgress(
                "VFX Recipe 수집",
                $"Recipe [{index + 1}/{recipePaths.Count}] {recipePath}",
                0.45f +
                0.10f * SafeRatio(index, recipePaths.Count));

            try
            {
                report.recipes.Add(
                    BuildRecipeRecord(
                        projectRoot,
                        recipePath));
            }
            catch (Exception exception)
            {
                report.errors.Add(
                    $"{recipePath} | {exception.GetType().Name}: {exception.Message}");
            }
        }

        BuildReverseReferences(
            targetPaths,
            report);

        report.assets =
            report.assets
                .OrderBy(
                    item => item.assetPath,
                    StringComparer.Ordinal)
                .ToList();

        report.graphs =
            report.graphs
                .OrderBy(
                    item => item.assetPath,
                    StringComparer.Ordinal)
                .ToList();

        report.prefabs =
            report.prefabs
                .OrderBy(
                    item => item.assetPath,
                    StringComparer.Ordinal)
                .ToList();

        report.recipes =
            report.recipes
                .OrderBy(
                    item => item.assetPath,
                    StringComparer.Ordinal)
                .ToList();

        report.reverseReferences =
            report.reverseReferences
                .OrderBy(
                    item => item.referencedVfxAssetPath,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item.referrerAssetPath,
                    StringComparer.Ordinal)
                .ToList();

        report.summary =
            BuildSummary(
                report);

        return report;
    }

    private static List<string> CollectAssetPathsUnderRoot(
        string root)
    {
        if (!AssetDatabase.IsValidFolder(root))
            return new List<string>();

        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    root
                });

        return guids
            .Select(
                AssetDatabase.GUIDToAssetPath)
            .Where(
                path =>
                    !string.IsNullOrWhiteSpace(path) &&
                    !AssetDatabase.IsValidFolder(path) &&
                    !path.EndsWith(
                        ".meta",
                        StringComparison.OrdinalIgnoreCase))
            .Distinct(
                StringComparer.Ordinal)
            .OrderBy(
                path => path,
                StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> CollectRecipePaths()
    {
        HashSet<string> result =
            new HashSet<string>(
                StringComparer.Ordinal);

        AddJsonAssetsUnderRoot(
            RecipeRoot,
            result);

        AddJsonAssetsUnderRoot(
            VfxRoot,
            result);

        return result
            .OrderBy(
                path => path,
                StringComparer.Ordinal)
            .ToList();
    }

    private static void AddJsonAssetsUnderRoot(
        string root,
        HashSet<string> output)
    {
        if (!AssetDatabase.IsValidFolder(root))
            return;

        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    root
                });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (string.Equals(
                    Path.GetExtension(path),
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                output.Add(
                    path);
            }
        }
    }

    private static VfxAssetRecord BuildAssetRecord(
        string projectRoot,
        string assetPath)
    {
        string absolutePath =
            ToAbsolutePath(
                projectRoot,
                assetPath);

        Type mainType =
            AssetDatabase.GetMainAssetTypeAtPath(
                assetPath);

        string[] directDependencies =
            AssetDatabase.GetDependencies(
                    assetPath,
                    false)
                .Where(
                    path =>
                        !string.Equals(
                            path,
                            assetPath,
                            StringComparison.Ordinal))
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToArray();

        VfxAssetRecord record =
            new VfxAssetRecord
            {
                assetPath = assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath),
                fileName = Path.GetFileName(assetPath),
                extension = Path.GetExtension(assetPath),
                assetType =
                    mainType == null
                        ? string.Empty
                        : mainType.FullName,
                fileSizeBytes =
                    File.Exists(absolutePath)
                        ? new FileInfo(absolutePath).Length
                        : 0L,
                sha256 =
                    File.Exists(absolutePath)
                        ? CalculateFileSha256(absolutePath)
                        : string.Empty,
                labels =
                    GetLabels(
                        assetPath),
                directDependencies =
                    directDependencies.ToList(),
                vfxDependencies =
                    directDependencies
                        .Where(
                            dependency =>
                                dependency.StartsWith(
                                    VfxRoot + "/",
                                    StringComparison.Ordinal))
                        .ToList()
            };

        UnityEngine.Object mainAsset =
            AssetDatabase.LoadMainAssetAtPath(
                assetPath);

        if (mainAsset is Texture texture)
        {
            record.texture =
                BuildTextureInfo(
                    assetPath,
                    texture);
        }
        else if (mainAsset is Mesh mesh)
        {
            record.mesh =
                BuildMeshInfo(
                    mesh);
        }
        else if (mainAsset is Material material)
        {
            record.material =
                BuildMaterialInfo(
                    material);
        }

        return record;
    }

    private static List<string> GetLabels(
        string assetPath)
    {
        UnityEngine.Object asset =
            AssetDatabase.LoadMainAssetAtPath(
                assetPath);

        if (asset == null)
            return new List<string>();

        return AssetDatabase.GetLabels(asset)
            .OrderBy(
                label => label,
                StringComparer.Ordinal)
            .ToList();
    }

    private static TextureInfo BuildTextureInfo(
        string assetPath,
        Texture texture)
    {
        TextureInfo info =
            new TextureInfo
            {
                width = texture.width,
                height = texture.height,
                dimension = texture.dimension.ToString(),
                wrapMode = texture.wrapMode.ToString(),
                filterMode = texture.filterMode.ToString(),
                anisoLevel = texture.anisoLevel
            };

        TextureImporter importer =
            AssetImporter.GetAtPath(
                assetPath) as TextureImporter;

        if (importer != null)
        {
            info.textureType =
                importer.textureType.ToString();

            info.textureShape =
                importer.textureShape.ToString();

            info.sRgbTexture =
                importer.sRGBTexture;

            info.alphaSource =
                importer.alphaSource.ToString();

            info.alphaIsTransparency =
                importer.alphaIsTransparency;

            info.mipmapEnabled =
                importer.mipmapEnabled;

            info.maxTextureSize =
                importer.maxTextureSize;

            info.textureCompression =
                importer.textureCompression.ToString();

            info.npotScale =
                importer.npotScale.ToString();
        }

        return info;
    }

    private static MeshInfo BuildMeshInfo(
        Mesh mesh)
    {
        return new MeshInfo
        {
            vertexCount = mesh.vertexCount,
            subMeshCount = mesh.subMeshCount,
            boundsCenter = Vector3Record.From(mesh.bounds.center),
            boundsSize = Vector3Record.From(mesh.bounds.size),
            isReadable = mesh.isReadable
        };
    }

    private static MaterialInfo BuildMaterialInfo(
        Material material)
    {
        return new MaterialInfo
        {
            shaderName =
                material.shader == null
                    ? string.Empty
                    : material.shader.name,
            shaderAssetPath =
                material.shader == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(material.shader),
            renderQueue = material.renderQueue,
            shaderKeywords =
                material.shaderKeywords
                    .OrderBy(
                        keyword => keyword,
                        StringComparer.Ordinal)
                    .ToList()
        };
    }

    private static VfxGraphRecord BuildGraphRecord(
        string assetPath)
    {
        VisualEffectAsset asset =
            AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                assetPath);

        VfxGraphRecord record =
            new VfxGraphRecord
            {
                assetPath = assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath)
            };

        if (asset == null)
        {
            record.readError =
                "VisualEffectAsset could not be loaded.";

            return record;
        }

        try
        {
            List<VFXExposedProperty> properties =
                new List<VFXExposedProperty>();

            asset.GetExposedProperties(
                properties);

            foreach (VFXExposedProperty property in properties)
            {
                record.exposedProperties.Add(
                    new ExposedPropertyRecord
                    {
                        name = property.name,
                        type =
                            property.type == null
                                ? string.Empty
                                : property.type.FullName
                    });
            }
        }
        catch (Exception exception)
        {
            record.readWarnings.Add(
                $"GetExposedProperties failed: {exception.Message}");
        }

        try
        {
            List<string> events =
                new List<string>();

            asset.GetEvents(
                events);

            record.events =
                events
                    .Distinct(
                        StringComparer.Ordinal)
                    .OrderBy(
                        eventName => eventName,
                        StringComparer.Ordinal)
                    .ToList();
        }
        catch (Exception exception)
        {
            record.readWarnings.Add(
                $"GetEvents failed: {exception.Message}");
        }

        string[] directDependencies =
            AssetDatabase.GetDependencies(
                    assetPath,
                    false)
                .Where(
                    path =>
                        !string.Equals(
                            path,
                            assetPath,
                            StringComparison.Ordinal))
                .ToArray();

        record.textureDependencies =
            directDependencies
                .Where(
                    path =>
                    {
                        Type type =
                            AssetDatabase.GetMainAssetTypeAtPath(path);

                        return type != null &&
                               typeof(Texture).IsAssignableFrom(type);
                    })
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        record.meshDependencies =
            directDependencies
                .Where(
                    path =>
                    {
                        Type type =
                            AssetDatabase.GetMainAssetTypeAtPath(path);

                        return type != null &&
                               typeof(Mesh).IsAssignableFrom(type);
                    })
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        record.subgraphDependencies =
            directDependencies
                .Where(
                    path =>
                    {
                        string extension =
                            Path.GetExtension(path);

                        return string.Equals(
                                   extension,
                                   ".vfxoperator",
                                   StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(
                                   extension,
                                   ".vfxblock",
                                   StringComparison.OrdinalIgnoreCase);
                    })
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        return record;
    }

    private static VfxPrefabRecord BuildPrefabRecord(
        string assetPath)
    {
        VfxPrefabRecord record =
            new VfxPrefabRecord
            {
                assetPath = assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath)
            };

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                assetPath);

        if (prefab == null)
            return record;

        VisualEffect[] components =
            prefab.GetComponentsInChildren<VisualEffect>(
                true);

        foreach (VisualEffect component in components)
        {
            VfxComponentRecord componentRecord =
                new VfxComponentRecord
                {
                    hierarchyPath =
                        GetHierarchyPath(
                            component.transform,
                            prefab.transform),
                    visualEffectAssetPath =
                        component.visualEffectAsset == null
                            ? string.Empty
                            : AssetDatabase.GetAssetPath(
                                component.visualEffectAsset),
                    componentJson =
                        TrySerializeComponent(
                            component)
                };

            record.visualEffects.Add(
                componentRecord);
        }

        return record;
    }

    private static string GetHierarchyPath(
        Transform transform,
        Transform prefabRoot)
    {
        List<string> names =
            new List<string>();

        Transform current =
            transform;

        while (current != null)
        {
            names.Add(
                current.name);

            if (current == prefabRoot)
                break;

            current =
                current.parent;
        }

        names.Reverse();

        return string.Join(
            "/",
            names);
    }

    private static string TrySerializeComponent(
        VisualEffect component)
    {
        try
        {
            return EditorJsonUtility.ToJson(
                component,
                true);
        }
        catch (Exception exception)
        {
            return $"SERIALIZE_ERROR: {exception.Message}";
        }
    }

    private static VfxRecipeRecord BuildRecipeRecord(
        string projectRoot,
        string assetPath)
    {
        string absolutePath =
            ToAbsolutePath(
                projectRoot,
                assetPath);

        string content =
            File.ReadAllText(
                absolutePath,
                Encoding.UTF8);

        RecipeProbe probe =
            new RecipeProbe();

        try
        {
            JsonUtility.FromJsonOverwrite(
                content,
                probe);
        }
        catch
        {
            // 원문과 해시는 계속 내보내므로 Probe 실패는 치명적이지 않다.
        }

        return new VfxRecipeRecord
        {
            assetPath = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath),
            name = probe.name ?? string.Empty,
            description = probe.description ?? string.Empty,
            schemaVersion = probe.schemaVersion ?? string.Empty,
            systemNames =
                probe.systems == null
                    ? new List<string>()
                    : probe.systems
                        .Where(
                            system => system != null)
                        .Select(
                            system => system.name ?? string.Empty)
                        .Where(
                            name => !string.IsNullOrWhiteSpace(name))
                        .ToList(),
            fileSizeBytes =
                new FileInfo(absolutePath).Length,
            sha256 =
                CalculateFileSha256(
                    absolutePath),
            rawJson = content
        };
    }

    private static void BuildReverseReferences(
        HashSet<string> targetPaths,
        VfxExportReport report)
    {
        if (targetPaths.Count == 0)
            return;

        string[] allAssetPaths =
            AssetDatabase.GetAllAssetPaths();

        List<string> candidates =
            allAssetPaths
                .Where(
                    path =>
                        path.StartsWith(
                            "Assets/",
                            StringComparison.Ordinal) &&
                        !path.StartsWith(
                            VfxRoot + "/",
                            StringComparison.Ordinal) &&
                        ReverseReferenceExtensions.Contains(
                            Path.GetExtension(path)))
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            string candidate =
                candidates[index];

            ShowProgress(
                "VFX 사용처 검색",
                $"Reference [{index + 1}/{candidates.Count}] {candidate}",
                0.55f +
                0.40f * SafeRatio(index, candidates.Count));

            try
            {
                string[] dependencies =
                    AssetDatabase.GetDependencies(
                        candidate,
                        false);

                foreach (string dependency in dependencies)
                {
                    if (!targetPaths.Contains(dependency))
                        continue;

                    report.reverseReferences.Add(
                        new ReverseReferenceRecord
                        {
                            referencedVfxAssetPath = dependency,
                            referrerAssetPath = candidate,
                            referrerAssetType =
                                GetAssetTypeName(
                                    candidate)
                        });
                }
            }
            catch (Exception exception)
            {
                report.errors.Add(
                    $"{candidate} | Reverse reference scan failed: {exception.Message}");
            }
        }
    }

    private static string GetAssetTypeName(
        string assetPath)
    {
        Type type =
            AssetDatabase.GetMainAssetTypeAtPath(
                assetPath);

        return type == null
            ? string.Empty
            : type.FullName;
    }

    private static VfxExportSummary BuildSummary(
        VfxExportReport report)
    {
        Dictionary<string, int> extensionCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        foreach (VfxAssetRecord asset in report.assets)
        {
            string extension =
                string.IsNullOrWhiteSpace(asset.extension)
                    ? "(none)"
                    : asset.extension.ToLowerInvariant();

            extensionCounts.TryGetValue(
                extension,
                out int count);

            extensionCounts[extension] =
                count + 1;
        }

        return new VfxExportSummary
        {
            assetCount = report.assets.Count,
            graphCount = report.graphs.Count,
            prefabCount = report.prefabs.Count,
            recipeCount = report.recipes.Count,
            exposedPropertyCount =
                report.graphs.Sum(
                    graph => graph.exposedProperties.Count),
            eventCount =
                report.graphs.Sum(
                    graph => graph.events.Count),
            reverseReferenceCount =
                report.reverseReferences.Count,
            errorCount =
                report.errors.Count,
            extensionCounts =
                extensionCounts
                    .OrderBy(
                        pair => pair.Key,
                        StringComparer.Ordinal)
                    .Select(
                        pair =>
                            new CountRecord
                            {
                                key = pair.Key,
                                count = pair.Value
                            })
                    .ToList()
        };
    }

    private static string BuildTextBundle(
        string projectRoot,
        VfxExportReport report)
    {
        HashSet<string> paths =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (VfxAssetRecord asset in report.assets)
        {
            if (TextExtensions.Contains(asset.extension))
            {
                paths.Add(
                    asset.assetPath);
            }
        }

        foreach (VfxRecipeRecord recipe in report.recipes)
        {
            paths.Add(
                recipe.assetPath);
        }

        List<string> orderedPaths =
            paths
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        StringBuilder output =
            new StringBuilder(
                64 * 1024);

        output.AppendLine(
            "PROJECT_ABYSS_VFX_TEXT_ASSET_EXPORT");

        output.AppendLine(
            "FORMAT_VERSION: 1");

        output.AppendLine(
            "PURPOSE: AI_VFX_GRAPH_AND_RECIPE_ANALYSIS");

        output.AppendLine(
            $"GENERATED_AT_UTC: {DateTime.UtcNow:O}");

        output.AppendLine(
            $"UNITY_VERSION: {Application.unityVersion}");

        output.AppendLine(
            $"SOURCE_ROOT: {VfxRoot}");

        output.AppendLine(
            $"TEXT_ASSET_COUNT: {orderedPaths.Count}");

        output.AppendLine();

        for (int index = 0;
             index < orderedPaths.Count;
             index++)
        {
            string assetPath =
                orderedPaths[index];

            ShowProgress(
                "VFX 텍스트 번들 생성",
                $"Text [{index + 1}/{orderedPaths.Count}] {assetPath}",
                0.95f +
                0.05f * SafeRatio(index, orderedPaths.Count));

            output.AppendLine(
                "================================================================================");

            output.AppendLine(
                "FILE_BEGIN");

            output.AppendLine(
                $"FILE_INDEX: {index + 1}/{orderedPaths.Count}");

            output.AppendLine(
                $"FILE_PATH: {assetPath}");

            output.AppendLine(
                $"FILE_GUID: {AssetDatabase.AssetPathToGUID(assetPath)}");

            output.AppendLine(
                $"FILE_TYPE: {GetAssetTypeName(assetPath)}");

            string absolutePath =
                ToAbsolutePath(
                    projectRoot,
                    assetPath);

            try
            {
                FileInfo fileInfo =
                    new FileInfo(
                        absolutePath);

                output.AppendLine(
                    $"FILE_SIZE_BYTES: {fileInfo.Length}");

                output.AppendLine(
                    $"SHA256: {CalculateFileSha256(absolutePath)}");

                if (fileInfo.Length >
                    MaximumTextAssetBytes)
                {
                    output.AppendLine(
                        "READ_STATUS: SKIPPED_TOO_LARGE");

                    output.AppendLine(
                        $"MAXIMUM_TEXT_ASSET_BYTES: {MaximumTextAssetBytes}");
                }
                else
                {
                    byte[] bytes =
                        File.ReadAllBytes(
                            absolutePath);

                    if (ContainsNullByte(bytes))
                    {
                        output.AppendLine(
                            "READ_STATUS: SKIPPED_BINARY");
                    }
                    else
                    {
                        string content =
                            DecodeText(
                                bytes);

                        output.AppendLine(
                            "READ_STATUS: OK");

                        output.AppendLine(
                            "CONTENT_BEGIN");

                        output.Append(
                            NormalizeNewlines(
                                content));

                        if (!content.EndsWith(
                                "\n",
                                StringComparison.Ordinal) &&
                            !content.EndsWith(
                                "\r",
                                StringComparison.Ordinal))
                        {
                            output.AppendLine();
                        }

                        output.AppendLine(
                            "CONTENT_END");
                    }
                }
            }
            catch (Exception exception)
            {
                output.AppendLine(
                    "READ_STATUS: ERROR");

                output.AppendLine(
                    $"READ_ERROR: {SanitizeSingleLine(exception.Message)}");
            }

            output.AppendLine(
                "FILE_END");

            output.AppendLine();
        }

        output.AppendLine(
            "PROJECT_ABYSS_VFX_TEXT_ASSET_EXPORT_END");

        return output.ToString();
    }

    private static string DecodeText(
        byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(
                bytes,
                3,
                bytes.Length - 3);
        }

        try
        {
            UTF8Encoding strictUtf8 =
                new UTF8Encoding(
                    false,
                    true);

            return strictUtf8.GetString(
                bytes);
        }
        catch
        {
            return Encoding.Default.GetString(
                bytes);
        }
    }

    private static bool ContainsNullByte(
        byte[] bytes)
    {
        for (int index = 0;
             index < bytes.Length;
             index++)
        {
            if (bytes[index] == 0)
                return true;
        }

        return false;
    }

    private static string NormalizeNewlines(
        string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace(
                    "\r\n",
                    "\n")
                .Replace(
                    "\r",
                    "\n");
    }

    private static float SafeRatio(
        int index,
        int count)
    {
        return count <= 0
            ? 1f
            : Mathf.Clamp01(
                (float)index / count);
    }

    private static void ShowProgress(
        string title,
        string info,
        float progress)
    {
        bool cancelled =
            EditorUtility.DisplayCancelableProgressBar(
                title,
                info,
                Mathf.Clamp01(progress));

        if (cancelled)
        {
            throw new OperationCanceledException(
                "VFX context export was cancelled.");
        }
    }

    private static string ToAbsolutePath(
        string projectRoot,
        string assetPath)
    {
        return Path.GetFullPath(
            Path.Combine(
                projectRoot,
                assetPath));
    }

    private static string CalculateFileSha256(
        string path)
    {
        using (SHA256 sha256 =
               SHA256.Create())
        using (FileStream stream =
               File.OpenRead(path))
        {
            byte[] hash =
                sha256.ComputeHash(
                    stream);

            StringBuilder output =
                new StringBuilder(
                    hash.Length * 2);

            foreach (byte value in hash)
            {
                output.Append(
                    value.ToString("x2"));
            }

            return output.ToString();
        }
    }

    private static void WriteAtomic(
        string temporaryPath,
        string outputPath,
        string content)
    {
        DeleteFileIfExists(
            temporaryPath);

        File.WriteAllText(
            temporaryPath,
            content,
            new UTF8Encoding(
                false));

        DeleteFileIfExists(
            outputPath);

        File.Move(
            temporaryPath,
            outputPath);
    }

    private static void DeleteFileIfExists(
        string path)
    {
        if (!string.IsNullOrWhiteSpace(path) &&
            File.Exists(path))
        {
            File.Delete(
                path);
        }
    }

    private static string NormalizePath(
        string path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : path.Replace(
                '\\',
                '/');
    }

    private static string SanitizeSingleLine(
        string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace(
                    "\r",
                    " ")
                .Replace(
                    "\n",
                    " ");
    }

    [Serializable]
    private sealed class VfxExportReport
    {
        public string formatVersion;
        public string purpose;
        public string generatedAtUtc;
        public string generatedAtLocal;
        public string unityVersion;
        public string sourceRoot;
        public string recipeRoot;
        public VfxExportSummary summary =
            new VfxExportSummary();
        public List<string> catalogNotes =
            new List<string>();
        public List<VfxAssetRecord> assets =
            new List<VfxAssetRecord>();
        public List<VfxGraphRecord> graphs =
            new List<VfxGraphRecord>();
        public List<VfxPrefabRecord> prefabs =
            new List<VfxPrefabRecord>();
        public List<VfxRecipeRecord> recipes =
            new List<VfxRecipeRecord>();
        public List<ReverseReferenceRecord> reverseReferences =
            new List<ReverseReferenceRecord>();
        public List<string> errors =
            new List<string>();
    }

    [Serializable]
    private sealed class VfxExportSummary
    {
        public int assetCount;
        public int graphCount;
        public int prefabCount;
        public int recipeCount;
        public int exposedPropertyCount;
        public int eventCount;
        public int reverseReferenceCount;
        public int errorCount;
        public List<CountRecord> extensionCounts =
            new List<CountRecord>();
    }

    [Serializable]
    private sealed class CountRecord
    {
        public string key;
        public int count;
    }

    [Serializable]
    private sealed class VfxAssetRecord
    {
        public string assetPath;
        public string guid;
        public string fileName;
        public string extension;
        public string assetType;
        public long fileSizeBytes;
        public string sha256;
        public List<string> labels =
            new List<string>();
        public List<string> directDependencies =
            new List<string>();
        public List<string> vfxDependencies =
            new List<string>();
        public TextureInfo texture;
        public MeshInfo mesh;
        public MaterialInfo material;
    }

    [Serializable]
    private sealed class TextureInfo
    {
        public int width;
        public int height;
        public string dimension;
        public string textureType;
        public string textureShape;
        public bool sRgbTexture;
        public string alphaSource;
        public bool alphaIsTransparency;
        public bool mipmapEnabled;
        public int maxTextureSize;
        public string textureCompression;
        public string npotScale;
        public string wrapMode;
        public string filterMode;
        public int anisoLevel;
    }

    [Serializable]
    private sealed class MeshInfo
    {
        public int vertexCount;
        public int subMeshCount;
        public Vector3Record boundsCenter;
        public Vector3Record boundsSize;
        public bool isReadable;
    }

    [Serializable]
    private sealed class MaterialInfo
    {
        public string shaderName;
        public string shaderAssetPath;
        public int renderQueue;
        public List<string> shaderKeywords =
            new List<string>();
    }

    [Serializable]
    private sealed class Vector3Record
    {
        public float x;
        public float y;
        public float z;

        public static Vector3Record From(
            Vector3 value)
        {
            return new Vector3Record
            {
                x = value.x,
                y = value.y,
                z = value.z
            };
        }
    }

    [Serializable]
    private sealed class VfxGraphRecord
    {
        public string assetPath;
        public string guid;
        public List<ExposedPropertyRecord> exposedProperties =
            new List<ExposedPropertyRecord>();
        public List<string> events =
            new List<string>();
        public List<string> textureDependencies =
            new List<string>();
        public List<string> meshDependencies =
            new List<string>();
        public List<string> subgraphDependencies =
            new List<string>();
        public List<string> readWarnings =
            new List<string>();
        public string readError;
    }

    [Serializable]
    private sealed class ExposedPropertyRecord
    {
        public string name;
        public string type;
    }

    [Serializable]
    private sealed class VfxPrefabRecord
    {
        public string assetPath;
        public string guid;
        public List<VfxComponentRecord> visualEffects =
            new List<VfxComponentRecord>();
    }

    [Serializable]
    private sealed class VfxComponentRecord
    {
        public string hierarchyPath;
        public string visualEffectAssetPath;
        public string componentJson;
    }

    [Serializable]
    private sealed class VfxRecipeRecord
    {
        public string assetPath;
        public string guid;
        public string schemaVersion;
        public string name;
        public string description;
        public List<string> systemNames =
            new List<string>();
        public long fileSizeBytes;
        public string sha256;
        public string rawJson;
    }

    [Serializable]
    private sealed class ReverseReferenceRecord
    {
        public string referencedVfxAssetPath;
        public string referrerAssetPath;
        public string referrerAssetType;
    }

    [Serializable]
    private sealed class RecipeProbe
    {
        public string schemaVersion;
        public string name;
        public string description;
        public RecipeSystemProbe[] systems;
    }

    [Serializable]
    private sealed class RecipeSystemProbe
    {
        public string name;
    }
}

#endif
