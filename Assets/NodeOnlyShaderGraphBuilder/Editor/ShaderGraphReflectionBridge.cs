using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    internal sealed partial class ShaderGraphReflectionBridge
    {
        internal sealed class GraphHandle : IDisposable
        {
            public UnityEngine.Object graphObject;
            public object graphData;

            public void Dispose()
            {
                if (graphObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(graphObject);
                    graphObject = null;
                }

                graphData = null;
            }
        }

        internal sealed class SlotInfo
        {
            public object rawSlot;
            public object slotReference;
            public int id;
            public string displayName;
            public string shaderOutputName;
            public bool isInput;
            public bool isOutput;

            public override string ToString()
            {
                string direction =
                    isInput ? "Input" :
                    isOutput ? "Output" :
                    "Unknown";

                return direction + " " + displayName +
                       " [" + shaderOutputName + "] #" + id;
            }
        }

        private Assembly shaderGraphAssembly;
        private Type graphObjectType;
        private Type graphDataType;
        private Type multiJsonType;
        private Type abstractMaterialNodeType;
        private Type materialSlotType;
        private Type blockNodeType;

        private Type[] officialNodeTypes = new Type[0];

        public bool IsReady { get; private set; }
        public string InitializationError { get; private set; }

        public ShaderGraphReflectionBridge()
        {
            Initialize();
        }

        public string[] GetAllowedAliases()
        {
            return NodeAliasRegistry.AllowedAliases.ToArray();
        }

        public bool TryResolveAllowedNode(
            string alias,
            out Type type,
            out string reason)
        {
            if (!IsReady)
            {
                type = null;
                reason = InitializationError;
                return false;
            }

            return NodeAliasRegistry.TryResolve(
                alias,
                officialNodeTypes,
                out type,
                out reason);
        }

        public Type[] GetOfficialNodeTypes()
        {
            return officialNodeTypes.ToArray();
        }

        public GraphHandle LoadGraph(string assetPath)
        {
            RequireReady();

            string absolutePath = ToAbsoluteProjectPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Shader Graph asset was not found.",
                    absolutePath);
            }

            string json = File.ReadAllText(absolutePath);

            UnityEngine.Object graphObject =
                ScriptableObject.CreateInstance(graphObjectType);

            graphObject.hideFlags = HideFlags.HideAndDontSave;

            object graphData =
                Activator.CreateInstance(
                    graphDataType,
                    true);

            SetMemberIfPresent(
                graphData,
                "assetGuid",
                AssetDatabase.AssetPathToGUID(assetPath));

            SetMemberIfPresent(graphData, "isSubGraph", false);
            SetMemberIfPresent(graphData, "messageManager", null);
            SetMemberIfPresent(graphData, "owner", graphObject);

            SetMemberRequired(
                graphObject,
                "graph",
                graphData);

            InvokeMultiJsonDeserialize(graphData, json);

            SetMemberIfPresent(graphData, "owner", graphObject);
            SetMemberRequired(graphObject, "graph", graphData);

            InvokeIfPresent(graphData, "OnEnable");
            InvokeRequired(graphData, "ValidateGraph");

            return new GraphHandle
            {
                graphObject = graphObject,
                graphData = graphData
            };
        }

        public void SaveGraph(
            GraphHandle handle,
            string assetPath)
        {
            RequireReady();

            if (handle == null || handle.graphData == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            InvokeRequired(handle.graphData, "ValidateGraph");

            string json = InvokeMultiJsonSerialize(handle.graphData);
            string absolutePath = ToAbsoluteProjectPath(assetPath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(absolutePath) ??
                Application.dataPath);

            File.WriteAllText(absolutePath, json);
        }

        public void ClearDeletableNonBlockNodes(object graphData)
        {
            RequireReady();

            List<object> nodes = GetGraphNodes(graphData)
                .Where(node =>
                    node != null &&
                    !blockNodeType.IsInstanceOfType(node))
                .ToList();

            foreach (object node in nodes)
            {
                bool canDelete =
                    GetMemberValue<bool>(
                        node,
                        "canDeleteNode",
                        true);

                if (!canDelete)
                {
                    continue;
                }

                List<object> edges =
                    InvokeEnumerable(graphData, "GetEdges", node)
                    .Cast<object>()
                    .ToList();

                foreach (object edge in edges)
                {
                    InvokeRequired(graphData, "RemoveEdge", edge);
                }

                InvokeRequired(graphData, "RemoveNode", node);
            }
        }

        public Dictionary<string, object> AddRecipeNodes(
            object graphData,
            RecipeNode[] recipeNodes)
        {
            RequireReady();

            Dictionary<string, object> nodes =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (RecipeNode recipeNode in recipeNodes)
            {
                if (!TryResolveAllowedNode(
                        recipeNode.type,
                        out Type nodeType,
                        out string reason))
                {
                    throw new InvalidOperationException(
                        "Cannot resolve node '" +
                        recipeNode.id + "': " + reason);
                }

                if (NodeAliasRegistry.IsForbiddenNodeType(nodeType))
                {
                    throw new InvalidOperationException(
                        "Forbidden node type resolved for '" +
                        recipeNode.id + "'.");
                }

                object node =
                    Activator.CreateInstance(nodeType, true);

                // Constant-like Shader Graph nodes create their input slots
                // from serialized node members. The values must therefore be
                // applied BEFORE UpdateNodeAfterDeserialization().
                //
                // v1.x applied members after AddNode(), which left Vector1Node
                // input slots at their original current value of zero even
                // though m_Value had been changed. Smoothstep thresholds then
                // all evaluated as zero and the final Alpha became empty.
                ApplyMembers(node, recipeNode);

                InvokeIfPresent(
                    node,
                    "UpdateNodeAfterDeserialization");

                SynchronizeConstantNodeSlots(
                    node,
                    recipeNode);

                SetNodeTitleIfPossible(
                    node,
                    recipeNode.title);

                SetNodePosition(
                    node,
                    new Rect(
                        recipeNode.x,
                        recipeNode.y,
                        240f,
                        180f));

                AddNodeToGraph(
                    graphData,
                    node);

                ApplyInputDefaults(node, recipeNode);

                // Some package versions refresh slots during AddNode().
                // Reapply constant slot state after insertion as well.
                SynchronizeConstantNodeSlots(
                    node,
                    recipeNode);

                nodes.Add(recipeNode.id, node);
            }

            return nodes;
        }

        private void SetNodeTitleIfPossible(
            object node,
            string title)
        {
            if (node == null ||
                string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            if (SetMemberIfPresent(
                    node,
                    "name",
                    title))
            {
                return;
            }

            SetMemberIfPresent(
                node,
                "m_Name",
                title);
        }

        private void SynchronizeConstantNodeSlots(
            object node,
            RecipeNode recipeNode)
        {
            if (node == null || recipeNode == null)
            {
                return;
            }

            RecipeMember valueMember =
                recipeNode.members
                    .FirstOrDefault(
                        member =>
                            member != null &&
                            Normalize(member.name) == "value");

            if (valueMember == null)
            {
                return;
            }

            float[] components =
                ParseFloatComponents(valueMember.value);

            string normalizedType =
                Normalize(node.GetType().Name);

            if (normalizedType == "vector1node")
            {
                RequireComponents(
                    recipeNode,
                    components,
                    1);

                SynchronizeInputSlotState(
                    node,
                    "X",
                    FormatFloat(components[0]));

                return;
            }

            if (normalizedType == "vector2node")
            {
                RequireComponents(
                    recipeNode,
                    components,
                    2);

                SynchronizeInputSlotState(
                    node,
                    "X",
                    FormatFloat(components[0]));

                SynchronizeInputSlotState(
                    node,
                    "Y",
                    FormatFloat(components[1]));

                return;
            }

            if (normalizedType == "vector3node")
            {
                RequireComponents(
                    recipeNode,
                    components,
                    3);

                SynchronizeInputSlotState(
                    node,
                    "X",
                    FormatFloat(components[0]));

                SynchronizeInputSlotState(
                    node,
                    "Y",
                    FormatFloat(components[1]));

                SynchronizeInputSlotState(
                    node,
                    "Z",
                    FormatFloat(components[2]));

                return;
            }

            if (normalizedType == "vector4node")
            {
                RequireComponents(
                    recipeNode,
                    components,
                    4);

                SynchronizeInputSlotState(
                    node,
                    "X",
                    FormatFloat(components[0]));

                SynchronizeInputSlotState(
                    node,
                    "Y",
                    FormatFloat(components[1]));

                SynchronizeInputSlotState(
                    node,
                    "Z",
                    FormatFloat(components[2]));

                SynchronizeInputSlotState(
                    node,
                    "W",
                    FormatFloat(components[3]));
            }
        }

        private static void RequireComponents(
            RecipeNode recipeNode,
            float[] components,
            int requiredCount)
        {
            if (components.Length >= requiredCount)
            {
                return;
            }

            throw new InvalidOperationException(
                "Node '" + recipeNode.id +
                "' requires " + requiredCount +
                " numeric component(s), but its value was '" +
                recipeNode.members
                    .FirstOrDefault(
                        member =>
                            member != null &&
                            Normalize(member.name) == "value")
                    ?.value +
                "'.");
        }

        private void SynchronizeInputSlotState(
            object node,
            string portName,
            string serializedValue)
        {
            SlotInfo slot =
                FindSlot(
                    node,
                    portName,
                    true);

            bool currentAssigned =
                TrySetMemberFromText(
                    slot.rawSlot,
                    "value",
                    serializedValue,
                    out string currentError);

            bool defaultAssigned =
                TrySetMemberFromText(
                    slot.rawSlot,
                    "defaultValue",
                    serializedValue,
                    out string defaultError);

            if (!currentAssigned && !defaultAssigned)
            {
                throw new InvalidOperationException(
                    "Could not synchronize constant node input '" +
                    portName + "' on '" +
                    GetNodeDisplayName(node) +
                    "'. Current-value error: " +
                    currentError +
                    " Default-value error: " +
                    defaultError);
            }
        }

        private static string FormatFloat(float value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private void AddNodeToGraph(
            object graphData,
            object node)
        {
            if (graphData == null)
            {
                throw new ArgumentNullException(nameof(graphData));
            }

            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            List<MethodInfo> addNodeMethods =
                GetAllInstanceMethods(graphData.GetType())
                    .Where(
                        candidate =>
                            candidate.Name == "AddNode" &&
                            !candidate.IsGenericMethodDefinition)
                    .OrderByDescending(
                        candidate =>
                            candidate.GetParameters().Length)
                    .ToList();

            foreach (MethodInfo method in addNodeMethods)
            {
                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length == 0)
                {
                    continue;
                }

                if (!parameters[0]
                        .ParameterType
                        .IsInstanceOfType(node))
                {
                    continue;
                }

                object[] arguments =
                    new object[parameters.Length];

                arguments[0] = node;

                bool compatible = true;

                for (int index = 1;
                     index < parameters.Length;
                     index++)
                {
                    ParameterInfo parameter =
                        parameters[index];

                    if (parameter.ParameterType == typeof(bool))
                    {
                        // Shader Graph versions use this flag for such
                        // options as applying the new-node preview
                        // preference. False keeps generation deterministic.
                        arguments[index] = false;
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        arguments[index] = parameter.DefaultValue;
                    }
                    else if (!parameter.ParameterType.IsValueType ||
                             Nullable.GetUnderlyingType(
                                 parameter.ParameterType) != null)
                    {
                        arguments[index] = null;
                    }
                    else
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                {
                    continue;
                }

                method.Invoke(graphData, arguments);
                return;
            }

            // Last-resort compatibility path for package versions where
            // the public AddNode wrapper changed but AddNodeNoValidate
            // remains available internally.
            MethodInfo addNodeNoValidate =
                GetAllInstanceMethods(graphData.GetType())
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == "AddNodeNoValidate" &&
                            !candidate.IsGenericMethodDefinition &&
                            candidate.GetParameters().Length == 1 &&
                            candidate.GetParameters()[0]
                                .ParameterType
                                .IsInstanceOfType(node));

            if (addNodeNoValidate != null)
            {
                addNodeNoValidate.Invoke(
                    graphData,
                    new[] { node });

                InvokeRequired(
                    graphData,
                    "ValidateGraph");

                return;
            }

            string availableSignatures =
                string.Join(
                    "\n",
                    GetAllInstanceMethods(graphData.GetType())
                        .Where(
                            candidate =>
                                candidate.Name.Contains(
                                    "AddNode",
                                    StringComparison.Ordinal))
                        .Select(FormatMethodSignature));

            if (string.IsNullOrWhiteSpace(availableSignatures))
            {
                availableSignatures = "(none)";
            }

            throw new MissingMethodException(
                "No compatible GraphData.AddNode overload was found " +
                "for node type '" + node.GetType().FullName + "'.\n" +
                "Available AddNode-like methods:\n" +
                availableSignatures);
        }

        private static string FormatMethodSignature(
            MethodInfo method)
        {
            string parameters =
                string.Join(
                    ", ",
                    method.GetParameters()
                        .Select(
                            parameter =>
                                parameter.ParameterType.FullName +
                                " " +
                                parameter.Name +
                                (parameter.HasDefaultValue
                                    ? " = " +
                                      (
                                          parameter.DefaultValue ??
                                          "null"
                                      )
                                    : string.Empty)));

            return
                method.ReturnType.FullName +
                " " +
                method.DeclaringType?.FullName +
                "." +
                method.Name +
                "(" +
                parameters +
                ")";
        }

        public void ConnectRecipeEdges(
            object graphData,
            Dictionary<string, object> nodes,
            RecipeEdge[] edges)
        {
            foreach (RecipeEdge edge in edges)
            {
                object fromNode = nodes[edge.fromNode];
                object toNode = nodes[edge.toNode];

                SlotInfo outputSlot =
                    FindSlot(fromNode, edge.fromPort, false);

                SlotInfo inputSlot =
                    FindSlot(toNode, edge.toPort, true);

                object connected =
                    InvokeRequired(
                        graphData,
                        "Connect",
                        outputSlot.slotReference,
                        inputSlot.slotReference);

                if (connected == null)
                {
                    throw new InvalidOperationException(
                        "Shader Graph rejected edge " +
                        edge.fromNode + "." + edge.fromPort +
                        " -> " +
                        edge.toNode + "." + edge.toPort + ".");
                }
            }
        }

        public void ConnectRecipeOutputs(
            object graphData,
            Dictionary<string, object> nodes,
            RecipeOutput[] outputs)
        {
            List<object> blocks = GetGraphNodes(graphData)
                .Where(node =>
                    node != null &&
                    blockNodeType.IsInstanceOfType(node))
                .ToList();

            foreach (RecipeOutput output in outputs)
            {
                object sourceNode = nodes[output.fromNode];
                SlotInfo sourceSlot =
                    FindSlot(sourceNode, output.fromPort, false);

                object block;

                try
                {
                    block =
                        FindMasterStackBlock(
                            blocks,
                            output.block);
                }
                catch (InvalidOperationException exception)
                {
                    if (IsOptionalMissingOutputBlock(output.block))
                    {
                        Debug.LogWarning(
                            "[Node-Only Shader Graph Builder] " +
                            "Optional Master Stack block '" +
                            output.block +
                            "' is not present in the selected template. " +
                            "The connection was skipped. " +
                            "For an Additive transparent graph this is " +
                            "safe because the generated Base Color is " +
                            "already multiplied by the effect mask.\n" +
                            exception.Message);

                        continue;
                    }

                    throw;
                }

                SlotInfo blockInput =
                    FindFirstInputSlot(block);

                object connected =
                    InvokeRequired(
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
            }
        }

        public List<SlotInfo> GetSlots(object node)
        {
            RequireReady();

            List<object> rawSlots =
                InvokeGenericSlotCollection(node, "GetSlots");

            if (rawSlots.Count == 0)
            {
                rawSlots.AddRange(
                    InvokeGenericSlotCollection(
                        node,
                        "GetInputSlots"));

                rawSlots.AddRange(
                    InvokeGenericSlotCollection(
                        node,
                        "GetOutputSlots"));
            }

            return rawSlots
                .Where(slot => slot != null)
                .Distinct()
                .Select(CreateSlotInfo)
                .ToList();
        }

        public string GetNodeDisplayName(object node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            string name =
                GetMemberValue<string>(node, "name", null);

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return node.GetType().Name;
        }

        private void Initialize()
        {
            try
            {
                shaderGraphAssembly =
                    AppDomain.CurrentDomain
                        .GetAssemblies()
                        .FirstOrDefault(
                            assembly =>
                                assembly.GetName().Name ==
                                "Unity.ShaderGraph.Editor");

                if (shaderGraphAssembly == null)
                {
                    shaderGraphAssembly =
                        Assembly.Load("Unity.ShaderGraph.Editor");
                }

                graphObjectType =
                    FindType(
                        "UnityEditor.Graphing.GraphObject",
                        "UnityEditor.ShaderGraph.GraphObject");

                graphDataType =
                    FindType("UnityEditor.ShaderGraph.GraphData");

                multiJsonType =
                    FindType(
                        "UnityEditor.ShaderGraph.Serialization.MultiJson");

                abstractMaterialNodeType =
                    FindType(
                        "UnityEditor.ShaderGraph.AbstractMaterialNode");

                materialSlotType =
                    FindType(
                        "UnityEditor.ShaderGraph.MaterialSlot");

                blockNodeType =
                    FindType(
                        "UnityEditor.ShaderGraph.BlockNode");

                officialNodeTypes =
                    AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Where(IsOfficialUnityEditorAssembly)
                        .SelectMany(GetLoadableTypes)
                        .Where(type =>
                            type != null &&
                            type.IsClass &&
                            !type.IsAbstract &&
                            !type.IsGenericTypeDefinition &&
                            abstractMaterialNodeType
                                .IsAssignableFrom(type))
                        .ToArray();

                IsReady = true;
                InitializationError = string.Empty;
            }
            catch (Exception exception)
            {
                IsReady = false;
                InitializationError =
                    "Shader Graph reflection initialization failed:\n" +
                    exception;
            }
        }

        private static bool IsOfficialUnityEditorAssembly(
            Assembly assembly)
        {
            string name = assembly.GetName().Name ?? string.Empty;

            return
                name == "Unity.ShaderGraph.Editor" ||
                name.StartsWith(
                    "Unity.RenderPipelines.",
                    StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetLoadableTypes(
            Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private Type FindType(params string[] fullNames)
        {
            foreach (string fullName in fullNames)
            {
                Type type =
                    shaderGraphAssembly.GetType(
                        fullName,
                        false);

                if (type != null)
                {
                    return type;
                }

                type =
                    AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Select(
                            assembly =>
                                assembly.GetType(
                                    fullName,
                                    false))
                        .FirstOrDefault(
                            candidate => candidate != null);

                if (type != null)
                {
                    return type;
                }
            }

            throw new TypeLoadException(
                "Could not find required Shader Graph type: " +
                string.Join(" or ", fullNames));
        }

        private void InvokeMultiJsonDeserialize(
            object graphData,
            string json)
        {
            MethodInfo method =
                multiJsonType
                    .GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == "Deserialize" &&
                            candidate.IsGenericMethodDefinition &&
                            candidate.GetParameters().Length >= 2);

            if (method == null)
            {
                throw new MissingMethodException(
                    multiJsonType.FullName,
                    "Deserialize<T>");
            }

            MethodInfo concrete =
                method.MakeGenericMethod(graphDataType);

            ParameterInfo[] parameters =
                concrete.GetParameters();

            object[] arguments =
                new object[parameters.Length];

            arguments[0] = graphData;
            arguments[1] = json;

            for (int index = 2;
                 index < parameters.Length;
                 index++)
            {
                Type type = parameters[index].ParameterType;

                if (type == typeof(bool))
                {
                    arguments[index] = false;
                }
                else if (parameters[index].HasDefaultValue)
                {
                    arguments[index] =
                        parameters[index].DefaultValue;
                }
                else
                {
                    arguments[index] = null;
                }
            }

            concrete.Invoke(null, arguments);
        }

        private string InvokeMultiJsonSerialize(object graphData)
        {
            MethodInfo method =
                multiJsonType
                    .GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Where(candidate =>
                        candidate.Name == "Serialize" &&
                        !candidate.IsGenericMethod)
                    .OrderBy(candidate =>
                        candidate.GetParameters().Length)
                    .FirstOrDefault(candidate =>
                    {
                        ParameterInfo[] parameters =
                            candidate.GetParameters();

                        return
                            parameters.Length >= 1 &&
                            parameters[0].ParameterType
                                .IsAssignableFrom(
                                    graphData.GetType());
                    });

            if (method == null)
            {
                throw new MissingMethodException(
                    multiJsonType.FullName,
                    "Serialize");
            }

            ParameterInfo[] parametersInfo =
                method.GetParameters();

            object[] arguments =
                new object[parametersInfo.Length];

            arguments[0] = graphData;

            for (int index = 1;
                 index < parametersInfo.Length;
                 index++)
            {
                if (parametersInfo[index].HasDefaultValue)
                {
                    arguments[index] =
                        parametersInfo[index].DefaultValue;
                }
                else
                {
                    arguments[index] = null;
                }
            }

            object result = method.Invoke(null, arguments);

            if (!(result is string json))
            {
                throw new InvalidOperationException(
                    "MultiJson.Serialize did not return a string.");
            }

            return json;
        }

        private List<object> GetGraphNodes(object graphData)
        {
            // Unity 6 / Shader Graph 17 exposes GraphData.GetNodes<T>()
            // as a generic, parameterless method. Older package versions may
            // expose a non-generic GetNodes(), so support both shapes.
            MethodInfo nonGenericMethod =
                GetAllInstanceMethods(graphData.GetType())
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == "GetNodes" &&
                            !candidate.IsGenericMethod &&
                            candidate.GetParameters().Length == 0);

            if (nonGenericMethod != null)
            {
                object nonGenericResult =
                    nonGenericMethod.Invoke(graphData, null);

                if (nonGenericResult is IEnumerable nonGenericEnumerable)
                {
                    return nonGenericEnumerable
                        .Cast<object>()
                        .Where(node => node != null)
                        .ToList();
                }
            }

            MethodInfo genericMethod =
                GetAllInstanceMethods(graphData.GetType())
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == "GetNodes" &&
                            candidate.IsGenericMethodDefinition &&
                            candidate.GetGenericArguments().Length == 1 &&
                            candidate.GetParameters().Length == 0);

            if (genericMethod == null)
            {
                throw new MissingMethodException(
                    graphData.GetType().FullName,
                    "GetNodes<T>()");
            }

            MethodInfo concreteMethod =
                genericMethod.MakeGenericMethod(
                    abstractMaterialNodeType);

            object genericResult =
                concreteMethod.Invoke(graphData, null);

            if (!(genericResult is IEnumerable genericEnumerable))
            {
                throw new InvalidOperationException(
                    "GraphData.GetNodes<T>() did not return IEnumerable.");
            }

            return genericEnumerable
                .Cast<object>()
                .Where(node => node != null)
                .ToList();
        }

        private IEnumerable InvokeEnumerable(
            object target,
            string methodName,
            params object[] arguments)
        {
            object result =
                InvokeRequired(
                    target,
                    methodName,
                    arguments);

            if (result is IEnumerable enumerable)
            {
                return enumerable;
            }

            return new object[0];
        }

        private List<object> InvokeGenericSlotCollection(
            object node,
            string methodName)
        {
            List<object> result = new List<object>();

            MethodInfo method =
                GetAllInstanceMethods(node.GetType())
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == methodName &&
                            candidate.IsGenericMethodDefinition &&
                            candidate.GetGenericArguments().Length == 1 &&
                            candidate.GetParameters().Length == 1);

            if (method == null)
            {
                return result;
            }

            Type listType =
                typeof(List<>).MakeGenericType(materialSlotType);

            object list =
                Activator.CreateInstance(listType);

            MethodInfo concrete =
                method.MakeGenericMethod(materialSlotType);

            concrete.Invoke(node, new[] { list });

            foreach (object slot in (IEnumerable)list)
            {
                result.Add(slot);
            }

            return result;
        }

        private SlotInfo CreateSlotInfo(object slot)
        {
            object owner =
                GetMemberValue<object>(
                    slot,
                    "owner",
                    null);

            int id =
                GetMemberValue<int>(
                    slot,
                    "id",
                    -1);

            object slotReference =
                GetMemberValue<object>(
                    slot,
                    "slotReference",
                    null);

            if (slotReference == null &&
                owner != null &&
                id >= 0)
            {
                slotReference =
                    InvokeRequired(
                        owner,
                        "GetSlotReference",
                        id);
            }

            return new SlotInfo
            {
                rawSlot = slot,
                slotReference = slotReference,
                id = id,
                displayName =
                    GetMemberValue<string>(
                        slot,
                        "displayName",
                        string.Empty),
                shaderOutputName =
                    GetMemberValue<string>(
                        slot,
                        "shaderOutputName",
                        string.Empty),
                isInput =
                    GetMemberValue<bool>(
                        slot,
                        "isInputSlot",
                        false),
                isOutput =
                    GetMemberValue<bool>(
                        slot,
                        "isOutputSlot",
                        false)
            };
        }

        private SlotInfo FindSlot(
            object node,
            string requestedName,
            bool requireInput)
        {
            List<SlotInfo> slots =
                GetSlots(node)
                    .Where(slot =>
                        requireInput
                            ? slot.isInput
                            : slot.isOutput)
                    .ToList();

            string requested =
                Normalize(requestedName);

            SlotInfo exact =
                slots.FirstOrDefault(
                    slot =>
                        Normalize(slot.displayName) == requested ||
                        Normalize(slot.shaderOutputName) == requested);

            if (exact != null)
            {
                return exact;
            }

            if (requested == "out" && slots.Count == 1)
            {
                return slots[0];
            }

            if (slots.Count == 1)
            {
                return slots[0];
            }

            string available =
                string.Join(
                    ", ",
                    slots.Select(slot => slot.ToString()));

            throw new InvalidOperationException(
                "Port '" + requestedName +
                "' was not found on node '" +
                GetNodeDisplayName(node) +
                "'. Available ports: " + available);
        }

        private SlotInfo FindFirstInputSlot(object node)
        {
            List<SlotInfo> slots =
                GetSlots(node)
                    .Where(slot => slot.isInput)
                    .ToList();

            if (slots.Count == 0)
            {
                throw new InvalidOperationException(
                    "Master Stack block '" +
                    GetBlockDisplayName(node) +
                    "' has no input slot.");
            }

            return slots[0];
        }

        private static bool IsOptionalMissingOutputBlock(
            string blockName)
        {
            string normalized =
                Normalize(blockName);

            return
                normalized == "alpha" ||
                normalized == "alphaclipthreshold";
        }

        private object FindMasterStackBlock(
            List<object> blocks,
            string requestedName)
        {
            string requested = Normalize(requestedName);

            object exact =
                blocks.FirstOrDefault(
                    block =>
                        Normalize(
                            GetBlockDisplayName(block)) ==
                        requested);

            if (exact != null)
            {
                return exact;
            }

            IEnumerable<object> candidates =
                blocks.Where(block =>
                {
                    string blockName =
                        Normalize(
                            GetBlockDisplayName(block));

                    if (requested == "alpha")
                    {
                        return
                            blockName.Contains("alpha") &&
                            !blockName.Contains("clip");
                    }

                    return
                        blockName.Contains(requested) ||
                        requested.Contains(blockName);
                });

            object candidate =
                candidates.FirstOrDefault();

            if (candidate != null)
            {
                return candidate;
            }

            string available =
                string.Join(
                    ", ",
                    blocks.Select(GetBlockDisplayName));

            throw new InvalidOperationException(
                "Master Stack block '" + requestedName +
                "' is not present in the selected template. " +
                "Available blocks: " + available);
        }

        private string GetBlockDisplayName(object block)
        {
            object descriptor =
                GetMemberValue<object>(
                    block,
                    "descriptor",
                    null);

            if (descriptor != null)
            {
                string displayName =
                    GetMemberValue<string>(
                        descriptor,
                        "displayName",
                        null);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }

                string descriptorName =
                    GetMemberValue<string>(
                        descriptor,
                        "name",
                        null);

                if (!string.IsNullOrWhiteSpace(descriptorName))
                {
                    return descriptorName;
                }
            }

            return GetNodeDisplayName(block);
        }

        private void ApplyMembers(
            object node,
            RecipeNode recipeNode)
        {
            foreach (RecipeMember member in recipeNode.members)
            {
                if (member == null ||
                    string.IsNullOrWhiteSpace(member.name))
                {
                    continue;
                }

                if (!TrySetMemberFromText(
                        node,
                        member.name,
                        member.value,
                        out string error))
                {
                    throw new InvalidOperationException(
                        "Node '" + recipeNode.id +
                        "' member '" + member.name +
                        "' could not be set: " + error);
                }
            }
        }

        private void ApplyInputDefaults(
            object node,
            RecipeNode recipeNode)
        {
            foreach (RecipeInputDefault inputDefault
                     in recipeNode.inputDefaults)
            {
                if (inputDefault == null ||
                    string.IsNullOrWhiteSpace(inputDefault.port))
                {
                    continue;
                }

                SlotInfo slot =
                    FindSlot(
                        node,
                        inputDefault.port,
                        true);

                bool currentAssigned =
                    TrySetMemberFromText(
                        slot.rawSlot,
                        "value",
                        inputDefault.value,
                        out string currentError);

                bool defaultAssigned =
                    TrySetMemberFromText(
                        slot.rawSlot,
                        "defaultValue",
                        inputDefault.value,
                        out string defaultError);

                if (!currentAssigned && !defaultAssigned)
                {
                    throw new InvalidOperationException(
                        "Node '" + recipeNode.id +
                        "' input default for port '" +
                        inputDefault.port +
                        "' could not be assigned. " +
                        "Current-value error: " +
                        currentError +
                        " Default-value error: " +
                        defaultError);
                }
            }
        }

        private bool TrySetMemberFromText(
            object target,
            string requestedName,
            string text,
            out string error)
        {
            error = string.Empty;

            MemberInfo member =
                FindWritableMember(
                    target.GetType(),
                    requestedName);

            if (member == null)
            {
                string[] fallbackNames =
                    GetFallbackMemberNames(requestedName);

                foreach (string fallbackName
                         in fallbackNames)
                {
                    member =
                        FindWritableMember(
                            target.GetType(),
                            fallbackName);

                    if (member != null)
                    {
                        break;
                    }
                }
            }

            if (member == null)
            {
                error =
                    "Writable member was not found on " +
                    target.GetType().FullName + ".";
                return false;
            }

            Type valueType = GetMemberType(member);

            if (!TryConvertText(
                    text,
                    valueType,
                    out object value,
                    out error))
            {
                return false;
            }

            SetMemberValue(target, member, value);
            return true;
        }

        private static string[] GetFallbackMemberNames(
            string requestedName)
        {
            string normalized = Normalize(requestedName);

            if (normalized == "color")
            {
                return new[]
                {
                    "value",
                    "m_Color",
                    "m_Value"
                };
            }

            if (normalized == "value")
            {
                return new[]
                {
                    "m_Value",
                    "color",
                    "m_Color"
                };
            }

            return new[]
            {
                "m_" + requestedName
            };
        }

        private static MemberInfo FindWritableMember(
            Type type,
            string requestedName)
        {
            string normalized =
                Normalize(requestedName);

            foreach (PropertyInfo property
                     in GetAllProperties(type))
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                if (Normalize(property.Name) == normalized)
                {
                    return property;
                }
            }

            foreach (FieldInfo field in GetAllFields(type))
            {
                if (Normalize(field.Name) == normalized)
                {
                    return field;
                }
            }

            return null;
        }

        private static bool TryConvertText(
            string text,
            Type targetType,
            out object value,
            out string error)
        {
            value = null;
            error = string.Empty;

            Type nullableType =
                Nullable.GetUnderlyingType(targetType);

            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType == typeof(string))
            {
                value = text ?? string.Empty;
                return true;
            }

            if (targetType == typeof(Guid))
            {
                if (Guid.TryParse(text, out Guid guidValue))
                {
                    value = guidValue;
                    return true;
                }

                error = "Expected a GUID string.";
                return false;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float floatValue))
                {
                    value = floatValue;
                    return true;
                }

                error = "Expected a floating-point number.";
                return false;
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double doubleValue))
                {
                    value = doubleValue;
                    return true;
                }

                error = "Expected a double-precision number.";
                return false;
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(
                        text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int intValue))
                {
                    value = intValue;
                    return true;
                }

                error = "Expected an integer.";
                return false;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(
                        text,
                        out bool boolValue))
                {
                    value = boolValue;
                    return true;
                }

                error = "Expected true or false.";
                return false;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    value =
                        Enum.Parse(
                            targetType,
                            text,
                            true);
                    return true;
                }
                catch
                {
                    error =
                        "Expected one of: " +
                        string.Join(
                            ", ",
                            Enum.GetNames(targetType));
                    return false;
                }
            }

            float[] components =
                ParseFloatComponents(text);

            if (targetType == typeof(Color) &&
                components.Length >= 3)
            {
                value =
                    new Color(
                        components[0],
                        components[1],
                        components[2],
                        components.Length >= 4
                            ? components[3]
                            : 1f);
                return true;
            }

            if (IsShaderGraphColorWrapper(targetType))
            {
                if (components.Length < 3)
                {
                    error =
                        "Expected at least RGB components for " +
                        targetType.FullName + ".";
                    return false;
                }

                if (TryCreateShaderGraphColorWrapper(
                        targetType,
                        components,
                        out value,
                        out error))
                {
                    return true;
                }

                return false;
            }

            if (targetType == typeof(Vector2) &&
                components.Length >= 1)
            {
                value =
                    components.Length == 1
                        ? new Vector2(
                            components[0],
                            components[0])
                        : new Vector2(
                            components[0],
                            components[1]);
                return true;
            }

            if (targetType == typeof(Vector3) &&
                components.Length >= 1)
            {
                value =
                    components.Length == 1
                        ? new Vector3(
                            components[0],
                            components[0],
                            components[0])
                        : new Vector3(
                            components[0],
                            components[1],
                            components.Length >= 3
                                ? components[2]
                                : components[1]);
                return true;
            }

            if (targetType == typeof(Vector4) &&
                components.Length >= 1)
            {
                value =
                    components.Length == 1
                        ? new Vector4(
                            components[0],
                            components[0],
                            components[0],
                            components[0])
                        : new Vector4(
                            components[0],
                            components[1],
                            components.Length >= 3
                                ? components[2]
                                : components[1],
                            components.Length >= 4
                                ? components[3]
                                : components[
                                    components.Length - 1]);
                return true;
            }

            if (targetType == typeof(Matrix4x4) &&
                components.Length >= 1)
            {
                Matrix4x4 matrix = Matrix4x4.identity;

                if (components.Length == 1)
                {
                    matrix = Matrix4x4.zero;
                    matrix.m00 = components[0];
                    matrix.m11 = components[0];
                    matrix.m22 = components[0];
                    matrix.m33 = components[0];
                }
                else
                {
                    for (int index = 0;
                         index < Math.Min(16, components.Length);
                         index++)
                    {
                        int row = index / 4;
                        int column = index % 4;
                        matrix[row, column] = components[index];
                    }
                }

                value = matrix;
                return true;
            }

            if (Normalize(targetType.Name).Contains("matrix") &&
                components.Length >= 1)
            {
                try
                {
                    object matrixWrapper =
                        Activator.CreateInstance(targetType, true);

                    bool assignedAny = false;

                    for (int row = 0; row < 4; row++)
                    {
                        for (int column = 0; column < 4; column++)
                        {
                            int index = row * 4 + column;
                            if (index >= components.Length)
                                continue;

                            MemberInfo matrixMember =
                                FindWritableMember(
                                    targetType,
                                    "e" + row + column);

                            if (matrixMember == null)
                            {
                                matrixMember =
                                    FindWritableMember(
                                        targetType,
                                        "m" + row + column);
                            }

                            if (matrixMember == null)
                                continue;

                            SetMemberValue(
                                matrixWrapper,
                                matrixMember,
                                components[index]);

                            assignedAny = true;
                        }
                    }

                    if (assignedAny)
                    {
                        value = matrixWrapper;
                        return true;
                    }
                }
                catch
                {
                    // Continue to the normal unsupported-type error.
                }
            }

            error =
                "Unsupported member value type: " +
                targetType.FullName;
            return false;
        }

        private static bool IsShaderGraphColorWrapper(
            Type targetType)
        {
            if (targetType == null)
            {
                return false;
            }

            Type declaringType = targetType.DeclaringType;

            return
                declaringType != null &&
                Normalize(declaringType.Name) == "colornode" &&
                Normalize(targetType.Name) == "color";
        }

        private static bool TryCreateShaderGraphColorWrapper(
            Type wrapperType,
            float[] components,
            out object value,
            out string error)
        {
            value = null;
            error = string.Empty;

            Color unityColor =
                new Color(
                    components[0],
                    components[1],
                    components[2],
                    components.Length >= 4
                        ? components[3]
                        : 1f);

            bool useHdr =
                unityColor.r > 1f ||
                unityColor.g > 1f ||
                unityColor.b > 1f ||
                unityColor.r < 0f ||
                unityColor.g < 0f ||
                unityColor.b < 0f;

            ConstructorInfo[] constructors =
                wrapperType.GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            foreach (ConstructorInfo constructor
                     in constructors
                         .OrderByDescending(
                             candidate =>
                                 candidate.GetParameters().Length))
            {
                ParameterInfo[] parameters =
                    constructor.GetParameters();

                object[] arguments =
                    new object[parameters.Length];

                bool compatible = true;
                bool assignedColor = false;

                for (int index = 0;
                     index < parameters.Length;
                     index++)
                {
                    Type parameterType =
                        parameters[index].ParameterType;

                    if (parameterType == typeof(Color))
                    {
                        arguments[index] = unityColor;
                        assignedColor = true;
                    }
                    else if (parameterType.IsEnum)
                    {
                        arguments[index] =
                            SelectColorModeEnumValue(
                                parameterType,
                                useHdr);
                    }
                    else if (parameterType == typeof(bool))
                    {
                        arguments[index] = useHdr;
                    }
                    else if (parameters[index].HasDefaultValue)
                    {
                        arguments[index] =
                            parameters[index].DefaultValue;
                    }
                    else
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible || !assignedColor)
                {
                    continue;
                }

                try
                {
                    value =
                        constructor.Invoke(arguments);

                    return true;
                }
                catch (Exception exception)
                {
                    error =
                        "Color wrapper constructor failed: " +
                        exception.GetBaseException().Message;
                }
            }

            // Compatibility fallback for package versions where the nested
            // wrapper has no usable constructor but exposes serialized
            // fields or properties.
            try
            {
                object wrapper =
                    Activator.CreateInstance(
                        wrapperType,
                        true);

                bool assignedColor =
                    TryAssignWrapperMember(
                        wrapper,
                        new[]
                        {
                            "color",
                            "value",
                            "m_Color",
                            "m_Value"
                        },
                        unityColor);

                bool assignedMode =
                    TryAssignWrapperEnumMember(
                        wrapper,
                        new[]
                        {
                            "mode",
                            "colorMode",
                            "m_Mode",
                            "m_ColorMode"
                        },
                        useHdr);

                if (assignedColor)
                {
                    value = wrapper;
                    return true;
                }

                error =
                    "No UnityEngine.Color constructor or writable color " +
                    "member was found on " +
                    wrapperType.FullName +
                    ". Mode assigned: " +
                    assignedMode + ".";
                return false;
            }
            catch (Exception exception)
            {
                error =
                    "Could not construct " +
                    wrapperType.FullName +
                    ": " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        private static object SelectColorModeEnumValue(
            Type enumType,
            bool useHdr)
        {
            string[] names =
                Enum.GetNames(enumType);

            string[] preferredNames =
                useHdr
                    ? new[]
                    {
                        "HDR",
                        "HighDynamicRange",
                        "Default",
                        "LDR"
                    }
                    : new[]
                    {
                        "Default",
                        "LDR",
                        "HDR",
                        "HighDynamicRange"
                    };

            foreach (string preferredName in preferredNames)
            {
                string matchedName =
                    names.FirstOrDefault(
                        name =>
                            string.Equals(
                                name,
                                preferredName,
                                StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchedName))
                {
                    return Enum.Parse(
                        enumType,
                        matchedName,
                        true);
                }
            }

            Array values = Enum.GetValues(enumType);

            if (values.Length > 0)
            {
                return values.GetValue(0);
            }

            return Activator.CreateInstance(enumType);
        }

        private static bool TryAssignWrapperMember(
            object target,
            string[] candidateNames,
            object memberValue)
        {
            foreach (string candidateName in candidateNames)
            {
                MemberInfo member =
                    FindWritableMember(
                        target.GetType(),
                        candidateName);

                if (member == null)
                {
                    continue;
                }

                Type memberType =
                    GetMemberType(member);

                if (!memberType.IsInstanceOfType(memberValue))
                {
                    continue;
                }

                try
                {
                    SetMemberValue(
                        target,
                        member,
                        memberValue);

                    return true;
                }
                catch
                {
                    // Try the next compatible serialized member.
                }
            }

            return false;
        }

        private static bool TryAssignWrapperEnumMember(
            object target,
            string[] candidateNames,
            bool useHdr)
        {
            foreach (string candidateName in candidateNames)
            {
                MemberInfo member =
                    FindWritableMember(
                        target.GetType(),
                        candidateName);

                if (member == null)
                {
                    continue;
                }

                Type memberType =
                    GetMemberType(member);

                if (!memberType.IsEnum)
                {
                    continue;
                }

                try
                {
                    SetMemberValue(
                        target,
                        member,
                        SelectColorModeEnumValue(
                            memberType,
                            useHdr));

                    return true;
                }
                catch
                {
                    // Try the next enum member candidate.
                }
            }

            return false;
        }

        private static float[] ParseFloatComponents(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new float[0];
            }

            string cleaned =
                text.Replace("(", string.Empty)
                    .Replace(")", string.Empty)
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty);

            string[] parts =
                cleaned.Split(
                    new[] { ',', ';', ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            List<float> values = new List<float>();

            foreach (string part in parts)
            {
                if (!float.TryParse(
                        part,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float parsed))
                {
                    return new float[0];
                }

                values.Add(parsed);
            }

            return values.ToArray();
        }

        private static void SetNodePosition(
            object node,
            Rect desiredPosition)
        {
            MemberInfo drawStateMember =
                FindReadableMember(
                    node.GetType(),
                    "drawState");

            if (drawStateMember == null)
            {
                return;
            }

            object drawState =
                GetMemberValue(node, drawStateMember);

            if (drawState == null)
            {
                return;
            }

            MemberInfo positionMember =
                FindWritableMember(
                    drawState.GetType(),
                    "position");

            if (positionMember == null)
            {
                return;
            }

            Rect current =
                GetMemberValue(drawState, positionMember)
                is Rect currentRect
                    ? currentRect
                    : desiredPosition;

            if (current.width > 1f)
            {
                desiredPosition.width = current.width;
            }

            if (current.height > 1f)
            {
                desiredPosition.height = current.height;
            }

            SetMemberValue(
                drawState,
                positionMember,
                desiredPosition);

            SetMemberValue(
                node,
                drawStateMember,
                drawState);
        }

        private static MemberInfo FindReadableMember(
            Type type,
            string requestedName)
        {
            string normalized =
                Normalize(requestedName);

            PropertyInfo property =
                GetAllProperties(type)
                    .FirstOrDefault(candidate =>
                        candidate.CanRead &&
                        Normalize(candidate.Name) ==
                        normalized);

            if (property != null)
            {
                return property;
            }

            return GetAllFields(type)
                .FirstOrDefault(candidate =>
                    Normalize(candidate.Name) ==
                    normalized);
        }

        private static IEnumerable<PropertyInfo> GetAllProperties(
            Type type)
        {
            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                foreach (PropertyInfo property
                         in current.GetProperties(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    yield return property;
                }
            }
        }

        private static IEnumerable<FieldInfo> GetAllFields(
            Type type)
        {
            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                foreach (FieldInfo field
                         in current.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    yield return field;
                }
            }
        }

        private static IEnumerable<MethodInfo>
            GetAllInstanceMethods(Type type)
        {
            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                foreach (MethodInfo method
                         in current.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    yield return method;
                }
            }
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is PropertyInfo property)
            {
                return property.PropertyType;
            }

            return ((FieldInfo)member).FieldType;
        }

        private static object GetMemberValue(
            object target,
            MemberInfo member)
        {
            if (member is PropertyInfo property)
            {
                return property.GetValue(target, null);
            }

            return ((FieldInfo)member).GetValue(target);
        }

        private static void SetMemberValue(
            object target,
            MemberInfo member,
            object value)
        {
            if (member is PropertyInfo property)
            {
                property.SetValue(target, value, null);
                return;
            }

            ((FieldInfo)member).SetValue(target, value);
        }

        private static T GetMemberValue<T>(
            object target,
            string name,
            T fallback)
        {
            MemberInfo member =
                FindReadableMember(
                    target.GetType(),
                    name);

            if (member == null)
            {
                return fallback;
            }

            object value =
                GetMemberValue(target, member);

            if (value is T typed)
            {
                return typed;
            }

            return fallback;
        }

        private static void SetMemberRequired(
            object target,
            string name,
            object value)
        {
            MemberInfo member =
                FindWritableMember(
                    target.GetType(),
                    name);

            if (member == null)
            {
                throw new MissingMemberException(
                    target.GetType().FullName,
                    name);
            }

            SetMemberValue(target, member, value);
        }

        private static bool SetMemberIfPresent(
            object target,
            string name,
            object value)
        {
            MemberInfo member =
                FindWritableMember(
                    target.GetType(),
                    name);

            if (member == null)
            {
                return false;
            }

            SetMemberValue(target, member, value);
            return true;
        }

        private static object InvokeRequired(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method =
                FindCompatibleMethod(
                    target.GetType(),
                    methodName,
                    arguments);

            if (method == null)
            {
                throw new MissingMethodException(
                    target.GetType().FullName,
                    methodName);
            }

            return method.Invoke(target, arguments);
        }

        private static bool InvokeIfPresent(
            object target,
            string methodName)
        {
            MethodInfo method =
                GetAllInstanceMethods(target.GetType())
                    .FirstOrDefault(
                        candidate =>
                            candidate.Name == methodName &&
                            candidate.GetParameters().Length == 0);

            if (method == null)
            {
                return false;
            }

            method.Invoke(target, null);
            return true;
        }

        private static MethodInfo FindCompatibleMethod(
            Type type,
            string methodName,
            object[] arguments)
        {
            foreach (MethodInfo method
                     in GetAllInstanceMethods(type)
                         .Where(candidate =>
                             candidate.Name == methodName &&
                             !candidate.IsGenericMethodDefinition))
            {
                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != arguments.Length)
                {
                    continue;
                }

                bool compatible = true;

                for (int index = 0;
                     index < parameters.Length;
                     index++)
                {
                    object argument = arguments[index];

                    if (argument == null)
                    {
                        if (parameters[index]
                                .ParameterType
                                .IsValueType &&
                            Nullable.GetUnderlyingType(
                                parameters[index]
                                    .ParameterType) == null)
                        {
                            compatible = false;
                            break;
                        }

                        continue;
                    }

                    if (!parameters[index]
                            .ParameterType
                            .IsInstanceOfType(argument))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (compatible)
                {
                    return method;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] characters =
                value
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray();

            return new string(characters);
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Expected an Assets-relative path.",
                    nameof(assetPath));
            }

            string projectRoot =
                Directory.GetParent(Application.dataPath)
                    .FullName;

            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void RequireReady()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    InitializationError);
            }
        }
    }
}
