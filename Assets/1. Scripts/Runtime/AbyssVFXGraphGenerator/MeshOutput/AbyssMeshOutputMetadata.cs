using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.VFX.MeshOutput
{
    public sealed class AbyssMeshOutputMetadata : MonoBehaviour
    {
        public string recipeName;
        public string sourceJsonPath;
        public string backend = "MeshOutputSupportBundle";
        public List<string> meshAssetPaths = new List<string>();
        public string materialAssetPath;
        public string notes;
    }
}
