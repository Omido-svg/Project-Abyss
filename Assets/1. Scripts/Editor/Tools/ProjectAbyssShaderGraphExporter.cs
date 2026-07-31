#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 전체의 Shader Graph/Sub Graph 원본과 의존성, 사용 Material,
/// 역참조 정보를 AI 분석용 JSON + 텍스트 번들로 내보냅니다.
/// Shader Graph 패키지의 internal API를 사용하지 않아 패키지 버전 의존성을 최소화합니다.
/// </summary>
public static class ProjectAbyssShaderGraphExporter
{
    private const string SearchRoot = "Assets";
    private const string OutputFolderName = "AllDataTXT/ShaderGraphs";
    private const string CatalogFileName = "Project_Abyss_ShaderGraph_Catalog_For_AI.json";
    private const string TextBundleFileName = "Project_Abyss_ShaderGraph_Text_Assets_For_AI.txt";
    private const string MenuPath = "Tools/Project Abyss/Exports/Export Shader Graph Context for AI";
    private const long MaximumTextAssetBytes = 4L * 1024L * 1024L;

    private static readonly HashSet<string> GraphExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".shadergraph",
            ".shadersubgraph"
        };

    private static readonly HashSet<string> SourceExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".shadergraph",
            ".shadersubgraph",
            ".shader",
            ".hlsl",
            ".cginc",
            ".compute",
            ".mat",
            ".json",
            ".txt",
            ".md"
        };

    private static readonly HashSet<string> ReverseReferenceExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mat",
            ".prefab",
            ".unity",
            ".asset",
            ".vfx",
            ".shadergraph",
            ".shadersubgraph",
            ".controller",
            ".overridecontroller",
            ".playable",
            ".shader",
            ".compute"
        };

    [MenuItem(MenuPath, priority = 2003)]
    public static void ExportShaderGraphContextForAI()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, OutputFolderName));
        string catalogPath = Path.Combine(outputDirectory, CatalogFileName);
        string textBundlePath = Path.Combine(outputDirectory, TextBundleFileName);
        string catalogTemporaryPath = catalogPath + ".tmp";
        string textTemporaryPath = textBundlePath + ".tmp";

        try
        {
            Directory.CreateDirectory(outputDirectory);

            ShaderGraphExportReport report = BuildReport(projectRoot);
            string textBundle = BuildTextBundle(projectRoot, report);
            string catalogJson = JsonUtility.ToJson(report, true);

            WriteAtomic(catalogTemporaryPath, catalogPath, catalogJson);
            WriteAtomic(textTemporaryPath, textBundlePath, textBundle);

            string normalizedCatalogPath = NormalizePath(catalogPath);
            string normalizedTextPath = NormalizePath(textBundlePath);

            Debug.Log(
                "[ProjectAbyssShaderGraphExporter] Export complete\n" +
                $"Shader Graphs : {report.summary.shaderGraphCount}\n" +
                $"Sub Graphs    : {report.summary.subGraphCount}\n" +
                $"Materials     : {report.summary.materialUsageCount}\n" +
                $"References    : {report.summary.reverseReferenceCount}\n" +
                $"Text Assets   : {report.summary.textAssetCount}\n" +
                $"Errors        : {report.summary.errorCount}\n" +
                $"Catalog       : {normalizedCatalogPath}\n" +
                $"Text Bundle   : {normalizedTextPath}");

            if (!ProjectAbyssExportSession.IsBatch)
            {
                EditorUtility.DisplayDialog(
                    "Shader Graph Export Complete",
                    "Shader Graph 컨텍스트 내보내기가 완료되었습니다.\n\n" +
                    $"Shader Graphs: {report.summary.shaderGraphCount}\n" +
                    $"Sub Graphs: {report.summary.subGraphCount}\n" +
                    $"Materials: {report.summary.materialUsageCount}\n" +
                    $"Reverse References: {report.summary.reverseReferenceCount}\n" +
                    $"Errors: {report.summary.errorCount}\n\n" +
                    normalizedCatalogPath + "\n" +
                    normalizedTextPath,
                    "확인");

                EditorUtility.RevealInFinder(catalogPath);
            }
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfExists(catalogTemporaryPath);
            DeleteFileIfExists(textTemporaryPath);
            Debug.LogWarning("[ProjectAbyssShaderGraphExporter] Export cancelled.");
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(catalogTemporaryPath);
            DeleteFileIfExists(textTemporaryPath);
            Debug.LogException(exception);

            if (!ProjectAbyssExportSession.IsBatch)
            {
                EditorUtility.DisplayDialog(
                    "Shader Graph Export Failed",
                    "Shader Graph 컨텍스트 내보내기에 실패했습니다.\n\n" + exception.Message,
                    "확인");
            }
            else
            {
                throw;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static ShaderGraphExportReport BuildReport(string projectRoot)
    {
        ShaderGraphExportReport report = new()
        {
            formatVersion = "1.0",
            purpose = "AI_SHADER_GRAPH_SOURCE_DEPENDENCY_AND_USAGE_ANALYSIS",
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            generatedAtLocal = DateTimeOffset.Now.ToString("O"),
            unityVersion = Application.unityVersion,
            sourceRoot = SearchRoot,
            catalogNotes = new List<string>
            {
                "graphs contains every .shadergraph and .shadersubgraph asset under Assets.",
                "The exporter intentionally avoids UnityEditor.ShaderGraph internal APIs.",
                "directDependencies are collected with AssetDatabase.GetDependencies(recursive: false).",
                "materialUsages maps Material.shader back to the Shader Graph asset path when Unity exposes that association.",
                "reverseReferences contains project assets whose direct dependencies include a Shader Graph or Sub Graph.",
                "The companion text bundle contains raw graph source, text dependencies, and Material YAML when readable."
            }
        };

        List<string> graphPaths = CollectGraphPaths();
        HashSet<string> graphPathSet = new(graphPaths, StringComparer.Ordinal);

        for (int index = 0; index < graphPaths.Count; index++)
        {
            string graphPath = graphPaths[index];
            ShowProgress(
                "Shader Graph 수집",
                $"Graph [{index + 1}/{graphPaths.Count}] {graphPath}",
                0.38f * SafeRatio(index, graphPaths.Count));

            try
            {
                report.graphs.Add(BuildGraphRecord(projectRoot, graphPath));
            }
            catch (Exception exception)
            {
                report.errors.Add(graphPath + ": " + exception.Message);
            }
        }

        CollectMaterialUsages(report, graphPathSet, 0.38f, 0.25f);
        CollectReverseReferences(report, graphPathSet, 0.63f, 0.30f);

        Dictionary<string, List<string>> materialUsersByGraph = report.materialUsages
            .GroupBy(record => record.shaderGraphPath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(record => record.materialPath)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        foreach (ShaderGraphRecord graph in report.graphs)
        {
            if (materialUsersByGraph.TryGetValue(graph.assetPath, out List<string> users))
                graph.materialUsers.AddRange(users);
        }

        report.summary.graphCount = report.graphs.Count;
        report.summary.shaderGraphCount = report.graphs.Count(record => record.graphKind == "ShaderGraph");
        report.summary.subGraphCount = report.graphs.Count(record => record.graphKind == "SubGraph");
        report.summary.materialUsageCount = report.materialUsages.Count;
        report.summary.reverseReferenceCount = report.reverseReferences.Count;
        report.summary.errorCount = report.errors.Count;

        return report;
    }

    private static ShaderGraphRecord BuildGraphRecord(string projectRoot, string assetPath)
    {
        string absolutePath = ToAbsolutePath(projectRoot, assetPath);
        FileInfo fileInfo = new(absolutePath);
        string extension = Path.GetExtension(assetPath);
        string[] directDependencies = AssetDatabase.GetDependencies(assetPath, false)
            .Where(path => !string.Equals(path, assetPath, StringComparison.Ordinal))
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Shader shader = string.Equals(extension, ".shadergraph", StringComparison.OrdinalIgnoreCase)
            ? AssetDatabase.LoadAssetAtPath<Shader>(assetPath)
            : null;

        ShaderGraphRecord record = new()
        {
            assetPath = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath),
            fileName = Path.GetFileName(assetPath),
            graphKind = string.Equals(extension, ".shadersubgraph", StringComparison.OrdinalIgnoreCase)
                ? "SubGraph"
                : "ShaderGraph",
            importedAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.FullName ?? string.Empty,
            shaderName = shader != null ? shader.name : string.Empty,
            fileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0L,
            sha256 = fileInfo.Exists ? CalculateFileSha256(absolutePath) : string.Empty,
            labels = GetLabels(assetPath),
            directDependencies = directDependencies.ToList()
        };

        foreach (string dependency in directDependencies)
        {
            string dependencyExtension = Path.GetExtension(dependency);
            Type dependencyType = AssetDatabase.GetMainAssetTypeAtPath(dependency);

            if (string.Equals(dependencyExtension, ".shadersubgraph", StringComparison.OrdinalIgnoreCase))
                record.subGraphDependencies.Add(dependency);
            else if (dependencyType != null && typeof(Texture).IsAssignableFrom(dependencyType))
                record.textureDependencies.Add(dependency);
            else if (IsCustomFunctionSource(dependencyExtension))
                record.customFunctionDependencies.Add(dependency);
            else
                record.otherDependencies.Add(dependency);
        }

        return record;
    }

    private static void CollectMaterialUsages(
        ShaderGraphExportReport report,
        HashSet<string> graphPathSet,
        float progressStart,
        float progressSpan)
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { SearchRoot });

        for (int index = 0; index < materialGuids.Length; index++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[index]);
            ShowProgress(
                "Shader Graph Material 사용처 수집",
                $"Material [{index + 1}/{materialGuids.Length}] {materialPath}",
                progressStart + progressSpan * SafeRatio(index, materialGuids.Length));

            try
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null)
                    continue;

                string shaderPath = AssetDatabase.GetAssetPath(material.shader);
                if (!graphPathSet.Contains(shaderPath))
                    continue;

                report.materialUsages.Add(new MaterialUsageRecord
                {
                    materialPath = materialPath,
                    guid = AssetDatabase.AssetPathToGUID(materialPath),
                    materialName = material.name,
                    shaderName = material.shader.name,
                    shaderGraphPath = shaderPath,
                    renderQueue = material.renderQueue,
                    shaderKeywords = material.shaderKeywords
                        .OrderBy(keyword => keyword, StringComparer.Ordinal)
                        .ToList()
                });
            }
            catch (Exception exception)
            {
                report.errors.Add(materialPath + ": " + exception.Message);
            }
        }
    }

    private static void CollectReverseReferences(
        ShaderGraphExportReport report,
        HashSet<string> graphPathSet,
        float progressStart,
        float progressSpan)
    {
        List<string> candidatePaths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .Where(path => !AssetDatabase.IsValidFolder(path))
            .Where(path => ReverseReferenceExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < candidatePaths.Count; index++)
        {
            string referrerPath = candidatePaths[index];
            ShowProgress(
                "Shader Graph 역참조 수집",
                $"Reference [{index + 1}/{candidatePaths.Count}] {referrerPath}",
                progressStart + progressSpan * SafeRatio(index, candidatePaths.Count));

            try
            {
                string[] dependencies = AssetDatabase.GetDependencies(referrerPath, false);
                foreach (string dependency in dependencies)
                {
                    if (string.Equals(dependency, referrerPath, StringComparison.Ordinal) ||
                        !graphPathSet.Contains(dependency))
                    {
                        continue;
                    }

                    report.reverseReferences.Add(new ReverseReferenceRecord
                    {
                        referencedGraphPath = dependency,
                        referrerAssetPath = referrerPath,
                        referrerAssetType = AssetDatabase.GetMainAssetTypeAtPath(referrerPath)?.FullName ?? string.Empty
                    });
                }
            }
            catch (Exception exception)
            {
                report.errors.Add(referrerPath + ": " + exception.Message);
            }
        }

        report.reverseReferences = report.reverseReferences
            .OrderBy(record => record.referencedGraphPath, StringComparer.Ordinal)
            .ThenBy(record => record.referrerAssetPath, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildTextBundle(string projectRoot, ShaderGraphExportReport report)
    {
        HashSet<string> textPaths = new(StringComparer.Ordinal);

        foreach (ShaderGraphRecord graph in report.graphs)
        {
            textPaths.Add(graph.assetPath);

            foreach (string dependency in graph.directDependencies)
            {
                if (SourceExtensions.Contains(Path.GetExtension(dependency)))
                    textPaths.Add(dependency);
            }
        }

        foreach (MaterialUsageRecord material in report.materialUsages)
            textPaths.Add(material.materialPath);

        List<string> orderedPaths = textPaths
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        report.summary.textAssetCount = orderedPaths.Count;

        StringBuilder output = new(64 * 1024);
        output.AppendLine("PROJECT_ABYSS_SHADER_GRAPH_TEXT_ASSET_EXPORT");
        output.AppendLine("FORMAT_VERSION: 1");
        output.AppendLine("PURPOSE: AI_SHADER_GRAPH_SOURCE_AND_DEPENDENCY_ANALYSIS");
        output.AppendLine($"GENERATED_AT_UTC: {DateTime.UtcNow:O}");
        output.AppendLine($"GENERATED_AT_LOCAL: {DateTimeOffset.Now:O}");
        output.AppendLine($"UNITY_VERSION: {Application.unityVersion}");
        output.AppendLine($"SOURCE_ROOT: {SearchRoot}");
        output.AppendLine($"TEXT_ASSET_COUNT: {orderedPaths.Count}");
        output.AppendLine();

        for (int index = 0; index < orderedPaths.Count; index++)
        {
            string assetPath = orderedPaths[index];
            ShowProgress(
                "Shader Graph 텍스트 번들 생성",
                $"Text [{index + 1}/{orderedPaths.Count}] {assetPath}",
                0.93f + 0.07f * SafeRatio(index, orderedPaths.Count));

            output.AppendLine("================================================================================");
            output.AppendLine("FILE_BEGIN");
            output.AppendLine($"FILE_INDEX: {index + 1}/{orderedPaths.Count}");
            output.AppendLine($"FILE_PATH: {assetPath}");
            output.AppendLine($"FILE_GUID: {AssetDatabase.AssetPathToGUID(assetPath)}");
            output.AppendLine($"FILE_TYPE: {AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.FullName ?? string.Empty}");

            string absolutePath = ToAbsolutePath(projectRoot, assetPath);

            try
            {
                FileInfo fileInfo = new(absolutePath);
                output.AppendLine($"FILE_SIZE_BYTES: {fileInfo.Length}");
                output.AppendLine($"SHA256: {CalculateFileSha256(absolutePath)}");

                if (fileInfo.Length > MaximumTextAssetBytes)
                {
                    output.AppendLine("READ_STATUS: SKIPPED_TOO_LARGE");
                    output.AppendLine($"MAXIMUM_TEXT_ASSET_BYTES: {MaximumTextAssetBytes}");
                }
                else
                {
                    byte[] bytes = File.ReadAllBytes(absolutePath);
                    if (ContainsNullByte(bytes))
                    {
                        output.AppendLine("READ_STATUS: SKIPPED_BINARY");
                    }
                    else
                    {
                        output.AppendLine("READ_STATUS: OK");
                        output.AppendLine("CONTENT_BEGIN");
                        output.Append(NormalizeNewlines(DecodeText(bytes)));
                        if (output.Length > 0 && output[output.Length - 1] != '\n')
                            output.AppendLine();
                        output.AppendLine("CONTENT_END");
                    }
                }
            }
            catch (Exception exception)
            {
                output.AppendLine("READ_STATUS: ERROR");
                output.AppendLine("READ_ERROR: " + SanitizeSingleLine(exception.Message));
                report.errors.Add(assetPath + ": " + exception.Message);
                report.summary.errorCount = report.errors.Count;
            }

            output.AppendLine("FILE_END");
        }

        output.AppendLine("================================================================================");
        return output.ToString();
    }

    private static List<string> CollectGraphPaths()
    {
        return AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .Where(path => GraphExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetLabels(string assetPath)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        return asset == null
            ? new List<string>()
            : AssetDatabase.GetLabels(asset)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToList();
    }

    private static bool IsCustomFunctionSource(string extension)
    {
        return string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cginc", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".compute", StringComparison.OrdinalIgnoreCase);
    }

    private static void ShowProgress(string title, string info, float progress)
    {
        if (EditorUtility.DisplayCancelableProgressBar(title, info, Mathf.Clamp01(progress)))
            throw new OperationCanceledException();
    }

    private static float SafeRatio(int index, int count)
    {
        return count <= 0 ? 1f : (float)index / count;
    }

    private static string ToAbsolutePath(string projectRoot, string assetPath)
    {
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void WriteAtomic(string temporaryPath, string finalPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? string.Empty);
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
        DeleteFileIfExists(finalPath);
        File.Move(temporaryPath, finalPath);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);
    }

    private static string CalculateFileSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool ContainsNullByte(byte[] bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == 0)
                return true;
        }

        return false;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        return new UTF8Encoding(false, false).GetString(bytes);
    }

    private static string NormalizeNewlines(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static string SanitizeSingleLine(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r", " ").Replace("\n", " ");
    }

    [Serializable]
    private sealed class ShaderGraphExportReport
    {
        public string formatVersion;
        public string purpose;
        public string generatedAtUtc;
        public string generatedAtLocal;
        public string unityVersion;
        public string sourceRoot;
        public ShaderGraphExportSummary summary = new();
        public List<string> catalogNotes = new();
        public List<ShaderGraphRecord> graphs = new();
        public List<MaterialUsageRecord> materialUsages = new();
        public List<ReverseReferenceRecord> reverseReferences = new();
        public List<string> errors = new();
    }

    [Serializable]
    private sealed class ShaderGraphExportSummary
    {
        public int graphCount;
        public int shaderGraphCount;
        public int subGraphCount;
        public int materialUsageCount;
        public int reverseReferenceCount;
        public int textAssetCount;
        public int errorCount;
    }

    [Serializable]
    private sealed class ShaderGraphRecord
    {
        public string assetPath;
        public string guid;
        public string fileName;
        public string graphKind;
        public string importedAssetType;
        public string shaderName;
        public long fileSizeBytes;
        public string sha256;
        public List<string> labels = new();
        public List<string> directDependencies = new();
        public List<string> subGraphDependencies = new();
        public List<string> textureDependencies = new();
        public List<string> customFunctionDependencies = new();
        public List<string> otherDependencies = new();
        public List<string> materialUsers = new();
    }

    [Serializable]
    private sealed class MaterialUsageRecord
    {
        public string materialPath;
        public string guid;
        public string materialName;
        public string shaderName;
        public string shaderGraphPath;
        public int renderQueue;
        public List<string> shaderKeywords = new();
    }

    [Serializable]
    private sealed class ReverseReferenceRecord
    {
        public string referencedGraphPath;
        public string referrerAssetPath;
        public string referrerAssetType;
    }
}

#endif
