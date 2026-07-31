using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    /// <summary>
    /// Catalog-driven alias registry for Unity Shader Graph built-in nodes.
    /// The registry stores human-readable recipe names, while resolution is
    /// performed against the official node types installed in the current
    /// Unity Editor. Full internal type names are also accepted.
    /// </summary>
    internal static class NodeAliasRegistry
    {
        private static readonly Dictionary<string, string[]> Aliases =
            CreateAliases();

        public static IEnumerable<string> AllowedAliases =>
            Aliases.Keys.OrderBy(value => value);

        public static bool IsAllowed(string alias)
        {
            // v3 does not use the convenience alias table as a security
            // allowlist. Any non-empty recipe text is sent to TryResolve(),
            // which can only return a concrete AbstractMaterialNode type from
            // an official Unity Shader Graph/render-pipeline Editor assembly.
            // This keeps every official installed node usable even when Unity
            // adds or renames a node after this package was authored.
            return !string.IsNullOrWhiteSpace(alias);
        }

        public static bool IsPropertyAlias(string alias)
        {
            string normalized = Normalize(alias);
            return normalized == "property" || normalized == "propertynode";
        }

        public static bool TryResolve(
            string alias,
            IEnumerable<Type> officialNodeTypes,
            out Type resolvedType,
            out string reason)
        {
            resolvedType = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(alias))
            {
                reason = "Node alias is empty.";
                return false;
            }

            Type[] types = officialNodeTypes
                .Where(type => type != null)
                .ToArray();

            // Recipe authors may use the exact internal simple/full name.
            resolvedType = types.FirstOrDefault(type =>
                string.Equals(type.Name, alias, StringComparison.Ordinal) ||
                string.Equals(type.FullName, alias, StringComparison.Ordinal));

            if (resolvedType != null)
                return true;

            string matchedAlias = Aliases.ContainsKey(alias)
                ? alias
                : Aliases.Keys.FirstOrDefault(key =>
                    Normalize(key) == Normalize(alias));

            if (!string.IsNullOrEmpty(matchedAlias) &&
                Aliases.TryGetValue(matchedAlias, out string[] candidateNames))
            {
                foreach (string candidateName in candidateNames)
                {
                    resolvedType = types.FirstOrDefault(type =>
                        string.Equals(
                            type.Name,
                            candidateName,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            type.FullName,
                            candidateName,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            type.FullName,
                            "UnityEditor.ShaderGraph." + candidateName,
                            StringComparison.Ordinal));

                    if (resolvedType != null)
                        return true;
                }
            }

            string normalizedAlias = Normalize(alias);

            resolvedType = types.FirstOrDefault(type =>
            {
                string simple = Normalize(type.Name);
                string withoutNode = simple.EndsWith("node")
                    ? simple.Substring(0, simple.Length - 4)
                    : simple;

                return simple == normalizedAlias ||
                       withoutNode == normalizedAlias ||
                       simple == normalizedAlias + "node";
            });

            if (resolvedType != null)
                return true;

            // Some internal classes use a historical name that differs from
            // the Create Node menu title. Compare TitleAttribute metadata.
            resolvedType = types.FirstOrDefault(type =>
                GetNormalizedTitleTokens(type).Any(token =>
                    token == normalizedAlias));

            if (resolvedType != null)
                return true;

            reason =
                "The installed Shader Graph package does not expose a " +
                "matching official node type for alias '" + alias + "'. " +
                "Export Installed Official Node Catalog to inspect the exact " +
                "node/type names available in this Unity version.";
            return false;
        }

        /// <summary>
        /// v3 permits official Custom Function and Sub Graph nodes because the
        /// user explicitly requested them. Project-specific arbitrary node
        /// implementations remain rejected by the bridge's official assembly
        /// scan rather than by a name blacklist.
        /// </summary>
        public static bool IsForbiddenNodeType(Type type)
        {
            return type == null;
        }

        public static string GetAllowlistText()
        {
            return string.Join(", ", AllowedAliases);
        }

        private static Dictionary<string, string[]> CreateAliases()
        {
            Dictionary<string, string[]> result =
                new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase);

            void Add(string alias, params string[] candidates)
            {
                result[alias] = candidates;
            }

            // Special graph-input node.
            Add("Property", "PropertyNode");

            // Artistic / Adjustment.
            Add("ChannelMixer", "ChannelMixerNode");
            Add("Contrast", "ContrastNode");
            Add("Hue", "HueNode");
            Add("InvertColors", "InvertColorsNode");
            Add("ReplaceColor", "ReplaceColorNode");
            Add("Saturation", "SaturationNode");
            Add("WhiteBalance", "WhiteBalanceNode");
            Add("Blend", "BlendNode");
            Add("Dither", "DitherNode");
            Add("ChannelMask", "ChannelMaskNode");
            Add("ColorMask", "ColorMaskNode");
            Add("NormalBlend", "NormalBlendNode");
            Add("NormalFromHeight", "NormalFromHeightNode");
            Add("NormalFromTexture", "NormalFromTextureNode");
            Add("NormalReconstructZ", "NormalReconstructZNode");
            Add("NormalStrength", "NormalStrengthNode");
            Add("NormalUnpack", "NormalUnpackNode");
            Add(
                "ColorspaceConversion",
                "ColorspaceConversionNode",
                "ColorSpaceConversionNode");

            // Channel / basic input.
            Add("Split", "SplitNode");
            Add("Combine", "CombineNode");
            Add("Flip", "FlipNode");
            Add("Swizzle", "SwizzleNode");
            Add("Boolean", "BooleanNode");
            Add("Color", "ColorNode");
            Add("Constant", "Vector1Node");
            Add("Integer", "IntegerNode", "Vector1Node");
            Add("Slider", "Vector1Node");
            Add("Vector1", "Vector1Node");
            Add("Vector2", "Vector2Node");
            Add("Vector3", "Vector3Node");
            Add("Vector4", "Vector4Node");
            Add("Matrix2", "Matrix2Node");
            Add("Matrix3", "Matrix3Node");
            Add("Matrix4", "Matrix4Node");
            Add("Gradient", "GradientNode");
            Add("Time", "TimeNode");

            // Geometry / scene input.
            Add("BitangentVector", "BitangentVectorNode");
            Add("NormalVector", "NormalVectorNode");
            Add("Position", "PositionNode");
            Add("ScreenPosition", "ScreenPositionNode");
            Add("TangentVector", "TangentVectorNode");
            Add("UV", "UVNode");
            Add("VertexColor", "VertexColorNode");
            Add("ViewDirection", "ViewDirectionNode");
            Add("Ambient", "AmbientNode");
            Add("BakedGI", "BakedGINode");
            Add("ReflectionProbe", "ReflectionProbeNode");
            Add("TransformationMatrix", "TransformationMatrixNode");
            Add("DielectricSpecular", "DielectricSpecularNode");
            Add(
                "MetalReflectance",
                "MetalReflectanceNode",
                "MetallicReflectanceNode");
            Add("Camera", "CameraNode");
            Add("Object", "ObjectNode");
            Add("SceneColor", "SceneColorNode");
            Add("SceneDepth", "SceneDepthNode");
            Add("Screen", "ScreenNode");
            Add("IsFrontFace", "IsFrontFaceNode");

            // Texture input.
            Add("SampleTexture2D", "SampleTexture2DNode");
            Add("CubemapAsset", "CubemapAssetNode");
            Add("Texture2DAsset", "Texture2DAssetNode");
            Add("SamplerState", "SamplerStateNode");
            Add("SampleTexture2DLOD", "SampleTexture2DLODNode");
            Add("Texture2DArrayAsset", "Texture2DArrayAssetNode");
            Add("SampleTexture2DArray", "SampleTexture2DArrayNode");
            Add("Texture3DAsset", "Texture3DAssetNode");
            Add("SampleTexture3D", "SampleTexture3DNode");
            Add("TexelSize", "TexelSizeNode");
            Add("Triplanar", "TriplanarNode");

            // Math / basic.
            Add("Absolute", "AbsoluteNode");
            Add("Exponential", "ExponentialNode");
            Add("Length", "LengthNode");
            Add("Log", "LogNode");
            Add("Modulo", "ModuloNode");
            Add("Negate", "NegateNode");
            Add("Normalize", "NormalizeNode");
            Add("Posterize", "PosterizeNode");
            Add("Reciprocal", "ReciprocalNode");
            Add("ReciprocalSquareRoot", "ReciprocalSquareRootNode");
            Add("Add", "AddNode");
            Add("Divide", "DivideNode");
            Add("Multiply", "MultiplyNode");
            Add("Power", "PowerNode");
            Add("SquareRoot", "SquareRootNode");
            Add("Subtract", "SubtractNode");
            Add("DDX", "DDXNode");
            Add("DDXY", "DDXYNode");
            Add("DDY", "DDYNode");
            Add("Lerp", "LerpNode");
            Add("InverseLerp", "InverseLerpNode");
            Add("Smoothstep", "SmoothstepNode");
            Add("MatrixConstruction", "MatrixConstructionNode");
            Add("MatrixDeterminant", "MatrixDeterminantNode");
            Add("MatrixTranspose", "MatrixTransposeNode");
            Add("Clamp", "ClampNode");
            Add("Fraction", "FractionNode");
            Add("Maximum", "MaximumNode");
            Add("Minimum", "MinimumNode");
            Add("OneMinus", "OneMinusNode");
            Add("RandomRange", "RandomRangeNode");
            Add("Remap", "RemapNode");
            Add("Saturate", "SaturateNode");
            Add("Ceiling", "CeilingNode");
            Add("Floor", "FloorNode");
            Add("Round", "RoundNode");
            Add("Step", "StepNode");
            Add("Sign", "SignNode");
            Add("Truncate", "TruncateNode");

            // Trigonometry.
            Add("Sine", "SineNode");
            Add("Cosine", "CosineNode");
            Add("Tangent", "TangentNode");
            Add("Arcsine", "ArcsineNode");
            Add("Arccosine", "ArccosineNode");
            Add("Arctangent", "ArctangentNode");
            Add("Arctangent2", "Arctangent2Node");
            Add("HyperbolicSine", "HyperbolicSineNode");
            Add("HyperbolicCosine", "HyperbolicCosineNode");
            Add("HyperbolicTangent", "HyperbolicTangentNode");
            Add("RadiansToDegrees", "RadiansToDegreesNode");
            Add("DegreesToRadians", "DegreesToRadiansNode");

            // Vector math.
            Add("CrossProduct", "CrossProductNode");
            Add("Distance", "DistanceNode");
            Add("DotProduct", "DotProductNode");
            Add("Projection", "ProjectionNode");
            Add("Reflection", "ReflectionNode");
            Add("Rejection", "RejectionNode");
            Add("RotateAboutAxis", "RotateAboutAxisNode");
            Add("SphereMask", "SphereMaskNode");
            Add("Transform", "TransformNode");
            Add("FresnelEffect", "FresnelNode", "FresnelEffectNode");

            // Procedural / noise / shapes.
            Add("NoiseSineWave", "NoiseSineWaveNode");
            Add("SawtoothWave", "SawtoothWaveNode");
            Add("SquareWave", "SquareWaveNode");
            Add("TriangleWave", "TriangleWaveNode");
            Add("Checkerboard", "CheckerboardNode");
            Add("GradientNoise", "GradientNoiseNode");
            Add("SimpleNoise", "NoiseNode", "SimpleNoiseNode");
            Add("Voronoi", "VoronoiNode");
            Add("Ellipse", "EllipseNode");
            Add("Polygon", "PolygonNode");
            Add("Rectangle", "RectangleNode");
            Add("RoundedRectangle", "RoundedRectangleNode");

            // Logic / utility.
            Add("Preview", "PreviewNode");
            Add("All", "AllNode");
            Add("And", "AndNode");
            Add("Any", "AnyNode");
            Add("Branch", "BranchNode");
            Add("Comparison", "ComparisonNode");
            Add("IsInfinite", "IsInfiniteNode");
            Add("IsNaN", "IsNaNNode");
            Add("Nand", "NandNode");
            Add("Not", "NotNode");
            Add("Or", "OrNode");

            // UV.
            Add("Flipbook", "FlipbookNode");
            Add("PolarCoordinates", "PolarCoordinatesNode");
            Add("RadialShear", "RadialShearNode");
            Add("Rotate", "RotateNode");
            Add("Spherize", "SpherizeNode");
            Add("TilingAndOffset", "TilingAndOffsetNode");
            Add("Twirl", "TwirlNode");

            // Advanced official nodes.
            Add("CustomFunction", "CustomFunctionNode");
            Add("SubGraph", "SubGraphNode");

            return result;
        }

        private static IEnumerable<string> GetNormalizedTitleTokens(Type type)
        {
            foreach (CustomAttributeData attribute in type.CustomAttributes)
            {
                string name = attribute.AttributeType.Name;
                if (name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                foreach (CustomAttributeTypedArgument argument
                         in attribute.ConstructorArguments)
                {
                    if (argument.Value is string text)
                    {
                        string token = Normalize(text);
                        if (!string.IsNullOrEmpty(token))
                            yield return token;

                        continue;
                    }

                    if (!(argument.Value is
                          IEnumerable<CustomAttributeTypedArgument> items))
                    {
                        continue;
                    }

                    string[] segments = items
                        .Select(item => item.Value as string ??
                                        Convert.ToString(item.Value))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();

                    foreach (string segment in segments)
                    {
                        string token = Normalize(segment);
                        if (!string.IsNullOrEmpty(token))
                            yield return token;
                    }

                    for (int index = 0; index < segments.Length; index++)
                    {
                        string suffix = Normalize(
                            string.Join("", segments.Skip(index)));

                        if (!string.IsNullOrEmpty(suffix))
                            yield return suffix;
                    }
                }
            }
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }
    }
}
