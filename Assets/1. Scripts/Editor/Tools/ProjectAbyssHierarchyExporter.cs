#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectAbyssHierarchyExporter
{
    private const string MenuPath =
        "Tools/Project Abyss/Export Current Hierarchy for AI";

    private const string OutputFolderName =
        "AllHierarchyTXT";

    private const string DefaultFileName =
        "Project_Abyss_Current_Hierarchy_For_AI.txt";

    private const string Separator =
        "================================================================================";

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    [MenuItem(MenuPath, priority = 2001)]
    public static void ExportCurrentHierarchyForAI()
    {
        List<SceneExportInfo> scenes =
            CollectLoadedScenes();

        if (scenes.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Hierarchy Export Failed",
                "내보낼 수 있는 열린 씬이 없습니다.",
                "확인");

            return;
        }

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

        string outputPath =
            Path.GetFullPath(
                Path.Combine(
                    outputDirectory,
                    DefaultFileName));

        string temporaryPath =
            outputPath + ".tmp";

        try
        {
            Export(
                projectRoot,
                scenes,
                outputPath,
                temporaryPath);
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfExists(
                temporaryPath);

            EditorUtility.DisplayDialog(
                "Hierarchy Export Cancelled",
                "하이어라키 내보내기를 취소했습니다.",
                "확인");
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(
                temporaryPath);

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Hierarchy Export Failed",
                $"하이어라키 내보내기에 실패했습니다.\n\n{exception.Message}",
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Export(
        string projectRoot,
        IReadOnlyList<SceneExportInfo> scenes,
        string outputPath,
        string temporaryPath)
    {
        List<GameObjectExportInfo> objects =
            BuildObjectList(
                scenes);

        Dictionary<GameObject, int> objectIndices =
            objects.ToDictionary(
                item => item.GameObject,
                item => item.Index);

        List<ReferenceRecord> references =
            new List<ReferenceRecord>();

        ExportStatistics statistics =
            new ExportStatistics
            {
                SceneCount =
                    scenes.Count,
                ObjectCount =
                    objects.Count
            };

        string outputDirectory =
            Path.GetDirectoryName(
                outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using (StreamWriter writer =
               new StreamWriter(
                   temporaryPath,
                   false,
                   new UTF8Encoding(
                       encoderShouldEmitUTF8Identifier: false)))
        {
            writer.NewLine = "\n";

            WriteDocumentHeader(
                writer,
                projectRoot,
                scenes,
                objects);

            WriteSceneSummary(
                writer,
                scenes);

            WriteHierarchyTree(
                writer,
                scenes,
                objectIndices);

            writer.WriteLine(
                "OBJECT_DETAILS_BEGIN");

            writer.WriteLine(
                Separator);

            for (int i = 0;
                 i < objects.Count;
                 i++)
            {
                GameObjectExportInfo objectInfo =
                    objects[i];

                bool cancelled =
                    EditorUtility.DisplayCancelableProgressBar(
                        "AI 분석용 하이어라키 내보내기",
                        $"[{i + 1}/{objects.Count}] {objectInfo.HierarchyPath}",
                        objects.Count == 0
                            ? 1f
                            : (float)i / objects.Count);

                if (cancelled)
                {
                    throw new OperationCanceledException();
                }

                WriteGameObject(
                    writer,
                    objectInfo,
                    objectIndices,
                    references,
                    statistics);
            }

            writer.WriteLine(
                "OBJECT_DETAILS_END");

            writer.WriteLine(
                Separator);

            writer.WriteLine();

            WriteReferenceIndex(
                writer,
                references);

            WriteDocumentFooter(
                writer,
                statistics,
                references.Count);
        }

        ReplaceOutputFile(
            temporaryPath,
            outputPath);

        string normalizedOutputPath =
            NormalizePath(
                outputPath);

        Debug.Log(
            $"[ProjectAbyssHierarchyExporter] Export complete\n" +
            $"Scenes        : {statistics.SceneCount}\n" +
            $"GameObjects   : {statistics.ObjectCount}\n" +
            $"Components    : {statistics.ComponentCount}\n" +
            $"Properties    : {statistics.PropertyCount}\n" +
            $"References    : {references.Count}\n" +
            $"MissingScript : {statistics.MissingScriptCount}\n" +
            $"ReadErrors    : {statistics.ReadErrorCount}\n" +
            $"Output        : {normalizedOutputPath}");

        EditorUtility.DisplayDialog(
            "Hierarchy Export Complete",
            $"현재 하이어라키를 내보냈습니다.\n\n" +
            $"Scenes: {statistics.SceneCount}\n" +
            $"GameObjects: {statistics.ObjectCount}\n" +
            $"Components: {statistics.ComponentCount}\n" +
            $"Properties: {statistics.PropertyCount}\n" +
            $"References: {references.Count}\n" +
            $"Missing Scripts: {statistics.MissingScriptCount}\n" +
            $"Read Errors: {statistics.ReadErrorCount}\n\n" +
            normalizedOutputPath,
            "확인");

        EditorUtility.RevealInFinder(
            outputPath);
    }

    private static List<SceneExportInfo> CollectLoadedScenes()
    {
        List<SceneExportInfo> result =
            new List<SceneExportInfo>();

        HashSet<int> sceneHandles =
            new HashSet<int>();

        for (int i = 0;
             i < SceneManager.sceneCount;
             i++)
        {
            Scene scene =
                SceneManager.GetSceneAt(i);

            AddSceneIfExportable(
                scene,
                result,
                sceneHandles);
        }

        // Play Mode의 DontDestroyOnLoad 씬이나 현재 Stage에 존재하지만
        // SceneManager 목록에 나타나지 않는 루트도 보완한다.
        GameObject[] allObjects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject gameObject in allObjects)
        {
            if (gameObject == null ||
                gameObject.transform.parent != null ||
                EditorUtility.IsPersistent(gameObject) ||
                (gameObject.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                continue;
            }

            AddSceneIfExportable(
                gameObject.scene,
                result,
                sceneHandles);
        }

        return result
            .OrderBy(
                item => item.SortKey,
                StringComparer.Ordinal)
            .ToList();
    }

    private static void AddSceneIfExportable(
        Scene scene,
        List<SceneExportInfo> output,
        HashSet<int> sceneHandles)
    {
        if (!scene.IsValid() ||
            !scene.isLoaded ||
            sceneHandles.Contains(scene.handle))
        {
            return;
        }

        GameObject[] roots;

        try
        {
            roots =
                scene.GetRootGameObjects();
        }
        catch
        {
            return;
        }

        sceneHandles.Add(scene.handle);

        output.Add(
            new SceneExportInfo(
                scene,
                roots));
    }

    private static List<GameObjectExportInfo> BuildObjectList(
        IReadOnlyList<SceneExportInfo> scenes)
    {
        List<GameObjectExportInfo> result =
            new List<GameObjectExportInfo>();

        int nextIndex = 1;

        foreach (SceneExportInfo sceneInfo in scenes)
        {
            foreach (GameObject root in sceneInfo.RootObjects)
            {
                if (root == null)
                    continue;

                AppendObjectRecursive(
                    root.transform,
                    sceneInfo,
                    result,
                    ref nextIndex);
            }
        }

        return result;
    }

    private static void AppendObjectRecursive(
        Transform transform,
        SceneExportInfo sceneInfo,
        List<GameObjectExportInfo> output,
        ref int nextIndex)
    {
        if (transform == null)
            return;

        GameObject gameObject =
            transform.gameObject;

        output.Add(
            new GameObjectExportInfo(
                nextIndex++,
                sceneInfo,
                gameObject,
                BuildHierarchyPath(
                    transform)));

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            AppendObjectRecursive(
                transform.GetChild(i),
                sceneInfo,
                output,
                ref nextIndex);
        }
    }

    private static void WriteDocumentHeader(
        StreamWriter writer,
        string projectRoot,
        IReadOnlyList<SceneExportInfo> scenes,
        IReadOnlyList<GameObjectExportInfo> objects)
    {
        writer.WriteLine(
            "PROJECT_ABYSS_HIERARCHY_EXPORT");

        writer.WriteLine(
            "FORMAT_VERSION: 1");

        writer.WriteLine(
            "PURPOSE: AI_SCENE_HIERARCHY_AND_REFERENCE_ANALYSIS");

        writer.WriteLine(
            $"GENERATED_AT_UTC: {DateTime.UtcNow:O}");

        writer.WriteLine(
            $"UNITY_VERSION: {Application.unityVersion}");

        writer.WriteLine(
            $"UNITY_PROJECT_ROOT: {Quote(NormalizePath(projectRoot))}");

        writer.WriteLine(
            $"PLAY_MODE: {Application.isPlaying}");

        writer.WriteLine(
            $"SCENE_COUNT: {scenes.Count}");

        writer.WriteLine(
            $"GAME_OBJECT_COUNT: {objects.Count}");

        writer.WriteLine(
            "ENCODING: UTF-8_NO_BOM");

        writer.WriteLine(
            "NEWLINE: LF");

        writer.WriteLine(
            "OBJECT_ORDER: SCENE_THEN_HIERARCHY_SIBLING_ORDER");

        writer.WriteLine(
            "SERIALIZED_PROPERTY_POLICY: ALL_SERIALIZED_PROPERTIES");

        writer.WriteLine(
            "REFERENCE_POLICY: INLINE_REFERENCE_DATA_PLUS_GLOBAL_REFERENCE_INDEX");

        writer.WriteLine();

        writer.WriteLine(
            "AI_READING_GUIDE:");

        writer.WriteLine(
            "- HIERARCHY_TREE는 오브젝트의 부모/자식 구조를 빠르게 파악하기 위한 요약이다.");

        writer.WriteLine(
            "- OBJECT_DETAILS의 OBJECT_BEGIN/OBJECT_END 사이가 오브젝트 하나의 전체 정보다.");

        writer.WriteLine(
            "- COMPONENT_BEGIN/COMPONENT_END 사이에 해당 컴포넌트의 모든 SerializedProperty가 기록된다.");

        writer.WriteLine(
            "- PROPERTY의 path는 Unity SerializedProperty.propertyPath다.");

        writer.WriteLine(
            "- ObjectReference는 대상 Asset GUID 또는 Scene/Hierarchy 경로까지 해석된다.");

        writer.WriteLine(
            "- 중복 이름은 경로 세그먼트의 sibling_index로 구분한다.");

        writer.WriteLine(
            "- NULL_REFERENCE와 MISSING_REFERENCE는 서로 다른 상태다.");

        writer.WriteLine(
            "- Missing Script는 COMPONENT_STATUS: MISSING_SCRIPT로 기록된다.");

        writer.WriteLine();

        writer.WriteLine(
            Separator);
    }

    private static void WriteSceneSummary(
        StreamWriter writer,
        IReadOnlyList<SceneExportInfo> scenes)
    {
        writer.WriteLine(
            "SCENE_SUMMARY_BEGIN");

        for (int i = 0;
             i < scenes.Count;
             i++)
        {
            SceneExportInfo sceneInfo =
                scenes[i];

            writer.WriteLine(
                "SCENE_BEGIN");

            writer.WriteLine(
                $"SCENE_INDEX: {i + 1}/{scenes.Count}");

            writer.WriteLine(
                $"SCENE_NAME: {Quote(sceneInfo.Scene.name)}");

            writer.WriteLine(
                $"SCENE_PATH: {Quote(sceneInfo.Scene.path)}");

            writer.WriteLine(
                $"SCENE_GUID: {Quote(GetAssetGuid(sceneInfo.Scene.path))}");

            writer.WriteLine(
                $"SCENE_HANDLE: {sceneInfo.Scene.handle}");

            writer.WriteLine(
                $"SCENE_BUILD_INDEX: {sceneInfo.Scene.buildIndex}");

            writer.WriteLine(
                $"SCENE_IS_DIRTY: {sceneInfo.Scene.isDirty}");

            writer.WriteLine(
                $"ROOT_OBJECT_COUNT: {sceneInfo.RootObjects.Count}");

            writer.WriteLine(
                "SCENE_END");
        }

        writer.WriteLine(
            "SCENE_SUMMARY_END");

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteHierarchyTree(
        StreamWriter writer,
        IReadOnlyList<SceneExportInfo> scenes,
        IReadOnlyDictionary<GameObject, int> objectIndices)
    {
        writer.WriteLine(
            "HIERARCHY_TREE_BEGIN");

        foreach (SceneExportInfo sceneInfo in scenes)
        {
            writer.WriteLine(
                $"SCENE_TREE_BEGIN | " +
                $"name={Quote(sceneInfo.Scene.name)} | " +
                $"path={Quote(sceneInfo.Scene.path)}");

            foreach (GameObject root in sceneInfo.RootObjects)
            {
                if (root == null)
                    continue;

                WriteHierarchyNode(
                    writer,
                    root.transform,
                    objectIndices,
                    0);
            }

            writer.WriteLine(
                "SCENE_TREE_END");
        }

        writer.WriteLine(
            "HIERARCHY_TREE_END");

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteHierarchyNode(
        StreamWriter writer,
        Transform transform,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        int depth)
    {
        if (transform == null)
            return;

        GameObject gameObject =
            transform.gameObject;

        int objectIndex =
            objectIndices.TryGetValue(
                gameObject,
                out int foundIndex)
                ? foundIndex
                : 0;

        Component[] components =
            gameObject.GetComponents<Component>();

        string componentSummary =
            string.Join(
                ", ",
                components.Select(
                    component =>
                        component == null
                            ? "<MissingScript>"
                            : component.GetType().FullName));

        writer.Write(
            new string(
                ' ',
                depth * 2));

        writer.WriteLine(
            $"- OBJECT[{objectIndex:D4}] " +
            $"name={Quote(gameObject.name)} | " +
            $"sibling_index={transform.GetSiblingIndex()} | " +
            $"active_self={gameObject.activeSelf} | " +
            $"active_in_hierarchy={gameObject.activeInHierarchy} | " +
            $"components={Quote(componentSummary)}");

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            WriteHierarchyNode(
                writer,
                transform.GetChild(i),
                objectIndices,
                depth + 1);
        }
    }

    private static void WriteGameObject(
        StreamWriter writer,
        GameObjectExportInfo objectInfo,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references,
        ExportStatistics statistics)
    {
        GameObject gameObject =
            objectInfo.GameObject;

        Transform transform =
            gameObject.transform;

        writer.WriteLine(
            "OBJECT_BEGIN");

        writer.WriteLine(
            $"OBJECT_INDEX: {objectInfo.Index}");

        writer.WriteLine(
            $"OBJECT_NAME: {Quote(gameObject.name)}");

        writer.WriteLine(
            $"SCENE_NAME: {Quote(objectInfo.SceneInfo.Scene.name)}");

        writer.WriteLine(
            $"SCENE_PATH: {Quote(objectInfo.SceneInfo.Scene.path)}");

        writer.WriteLine(
            $"HIERARCHY_PATH: {Quote(objectInfo.HierarchyPath)}");

        writer.WriteLine(
            $"PARENT_PATH: {Quote(GetParentPath(transform))}");

        writer.WriteLine(
            $"SIBLING_INDEX: {transform.GetSiblingIndex()}");

        writer.WriteLine(
            $"CHILD_COUNT: {transform.childCount}");

        writer.WriteLine(
            $"ACTIVE_SELF: {gameObject.activeSelf}");

        writer.WriteLine(
            $"ACTIVE_IN_HIERARCHY: {gameObject.activeInHierarchy}");

        writer.WriteLine(
            $"TAG: {Quote(GetTagSafely(gameObject))}");

        writer.WriteLine(
            $"LAYER_INDEX: {gameObject.layer}");

        writer.WriteLine(
            $"LAYER_NAME: {Quote(LayerMask.LayerToName(gameObject.layer))}");

        writer.WriteLine(
            $"HIDE_FLAGS: {Quote(gameObject.hideFlags.ToString())}");

        writer.WriteLine(
            $"STATIC_FLAGS: {Quote(GameObjectUtility.GetStaticEditorFlags(gameObject).ToString())}");

        writer.WriteLine(
            $"GLOBAL_OBJECT_ID: {Quote(GetGlobalObjectId(gameObject))}");

        WriteTransformSummary(
            writer,
            transform);

        WritePrefabSummary(
            writer,
            gameObject);

        Component[] components =
            gameObject.GetComponents<Component>();

        writer.WriteLine(
            $"COMPONENT_COUNT: {components.Length}");

        for (int componentIndex = 0;
             componentIndex < components.Length;
             componentIndex++)
        {
            WriteComponent(
                writer,
                objectInfo,
                components[componentIndex],
                componentIndex,
                objectIndices,
                references,
                statistics);
        }

        writer.WriteLine(
            "OBJECT_END");

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteTransformSummary(
        StreamWriter writer,
        Transform transform)
    {
        writer.WriteLine(
            "TRANSFORM_SUMMARY_BEGIN");

        writer.WriteLine(
            $"LOCAL_POSITION: {FormatVector3(transform.localPosition)}");

        writer.WriteLine(
            $"LOCAL_ROTATION_EULER: {FormatVector3(transform.localEulerAngles)}");

        writer.WriteLine(
            $"LOCAL_ROTATION_QUATERNION: {FormatQuaternion(transform.localRotation)}");

        writer.WriteLine(
            $"LOCAL_SCALE: {FormatVector3(transform.localScale)}");

        writer.WriteLine(
            $"WORLD_POSITION: {FormatVector3(transform.position)}");

        writer.WriteLine(
            $"WORLD_ROTATION_EULER: {FormatVector3(transform.eulerAngles)}");

        writer.WriteLine(
            $"WORLD_ROTATION_QUATERNION: {FormatQuaternion(transform.rotation)}");

        writer.WriteLine(
            $"LOSSY_SCALE: {FormatVector3(transform.lossyScale)}");

        if (transform is RectTransform rectTransform)
        {
            writer.WriteLine(
                $"RECT_ANCHORED_POSITION: {FormatVector2(rectTransform.anchoredPosition)}");

            writer.WriteLine(
                $"RECT_SIZE_DELTA: {FormatVector2(rectTransform.sizeDelta)}");

            writer.WriteLine(
                $"RECT_ANCHOR_MIN: {FormatVector2(rectTransform.anchorMin)}");

            writer.WriteLine(
                $"RECT_ANCHOR_MAX: {FormatVector2(rectTransform.anchorMax)}");

            writer.WriteLine(
                $"RECT_PIVOT: {FormatVector2(rectTransform.pivot)}");

            writer.WriteLine(
                $"RECT_OFFSET_MIN: {FormatVector2(rectTransform.offsetMin)}");

            writer.WriteLine(
                $"RECT_OFFSET_MAX: {FormatVector2(rectTransform.offsetMax)}");
        }

        writer.WriteLine(
            "TRANSFORM_SUMMARY_END");
    }

    private static void WritePrefabSummary(
        StreamWriter writer,
        GameObject gameObject)
    {
        PrefabInstanceStatus instanceStatus =
            PrefabUtility.GetPrefabInstanceStatus(
                gameObject);

        PrefabAssetType assetType =
            PrefabUtility.GetPrefabAssetType(
                gameObject);

        string nearestPrefabPath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                gameObject);

        UnityEngine.Object correspondingSource =
            PrefabUtility.GetCorrespondingObjectFromSource(
                gameObject);

        writer.WriteLine(
            "PREFAB_SUMMARY_BEGIN");

        writer.WriteLine(
            $"PREFAB_INSTANCE_STATUS: {Quote(instanceStatus.ToString())}");

        writer.WriteLine(
            $"PREFAB_ASSET_TYPE: {Quote(assetType.ToString())}");

        writer.WriteLine(
            $"PREFAB_ASSET_PATH: {Quote(nearestPrefabPath)}");

        writer.WriteLine(
            $"PREFAB_ASSET_GUID: {Quote(GetAssetGuid(nearestPrefabPath))}");

        writer.WriteLine(
            $"CORRESPONDING_SOURCE_GLOBAL_ID: {Quote(GetGlobalObjectId(correspondingSource))}");

        writer.WriteLine(
            "PREFAB_SUMMARY_END");
    }

    private static void WriteComponent(
        StreamWriter writer,
        GameObjectExportInfo ownerInfo,
        Component component,
        int componentIndex,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references,
        ExportStatistics statistics)
    {
        statistics.ComponentCount++;

        writer.WriteLine(
            "COMPONENT_BEGIN");

        writer.WriteLine(
            $"COMPONENT_INDEX: {componentIndex}");

        if (component == null)
        {
            statistics.MissingScriptCount++;

            writer.WriteLine(
                "COMPONENT_STATUS: MISSING_SCRIPT");

            writer.WriteLine(
                "COMPONENT_TYPE: \"<MissingScript>\"");

            writer.WriteLine(
                "COMPONENT_END");

            return;
        }

        Type componentType =
            component.GetType();

        writer.WriteLine(
            "COMPONENT_STATUS: OK");

        writer.WriteLine(
            $"COMPONENT_TYPE: {Quote(componentType.FullName)}");

        writer.WriteLine(
            $"COMPONENT_ASSEMBLY: {Quote(componentType.Assembly.GetName().Name)}");

        writer.WriteLine(
            $"COMPONENT_NAME: {Quote(component.name)}");

        writer.WriteLine(
            $"COMPONENT_HIDE_FLAGS: {Quote(component.hideFlags.ToString())}");

        writer.WriteLine(
            $"COMPONENT_GLOBAL_OBJECT_ID: {Quote(GetGlobalObjectId(component))}");

        if (component is Behaviour behaviour)
        {
            writer.WriteLine(
                $"BEHAVIOUR_ENABLED: {behaviour.enabled}");

            writer.WriteLine(
                $"BEHAVIOUR_ACTIVE_AND_ENABLED: {behaviour.isActiveAndEnabled}");
        }

        if (component is Renderer renderer)
        {
            writer.WriteLine(
                $"RENDERER_ENABLED: {renderer.enabled}");

            writer.WriteLine(
                $"RENDERER_VISIBLE: {renderer.isVisible}");
        }

        if (component is MonoBehaviour monoBehaviour)
        {
            MonoScript script =
                MonoScript.FromMonoBehaviour(
                    monoBehaviour);

            string scriptPath =
                AssetDatabase.GetAssetPath(
                    script);

            writer.WriteLine(
                $"MONO_SCRIPT_PATH: {Quote(scriptPath)}");

            writer.WriteLine(
                $"MONO_SCRIPT_GUID: {Quote(GetAssetGuid(scriptPath))}");
        }

        try
        {
            WriteSerializedProperties(
                writer,
                ownerInfo,
                component,
                componentIndex,
                objectIndices,
                references,
                statistics);
        }
        catch (Exception exception)
        {
            statistics.ReadErrorCount++;

            writer.WriteLine(
                $"COMPONENT_SERIALIZATION_STATUS: ERROR");

            writer.WriteLine(
                $"COMPONENT_SERIALIZATION_ERROR: {Quote(exception.GetType().Name + ": " + exception.Message)}");
        }

        writer.WriteLine(
            "COMPONENT_END");
    }

    private static void WriteSerializedProperties(
        StreamWriter writer,
        GameObjectExportInfo ownerInfo,
        Component component,
        int componentIndex,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references,
        ExportStatistics statistics)
    {
        SerializedObject serializedObject =
            new SerializedObject(
                component);

        serializedObject.UpdateIfRequiredOrScript();

        SerializedProperty iterator =
            serializedObject.GetIterator();

        bool enterChildren = true;
        int propertyCount = 0;

        writer.WriteLine(
            "SERIALIZED_PROPERTIES_BEGIN");

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            propertyCount++;
            statistics.PropertyCount++;

            string value =
                FormatSerializedProperty(
                    iterator,
                    ownerInfo,
                    component,
                    componentIndex,
                    objectIndices,
                    references);

            writer.WriteLine(
                $"PROPERTY | " +
                $"depth={iterator.depth} | " +
                $"path={Quote(iterator.propertyPath)} | " +
                $"display_name={Quote(iterator.displayName)} | " +
                $"type={Quote(iterator.propertyType.ToString())} | " +
                $"type_name={Quote(iterator.type)} | " +
                $"is_array={iterator.isArray} | " +
                $"value={Quote(value)}");
        }

        writer.WriteLine(
            $"SERIALIZED_PROPERTY_COUNT: {propertyCount}");

        writer.WriteLine(
            "SERIALIZED_PROPERTIES_END");
    }

    private static string FormatSerializedProperty(
        SerializedProperty property,
        GameObjectExportInfo ownerInfo,
        Component ownerComponent,
        int componentIndex,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.longValue
                    .ToString(Invariant);

            case SerializedPropertyType.Boolean:
                return property.boolValue
                    .ToString();

            case SerializedPropertyType.Float:
                return property.doubleValue
                    .ToString("R", Invariant);

            case SerializedPropertyType.String:
                return property.stringValue ??
                       string.Empty;

            case SerializedPropertyType.Color:
                return FormatColor(
                    property.colorValue);

            case SerializedPropertyType.ObjectReference:
                return FormatObjectReference(
                    property.objectReferenceValue,
                    property.objectReferenceInstanceIDValue,
                    ownerInfo,
                    ownerComponent,
                    componentIndex,
                    property.propertyPath,
                    objectIndices,
                    references);

            case SerializedPropertyType.LayerMask:
                return property.intValue
                    .ToString(Invariant);

            case SerializedPropertyType.Enum:
                return FormatEnum(
                    property);

            case SerializedPropertyType.Vector2:
                return FormatVector2(
                    property.vector2Value);

            case SerializedPropertyType.Vector3:
                return FormatVector3(
                    property.vector3Value);

            case SerializedPropertyType.Vector4:
                return FormatVector4(
                    property.vector4Value);

            case SerializedPropertyType.Rect:
                return FormatRect(
                    property.rectValue);

            case SerializedPropertyType.ArraySize:
                return property.intValue
                    .ToString(Invariant);

            case SerializedPropertyType.Character:
                return property.intValue
                    .ToString(Invariant);

            case SerializedPropertyType.AnimationCurve:
                return FormatAnimationCurve(
                    property.animationCurveValue);

            case SerializedPropertyType.Bounds:
                return FormatBounds(
                    property.boundsValue);

            case SerializedPropertyType.Gradient:
                return FormatGradient(
                    property.gradientValue);

            case SerializedPropertyType.Quaternion:
                return FormatQuaternion(
                    property.quaternionValue);

            case SerializedPropertyType.ExposedReference:
                return FormatExposedReference(
                    property,
                    ownerInfo,
                    ownerComponent,
                    componentIndex,
                    objectIndices,
                    references);

            case SerializedPropertyType.FixedBufferSize:
                return property.fixedBufferSize
                    .ToString(Invariant);

            case SerializedPropertyType.Vector2Int:
                return FormatVector2Int(
                    property.vector2IntValue);

            case SerializedPropertyType.Vector3Int:
                return FormatVector3Int(
                    property.vector3IntValue);

            case SerializedPropertyType.RectInt:
                return FormatRectInt(
                    property.rectIntValue);

            case SerializedPropertyType.BoundsInt:
                return FormatBoundsInt(
                    property.boundsIntValue);

            case SerializedPropertyType.ManagedReference:
                return
                    $"MANAGED_REFERENCE(" +
                    $"id={property.managedReferenceId}, " +
                    $"type={property.managedReferenceFullTypename})";

            case SerializedPropertyType.Hash128:
                return property.hash128Value
                    .ToString();

            case SerializedPropertyType.Generic:
                return property.isArray
                    ? $"ARRAY(size={property.arraySize})"
                    : "GENERIC";

            default:
                return
                    $"UNSUPPORTED_PROPERTY_TYPE(" +
                    $"{property.propertyType})";
        }
    }

    private static string FormatObjectReference(
        UnityEngine.Object target,
        int instanceId,
        GameObjectExportInfo ownerInfo,
        Component ownerComponent,
        int componentIndex,
        string propertyPath,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references)
    {
        ReferenceTargetInfo targetInfo =
            BuildReferenceTargetInfo(
                target,
                instanceId,
                objectIndices);

        references.Add(
            new ReferenceRecord
            {
                SourceObjectIndex =
                    ownerInfo.Index,
                SourceHierarchyPath =
                    ownerInfo.HierarchyPath,
                SourceComponentIndex =
                    componentIndex,
                SourceComponentType =
                    ownerComponent.GetType().FullName,
                SourcePropertyPath =
                    propertyPath,
                Target =
                    targetInfo
            });

        return targetInfo.ToInlineString();
    }

    private static string FormatExposedReference(
        SerializedProperty property,
        GameObjectExportInfo ownerInfo,
        Component ownerComponent,
        int componentIndex,
        IReadOnlyDictionary<GameObject, int> objectIndices,
        List<ReferenceRecord> references)
    {
        try
        {
            UnityEngine.Object target =
                property.exposedReferenceValue;

            return FormatObjectReference(
                target,
                target != null
                    ? target.GetInstanceID()
                    : 0,
                ownerInfo,
                ownerComponent,
                componentIndex,
                property.propertyPath,
                objectIndices,
                references);
        }
        catch (Exception exception)
        {
            return
                $"EXPOSED_REFERENCE_ERROR(" +
                $"{exception.GetType().Name}: " +
                $"{exception.Message})";
        }
    }

    private static ReferenceTargetInfo BuildReferenceTargetInfo(
        UnityEngine.Object target,
        int instanceId,
        IReadOnlyDictionary<GameObject, int> objectIndices)
    {
        if (target == null)
        {
            return new ReferenceTargetInfo
            {
                Status =
                    instanceId == 0
                        ? "NULL_REFERENCE"
                        : "MISSING_REFERENCE",
                InstanceId =
                    instanceId
            };
        }

        ReferenceTargetInfo info =
            new ReferenceTargetInfo
            {
                Status = "RESOLVED",
                InstanceId =
                    target.GetInstanceID(),
                ObjectType =
                    target.GetType().FullName,
                ObjectName =
                    target.name,
                GlobalObjectId =
                    GetGlobalObjectId(target)
            };

        string assetPath =
            AssetDatabase.GetAssetPath(
                target);

        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            info.TargetKind =
                "ASSET";

            info.AssetPath =
                assetPath;

            info.AssetGuid =
                GetAssetGuid(assetPath);

            return info;
        }

        GameObject targetGameObject = null;
        Component targetComponent = null;

        if (target is GameObject gameObject)
        {
            targetGameObject =
                gameObject;
        }
        else if (target is Component component)
        {
            targetComponent =
                component;

            targetGameObject =
                component.gameObject;
        }

        if (targetGameObject != null)
        {
            info.TargetKind =
                "SCENE_OBJECT";

            info.SceneName =
                targetGameObject.scene.name;

            info.ScenePath =
                targetGameObject.scene.path;

            info.HierarchyPath =
                BuildHierarchyPath(
                    targetGameObject.transform);

            info.TargetObjectIndex =
                objectIndices.TryGetValue(
                    targetGameObject,
                    out int objectIndex)
                    ? objectIndex
                    : 0;

            if (targetComponent != null)
            {
                info.ComponentType =
                    targetComponent
                        .GetType()
                        .FullName;

                info.ComponentIndex =
                    GetComponentIndex(
                        targetComponent);
            }

            return info;
        }

        info.TargetKind =
            "UNITY_OBJECT";

        return info;
    }

    private static void WriteReferenceIndex(
        StreamWriter writer,
        IReadOnlyList<ReferenceRecord> references)
    {
        writer.WriteLine(
            "REFERENCE_INDEX_BEGIN");

        writer.WriteLine(
            $"REFERENCE_COUNT: {references.Count}");

        for (int i = 0;
             i < references.Count;
             i++)
        {
            ReferenceRecord record =
                references[i];

            writer.WriteLine(
                "REFERENCE_BEGIN");

            writer.WriteLine(
                $"REFERENCE_INDEX: {i + 1}");

            writer.WriteLine(
                $"SOURCE_OBJECT_INDEX: {record.SourceObjectIndex}");

            writer.WriteLine(
                $"SOURCE_HIERARCHY_PATH: {Quote(record.SourceHierarchyPath)}");

            writer.WriteLine(
                $"SOURCE_COMPONENT_INDEX: {record.SourceComponentIndex}");

            writer.WriteLine(
                $"SOURCE_COMPONENT_TYPE: {Quote(record.SourceComponentType)}");

            writer.WriteLine(
                $"SOURCE_PROPERTY_PATH: {Quote(record.SourcePropertyPath)}");

            writer.WriteLine(
                $"TARGET_STATUS: {Quote(record.Target.Status)}");

            writer.WriteLine(
                $"TARGET_KIND: {Quote(record.Target.TargetKind)}");

            writer.WriteLine(
                $"TARGET_OBJECT_INDEX: {record.Target.TargetObjectIndex}");

            writer.WriteLine(
                $"TARGET_OBJECT_TYPE: {Quote(record.Target.ObjectType)}");

            writer.WriteLine(
                $"TARGET_OBJECT_NAME: {Quote(record.Target.ObjectName)}");

            writer.WriteLine(
                $"TARGET_INSTANCE_ID: {record.Target.InstanceId}");

            writer.WriteLine(
                $"TARGET_GLOBAL_OBJECT_ID: {Quote(record.Target.GlobalObjectId)}");

            writer.WriteLine(
                $"TARGET_SCENE_NAME: {Quote(record.Target.SceneName)}");

            writer.WriteLine(
                $"TARGET_SCENE_PATH: {Quote(record.Target.ScenePath)}");

            writer.WriteLine(
                $"TARGET_HIERARCHY_PATH: {Quote(record.Target.HierarchyPath)}");

            writer.WriteLine(
                $"TARGET_COMPONENT_INDEX: {record.Target.ComponentIndex}");

            writer.WriteLine(
                $"TARGET_COMPONENT_TYPE: {Quote(record.Target.ComponentType)}");

            writer.WriteLine(
                $"TARGET_ASSET_PATH: {Quote(record.Target.AssetPath)}");

            writer.WriteLine(
                $"TARGET_ASSET_GUID: {Quote(record.Target.AssetGuid)}");

            writer.WriteLine(
                "REFERENCE_END");
        }

        writer.WriteLine(
            "REFERENCE_INDEX_END");

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteDocumentFooter(
        StreamWriter writer,
        ExportStatistics statistics,
        int referenceCount)
    {
        writer.WriteLine(
            "EXPORT_SUMMARY_BEGIN");

        writer.WriteLine(
            $"SCENE_COUNT: {statistics.SceneCount}");

        writer.WriteLine(
            $"GAME_OBJECT_COUNT: {statistics.ObjectCount}");

        writer.WriteLine(
            $"COMPONENT_COUNT: {statistics.ComponentCount}");

        writer.WriteLine(
            $"SERIALIZED_PROPERTY_COUNT: {statistics.PropertyCount}");

        writer.WriteLine(
            $"REFERENCE_COUNT: {referenceCount}");

        writer.WriteLine(
            $"MISSING_SCRIPT_COUNT: {statistics.MissingScriptCount}");

        writer.WriteLine(
            $"READ_ERROR_COUNT: {statistics.ReadErrorCount}");

        writer.WriteLine(
            "EXPORT_SUMMARY_END");

        writer.WriteLine(
            "PROJECT_ABYSS_HIERARCHY_EXPORT_END");
    }

    private static string BuildHierarchyPath(
        Transform transform)
    {
        if (transform == null)
            return string.Empty;

        Stack<string> segments =
            new Stack<string>();

        Transform current =
            transform;

        while (current != null)
        {
            segments.Push(
                $"{EscapePathSegment(current.name)}" +
                $"[sibling={current.GetSiblingIndex()}]");

            current =
                current.parent;
        }

        return string.Join(
            "/",
            segments);
    }

    private static string GetParentPath(
        Transform transform)
    {
        return transform != null &&
               transform.parent != null
            ? BuildHierarchyPath(
                transform.parent)
            : string.Empty;
    }

    private static string EscapePathSegment(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return "<EmptyName>";

        return value
            .Replace(
                "\\",
                "\\\\")
            .Replace(
                "/",
                "\\/");
    }

    private static string GetTagSafely(
        GameObject gameObject)
    {
        try
        {
            return gameObject != null
                ? gameObject.tag
                : string.Empty;
        }
        catch
        {
            return "<InvalidTag>";
        }
    }

    private static int GetComponentIndex(
        Component target)
    {
        if (target == null)
            return -1;

        Component[] components =
            target.gameObject
                .GetComponents<Component>();

        for (int i = 0;
             i < components.Length;
             i++)
        {
            if (components[i] == target)
                return i;
        }

        return -1;
    }

    private static string GetGlobalObjectId(
        UnityEngine.Object target)
    {
        if (target == null)
            return string.Empty;

        try
        {
            return GlobalObjectId
                .GetGlobalObjectIdSlow(
                    target)
                .ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetAssetGuid(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(
            assetPath) ??
            string.Empty;
    }

    private static string FormatEnum(
        SerializedProperty property)
    {
        int index =
            property.enumValueIndex;

        string enumName =
            index >= 0 &&
            index < property.enumNames.Length
                ? property.enumNames[index]
                : "<InvalidEnumIndex>";

        return
            $"index={index}, name={enumName}";
    }

    private static string FormatAnimationCurve(
        AnimationCurve curve)
    {
        if (curve == null)
            return "NULL_CURVE";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"keys={curve.length}");

        for (int i = 0;
             i < curve.length;
             i++)
        {
            Keyframe key =
                curve.keys[i];

            builder.Append(
                $" | [{i}](" +
                $"time={F(key.time)}, " +
                $"value={F(key.value)}, " +
                $"in={F(key.inTangent)}, " +
                $"out={F(key.outTangent)}, " +
                $"inWeight={F(key.inWeight)}, " +
                $"outWeight={F(key.outWeight)}, " +
                $"weightedMode={key.weightedMode})");
        }

        return builder.ToString();
    }

    private static string FormatGradient(
        Gradient gradient)
    {
        if (gradient == null)
            return "NULL_GRADIENT";

        StringBuilder builder =
            new StringBuilder();

        GradientColorKey[] colorKeys =
            gradient.colorKeys;

        GradientAlphaKey[] alphaKeys =
            gradient.alphaKeys;

        builder.Append(
            $"mode={gradient.mode}, " +
            $"colorKeys={colorKeys.Length}, " +
            $"alphaKeys={alphaKeys.Length}");

        for (int i = 0;
             i < colorKeys.Length;
             i++)
        {
            builder.Append(
                $" | C[{i}](" +
                $"time={F(colorKeys[i].time)}, " +
                $"color={FormatColor(colorKeys[i].color)})");
        }

        for (int i = 0;
             i < alphaKeys.Length;
             i++)
        {
            builder.Append(
                $" | A[{i}](" +
                $"time={F(alphaKeys[i].time)}, " +
                $"alpha={F(alphaKeys[i].alpha)})");
        }

        return builder.ToString();
    }

    private static string FormatVector2(
        Vector2 value)
    {
        return
            $"({F(value.x)}, {F(value.y)})";
    }

    private static string FormatVector3(
        Vector3 value)
    {
        return
            $"({F(value.x)}, {F(value.y)}, {F(value.z)})";
    }

    private static string FormatVector4(
        Vector4 value)
    {
        return
            $"({F(value.x)}, {F(value.y)}, {F(value.z)}, {F(value.w)})";
    }

    private static string FormatVector2Int(
        Vector2Int value)
    {
        return
            $"({value.x}, {value.y})";
    }

    private static string FormatVector3Int(
        Vector3Int value)
    {
        return
            $"({value.x}, {value.y}, {value.z})";
    }

    private static string FormatQuaternion(
        Quaternion value)
    {
        return
            $"({F(value.x)}, {F(value.y)}, {F(value.z)}, {F(value.w)})";
    }

    private static string FormatColor(
        Color value)
    {
        return
            $"RGBA({F(value.r)}, {F(value.g)}, {F(value.b)}, {F(value.a)})";
    }

    private static string FormatRect(
        Rect value)
    {
        return
            $"Rect(x={F(value.x)}, y={F(value.y)}, " +
            $"width={F(value.width)}, height={F(value.height)})";
    }

    private static string FormatRectInt(
        RectInt value)
    {
        return
            $"RectInt(x={value.x}, y={value.y}, " +
            $"width={value.width}, height={value.height})";
    }

    private static string FormatBounds(
        Bounds value)
    {
        return
            $"Bounds(center={FormatVector3(value.center)}, " +
            $"size={FormatVector3(value.size)})";
    }

    private static string FormatBoundsInt(
        BoundsInt value)
    {
        return
            $"BoundsInt(position={FormatVector3Int(value.position)}, " +
            $"size={FormatVector3Int(value.size)})";
    }

    private static string F(
        float value)
    {
        return value.ToString(
            "R",
            Invariant);
    }

    private static string Quote(
        string value)
    {
        if (value == null)
            return "\"\"";

        StringBuilder builder =
            new StringBuilder(
                value.Length + 2);

        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;

                case '"':
                    builder.Append("\\\"");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                default:
                    if (char.IsControl(character))
                    {
                        builder.Append(
                            "\\u");

                        builder.Append(
                            ((int)character)
                            .ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');

        return builder.ToString();
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

    private static void ReplaceOutputFile(
        string temporaryPath,
        string outputPath)
    {
        if (!File.Exists(temporaryPath))
        {
            throw new FileNotFoundException(
                "임시 출력 파일을 찾을 수 없습니다.",
                temporaryPath);
        }

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
            File.Delete(path);
        }
    }

    private sealed class SceneExportInfo
    {
        public Scene Scene { get; }
        public IReadOnlyList<GameObject> RootObjects { get; }
        public string SortKey { get; }

        public SceneExportInfo(
            Scene scene,
            IReadOnlyList<GameObject> rootObjects)
        {
            Scene =
                scene;

            RootObjects =
                rootObjects ??
                Array.Empty<GameObject>();

            SortKey =
                string.IsNullOrWhiteSpace(scene.path)
                    ? $"~{scene.name}_{scene.handle}"
                    : NormalizePath(scene.path);
        }
    }

    private sealed class GameObjectExportInfo
    {
        public int Index { get; }
        public SceneExportInfo SceneInfo { get; }
        public GameObject GameObject { get; }
        public string HierarchyPath { get; }

        public GameObjectExportInfo(
            int index,
            SceneExportInfo sceneInfo,
            GameObject gameObject,
            string hierarchyPath)
        {
            Index =
                index;

            SceneInfo =
                sceneInfo;

            GameObject =
                gameObject;

            HierarchyPath =
                hierarchyPath;
        }
    }

    private sealed class ReferenceRecord
    {
        public int SourceObjectIndex;
        public string SourceHierarchyPath;
        public int SourceComponentIndex;
        public string SourceComponentType;
        public string SourcePropertyPath;
        public ReferenceTargetInfo Target;
    }

    private sealed class ReferenceTargetInfo
    {
        public string Status;
        public string TargetKind;
        public int TargetObjectIndex;
        public string ObjectType;
        public string ObjectName;
        public int InstanceId;
        public string GlobalObjectId;
        public string SceneName;
        public string ScenePath;
        public string HierarchyPath;
        public int ComponentIndex = -1;
        public string ComponentType;
        public string AssetPath;
        public string AssetGuid;

        public string ToInlineString()
        {
            return
                $"status={Status}; " +
                $"kind={TargetKind}; " +
                $"target_object_index={TargetObjectIndex}; " +
                $"type={ObjectType}; " +
                $"name={ObjectName}; " +
                $"instance_id={InstanceId}; " +
                $"global_id={GlobalObjectId}; " +
                $"scene={ScenePath}; " +
                $"hierarchy={HierarchyPath}; " +
                $"component_index={ComponentIndex}; " +
                $"component_type={ComponentType}; " +
                $"asset_path={AssetPath}; " +
                $"asset_guid={AssetGuid}";
        }
    }

    private sealed class ExportStatistics
    {
        public int SceneCount;
        public int ObjectCount;
        public int ComponentCount;
        public int PropertyCount;
        public int MissingScriptCount;
        public int ReadErrorCount;
    }
}

#endif
