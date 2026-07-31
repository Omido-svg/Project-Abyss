using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectAbyss.VFX
{
    /// <summary>
    /// Creates two back-face-only SkinnedMeshRenderer shells that share the source
    /// character mesh and bones. The original opaque character depth hides the shell
    /// interior, leaving a broad procedural silhouette behind the character instead
    /// of coating the visible front surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbyssSkinnedAuraController : MonoBehaviour
    {
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Material coreAuraMaterial;
        [SerializeField] private Material flameAuraMaterial;
        [SerializeField] private bool autoBindToParent = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField, HideInInspector] private bool previewInEditMode = false;
        [SerializeField, Min(0.02f)] private float boundsPadding = 0.42f;
        [SerializeField] private string[] excludedNameContains =
            { "Weapon", "Sword", "Blade", "Gun", "Rifle", "Bow" };

        private readonly List<CloneEntry> clones = new List<CloneEntry>();
        private bool rebuildRequested;
        private Transform lastResolvedRoot;

        public Transform TargetRoot => targetRoot;
        public Material CoreAuraMaterial => coreAuraMaterial;
        public Material FlameAuraMaterial => flameAuraMaterial;

        public void SetTarget(Transform root, bool rebuild = true)
        {
            targetRoot = root;
            if (rebuild)
                RequestRebuild();
        }

        public void Configure(
            Material coreMaterial,
            Material flameMaterial,
            bool bindToParent,
            bool includeInactive,
            bool allowEditModePreview,
            float localBoundsPadding,
            IEnumerable<string> exclusions)
        {
            coreAuraMaterial = coreMaterial;
            flameAuraMaterial = flameMaterial;
            autoBindToParent = bindToParent;
            includeInactiveRenderers = includeInactive;
            previewInEditMode = allowEditModePreview;
            boundsPadding = Mathf.Max(0.02f, localBoundsPadding);
            excludedNameContains = exclusions?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
            RequestRebuild();
        }

        private void OnEnable()
        {
            RequestRebuild();
        }

        private void OnDisable()
        {
            ClearClones();
        }

        private void OnDestroy()
        {
            ClearClones();
        }

        private void OnValidate()
        {
            if (previewInEditMode)
                previewInEditMode = false;

            boundsPadding = Mathf.Max(0.02f, boundsPadding);
            RequestRebuild();
        }

        private void OnTransformParentChanged()
        {
            if (autoBindToParent)
                RequestRebuild();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            Transform resolved = ResolveTargetRoot();
            if (resolved != lastResolvedRoot)
                rebuildRequested = true;

            if (rebuildRequested && isActiveAndEnabled)
                Rebuild();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            for (int i = clones.Count - 1; i >= 0; i--)
            {
                CloneEntry entry = clones[i];
                if (entry.Source == null || entry.Renderer == null || entry.GameObject == null)
                {
                    DestroyClone(entry.GameObject);
                    clones.RemoveAt(i);
                    continue;
                }

                SyncClone(entry);
            }
        }

        private void RequestRebuild()
        {
            rebuildRequested = true;
        }

        [ContextMenu("Rebuild Procedural Surface Aura")]
        public void Rebuild()
        {
            rebuildRequested = false;
            ClearClones();

            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[AbyssSkinnedAura] Scene/Prefab 저장 안전성을 위해 Aura Shell은 Play Mode에서만 생성됩니다.",
                    this);
                return;
            }

            Transform resolvedRoot = ResolveTargetRoot();
            lastResolvedRoot = resolvedRoot;
            if (resolvedRoot == null)
            {
                Debug.LogWarning(
                    "[AbyssSkinnedAura] Target Root가 없습니다. 생성 Prefab을 캐릭터 루트의 자식으로 배치하거나 SetTarget을 호출하세요.",
                    this);
                return;
            }

            if (coreAuraMaterial == null || flameAuraMaterial == null)
            {
                Debug.LogWarning("[AbyssSkinnedAura] Core/Flame Aura Material이 모두 필요합니다.", this);
                return;
            }

            SkinnedMeshRenderer[] sources = resolvedRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveRenderers)
                .Where(IsEligibleSource)
                .ToArray();

            foreach (SkinnedMeshRenderer source in sources)
            {
                CreateClone(source, flameAuraMaterial, "Flame", -2, 5.17f);
                CreateClone(source, coreAuraMaterial, "Core", -1, 0.73f);
            }

            if (clones.Count == 0)
                Debug.LogWarning("[AbyssSkinnedAura] 복제할 SkinnedMeshRenderer를 찾지 못했습니다.", this);
        }

        private Transform ResolveTargetRoot()
        {
            if (targetRoot != null)
                return targetRoot;
            return autoBindToParent ? transform.parent : null;
        }

        private bool IsEligibleSource(SkinnedMeshRenderer source)
        {
            if (source == null || source.sharedMesh == null)
                return false;

            string rendererName = source.name ?? string.Empty;
            if (rendererName.StartsWith("__AbyssAura_", StringComparison.Ordinal))
                return false;

            return excludedNameContains == null || excludedNameContains.All(token =>
                string.IsNullOrWhiteSpace(token) ||
                rendererName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0);
        }

        private void CreateClone(
            SkinnedMeshRenderer source,
            Material material,
            string layerName,
            int sortingOffset,
            float phaseOffset)
        {
            GameObject cloneObject = new GameObject($"__AbyssAura_{layerName}_{source.name}");
            cloneObject.layer = source.gameObject.layer;
            cloneObject.transform.SetParent(source.transform.parent, false);
            cloneObject.transform.localPosition = source.transform.localPosition;
            cloneObject.transform.localRotation = source.transform.localRotation;
            cloneObject.transform.localScale = source.transform.localScale;

            SkinnedMeshRenderer clone = cloneObject.AddComponent<SkinnedMeshRenderer>();
            clone.sharedMesh = source.sharedMesh;
            clone.bones = source.bones;
            clone.rootBone = source.rootBone;
            clone.quality = source.quality;
            clone.updateWhenOffscreen = true;
            clone.skinnedMotionVectors = false;
            clone.shadowCastingMode = ShadowCastingMode.Off;
            clone.receiveShadows = false;
            clone.lightProbeUsage = LightProbeUsage.Off;
            clone.reflectionProbeUsage = ReflectionProbeUsage.Off;
            clone.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            clone.allowOcclusionWhenDynamic = false;
            clone.sortingLayerID = source.sortingLayerID;
            clone.sortingOrder = source.sortingOrder + sortingOffset;

            int materialCount = Mathf.Max(1, source.sharedMaterials != null ? source.sharedMaterials.Length : 0);
            Material[] materials = new Material[materialCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            clone.sharedMaterials = materials;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetFloat("_PhaseOffset", phaseOffset);
            clone.SetPropertyBlock(block);

            CloneEntry entry = new CloneEntry(source, clone, cloneObject, block);
            clones.Add(entry);
            SyncClone(entry);
        }

        private void SyncClone(CloneEntry entry)
        {
            SkinnedMeshRenderer source = entry.Source;
            SkinnedMeshRenderer clone = entry.Renderer;

            clone.enabled = source.enabled && source.gameObject.activeInHierarchy;
            clone.sharedMesh = source.sharedMesh;
            clone.bones = source.bones;
            clone.rootBone = source.rootBone;

            Bounds expanded = source.localBounds;
            expanded.Expand(boundsPadding * 2f);
            clone.localBounds = expanded;

            Mesh mesh = source.sharedMesh;
            if (mesh == null)
                return;

            int blendShapeCount = mesh.blendShapeCount;
            for (int i = 0; i < blendShapeCount; i++)
                clone.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));

            clone.SetPropertyBlock(entry.PropertyBlock);
        }

        private void ClearClones()
        {
            for (int i = clones.Count - 1; i >= 0; i--)
                DestroyClone(clones[i].GameObject);
            clones.Clear();
            lastResolvedRoot = null;
        }

        private static void DestroyClone(GameObject clone)
        {
            if (clone == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(clone);
            else
                UnityEngine.Object.DestroyImmediate(clone);
        }

        private readonly struct CloneEntry
        {
            public CloneEntry(
                SkinnedMeshRenderer source,
                SkinnedMeshRenderer renderer,
                GameObject gameObject,
                MaterialPropertyBlock propertyBlock)
            {
                Source = source;
                Renderer = renderer;
                GameObject = gameObject;
                PropertyBlock = propertyBlock;
            }

            public SkinnedMeshRenderer Source { get; }
            public SkinnedMeshRenderer Renderer { get; }
            public GameObject GameObject { get; }
            public MaterialPropertyBlock PropertyBlock { get; }
        }
    }
}
