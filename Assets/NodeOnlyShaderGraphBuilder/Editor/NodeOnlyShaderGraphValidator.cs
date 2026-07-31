using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    internal static class NodeOnlyShaderGraphValidator
    {
        private const int SafetyNodeLimit = 2048;

        public static List<string> Validate(
            ShaderGraphRecipe recipe,
            ShaderGraphReflectionBridge bridge)
        {
            List<string> errors = new List<string>();

            if (recipe == null)
            {
                errors.Add("Recipe is null.");
                return errors;
            }

            if (recipe.schemaVersion != 1 && recipe.schemaVersion != 3)
            {
                errors.Add(
                    "Unsupported schemaVersion. Expected 1 or 3, received " +
                    recipe.schemaVersion + ".");
            }

            if (recipe.nodes.Length == 0)
                errors.Add("Recipe contains no nodes.");

            if (recipe.nodes.Length > SafetyNodeLimit)
            {
                errors.Add(
                    "Recipe exceeds the safety node limit of " +
                    SafetyNodeLimit + ".");
            }

            Dictionary<string, RecipeProperty> propertiesById =
                ValidateProperties(recipe, errors);

            Dictionary<string, RecipeGroup> groupsById =
                ValidateGroups(recipe, errors);

            Dictionary<string, RecipeNode> nodesById =
                ValidateNodes(
                    recipe,
                    bridge,
                    propertiesById,
                    groupsById,
                    errors);

            ValidateEdges(recipe, nodesById, errors);
            ValidateOutputs(recipe, nodesById, errors);
            ValidateGraphSettings(recipe, errors);

            return errors;
        }

        private static Dictionary<string, RecipeProperty>
            ValidateProperties(
                ShaderGraphRecipe recipe,
                List<string> errors)
        {
            Dictionary<string, RecipeProperty> result =
                new Dictionary<string, RecipeProperty>(
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> referenceNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RecipeProperty property in recipe.properties)
            {
                if (property == null)
                {
                    errors.Add("Recipe contains a null property entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(property.id))
                {
                    errors.Add("A property has an empty id.");
                    continue;
                }

                if (!result.TryAdd(property.id, property))
                {
                    errors.Add("Duplicate property id: " + property.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(property.type))
                    errors.Add("Property '" + property.id + "' has no type.");

                if (!string.IsNullOrWhiteSpace(property.referenceName))
                {
                    if (!property.referenceName.StartsWith(
                            "_",
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            "Property '" + property.id +
                            "' referenceName should start with '_': " +
                            property.referenceName);
                    }

                    if (!referenceNames.Add(property.referenceName))
                    {
                        errors.Add(
                            "Duplicate material referenceName: " +
                            property.referenceName);
                    }
                }

                string normalizedType =
                    NodeAliasRegistry.Normalize(property.type);

                if (normalizedType == "slider" &&
                    property.rangeMax <= property.rangeMin)
                {
                    errors.Add(
                        "Slider property '" + property.id +
                        "' requires rangeMax > rangeMin.");
                }

                if (!string.IsNullOrWhiteSpace(property.assetPath) &&
                    AssetDatabase.LoadMainAssetAtPath(property.assetPath) == null)
                {
                    errors.Add(
                        "Property '" + property.id +
                        "' assetPath was not found: " + property.assetPath);
                }
            }

            return result;
        }

        private static Dictionary<string, RecipeGroup> ValidateGroups(
            ShaderGraphRecipe recipe,
            List<string> errors)
        {
            Dictionary<string, RecipeGroup> result =
                new Dictionary<string, RecipeGroup>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (RecipeGroup group in recipe.groups)
            {
                if (group == null)
                {
                    errors.Add("Recipe contains a null group entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(group.id))
                {
                    errors.Add("A group has an empty id.");
                    continue;
                }

                if (!result.TryAdd(group.id, group))
                    errors.Add("Duplicate group id: " + group.id);
            }

            return result;
        }

        private static Dictionary<string, RecipeNode> ValidateNodes(
            ShaderGraphRecipe recipe,
            ShaderGraphReflectionBridge bridge,
            Dictionary<string, RecipeProperty> properties,
            Dictionary<string, RecipeGroup> groups,
            List<string> errors)
        {
            Dictionary<string, RecipeNode> result =
                new Dictionary<string, RecipeNode>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (RecipeNode node in recipe.nodes)
            {
                if (node == null)
                {
                    errors.Add("Recipe contains a null node entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.id))
                {
                    errors.Add("A node has an empty id.");
                    continue;
                }

                if (!result.TryAdd(node.id, node))
                {
                    errors.Add("Duplicate node id: " + node.id);
                    continue;
                }

                if (!NodeAliasRegistry.IsAllowed(node.type))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' uses unknown type '" + node.type + "'.");
                    continue;
                }

                if (!bridge.TryResolveRecipeNodeV3(
                        node,
                        out Type resolvedType,
                        out string reason))
                {
                    errors.Add("Node '" + node.id + "': " + reason);
                    continue;
                }

                if (NodeAliasRegistry.IsForbiddenNodeType(resolvedType))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' did not resolve to an official built-in node type.");
                }

                if (NodeAliasRegistry.IsPropertyAlias(node.type))
                {
                    if (string.IsNullOrWhiteSpace(node.property) ||
                        !properties.ContainsKey(node.property ?? string.Empty))
                    {
                        errors.Add(
                            "Property node '" + node.id +
                            "' references missing property id '" +
                            node.property + "'.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(node.group) &&
                    !groups.ContainsKey(node.group))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' references missing group '" + node.group + "'.");
                }

                if (!string.IsNullOrWhiteSpace(node.assetPath) &&
                    AssetDatabase.LoadMainAssetAtPath(node.assetPath) == null)
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' assetPath was not found: " + node.assetPath);
                }

                ValidateCustomFunction(node, errors);
                ValidatePorts(node, errors);
            }

            return result;
        }

        private static void ValidateCustomFunction(
            RecipeNode node,
            List<string> errors)
        {
            bool isCustomFunctionNode =
                NodeAliasRegistry.Normalize(node.type)
                    .Contains("customfunction");

            if (!isCustomFunctionNode)
            {
                if (HasExplicitCustomFunctionPayload(node.customFunction))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' has customFunction data but type is '" +
                        node.type + "'.");
                }

                return;
            }

            if (node.customFunction == null)
            {
                errors.Add(
                    "Custom Function node '" + node.id +
                    "' requires a customFunction object.");
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    node.customFunction.functionName))
            {
                errors.Add(
                    "Custom Function node '" + node.id +
                    "' requires functionName.");
            }

            string sourceType = NodeAliasRegistry.Normalize(
                node.customFunction.sourceType);

            if (sourceType == "file")
            {
                if (string.IsNullOrWhiteSpace(
                        node.customFunction.filePath) ||
                    AssetDatabase.LoadMainAssetAtPath(
                        node.customFunction.filePath) == null)
                {
                    errors.Add(
                        "Custom Function node '" + node.id +
                        "' filePath was not found: " +
                        node.customFunction.filePath);
                }
            }
            else if (string.IsNullOrWhiteSpace(
                         node.customFunction.body))
            {
                errors.Add(
                    "String-mode Custom Function node '" + node.id +
                    "' requires body.");
            }
        }

        private static bool HasExplicitCustomFunctionPayload(
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

        private static void ValidatePorts(
            RecipeNode node,
            List<string> errors)
        {
            HashSet<int> ids = new HashSet<int>();
            HashSet<string> names =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RecipePort port in node.ports)
            {
                if (port == null)
                {
                    errors.Add(
                        "Node '" + node.id + "' has a null port entry.");
                    continue;
                }

                if (!ids.Add(port.id))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' has duplicate dynamic port id " + port.id + ".");
                }

                if (string.IsNullOrWhiteSpace(port.name))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' has a dynamic port with an empty name.");
                }
                else if (!names.Add(port.name))
                {
                    errors.Add(
                        "Node '" + node.id +
                        "' has duplicate dynamic port name '" +
                        port.name + "'.");
                }

                string direction =
                    NodeAliasRegistry.Normalize(port.direction);

                if (direction != "input" && direction != "output")
                {
                    errors.Add(
                        "Node '" + node.id + "' port '" + port.name +
                        "' direction must be Input or Output.");
                }
            }
        }

        private static void ValidateEdges(
            ShaderGraphRecipe recipe,
            Dictionary<string, RecipeNode> nodesById,
            List<string> errors)
        {
            HashSet<string> occupiedInputs =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, List<string>> adjacency =
                nodesById.Keys.ToDictionary(
                    key => key,
                    _ => new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (RecipeEdge edge in recipe.edges)
            {
                if (edge == null)
                {
                    errors.Add("Recipe contains a null edge entry.");
                    continue;
                }

                if (!nodesById.ContainsKey(edge.fromNode ?? string.Empty))
                {
                    errors.Add(
                        "Edge source node does not exist: " + edge.fromNode);
                    continue;
                }

                if (!nodesById.ContainsKey(edge.toNode ?? string.Empty))
                {
                    errors.Add(
                        "Edge destination node does not exist: " + edge.toNode);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(edge.fromPort) ||
                    string.IsNullOrWhiteSpace(edge.toPort))
                {
                    errors.Add(
                        "An edge has an empty port name: " +
                        edge.fromNode + " -> " + edge.toNode);
                }

                string inputKey = edge.toNode + "::" + edge.toPort;
                if (!occupiedInputs.Add(inputKey))
                {
                    errors.Add(
                        "Multiple edges target the same input port: " +
                        inputKey);
                }

                adjacency[edge.fromNode].Add(edge.toNode);
            }

            if (HasCycle(adjacency))
            {
                errors.Add(
                    "Recipe contains a cycle. Shader Graph node flow " +
                    "must remain acyclic.");
            }
        }

        private static void ValidateOutputs(
            ShaderGraphRecipe recipe,
            Dictionary<string, RecipeNode> nodesById,
            List<string> errors)
        {
            HashSet<string> occupiedBlocks =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RecipeOutput output in recipe.outputs)
            {
                if (output == null)
                {
                    errors.Add("Recipe contains a null output entry.");
                    continue;
                }

                if (!nodesById.ContainsKey(
                        output.fromNode ?? string.Empty))
                {
                    errors.Add(
                        "Output source node does not exist: " +
                        output.fromNode);
                }

                if (string.IsNullOrWhiteSpace(output.fromPort))
                {
                    errors.Add(
                        "Output from node '" + output.fromNode +
                        "' has an empty source port.");
                }

                if (string.IsNullOrWhiteSpace(output.block))
                {
                    errors.Add(
                        "Output from node '" + output.fromNode +
                        "' has an empty Master Stack block name.");
                }
                else if (!occupiedBlocks.Add(output.block))
                {
                    errors.Add(
                        "Multiple outputs target the same Master Stack block: " +
                        output.block);
                }
            }

            if (recipe.outputs.Length == 0)
            {
                errors.Add(
                    "Recipe has no Master Stack output connections.");
            }
        }

        private static void ValidateGraphSettings(
            ShaderGraphRecipe recipe,
            List<string> errors)
        {
            foreach (RecipeGraphSetting setting in recipe.graphSettings)
            {
                if (setting == null)
                {
                    errors.Add("Recipe contains a null graph setting.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(setting.member))
                    errors.Add("A graph setting has an empty member name.");
            }
        }

        private static bool HasCycle(
            Dictionary<string, List<string>> adjacency)
        {
            HashSet<string> visiting =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string node in adjacency.Keys)
            {
                if (Visit(node, adjacency, visiting, visited))
                    return true;
            }

            return false;
        }

        private static bool Visit(
            string node,
            Dictionary<string, List<string>> adjacency,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(node))
                return false;

            if (!visiting.Add(node))
                return true;

            foreach (string next in adjacency[node])
            {
                if (Visit(next, adjacency, visiting, visited))
                    return true;
            }

            visiting.Remove(node);
            visited.Add(node);
            return false;
        }
    }
}
