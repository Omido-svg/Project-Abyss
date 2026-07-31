using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    internal sealed class RecipeBuildResult
    {
        public int propertyCount;
        public int groupCount;
        public int stickyNoteCount;
        public int nodeCount;
        public int edgeCount;
        public int outputCount;
        public int graphSettingCount;

        public string GetSummary()
        {
            return
                "Properties: " + propertyCount + "\n" +
                "Groups: " + groupCount + "\n" +
                "Sticky Notes: " + stickyNoteCount + "\n" +
                "Nodes: " + nodeCount + "\n" +
                "Edges: " + edgeCount + "\n" +
                "Master Stack Outputs: " + outputCount + "\n" +
                "Graph Settings: " + graphSettingCount;
        }
    }

    internal sealed partial class ShaderGraphReflectionBridge
    {
        public RecipeBuildResult ApplyRecipeV3(
            object graphData,
            ShaderGraphRecipe recipe,
            bool clearTemplateProperties,
            bool clearTemplateLayout)
        {
            RequireReady();

            if (graphData == null)
                throw new ArgumentNullException(nameof(graphData));
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            if (clearTemplateProperties)
                ClearGraphPropertiesV3(graphData);

            if (clearTemplateLayout)
                ClearGraphLayoutV3(graphData);

            int appliedSettings =
                ApplyGraphSettingsV3(graphData, recipe.graphSettings);

            Dictionary<string, object> properties =
                AddRecipePropertiesV3(graphData, recipe.properties);

            Dictionary<string, object> groups =
                AddRecipeGroupsV3(graphData, recipe.groups);

            Dictionary<string, object> nodes =
                AddRecipeNodesV3(
                    graphData,
                    recipe.nodes,
                    properties,
                    groups);

            int stickyCount =
                AddStickyNotesV3(
                    graphData,
                    recipe.stickyNotes);

            ConnectRecipeEdges(graphData, nodes, recipe.edges);
            int connectedOutputs =
                ConnectRecipeOutputsV3(
                    graphData,
                    nodes,
                    recipe.outputs);

            InvokeRequired(graphData, "ValidateGraph");

            return new RecipeBuildResult
            {
                propertyCount = properties.Count,
                groupCount = groups.Count,
                stickyNoteCount = stickyCount,
                nodeCount = nodes.Count,
                edgeCount = recipe.edges.Length,
                outputCount = connectedOutputs,
                graphSettingCount = appliedSettings
            };
        }

        public bool TryResolveRecipeNodeV3(
            RecipeNode recipeNode,
            out Type type,
            out string reason)
        {
            type = null;
            reason = string.Empty;

            if (recipeNode == null)
            {
                reason = "Recipe node is null.";
                return false;
            }

            if (NodeAliasRegistry.IsPropertyAlias(recipeNode.type))
            {
                type = FindOptionalTypeV3(
                    "UnityEditor.ShaderGraph.PropertyNode");

                if (type == null)
                {
                    reason = "PropertyNode was not found in the installed Shader Graph package.";
                    return false;
                }

                return true;
            }

            return TryResolveAllowedNode(
                recipeNode.type,
                out type,
                out reason);
        }

        // ------------------------------------------------------------------
        // Graph settings
        // ------------------------------------------------------------------

        private int ApplyGraphSettingsV3(
            object graphData,
            RecipeGraphSetting[] settings)
        {
            if (settings == null || settings.Length == 0)
                return 0;

            List<object> objects =
                EnumerateConfigurationObjectsV3(graphData).ToList();

            int applied = 0;

            foreach (RecipeGraphSetting setting in settings)
            {
                if (setting == null ||
                    string.IsNullOrWhiteSpace(setting.member))
                {
                    continue;
                }

                string requestedTarget =
                    NodeAliasRegistry.Normalize(setting.target);

                IEnumerable<object> candidates = objects;

                if (!string.IsNullOrEmpty(requestedTarget))
                {
                    candidates = candidates.Where(candidate =>
                    {
                        Type type = candidate.GetType();
                        string simple = NodeAliasRegistry.Normalize(type.Name);
                        string full = NodeAliasRegistry.Normalize(type.FullName);
                        return simple == requestedTarget ||
                               simple.Contains(requestedTarget) ||
                               full.Contains(requestedTarget);
                    });
                }

                bool assigned = false;
                string lastError = string.Empty;

                foreach (object candidate in candidates)
                {
                    if (TrySetMemberFromText(
                            candidate,
                            setting.member,
                            setting.value,
                            out string error))
                    {
                        assigned = true;
                        applied++;
                        break;
                    }

                    lastError = error;
                }

                if (!assigned && !setting.optional)
                {
                    throw new InvalidOperationException(
                        "Graph setting could not be applied. Target='" +
                        setting.target + "', Member='" + setting.member +
                        "', Value='" + setting.value + "'. Last error: " +
                        lastError);
                }
            }

            InvokeIfPresent(graphData, "ValidateGraph");
            return applied;
        }

        private IEnumerable<object> EnumerateConfigurationObjectsV3(
            object root)
        {
            if (root == null)
                yield break;

            Queue<Tuple<object, int>> queue =
                new Queue<Tuple<object, int>>();

            HashSet<object> visited =
                new HashSet<object>(ReferenceEqualityComparerV3.Instance);

            queue.Enqueue(Tuple.Create(root, 0));
            visited.Add(root);

            while (queue.Count > 0)
            {
                Tuple<object, int> item = queue.Dequeue();
                object current = item.Item1;
                int depth = item.Item2;

                yield return current;

                if (depth >= 5)
                    continue;

                foreach (object child in GetConfigurationChildrenV3(current))
                {
                    if (child == null || !visited.Add(child))
                        continue;

                    queue.Enqueue(Tuple.Create(child, depth + 1));
                }
            }
        }

        private IEnumerable<object> GetConfigurationChildrenV3(object target)
        {
            Type targetType = target.GetType();

            foreach (MemberInfo member in
                     GetAllProperties(targetType).Cast<MemberInfo>()
                         .Concat(GetAllFields(targetType)))
            {
                string memberName = NodeAliasRegistry.Normalize(member.Name);
                Type memberType = GetMemberType(member);

                bool interestingName =
                    memberName.Contains("target") ||
                    memberName.Contains("subtarget") ||
                    memberName.Contains("data") ||
                    memberName.Contains("active");

                bool interestingType =
                    IsShaderGraphEditorTypeV3(memberType);

                if (!interestingName && !interestingType &&
                    !typeof(IEnumerable).IsAssignableFrom(memberType))
                {
                    continue;
                }

                object value;
                try
                {
                    value = GetMemberValue(target, member);
                }
                catch
                {
                    continue;
                }

                if (value == null || value is string)
                    continue;

                if (value is IEnumerable enumerable)
                {
                    foreach (object element in enumerable)
                    {
                        if (element != null &&
                            IsShaderGraphEditorTypeV3(element.GetType()))
                        {
                            yield return element;
                        }
                    }
                }
                else if (IsShaderGraphEditorTypeV3(value.GetType()))
                {
                    yield return value;
                }
            }
        }

        private static bool IsShaderGraphEditorTypeV3(Type type)
        {
            if (type == null || type.IsPrimitive || type.IsEnum)
                return false;

            string assembly = type.Assembly.GetName().Name ?? string.Empty;
            string name = type.FullName ?? type.Name;

            return
                assembly == "Unity.ShaderGraph.Editor" ||
                assembly.StartsWith("Unity.RenderPipelines.", StringComparison.Ordinal) ||
                name.StartsWith("UnityEditor.ShaderGraph.", StringComparison.Ordinal) ||
                name.StartsWith("UnityEditor.Rendering.", StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------
        // Blackboard properties
        // ------------------------------------------------------------------

        private void ClearGraphPropertiesV3(object graphData)
        {
            IEnumerable properties =
                ReadEnumerableMemberV3(
                    graphData,
                    "properties",
                    "m_Properties");

            if (properties == null)
                return;

            List<object> snapshot = properties.Cast<object>()
                .Where(value => value != null)
                .ToList();

            foreach (object property in snapshot)
            {
                if (!InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "RemoveGraphInput",
                        property))
                {
                    // Older package versions used RemoveShaderInput.
                    InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "RemoveShaderInput",
                        property);
                }
            }
        }

        private void ClearGraphLayoutV3(object graphData)
        {
            IEnumerable groups = ReadEnumerableMemberV3(
                graphData,
                "groups",
                "groupDatas",
                "m_GroupDatas");

            if (groups != null)
            {
                foreach (object group in groups.Cast<object>()
                             .Where(value => value != null)
                             .ToList())
                {
                    InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "RemoveGroup",
                        group);
                }
            }

            IEnumerable stickyNotes = ReadEnumerableMemberV3(
                graphData,
                "stickyNotes",
                "stickyNoteDatas",
                "m_StickyNoteDatas");

            if (stickyNotes != null)
            {
                foreach (object stickyNote in stickyNotes.Cast<object>()
                             .Where(value => value != null)
                             .ToList())
                {
                    InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "RemoveStickyNote",
                        stickyNote);
                }
            }
        }

        private Dictionary<string, object> AddRecipePropertiesV3(
            object graphData,
            RecipeProperty[] recipes)
        {
            Dictionary<string, object> result =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

            if (recipes == null)
                return result;

            foreach (RecipeProperty recipe in recipes)
            {
                if (recipe == null)
                    continue;

                Type propertyType =
                    ResolveShaderPropertyTypeV3(recipe.type);

                object property =
                    Activator.CreateInstance(propertyType, true);

                ConfigureShaderPropertyV3(property, recipe);

                if (!InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "AddGraphInput",
                        property))
                {
                    if (!InvokeMethodWithLeadingArgumentV3(
                            graphData,
                            "AddShaderInput",
                            property))
                    {
                        throw new MissingMethodException(
                            graphData.GetType().FullName,
                            "AddGraphInput/AddShaderInput");
                    }
                }

                result.Add(recipe.id, property);
            }

            return result;
        }

        private Type ResolveShaderPropertyTypeV3(string recipeType)
        {
            string normalized = NodeAliasRegistry.Normalize(recipeType);
            string[] candidates;

            switch (normalized)
            {
                case "float":
                case "slider":
                case "vector1":
                    candidates = new[]
                    {
                        "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty",
                        "UnityEditor.ShaderGraph.Vector1ShaderProperty"
                    };
                    break;

                case "integer":
                case "int":
                    candidates = new[]
                    {
                        "UnityEditor.ShaderGraph.Internal.IntegerShaderProperty",
                        "UnityEditor.ShaderGraph.IntegerShaderProperty",
                        "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty"
                    };
                    break;

                case "boolean":
                case "bool":
                    candidates = new[]
                    {
                        "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty",
                        "UnityEditor.ShaderGraph.BooleanShaderProperty"
                    };
                    break;

                case "color":
                    candidates = new[]
                    {
                        "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
                        "UnityEditor.ShaderGraph.ColorShaderProperty"
                    };
                    break;

                case "vector2":
                    candidates = PropertyCandidatesV3("Vector2");
                    break;
                case "vector3":
                    candidates = PropertyCandidatesV3("Vector3");
                    break;
                case "vector4":
                    candidates = PropertyCandidatesV3("Vector4");
                    break;
                case "matrix2":
                case "matrix2x2":
                    candidates = PropertyCandidatesV3("Matrix2");
                    break;
                case "matrix3":
                case "matrix3x3":
                    candidates = PropertyCandidatesV3("Matrix3");
                    break;
                case "matrix4":
                case "matrix4x4":
                    candidates = PropertyCandidatesV3("Matrix4");
                    break;
                case "texture2d":
                    candidates = PropertyCandidatesV3("Texture2D");
                    break;
                case "texture2darray":
                    candidates = PropertyCandidatesV3("Texture2DArray");
                    break;
                case "texture3d":
                    candidates = PropertyCandidatesV3("Texture3D");
                    break;
                case "cubemap":
                    candidates = PropertyCandidatesV3("Cubemap");
                    break;
                case "samplerstate":
                    candidates = PropertyCandidatesV3("SamplerState");
                    break;
                case "gradient":
                    candidates = PropertyCandidatesV3("Gradient");
                    break;
                default:
                    candidates = new[] { recipeType };
                    break;
            }

            Type type = FindOptionalTypeV3(candidates);
            if (type == null)
            {
                throw new TypeLoadException(
                    "Shader property type '" + recipeType +
                    "' is not available in the installed Shader Graph package. " +
                    "Candidates: " + string.Join(", ", candidates));
            }

            return type;
        }

        private static string[] PropertyCandidatesV3(string name)
        {
            return new[]
            {
                "UnityEditor.ShaderGraph.Internal." + name + "ShaderProperty",
                "UnityEditor.ShaderGraph." + name + "ShaderProperty"
            };
        }

        private void ConfigureShaderPropertyV3(
            object property,
            RecipeProperty recipe)
        {
            string displayName =
                string.IsNullOrWhiteSpace(recipe.displayName)
                    ? recipe.id
                    : recipe.displayName;

            TrySetAnyTextV3(
                property,
                displayName,
                "displayName",
                "name",
                "m_Name");

            if (!string.IsNullOrWhiteSpace(recipe.referenceName))
            {
                TrySetAnyTextV3(
                    property,
                    recipe.referenceName,
                    "overrideReferenceName",
                    "m_OverrideReferenceName");
            }

            SetAnyMemberV3(
                property,
                recipe.exposed,
                "generatePropertyBlock",
                "m_GeneratePropertyBlock");

            SetAnyMemberV3(
                property,
                recipe.hidden,
                "hidden",
                "m_Hidden");

            TrySetAnyTextV3(
                property,
                recipe.precision,
                "precision",
                "m_Precision");

            if (!string.IsNullOrWhiteSpace(recipe.hlslDeclaration))
            {
                TrySetAnyTextV3(
                    property,
                    recipe.hlslDeclaration,
                    "hlslDeclarationOverride",
                    "m_HlslDeclarationOverride");
            }

            SetAnyMemberV3(
                property,
                recipe.mainTexture,
                "isMainTexture");

            SetAnyMemberV3(
                property,
                recipe.mainColor,
                "isMainColor");

            string normalizedType =
                NodeAliasRegistry.Normalize(recipe.type);

            if (normalizedType == "slider")
            {
                TrySetAnyTextV3(
                    property,
                    "Slider",
                    "floatType",
                    "m_FloatType");

                SetAnyMemberV3(
                    property,
                    new Vector2(recipe.rangeMin, recipe.rangeMax),
                    "rangeValues",
                    "m_RangeValues");
            }
            else if (normalizedType == "integer" || normalizedType == "int")
            {
                TrySetAnyTextV3(
                    property,
                    "Integer",
                    "floatType",
                    "m_FloatType");
            }
            else if (normalizedType == "float" || normalizedType == "vector1")
            {
                TrySetAnyTextV3(
                    property,
                    "Default",
                    "floatType",
                    "m_FloatType");
            }

            if (normalizedType == "color")
            {
                TrySetAnyTextV3(
                    property,
                    recipe.hdr ? "HDR" : "Default",
                    "colorMode",
                    "m_ColorMode");
            }

            if (normalizedType == "gradient")
            {
                Gradient gradient = BuildGradientV3(recipe);
                SetAnyMemberV3(property, gradient, "value", "m_Value");
            }
            else if (!string.IsNullOrWhiteSpace(recipe.assetPath))
            {
                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(recipe.assetPath);

                if (asset == null)
                {
                    throw new InvalidOperationException(
                        "Property '" + recipe.id + "' asset was not found: " +
                        recipe.assetPath);
                }

                if (!AssignObjectOrWrapperV3(
                        property,
                        asset,
                        "value",
                        "m_Value"))
                {
                    throw new InvalidOperationException(
                        "Property '" + recipe.id +
                        "' could not accept asset '" + recipe.assetPath + "'.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(recipe.defaultValue))
            {
                if (!TrySetAnyTextV3(
                        property,
                        recipe.defaultValue,
                        "value",
                        "m_Value"))
                {
                    throw new InvalidOperationException(
                        "Property '" + recipe.id +
                        "' defaultValue could not be assigned: " +
                        recipe.defaultValue);
                }
            }
        }

        private static Gradient BuildGradientV3(RecipeProperty recipe)
        {
            Gradient gradient = new Gradient();

            GradientColorKey[] colorKeys =
                recipe.colorKeys
                    .Where(key => key != null)
                    .Select(key =>
                    {
                        float[] values = ParseFloatComponents(key.color);
                        Color color = values.Length >= 3
                            ? new Color(
                                values[0],
                                values[1],
                                values[2],
                                values.Length >= 4 ? values[3] : 1f)
                            : Color.white;

                        return new GradientColorKey(
                            color,
                            Mathf.Clamp01(key.time));
                    })
                    .ToArray();

            GradientAlphaKey[] alphaKeys =
                recipe.alphaKeys
                    .Where(key => key != null)
                    .Select(key =>
                        new GradientAlphaKey(
                            key.alpha,
                            Mathf.Clamp01(key.time)))
                    .ToArray();

            if (colorKeys.Length == 0)
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f)
                };
            }

            if (alphaKeys.Length == 0)
            {
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                };
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        // ------------------------------------------------------------------
        // Groups and sticky notes
        // ------------------------------------------------------------------

        private Dictionary<string, object> AddRecipeGroupsV3(
            object graphData,
            RecipeGroup[] recipes)
        {
            Dictionary<string, object> result =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

            if (recipes == null || recipes.Length == 0)
                return result;

            Type groupType = FindOptionalTypeV3(
                "UnityEditor.ShaderGraph.GroupData");

            if (groupType == null)
                throw new TypeLoadException("Shader Graph GroupData was not found.");

            foreach (RecipeGroup recipe in recipes)
            {
                if (recipe == null)
                    continue;

                object group = CreateGroupDataV3(groupType, recipe);

                TrySetAnyTextV3(
                    group,
                    recipe.title ?? string.Empty,
                    "title",
                    "m_Title");

                SetAnyMemberV3(
                    group,
                    new Vector2(recipe.x, recipe.y),
                    "position",
                    "m_Position");

                if (!InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "AddGroup",
                        group))
                {
                    throw new MissingMethodException(
                        graphData.GetType().FullName,
                        "AddGroup");
                }

                result.Add(recipe.id, group);
            }

            return result;
        }

        private int AddStickyNotesV3(
            object graphData,
            RecipeStickyNote[] recipes)
        {
            if (recipes == null || recipes.Length == 0)
                return 0;

            Type stickyType = FindOptionalTypeV3(
                "UnityEditor.ShaderGraph.StickyNoteData");

            if (stickyType == null)
                throw new TypeLoadException("Shader Graph StickyNoteData was not found.");

            int count = 0;

            foreach (RecipeStickyNote recipe in recipes)
            {
                if (recipe == null)
                    continue;

                object sticky = CreateStickyNoteDataV3(stickyType, recipe);

                TrySetAnyTextV3(sticky, recipe.title, "title", "m_Title");
                TrySetAnyTextV3(sticky, recipe.content, "content", "m_Content");

                SetAnyMemberV3(
                    sticky,
                    new Rect(
                        recipe.x,
                        recipe.y,
                        Mathf.Max(80f, recipe.width),
                        Mathf.Max(60f, recipe.height)),
                    "position",
                    "m_Position");

                TrySetAnyTextV3(sticky, recipe.theme, "theme", "m_Theme");
                TrySetAnyTextV3(sticky, recipe.textSize, "textSize", "m_TextSize");

                if (!InvokeMethodWithLeadingArgumentV3(
                        graphData,
                        "AddStickyNote",
                        sticky))
                {
                    throw new MissingMethodException(
                        graphData.GetType().FullName,
                        "AddStickyNote");
                }

                count++;
            }

            return count;
        }

        private object CreateGroupDataV3(
            Type groupType,
            RecipeGroup recipe)
        {
            try
            {
                return Activator.CreateInstance(groupType, true);
            }
            catch
            {
                foreach (ConstructorInfo constructor in groupType
                             .GetConstructors(
                                 BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic)
                             .OrderBy(candidate =>
                                 candidate.GetParameters().Length))
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    object[] arguments = new object[parameters.Length];
                    bool compatible = true;

                    for (int index = 0; index < parameters.Length; index++)
                    {
                        Type type = parameters[index].ParameterType;
                        string name = NodeAliasRegistry.Normalize(
                            parameters[index].Name);

                        if (type == typeof(string))
                            arguments[index] = recipe.title ?? string.Empty;
                        else if (type == typeof(Vector2))
                            arguments[index] = new Vector2(recipe.x, recipe.y);
                        else if (type == typeof(Rect))
                            arguments[index] = new Rect(recipe.x, recipe.y, 300f, 200f);
                        else if (parameters[index].HasDefaultValue)
                            arguments[index] = parameters[index].DefaultValue;
                        else if (!type.IsValueType)
                            arguments[index] = null;
                        else
                        {
                            try
                            {
                                arguments[index] = Activator.CreateInstance(type);
                            }
                            catch
                            {
                                compatible = false;
                                break;
                            }
                        }
                    }

                    if (!compatible)
                        continue;

                    try
                    {
                        return constructor.Invoke(arguments);
                    }
                    catch
                    {
                        // Try the next constructor.
                    }
                }
            }

            throw new InvalidOperationException(
                "Could not construct Shader Graph GroupData.");
        }

        private object CreateStickyNoteDataV3(
            Type stickyType,
            RecipeStickyNote recipe)
        {
            try
            {
                return Activator.CreateInstance(stickyType, true);
            }
            catch
            {
                foreach (ConstructorInfo constructor in stickyType
                             .GetConstructors(
                                 BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic)
                             .OrderBy(candidate =>
                                 candidate.GetParameters().Length))
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    object[] arguments = new object[parameters.Length];
                    bool compatible = true;

                    for (int index = 0; index < parameters.Length; index++)
                    {
                        Type type = parameters[index].ParameterType;
                        string name = NodeAliasRegistry.Normalize(
                            parameters[index].Name);

                        if (type == typeof(string))
                        {
                            arguments[index] = name.Contains("content")
                                ? recipe.content ?? string.Empty
                                : recipe.title ?? string.Empty;
                        }
                        else if (type == typeof(Rect))
                        {
                            arguments[index] = new Rect(
                                recipe.x,
                                recipe.y,
                                recipe.width,
                                recipe.height);
                        }
                        else if (type == typeof(Vector2))
                        {
                            arguments[index] = new Vector2(recipe.x, recipe.y);
                        }
                        else if (parameters[index].HasDefaultValue)
                        {
                            arguments[index] = parameters[index].DefaultValue;
                        }
                        else if (!type.IsValueType)
                        {
                            arguments[index] = null;
                        }
                        else
                        {
                            try
                            {
                                arguments[index] = Activator.CreateInstance(type);
                            }
                            catch
                            {
                                compatible = false;
                                break;
                            }
                        }
                    }

                    if (!compatible)
                        continue;

                    try
                    {
                        return constructor.Invoke(arguments);
                    }
                    catch
                    {
                        // Try the next constructor.
                    }
                }
            }

            throw new InvalidOperationException(
                "Could not construct Shader Graph StickyNoteData.");
        }

        // ------------------------------------------------------------------
        // Nodes, PropertyNode, Sub Graph, Custom Function, dynamic ports
        // ------------------------------------------------------------------

        private Dictionary<string, object> AddRecipeNodesV3(
            object graphData,
            RecipeNode[] recipes,
            Dictionary<string, object> properties,
            Dictionary<string, object> groups)
        {
            Dictionary<string, object> result =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (RecipeNode recipe in recipes)
            {
                if (recipe == null)
                    continue;

                object node;
                object boundProperty = null;

                if (NodeAliasRegistry.IsPropertyAlias(recipe.type))
                {
                    if (!properties.TryGetValue(
                            recipe.property ?? string.Empty,
                            out object property))
                    {
                        throw new InvalidOperationException(
                            "Property node '" + recipe.id +
                            "' references missing property id '" +
                            recipe.property + "'.");
                    }

                    boundProperty = property;

                    Type propertyNodeType = FindOptionalTypeV3(
                        "UnityEditor.ShaderGraph.PropertyNode");

                    if (propertyNodeType == null)
                    {
                        throw new TypeLoadException(
                            "UnityEditor.ShaderGraph.PropertyNode was not found.");
                    }

                    node = Activator.CreateInstance(propertyNodeType, true);

                    if (!BindPropertyNodeV3(node, property))
                    {
                        throw new InvalidOperationException(
                            "PropertyNode could not bind property '" +
                            recipe.property + "'. The installed Shader Graph " +
                            "package exposed neither SetShaderProperty nor a " +
                            "compatible property/propertyGuid member.");
                    }
                }
                else
                {
                    if (!TryResolveAllowedNode(
                            recipe.type,
                            out Type nodeType,
                            out string reason))
                    {
                        throw new InvalidOperationException(
                            "Cannot resolve node '" + recipe.id + "': " + reason);
                    }

                    node = Activator.CreateInstance(nodeType, true);
                    ApplyMembers(node, recipe);
                    ApplyNodeAssetBindingV3(node, recipe);
                    ApplyCustomFunctionV3(node, recipe);
                }

                InvokeIfPresent(node, "UpdateNodeAfterDeserialization");

                if (recipe.ports != null && recipe.ports.Length > 0)
                    RebuildDynamicPortsV3(node, recipe);

                SynchronizeConstantNodeSlots(node, recipe);
                SetNodeTitleIfPossible(node, recipe.title);

                SetNodePosition(
                    node,
                    new Rect(
                        recipe.x,
                        recipe.y,
                        recipe.width > 1f ? recipe.width : 240f,
                        recipe.height > 1f ? recipe.height : 180f));

                if (!string.IsNullOrWhiteSpace(recipe.group))
                {
                    if (!groups.TryGetValue(recipe.group, out object group))
                    {
                        throw new InvalidOperationException(
                            "Node '" + recipe.id +
                            "' references missing group '" + recipe.group + "'.");
                    }

                    SetNodeGroupV3(node, group);
                }

                AddNodeToGraph(graphData, node);

                // PropertyNode resolves its slot from GraphData. Several
                // Shader Graph versions require the node to have an owner
                // before UpdateNodeAfterDeserialization can create that slot.
                if (boundProperty != null)
                {
                    if (!BindPropertyNodeV3(node, boundProperty))
                    {
                        throw new InvalidOperationException(
                            "PropertyNode lost its property binding after it " +
                            "was inserted into GraphData: " + recipe.property);
                    }

                    InvokeIfPresent(node, "UpdateNodeAfterDeserialization");
                }

                ApplyInputDefaults(node, recipe);
                SynchronizeConstantNodeSlots(node, recipe);

                result.Add(recipe.id, node);
            }

            return result;
        }

        private bool BindPropertyNodeV3(
            object node,
            object property)
        {
            if (node == null || property == null)
                return false;

            // Current Shader Graph versions expose SetShaderProperty().
            // Older/newer versions have alternated between a direct property
            // reference and a serialized property GUID. Try every official
            // representation so schema v3 is not tied to one package layout.
            if (InvokeMethodWithLeadingArgumentV3(
                    node,
                    "SetShaderProperty",
                    property) ||
                InvokeMethodWithLeadingArgumentV3(
                    node,
                    "SetProperty",
                    property) ||
                SetAnyMemberV3(
                    node,
                    property,
                    "property",
                    "m_Property",
                    "shaderInput"))
            {
                return true;
            }

            if (!TryReadGuidTextV3(property, out string guidText))
                return false;

            if (TrySetAnyTextV3(
                    node,
                    guidText,
                    "propertyGuid",
                    "propertyGuidSerialized",
                    "m_PropertyGuid",
                    "m_PropertyGuidSerialized"))
            {
                return true;
            }

            // Some versions wrap the GUID in a JsonRef/SerializableGuid.
            return SetAnyMemberV3(
                node,
                guidText,
                "propertyGuid",
                "m_PropertyGuid");
        }

        private static bool TryReadGuidTextV3(
            object source,
            out string guidText)
        {
            guidText = string.Empty;
            if (source == null)
                return false;

            string[] candidates =
            {
                "guid",
                "objectId",
                "m_Guid",
                "m_GuidSerialized",
                "guidSerialized"
            };

            foreach (string candidate in candidates)
            {
                MemberInfo member =
                    FindReadableMember(source.GetType(), candidate);

                if (member == null)
                    continue;

                object value;
                try
                {
                    value = GetMemberValue(source, member);
                }
                catch
                {
                    continue;
                }

                if (value == null)
                    continue;

                if (value is Guid guid)
                {
                    guidText = guid.ToString();
                    return true;
                }

                if (value is string text &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    guidText = text;
                    return true;
                }

                // SerializableGuid-like wrapper. Do not recurse into the
                // original object itself and keep the search shallow.
                if (!ReferenceEquals(value, source))
                {
                    foreach (string nestedName in new[]
                             {
                                 "value",
                                 "guid",
                                 "m_GuidSerialized",
                                 "guidSerialized"
                             })
                    {
                        MemberInfo nested =
                            FindReadableMember(value.GetType(), nestedName);

                        if (nested == null)
                            continue;

                        object nestedValue;
                        try
                        {
                            nestedValue = GetMemberValue(value, nested);
                        }
                        catch
                        {
                            continue;
                        }

                        if (nestedValue is Guid nestedGuid)
                        {
                            guidText = nestedGuid.ToString();
                            return true;
                        }

                        if (nestedValue is string nestedText &&
                            !string.IsNullOrWhiteSpace(nestedText))
                        {
                            guidText = nestedText;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void ApplyNodeAssetBindingV3(
            object node,
            RecipeNode recipe)
        {
            string path = recipe.assetPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            UnityEngine.Object asset =
                AssetDatabase.LoadMainAssetAtPath(path);

            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Node '" + recipe.id + "' asset was not found: " + path);
            }

            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(recipe.assetMember))
                candidates.Add(recipe.assetMember);

            candidates.AddRange(new[]
            {
                "asset",
                "subGraphAsset",
                "subGraph",
                "texture",
                "cubemap",
                "functionSource",
                "source",
                "m_SubGraph",
                "m_FunctionSource"
            });

            if (AssignObjectOrWrapperV3(node, asset, candidates.ToArray()))
                return;

            // Some official nodes expose a setter method instead of a field.
            if (InvokeMethodWithLeadingArgumentV3(node, "SetAsset", asset) ||
                InvokeMethodWithLeadingArgumentV3(node, "SetSubGraph", asset))
            {
                return;
            }

            throw new InvalidOperationException(
                "Node '" + recipe.id + "' could not bind asset '" + path +
                "'. Set assetMember to the installed node's writable member " +
                "shown by the node catalog/exported Shader Graph JSON.");
        }

        private void ApplyCustomFunctionV3(
            object node,
            RecipeNode recipe)
        {
            string normalizedType =
                NodeAliasRegistry.Normalize(node.GetType().Name);

            bool isCustomFunctionNode =
                normalizedType.Contains("customfunction");

            if (!isCustomFunctionNode)
            {
                if (HasExplicitCustomFunctionPayloadV3(recipe.customFunction))
                {
                    throw new InvalidOperationException(
                        "Node '" + recipe.id +
                        "' defines customFunction data but is not a Custom Function node.");
                }

                return;
            }

            if (recipe.customFunction == null)
            {
                throw new InvalidOperationException(
                    "Custom Function node '" + recipe.id +
                    "' has no customFunction configuration.");
            }

            RecipeCustomFunction function = recipe.customFunction;

            TrySetAnyTextV3(
                node,
                function.sourceType,
                "sourceType",
                "m_SourceType");

            TrySetAnyTextV3(
                node,
                function.functionName,
                "functionName",
                "m_FunctionName");

            TrySetAnyTextV3(
                node,
                function.body,
                "functionBody",
                "body",
                "m_FunctionBody");

            TrySetAnyTextV3(
                node,
                function.precision,
                "precision",
                "m_Precision");

            if (!string.IsNullOrWhiteSpace(function.filePath))
            {
                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(function.filePath);

                if (asset == null)
                {
                    throw new InvalidOperationException(
                        "Custom Function source file was not found: " +
                        function.filePath);
                }

                if (!AssignObjectOrWrapperV3(
                        node,
                        asset,
                        "functionSource",
                        "source",
                        "m_FunctionSource"))
                {
                    throw new InvalidOperationException(
                        "Custom Function node could not bind source file: " +
                        function.filePath);
                }
            }
        }

        private static bool HasExplicitCustomFunctionPayloadV3(
            RecipeCustomFunction function)
        {
            if (function == null)
                return false;

            if (!string.IsNullOrWhiteSpace(function.functionName) ||
                !string.IsNullOrWhiteSpace(function.body) ||
                !string.IsNullOrWhiteSpace(function.filePath))
            {
                return true;
            }

            bool nonDefaultSourceType =
                !string.IsNullOrWhiteSpace(function.sourceType) &&
                !string.Equals(
                    function.sourceType,
                    "String",
                    StringComparison.OrdinalIgnoreCase);

            bool nonDefaultPrecision =
                !string.IsNullOrWhiteSpace(function.precision) &&
                !string.Equals(
                    function.precision,
                    "Inherit",
                    StringComparison.OrdinalIgnoreCase);

            return nonDefaultSourceType || nonDefaultPrecision;
        }

        private void RebuildDynamicPortsV3(
            object node,
            RecipeNode recipe)
        {
            foreach (SlotInfo existing in GetSlots(node))
            {
                InvokeMethodWithLeadingArgumentV3(
                    node,
                    "RemoveSlot",
                    existing.id);
            }

            foreach (RecipePort port in recipe.ports)
            {
                if (port == null)
                    continue;

                object slot = CreateMaterialSlotV3(port);

                if (!InvokeMethodWithLeadingArgumentV3(
                        node,
                        "AddSlot",
                        slot))
                {
                    throw new MissingMethodException(
                        node.GetType().FullName,
                        "AddSlot");
                }
            }
        }

        private object CreateMaterialSlotV3(RecipePort port)
        {
            string normalized = NodeAliasRegistry.Normalize(port.valueType);
            string[] candidates;

            switch (normalized)
            {
                case "vector1": candidates = new[] { "Vector1MaterialSlot" }; break;
                case "vector2": candidates = new[] { "Vector2MaterialSlot" }; break;
                case "vector3": candidates = new[] { "Vector3MaterialSlot" }; break;
                case "vector4": candidates = new[] { "Vector4MaterialSlot" }; break;
                case "boolean": candidates = new[] { "BooleanMaterialSlot" }; break;
                case "matrix2": candidates = new[] { "Matrix2MaterialSlot", "DynamicMatrixMaterialSlot" }; break;
                case "matrix3": candidates = new[] { "Matrix3MaterialSlot", "DynamicMatrixMaterialSlot" }; break;
                case "matrix4": candidates = new[] { "Matrix4MaterialSlot", "DynamicMatrixMaterialSlot" }; break;
                case "texture2d": candidates = new[] { "Texture2DInputMaterialSlot", "Texture2DMaterialSlot" }; break;
                case "texture2darray": candidates = new[] { "Texture2DArrayInputMaterialSlot", "Texture2DArrayMaterialSlot" }; break;
                case "texture3d": candidates = new[] { "Texture3DInputMaterialSlot", "Texture3DMaterialSlot" }; break;
                case "cubemap": candidates = new[] { "CubemapInputMaterialSlot", "CubemapMaterialSlot" }; break;
                case "samplerstate": candidates = new[] { "SamplerStateMaterialSlot" }; break;
                case "gradient": candidates = new[] { "GradientMaterialSlot" }; break;
                default: candidates = new[] { "DynamicVectorMaterialSlot", "DynamicValueMaterialSlot" }; break;
            }

            Type slotType = candidates
                .Select(name => FindOptionalTypeV3(
                    "UnityEditor.ShaderGraph." + name,
                    name))
                .FirstOrDefault(type => type != null);

            if (slotType == null)
            {
                throw new TypeLoadException(
                    "Material slot type for '" + port.valueType +
                    "' was not found. Candidates: " +
                    string.Join(", ", candidates));
            }

            object slot = CreateSlotByConstructorV3(slotType, port);

            SetAnyMemberV3(slot, port.id, "id", "m_Id");
            TrySetAnyTextV3(slot, port.name, "displayName", "m_DisplayName");
            TrySetAnyTextV3(
                slot,
                string.IsNullOrWhiteSpace(port.shaderName)
                    ? port.name
                    : port.shaderName,
                "shaderOutputName",
                "m_ShaderOutputName");
            TrySetAnyTextV3(slot, port.direction, "slotType", "m_SlotType");
            TrySetAnyTextV3(slot, port.stage, "stageCapability", "m_StageCapability");
            SetAnyMemberV3(slot, port.hidden, "hidden", "m_Hidden");

            if (!string.IsNullOrWhiteSpace(port.defaultValue))
            {
                TrySetAnyTextV3(slot, port.defaultValue, "value", "m_Value");
                TrySetAnyTextV3(slot, port.defaultValue, "defaultValue", "m_DefaultValue");
            }

            return slot;
        }

        private object CreateSlotByConstructorV3(
            Type slotType,
            RecipePort port)
        {
            foreach (ConstructorInfo constructor in slotType
                         .GetConstructors(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic)
                         .OrderByDescending(candidate =>
                             candidate.GetParameters().Length))
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                object[] arguments = new object[parameters.Length];
                bool compatible = true;

                for (int index = 0; index < parameters.Length; index++)
                {
                    if (!TryCreateSlotConstructorArgumentV3(
                            parameters[index],
                            port,
                            out arguments[index]))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                    continue;

                try
                {
                    return constructor.Invoke(arguments);
                }
                catch
                {
                    // Try the next constructor.
                }
            }

            try
            {
                return Activator.CreateInstance(slotType, true);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "No compatible constructor was found for material slot " +
                    slotType.FullName + ". " +
                    exception.GetBaseException().Message);
            }
        }

        private bool TryCreateSlotConstructorArgumentV3(
            ParameterInfo parameter,
            RecipePort port,
            out object value)
        {
            Type type = parameter.ParameterType;
            string name = NodeAliasRegistry.Normalize(parameter.Name);

            if (type == typeof(int))
            {
                value = port.id;
                return true;
            }

            if (type == typeof(string))
            {
                value = name.Contains("shader")
                    ? (string.IsNullOrWhiteSpace(port.shaderName)
                        ? port.name
                        : port.shaderName)
                    : port.name;
                return true;
            }

            if (type == typeof(bool))
            {
                value = name.Contains("hidden") && port.hidden;
                return true;
            }

            if (type.IsEnum)
            {
                string enumText =
                    name.Contains("stage")
                        ? port.stage
                        : name.Contains("slot") || name.Contains("direction")
                            ? port.direction
                            : Enum.GetNames(type).FirstOrDefault() ?? "0";

                try
                {
                    value = Enum.Parse(type, enumText, true);
                    return true;
                }
                catch
                {
                    Array values = Enum.GetValues(type);
                    value = values.Length > 0
                        ? values.GetValue(0)
                        : Activator.CreateInstance(type);
                    return true;
                }
            }

            if (TryConvertText(
                    port.defaultValue,
                    type,
                    out value,
                    out _))
            {
                return true;
            }

            if (parameter.HasDefaultValue)
            {
                value = parameter.DefaultValue;
                return true;
            }

            if (!type.IsValueType)
            {
                value = null;
                return true;
            }

            try
            {
                value = Activator.CreateInstance(type);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private void SetNodeGroupV3(object node, object group)
        {
            if (InvokeMethodWithLeadingArgumentV3(node, "SetGroup", group) ||
                SetAnyMemberV3(node, group, "group", "m_Group"))
            {
                return;
            }

            if (TryReadGuidTextV3(group, out string guidText) &&
                TrySetAnyTextV3(
                    node,
                    guidText,
                    "groupGuid",
                    "groupGuidSerialized",
                    "m_GroupGuid",
                    "m_GroupGuidSerialized"))
            {
                return;
            }

            throw new InvalidOperationException(
                "Node '" + GetNodeDisplayName(node) +
                "' could not be assigned to a Shader Graph group. " +
                "No compatible SetGroup/group/groupGuid representation " +
                "was exposed by the installed package.");
        }

        // ------------------------------------------------------------------
        // Outputs
        // ------------------------------------------------------------------

        private int ConnectRecipeOutputsV3(
            object graphData,
            Dictionary<string, object> nodes,
            RecipeOutput[] outputs)
        {
            List<object> blocks = GetGraphNodes(graphData)
                .Where(node =>
                    node != null &&
                    blockNodeType.IsInstanceOfType(node))
                .ToList();

            int count = 0;

            foreach (RecipeOutput output in outputs)
            {
                object sourceNode = nodes[output.fromNode];
                SlotInfo sourceSlot =
                    FindSlot(sourceNode, output.fromPort, false);

                object block;
                try
                {
                    block = FindMasterStackBlock(blocks, output.block);
                }
                catch (InvalidOperationException)
                {
                    if (output.optional ||
                        IsOptionalMissingOutputBlock(output.block))
                    {
                        continue;
                    }

                    throw;
                }

                SlotInfo blockInput = FindFirstInputSlot(block);

                object connected = InvokeRequired(
                    graphData,
                    "Connect",
                    sourceSlot.slotReference,
                    blockInput.slotReference);

                if (connected == null)
                {
                    throw new InvalidOperationException(
                        "Shader Graph rejected output connection " +
                        output.fromNode + "." + output.fromPort +
                        " -> Master Stack '" + output.block + "'.");
                }

                count++;
            }

            return count;
        }

        // ------------------------------------------------------------------
        // Reflection compatibility helpers
        // ------------------------------------------------------------------

        private Type FindOptionalTypeV3(params string[] names)
        {
            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type exact = assembly.GetType(name, false);
                    if (exact != null)
                        return exact;

                    Type simple = GetLoadableTypes(assembly)
                        .FirstOrDefault(type =>
                            type != null &&
                            string.Equals(type.Name, name, StringComparison.Ordinal));

                    if (simple != null)
                        return simple;
                }
            }

            return null;
        }

        private static IEnumerable ReadEnumerableMemberV3(
            object target,
            params string[] names)
        {
            foreach (string name in names)
            {
                MemberInfo member = FindReadableMember(target.GetType(), name);
                if (member == null)
                    continue;

                object value;
                try
                {
                    value = GetMemberValue(target, member);
                }
                catch
                {
                    continue;
                }

                if (value is IEnumerable enumerable)
                    return enumerable;
            }

            return null;
        }

        private bool InvokeMethodWithLeadingArgumentV3(
            object target,
            string methodName,
            object firstArgument)
        {
            if (target == null)
                return false;

            foreach (MethodInfo method in GetAllInstanceMethods(target.GetType())
                         .Where(candidate =>
                             candidate.Name == methodName &&
                             !candidate.IsGenericMethodDefinition)
                         .OrderBy(candidate =>
                             candidate.GetParameters().Length))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    continue;

                if (firstArgument != null &&
                    !parameters[0].ParameterType.IsInstanceOfType(firstArgument) &&
                    !(parameters[0].ParameterType == typeof(int) &&
                      firstArgument is int))
                {
                    continue;
                }

                object[] arguments = new object[parameters.Length];
                arguments[0] = firstArgument;
                bool compatible = true;

                for (int index = 1; index < parameters.Length; index++)
                {
                    ParameterInfo parameter = parameters[index];

                    if (parameter.HasDefaultValue)
                    {
                        arguments[index] = parameter.DefaultValue;
                    }
                    else if (parameter.ParameterType == typeof(bool))
                    {
                        arguments[index] = false;
                    }
                    else if (!parameter.ParameterType.IsValueType ||
                             Nullable.GetUnderlyingType(parameter.ParameterType) != null)
                    {
                        arguments[index] = null;
                    }
                    else
                    {
                        try
                        {
                            arguments[index] =
                                Activator.CreateInstance(parameter.ParameterType);
                        }
                        catch
                        {
                            compatible = false;
                            break;
                        }
                    }
                }

                if (!compatible)
                    continue;

                try
                {
                    method.Invoke(target, arguments);
                    return true;
                }
                catch
                {
                    // Try another overload.
                }
            }

            return false;
        }

        private bool TrySetAnyTextV3(
            object target,
            string value,
            params string[] names)
        {
            foreach (string name in names)
            {
                if (TrySetMemberFromText(
                        target,
                        name,
                        value,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SetAnyMemberV3(
            object target,
            object value,
            params string[] names)
        {
            foreach (string name in names)
            {
                MemberInfo member =
                    FindWritableMember(target.GetType(), name);

                if (member == null)
                    continue;

                Type memberType = GetMemberType(member);
                object assignedValue = value;

                if (assignedValue == null)
                {
                    if (memberType.IsValueType &&
                        Nullable.GetUnderlyingType(memberType) == null)
                    {
                        continue;
                    }
                }
                else if (!memberType.IsInstanceOfType(assignedValue))
                {
                    if (!TryCreateReferenceWrapperV3(
                            memberType,
                            assignedValue,
                            out object wrapper))
                    {
                        continue;
                    }

                    assignedValue = wrapper;
                }

                try
                {
                    SetMemberValue(target, member, assignedValue);
                    return true;
                }
                catch
                {
                    // Try the next member candidate.
                }
            }

            return false;
        }

        private static bool TryCreateReferenceWrapperV3(
            Type wrapperType,
            object referencedObject,
            out object wrapper)
        {
            wrapper = null;

            if (wrapperType == null || referencedObject == null)
                return false;

            if (wrapperType.IsPrimitive ||
                wrapperType.IsEnum ||
                wrapperType == typeof(string))
            {
                return false;
            }

            try
            {
                wrapper = Activator.CreateInstance(wrapperType, true);
            }
            catch
            {
                return false;
            }

            if (wrapper == null)
                return false;

            string[] preferredNames =
            {
                "value",
                "reference",
                "data",
                "target",
                "object",
                "m_Value",
                "m_Reference",
                "m_Data",
                "m_Target",
                "m_Object"
            };

            foreach (string name in preferredNames)
            {
                MemberInfo member = FindWritableMember(wrapperType, name);
                if (member == null)
                    continue;

                Type memberType = GetMemberType(member);
                if (!memberType.IsInstanceOfType(referencedObject))
                    continue;

                try
                {
                    SetMemberValue(wrapper, member, referencedObject);
                    return true;
                }
                catch
                {
                    // Try another wrapper member.
                }
            }

            foreach (MemberInfo member in
                     GetAllProperties(wrapperType).Cast<MemberInfo>()
                         .Concat(GetAllFields(wrapperType)))
            {
                Type memberType = GetMemberType(member);
                if (!memberType.IsInstanceOfType(referencedObject))
                    continue;

                if (member is PropertyInfo property && !property.CanWrite)
                    continue;

                try
                {
                    SetMemberValue(wrapper, member, referencedObject);
                    return true;
                }
                catch
                {
                    // Try another wrapper member.
                }
            }

            wrapper = null;
            return false;
        }

        private bool AssignObjectOrWrapperV3(
            object target,
            UnityEngine.Object asset,
            params string[] names)
        {
            foreach (string name in names)
            {
                MemberInfo member = FindWritableMember(target.GetType(), name);
                if (member == null)
                    continue;

                Type memberType = GetMemberType(member);

                if (memberType.IsInstanceOfType(asset))
                {
                    SetMemberValue(target, member, asset);
                    return true;
                }

                if (typeof(UnityEngine.Object).IsAssignableFrom(memberType))
                {
                    if (memberType.IsAssignableFrom(asset.GetType()))
                    {
                        SetMemberValue(target, member, asset);
                        return true;
                    }

                    continue;
                }

                object wrapper;
                try
                {
                    wrapper = Activator.CreateInstance(memberType, true);
                }
                catch
                {
                    continue;
                }

                if (wrapper == null)
                    continue;

                if (!SetAnyMemberV3(
                        wrapper,
                        asset,
                        "texture",
                        "cubemap",
                        "asset",
                        "value",
                        "m_Texture",
                        "m_Value"))
                {
                    continue;
                }

                SetMemberValue(target, member, wrapper);
                return true;
            }

            return false;
        }

        private sealed class ReferenceEqualityComparerV3 :
            IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparerV3 Instance =
                new ReferenceEqualityComparerV3();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
