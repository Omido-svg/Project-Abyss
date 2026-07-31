using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    internal sealed class NodeOnlyShaderGraphBuilderWindow :
        EditorWindow
    {
        private UnityEngine.Object templateGraph;
        private TextAsset recipeAsset;

        private string outputPath =
            "Assets/GeneratedShaderGraphs/AI_NodeGraph.shadergraph";

        private bool clearTemplateNodes = true;
        private bool clearTemplateProperties = true;
        private bool clearTemplateLayout = true;

        private Vector2 scroll;
        private string status =
            "Select a Shader Graph template and a recipe.";
        private MessageType statusType = MessageType.Info;

        private ShaderGraphReflectionBridge bridge;

        [MenuItem(
            "Tools/Project Abyss/Node-Only Shader Graph Builder")]
        private static void Open()
        {
            NodeOnlyShaderGraphBuilderWindow window =
                GetWindow<NodeOnlyShaderGraphBuilderWindow>();

            window.titleContent =
                new GUIContent("Node-Only Shader Graph");

            window.minSize = new Vector2(680f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            bridge = new ShaderGraphReflectionBridge();

            if (!bridge.IsReady)
            {
                SetStatus(
                    bridge.InitializationError,
                    MessageType.Error);
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Node-Only Shader Graph Builder v3",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Schema v3 creates official installed Shader Graph nodes, " +
                "Blackboard material properties, Property nodes, groups, " +
                "sticky notes, graph/target settings, Sub Graph nodes, and " +
                "Custom Function nodes. Node resolution is catalog-driven " +
                "for the Shader Graph package installed in this Unity Editor.",
                MessageType.Info);

            EditorGUILayout.Space(8f);

            templateGraph =
                EditorGUILayout.ObjectField(
                    "Template Shader Graph",
                    templateGraph,
                    typeof(UnityEngine.Object),
                    false);

            recipeAsset =
                (TextAsset)EditorGUILayout.ObjectField(
                    "Recipe JSON",
                    recipeAsset,
                    typeof(TextAsset),
                    false);

            outputPath =
                EditorGUILayout.TextField(
                    "Output Asset Path",
                    outputPath);

            clearTemplateNodes =
                EditorGUILayout.ToggleLeft(
                    "Remove existing non-Master-Stack nodes",
                    clearTemplateNodes);

            clearTemplateProperties =
                EditorGUILayout.ToggleLeft(
                    "Remove existing Blackboard properties",
                    clearTemplateProperties);

            clearTemplateLayout =
                EditorGUILayout.ToggleLeft(
                    "Remove existing groups and sticky notes",
                    clearTemplateLayout);

            EditorGUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(
                       bridge == null ||
                       !bridge.IsReady ||
                       recipeAsset == null))
            {
                if (GUILayout.Button(
                        "1. Validate Recipe",
                        GUILayout.Height(34f)))
                {
                    ValidateRecipe();
                }
            }

            using (new EditorGUI.DisabledScope(
                       bridge == null ||
                       !bridge.IsReady ||
                       templateGraph == null ||
                       recipeAsset == null))
            {
                if (GUILayout.Button(
                        "2. Generate + Verify Shader Graph",
                        GUILayout.Height(42f)))
                {
                    Generate();
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(
                       bridge == null || !bridge.IsReady))
            {
                if (GUILayout.Button(
                        "Export Installed Official Node Catalog",
                        GUILayout.Height(30f)))
                {
                    ExportCatalog();
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(status, statusType);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField(
                "Recipe Node Aliases",
                EditorStyles.boldLabel);

            EditorGUILayout.SelectableLabel(
                bridge != null && bridge.IsReady
                    ? string.Join(", ", bridge.GetAllowedAliases())
                    : "Shader Graph reflection bridge unavailable.",
                EditorStyles.textArea,
                GUILayout.MinHeight(150f));

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField(
                "Template and Target",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "The template still determines the render pipeline and " +
                "Master Stack layout (URP Lit, URP Unlit, Sprite Lit, etc.). " +
                "schema v3 graphSettings can change writable Target/SubTarget " +
                "members such as Surface Type, Blend Mode, Render Face, " +
                "Alpha Clip, shadow flags, and VFX support when those members " +
                "exist in the selected template's installed package version.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void ValidateRecipe()
        {
            if (!TryReadAndValidate(
                    out ShaderGraphRecipe recipe,
                    out List<string> errors))
            {
                SetStatus(
                    string.Join("\n", errors),
                    MessageType.Error);
                return;
            }

            SetStatus(
                "Recipe validation passed.\n" +
                "Schema: " + recipe.schemaVersion + "\n" +
                "Properties: " + recipe.properties.Length + "\n" +
                "Nodes: " + recipe.nodes.Length + "\n" +
                "Edges: " + recipe.edges.Length,
                MessageType.Info);
        }

        private void Generate()
        {
            if (!TryReadAndValidate(
                    out ShaderGraphRecipe recipe,
                    out List<string> validationErrors))
            {
                SetStatus(
                    string.Join("\n", validationErrors),
                    MessageType.Error);
                return;
            }

            string templatePath =
                AssetDatabase.GetAssetPath(templateGraph);

            if (!IsShaderGraphPath(templatePath))
            {
                SetStatus(
                    "Template must be a .shadergraph asset.",
                    MessageType.Error);
                return;
            }

            outputPath = NormalizeAssetPath(outputPath);

            if (!IsShaderGraphPath(outputPath) ||
                !outputPath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                SetStatus(
                    "Output path must be an Assets-relative " +
                    ".shadergraph path.",
                    MessageType.Error);
                return;
            }

            if (string.Equals(
                    templatePath,
                    outputPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetStatus(
                    "Output path cannot overwrite the selected template.",
                    MessageType.Error);
                return;
            }

            try
            {
                EnsureAssetDirectory(outputPath);

                if (AssetDatabase.LoadMainAssetAtPath(outputPath) != null)
                {
                    if (!EditorUtility.DisplayDialog(
                            "Overwrite Shader Graph?",
                            "The output asset already exists:\n" +
                            outputPath,
                            "Overwrite",
                            "Cancel"))
                    {
                        return;
                    }

                    AssetDatabase.DeleteAsset(outputPath);
                }

                if (!AssetDatabase.CopyAsset(templatePath, outputPath))
                {
                    throw new InvalidOperationException(
                        "AssetDatabase.CopyAsset failed.");
                }

                AssetDatabase.ImportAsset(
                    outputPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                RecipeBuildResult buildResult;

                using (ShaderGraphReflectionBridge.GraphHandle handle =
                       bridge.LoadGraph(outputPath))
                {
                    if (clearTemplateNodes)
                    {
                        bridge.ClearDeletableNonBlockNodes(
                            handle.graphData);
                    }

                    buildResult = bridge.ApplyRecipeV3(
                        handle.graphData,
                        recipe,
                        clearTemplateProperties,
                        clearTemplateLayout);

                    bridge.SaveGraph(handle, outputPath);
                }

                AssetDatabase.ImportAsset(
                    outputPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                UnityEngine.Object generatedAsset =
                    AssetDatabase.LoadMainAssetAtPath(outputPath);

                if (generatedAsset == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not import the generated " +
                        ".shadergraph asset.");
                }

                Selection.activeObject = generatedAsset;
                EditorGUIUtility.PingObject(generatedAsset);

                GeneratedShaderGraphVerification verification =
                    GeneratedShaderGraphVerifier.Verify(
                        generatedAsset,
                        recipe.properties);

                SetStatus(
                    "Generated Shader Graph:\n" + outputPath +
                    "\n\n" + buildResult.GetSummary() +
                    "\n\n" + verification.summary,
                    verification.success
                        ? MessageType.Info
                        : MessageType.Error);
            }
            catch (Exception exception)
            {
                SetStatus(
                    exception.ToString(),
                    MessageType.Error);

                Debug.LogException(exception);
            }
        }

        private void ExportCatalog()
        {
            try
            {
                string path =
                    InstalledNodeCatalogExporter.Export(bridge);

                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(path);

                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);

                SetStatus(
                    "Installed node catalog exported:\n" + path,
                    MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(
                    exception.ToString(),
                    MessageType.Error);

                Debug.LogException(exception);
            }
        }

        private bool TryReadAndValidate(
            out ShaderGraphRecipe recipe,
            out List<string> errors)
        {
            recipe = null;
            errors = new List<string>();

            if (recipeAsset == null)
            {
                errors.Add("Select a recipe JSON TextAsset.");
                return false;
            }

            if (!ShaderGraphRecipeParser.TryParse(
                    recipeAsset.text,
                    out recipe,
                    out string parseError))
            {
                errors.Add(parseError);
                return false;
            }

            errors =
                NodeOnlyShaderGraphValidator.Validate(
                    recipe,
                    bridge);

            return errors.Count == 0;
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory =
                Path.GetDirectoryName(assetPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(directory) ||
                directory == "Assets")
            {
                return;
            }

            string[] parts = directory.Split('/');
            string current = parts[0];

            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        private static bool IsShaderGraphPath(string path)
        {
            return
                !string.IsNullOrWhiteSpace(path) &&
                path.EndsWith(
                    ".shadergraph",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
        }

        private void SetStatus(
            string message,
            MessageType type)
        {
            status = message;
            statusType = type;
            Repaint();
        }
    }
}
