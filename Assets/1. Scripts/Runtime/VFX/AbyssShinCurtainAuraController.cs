using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectAbyss.VFX
{
    /// <summary>
    /// Builds a camera-backed, curved procedural flame curtain behind a character.
    /// The effect is intentionally not a skinned-mesh coating: the opaque character
    /// naturally occludes the curtain and leaves the broad vertical gold silhouette
    /// seen in the Shin reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbyssShinCurtainAuraController : MonoBehaviour
    {
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material curtainMaterial;
        [SerializeField] private Material coreMaterial;
        [SerializeField] private bool autoBindToParent = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField, HideInInspector] private bool previewInEditMode = false;
        [SerializeField] private bool followCamera = true;

        [Header("Curtain Geometry")]
        [SerializeField, Range(1, 6)] private int curtainLayerCount = 3;
        [SerializeField, Min(0.1f)] private float widthMultiplier = 1.48f;
        [SerializeField, Min(0.1f)] private float heightMultiplier = 1.46f;
        [SerializeField, Min(0f)] private float rearDistance = 0.14f;
        [SerializeField, Range(-0.75f, 0.75f)] private float verticalOffset = 0.08f;
        [SerializeField, Min(0f)] private float curveDepth = 0.12f;
        [SerializeField, Min(0f)] private float layerDepthStep = 0.035f;
        [SerializeField, Min(0f)] private float layerScaleStep = 0.055f;
        [SerializeField, Range(4, 96)] private int meshColumns = 32;
        [SerializeField, Range(4, 96)] private int meshRows = 24;
        [SerializeField, Min(0.01f)] private float boundsPadding = 0.42f;

        [Header("Renderer Filtering")]
        [SerializeField] private string[] excludedNameContains =
            { "Weapon", "Sword", "Blade", "Gun", "Rifle", "Bow" };

        private const string GeneratedPrefix = "__AbyssShinCurtain_";
        private readonly List<LayerEntry> layers = new();
        private Mesh curtainMesh;
        private bool rebuildRequested = true;
        private Transform lastResolvedRoot;

        public Transform TargetRoot => targetRoot;

        private void OnEnable()
        {
            rebuildRequested = true;
            TryRebuildAndSync();
        }

        private void OnDisable()
        {
            ClearGenerated();
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }

        private void OnValidate()
        {
            if (previewInEditMode)
                previewInEditMode = false;

            curtainLayerCount = Mathf.Clamp(curtainLayerCount, 1, 6);
            widthMultiplier = Mathf.Max(0.1f, widthMultiplier);
            heightMultiplier = Mathf.Max(0.1f, heightMultiplier);
            rearDistance = Mathf.Max(0f, rearDistance);
            curveDepth = Mathf.Max(0f, curveDepth);
            layerDepthStep = Mathf.Max(0f, layerDepthStep);
            layerScaleStep = Mathf.Max(0f, layerScaleStep);
            meshColumns = Mathf.Clamp(meshColumns, 4, 96);
            meshRows = Mathf.Clamp(meshRows, 4, 96);
            boundsPadding = Mathf.Max(0.01f, boundsPadding);
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            TryRebuildAndSync();
        }

        [ContextMenu("Rebuild Shin Curtain Aura")]
        public void Rebuild()
        {
            rebuildRequested = true;
            if (!Application.isPlaying)
            {
                ClearGenerated();
                Debug.LogWarning(
                    "[AbyssShinCurtain] Scene/Prefab 저장 안전성을 위해 Curtain은 Play Mode에서만 생성됩니다.",
                    this);
                return;
            }

            TryRebuildAndSync();
        }

        private void TryRebuildAndSync()
        {
            if (!Application.isPlaying)
                return;

            Transform resolvedRoot = ResolveTargetRoot();
            if (resolvedRoot == null)
            {
                if (layers.Count > 0)
                    ClearGenerated();
                return;
            }

            if (rebuildRequested || lastResolvedRoot != resolvedRoot || layers.Count != curtainLayerCount + 1)
                BuildLayers(resolvedRoot);

            if (layers.Count == 0)
                return;

            if (!TryCalculateTargetBounds(resolvedRoot, out Bounds targetBounds))
                return;

            SyncLayers(targetBounds);
        }

        private Transform ResolveTargetRoot()
        {
            if (targetRoot != null)
                return targetRoot;
            return autoBindToParent ? transform.parent : null;
        }

        private void BuildLayers(Transform resolvedRoot)
        {
            ClearGenerated();
            rebuildRequested = false;
            lastResolvedRoot = resolvedRoot;

            if (curtainMaterial == null || coreMaterial == null)
            {
                Debug.LogWarning("[AbyssShinCurtain] Curtain/Core Material이 모두 필요합니다.", this);
                return;
            }

            curtainMesh = BuildCurtainMesh(meshColumns, meshRows, curveDepth, boundsPadding);

            // Broad, softly separated layers create the persistent painterly gold wall.
            for (int i = 0; i < curtainLayerCount; i++)
            {
                float phase = 1.37f + i * 4.91f;
                float intensity = Mathf.Lerp(1f, 0.72f, curtainLayerCount <= 1 ? 0f : i / (float)(curtainLayerCount - 1));
                CreateLayer($"Flame_{i:00}", curtainMaterial, phase, intensity, i, false);
            }

            // A narrower high-emission layer supplies the vertical hot streaks.
            CreateLayer("Core", coreMaterial, 8.73f, 1f, -1, true);
        }

        private void CreateLayer(
            string suffix,
            Material material,
            float phase,
            float intensity,
            int layerIndex,
            bool core)
        {
            GameObject layerObject = new(GeneratedPrefix + suffix);
            layerObject.layer = gameObject.layer;
            layerObject.transform.SetParent(transform, false);

            MeshFilter filter = layerObject.AddComponent<MeshFilter>();
            filter.sharedMesh = curtainMesh;

            MeshRenderer renderer = layerObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = core ? -1 : -2 - Mathf.Max(0, layerIndex);

            MaterialPropertyBlock block = new();
            block.SetFloat("_PhaseOffset", phase);
            block.SetFloat("_IntensityScale", intensity);
            block.SetFloat("_LayerIndex", Mathf.Max(0, layerIndex));
            renderer.SetPropertyBlock(block);

            layers.Add(new LayerEntry(layerObject.transform, renderer, block, layerIndex, core));
        }

        private bool TryCalculateTargetBounds(Transform resolvedRoot, out Bounds result)
        {
            Renderer[] renderers = resolvedRoot.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            bool initialized = false;
            result = default;

            foreach (Renderer renderer in renderers)
            {
                if (!IsEligibleRenderer(renderer))
                    continue;

                if (!initialized)
                {
                    result = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }

            if (!initialized)
            {
                Debug.LogWarning("[AbyssShinCurtain] 대상 캐릭터 Renderer bounds를 찾지 못했습니다.", this);
                return false;
            }

            return true;
        }

        private bool IsEligibleRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;
            string rendererName = renderer.name ?? string.Empty;
            if (rendererName.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                return false;

            return excludedNameContains == null || excludedNameContains.All(token =>
                string.IsNullOrWhiteSpace(token) ||
                rendererName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0);
        }

        private void SyncLayers(Bounds targetBounds)
        {
            Vector3 center = targetBounds.center;
            float characterWidth = Mathf.Max(targetBounds.size.x, targetBounds.size.z * 0.72f, 0.1f);
            float characterHeight = Mathf.Max(targetBounds.size.y, 0.1f);

            Vector3 toCamera = ResolveHorizontalDirectionToCamera(center);
            Quaternion rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            Vector3 upOffset = Vector3.up * (characterHeight * verticalOffset);
            Vector3 basePosition = center + upOffset - toCamera * rearDistance;

            foreach (LayerEntry layer in layers)
            {
                float layerScale = layer.Core
                    ? 0.82f
                    : 1f + Mathf.Max(0, layer.LayerIndex) * layerScaleStep;
                float layerHeightScale = layer.Core ? 0.94f : 1f + Mathf.Max(0, layer.LayerIndex) * layerScaleStep * 0.35f;
                float depth = layer.Core
                    ? -layerDepthStep * 0.25f
                    : Mathf.Max(0, layer.LayerIndex) * layerDepthStep;

                layer.Transform.SetPositionAndRotation(basePosition - toCamera * depth, rotation);
                layer.Transform.localScale = new Vector3(
                    characterWidth * widthMultiplier * layerScale,
                    characterHeight * heightMultiplier * layerHeightScale,
                    1f);
                layer.Renderer.SetPropertyBlock(layer.PropertyBlock);
            }
        }

        private Vector3 ResolveHorizontalDirectionToCamera(Vector3 center)
        {
            if (!followCamera)
            {
                Vector3 fallback = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
            }

            Camera camera = targetCamera;
            if (camera == null)
                camera = Camera.main;
#if UNITY_EDITOR
            if (camera == null && SceneView.lastActiveSceneView != null)
                camera = SceneView.lastActiveSceneView.camera;
#endif
            if (camera != null)
            {
                Vector3 direction = Vector3.ProjectOnPlane(camera.transform.position - center, Vector3.up);
                if (direction.sqrMagnitude > 0.0001f)
                    return direction.normalized;
            }

            Vector3 rootForward = lastResolvedRoot != null ? -lastResolvedRoot.forward : Vector3.forward;
            rootForward = Vector3.ProjectOnPlane(rootForward, Vector3.up);
            return rootForward.sqrMagnitude > 0.0001f ? rootForward.normalized : Vector3.forward;
        }

        private static Mesh BuildCurtainMesh(int columns, int rows, float depth, float padding)
        {
            int vertexColumns = columns + 1;
            int vertexRows = rows + 1;
            Vector3[] vertices = new Vector3[vertexColumns * vertexRows];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[columns * rows * 6];

            int vertexIndex = 0;
            for (int y = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                float localY = v - 0.5f;
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    float localX = u - 0.5f;
                    float normalizedX = localX * 2f;
                    float curve = -depth * (1f - normalizedX * normalizedX);
                    vertices[vertexIndex] = new Vector3(localX, localY, curve);
                    normals[vertexIndex] = Vector3.forward;
                    uv[vertexIndex] = new Vector2(u, v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int a = y * vertexColumns + x;
                    int b = a + 1;
                    int c = a + vertexColumns;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            Mesh mesh = new()
            {
                name = "AbyssShinCurtain_Runtime",
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(
                Vector3.zero,
                new Vector3(1f + padding * 2f, 1f + padding * 2f, Mathf.Max(0.5f, depth * 4f + padding * 2f)));
            mesh.UploadMeshData(false);
            return mesh;
        }

        private void ClearGenerated()
        {
            for (int i = layers.Count - 1; i >= 0; i--)
                DestroyGeneratedObject(layers[i].Transform != null ? layers[i].Transform.gameObject : null);
            layers.Clear();

            if (curtainMesh != null)
            {
                DestroyGeneratedObject(curtainMesh);
                curtainMesh = null;
            }

            lastResolvedRoot = null;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private readonly struct LayerEntry
        {
            public LayerEntry(
                Transform transform,
                MeshRenderer renderer,
                MaterialPropertyBlock propertyBlock,
                int layerIndex,
                bool core)
            {
                Transform = transform;
                Renderer = renderer;
                PropertyBlock = propertyBlock;
                LayerIndex = layerIndex;
                Core = core;
            }

            public Transform Transform { get; }
            public MeshRenderer Renderer { get; }
            public MaterialPropertyBlock PropertyBlock { get; }
            public int LayerIndex { get; }
            public bool Core { get; }
        }
    }
}
