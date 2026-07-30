#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;

public static class ProjectAbyssVFXReferenceExporter
{
    private const string MenuPath =
        "Tools/Project Abyss/Exports/Export Selected VFX as AI Reference";

    private const BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [MenuItem(MenuPath, false, 50)]
    public static void ExportSelected()
    {
        List<VisualEffectAsset> assets = Selection.objects
            .OfType<VisualEffectAsset>()
            .Distinct()
            .ToList();

        if (assets.Count == 0 && Selection.activeObject is VisualEffectAsset active)
            assets.Add(active);

        if (assets.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "VFX Reference Export",
                "Project 창에서 하나 이상의 .vfx 에셋을 선택하세요.",
                "확인");
            return;
        }

        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        string root = Path.Combine(
            projectRoot,
            "AllDataTXT",
            "VFX",
            "References");
        Directory.CreateDirectory(root);

        List<string> packages = new();

        try
        {
            for (int i = 0; i < assets.Count; i++)
            {
                VisualEffectAsset asset = assets[i];
                EditorUtility.DisplayProgressBar(
                    "VFX AI Reference Export",
                    asset.name,
                    assets.Count == 0 ? 1f : (float)i / assets.Count);

                packages.Add(ExportOne(asset, root));
            }

            string message =
                $"{packages.Count}개의 VFX Reference Package를 생성했습니다.\n\n" +
                string.Join("\n", packages.Select(Normalize));

            Debug.Log("[ProjectAbyssVFXReferenceExporter]\n" + message);
            EditorUtility.DisplayDialog(
                "VFX Reference Export Complete",
                message,
                "확인");

            if (packages.Count > 0)
                EditorUtility.RevealInFinder(packages[0]);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "VFX Reference Export Failed",
                exception.Message,
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateExportSelected()
    {
        return Selection.objects.OfType<VisualEffectAsset>().Any() ||
               Selection.activeObject is VisualEffectAsset;
    }

    private static string ExportOne(
        VisualEffectAsset asset,
        string root)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        string safeName = Sanitize(asset.name);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(root, $"{safeName}_{stamp}");
        Directory.CreateDirectory(folder);

        VfxReferenceReport report = BuildReport(asset, assetPath);
        string json = JsonUtility.ToJson(report, true);
        File.WriteAllText(
            Path.Combine(folder, "VFX_REFERENCE.json"),
            json,
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(folder, "VFX_REFERENCE.txt"),
            BuildReadableText(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(folder, "CLONE_TEMPLATE_RECIPE.json"),
            JsonUtility.ToJson(
                CreateCloneRecipe(report),
                true),
            new UTF8Encoding(false));

        string absoluteAsset = ToAbsoluteAssetPath(assetPath);
        if (File.Exists(absoluteAsset))
        {
            File.Copy(
                absoluteAsset,
                Path.Combine(folder, safeName + ".vfx.txt"),
                true);
        }

        string metaPath = absoluteAsset + ".meta";
        if (File.Exists(metaPath))
        {
            File.Copy(
                metaPath,
                Path.Combine(folder, safeName + ".vfx.meta.txt"),
                true);
        }

        WriteDependencyReferences(report, folder);
        WritePreview(asset, Path.Combine(folder, "PREVIEW.png"));
        WritePrompt(report, Path.Combine(folder, "AI_REFERENCE_PROMPT.txt"));

        string zipPath = folder + ".zip";
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(
            folder,
            zipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);

        return zipPath;
    }

    private static CloneTemplateRecipe CreateCloneRecipe(
        VfxReferenceReport report)
    {
        return new CloneTemplateRecipe
        {
            schemaVersion = "2.0",
            name = Sanitize(report.assetName) + "_Variant",
            description =
                "Exported reference VFX를 전체 그래프 보존 방식으로 복제",
            buildMode = "CloneTemplate",
            templateAssetPath = report.assetPath,
            preserveTemplateGraph = true,
            requiredCapabilities =
                new List<string>(report.detectedCapabilities)
        };
    }

    private static VfxReferenceReport BuildReport(
        VisualEffectAsset asset,
        string assetPath)
    {
        VfxReferenceReport report = new()
        {
            formatVersion = "2.0",
            generatedAtLocal = DateTimeOffset.Now.ToString("O"),
            unityVersion = Application.unityVersion,
            assetName = asset.name,
            assetPath = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath)
        };

        List<VFXExposedProperty> exposed = new();
        asset.GetExposedProperties(exposed);
        foreach (VFXExposedProperty property in exposed)
        {
            report.exposedProperties.Add(new ExposedPropertyRecord
            {
                name = property.name,
                type = property.type?.FullName ?? string.Empty
            });
        }

        List<string> events = new();
        asset.GetEvents(events);
        report.events = events
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        foreach (string dependency in AssetDatabase.GetDependencies(assetPath, false)
                     .Where(path => path != assetPath)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(dependency);
            report.dependencies.Add(new DependencyRecord
            {
                assetPath = dependency,
                guid = AssetDatabase.AssetPathToGUID(dependency),
                type = type?.FullName ?? string.Empty
            });
        }

        try
        {
            object graph = ResolveGraphObject(asset, assetPath, out string graphWarning);
            if (graph != null)
            {
                int nextId = 0;
                foreach (object child in EnumerateChildren(graph))
                {
                    if (!IsVfxModelObject(child))
                        continue;

                    report.models.Add(
                        BuildModelRecord(child, -1, ref nextId));
                }
            }
            else if (!string.IsNullOrWhiteSpace(graphWarning))
            {
                report.warnings.Add(graphWarning);
            }
        }
        catch (Exception exception)
        {
            report.warnings.Add(
                "Graph topology reflection failed: " + exception.Message);
        }

        HashSet<string> capabilities = new(StringComparer.OrdinalIgnoreCase);
        List<ModelRecord> flatModels = Flatten(report.models).ToList();
        foreach (ModelRecord model in flatModels)
        {
            DetectCapabilities(model.type + " " + model.label, capabilities);
            foreach (NamedValueRecord setting in model.settings)
            {
                DetectCapabilities(
                    setting.name + " " + setting.type + " " + setting.value,
                    capabilities);
            }
        }

        foreach (string dependency in report.dependencies.Select(x => x.assetPath))
            DetectCapabilities(dependency, capabilities);

        foreach (ExposedPropertyRecord property in report.exposedProperties)
            DetectCapabilities(property.name + " " + property.type, capabilities);

        int outputContextCount = flatModels.Count(model =>
            model.type?.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0);
        if (outputContextCount > 1)
            capabilities.Add("Multiple Outputs");

        if (report.exposedProperties.Any(property =>
                property.type?.IndexOf("Mesh", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            capabilities.Add("Exposed Mesh");
        }

        if (report.exposedProperties.Any(property =>
                property.type?.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            capabilities.Add("Exposed Texture");
        }

        report.detectedCapabilities = capabilities
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return report;
    }

    private static object ResolveGraphObject(
        VisualEffectAsset asset,
        string assetPath,
        out string warning)
    {
        warning = string.Empty;
        if (asset == null)
        {
            warning = "Graph topology reflection skipped: VisualEffectAsset is null.";
            return null;
        }

        // VFX Graph 17.x의 VFXGraph/VFXResource는 패키지 내부 타입이다.
        // 컴파일 타임 타입 참조 대신 Reflection과 sub-asset 탐색으로 접근한다.
        object resource =
            InvokeNoArgumentMember(asset, "GetResource") ??
            ReadMember(asset, "resource") ??
            ReadMember(asset, "m_Resource");

        object graph = ResolveGraphFromCandidate(resource);
        if (graph != null)
            return graph;

        graph = ResolveGraphFromCandidate(asset);
        if (graph != null)
            return graph;

        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            foreach (UnityEngine.Object candidate in
                     AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (candidate == null)
                    continue;

                if (IsTypeOrBaseType(
                        candidate.GetType(),
                        "UnityEditor.VFX.VFXGraph"))
                {
                    return candidate;
                }

                graph = ResolveGraphFromCandidate(candidate);
                if (graph != null)
                    return graph;
            }
        }

        warning =
            "Graph topology could not be opened through the public Unity API. " +
            "The exporter still wrote the original .vfx YAML, exposed properties, " +
            "events, dependencies and previews.";
        return null;
    }

    private static object ResolveGraphFromCandidate(object candidate)
    {
        if (candidate == null)
            return null;

        if (IsTypeOrBaseType(
                candidate.GetType(),
                "UnityEditor.VFX.VFXGraph"))
        {
            return candidate;
        }

        object graph =
            InvokeNoArgumentMember(candidate, "GetOrCreateGraph") ??
            InvokeNoArgumentMember(candidate, "GetGraph") ??
            ReadMember(candidate, "graph") ??
            ReadMember(candidate, "m_Graph");

        return graph != null &&
               IsTypeOrBaseType(
                   graph.GetType(),
                   "UnityEditor.VFX.VFXGraph")
            ? graph
            : null;
    }

    private static object InvokeNoArgumentMember(
        object owner,
        string methodName)
    {
        if (owner == null || string.IsNullOrWhiteSpace(methodName))
            return null;

        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                InstanceFlags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method == null)
                continue;

            try
            {
                return method.Invoke(owner, null);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static ModelRecord BuildModelRecord(
        object model,
        int parentId,
        ref int nextId)
    {
        int id = nextId++;
        ModelRecord record = new()
        {
            id = id,
            parentId = parentId,
            type = model.GetType().FullName,
            label = ReadStringMember(model, "label")
        };

        object position = ReadMember(model, "position");
        if (position is Vector2 vector2)
        {
            record.positionX = vector2.x;
            record.positionY = vector2.y;
        }

        foreach (FieldInfo field in EnumerateFields(model.GetType()))
        {
            if (field.IsStatic || field.Name.Contains("cached", StringComparison.OrdinalIgnoreCase))
                continue;

            object value;
            try
            {
                value = field.GetValue(model);
            }
            catch
            {
                continue;
            }

            if (!TryFormatValue(value, out string formatted))
                continue;

            record.settings.Add(new NamedValueRecord
            {
                name = field.Name,
                value = formatted,
                type = field.FieldType.FullName
            });
        }

        foreach (object slot in EnumerateMember(model, "inputSlots"))
        {
            if (IsVfxSlotObject(slot))
                record.inputSlots.Add(BuildSlotRecord(slot));
        }

        foreach (object slot in EnumerateMember(model, "outputSlots"))
        {
            if (IsVfxSlotObject(slot))
                record.outputSlots.Add(BuildSlotRecord(slot));
        }

        foreach (object child in EnumerateChildren(model))
        {
            if (IsVfxModelObject(child) && !IsVfxSlotObject(child))
                record.children.Add(BuildModelRecord(child, id, ref nextId));
        }

        return record;
    }

    private static SlotRecord BuildSlotRecord(object slot)
    {
        SlotRecord record = new()
        {
            name = ReadStringMember(slot, "name"),
            type = slot?.GetType().FullName ?? string.Empty
        };

        try
        {
            object rawValue = ReadMember(slot, "value") ??
                              ReadMember(slot, "m_Value");
            if (TryFormatValue(rawValue, out string value))
                record.value = value;
        }
        catch (Exception exception)
        {
            record.value = "<unreadable: " + exception.Message + ">";
        }

        foreach (object child in EnumerateChildren(slot))
        {
            if (IsVfxSlotObject(child))
                record.children.Add(BuildSlotRecord(child));
        }

        foreach (object linked in EnumerateLinkedSlots(slot))
        {
            if (!IsVfxSlotObject(linked))
                continue;

            string descriptor = DescribeSlotLink(linked);
            if (!string.IsNullOrWhiteSpace(descriptor) &&
                !record.links.Contains(descriptor))
            {
                record.links.Add(descriptor);
            }
        }

        return record;
    }

    private static IEnumerable<object> EnumerateLinkedSlots(object slot)
    {
        if (slot == null)
            yield break;

        HashSet<object> seen =
            new(ReferenceEqualityComparer.Instance);

        foreach (string memberName in new[]
                 {
                     "links", "linkedSlots", "m_LinkedSlots",
                     "m_Links", "connections", "m_Connections"
                 })
        {
            foreach (object value in EnumerateMember(slot, memberName))
            {
                if (value == null)
                    continue;

                if (IsVfxSlotObject(value))
                {
                    if (seen.Add(value))
                        yield return value;
                    continue;
                }

                object candidate =
                    ReadMember(value, "slot") ??
                    ReadMember(value, "otherSlot") ??
                    ReadMember(value, "linkedSlot") ??
                    ReadMember(value, "m_Slot");

                if (IsVfxSlotObject(candidate) &&
                    seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static string DescribeSlotLink(object slot)
    {
        if (slot == null)
            return string.Empty;

        object owner = ReadMember(slot, "owner") ??
                       ReadMember(slot, "m_Owner") ??
                       ReadMember(slot, "parent") ??
                       ReadMember(slot, "m_Parent");

        HashSet<object> visited =
            new(ReferenceEqualityComparer.Instance);
        while (IsVfxSlotObject(owner) && visited.Add(owner))
        {
            owner = ReadMember(owner, "owner") ??
                    ReadMember(owner, "m_Owner") ??
                    ReadMember(owner, "parent") ??
                    ReadMember(owner, "m_Parent");
        }

        string ownerType = owner?.GetType().FullName ?? string.Empty;
        string ownerLabel = ReadStringMember(owner, "label");
        string slotName = ReadStringMember(slot, "name");

        if (string.IsNullOrWhiteSpace(slotName))
            slotName = slot?.GetType().Name ?? string.Empty;

        return $"{ownerType} | {ownerLabel} | {slotName}";
    }

    private static bool IsVfxModelObject(object value)
    {
        return IsTypeOrBaseType(
            value?.GetType(),
            "UnityEditor.VFX.VFXModel");
    }

    private static bool IsVfxSlotObject(object value)
    {
        return IsTypeOrBaseType(
            value?.GetType(),
            "UnityEditor.VFX.VFXSlot");
    }

    private static bool IsTypeOrBaseType(
        Type type,
        string expectedFullName)
    {
        while (type != null)
        {
            if (string.Equals(
                    type.FullName,
                    expectedFullName,
                    StringComparison.Ordinal))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    private static IEnumerable<object> EnumerateChildren(object owner)
    {
        HashSet<object> seen =
            new(ReferenceEqualityComparer.Instance);

        foreach (string memberName in new[] { "children", "m_Children" })
        {
            foreach (object value in EnumerateMember(owner, memberName))
            {
                if (value != null && seen.Add(value))
                    yield return value;
            }
        }
    }

    private static IEnumerable<object> EnumerateMember(
        object owner,
        string name)
    {
        object value = ReadMember(owner, name);
        if (value is not IEnumerable enumerable || value is string)
            yield break;

        foreach (object item in enumerable)
            yield return item;
    }

    private static object ReadMember(object owner, string name)
    {
        if (owner == null)
            return null;

        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(name, InstanceFlags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try { return property.GetValue(owner); }
                catch { }
            }

            FieldInfo field = type.GetField(name, InstanceFlags);
            if (field != null)
            {
                try { return field.GetValue(owner); }
                catch { }
            }
        }

        return null;
    }

    private static string ReadStringMember(object owner, string name)
    {
        return ReadMember(owner, name)?.ToString() ?? string.Empty;
    }

    private static IEnumerable<FieldInfo> EnumerateFields(Type type)
    {
        HashSet<string> seen = new();
        for (Type current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(InstanceFlags))
            {
                string key = current.FullName + "." + field.Name;
                if (seen.Add(key))
                    yield return field;
            }
        }
    }

    private static bool TryFormatValue(object value, out string formatted)
    {
        formatted = string.Empty;
        if (value == null)
        {
            formatted = "null";
            return true;
        }

        Type type = value.GetType();
        if (type.IsEnum || type.IsPrimitive || value is decimal ||
            value is string || value is Vector2 || value is Vector3 ||
            value is Vector4 || value is Color || value is Bounds)
        {
            formatted = value.ToString();
            return true;
        }

        if (value is UnityEngine.Object unityObject)
        {
            formatted =
                $"{unityObject.name} | {AssetDatabase.GetAssetPath(unityObject)}";
            return true;
        }

        return false;
    }

    private static IEnumerable<ModelRecord> Flatten(
        IEnumerable<ModelRecord> roots)
    {
        foreach (ModelRecord root in roots)
        {
            yield return root;
            foreach (ModelRecord child in Flatten(root.children))
                yield return child;
        }
    }

    private static void DetectCapabilities(
        string text,
        ISet<string> result)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        AddIfContains(text, result, "Spawner", "Spawn Context");
        AddIfContains(text, result, "Initialize", "Initialize Context");
        AddIfContains(text, result, "Update", "Update Context");
        AddIfContains(text, result, "Output", "Output Context");
        AddIfContains(text, result, "capacity", "Capacity");
        AddIfContains(text, result, "Bounds", "Bounds");
        AddIfContains(text, result, "FaceCamera", "Orient Face Camera");
        AddIfContains(text, result, "FixedAxis", "Orient Fixed Axis");
        AddIfContains(text, result, "Orient", "Orient Advanced");
        AddIfContains(text, result, "Angle", "Rotation Angle");
        AddIfContains(text, result, "Angular", "Angular Velocity");
        AddIfContains(text, result, "TexIndex", "TexIndex");
        AddIfContains(text, result, "Flipbook", "Flipbook");
        AddIfContains(text, result, "FlipbookBlend", "Flipbook Blending");
        AddIfContains(text, result, "frameBlending", "Flipbook Blending");
        AddIfContains(text, result, "Pivot", "Pivot");
        AddIfContains(text, result, "SampleMesh", "Sample Mesh");
        AddIfContains(text, result, "SampleTexture", "Sample Texture 2D");
        AddIfContains(text, result, "Texture2D", "Sample Texture 2D");
        AddIfContains(text, result, "SDF", "Signed Distance Field");
        AddIfContains(text, result, "SignedDistance", "Signed Distance Field");
        AddIfContains(text, result, "Skinned", "Sampled Skinned Mesh");
        AddIfContains(text, result, "Collision", "Collision");
        AddIfContains(text, result, "TriggerEvent", "Trigger Event on Collide");
        AddIfContains(text, result, "Trigger Event", "Trigger Event on Collide");
        AddIfContains(text, result, "GPUEvent", "GPU Event");
        AddIfContains(text, result, "GPU Event", "GPU Event");
        AddIfContains(text, result, "Decal", "Decal Particles");
        AddIfContains(text, result, "Strip", "Particle Strip");
        AddIfContains(text, result, "MultiStrip", "Multi-Strip");
        AddIfContains(text, result, "Burst", "Burst Spawn");
        AddIfContains(text, result, "ConstantRate", "Spawn Rate");
        AddIfContains(text, result, "MeshOutput", "Mesh Output");
        AddIfContains(text, result, "Mesh", "Mesh");
        AddIfContains(text, result, "Texture", "Texture");
        AddIfContains(text, result, "ShaderGraph", "Shader Graph Output");
        AddIfContains(text, result, "VFXShaderGraph", "Shader Graph Output");
    }

    private static void AddIfContains(
        string text,
        ISet<string> result,
        string needle,
        string capability)
    {
        if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            result.Add(capability);
    }

    private static string BuildReadableText(VfxReferenceReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("PROJECT ABYSS VFX AI REFERENCE");
        builder.AppendLine($"Asset: {report.assetPath}");
        builder.AppendLine($"Unity: {report.unityVersion}");
        builder.AppendLine($"Capabilities: {string.Join(", ", report.detectedCapabilities)}");
        builder.AppendLine();
        builder.AppendLine("EXPOSED PROPERTIES");
        foreach (ExposedPropertyRecord property in report.exposedProperties)
            builder.AppendLine($"- {property.name}: {property.type}");
        builder.AppendLine();
        builder.AppendLine("EVENTS");
        foreach (string eventName in report.events)
            builder.AppendLine("- " + eventName);
        builder.AppendLine();
        builder.AppendLine("DEPENDENCIES");
        foreach (DependencyRecord dependency in report.dependencies)
            builder.AppendLine($"- {dependency.type} | {dependency.assetPath}");
        builder.AppendLine();
        builder.AppendLine("GRAPH TOPOLOGY");
        foreach (ModelRecord model in report.models)
            AppendModel(builder, model, 0);
        return builder.ToString();
    }

    private static void AppendModel(
        StringBuilder builder,
        ModelRecord model,
        int depth)
    {
        string indent = new(' ', depth * 2);
        builder.AppendLine(
            $"{indent}- [{model.id}] {model.type} | {model.label} | ({model.positionX}, {model.positionY})");

        foreach (NamedValueRecord setting in model.settings)
            builder.AppendLine($"{indent}  setting {setting.name} = {setting.value}");
        foreach (SlotRecord slot in model.inputSlots)
            AppendSlot(builder, slot, indent + "  input ");
        foreach (SlotRecord slot in model.outputSlots)
            AppendSlot(builder, slot, indent + "  output ");
        foreach (ModelRecord child in model.children)
            AppendModel(builder, child, depth + 1);
    }

    private static void AppendSlot(
        StringBuilder builder,
        SlotRecord slot,
        string prefix)
    {
        builder.AppendLine($"{prefix}{slot.name} = {slot.value} ({slot.type})");
        foreach (string link in slot.links)
            builder.AppendLine($"{prefix}  -> {link}");
        foreach (SlotRecord child in slot.children)
            AppendSlot(builder, child, prefix + "  ");
    }

    private static void WriteDependencyReferences(
        VfxReferenceReport report,
        string outputFolder)
    {
        string dependencyFolder =
            Path.Combine(outputFolder, "Dependencies");
        string previewFolder =
            Path.Combine(outputFolder, "DependencyPreviews");

        Directory.CreateDirectory(dependencyFolder);
        Directory.CreateDirectory(previewFolder);

        foreach (DependencyRecord dependency in report.dependencies)
        {
            UnityEngine.Object asset =
                AssetDatabase.LoadMainAssetAtPath(dependency.assetPath);
            if (asset == null)
                continue;

            string safeName =
                Sanitize(Path.GetFileNameWithoutExtension(
                    dependency.assetPath));
            string idPrefix = string.IsNullOrWhiteSpace(dependency.guid)
                ? ComputeStableSuffix(dependency.assetPath)
                : dependency.guid.Substring(0, Mathf.Min(8, dependency.guid.Length));
            string outputBaseName = idPrefix + "_" + safeName;
            string extension =
                Path.GetExtension(dependency.assetPath);

            string absolute =
                ToAbsoluteAssetPath(dependency.assetPath);

            if (IsReadableTextExtension(extension) &&
                File.Exists(absolute))
            {
                File.Copy(
                    absolute,
                    Path.Combine(
                        dependencyFolder,
                        outputBaseName + extension + ".txt"),
                    true);
            }

            string dependencyMeta = absolute + ".meta";
            if (File.Exists(dependencyMeta))
            {
                File.Copy(
                    dependencyMeta,
                    Path.Combine(
                        dependencyFolder,
                        outputBaseName + extension + ".meta.txt"),
                    true);
            }

            WritePreview(
                asset,
                Path.Combine(
                    previewFolder,
                    outputBaseName + ".png"));
        }
    }

    private static bool IsReadableTextExtension(
        string extension)
    {
        return extension.Equals(".vfx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".shadersubgraph", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".shader", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".hlsl", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".compute", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".asset", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePrompt(
        VfxReferenceReport report,
        string outputPath)
    {
        string prompt =
            "첨부된 VFX_REFERENCE.json, VFX_REFERENCE.txt, 원본 .vfx YAML, 의존 에셋 목록과 PREVIEW.png를 함께 분석하라.\n" +
            "이 효과의 Context/Block/Operator/Output/Asset 연결과 시간 흐름을 설명하고, Project Abyss VFX Recipe 2.0에서 CloneTemplate 모드로 변형할 JSON을 작성하라.\n" +
            "원본 기능(Strip, GPU Event, Collision, Decal, SDF, Mesh, Shader Graph 등)은 삭제하지 말고 유지하라.\n\n" +
            $"Reference Asset: {report.assetPath}\n" +
            $"Detected Capabilities: {string.Join(", ", report.detectedCapabilities)}\n";

        File.WriteAllText(outputPath, prompt, new UTF8Encoding(false));
    }

    private static void WritePreview(
        UnityEngine.Object asset,
        string path)
    {
        Texture2D preview = AssetPreview.GetAssetPreview(asset) ??
                            AssetPreview.GetMiniThumbnail(asset);
        if (preview == null)
            return;

        Texture2D readable = null;
        RenderTexture temporary = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            readable = new Texture2D(
                preview.width,
                preview.height,
                TextureFormat.RGBA32,
                false);
            temporary = RenderTexture.GetTemporary(
                preview.width,
                preview.height,
                0,
                RenderTextureFormat.ARGB32);
            Graphics.Blit(preview, temporary);
            RenderTexture.active = temporary;
            readable.ReadPixels(
                new Rect(0, 0, preview.width, preview.height),
                0,
                0);
            readable.Apply();
            File.WriteAllBytes(path, readable.EncodeToPNG());
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[VFX Reference Export] Preview 저장 실패: " +
                exception.Message);
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

    private static string ComputeStableSuffix(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "VFX_Reference" : value;
    }

    private static string Normalize(string value) =>
        value.Replace('\\', '/');

    private sealed class ReferenceEqualityComparer :
        IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object x, object y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(object obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers
                .GetHashCode(obj);
    }

    [Serializable]
    private sealed class CloneTemplateRecipe
    {
        public string schemaVersion;
        public string name;
        public string description;
        public string buildMode;
        public string templateAssetPath;
        public bool preserveTemplateGraph;
        public List<string> requiredCapabilities = new();
        public List<CloneTemplateSystemPlaceholder> systems = new();
        public List<CloneTemplateSceneObjectPlaceholder> sceneObjects = new();
        public List<CloneTemplateOverridePlaceholder> exposedOverrides = new();
    }

    [Serializable]
    private sealed class CloneTemplateSystemPlaceholder
    {
    }

    [Serializable]
    private sealed class CloneTemplateSceneObjectPlaceholder
    {
    }

    [Serializable]
    private sealed class CloneTemplateOverridePlaceholder
    {
    }

    [Serializable]
    private sealed class VfxReferenceReport
    {
        public string formatVersion;
        public string generatedAtLocal;
        public string unityVersion;
        public string assetName;
        public string assetPath;
        public string guid;
        public List<string> detectedCapabilities = new();
        public List<ExposedPropertyRecord> exposedProperties = new();
        public List<string> events = new();
        public List<DependencyRecord> dependencies = new();
        public List<ModelRecord> models = new();
        public List<string> warnings = new();
    }

    [Serializable]
    private sealed class ExposedPropertyRecord
    {
        public string name;
        public string type;
    }

    [Serializable]
    private sealed class DependencyRecord
    {
        public string assetPath;
        public string guid;
        public string type;
    }

    [Serializable]
    private sealed class ModelRecord
    {
        public int id;
        public int parentId;
        public string type;
        public string label;
        public float positionX;
        public float positionY;
        public List<NamedValueRecord> settings = new();
        public List<SlotRecord> inputSlots = new();
        public List<SlotRecord> outputSlots = new();
        public List<ModelRecord> children = new();
    }

    [Serializable]
    private sealed class NamedValueRecord
    {
        public string name;
        public string type;
        public string value;
    }

    [Serializable]
    private sealed class SlotRecord
    {
        public string name;
        public string type;
        public string value;
        public List<string> links = new();
        public List<SlotRecord> children = new();
    }
}
#endif
