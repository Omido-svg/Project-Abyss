using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.NodeOnlyShaderGraph
{
    [Serializable]
    internal sealed class InstalledNodeCatalog
    {
        public string unityVersion;
        public string generatedUtc;
        public CatalogNode[] nodes;
    }

    [Serializable]
    internal sealed class CatalogNode
    {
        public string alias;
        public string fullTypeName;
        public string assemblyName;
        public string displayName;
        public bool allowedByBuilder;
        public string error;
        public CatalogPort[] ports;
    }

    [Serializable]
    internal sealed class CatalogPort
    {
        public string direction;
        public string displayName;
        public string shaderOutputName;
        public int id;
    }

    internal static class InstalledNodeCatalogExporter
    {
        public static string Export(
            ShaderGraphReflectionBridge bridge)
        {
            string folder =
                "Assets/NodeOnlyShaderGraphBuilder/GeneratedCatalog";

            Directory.CreateDirectory(folder);

            string assetPath =
                folder + "/InstalledShaderGraphNodes.json";

            Dictionary<Type, string> aliasByType =
                new Dictionary<Type, string>();

            foreach (string alias in bridge.GetAllowedAliases())
            {
                if (bridge.TryResolveAllowedNode(
                        alias,
                        out Type type,
                        out _))
                {
                    aliasByType[type] = alias;
                }
            }

            List<CatalogNode> entries =
                new List<CatalogNode>();

            foreach (Type type in bridge
                         .GetOfficialNodeTypes()
                         .OrderBy(candidate =>
                             candidate.FullName))
            {
                CatalogNode entry =
                    new CatalogNode
                    {
                        alias =
                            aliasByType.TryGetValue(
                                type,
                                out string alias)
                                ? alias
                                : string.Empty,
                        fullTypeName =
                            type.FullName ?? type.Name,
                        assemblyName =
                            type.Assembly.GetName().Name,
                        // Every type in this loop already passed the bridge's
                        // official AbstractMaterialNode assembly filter. It is
                        // therefore usable through its exact simple/full type
                        // name even when no convenience alias exists yet.
                        allowedByBuilder = true,
                        ports = new CatalogPort[0],
                        error = string.Empty
                    };

                object node = null;

                try
                {
                    node =
                        Activator.CreateInstance(type, true);

                    entry.displayName =
                        bridge.GetNodeDisplayName(node);

                    entry.ports =
                        bridge.GetSlots(node)
                            .Select(slot =>
                                new CatalogPort
                                {
                                    direction =
                                        slot.isInput
                                            ? "Input"
                                            : slot.isOutput
                                                ? "Output"
                                                : "Unknown",
                                    displayName =
                                        slot.displayName,
                                    shaderOutputName =
                                        slot.shaderOutputName,
                                    id = slot.id
                                })
                            .ToArray();
                }
                catch (Exception exception)
                {
                    entry.error =
                        exception.GetBaseException().Message;
                }

                entries.Add(entry);
            }

            InstalledNodeCatalog catalog =
                new InstalledNodeCatalog
                {
                    unityVersion =
                        Application.unityVersion,
                    generatedUtc =
                        DateTime.UtcNow.ToString("O"),
                    nodes = entries.ToArray()
                };

            File.WriteAllText(
                assetPath,
                JsonUtility.ToJson(catalog, true));

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate);

            return assetPath;
        }
    }
}
