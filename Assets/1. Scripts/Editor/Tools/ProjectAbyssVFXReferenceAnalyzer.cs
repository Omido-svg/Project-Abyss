#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.VFX;

internal static class ProjectAbyssVFXReferenceAnalyzer
{
    private static readonly Regex DocumentHeaderRegex = new(
        @"^--- !u!(?<classId>-?\d+) &(?<localId>-?\d+)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex InlineReferenceRegex = new(
        @"\{fileID:\s*(?<fileId>-?\d+)(?:,\s*guid:\s*(?<guid>[0-9a-fA-F]{32}))?(?:,\s*type:\s*(?<type>-?\d+))?\}",
        RegexOptions.Compiled);

    private static readonly Regex GuidRegex = new(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{32}(?![0-9a-fA-F])",
        RegexOptions.Compiled);

    private static readonly Regex AaBoxJsonRegex = new(
        @"""center""\s*:\s*\{\s*""x""\s*:\s*(?<cx>[-+0-9.eE]+)\s*,\s*""y""\s*:\s*(?<cy>[-+0-9.eE]+)\s*,\s*""z""\s*:\s*(?<cz>[-+0-9.eE]+)\s*\}\s*,\s*""size""\s*:\s*\{\s*""x""\s*:\s*(?<sx>[-+0-9.eE]+)\s*,\s*""y""\s*:\s*(?<sy>[-+0-9.eE]+)\s*,\s*""z""\s*:\s*(?<sz>[-+0-9.eE]+)\s*\}",
        RegexOptions.Compiled);

    private static readonly HashSet<string> BoilerplatePropertyNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
        "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags",
        "m_EditorClassIdentifier", "m_UIIgnoredErrors", "m_UICollapsed",
        "m_UISuperCollapsed", "m_Name", "m_Script"
    };

    private static readonly string[] ImportantSettingTokens =
    {
        "capacity", "strip", "particleperstrip", "bounds", "lifetime", "spawn",
        "rate", "count", "loop", "delay", "attribute", "composition", "source",
        "random", "channel", "blend", "shader", "texture", "mesh", "flipbook",
        "texindex", "pivot", "orient", "angle", "velocity", "drag", "gravity",
        "turbulence", "noise", "collision", "event", "decal", "sdf", "space",
        "prewarm", "initialevent", "output", "dataType", "normal", "softparticle"
    };

    private const int MaximumStructuredPropertyValueLength = 8192;

    private static readonly Dictionary<string, ScriptTypeInfo> ScriptTypeCache =
        new(StringComparer.OrdinalIgnoreCase);

    internal static VfxAnalysisResult Analyze(
        VisualEffectAsset asset,
        string outputFolder)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        string assetPath = AssetDatabase.GetAssetPath(asset);
        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        string physicalPath = ResolvePhysicalPath(assetPath);
        string yaml = File.Exists(physicalPath)
            ? File.ReadAllText(physicalPath, Encoding.UTF8)
            : string.Empty;

        VfxAiReferenceReport report = new()
        {
            generatedAtLocal = DateTimeOffset.Now.ToString("O"),
            unityVersion = Application.unityVersion,
            visualEffectGraphPackageVersion = ResolveVfxPackageVersion(assetPath),
            assetName = asset.name,
            assetPath = assetPath,
            assetGuid = assetGuid
        };

        report.capture.rawYamlIncluded = !string.IsNullOrEmpty(yaml);
        report.capture.rawMetaIncluded = File.Exists(physicalPath + ".meta");

        CapturePublicApi(asset, report);

        List<YamlDocument> yamlDocuments = ParseYamlDocuments(yaml);
        report.capture.yamlFallbackCaptured = yamlDocuments.Count > 0;
        report.capture.yamlDocumentCount = yamlDocuments.Count;

        Dictionary<long, UnityEngine.Object> loadedObjects =
            LoadAllObjects(assetPath, report);

        Dictionary<long, VfxGraphNodeRecord> nodeMap = new();
        CaptureLoadedObjects(
            loadedObjects,
            assetGuid,
            assetPath,
            nodeMap,
            report);

        MergeYamlDocuments(
            yamlDocuments,
            loadedObjects,
            assetGuid,
            assetPath,
            outputFolder,
            nodeMap,
            report);

        report.graph.nodes = nodeMap.Values
            .OrderBy(node => node.localId)
            .ToList();

        BuildEdges(report);
        RefineContextStages(report);
        CaptureGraphMetadata(report, yamlDocuments);
        CaptureUiMetadata(report, yaml);
        CollectDependencies(report, yaml, assetPath, assetGuid);
        DetectCapabilities(report, yaml);
        BuildSystems(report);
        ComputeQuality(report, yamlDocuments.Count);

        report.capture.graphNodeCount = report.graph.nodes.Count;
        report.capture.graphEdgeCount = report.graph.edges.Count;
        report.capture.dependencyCount = report.dependencies.Count;

        return new VfxAnalysisResult
        {
            report = report,
            sourceAssetPhysicalPath = physicalPath,
            rawYaml = yaml,
            yamlDocuments = yamlDocuments
        };
    }

    private static void CapturePublicApi(
        VisualEffectAsset asset,
        VfxAiReferenceReport report)
    {
        try
        {
            List<VFXExposedProperty> properties = new();
            asset.GetExposedProperties(properties);
            foreach (VFXExposedProperty property in properties)
            {
                report.publicApi.exposedProperties.Add(
                    new VfxExposedPropertyRecord
                    {
                        name = property.name,
                        type = property.type?.FullName ?? string.Empty
                    });
            }

            List<string> events = new();
            asset.GetEvents(events);
            report.publicApi.events = events
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();

            report.capture.publicApiCaptured = true;
        }
        catch (Exception exception)
        {
            report.warnings.Add(
                "Public VisualEffectAsset API capture failed: " + exception.Message);
        }
    }

    private static Dictionary<long, UnityEngine.Object> LoadAllObjects(
        string assetPath,
        VfxAiReferenceReport report)
    {
        Dictionary<long, UnityEngine.Object> result = new();

        try
        {
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            report.capture.loadedObjectCount = all?.Length ?? 0;
            report.capture.loadedSubAssetsCaptured = all != null && all.Length > 0;

            if (all == null)
                return result;

            foreach (UnityEngine.Object value in all)
            {
                if (value == null)
                    continue;

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        value,
                        out _,
                        out long localId))
                {
                    continue;
                }

                result[localId] = value;
            }
        }
        catch (Exception exception)
        {
            report.warnings.Add(
                "AssetDatabase.LoadAllAssetsAtPath failed: " + exception.Message);
        }

        return result;
    }

    private static void CaptureLoadedObjects(
        Dictionary<long, UnityEngine.Object> loadedObjects,
        string sourceGuid,
        string sourceAssetPath,
        Dictionary<long, VfxGraphNodeRecord> nodeMap,
        VfxAiReferenceReport report)
    {
        int serializedCount = 0;

        foreach (KeyValuePair<long, UnityEngine.Object> pair in loadedObjects)
        {
            UnityEngine.Object value = pair.Value;
            if (IsCompiledOrRuntimeArtifact(value))
            {
                report.compiledArtifacts.Add(new VfxCompiledArtifactRecord
                {
                    localId = pair.Key,
                    name = value.name ?? string.Empty,
                    runtimeType = value.GetType().FullName ?? value.GetType().Name,
                    reason = value is VisualEffectAsset
                        ? "VisualEffectAsset runtime/compiled representation; graph source is preserved through YAML and public API."
                        : "Generated Shader/ComputeShader sub-asset; excluded from the core graph property stream to prevent compiled-code bloat."
                });
                continue;
            }

            VfxGraphNodeRecord node = new()
            {
                localId = pair.Key,
                runtimeType = value.GetType().FullName ?? value.GetType().Name,
                objectName = value.name ?? string.Empty
            };

            TryResolveMonoScript(value, node);

            try
            {
                SerializedObject serializedObject = new(value);
                SerializedProperty iterator = serializedObject.GetIterator();

                while (iterator.Next(true))
                {
                    SerializedProperty property = iterator.Copy();
                    VfxSerializedPropertyRecord record = CaptureSerializedProperty(property);
                    node.properties.Add(record);
                    report.capture.coreSerializedPropertyCount++;
                    if (record.valueTruncated)
                        report.capture.omittedSerializedPropertyCount++;

                    if (property.propertyType == SerializedPropertyType.ObjectReference ||
                        property.propertyType == SerializedPropertyType.ExposedReference)
                    {
                        UnityEngine.Object target = property.propertyType ==
                                                    SerializedPropertyType.ObjectReference
                            ? property.objectReferenceValue
                            : property.exposedReferenceValue;

                        if (target != null)
                        {
                            node.references.Add(
                                CreateObjectReference(
                                    property.propertyPath,
                                    target,
                                    sourceGuid,
                                    sourceAssetPath,
                                    "SerializedObject"));
                        }
                    }

                    ApplyKnownProperty(node, property, record.value);
                }

                serializedCount++;
            }
            catch (Exception exception)
            {
                report.warnings.Add(
                    $"SerializedObject capture failed for localId {pair.Key} " +
                    $"({node.runtimeType}): {exception.Message}");
            }

            ClassifyNode(node);
            RefreshImportantSettings(node);
            nodeMap[node.localId] = node;
        }

        report.capture.compiledArtifactCount = report.compiledArtifacts.Count;
        report.capture.serializedObjectCount = serializedCount;
        report.capture.serializedObjectCaptured = serializedCount > 0;
    }

    private static bool IsCompiledOrRuntimeArtifact(UnityEngine.Object value)
    {
        return value is VisualEffectAsset || value is Shader || value is ComputeShader;
    }

    private static VfxSerializedPropertyRecord CaptureSerializedProperty(
        SerializedProperty property)
    {
        VfxSerializedPropertyRecord result = new()
        {
            path = property.propertyPath,
            displayName = property.displayName,
            serializedType = property.propertyType.ToString(),
            depth = property.depth,
            isArray = property.isArray && property.propertyType != SerializedPropertyType.String,
            arraySize = property.isArray && property.propertyType != SerializedPropertyType.String
                ? property.arraySize
                : -1
        };

        try
        {
            ApplyCompactValue(result, FormatSerializedPropertyValue(property));
        }
        catch (Exception exception)
        {
            ApplyCompactValue(result, "<unreadable: " + exception.Message + ">");
        }

        return result;
    }

    private static VfxSerializedPropertyRecord CreateYamlProperty(YamlScalar scalar)
    {
        VfxSerializedPropertyRecord result = new()
        {
            path = scalar.path,
            displayName = GetLeafName(scalar.path),
            serializedType = "YAML",
            depth = scalar.depth,
            isArray = scalar.isListItem,
            arraySize = -1
        };
        ApplyCompactValue(result, scalar.value);
        return result;
    }

    private static void ApplyCompactValue(
        VfxSerializedPropertyRecord record,
        string value)
    {
        string source = value ?? string.Empty;
        record.originalLength = source.Length;
        if (source.Length <= MaximumStructuredPropertyValueLength)
        {
            record.value = source;
            return;
        }

        record.valueTruncated = true;
        record.valueSha256 = ComputeSha256Text(source);
        record.value = source.Substring(0, MaximumStructuredPropertyValueLength) +
                       $"… <truncated {source.Length - MaximumStructuredPropertyValueLength} chars; " +
                       $"sha256={record.valueSha256}>";
    }

    private static string FormatSerializedPropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return property.stringValue ?? string.Empty;
            case SerializedPropertyType.Color:
                return FormatColor(property.colorValue);
            case SerializedPropertyType.ObjectReference:
                return DescribeUnityObject(property.objectReferenceValue);
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 &&
                       property.enumValueIndex < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[property.enumValueIndex]
                    : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Vector2:
                return FormatVector2(property.vector2Value);
            case SerializedPropertyType.Vector3:
                return FormatVector3(property.vector3Value);
            case SerializedPropertyType.Vector4:
                return FormatVector4(property.vector4Value);
            case SerializedPropertyType.Rect:
                return FormatRect(property.rectValue);
            case SerializedPropertyType.ArraySize:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Character:
                return ((char)property.intValue).ToString();
            case SerializedPropertyType.AnimationCurve:
                return FormatAnimationCurve(property.animationCurveValue);
            case SerializedPropertyType.Bounds:
                return FormatBounds(property.boundsValue);
            case SerializedPropertyType.Gradient:
                return FormatGradient(property.gradientValue);
            case SerializedPropertyType.Quaternion:
                return FormatQuaternion(property.quaternionValue);
            case SerializedPropertyType.ExposedReference:
                return DescribeUnityObject(property.exposedReferenceValue);
            case SerializedPropertyType.FixedBufferSize:
                return property.fixedBufferSize.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Vector2Int:
                return property.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int:
                return property.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt:
                return property.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt:
                return property.boundsIntValue.ToString();
            case SerializedPropertyType.ManagedReference:
                return property.managedReferenceFullTypename ?? string.Empty;
            case SerializedPropertyType.Hash128:
                return property.hash128Value.ToString();
            case SerializedPropertyType.Generic:
                return property.isArray
                    ? $"Array[{property.arraySize}]"
                    : property.type ?? string.Empty;
            default:
                return property.type ?? string.Empty;
        }
    }

    private static VfxReferenceRecord CreateObjectReference(
        string propertyPath,
        UnityEngine.Object target,
        string sourceGuid,
        string sourceAssetPath,
        string sourceLayer)
    {
        VfxReferenceRecord record = new()
        {
            propertyPath = propertyPath ?? string.Empty,
            directionHint = ClassifyReferenceDirection(propertyPath),
            targetType = target.GetType().FullName ?? target.GetType().Name,
            targetName = target.name ?? string.Empty,
            sourceLayer = sourceLayer
        };

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target,
                out string guid,
                out long localId))
        {
            record.targetGuid = guid ?? string.Empty;
            record.targetLocalId = localId;
            record.targetAssetPath = AssetDatabase.GUIDToAssetPath(guid) ?? string.Empty;
            record.sameAsset = string.Equals(
                sourceGuid,
                guid,
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    sourceAssetPath,
                    record.targetAssetPath,
                    StringComparison.Ordinal);
        }

        return record;
    }

    private static void ApplyKnownProperty(
        VfxGraphNodeRecord node,
        SerializedProperty property,
        string formattedValue)
    {
        string path = property.propertyPath ?? string.Empty;
        string leaf = GetLeafName(path);

        if (leaf.Equals("m_Label", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("label", StringComparison.OrdinalIgnoreCase))
        {
            node.label = property.stringValue ?? formattedValue;
        }
        else if (leaf.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            node.title = property.stringValue ?? formattedValue;
        }
        else if (leaf.Equals("m_Name", StringComparison.OrdinalIgnoreCase))
        {
            node.objectName = property.stringValue ?? formattedValue;
        }
        else if (path.EndsWith("m_UIPosition.x", StringComparison.OrdinalIgnoreCase))
        {
            node.positionX = property.floatValue;
            node.hasPosition = true;
        }
        else if (path.EndsWith("m_UIPosition.y", StringComparison.OrdinalIgnoreCase))
        {
            node.positionY = property.floatValue;
            node.hasPosition = true;
        }
        else if (leaf.Equals("m_Parent", StringComparison.OrdinalIgnoreCase) &&
                 property.propertyType == SerializedPropertyType.ObjectReference &&
                 property.objectReferenceValue != null &&
                 AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                     property.objectReferenceValue,
                     out _,
                     out long parentId))
        {
            node.parentLocalId = parentId;
        }
    }

    private static void TryResolveMonoScript(
        UnityEngine.Object value,
        VfxGraphNodeRecord node)
    {
        try
        {
            if (value is ScriptableObject scriptableObject)
            {
                MonoScript monoScript = MonoScript.FromScriptableObject(scriptableObject);
                if (monoScript != null)
                {
                    string path = AssetDatabase.GetAssetPath(monoScript);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    Type type = monoScript.GetClass();
                    node.scriptAssetPath = path ?? string.Empty;
                    node.scriptGuid = guid ?? string.Empty;
                    node.scriptType = type?.FullName ?? string.Empty;
                }
            }
        }
        catch
        {
            // YAML fallback resolves the script GUID and type.
        }
    }

    private static void MergeYamlDocuments(
        List<YamlDocument> documents,
        Dictionary<long, UnityEngine.Object> loadedObjects,
        string sourceGuid,
        string sourceAssetPath,
        string outputFolder,
        Dictionary<long, VfxGraphNodeRecord> nodeMap,
        VfxAiReferenceReport report)
    {
        string objectFolder = Path.Combine(outputFolder, "YAML_OBJECTS");
        Directory.CreateDirectory(objectFolder);

        foreach (YamlDocument document in documents)
        {
            if (!nodeMap.TryGetValue(document.localId, out VfxGraphNodeRecord node))
            {
                node = new VfxGraphNodeRecord
                {
                    localId = document.localId
                };
                nodeMap.Add(document.localId, node);
            }

            node.unityClassId = document.unityClassId;
            node.unityClassName = document.unityClassName;
            node.yamlStartLine = document.startLine;
            node.yamlEndLine = document.endLine;
            node.scriptGuid = FirstNonEmpty(node.scriptGuid, document.scriptGuid);
            node.objectName = FirstNonEmpty(node.objectName, document.objectName);
            node.label = FirstNonEmpty(node.label, document.label);
            node.title = FirstNonEmpty(node.title, document.title);
            node.parentLocalId = node.parentLocalId != 0
                ? node.parentLocalId
                : document.parentLocalId;

            if (document.hasPosition && !node.hasPosition)
            {
                node.positionX = document.positionX;
                node.positionY = document.positionY;
                node.hasPosition = true;
            }

            foreach (long childId in document.childLocalIds)
            {
                if (!node.childLocalIds.Contains(childId))
                    node.childLocalIds.Add(childId);
            }

            ScriptTypeInfo scriptInfo = ResolveScriptType(document.scriptGuid);
            node.scriptAssetPath = FirstNonEmpty(
                node.scriptAssetPath,
                scriptInfo.assetPath);
            node.scriptType = FirstNonEmpty(node.scriptType, scriptInfo.typeName);

            if (string.IsNullOrWhiteSpace(node.runtimeType) &&
                loadedObjects.TryGetValue(document.localId, out UnityEngine.Object loaded) &&
                loaded != null)
            {
                node.runtimeType = loaded.GetType().FullName ?? loaded.GetType().Name;
            }

            foreach (YamlScalar scalar in document.scalars)
            {
                if (!node.properties.Any(property =>
                        string.Equals(property.path, scalar.path, StringComparison.Ordinal)))
                {
                    VfxSerializedPropertyRecord yamlProperty = CreateYamlProperty(scalar);
                    node.properties.Add(yamlProperty);
                    report.capture.coreSerializedPropertyCount++;
                    if (yamlProperty.valueTruncated)
                        report.capture.omittedSerializedPropertyCount++;
                }
            }

            foreach (YamlReference reference in document.references)
            {
                VfxReferenceRecord converted = new()
                {
                    propertyPath = reference.path,
                    directionHint = ClassifyReferenceDirection(reference.path),
                    targetLocalId = reference.fileId,
                    targetGuid = reference.guid,
                    targetAssetPath = string.IsNullOrWhiteSpace(reference.guid)
                        ? string.Empty
                        : AssetDatabase.GUIDToAssetPath(reference.guid),
                    sameAsset = string.IsNullOrWhiteSpace(reference.guid) ||
                                string.Equals(
                                    reference.guid,
                                    sourceGuid,
                                    StringComparison.OrdinalIgnoreCase),
                    sourceLayer = "YAML"
                };

                if (converted.sameAsset &&
                    loadedObjects.TryGetValue(reference.fileId, out UnityEngine.Object target) &&
                    target != null)
                {
                    converted.targetType = target.GetType().FullName ?? target.GetType().Name;
                    converted.targetName = target.name ?? string.Empty;
                }
                else if (!string.IsNullOrWhiteSpace(converted.targetAssetPath))
                {
                    Type mainType = AssetDatabase.GetMainAssetTypeAtPath(
                        converted.targetAssetPath);
                    converted.targetType = mainType?.FullName ?? string.Empty;
                    converted.targetName = Path.GetFileNameWithoutExtension(
                        converted.targetAssetPath);
                }

                if (!HasEquivalentReference(node.references, converted))
                    node.references.Add(converted);
            }

            string safeType = SanitizeFileName(
                ShortTypeName(FirstNonEmpty(
                    node.runtimeType,
                    node.scriptType,
                    node.unityClassName,
                    "Object")));
            string fileName = $"{document.localId}_{safeType}.yaml";
            string relativePath = NormalizePath(
                Path.Combine("YAML_OBJECTS", fileName));
            string absolutePath = Path.Combine(objectFolder, fileName);
            File.WriteAllText(absolutePath, document.rawText, new UTF8Encoding(false));
            node.yamlObjectFile = relativePath;
            node.yamlSha256 = ComputeSha256(absolutePath);

            ClassifyNode(node);
            RefreshImportantSettings(node);
        }
    }

    private static bool HasEquivalentReference(
        List<VfxReferenceRecord> references,
        VfxReferenceRecord candidate)
    {
        return references.Any(existing =>
            existing.targetLocalId == candidate.targetLocalId &&
            string.Equals(existing.targetGuid, candidate.targetGuid,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.propertyPath, candidate.propertyPath,
                StringComparison.Ordinal));
    }

    private static void BuildEdges(VfxAiReferenceReport report)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        HashSet<long> knownIds = report.graph.nodes
            .Select(node => node.localId)
            .ToHashSet();

        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            foreach (VfxReferenceRecord reference in node.references)
            {
                if (!reference.sameAsset ||
                    reference.targetLocalId == 0 ||
                    !knownIds.Contains(reference.targetLocalId))
                {
                    continue;
                }

                string kind = ClassifyEdgeKind(reference.propertyPath);
                string key = node.localId + "|" + reference.targetLocalId + "|" +
                             kind + "|" + reference.propertyPath;
                if (!seen.Add(key))
                    continue;

                report.graph.edges.Add(new VfxGraphEdgeRecord
                {
                    sourceLocalId = node.localId,
                    targetLocalId = reference.targetLocalId,
                    kind = kind,
                    propertyPath = reference.propertyPath,
                    sourceLayer = reference.sourceLayer
                });
            }
        }

        report.graph.edges = report.graph.edges
            .OrderBy(edge => edge.sourceLocalId)
            .ThenBy(edge => edge.kind, StringComparer.Ordinal)
            .ThenBy(edge => edge.targetLocalId)
            .ToList();
    }

    private static void RefineContextStages(VfxAiReferenceReport report)
    {
        Dictionary<long, VfxGraphNodeRecord> nodes = report.graph.nodes
            .ToDictionary(node => node.localId);

        foreach (VfxGraphNodeRecord context in report.graph.nodes
                     .Where(node => node.category == "Context" &&
                                    string.IsNullOrWhiteSpace(node.stage)))
        {
            if (HasOutputContextSignature(context))
            {
                context.stage = "Output";
                continue;
            }

            VfxGraphEdgeRecord dataEdge = report.graph.edges.FirstOrDefault(edge =>
                edge.sourceLocalId == context.localId && edge.kind == "DataBinding");
            if (dataEdge != null && nodes.TryGetValue(
                    dataEdge.targetLocalId,
                    out VfxGraphNodeRecord dataNode))
            {
                string dataType = NodeTypeText(dataNode);
                if (dataType.Contains("vfxdataspawner", StringComparison.OrdinalIgnoreCase))
                {
                    context.stage = "Spawn";
                    continue;
                }
            }

            bool hasIncomingFlow = report.graph.edges.Any(edge =>
                edge.targetLocalId == context.localId && edge.kind == "FlowOutput");
            bool hasOutgoingFlow = report.graph.edges.Any(edge =>
                edge.sourceLocalId == context.localId && edge.kind == "FlowOutput");
            if (hasIncomingFlow && !hasOutgoingFlow && dataEdge != null)
                context.stage = "Output";
        }
    }

    private static bool HasOutputContextSignature(VfxGraphNodeRecord node)
    {
        if (node == null)
            return false;

        string typeText = NodeTypeText(node);
        if (typeText.Contains("output", StringComparison.OrdinalIgnoreCase))
            return true;

        string[] outputProperties =
        {
            "blendMode", "zWriteMode", "zTestMode", "primitiveType",
            "shaderGraph", "materialSettings", "useSoftParticle",
            "useBaseColorMap", "useNormalMap", "receiveShadows",
            "indirectDraw", "castShadows", "sortingPriority"
        };
        return node.properties.Any(property => outputProperties.Any(token =>
            property.path.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static void BuildSystems(VfxAiReferenceReport report)
    {
        Dictionary<long, VfxGraphNodeRecord> nodes = report.graph.nodes
            .ToDictionary(node => node.localId);
        List<VfxGraphNodeRecord> contexts = report.graph.nodes
            .Where(node => node.category == "Context")
            .ToList();

        Dictionary<long, HashSet<long>> adjacency = contexts
            .ToDictionary(node => node.localId, _ => new HashSet<long>());

        foreach (VfxGraphEdgeRecord edge in report.graph.edges)
        {
            if (!adjacency.ContainsKey(edge.sourceLocalId) ||
                !adjacency.ContainsKey(edge.targetLocalId) ||
                !edge.kind.StartsWith("Flow", StringComparison.Ordinal))
            {
                continue;
            }

            adjacency[edge.sourceLocalId].Add(edge.targetLocalId);
            adjacency[edge.targetLocalId].Add(edge.sourceLocalId);
        }

        HashSet<long> visited = new();
        foreach (VfxGraphNodeRecord context in contexts)
        {
            if (!visited.Add(context.localId))
                continue;

            Queue<long> queue = new();
            queue.Enqueue(context.localId);
            List<long> component = new();

            while (queue.Count > 0)
            {
                long id = queue.Dequeue();
                component.Add(id);

                if (!adjacency.TryGetValue(id, out HashSet<long> neighbors))
                    continue;

                foreach (long neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            report.graph.systems.Add(
                BuildSystemRecord(component, nodes, report.graph.edges));
        }

        report.graph.systems = report.graph.systems
            .OrderBy(system => system.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static VfxSystemRecord BuildSystemRecord(
        List<long> contextIds,
        Dictionary<long, VfxGraphNodeRecord> nodes,
        List<VfxGraphEdgeRecord> edges)
    {
        VfxSystemRecord system = new();
        HashSet<long> contextSet = new(contextIds);
        system.contextLocalIds = contextIds
            .OrderBy(id => StageRank(nodes[id].stage))
            .ThenBy(id => nodes[id].positionY)
            .ThenBy(id => id)
            .ToList();

        List<VfxGraphNodeRecord> dataCandidates = edges
            .Where(edge => contextSet.Contains(edge.sourceLocalId) &&
                           edge.kind == "DataBinding")
            .Select(edge => nodes.TryGetValue(edge.targetLocalId, out VfxGraphNodeRecord data)
                ? data
                : null)
            .Where(data => data != null && data.category == "Data")
            .Distinct()
            .ToList();

        VfxGraphNodeRecord particleData = dataCandidates
            .OrderByDescending(data => ScoreParticleDataCandidate(
                data,
                contextSet,
                nodes,
                edges))
            .FirstOrDefault(data => IsParticleDataNode(data));

        if (particleData == null)
        {
            particleData = dataCandidates
                .OrderByDescending(data => ScoreParticleDataCandidate(
                    data,
                    contextSet,
                    nodes,
                    edges))
                .FirstOrDefault();
        }

        VfxGraphNodeRecord spawnData = dataCandidates.FirstOrDefault(data =>
            NodeTypeText(data).Contains(
                "vfxdataspawner",
                StringComparison.OrdinalIgnoreCase));

        if (particleData != null)
        {
            system.dataNodeLocalId = particleData.localId;
            system.dataType = FirstNonEmpty(
                particleData.scriptType,
                particleData.runtimeType,
                particleData.unityClassName);
            system.name = FirstNonEmpty(
                particleData.title,
                particleData.label,
                particleData.objectName,
                particleData.displayName);
            system.capacity = ReadIntSetting(particleData, "capacity");
            system.stripCapacity = ReadIntSetting(particleData, "stripCapacity");
            system.particlePerStripCount = ReadIntSetting(
                particleData,
                "particlePerStripCount");
        }

        if (spawnData != null)
            system.spawnDataNodeLocalId = spawnData.localId;

        if (string.IsNullOrWhiteSpace(system.name))
        {
            system.name = DeriveSystemName(
                system.contextLocalIds.Select(id => nodes[id]));
        }

        HashSet<long> included = new(contextIds);
        if (particleData != null)
            included.Add(particleData.localId);
        if (spawnData != null)
            included.Add(spawnData.localId);

        foreach (VfxGraphNodeRecord node in nodes.Values)
        {
            if (!contextSet.Contains(node.parentLocalId))
                continue;

            if (node.category == "Block")
                system.blockLocalIds.Add(node.localId);
            included.Add(node.localId);
        }

        HashSet<long> frontier = new(included);
        for (int pass = 0; pass < 6; pass++)
        {
            bool changed = false;
            foreach (VfxGraphEdgeRecord edge in edges)
            {
                if (edge.kind != "SlotLink" && edge.kind != "SlotOwnership")
                    continue;

                if (frontier.Contains(edge.sourceLocalId) &&
                    nodes.TryGetValue(edge.targetLocalId, out VfxGraphNodeRecord target))
                {
                    if (target.category == "Operator" && included.Add(target.localId))
                    {
                        system.operatorLocalIds.Add(target.localId);
                        changed = true;
                    }
                    frontier.Add(target.localId);
                }

                if (frontier.Contains(edge.targetLocalId) &&
                    nodes.TryGetValue(edge.sourceLocalId, out VfxGraphNodeRecord source))
                {
                    if (source.category == "Operator" && included.Add(source.localId))
                    {
                        system.operatorLocalIds.Add(source.localId);
                        changed = true;
                    }
                    frontier.Add(source.localId);
                }
            }

            if (!changed)
                break;
        }

        system.blockLocalIds = system.blockLocalIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        system.operatorLocalIds = system.operatorLocalIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        system.stages = system.contextLocalIds
            .Select(id => nodes[id].stage)
            .Where(stage => !string.IsNullOrWhiteSpace(stage))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(StageRank)
            .ToList();

        HashSet<string> settingKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (long id in included.OrderBy(value => value))
        {
            if (!nodes.TryGetValue(id, out VfxGraphNodeRecord node))
                continue;

            foreach (VfxKeyValueRecord setting in node.importantSettings)
            {
                string key = id + "|" + setting.key + "|" + setting.value;
                if (settingKeys.Add(key))
                    system.importantSettings.Add(setting);
            }
        }

        HashSet<string> capabilities = new(StringComparer.OrdinalIgnoreCase);
        foreach (long id in included)
        {
            if (!nodes.TryGetValue(id, out VfxGraphNodeRecord node))
                continue;
            foreach (string capability in node.detectedCapabilities)
                capabilities.Add(capability);
        }
        system.detectedCapabilities = capabilities
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        return system;
    }

    private static int ScoreParticleDataCandidate(
        VfxGraphNodeRecord data,
        HashSet<long> contextIds,
        Dictionary<long, VfxGraphNodeRecord> nodes,
        List<VfxGraphEdgeRecord> edges)
    {
        int score = 0;
        string typeText = NodeTypeText(data);
        if (typeText.Contains("vfxdataparticle", StringComparison.OrdinalIgnoreCase))
            score += 1000;
        if (typeText.Contains("vfxdataspawner", StringComparison.OrdinalIgnoreCase))
            score -= 1000;
        if (ReadIntSetting(data, "capacity") > 0)
            score += 100;

        foreach (VfxGraphEdgeRecord edge in edges.Where(edge =>
                     edge.kind == "DataBinding" &&
                     edge.targetLocalId == data.localId &&
                     contextIds.Contains(edge.sourceLocalId)))
        {
            score += 20;
            if (nodes.TryGetValue(edge.sourceLocalId, out VfxGraphNodeRecord context) &&
                context.stage != "Spawn")
            {
                score += 100;
            }
        }

        return score;
    }

    private static bool IsParticleDataNode(VfxGraphNodeRecord node)
    {
        string typeText = NodeTypeText(node);
        return typeText.Contains("vfxdataparticle", StringComparison.OrdinalIgnoreCase) &&
               !typeText.Contains("vfxdataspawner", StringComparison.OrdinalIgnoreCase);
    }

    private static void CaptureGraphMetadata(
        VfxAiReferenceReport report,
        List<YamlDocument> documents)
    {
        VfxGraphNodeRecord graphNode = report.graph.nodes.FirstOrDefault(node =>
            node.category == "Graph");
        VfxGraphNodeRecord resourceNode = report.graph.nodes.FirstOrDefault(node =>
            node.category == "Resource");

        report.graph.graphName = FirstNonEmpty(
            graphNode?.objectName,
            graphNode?.displayName,
            report.assetName);
        report.graph.graphVersion = ReadIntSetting(graphNode, "m_GraphVersion");
        report.graph.resourceVersion = ReadIntSetting(graphNode, "m_ResourceVersion");
        report.graph.initialEventName = ReadStringSetting(
            resourceNode,
            "m_InitialEventName");
        report.graph.prewarmDeltaTime = ReadFloatSetting(
            resourceNode,
            "m_PreWarmDeltaTime");
        report.graph.prewarmStepCount = ReadIntSetting(
            resourceNode,
            "m_PreWarmStepCount");

        List<Bounds> boundsCandidates = new();
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            foreach (VfxSerializedPropertyRecord property in node.properties)
            {
                if (property.serializedType == SerializedPropertyType.Bounds.ToString() &&
                    TryParseBoundsString(property.value, out Bounds serializedBounds))
                {
                    boundsCandidates.Add(serializedBounds);
                }

                if (property.path.EndsWith("m_SerializableObject", StringComparison.Ordinal) &&
                    TryParseAaBoxJson(property.value, out Bounds yamlBounds))
                {
                    boundsCandidates.Add(yamlBounds);
                }
            }
        }

        if (boundsCandidates.Count > 0)
        {
            Bounds union = boundsCandidates[0];
            for (int i = 1; i < boundsCandidates.Count; i++)
                union.Encapsulate(boundsCandidates[i]);

            report.graph.suggestedBounds = ToBoundsRecord(union, "Serialized VFX AABox/Bounds");
        }
        else
        {
            report.graph.suggestedBounds = ToBoundsRecord(
                new Bounds(new Vector3(0f, 1f, 0f), new Vector3(4f, 4f, 4f)),
                "Fallback preview bounds");
        }
    }

    private static void CaptureUiMetadata(
        VfxAiReferenceReport report,
        string yaml)
    {
        if (string.IsNullOrEmpty(yaml))
            return;

        // UI groups and sticky notes are editorial metadata. A tolerant line parser is
        // used so package-version field additions do not break the export.
        string[] lines = NormalizeNewLines(yaml).Split('\n');
        string section = string.Empty;
        VfxYamlGroupRecord currentGroup = null;
        VfxYamlStickyNoteRecord currentNote = null;
        string positionTarget = string.Empty;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed == "groupInfos:")
            {
                section = "group";
                continue;
            }
            if (trimmed == "stickyNoteInfos:")
            {
                section = "note";
                continue;
            }
            if (trimmed.StartsWith("categories:", StringComparison.Ordinal))
            {
                section = string.Empty;
                continue;
            }

            if (section == "group" && trimmed.StartsWith("- title:", StringComparison.Ordinal))
            {
                currentGroup = new VfxYamlGroupRecord
                {
                    title = Unquote(trimmed.Substring("- title:".Length).Trim())
                };
                report.graph.uiGroups.Add(currentGroup);
                currentNote = null;
                positionTarget = string.Empty;
                continue;
            }

            if (section == "note" && trimmed.StartsWith("- title:", StringComparison.Ordinal))
            {
                currentNote = new VfxYamlStickyNoteRecord
                {
                    title = Unquote(trimmed.Substring("- title:".Length).Trim())
                };
                report.graph.stickyNotes.Add(currentNote);
                currentGroup = null;
                positionTarget = string.Empty;
                continue;
            }

            if (trimmed == "position:")
            {
                positionTarget = section;
                continue;
            }

            if (currentGroup != null)
            {
                Match modelMatch = Regex.Match(
                    trimmed,
                    @"model:\s*\{fileID:\s*(-?\d+)\}");
                if (modelMatch.Success &&
                    long.TryParse(modelMatch.Groups[1].Value, out long modelId) &&
                    !currentGroup.modelLocalIds.Contains(modelId))
                {
                    currentGroup.modelLocalIds.Add(modelId);
                }

                ApplyRectScalar(currentGroup, trimmed, positionTarget == "group");
            }
            else if (currentNote != null)
            {
                ApplyRectScalar(currentNote, trimmed, positionTarget == "note");
                if (trimmed.StartsWith("theme:", StringComparison.Ordinal))
                    currentNote.theme = Unquote(AfterColon(trimmed));
                else if (trimmed.StartsWith("textSize:", StringComparison.Ordinal))
                    currentNote.textSize = Unquote(AfterColon(trimmed));
                else if (trimmed.StartsWith("contents:", StringComparison.Ordinal))
                    currentNote.contents = Unquote(AfterColon(trimmed));
            }
        }
    }

    private static void DetectCapabilities(
        VfxAiReferenceReport report,
        string yaml)
    {
        Dictionary<string, HashSet<string>> aggregate =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            Dictionary<string, HashSet<string>> local =
                new(StringComparer.OrdinalIgnoreCase);
            DetectNodeCapabilities(node, local);
            node.detectedCapabilities = local.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();

            foreach (KeyValuePair<string, HashSet<string>> pair in local)
            {
                foreach (string evidence in pair.Value)
                    AddEvidence(aggregate, pair.Key, evidence);
            }
        }

        int outputCount = report.graph.nodes.Count(node =>
            node.category == "Context" && node.stage == "Output");
        if (outputCount > 1)
        {
            AddEvidence(
                aggregate,
                "Multiple Outputs",
                $"GRAPH: {outputCount} Output contexts detected.");
        }

        if (report.publicApi.exposedProperties.Count > 0)
        {
            AddEvidence(
                aggregate,
                "Exposed Property",
                $"PUBLIC: {report.publicApi.exposedProperties.Count} exposed Blackboard properties reported by VisualEffectAsset.");
        }

        if (report.publicApi.exposedProperties.Any(property =>
                property.type.IndexOf("Mesh", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            AddEvidence(
                aggregate,
                "Exposed Mesh",
                "PUBLIC: VisualEffectAsset reports an exposed Mesh property.");
        }

        if (report.publicApi.exposedProperties.Any(property =>
                property.type.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            AddEvidence(
                aggregate,
                "Exposed Texture",
                "PUBLIC: VisualEffectAsset reports an exposed Texture property.");
        }

        foreach (VfxDependencyRecord dependency in report.dependencies)
        {
            string extension = dependency.extension?.ToLowerInvariant() ?? string.Empty;
            if (extension == ".vfxoperator" || extension == ".vfxblock")
            {
                AddEvidence(
                    aggregate,
                    "Subgraph",
                    $"DEPENDENCY: {dependency.assetPath}");
            }

        }

        report.capabilities = aggregate
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new VfxCapabilityRecord
            {
                name = pair.Key,
                confidence = DetermineCapabilityConfidence(pair.Value),
                evidence = pair.Value.Take(24).ToList()
            })
            .ToList();
    }

    private static void DetectNodeCapabilities(
        VfxGraphNodeRecord node,
        Dictionary<string, HashSet<string>> evidence)
    {
        string implementation = NodeImplementationText(node);
        string typeEvidence =
            $"TYPE: Node {node.localId} {node.displayName} ({FirstNonEmpty(node.scriptType, node.runtimeType, node.unityClassName)})";

        if (node.stage == "GPU Event" || TypeContains(implementation, "gpuevent"))
            AddEvidence(evidence, "GPU Event", typeEvidence);
        if (node.stage == "Output Event" || TypeContains(implementation, "outputevent"))
            AddEvidence(evidence, "Output Event", typeEvidence);

        if (IsParticleDataNode(node) && ReadIntSetting(node, "capacity") >= 0)
            AddEvidence(evidence, "Capacity", PropertyEvidence(node, "capacity"));
        if (node.properties.Any(property =>
                property.serializedType == SerializedPropertyType.Bounds.ToString() ||
                property.path.Contains("bounds", StringComparison.OrdinalIgnoreCase) ||
                property.value.Contains("AABox", StringComparison.OrdinalIgnoreCase)))
        {
            AddEvidence(evidence, "Bounds", $"PROPERTY: Node {node.localId} contains a Bounds/AABox setting.");
        }

        if (TypeContains(
                implementation,
                "constantspawnrate",
                "spawnerconstantrate",
                "spawnrate"))
        {
            AddEvidence(evidence, "Spawn Rate", typeEvidence);
        }
        if (TypeContains(implementation, "spawnerburst", "singleburst", "periodicburst"))
        {
            if (TypeContains(implementation, "periodicburst") ||
                HasEnabledProperty(node, "spawnMode"))
            {
                AddEvidence(evidence, "Periodic Burst", PropertyEvidence(node, "spawnMode"));
            }
            else
            {
                AddEvidence(evidence, "Single Burst", typeEvidence);
            }
        }
        if (TypeContains(implementation, "spawnoverdistance"))
            AddEvidence(evidence, "Spawn Over Distance", typeEvidence);

        bool strip = TypeContains(
            implementation,
            "vfxdataparticlestrip",
            "outputparticlestrip",
            "particlestripoutput",
            "stripoutput");
        if (strip)
            AddEvidence(evidence, "Particle Strip", typeEvidence);
        if (strip && (TypeContains(implementation, "multistrip") ||
                      HasEnabledProperty(node, "multiStrip") ||
                      HasPropertyLeaf(node, "stripIndex")))
        {
            AddEvidence(evidence, "Multi-Strip", typeEvidence);
        }

        bool output = node.category == "Context" && node.stage == "Output";
        if (output && (TypeContains(implementation, "outputparticlemesh", "particleoutputmesh") ||
                       HasAssignedReference(node, "mesh")))
        {
            AddEvidence(evidence, "Mesh Output", typeEvidence);
        }
        if (output && (TypeContains(implementation, "shadergraph") ||
                       HasAssignedReference(node, "shaderGraph")))
        {
            AddEvidence(evidence, "Shader Graph Output", PropertyEvidence(node, "shaderGraph"));
        }
        if (output && (TypeContains(implementation, "outputparticlelit", "particleshadinglit") ||
                       HasEnabledProperty(node, "useNormalMap") ||
                       HasEnabledProperty(node, "receiveShadows") ||
                       HasEnabledProperty(node, "enableEnvLight") ||
                       HasEnabledProperty(node, "enableSpecular")))
        {
            AddEvidence(evidence, "Lit Output", typeEvidence);
        }
        if (output && TypeContains(implementation, "distortion"))
            AddEvidence(evidence, "Distortion Output", typeEvidence);
        if (output && TypeContains(implementation, "decal"))
            AddEvidence(evidence, "Decal Output", typeEvidence);

        if (TypeContains(implementation, "flipbook", "settexindex") ||
            (output && (HasEnabledProperty(node, "uvMode") ||
                        HasEnabledProperty(node, "flipbookLayout"))))
        {
            AddEvidence(evidence, "Flipbook", typeEvidence);
        }
        if (HasEnabledProperty(node, "flipbookBlendFrames"))
            AddEvidence(evidence, "Flipbook Blending", PropertyEvidence(node, "flipbookBlendFrames"));
        if (TypeContains(implementation, "texindex") ||
            PropertyValueContains(node, "attribute", "texindex"))
        {
            AddEvidence(evidence, "TexIndex Attribute", typeEvidence);
        }

        if (TypeContains(implementation, "samplemesh", "meshsampling"))
            AddEvidence(evidence, "Sample Mesh", typeEvidence);
        if (TypeContains(implementation, "sampletexture", "sampletexture2d"))
            AddEvidence(evidence, "Sample Texture", typeEvidence);
        if (TypeContains(implementation, "samplesdf", "signeddistancefield"))
            AddEvidence(evidence, "Sample Signed Distance Field", typeEvidence);
        if (TypeContains(implementation, "sampleskinned", "skinnedmesh"))
            AddEvidence(evidence, "Sample Skinned Mesh", typeEvidence);

        if ((node.category == "Block" || node.category == "Operator") &&
            TypeContains(implementation, "collision", "collide"))
        {
            AddEvidence(evidence, "Collision", typeEvidence);
        }
        if (TypeContains(implementation, "triggereventoncollide"))
            AddEvidence(evidence, "Trigger Event on Collide", typeEvidence);
        else if (TypeContains(implementation, "triggerevent"))
            AddEvidence(evidence, "Trigger Event", typeEvidence);
        if (TypeContains(implementation, "sdfcollision", "collisionsdf"))
            AddEvidence(evidence, "SDF Collision", typeEvidence);

        if (TypeContains(implementation, "turbulence", "vectorfield", "curlnoise"))
            AddEvidence(evidence, "Vector Field / Turbulence", typeEvidence);
        if (TypeContains(implementation, "noise", "voronoi", "perlin", "simplex"))
            AddEvidence(evidence, "Noise", typeEvidence);
        if (TypeContains(implementation, "subgraph"))
            AddEvidence(evidence, "Subgraph", typeEvidence);
        if (TypeContains(implementation, "customhlsl"))
            AddEvidence(evidence, "Custom HLSL", typeEvidence);

        if (TypeContains(implementation, "camerabuffer") ||
            HasNonZeroNumericProperty(node, "m_NeededMainCameraBuffers"))
        {
            AddEvidence(evidence, "Camera Buffer", PropertyEvidence(node, "m_NeededMainCameraBuffers"));
        }

        if (TypeContains(implementation, "orient"))
            AddEvidence(evidence, "Orientation", typeEvidence);
        if (TypeContains(implementation, "facecamera"))
            AddEvidence(evidence, "Orient Face Camera", typeEvidence);
        if (TypeContains(implementation, "fixedaxis"))
            AddEvidence(evidence, "Orient Fixed Axis", typeEvidence);
        if (TypeContains(implementation, "advancedorient", "orientadvanced"))
            AddEvidence(evidence, "Orient Advanced", typeEvidence);
        if (TypeContains(implementation, "pivot") || HasNonDefaultVectorProperty(node, "pivot"))
            AddEvidence(evidence, "Pivot", PropertyEvidence(node, "pivot"));
        if (TypeContains(implementation, "setangle") ||
            PropertyValueContains(node, "attribute", "angle"))
        {
            AddEvidence(evidence, "Rotation Angle", typeEvidence);
        }
        if (TypeContains(implementation, "angularvelocity"))
            AddEvidence(evidence, "Angular Velocity", typeEvidence);

        if (HasEnabledProperty(node, "useSoftParticle"))
            AddEvidence(evidence, "Soft Particles", PropertyEvidence(node, "useSoftParticle"));
        if (HasEnabledProperty(node, "useNormalMap"))
            AddEvidence(evidence, "Normal Map", PropertyEvidence(node, "useNormalMap"));
        if (HasEnabledProperty(node, "generateMotionVector") ||
            HasEnabledProperty(node, "flipbookMotionVectors"))
        {
            AddEvidence(evidence, "Motion Vectors", PropertyEvidence(node, "generateMotionVector"));
        }
    }

    private static string NodeTypeText(VfxGraphNodeRecord node)
    {
        return string.Join(" ", new[]
        {
            node?.runtimeType,
            node?.scriptType,
            node?.unityClassName,
            node?.objectName,
            node?.label,
            node?.title,
            node?.displayName
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string NodeImplementationText(VfxGraphNodeRecord node)
    {
        string strong = string.Join(" ", new[]
        {
            node?.runtimeType,
            node?.scriptType,
            node?.unityClassName
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return !string.IsNullOrWhiteSpace(strong) ? strong : NodeTypeText(node);
    }

    private static bool TypeContains(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return tokens.Any(token =>
            text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool HasPropertyLeaf(VfxGraphNodeRecord node, string leafName)
    {
        return FindProperty(node, leafName) != null;
    }

    private static VfxSerializedPropertyRecord FindProperty(
        VfxGraphNodeRecord node,
        string leafName)
    {
        return node?.properties.LastOrDefault(property =>
            GetLeafName(property.path).Equals(
                leafName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasEnabledProperty(
        VfxGraphNodeRecord node,
        string leafName)
    {
        VfxSerializedPropertyRecord property = FindProperty(node, leafName);
        return property != null && IsEnabledValue(property.value);
    }

    private static bool HasNonZeroNumericProperty(
        VfxGraphNodeRecord node,
        string leafName)
    {
        VfxSerializedPropertyRecord property = FindProperty(node, leafName);
        if (property == null)
            return false;

        return double.TryParse(
                   property.value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out double value) && Math.Abs(value) > double.Epsilon;
    }

    private static bool HasNonDefaultVectorProperty(
        VfxGraphNodeRecord node,
        string leafName)
    {
        VfxSerializedPropertyRecord property = FindProperty(node, leafName);
        if (property == null || string.IsNullOrWhiteSpace(property.value))
            return false;

        foreach (Match match in Regex.Matches(
                     property.value,
                     @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?"))
        {
            if (double.TryParse(
                    match.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value) && Math.Abs(value) > 0.000001d)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAssignedReference(
        VfxGraphNodeRecord node,
        string propertyToken)
    {
        if (node.references.Any(reference =>
                reference.propertyPath.IndexOf(
                    propertyToken,
                    StringComparison.OrdinalIgnoreCase) >= 0 &&
                (reference.targetLocalId != 0 ||
                 !string.IsNullOrWhiteSpace(reference.targetGuid) ||
                 !string.IsNullOrWhiteSpace(reference.targetAssetPath))))
        {
            return true;
        }

        return node.properties.Any(property =>
            property.path.IndexOf(propertyToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
            IsAssignedReferenceValue(property.value));
    }

    private static bool PropertyValueContains(
        VfxGraphNodeRecord node,
        string propertyToken,
        string valueToken)
    {
        return node.properties.Any(property =>
            property.path.IndexOf(propertyToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
            property.value.IndexOf(valueToken, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsEnabledValue(string value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized == "0" || normalized == "0.0" ||
            normalized == "false" || normalized == "off" ||
            normalized == "disabled" || normalized == "none" ||
            normalized == "null" || normalized == "[]" || normalized == "{}")
        {
            return false;
        }

        if (double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number))
        {
            return Math.Abs(number) > double.Epsilon;
        }

        return true;
    }

    private static bool IsAssignedReferenceValue(string value)
    {
        string normalized = (value ?? string.Empty).Replace(" ", string.Empty);
        if (normalized.Contains("fileID:0", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("guid:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return normalized.Contains("fileID:", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("guid:", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("path=", StringComparison.OrdinalIgnoreCase);
    }

    private static string PropertyEvidence(
        VfxGraphNodeRecord node,
        string leafName)
    {
        VfxSerializedPropertyRecord property = FindProperty(node, leafName);
        return property == null
            ? $"PROPERTY: Node {node.localId} {leafName}"
            : $"PROPERTY: Node {node.localId} {property.path} = {Limit(property.value, 160)}";
    }

    private static void AddEvidence(
        Dictionary<string, HashSet<string>> evidence,
        string capability,
        string source)
    {
        if (!evidence.TryGetValue(capability, out HashSet<string> values))
        {
            values = new HashSet<string>(StringComparer.Ordinal);
            evidence.Add(capability, values);
        }

        values.Add(source);
    }

    private static string DetermineCapabilityConfidence(HashSet<string> evidence)
    {
        if (evidence.Any(value =>
                value.StartsWith("TYPE:", StringComparison.Ordinal) ||
                value.StartsWith("PROPERTY:", StringComparison.Ordinal) ||
                value.StartsWith("GRAPH:", StringComparison.Ordinal) ||
                value.StartsWith("PUBLIC:", StringComparison.Ordinal)))
        {
            return "High";
        }

        return evidence.Count >= 1 ? "Medium" : "Low";
    }

    private static void CollectDependencies(
        VfxAiReferenceReport report,
        string yaml,
        string assetPath,
        string assetGuid)
    {
        Dictionary<string, DependencyAccumulator> byGuid =
            new(StringComparer.OrdinalIgnoreCase);

        HashSet<string> direct = new(
            AssetDatabase.GetDependencies(assetPath, false),
            StringComparer.Ordinal);
        HashSet<string> recursive = new(
            AssetDatabase.GetDependencies(assetPath, true),
            StringComparer.Ordinal);

        foreach (string path in recursive)
        {
            if (string.Equals(path, assetPath, StringComparison.Ordinal))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                continue;

            DependencyAccumulator accumulator = GetDependency(byGuid, guid);
            accumulator.assetPath = path;
            accumulator.direct |= direct.Contains(path);
            accumulator.recursive = true;
            accumulator.usages.Add("AssetDatabase.GetDependencies");
        }

        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            foreach (VfxReferenceRecord reference in node.references)
            {
                if (reference.sameAsset || string.IsNullOrWhiteSpace(reference.targetGuid))
                    continue;

                DependencyAccumulator accumulator = GetDependency(
                    byGuid,
                    reference.targetGuid);
                accumulator.fromSerialized = true;
                accumulator.assetPath = FirstNonEmpty(
                    accumulator.assetPath,
                    reference.targetAssetPath);
                accumulator.usages.Add(
                    $"Node {node.localId}: {reference.propertyPath}");
                accumulator.roles.Add(ClassifyDependencyRole(reference.propertyPath));
            }
        }

        if (!string.IsNullOrEmpty(yaml))
        {
            string[] lines = NormalizeNewLines(yaml).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in GuidRegex.Matches(lines[i]))
                {
                    string guid = match.Value.ToLowerInvariant();
                    if (guid == assetGuid)
                        continue;

                    DependencyAccumulator accumulator = GetDependency(byGuid, guid);
                    accumulator.fromYaml = true;
                    accumulator.assetPath = FirstNonEmpty(
                        accumulator.assetPath,
                        AssetDatabase.GUIDToAssetPath(guid));
                    string usage = $"YAML line {i + 1}: {Limit(lines[i].Trim(), 220)}";
                    accumulator.usages.Add(usage);
                    accumulator.roles.Add(ClassifyDependencyRole(lines[i]));
                }
            }
        }

        foreach (DependencyAccumulator accumulator in byGuid.Values)
        {
            string path = FirstNonEmpty(
                accumulator.assetPath,
                AssetDatabase.GUIDToAssetPath(accumulator.guid));
            string physicalPath = ResolvePhysicalPath(path);
            Type type = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.GetMainAssetTypeAtPath(path);

            VfxDependencyRecord record = new()
            {
                guid = accumulator.guid,
                assetPath = path,
                physicalPath = physicalPath,
                mainAssetType = type?.FullName ?? string.Empty,
                extension = Path.GetExtension(path ?? string.Empty),
                isDirectAssetDatabaseDependency = accumulator.direct,
                isRecursiveAssetDatabaseDependency = accumulator.recursive,
                discoveredFromYaml = accumulator.fromYaml,
                discoveredFromSerializedObject = accumulator.fromSerialized,
                isPackageAsset = !string.IsNullOrWhiteSpace(path) &&
                                 path.StartsWith("Packages/", StringComparison.Ordinal),
                isBuiltInOrUnresolved = string.IsNullOrWhiteSpace(path),
                roles = accumulator.roles
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToList(),
                usages = accumulator.usages
                    .Distinct(StringComparer.Ordinal)
                    .Take(64)
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(physicalPath) && File.Exists(physicalPath))
            {
                FileInfo info = new(physicalPath);
                record.byteSize = info.Length;
                record.sha256 = ComputeSha256(physicalPath);
            }

            report.dependencies.Add(record);
        }

        report.dependencies = report.dependencies
            .OrderBy(record => record.assetPath, StringComparer.Ordinal)
            .ThenBy(record => record.guid, StringComparer.Ordinal)
            .ToList();

        foreach (VfxDependencyRecord dependency in report.dependencies
                     .Where(item => item.isBuiltInOrUnresolved))
        {
            report.graph.unresolvedGuids.Add(new VfxUnresolvedGuidRecord
            {
                guid = dependency.guid,
                usages = dependency.usages.Take(16).ToList()
            });
        }
    }

    private static DependencyAccumulator GetDependency(
        Dictionary<string, DependencyAccumulator> values,
        string guid)
    {
        if (!values.TryGetValue(guid, out DependencyAccumulator accumulator))
        {
            accumulator = new DependencyAccumulator { guid = guid };
            values.Add(guid, accumulator);
        }

        return accumulator;
    }

    private static void ComputeQuality(
        VfxAiReferenceReport report,
        int yamlDocumentCount)
    {
        VfxExportQuality quality = report.quality;
        quality.yamlDocumentsTotal = yamlDocumentCount;
        quality.yamlDocumentsRepresented = report.graph.nodes.Count(node =>
            node.yamlStartLine > 0);
        quality.graphObjectCoverage = yamlDocumentCount <= 0
            ? (report.graph.nodes.Count > 0 ? 1f : 0f)
            : Mathf.Clamp01((float)quality.yamlDocumentsRepresented / yamlDocumentCount);
        quality.unresolvedNodeTypeCount = report.graph.nodes.Count(node =>
            node.unityClassName == "MonoBehaviour" &&
            string.IsNullOrWhiteSpace(node.runtimeType) &&
            string.IsNullOrWhiteSpace(node.scriptType) &&
            string.IsNullOrWhiteSpace(node.scriptGuid));
        quality.unresolvedGuidCount = report.graph.unresolvedGuids.Count;
        quality.externalDependencyCount = report.dependencies.Count;
        quality.missingDependencyPathCount = report.dependencies.Count(dependency =>
            dependency.isBuiltInOrUnresolved);
        quality.rawSourceGuaranteesLosslessFallback = report.capture.rawYamlIncluded;

        quality.checks.Add(report.capture.rawYamlIncluded
            ? "PASS: Original .vfx YAML is included."
            : "FAIL: Original .vfx YAML could not be read.");
        quality.checks.Add(report.graph.nodes.Count > 0
            ? $"PASS: {report.graph.nodes.Count} graph objects represented."
            : "FAIL: No graph objects represented.");
        quality.checks.Add(quality.graphObjectCoverage >= 0.98f
            ? $"PASS: YAML object coverage {quality.graphObjectCoverage:P1}."
            : $"WARN: YAML object coverage {quality.graphObjectCoverage:P1}.");
        quality.checks.Add(report.graph.edges.Count > 0
            ? $"PASS: {report.graph.edges.Count} graph relationships represented."
            : "WARN: No internal graph relationships detected.");
        quality.checks.Add(report.dependencies.Count > 0
            ? $"PASS: {report.dependencies.Count} dependency GUIDs represented."
            : "WARN: No external dependencies detected.");

        bool fatal = !report.capture.rawYamlIncluded || report.graph.nodes.Count == 0;
        bool partial = quality.graphObjectCoverage < 0.90f ||
                       quality.unresolvedNodeTypeCount > 0 ||
                       report.graph.edges.Count == 0;
        quality.status = fatal ? "FAILED" : partial ? "PARTIAL" : "READY";
    }

    private static List<YamlDocument> ParseYamlDocuments(string yaml)
    {
        List<YamlDocument> result = new();
        if (string.IsNullOrWhiteSpace(yaml))
            return result;

        string normalized = NormalizeNewLines(yaml);
        MatchCollection matches = DocumentHeaderRegex.Matches(normalized);
        int[] lineStarts = BuildLineStartIndex(normalized);

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            int start = match.Index;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : normalized.Length;
            string raw = normalized.Substring(start, end - start).TrimEnd('\n', '\r') + "\n";
            int startLine = FindLineNumber(lineStarts, start);
            int endLine = FindLineNumber(lineStarts, Math.Max(start, end - 1));

            YamlDocument document = new()
            {
                unityClassId = ParseInt(match.Groups["classId"].Value),
                localId = ParseLong(match.Groups["localId"].Value),
                rawText = raw,
                startLine = startLine,
                endLine = endLine
            };

            ParseYamlDocumentContent(document);
            result.Add(document);
        }

        return result;
    }

    private static void ParseYamlDocumentContent(YamlDocument document)
    {
        string[] lines = NormalizeNewLines(document.rawText).Split('\n');
        List<YamlPathEntry> stack = new();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int indent = CountLeadingSpaces(line);
            string trimmed = line.TrimStart();
            bool listItem = trimmed.StartsWith("- ", StringComparison.Ordinal);
            if (listItem)
                trimmed = trimmed.Substring(2).TrimStart();

            if (i == 1 && trimmed.EndsWith(":", StringComparison.Ordinal))
                document.unityClassName = trimmed.TrimEnd(':').Trim();

            while (stack.Count > 0 &&
                   (listItem ? stack[^1].indent > indent : stack[^1].indent >= indent))
            {
                stack.RemoveAt(stack.Count - 1);
            }

            int colon = FindYamlKeyColon(trimmed);
            string key = colon >= 0 ? trimmed.Substring(0, colon).Trim() : string.Empty;
            string value = colon >= 0 ? trimmed.Substring(colon + 1).Trim() : trimmed;
            string parentPath = string.Join(".", stack.Select(entry => entry.key));
            string path = !string.IsNullOrWhiteSpace(key)
                ? JoinPath(parentPath, key)
                : JoinPath(parentPath, listItem ? "[]" : string.Empty);

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                document.scalars.Add(new YamlScalar
                {
                    path = path,
                    value = Unquote(value),
                    depth = stack.Count,
                    isListItem = listItem,
                    lineNumber = document.startLine + i
                });
            }

            foreach (Match referenceMatch in InlineReferenceRegex.Matches(line))
            {
                document.references.Add(new YamlReference
                {
                    path = path,
                    fileId = ParseLong(referenceMatch.Groups["fileId"].Value),
                    guid = referenceMatch.Groups["guid"].Success
                        ? referenceMatch.Groups["guid"].Value.ToLowerInvariant()
                        : string.Empty,
                    type = referenceMatch.Groups["type"].Success
                        ? ParseInt(referenceMatch.Groups["type"].Value)
                        : 0,
                    lineNumber = document.startLine + i
                });
            }

            if (path.EndsWith("m_Script", StringComparison.Ordinal))
            {
                Match scriptRef = InlineReferenceRegex.Match(line);
                if (scriptRef.Success)
                    document.scriptGuid = scriptRef.Groups["guid"].Value.ToLowerInvariant();
            }
            else if (path.EndsWith("m_Name", StringComparison.Ordinal))
            {
                document.objectName = Unquote(value);
            }
            else if (path.EndsWith("m_Label", StringComparison.Ordinal) ||
                     path.EndsWith("label", StringComparison.Ordinal))
            {
                document.label = Unquote(value);
            }
            else if (path.EndsWith("title", StringComparison.Ordinal))
            {
                document.title = Unquote(value);
            }
            else if (path.EndsWith("m_Parent", StringComparison.Ordinal))
            {
                Match parentRef = InlineReferenceRegex.Match(line);
                if (parentRef.Success)
                    document.parentLocalId = ParseLong(parentRef.Groups["fileId"].Value);
            }
            else if (path.Contains("m_Children", StringComparison.Ordinal))
            {
                Match childRef = InlineReferenceRegex.Match(line);
                if (childRef.Success)
                {
                    long childId = ParseLong(childRef.Groups["fileId"].Value);
                    if (childId != 0 && !document.childLocalIds.Contains(childId))
                        document.childLocalIds.Add(childId);
                }
            }

            if (path.EndsWith("m_UIPosition", StringComparison.Ordinal) &&
                TryParseInlineVector2(value, out Vector2 position))
            {
                document.positionX = position.x;
                document.positionY = position.y;
                document.hasPosition = true;
            }

            if (!string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(value))
            {
                stack.Add(new YamlPathEntry { indent = indent, key = key });
            }
        }
    }

    private static void ClassifyNode(VfxGraphNodeRecord node)
    {
        string typeText = string.Join(" ", new[]
        {
            node.runtimeType,
            node.scriptType,
            node.unityClassName,
            node.objectName,
            node.label,
            node.title
        }).ToLowerInvariant();

        bool hasFlowSlots = node.properties.Any(property =>
            property.path.Contains("m_InputFlowSlot", StringComparison.Ordinal) ||
            property.path.Contains("m_OutputFlowSlot", StringComparison.Ordinal));
        bool hasSlotProperty = node.properties.Any(property =>
            property.path.Contains("m_MasterSlot", StringComparison.Ordinal) ||
            property.path.Contains("m_LinkedSlots", StringComparison.Ordinal));

        if (node.unityClassName == "VisualEffectResource" ||
            typeText.Contains("visualeffectresource"))
        {
            node.category = "Resource";
        }
        else if (typeText.Contains("vfxui"))
        {
            node.category = "UI";
        }
        else if (typeText.Contains("vfxgraph") ||
                 node.properties.Any(property =>
                     property.path.EndsWith("m_GraphVersion", StringComparison.Ordinal)))
        {
            node.category = "Graph";
        }
        else if (typeText.Contains("vfxcontext") || hasFlowSlots)
        {
            node.category = "Context";
        }
        else if (typeText.Contains("vfxblock") ||
                 (node.parentLocalId != 0 &&
                  node.properties.Any(property =>
                      property.path.Contains("m_ActivationSlot", StringComparison.Ordinal))))
        {
            node.category = "Block";
        }
        else if (typeText.Contains("vfxdata") ||
                 (node.properties.Any(property =>
                      GetLeafName(property.path).Equals("m_Owners", StringComparison.OrdinalIgnoreCase)) &&
                  node.properties.Any(property =>
                      GetLeafName(property.path).Equals("capacity", StringComparison.OrdinalIgnoreCase) ||
                      GetLeafName(property.path).Equals("dataType", StringComparison.OrdinalIgnoreCase))))
        {
            node.category = "Data";
        }
        else if (typeText.Contains("vfxparameter") ||
                 node.properties.Any(property =>
                     property.path.Contains("m_ParameterInfo", StringComparison.Ordinal)))
        {
            node.category = "Parameter";
        }
        else if (typeText.Contains("vfxoperator") ||
                 (node.properties.Any(property =>
                      GetLeafName(property.path).Equals("m_InputSlots", StringComparison.OrdinalIgnoreCase) ||
                      GetLeafName(property.path).Equals("m_OutputSlots", StringComparison.OrdinalIgnoreCase)) &&
                  !node.properties.Any(property =>
                      property.path.Contains("m_ActivationSlot", StringComparison.Ordinal)) &&
                  !hasSlotProperty && !hasFlowSlots))
        {
            node.category = "Operator";
        }
        else if (typeText.Contains("vfxslot") || hasSlotProperty)
        {
            node.category = "Slot";
        }
        else
        {
            node.category = "Object";
        }

        node.stage = ClassifyStage(typeText);
        node.displayName = FirstNonEmpty(
            node.label,
            node.title,
            node.objectName,
            ShortTypeName(FirstNonEmpty(
                node.scriptType,
                node.runtimeType,
                node.unityClassName,
                "Object")));
    }

    private static string ClassifyStage(string typeText)
    {
        if (typeText.Contains("gpuevent"))
            return "GPU Event";
        if (typeText.Contains("outputevent"))
            return "Output Event";
        if (typeText.Contains("spawner") || typeText.Contains("spawn ") ||
            typeText.EndsWith(" spawn", StringComparison.Ordinal))
            return "Spawn";
        if (typeText.Contains("initialize") || typeText.Contains("init "))
            return "Initialize";
        if (typeText.Contains("update"))
            return "Update";
        if (typeText.Contains("output"))
            return "Output";
        return string.Empty;
    }

    private static void RefreshImportantSettings(VfxGraphNodeRecord node)
    {
        node.importantSettings.Clear();
        foreach (VfxSerializedPropertyRecord property in node.properties)
        {
            string leaf = GetLeafName(property.path);
            if (BoilerplatePropertyNames.Contains(leaf))
                continue;

            string lower = property.path.ToLowerInvariant();
            if (!ImportantSettingTokens.Any(token => lower.Contains(token.ToLowerInvariant())))
                continue;

            if (property.serializedType == "Generic" &&
                string.IsNullOrWhiteSpace(property.value))
                continue;

            node.importantSettings.Add(new VfxKeyValueRecord
            {
                key = property.path,
                value = Limit(property.value, 1000),
                source = property.serializedType == "YAML" ? "YAML" : "SerializedObject"
            });
        }

        node.importantSettings = node.importantSettings
            .GroupBy(item => item.key + "|" + item.value, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(160)
            .ToList();
    }

    private static ScriptTypeInfo ResolveScriptType(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return new ScriptTypeInfo();

        if (ScriptTypeCache.TryGetValue(guid, out ScriptTypeInfo cached))
            return cached;

        ScriptTypeInfo result = new();
        try
        {
            result.assetPath = AssetDatabase.GUIDToAssetPath(guid) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(result.assetPath))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                    result.assetPath);
                Type type = script?.GetClass();
                result.typeName = type?.FullName ??
                                  Path.GetFileNameWithoutExtension(result.assetPath);
            }
        }
        catch
        {
            // Keep the GUID even when the package script cannot be loaded.
        }

        ScriptTypeCache[guid] = result;
        return result;
    }

    internal static string ResolvePhysicalPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            string.Equals(assetPath, "Assets", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
        {
            try
            {
                UpmPackageInfo package = UpmPackageInfo.FindForAssetPath(assetPath);
                if (package != null && !string.IsNullOrWhiteSpace(package.resolvedPath))
                {
                    string prefix = package.assetPath.TrimEnd('/') + "/";
                    string relative = assetPath.StartsWith(prefix, StringComparison.Ordinal)
                        ? assetPath.Substring(prefix.Length)
                        : Path.GetFileName(assetPath);
                    return Path.GetFullPath(Path.Combine(package.resolvedPath, relative));
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ResolveVfxPackageVersion(string assetPath)
    {
        try
        {
            UpmPackageInfo package = UpmPackageInfo.FindForAssetPath(assetPath);
            if (package != null && package.name == "com.unity.visualeffectgraph")
                return package.version ?? string.Empty;

            UpmPackageInfo[] packages = UpmPackageInfo.GetAllRegisteredPackages();
            UpmPackageInfo vfx = packages?.FirstOrDefault(item =>
                item.name == "com.unity.visualeffectgraph");
            return vfx?.version ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string ComputeSha256(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;

        using FileStream stream = File.OpenRead(filePath);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return string.Concat(hash.Select(value => value.ToString("x2")));
    }

    private static string ComputeSha256Text(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        byte[] hash = sha.ComputeHash(bytes);
        return string.Concat(hash.Select(item => item.ToString("x2")));
    }

    internal static string NormalizePath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    internal static string SanitizeFileName(string value)
    {
        string result = value ?? string.Empty;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            result = result.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(result) ? "VFX_Object" : result;
    }

    internal static string Limit(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;
        return value.Substring(0, maxLength) + $"… <truncated {value.Length - maxLength} chars>";
    }

    private static string ClassifyReferenceDirection(string propertyPath)
    {
        string lower = (propertyPath ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("outputflowslot")) return "OutputFlow";
        if (lower.Contains("inputflowslot")) return "InputFlow";
        if (lower.Contains("linkedslot") || lower.Contains("connection")) return "SlotLink";
        if (lower.Contains("parent")) return "Parent";
        if (lower.Contains("children")) return "Child";
        if (lower.EndsWith("m_data") || lower.Contains(".m_data")) return "Data";
        return "Reference";
    }

    private static string ClassifyEdgeKind(string propertyPath)
    {
        string lower = (propertyPath ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("outputflowslot")) return "FlowOutput";
        if (lower.Contains("inputflowslot")) return "FlowInput";
        if (lower.Contains("linkedslot") || lower.Contains("connection") ||
            lower.Contains("m_links")) return "SlotLink";
        if (lower.Contains("inputslots") || lower.Contains("outputslots") ||
            lower.Contains("masterslot")) return "SlotOwnership";
        if (lower.EndsWith("m_parent") || lower.Contains(".m_parent")) return "Parent";
        if (lower.Contains("m_children")) return "Child";
        if (lower.EndsWith("m_data") || lower.Contains(".m_data")) return "DataBinding";
        if (lower.Contains("m_graph")) return "ResourceGraph";
        if (lower.Contains("m_uiinfos")) return "UIBinding";
        if (lower.Contains("subgraph")) return "Subgraph";
        return "Reference";
    }

    private static string ClassifyDependencyRole(string usage)
    {
        string lower = (usage ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("m_script")) return "Node Script Type";
        if (lower.Contains("subgraph")) return "VFX Subgraph";
        if (lower.Contains("shadergraph") || lower.Contains("shader graph")) return "Shader Graph";
        if (lower.Contains("shader")) return "Shader";
        if (lower.Contains("normal") && lower.Contains("texture")) return "Normal Texture";
        if (lower.Contains("texture") || lower.Contains("map")) return "Texture";
        if (lower.Contains("mesh")) return "Mesh";
        if (lower.Contains("material")) return "Material";
        if (lower.Contains("curve")) return "Curve";
        return "Serialized Dependency";
    }

    private static string DescribeUnityObject(UnityEngine.Object value)
    {
        if (value == null)
            return "null";

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                value,
                out string guid,
                out long localId))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return $"{value.GetType().FullName} '{value.name}' " +
                   $"(guid={guid}, localId={localId}, path={path})";
        }

        return $"{value.GetType().FullName} '{value.name}'";
    }

    private static string FormatVector2(Vector2 value) =>
        $"({F(value.x)}, {F(value.y)})";
    private static string FormatVector3(Vector3 value) =>
        $"({F(value.x)}, {F(value.y)}, {F(value.z)})";
    private static string FormatVector4(Vector4 value) =>
        $"({F(value.x)}, {F(value.y)}, {F(value.z)}, {F(value.w)})";
    private static string FormatQuaternion(Quaternion value) =>
        $"({F(value.x)}, {F(value.y)}, {F(value.z)}, {F(value.w)})";
    private static string FormatColor(Color value) =>
        $"RGBA({F(value.r)}, {F(value.g)}, {F(value.b)}, {F(value.a)})";
    private static string FormatRect(Rect value) =>
        $"Rect(x={F(value.x)}, y={F(value.y)}, w={F(value.width)}, h={F(value.height)})";
    private static string FormatBounds(Bounds value) =>
        $"Bounds(center={FormatVector3(value.center)}, size={FormatVector3(value.size)})";

    private static string FormatAnimationCurve(AnimationCurve curve)
    {
        if (curve == null)
            return "null";
        return "[" + string.Join(", ", curve.keys.Select(key =>
            $"{{t={F(key.time)},v={F(key.value)},in={F(key.inTangent)},out={F(key.outTangent)}}}")) + "]";
    }

    private static string FormatGradient(Gradient gradient)
    {
        if (gradient == null)
            return "null";
        string colors = string.Join(", ", gradient.colorKeys.Select(key =>
            $"{{t={F(key.time)},c={FormatColor(key.color)}}}"));
        string alphas = string.Join(", ", gradient.alphaKeys.Select(key =>
            $"{{t={F(key.time)},a={F(key.alpha)}}}"));
        return $"colors=[{colors}] alpha=[{alphas}] mode={gradient.mode}";
    }

    private static string F(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static int ReadIntSetting(VfxGraphNodeRecord node, string leafName)
    {
        string value = ReadStringSetting(node, leafName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int parsed) ? parsed : 0;
    }

    private static float ReadFloatSetting(VfxGraphNodeRecord node, string leafName)
    {
        string value = ReadStringSetting(node, leafName);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
            out float parsed) ? parsed : 0f;
    }

    private static string ReadStringSetting(VfxGraphNodeRecord node, string leafName)
    {
        if (node == null)
            return string.Empty;
        VfxSerializedPropertyRecord property = node.properties.LastOrDefault(item =>
            GetLeafName(item.path).Equals(leafName, StringComparison.OrdinalIgnoreCase));
        return property?.value ?? string.Empty;
    }

    private static string DeriveSystemName(IEnumerable<VfxGraphNodeRecord> contexts)
    {
        List<string> labels = contexts
            .Select(context => FirstNonEmpty(context.label, context.displayName))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        foreach (string label in labels)
        {
            string cleaned = Regex.Replace(
                label,
                @"\b(spawn|initialize|init|update|output|particle|context)\b",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ', '-', '_');
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return labels.FirstOrDefault() ?? "VFX System";
    }

    private static int StageRank(string stage)
    {
        return stage switch
        {
            "Spawn" => 0,
            "GPU Event" => 1,
            "Initialize" => 2,
            "Update" => 3,
            "Output" => 4,
            "Output Event" => 5,
            _ => 10
        };
    }

    private static int CountLeadingSpaces(string line)
    {
        int count = 0;
        while (count < line.Length && line[count] == ' ')
            count++;
        return count;
    }

    private static int FindYamlKeyColon(string text)
    {
        bool inSingle = false;
        bool inDouble = false;
        int braceDepth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle && (i == 0 || text[i - 1] != '\\')) inDouble = !inDouble;
            else if (!inSingle && !inDouble)
            {
                if (c == '{' || c == '[') braceDepth++;
                else if (c == '}' || c == ']') braceDepth = Math.Max(0, braceDepth - 1);
                else if (c == ':' && braceDepth == 0) return i;
            }
        }
        return -1;
    }

    private static int[] BuildLineStartIndex(string text)
    {
        List<int> values = new() { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && i + 1 < text.Length)
                values.Add(i + 1);
        }
        return values.ToArray();
    }

    private static int FindLineNumber(int[] lineStarts, int index)
    {
        int position = Array.BinarySearch(lineStarts, index);
        if (position < 0)
            position = ~position - 1;
        return Math.Max(0, position) + 1;
    }

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int parsed) ? parsed : 0;

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out long parsed) ? parsed : 0L;

    private static string GetLeafName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        int dot = path.LastIndexOf('.');
        return dot >= 0 ? path.Substring(dot + 1) : path;
    }

    private static string JoinPath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left)) return right ?? string.Empty;
        if (string.IsNullOrWhiteSpace(right)) return left;
        return left + "." + right;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string ShortTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        int comma = value.IndexOf(',');
        if (comma >= 0) value = value.Substring(0, comma);
        int dot = value.LastIndexOf('.');
        return dot >= 0 ? value.Substring(dot + 1) : value;
    }

    private static string NormalizeNewLines(string value) =>
        (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private static string Unquote(string value)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length >= 2 &&
            ((result[0] == '\'' && result[^1] == '\'') ||
             (result[0] == '"' && result[^1] == '"')))
        {
            result = result.Substring(1, result.Length - 2);
        }
        return result;
    }

    private static string AfterColon(string value)
    {
        int index = value.IndexOf(':');
        return index >= 0 ? value.Substring(index + 1).Trim() : string.Empty;
    }

    private static bool TryParseInlineVector2(string value, out Vector2 result)
    {
        Match match = Regex.Match(
            value ?? string.Empty,
            @"\{\s*x:\s*(?<x>[-+0-9.eE]+)\s*,\s*y:\s*(?<y>[-+0-9.eE]+)\s*\}");
        if (match.Success &&
            float.TryParse(match.Groups["x"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(match.Groups["y"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float y))
        {
            result = new Vector2(x, y);
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryParseBoundsString(string value, out Bounds bounds)
    {
        Match match = Regex.Match(
            value ?? string.Empty,
            @"center=\((?<cx>[-+0-9.eE]+),\s*(?<cy>[-+0-9.eE]+),\s*(?<cz>[-+0-9.eE]+)\),\s*size=\((?<sx>[-+0-9.eE]+),\s*(?<sy>[-+0-9.eE]+),\s*(?<sz>[-+0-9.eE]+)\)");
        return TryCreateBounds(match, "cx", "cy", "cz", "sx", "sy", "sz", out bounds);
    }

    private static bool TryParseAaBoxJson(string value, out Bounds bounds)
    {
        string normalized = (value ?? string.Empty).Replace("\\\"", "\"");
        Match match = AaBoxJsonRegex.Match(normalized);
        return TryCreateBounds(match, "cx", "cy", "cz", "sx", "sy", "sz", out bounds);
    }

    private static bool TryCreateBounds(
        Match match,
        string cxName,
        string cyName,
        string czName,
        string sxName,
        string syName,
        string szName,
        out Bounds bounds)
    {
        if (match.Success &&
            TryFloat(match.Groups[cxName].Value, out float cx) &&
            TryFloat(match.Groups[cyName].Value, out float cy) &&
            TryFloat(match.Groups[czName].Value, out float cz) &&
            TryFloat(match.Groups[sxName].Value, out float sx) &&
            TryFloat(match.Groups[syName].Value, out float sy) &&
            TryFloat(match.Groups[szName].Value, out float sz))
        {
            bounds = new Bounds(
                new Vector3(cx, cy, cz),
                new Vector3(Mathf.Abs(sx), Mathf.Abs(sy), Mathf.Abs(sz)));
            return bounds.size.sqrMagnitude > 0.000001f;
        }

        bounds = default;
        return false;
    }

    private static bool TryFloat(string value, out float parsed) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    private static VfxBoundsRecord ToBoundsRecord(Bounds bounds, string source) =>
        new()
        {
            valid = true,
            centerX = bounds.center.x,
            centerY = bounds.center.y,
            centerZ = bounds.center.z,
            sizeX = bounds.size.x,
            sizeY = bounds.size.y,
            sizeZ = bounds.size.z,
            source = source
        };

    private static void ApplyRectScalar(
        VfxYamlGroupRecord target,
        string trimmed,
        bool active)
    {
        if (!active || target == null) return;
        if (TryRectField(trimmed, "x", out float x)) target.x = x;
        else if (TryRectField(trimmed, "y", out float y)) target.y = y;
        else if (TryRectField(trimmed, "width", out float width)) target.width = width;
        else if (TryRectField(trimmed, "height", out float height)) target.height = height;
    }

    private static void ApplyRectScalar(
        VfxYamlStickyNoteRecord target,
        string trimmed,
        bool active)
    {
        if (!active || target == null) return;
        if (TryRectField(trimmed, "x", out float x)) target.x = x;
        else if (TryRectField(trimmed, "y", out float y)) target.y = y;
        else if (TryRectField(trimmed, "width", out float width)) target.width = width;
        else if (TryRectField(trimmed, "height", out float height)) target.height = height;
    }

    private static bool TryRectField(string line, string field, out float value)
    {
        string prefix = field + ":";
        if (line.StartsWith(prefix, StringComparison.Ordinal) &&
            float.TryParse(line.Substring(prefix.Length).Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        value = 0f;
        return false;
    }

    private sealed class DependencyAccumulator
    {
        public string guid = string.Empty;
        public string assetPath = string.Empty;
        public bool direct;
        public bool recursive;
        public bool fromYaml;
        public bool fromSerialized;
        public HashSet<string> roles = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> usages = new(StringComparer.Ordinal);
    }

    private sealed class ScriptTypeInfo
    {
        public string assetPath = string.Empty;
        public string typeName = string.Empty;
    }

    private sealed class YamlPathEntry
    {
        public int indent;
        public string key = string.Empty;
    }
}

internal sealed class VfxAnalysisResult
{
    public VfxAiReferenceReport report;
    public string sourceAssetPhysicalPath = string.Empty;
    public string rawYaml = string.Empty;
    public List<YamlDocument> yamlDocuments = new();
}

internal sealed class YamlDocument
{
    public int unityClassId;
    public long localId;
    public string unityClassName = string.Empty;
    public string scriptGuid = string.Empty;
    public string objectName = string.Empty;
    public string label = string.Empty;
    public string title = string.Empty;
    public long parentLocalId;
    public List<long> childLocalIds = new();
    public float positionX;
    public float positionY;
    public bool hasPosition;
    public int startLine;
    public int endLine;
    public string rawText = string.Empty;
    public List<YamlScalar> scalars = new();
    public List<YamlReference> references = new();
}

internal sealed class YamlScalar
{
    public string path = string.Empty;
    public string value = string.Empty;
    public int depth;
    public bool isListItem;
    public int lineNumber;
}

internal sealed class YamlReference
{
    public string path = string.Empty;
    public long fileId;
    public string guid = string.Empty;
    public int type;
    public int lineNumber;
}
#endif
