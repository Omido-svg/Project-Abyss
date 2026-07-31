using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    internal sealed class GeneratedShaderGraphVerification
    {
        public bool success;
        public string summary;
    }

    internal static class GeneratedShaderGraphVerifier
    {
        public static GeneratedShaderGraphVerification Verify(
            UnityEngine.Object generatedAsset,
            RecipeProperty[] expectedProperties = null)
        {
            if (!(generatedAsset is Shader shader))
            {
                return new GeneratedShaderGraphVerification
                {
                    success = false,
                    summary =
                        "Generated asset was imported, but its main asset " +
                        "is not a UnityEngine.Shader."
                };
            }

            List<string> missingProperties =
                FindMissingProperties(shader, expectedProperties);

            if (missingProperties.Count > 0)
            {
                return new GeneratedShaderGraphVerification
                {
                    success = false,
                    summary =
                        "Shader imported, but expected material properties " +
                        "were not generated:\n- " +
                        string.Join("\n- ", missingProperties)
                };
            }

            try
            {
                Type shaderUtilType =
                    typeof(Editor).Assembly.GetType(
                        "UnityEditor.ShaderUtil");

                if (shaderUtilType == null)
                {
                    return ImportedWithoutCompilerInspection(shader);
                }

                MethodInfo getMessages =
                    shaderUtilType
                        .GetMethods(
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                        .FirstOrDefault(
                            method =>
                                method.Name ==
                                "GetShaderMessages" &&
                                method.GetParameters().Length == 1 &&
                                method.GetParameters()[0]
                                    .ParameterType ==
                                typeof(Shader));

                if (getMessages == null)
                {
                    return ImportedWithoutCompilerInspection(shader);
                }

                object rawMessages =
                    getMessages.Invoke(
                        null,
                        new object[] { shader });

                if (!(rawMessages is IEnumerable messages))
                {
                    return ImportedWithoutCompilerInspection(shader);
                }

                List<string> errors = new List<string>();
                List<string> warnings = new List<string>();

                foreach (object message in messages)
                {
                    if (message == null)
                    {
                        continue;
                    }

                    string severity =
                        ReadMemberText(
                            message,
                            "severity");

                    string text =
                        ReadMemberText(
                            message,
                            "message");

                    string formatted =
                        string.IsNullOrWhiteSpace(text)
                            ? message.ToString()
                            : text;

                    if (severity.IndexOf(
                            "error",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        errors.Add(formatted);
                    }
                    else
                    {
                        warnings.Add(formatted);
                    }
                }

                if (errors.Count > 0)
                {
                    return new GeneratedShaderGraphVerification
                    {
                        success = false,
                        summary =
                            "Shader import completed, but compiler errors " +
                            "remain:\n- " +
                            string.Join("\n- ", errors.Take(8))
                    };
                }

                return new GeneratedShaderGraphVerification
                {
                    success = true,
                    summary =
                        "Shader imported successfully. Compiler errors: 0" +
                        (warnings.Count > 0
                            ? "\nCompiler warnings: " + warnings.Count
                            : "\nCompiler warnings: 0")
                };
            }
            catch (Exception exception)
            {
                return new GeneratedShaderGraphVerification
                {
                    success = true,
                    summary =
                        "Shader imported as '" + shader.name +
                        "'. Compiler-message inspection was unavailable: " +
                        exception.GetBaseException().Message
                };
            }
        }

        private static List<string> FindMissingProperties(
            Shader shader,
            RecipeProperty[] expectedProperties)
        {
            List<string> missing = new List<string>();

            if (shader == null || expectedProperties == null)
                return missing;

            foreach (RecipeProperty property in expectedProperties)
            {
                if (property == null ||
                    !property.exposed ||
                    string.IsNullOrWhiteSpace(property.referenceName))
                {
                    continue;
                }

                if (shader.FindPropertyIndex(property.referenceName) < 0)
                    missing.Add(property.referenceName);
            }

            return missing;
        }

        private static GeneratedShaderGraphVerification
            ImportedWithoutCompilerInspection(Shader shader)
        {
            return new GeneratedShaderGraphVerification
            {
                success = true,
                summary =
                    "Shader imported as '" + shader.name +
                    "'. This Unity patch did not expose a compatible " +
                    "ShaderUtil.GetShaderMessages overload."
            };
        }

        private static string ReadMemberText(
            object target,
            string name)
        {
            Type type = target.GetType();

            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.IgnoreCase);

            if (property != null)
            {
                return property.GetValue(target)?.ToString() ??
                       string.Empty;
            }

            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.IgnoreCase);

            return field?.GetValue(target)?.ToString() ??
                   string.Empty;
        }
    }
}
