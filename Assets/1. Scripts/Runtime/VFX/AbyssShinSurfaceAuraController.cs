using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectAbyss.VFX
{
    /// <summary>
    /// Renders a procedural, texture-free Shin aura over the animated character surface.
    /// The generated renderer copies share the source mesh, bones and root bone, so they
    /// follow the same skeletal animation without baking a new mesh every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbyssShinSurfaceAuraController : MonoBehaviour
    {
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Material innerAuraMaterial;
        [SerializeField] private Material outerAuraMaterial;
        [SerializeField] private bool autoBindToParent = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private string[] excludedNameContains = { "Weapon", "Sword", "Blade", "Gun", "Rifle", "Bow" };

        private readonly List<CloneEntry> clones = new();

        public Transform TargetRoot => targetRoot;
        public Material InnerAuraMaterial => innerAuraMaterial;
        public Material OuterAuraMaterial => outerAuraMaterial;

        public void Configure(
            Material innerMaterial,
            Material outerMaterial,
            bool bindToParent,
            bool includeInactive,
            IEnumerable<string> exclusions)
        {
            innerAuraMaterial = innerMaterial;
            outerAuraMaterial = outerMaterial;
            autoBindToParent = bindToParent;
            includeInactiveRenderers = includeInactive;
            excludedNameContains = exclusions?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
        }

        public void SetTarget(Transform root, bool rebuild = true)
        {
            targetRoot = root;
            if (rebuild && isActiveAndEnabled)
                Rebuild();
        }

        private void OnEnable() => Rebuild();
        private void OnDisable() => ClearClones();
        private void OnDestroy() => ClearClones();

        [ContextMenu("Rebuild Shin Surface Aura")]
        public void Rebuild()
        {
            ClearClones();

            Transform resolvedRoot = targetRoot;
            if (resolvedRoot == null && autoBindToParent)
                resolvedRoot = transform.parent;

            if (resolvedRoot == null)
            {
                Debug.LogWarning("[AbyssShinSurfaceAura] Target Root가 없습니다. 생성된 Prefab을 캐릭터 루트의 자식으로 배치하거나 SetTarget을 호출하세요.", this);
                return;
            }

            if (innerAuraMaterial == null || outerAuraMaterial == null)
            {
                Debug.LogWarning("[AbyssShinSurfaceAura] Inner/Outer Aura Material이 모두 필요합니다.", this);
                return;
            }

            SkinnedMeshRenderer[] sources = resolvedRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveRenderers)
                .Where(IsEligibleSource)
                .ToArray();

            foreach (SkinnedMeshRenderer source in sources)
            {
                CreateClone(source, innerAuraMaterial, "Inner");
                CreateClone(source, outerAuraMaterial, "Outer");
            }

            if (clones.Count == 0)
                Debug.LogWarning("[AbyssShinSurfaceAura] 복제할 SkinnedMeshRenderer를 찾지 못했습니다.", this);
        }

        private bool IsEligibleSource(SkinnedMeshRenderer source)
        {
            if (source == null || source.sharedMesh == null)
                return false;
            string rendererName = source.name ?? string.Empty;
            if (rendererName.StartsWith("__AbyssShinAura_", StringComparison.Ordinal))
                return false;
            return excludedNameContains == null || excludedNameContains.All(token =>
                string.IsNullOrWhiteSpace(token) ||
                rendererName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0);
        }

        private void CreateClone(SkinnedMeshRenderer source, Material material, string layerName)
        {
            GameObject cloneObject = new($"__AbyssShinAura_{layerName}_{source.name}");
            cloneObject.layer = source.gameObject.layer;
            cloneObject.transform.SetParent(source.transform.parent, false);
            cloneObject.transform.localPosition = source.transform.localPosition;
            cloneObject.transform.localRotation = source.transform.localRotation;
            cloneObject.transform.localScale = source.transform.localScale;
            SkinnedMeshRenderer clone = cloneObject.AddComponent<SkinnedMeshRenderer>();
            clone.sharedMesh = source.sharedMesh;
            clone.bones = source.bones;
            clone.rootBone = source.rootBone;
            clone.localBounds = source.localBounds;
            clone.quality = source.quality;
            clone.updateWhenOffscreen = source.updateWhenOffscreen;
            clone.skinnedMotionVectors = false;
            clone.shadowCastingMode = ShadowCastingMode.Off;
            clone.receiveShadows = false;
            clone.lightProbeUsage = LightProbeUsage.Off;
            clone.reflectionProbeUsage = ReflectionProbeUsage.Off;
            clone.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            clone.allowOcclusionWhenDynamic = false;

            int materialCount = Mathf.Max(1, source.sharedMaterials?.Length ?? 0);
            Material[] materials = new Material[materialCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            clone.sharedMaterials = materials;

            clones.Add(new CloneEntry(source, clone, cloneObject));
            SyncClone(clones[^1]);
        }

        private void LateUpdate()
        {
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

        private static void SyncClone(CloneEntry entry)
        {
            SkinnedMeshRenderer source = entry.Source;
            SkinnedMeshRenderer clone = entry.Renderer;

            clone.enabled = source.enabled && source.gameObject.activeInHierarchy;
            clone.localBounds = source.localBounds;

            Mesh mesh = source.sharedMesh;
            if (mesh == null)
                return;

            int blendShapeCount = mesh.blendShapeCount;
            for (int i = 0; i < blendShapeCount; i++)
                clone.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
        }

        private void ClearClones()
        {
            for (int i = clones.Count - 1; i >= 0; i--)
                DestroyClone(clones[i].GameObject);
            clones.Clear();
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
            public CloneEntry(SkinnedMeshRenderer source, SkinnedMeshRenderer renderer, GameObject gameObject)
            {
                Source = source;
                Renderer = renderer;
                GameObject = gameObject;
            }

            public SkinnedMeshRenderer Source { get; }
            public SkinnedMeshRenderer Renderer { get; }
            public GameObject GameObject { get; }
        }
    }

}
