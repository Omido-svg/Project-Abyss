using System;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    [Serializable]
    internal sealed class ShaderGraphRecipe
    {
        public int schemaVersion = 3;
        public string graphName = "Generated_Node_Graph";

        public RecipeGraphSetting[] graphSettings =
            new RecipeGraphSetting[0];

        public RecipeProperty[] properties =
            new RecipeProperty[0];

        public RecipeGroup[] groups =
            new RecipeGroup[0];

        public RecipeStickyNote[] stickyNotes =
            new RecipeStickyNote[0];

        public RecipeNode[] nodes = new RecipeNode[0];
        public RecipeEdge[] edges = new RecipeEdge[0];
        public RecipeOutput[] outputs = new RecipeOutput[0];
    }

    /// <summary>
    /// Applies a serialized setting to GraphData, a Target, or a SubTarget.
    /// target accepts a simple/full type name such as UniversalTarget.
    /// Leave target empty to apply the first writable matching member.
    /// </summary>
    [Serializable]
    internal sealed class RecipeGraphSetting
    {
        public string target;
        public string member;
        public string value;
        public bool optional;
    }

    /// <summary>
    /// Blackboard material property definition.
    /// Supported type examples:
    /// Float, Slider, Integer, Boolean, Color, Vector2, Vector3, Vector4,
    /// Matrix2, Matrix3, Matrix4, Texture2D, Texture2DArray, Texture3D,
    /// Cubemap, SamplerState, Gradient.
    /// </summary>
    [Serializable]
    internal sealed class RecipeProperty
    {
        public string id;
        public string type;
        public string displayName;
        public string referenceName;
        public string defaultValue;
        public string assetPath;

        public bool exposed = true;
        public bool hidden;
        public bool hdr;
        public bool mainTexture;
        public bool mainColor;

        public float rangeMin;
        public float rangeMax = 1f;

        public string precision = "Inherit";
        public string hlslDeclaration = "PerMaterial";

        public RecipeGradientColorKey[] colorKeys =
            new RecipeGradientColorKey[0];

        public RecipeGradientAlphaKey[] alphaKeys =
            new RecipeGradientAlphaKey[0];
    }

    [Serializable]
    internal sealed class RecipeGradientColorKey
    {
        public string color = "1,1,1,1";
        public float time;
    }

    [Serializable]
    internal sealed class RecipeGradientAlphaKey
    {
        public float alpha = 1f;
        public float time;
    }

    [Serializable]
    internal sealed class RecipeGroup
    {
        public string id;
        public string title;
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class RecipeStickyNote
    {
        public string id;
        public string title;
        [TextArea(2, 12)] public string content;
        public float x;
        public float y;
        public float width = 300f;
        public float height = 180f;
        public string theme = "Classic";
        public string textSize = "Medium";
    }

    [Serializable]
    internal sealed class RecipeNode
    {
        public string id;
        public string type;
        public string title;
        public float x;
        public float y;
        public float width = 240f;
        public float height = 180f;

        public string group;

        /// <summary>
        /// For type=Property, references RecipeProperty.id.
        /// </summary>
        public string property;

        /// <summary>
        /// Generic asset binding for Sub Graph, texture asset, cubemap,
        /// Custom Function file mode, and other official asset-backed nodes.
        /// </summary>
        public string assetPath;
        public string assetMember;

        public RecipeCustomFunction customFunction;

        public RecipePort[] ports = new RecipePort[0];
        public RecipeMember[] members = new RecipeMember[0];
        public RecipeInputDefault[] inputDefaults =
            new RecipeInputDefault[0];
    }

    [Serializable]
    internal sealed class RecipeCustomFunction
    {
        public string sourceType = "String";
        public string functionName;
        [TextArea(3, 30)] public string body;
        public string filePath;
        public string precision = "Inherit";
    }

    /// <summary>
    /// Dynamic port used primarily by Custom Function nodes.
    /// valueType examples: DynamicVector, Vector1, Vector2, Vector3, Vector4,
    /// Boolean, Matrix2, Matrix3, Matrix4, Texture2D, Texture2DArray,
    /// Texture3D, Cubemap, SamplerState, Gradient.
    /// </summary>
    [Serializable]
    internal sealed class RecipePort
    {
        public int id;
        public string name;
        public string shaderName;
        public string direction = "Input";
        public string valueType = "DynamicVector";
        public string defaultValue;
        public string stage = "All";
        public bool hidden;
    }

    [Serializable]
    internal sealed class RecipeMember
    {
        public string name;
        public string value;
    }

    [Serializable]
    internal sealed class RecipeInputDefault
    {
        public string port;
        public string value;
    }

    [Serializable]
    internal sealed class RecipeEdge
    {
        public string fromNode;
        public string fromPort;
        public string toNode;
        public string toPort;
    }

    [Serializable]
    internal sealed class RecipeOutput
    {
        public string fromNode;
        public string fromPort;
        public string block;
        public bool optional;
    }

    internal static class ShaderGraphRecipeParser
    {
        public static bool TryParse(
            string json,
            out ShaderGraphRecipe recipe,
            out string error)
        {
            recipe = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Recipe JSON is empty.";
                return false;
            }

            try
            {
                recipe = JsonUtility.FromJson<ShaderGraphRecipe>(json);
            }
            catch (Exception exception)
            {
                error = "Recipe JSON parsing failed:\n" + exception.Message;
                return false;
            }

            if (recipe == null)
            {
                error = "Recipe JSON produced no object.";
                return false;
            }

            recipe.graphSettings = recipe.graphSettings ??
                                   new RecipeGraphSetting[0];
            recipe.properties = recipe.properties ??
                                new RecipeProperty[0];
            recipe.groups = recipe.groups ?? new RecipeGroup[0];
            recipe.stickyNotes = recipe.stickyNotes ??
                                 new RecipeStickyNote[0];
            recipe.nodes = recipe.nodes ?? new RecipeNode[0];
            recipe.edges = recipe.edges ?? new RecipeEdge[0];
            recipe.outputs = recipe.outputs ?? new RecipeOutput[0];

            foreach (RecipeProperty property in recipe.properties)
            {
                if (property == null)
                    continue;

                property.colorKeys = property.colorKeys ??
                                     new RecipeGradientColorKey[0];
                property.alphaKeys = property.alphaKeys ??
                                     new RecipeGradientAlphaKey[0];
            }

            foreach (RecipeNode node in recipe.nodes)
            {
                if (node == null)
                    continue;

                node.members = node.members ?? new RecipeMember[0];
                node.inputDefaults = node.inputDefaults ??
                                     new RecipeInputDefault[0];
                node.ports = node.ports ?? new RecipePort[0];

                // Unity JsonUtility can instantiate an empty nested serializable
                // object even when the JSON did not contain that field. In this
                // recipe schema that makes every ordinary node appear to carry
                // Custom Function configuration because RecipeCustomFunction's
                // default sourceType/precision values are populated.
                //
                // Preserve real payloads so malformed JSON is still reported,
                // but normalize the synthetic empty object back to null.
                if (!IsCustomFunctionNode(node.type) &&
                    !HasExplicitCustomFunctionPayload(node.customFunction))
                {
                    node.customFunction = null;
                }
            }

            // Backward compatibility: schema v1 recipes had no v3 arrays.
            if (recipe.schemaVersion == 1)
            {
                recipe.graphSettings = new RecipeGraphSetting[0];
                recipe.properties = new RecipeProperty[0];
                recipe.groups = new RecipeGroup[0];
                recipe.stickyNotes = new RecipeStickyNote[0];
            }

            return true;
        }

        private static bool IsCustomFunctionNode(string type)
        {
            return NodeAliasRegistry.Normalize(type)
                .Contains("customfunction");
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

            // Ignore constructor/default values produced by JsonUtility.
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
    }
}
