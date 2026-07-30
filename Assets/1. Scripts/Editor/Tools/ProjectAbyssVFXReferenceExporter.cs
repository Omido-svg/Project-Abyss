#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public static class ProjectAbyssVFXReferenceExporter
{
    private const string ExportMenuPath =
        "Tools/Project Abyss/Exports/Export Selected VFX as AI Reference";
    private const string ValidateMenuPath =
        "Tools/Project Abyss/Exports/Validate Selected VFX AI Reference Capture";
    private const long MaximumSingleDependencyBytes = 128L * 1024L * 1024L;
    private const long MaximumTotalDependencyBytes = 1024L * 1024L * 1024L;

    [MenuItem(ExportMenuPath, false, 50)]
    public static void ExportSelected()
    {
        List<VisualEffectAsset> assets = GetSelectedAssets();
        if (assets.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "VFX AI Reference Export",
                "Project 창에서 하나 이상의 .vfx 에셋을 선택하세요.",
                "확인");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string root = Path.Combine(projectRoot, "AllDataTXT", "VFX", "References");
        Directory.CreateDirectory(root);

        List<string> packages = new();
        List<string> summaries = new();

        try
        {
            for (int i = 0; i < assets.Count; i++)
            {
                VisualEffectAsset asset = assets[i];
                EditorUtility.DisplayProgressBar(
                    "VFX AI Reference Export 3.1",
                    asset.name,
                    assets.Count == 0 ? 1f : (float)i / assets.Count);

                ExportResult result = ExportOne(asset, root);
                packages.Add(result.zipPath);
                summaries.Add(
                    $"{asset.name}: {result.status}, " +
                    $"Nodes={result.nodeCount}, Edges={result.edgeCount}, " +
                    $"Systems={result.systemCount}, Dependencies={result.dependencyCount}, " +
                    $"Preview={(result.previewRendered ? "Rendered" : "Fallback/Unavailable")}");
            }

            string message =
                $"{packages.Count}개의 VFX AI Reference Package를 생성했습니다.\n\n" +
                string.Join("\n", summaries) + "\n\n" +
                string.Join("\n", packages.Select(Normalize));

            Debug.Log("[ProjectAbyssVFXReferenceExporter 3.1]\n" + message);
            EditorUtility.DisplayDialog("VFX AI Reference Export Complete", message, "확인");

            if (packages.Count > 0)
                EditorUtility.RevealInFinder(packages[0]);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "VFX AI Reference Export Failed",
                exception.Message,
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(ExportMenuPath, true)]
    private static bool ValidateExportMenu() => GetSelectedAssets().Count > 0;

    [MenuItem(ValidateMenuPath, false, 51)]
    public static void ValidateSelectedCapture()
    {
        List<VisualEffectAsset> assets = GetSelectedAssets();
        if (assets.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "VFX AI Reference Validation",
                "Project 창에서 하나 이상의 .vfx 에셋을 선택하세요.",
                "확인");
            return;
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ProjectAbyssVFXReferenceValidation",
            DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(tempRoot);

        StringBuilder summary = new();
        try
        {
            foreach (VisualEffectAsset asset in assets)
            {
                string folder = Path.Combine(tempRoot, Sanitize(asset.name));
                Directory.CreateDirectory(folder);
                VfxAnalysisResult analysis =
                    ProjectAbyssVFXReferenceAnalyzer.Analyze(asset, folder);
                VfxAiReferenceReport report = analysis.report;
                ProjectAbyssVFXReferencePreviewRenderer.Render(asset, report, folder);
                FinalizeQualityAfterOutput(report);
                summary.AppendLine(
                    $"{asset.name}: {report.quality.status} | " +
                    $"YAML={report.capture.yamlDocumentCount}, " +
                    $"Nodes={report.graph.nodes.Count}, " +
                    $"Edges={report.graph.edges.Count}, " +
                    $"Systems={report.graph.systems.Count}, " +
                    $"Deps={report.dependencies.Count}, " +
                    $"CoreProps={report.capture.coreSerializedPropertyCount}, " +
                    $"CompiledArtifacts={report.capture.compiledArtifactCount}, " +
                    $"Preview={(report.preview.rendered ? "PASS" : "FAIL")}, " +
                    $"DistinctFrames={report.preview.distinctFrameCount}, " +
                    $"Coverage={report.quality.graphObjectCoverage:P1}");

                foreach (string warning in report.warnings.Take(8))
                    summary.AppendLine("  WARN: " + warning);
                foreach (string error in report.errors.Take(8))
                    summary.AppendLine("  ERROR: " + error);
            }

            Debug.Log("=== VFX AI REFERENCE CAPTURE VALIDATION ===\n" + summary);
            EditorUtility.DisplayDialog(
                "VFX AI Reference Validation",
                summary.ToString(),
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "VFX AI Reference Validation Failed",
                exception.Message,
                "확인");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [MenuItem(ValidateMenuPath, true)]
    private static bool ValidateValidationMenu() => GetSelectedAssets().Count > 0;

    private static ExportResult ExportOne(
        VisualEffectAsset asset,
        string root)
    {
        string safeName = Sanitize(asset.name);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(root, $"{safeName}_{stamp}");
        Directory.CreateDirectory(folder);

        VfxAnalysisResult analysis =
            ProjectAbyssVFXReferenceAnalyzer.Analyze(asset, folder);
        VfxAiReferenceReport report = analysis.report;

        WriteRawSource(analysis, safeName, folder);
        CopyDependencies(report, folder);
        ProjectAbyssVFXReferencePreviewRenderer.Render(asset, report, folder);
        FinalizeQualityAfterOutput(report);

        WriteStructuredOutputs(report, folder);
        WritePackageManifest(report, folder);

        string zipPath = folder + ".zip";
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(
            folder,
            zipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);

        return new ExportResult
        {
            zipPath = zipPath,
            status = report.quality.status,
            nodeCount = report.graph.nodes.Count,
            edgeCount = report.graph.edges.Count,
            systemCount = report.graph.systems.Count,
            dependencyCount = report.dependencies.Count,
            previewRendered = report.preview.rendered
        };
    }

    private static void WriteRawSource(
        VfxAnalysisResult analysis,
        string safeName,
        string folder)
    {
        string rawFolder = Path.Combine(folder, "RAW");
        Directory.CreateDirectory(rawFolder);

        if (!string.IsNullOrWhiteSpace(analysis.rawYaml))
        {
            File.WriteAllText(
                Path.Combine(rawFolder, safeName + ".vfx.txt"),
                analysis.rawYaml,
                new UTF8Encoding(false));
        }

        string source = analysis.sourceAssetPhysicalPath;
        if (!string.IsNullOrWhiteSpace(source) && File.Exists(source + ".meta"))
        {
            File.Copy(
                source + ".meta",
                Path.Combine(rawFolder, safeName + ".vfx.meta.txt"),
                true);
        }
    }

    private static void CopyDependencies(
        VfxAiReferenceReport report,
        string outputFolder)
    {
        string dependencyRoot = Path.Combine(outputFolder, "DEPENDENCIES");
        string previewRoot = Path.Combine(outputFolder, "DEPENDENCY_PREVIEWS");
        Directory.CreateDirectory(dependencyRoot);
        Directory.CreateDirectory(previewRoot);

        long totalCopied = 0;
        int copiedCount = 0;

        foreach (VfxDependencyRecord dependency in report.dependencies)
        {
            if (dependency.isBuiltInOrUnresolved ||
                string.IsNullOrWhiteSpace(dependency.assetPath))
            {
                dependency.copySkipReason = "Built-in or unresolved GUID.";
                continue;
            }

            string extension = dependency.extension?.ToLowerInvariant() ?? string.Empty;
            if (extension == ".dll" || extension == ".pdb")
            {
                dependency.copySkipReason = "Compiled binary dependency is not copied.";
                continue;
            }

            if (dependency.isPackageAsset && extension == ".cs")
            {
                dependency.copySkipReason =
                    "Unity package implementation script is represented by type/path metadata only.";
                continue;
            }

            string physicalPath = dependency.physicalPath;
            if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath))
            {
                dependency.copySkipReason = "Physical dependency file was not found.";
                continue;
            }

            FileInfo sourceInfo = new(physicalPath);
            if (sourceInfo.Length > MaximumSingleDependencyBytes)
            {
                dependency.copySkipReason =
                    $"File exceeds per-file copy limit ({MaximumSingleDependencyBytes / (1024 * 1024)} MiB).";
                continue;
            }

            if (totalCopied + sourceInfo.Length > MaximumTotalDependencyBytes)
            {
                dependency.copySkipReason =
                    $"Package dependency copy limit reached ({MaximumTotalDependencyBytes / (1024 * 1024)} MiB).";
                continue;
            }

            string relative = dependency.assetPath
                .Replace(':', '_')
                .TrimStart('/', '\\');
            string destination = Path.Combine(dependencyRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? dependencyRoot);
            File.Copy(physicalPath, destination, true);

            string metaSource = physicalPath + ".meta";
            if (File.Exists(metaSource))
                File.Copy(metaSource, destination + ".meta", true);

            dependency.copied = true;
            dependency.copiedRelativePath = Normalize(
                Path.Combine("DEPENDENCIES", relative));
            totalCopied += sourceInfo.Length;
            copiedCount++;

            WriteDependencyPreview(dependency, previewRoot);
        }

        report.capture.copiedDependencyCount = copiedCount;
    }

    private static void WriteDependencyPreview(
        VfxDependencyRecord dependency,
        string previewRoot)
    {
        UnityEngine.Object asset =
            AssetDatabase.LoadMainAssetAtPath(dependency.assetPath);
        if (asset == null)
            return;

        Texture2D preview = AssetPreview.GetAssetPreview(asset) ??
                            AssetPreview.GetMiniThumbnail(asset);
        if (preview == null)
            return;

        string safeName = Sanitize(
            Path.GetFileNameWithoutExtension(dependency.assetPath));
        string prefix = string.IsNullOrWhiteSpace(dependency.guid)
            ? "unknown"
            : dependency.guid.Substring(0, Mathf.Min(8, dependency.guid.Length));
        string outputPath = Path.Combine(previewRoot, prefix + "_" + safeName + ".png");

        RenderTexture temporary = null;
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;
        try
        {
            int width = Mathf.Max(64, preview.width);
            int height = Mathf.Max(64, preview.height);
            temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(preview, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();
            File.WriteAllBytes(outputPath, readable.EncodeToPNG());
        }
        catch
        {
            // Dependency preview is optional; source and metadata remain available.
        }
        finally
        {
            RenderTexture.active = previous;
            if (temporary != null)
                RenderTexture.ReleaseTemporary(temporary);
            if (readable != null)
                UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    private static void FinalizeQualityAfterOutput(VfxAiReferenceReport report)
    {
        report.capture.renderedPreviewSucceeded = report.preview.rendered;
        report.capture.dependencyCount = report.dependencies.Count;
        report.capture.copiedDependencyCount = report.dependencies.Count(item => item.copied);

        if (report.preview.rendered)
        {
            report.quality.checks.Add(
                $"PASS: Isolated-layer preview verified ({report.preview.distinctFrameCount} distinct frames, " +
                $"max delta={report.preview.maxInterFrameDifference:R}).");
        }
        else if (report.preview.usedFallbackAssetIcon)
        {
            report.quality.checks.Add(
                "WARN: Runtime preview did not pass isolation/temporal validation; an asset icon fallback was included. " +
                report.preview.validationReason);
        }
        else
        {
            report.quality.checks.Add(
                "WARN: No validated preview image could be produced. " +
                report.preview.validationReason);
        }

        report.quality.checks.Add(
            $"INFO: Copied {report.capture.copiedDependencyCount}/{report.dependencies.Count} dependencies; " +
            "all dependencies remain represented in metadata even when not copied.");

        if (report.quality.status == "READY" && report.errors.Count > 0)
            report.quality.status = "PARTIAL";
    }

    private static void WriteStructuredOutputs(
        VfxAiReferenceReport report,
        string folder)
    {
        UTF8Encoding utf8 = new(false);

        File.WriteAllText(
            Path.Combine(folder, "VFX_REFERENCE.json"),
            JsonUtility.ToJson(report, true),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "VFX_REFERENCE.txt"),
            BuildReadableText(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "GRAPH_SUMMARY.md"),
            BuildGraphMarkdown(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "GRAPH_NODES.csv"),
            BuildNodesCsv(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "GRAPH_EDGES.csv"),
            BuildEdgesCsv(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "SERIALIZED_PROPERTIES.csv"),
            BuildPropertiesCsv(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "GRAPH.mmd"),
            BuildMermaid(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "GRAPH.dot"),
            BuildDot(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "CAPABILITY_MATRIX.md"),
            BuildCapabilityMarkdown(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "DEPENDENCY_MANIFEST.json"),
            JsonUtility.ToJson(new DependencyManifestWrapper
            {
                dependencies = report.dependencies
            }, true),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "COMPILED_ARTIFACTS.json"),
            JsonUtility.ToJson(new CompiledArtifactManifestWrapper
            {
                artifacts = report.compiledArtifacts
            }, true),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "CLONE_TEMPLATE_RECIPE.json"),
            JsonUtility.ToJson(CreateCloneRecipe(report), true),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "AI_REFERENCE_PROMPT.txt"),
            BuildPrompt(report),
            utf8);
        File.WriteAllText(
            Path.Combine(folder, "README_KO.md"),
            BuildReadme(report),
            utf8);
    }

    private static VfxCloneTemplateRecipe CreateCloneRecipe(
        VfxAiReferenceReport report)
    {
        VfxCloneTemplateRecipe recipe = new()
        {
            name = Sanitize(report.assetName) + "_Variant",
            description =
                "AI Reference Export 3.1에서 분석한 원본 VFX를 그래프 손실 없이 복제하여 변형",
            templateAssetPath = report.assetPath,
            templateGuid = report.assetGuid,
            requiredCapabilities = report.capabilities
                .Select(capability => capability.name)
                .ToList()
        };

        foreach (VfxExposedPropertyRecord property in report.publicApi.exposedProperties)
        {
            recipe.exposedOverrides.Add(new VfxCloneOverridePlaceholder
            {
                propertyName = property.name,
                propertyType = property.type,
                value = "<keep-template-default-or-set-project-asset-path/value>"
            });
        }

        return recipe;
    }

    private static string BuildReadableText(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("PROJECT ABYSS VFX AI REFERENCE 3.1");
        builder.AppendLine($"Status: {report.quality.status}");
        builder.AppendLine($"Asset: {report.assetPath}");
        builder.AppendLine($"GUID: {report.assetGuid}");
        builder.AppendLine($"Unity: {report.unityVersion}");
        builder.AppendLine($"VFX Graph Package: {report.visualEffectGraphPackageVersion}");
        builder.AppendLine($"YAML Documents: {report.capture.yamlDocumentCount}");
        builder.AppendLine($"Graph Nodes: {report.graph.nodes.Count}");
        builder.AppendLine($"Graph Edges: {report.graph.edges.Count}");
        builder.AppendLine($"Systems: {report.graph.systems.Count}");
        builder.AppendLine($"Dependencies: {report.dependencies.Count}");
        builder.AppendLine($"Graph Coverage: {report.quality.graphObjectCoverage:P1}");
        builder.AppendLine($"Preview Rendered: {report.preview.rendered}");
        builder.AppendLine($"Preview Distinct Frames: {report.preview.distinctFrameCount}");
        builder.AppendLine($"Core Serialized Properties: {report.capture.coreSerializedPropertyCount}");
        builder.AppendLine($"Compiled Artifacts Excluded: {report.capture.compiledArtifactCount}");
        builder.AppendLine();

        builder.AppendLine("CAPABILITIES");
        foreach (VfxCapabilityRecord capability in report.capabilities)
            builder.AppendLine($"- {capability.name} [{capability.confidence}]");
        builder.AppendLine();

        builder.AppendLine("SYSTEMS");
        foreach (VfxSystemRecord system in report.graph.systems)
        {
            builder.AppendLine(
                $"- {system.name}: {string.Join(" -> ", system.stages)} | " +
                $"capacity={system.capacity}, contexts={system.contextLocalIds.Count}, " +
                $"blocks={system.blockLocalIds.Count}, operators={system.operatorLocalIds.Count}");
        }
        builder.AppendLine();

        builder.AppendLine("QUALITY CHECKS");
        foreach (string check in report.quality.checks)
            builder.AppendLine("- " + check);

        if (report.warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("WARNINGS");
            foreach (string warning in report.warnings)
                builder.AppendLine("- " + warning);
        }

        if (report.errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("ERRORS");
            foreach (string error in report.errors)
                builder.AppendLine("- " + error);
        }

        return builder.ToString();
    }

    private static string BuildGraphMarkdown(VfxAiReferenceReport report)
    {
        Dictionary<long, VfxGraphNodeRecord> nodes = report.graph.nodes
            .ToDictionary(node => node.localId);
        StringBuilder builder = new();
        builder.AppendLine($"# {report.assetName} — VFX Graph Structure");
        builder.AppendLine();
        builder.AppendLine($"- Status: **{report.quality.status}**");
        builder.AppendLine($"- Graph object coverage: **{report.quality.graphObjectCoverage:P1}**");
        builder.AppendLine($"- Nodes / Edges: **{report.graph.nodes.Count} / {report.graph.edges.Count}**");
        builder.AppendLine($"- Systems: **{report.graph.systems.Count}**");
        builder.AppendLine();

        foreach (VfxSystemRecord system in report.graph.systems)
        {
            builder.AppendLine($"## System: {EscapeMarkdown(system.name)}");
            builder.AppendLine();
            builder.AppendLine($"- Flow: `{string.Join(" → ", system.stages)}`");
            builder.AppendLine($"- Particle data node: `{system.dataNodeLocalId}` / `{system.dataType}`");
            if (system.spawnDataNodeLocalId != 0)
                builder.AppendLine($"- Spawn data node: `{system.spawnDataNodeLocalId}`");
            builder.AppendLine($"- Capacity: `{system.capacity}`");
            if (system.detectedCapabilities.Count > 0)
                builder.AppendLine($"- Capabilities: {string.Join(", ", system.detectedCapabilities.Select(value => "`" + value + "`"))}");
            builder.AppendLine();
            builder.AppendLine("| Stage | Local ID | Node | Type |");
            builder.AppendLine("|---|---:|---|---|");
            foreach (long id in system.contextLocalIds)
            {
                if (!nodes.TryGetValue(id, out VfxGraphNodeRecord node))
                    continue;
                builder.AppendLine(
                    $"| {node.stage} | {id} | {EscapeMarkdown(node.displayName)} | `{EscapeMarkdown(FirstType(node))}` |");
            }
            builder.AppendLine();

            if (system.importantSettings.Count > 0)
            {
                builder.AppendLine("### Important settings");
                foreach (VfxKeyValueRecord setting in system.importantSettings.Take(40))
                    builder.AppendLine($"- `{setting.key}` = `{EscapeMarkdown(setting.value)}`");
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Unassigned / Supporting Nodes");
        builder.AppendLine();
        foreach (IGrouping<string, VfxGraphNodeRecord> group in report.graph.nodes
                     .GroupBy(node => node.category)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"### {group.Key} ({group.Count()})");
            foreach (VfxGraphNodeRecord node in group.Take(200))
                builder.AppendLine($"- `{node.localId}` {EscapeMarkdown(node.displayName)} — `{EscapeMarkdown(FirstType(node))}`");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildNodesCsv(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("localId,category,stage,displayName,runtimeType,scriptType,parentLocalId,positionX,positionY,yamlStartLine,yamlEndLine");
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(node.localId.ToString(CultureInfo.InvariantCulture)),
                Csv(node.category),
                Csv(node.stage),
                Csv(node.displayName),
                Csv(node.runtimeType),
                Csv(node.scriptType),
                Csv(node.parentLocalId.ToString(CultureInfo.InvariantCulture)),
                Csv(node.positionX.ToString("R", CultureInfo.InvariantCulture)),
                Csv(node.positionY.ToString("R", CultureInfo.InvariantCulture)),
                Csv(node.yamlStartLine.ToString(CultureInfo.InvariantCulture)),
                Csv(node.yamlEndLine.ToString(CultureInfo.InvariantCulture))
            }));
        }
        return builder.ToString();
    }

    private static string BuildEdgesCsv(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("sourceLocalId,targetLocalId,kind,propertyPath,sourceLayer");
        foreach (VfxGraphEdgeRecord edge in report.graph.edges)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(edge.sourceLocalId.ToString(CultureInfo.InvariantCulture)),
                Csv(edge.targetLocalId.ToString(CultureInfo.InvariantCulture)),
                Csv(edge.kind),
                Csv(edge.propertyPath),
                Csv(edge.sourceLayer)
            }));
        }
        return builder.ToString();
    }

    private static string BuildPropertiesCsv(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine(
            "nodeLocalId,nodeName,path,serializedType,value,depth,isArray,arraySize,valueTruncated,originalLength,valueSha256");
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            foreach (VfxSerializedPropertyRecord property in node.properties)
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    Csv(node.localId.ToString(CultureInfo.InvariantCulture)),
                    Csv(node.displayName),
                    Csv(property.path),
                    Csv(property.serializedType),
                    Csv(property.value),
                    Csv(property.depth.ToString(CultureInfo.InvariantCulture)),
                    Csv(property.isArray ? "true" : "false"),
                    Csv(property.arraySize.ToString(CultureInfo.InvariantCulture)),
                    Csv(property.valueTruncated ? "true" : "false"),
                    Csv(property.originalLength.ToString(CultureInfo.InvariantCulture)),
                    Csv(property.valueSha256)
                }));
            }
        }
        return builder.ToString();
    }

    private static string BuildMermaid(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("flowchart LR");
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            string shape = node.category == "Context"
                ? $"[{MermaidText(node.displayName + "\\n" + node.stage)}]"
                : $"({MermaidText(node.displayName)})";
            builder.AppendLine($"  N{SafeId(node.localId)}{shape}");
        }
        foreach (VfxGraphEdgeRecord edge in report.graph.edges)
        {
            builder.AppendLine(
                $"  N{SafeId(edge.sourceLocalId)} -->|{MermaidText(edge.kind)}| N{SafeId(edge.targetLocalId)}");
        }
        return builder.ToString();
    }

    private static string BuildDot(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("digraph VFXGraph {");
        builder.AppendLine("  rankdir=LR;");
        builder.AppendLine("  node [shape=box, fontname=\"Arial\"];");
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            string label = DotEscape(node.displayName + "\\n" + node.category +
                                     (string.IsNullOrWhiteSpace(node.stage) ? string.Empty : " / " + node.stage));
            builder.AppendLine($"  N{SafeId(node.localId)} [label=\"{label}\"];");
        }
        foreach (VfxGraphEdgeRecord edge in report.graph.edges)
        {
            builder.AppendLine(
                $"  N{SafeId(edge.sourceLocalId)} -> N{SafeId(edge.targetLocalId)} " +
                $"[label=\"{DotEscape(edge.kind)}\"];");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildCapabilityMarkdown(VfxAiReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Detected VFX Capabilities");
        builder.AppendLine();
        builder.AppendLine("Capability detection is based on actual node types and enabled serialized settings. Compiled Shader text is never used as feature evidence. Unknown or future VFX Graph features remain preserved in raw YAML, per-object YAML, compact properties, and graph edges.");
        builder.AppendLine();
        foreach (VfxCapabilityRecord capability in report.capabilities)
        {
            builder.AppendLine($"## {capability.name} — {capability.confidence}");
            foreach (string evidence in capability.evidence)
                builder.AppendLine("- " + EscapeMarkdown(evidence));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string BuildPrompt(VfxAiReferenceReport report)
    {
        return
            "이 ZIP은 Project Abyss VFX AI Reference Export 3.1 패키지다.\n" +
            "반드시 VFX_REFERENCE.json, GRAPH_SUMMARY.md, GRAPH_NODES.csv, GRAPH_EDGES.csv, " +
            "SERIALIZED_PROPERTIES.csv, YAML_OBJECTS, RAW 원본, DEPENDENCY_MANIFEST.json, COMPILED_ARTIFACTS.json과 PREVIEW 폴더를 함께 분석하라.\n" +
            "SERIALIZED_PROPERTIES.csv는 AI 분석용 핵심 프로퍼티 스트림이며 긴 값은 해시와 함께 제한될 수 있다. 손실 없는 복원은 RAW/YAML_OBJECTS를 사용하라.\n" +
            "Capability는 실제 노드 타입과 활성 설정만 근거로 하며 컴파일 Shader 문자열 검색 결과가 아니다.\n" +
            "Context 흐름, Data/Capacity/Bounds, Block, Operator, Slot 연결, Output, Shader Graph, Texture/Mesh/Subgraph 의존성, 시간 흐름을 설명하라.\n" +
            "변형 제작 시 CloneTemplate을 기본으로 사용하고 GPU Event, Strip, Collision, Decal, SDF, Mesh, Shader Graph, Flipbook, Custom HLSL 등 기존 구조를 임의로 삭제하지 말라.\n\n" +
            $"Reference Asset: {report.assetPath}\n" +
            $"Export Status: {report.quality.status}\n" +
            $"Graph Coverage: {report.quality.graphObjectCoverage:P1}\n" +
            $"Detected Capabilities: {string.Join(", ", report.capabilities.Select(value => value.name))}\n";
    }

    private static string BuildReadme(VfxAiReferenceReport report)
    {
        return
            $"# Project Abyss VFX AI Reference 3.1\n\n" +
            $"- Asset: `{report.assetPath}`\n" +
            $"- Status: **{report.quality.status}**\n" +
            $"- YAML object coverage: **{report.quality.graphObjectCoverage:P1}**\n" +
            $"- Nodes / Edges / Systems: **{report.graph.nodes.Count} / {report.graph.edges.Count} / {report.graph.systems.Count}**\n" +
            $"- Dependencies: **{report.dependencies.Count}**\n" +
            $"- Rendered preview: **{report.preview.rendered}**\n\n" +
            "## 분석 우선순위\n\n" +
            "1. `VFX_REFERENCE.json`: 전체 구조화 데이터\n" +
            "2. `GRAPH_SUMMARY.md`: 시스템별 읽기 쉬운 요약\n" +
            "3. `GRAPH_NODES.csv`, `GRAPH_EDGES.csv`: 그래프 관계\n" +
            "4. `SERIALIZED_PROPERTIES.csv`: 컴파일 산출물을 제외한 AI 분석용 핵심 직렬화 프로퍼티\n" +
            "5. `YAML_OBJECTS/`: Unity 로컬 오브젝트별 원문\n" +
            "6. `RAW/`: 손실 없는 원본 `.vfx` YAML과 `.meta`\n" +
            "7. `DEPENDENCIES/`: Shader Graph, Texture, Mesh, Material, Subgraph 등 복사 가능한 의존 에셋\n" +
            "8. `PREVIEW/`: 전용 Layer로 격리하고 프레임 변화/생존 파티클을 검증한 시간별 렌더\n" +
            "9. `COMPILED_ARTIFACTS.json`: 핵심 JSON에서 제외된 생성 Shader/ComputeShader 메타데이터\n\n" +
            "내부 VFX Graph API에 직접 의존하지 않는다. Unity가 내부 타입의 접근 수준이나 구현을 변경하더라도 " +
            "Loaded Sub-Asset + SerializedObject + YAML fallback의 세 계층으로 구조를 보존한다. 생성 Shader/ComputeShader의 방대한 직렬화 배열은 핵심 데이터에서 제외하고 메타데이터만 남긴다.\n";
    }

    private static void WritePackageManifest(
        VfxAiReferenceReport report,
        string folder)
    {
        VfxPackageManifest manifest = new()
        {
            generatedAtLocal = DateTimeOffset.Now.ToString("O"),
            sourceAssetPath = report.assetPath,
            packageStatus = report.quality.status
        };

        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                     .Where(path => !path.EndsWith("PACKAGE_MANIFEST.json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            FileInfo info = new(file);
            manifest.files.Add(new VfxPackageFileRecord
            {
                relativePath = Normalize(GetRelativePath(folder, file)),
                byteSize = info.Length,
                sha256 = ProjectAbyssVFXReferenceAnalyzer.ComputeSha256(file)
            });
        }

        File.WriteAllText(
            Path.Combine(folder, "PACKAGE_MANIFEST.json"),
            JsonUtility.ToJson(manifest, true),
            new UTF8Encoding(false));
    }


    private static string GetRelativePath(string basePath, string fullPath)
    {
        Uri baseUri = new(AppendDirectorySeparator(Path.GetFullPath(basePath)));
        Uri fileUri = new(Path.GetFullPath(fullPath));
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Path.DirectorySeparatorChar.ToString();
        char last = path[path.Length - 1];
        return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static List<VisualEffectAsset> GetSelectedAssets()
    {
        List<VisualEffectAsset> assets = Selection.objects
            .OfType<VisualEffectAsset>()
            .Where(asset => asset != null)
            .Distinct()
            .ToList();

        if (assets.Count == 0 && Selection.activeObject is VisualEffectAsset active)
            assets.Add(active);

        return assets;
    }

    private static string FirstType(VfxGraphNodeRecord node) =>
        !string.IsNullOrWhiteSpace(node.runtimeType)
            ? node.runtimeType
            : !string.IsNullOrWhiteSpace(node.scriptType)
                ? node.scriptType
                : node.unityClassName;

    private static string Csv(string value)
    {
        string text = value ?? string.Empty;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeMarkdown(string value) =>
        (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string MermaidText(string value) =>
        (value ?? string.Empty).Replace("\"", "'").Replace("[", "(").Replace("]", ")");

    private static string DotEscape(string value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string SafeId(long value) => value < 0 ? "M" + Math.Abs(value) : value.ToString();

    private static string Sanitize(string value) =>
        ProjectAbyssVFXReferenceAnalyzer.SanitizeFileName(value);

    private static string Normalize(string value) =>
        ProjectAbyssVFXReferenceAnalyzer.NormalizePath(value);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Validation temp data can be removed by the OS later.
        }
    }

    [Serializable]
    private sealed class DependencyManifestWrapper
    {
        public List<VfxDependencyRecord> dependencies = new();
    }

    [Serializable]
    private sealed class CompiledArtifactManifestWrapper
    {
        public List<VfxCompiledArtifactRecord> artifacts = new();
    }

    private sealed class ExportResult
    {
        public string zipPath = string.Empty;
        public string status = string.Empty;
        public int nodeCount;
        public int edgeCount;
        public int systemCount;
        public int dependencyCount;
        public bool previewRendered;
    }
}
#endif
