#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectAbyss.Editor.VFXAI
{
    internal sealed class AbyssVFXEnvironmentStatus
    {
        public string unityVersion;
        public string vfxVersion;
        public string urpVersion;
        public bool isExpectedUnity;
        public bool isExpectedVFX;
        public bool isExpectedURP;
        public bool isURPActive;
        public bool isLinearColorSpace;
        public bool supportsCompute;

        public bool CoreCompatible => isExpectedVFX && isExpectedURP && isURPActive;
    }

    internal static class AbyssVFXVersionGuard
    {
        internal const string ExpectedUnity = "6000.0.56f1";
        internal const string ExpectedVFX = "17.0.4";
        internal const string ExpectedURP = "17.0.4";

        internal static AbyssVFXEnvironmentStatus GetStatus()
        {
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            string vfx = packages.FirstOrDefault(x => x.name == "com.unity.visualeffectgraph")?.version ?? "Not Found";
            string urp = packages.FirstOrDefault(x => x.name == "com.unity.render-pipelines.universal")?.version ?? "Not Found";
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;

            return new AbyssVFXEnvironmentStatus
            {
                unityVersion = Application.unityVersion,
                vfxVersion = vfx,
                urpVersion = urp,
                isExpectedUnity = string.Equals(Application.unityVersion, ExpectedUnity, StringComparison.Ordinal),
                isExpectedVFX = string.Equals(vfx, ExpectedVFX, StringComparison.Ordinal),
                isExpectedURP = string.Equals(urp, ExpectedURP, StringComparison.Ordinal),
                isURPActive = pipeline != null && pipeline.GetType().FullName?.Contains("UniversalRenderPipelineAsset", StringComparison.Ordinal) == true,
                isLinearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear,
                supportsCompute = SystemInfo.supportsComputeShaders
            };
        }
    }
}
#endif
