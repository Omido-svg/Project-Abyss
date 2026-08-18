using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class OutlineController : MonoBehaviour
{
    private static bool globalSuppressed;

    public static bool IsGloballySuppressed => globalSuppressed;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        globalSuppressed = false;
    }

    public static void SetGlobalSuppressed(bool value)
    {
        if (globalSuppressed == value)
            return;

        globalSuppressed = value;

        OutlineController[] controllers =
            FindObjectsByType<OutlineController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (OutlineController controller
                 in controllers)
        {
            controller?.RefreshOutlineState();
        }
    }

    [Header("Outline Material")]
    [FormerlySerializedAs("outlineMaterial")]
    [SerializeField] private Material outlineMaterialTemplate;

    [Header("Per Character Settings")]
    [SerializeField, ColorUsage(false, true)]
    private Color outlineColor =
        new(0.20f, 0.85f, 1f, 1f);

    [SerializeField, Range(0.001f, 0.12f)]
    private float outlineWidth = 0.025f;

    [Header("Renderer Cache")]
    [SerializeField] private Renderer[] targetRenderers;

    private readonly Dictionary<Renderer, Material[]> originalMaterials =
        new();

    private Material runtimeOutlineMaterial;
    private bool isHovered;
    private bool isSelected;
    private bool outlineApplied;

    public bool IsHovered => isHovered;
    public bool IsSelected => isSelected;
    public bool IsOutlined => outlineApplied;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;

    public void Configure(
        Material material,
        Color color)
    {
        bool shouldRestore =
            outlineApplied;

        if (shouldRestore)
            RestoreOriginalMaterials();

        outlineMaterialTemplate = material;
        outlineColor = color;

        RefreshRendererCache();

        if (Application.isPlaying)
        {
            RebuildRuntimeMaterial();
            RefreshOutlineState();
        }
    }

    public void Configure(
        Material material)
    {
        Configure(
            material,
            outlineColor);
    }

    public void SetOutlineColor(
        Color color)
    {
        outlineColor = color;
        ApplyMaterialProperties();
    }

    public void SetOutlineWidth(
        float width)
    {
        outlineWidth =
            Mathf.Clamp(
                width,
                0.001f,
                0.12f);

        ApplyMaterialProperties();
    }

    private void Awake()
    {
        RebuildRuntimeMaterial();
        RefreshRendererCache();
        RefreshOutlineState();
    }

    private void OnEnable()
    {
        RebuildRuntimeMaterial();
        RefreshRendererCache();
        RefreshOutlineState();
    }

    private void OnDisable()
    {
        RestoreOriginalMaterials();
        isHovered = false;
        isSelected = false;
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();

        DestroyRuntimeMaterial();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        outlineWidth =
            Mathf.Clamp(
                outlineWidth,
                0.001f,
                0.12f);

        if (!Application.isPlaying)
            return;

        ApplyMaterialProperties();
        RefreshOutlineState();
    }
#endif

    public void SetHovered(
        bool value)
    {
        if (isHovered == value)
            return;

        isHovered = value;
        RefreshOutlineState();
    }

    public void SetSelected(
        bool value)
    {
        if (isSelected == value)
            return;

        isSelected = value;
        RefreshOutlineState();
    }

    public void EnableOutline()
    {
        SetSelected(true);
    }

    public void DisableOutline()
    {
        SetSelected(false);
    }

    public void RefreshRendererCache()
    {
        Renderer[] found =
            GetComponentsInChildren<Renderer>(
                true);

        List<Renderer> filtered =
            new();

        foreach (Renderer renderer
                 in found)
        {
            if (renderer == null)
                continue;

            if (renderer is MeshRenderer ||
                renderer is SkinnedMeshRenderer)
            {
                filtered.Add(
                    renderer);
            }
        }

        targetRenderers =
            filtered.ToArray();
    }

    private void RefreshOutlineState()
    {
        bool shouldShow =
            !globalSuppressed &&
            (isHovered ||
             isSelected);

        if (shouldShow)
            ApplyOutlineMaterial();
        else
            RestoreOriginalMaterials();
    }

    private void RebuildRuntimeMaterial()
    {
        if (outlineMaterialTemplate == null)
            return;

        if (runtimeOutlineMaterial != null &&
            runtimeOutlineMaterial.shader ==
            outlineMaterialTemplate.shader)
        {
            ApplyMaterialProperties();
            return;
        }

        DestroyRuntimeMaterial();

        runtimeOutlineMaterial =
            new Material(
                outlineMaterialTemplate)
            {
                name =
                    outlineMaterialTemplate.name +
                    " (Runtime " +
                    gameObject.name +
                    ")",
                // Renderer가 이 runtime Material을 직접 참조하므로
                // DontSaveInEditor는 Editor persistence assertion을 만들 수 있다.
                hideFlags =
                    HideFlags.DontSaveInBuild
            };

        ApplyMaterialProperties();
    }


    private void DestroyRuntimeMaterial()
    {
        if (runtimeOutlineMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeOutlineMaterial);
        else
            DestroyImmediate(runtimeOutlineMaterial);

        runtimeOutlineMaterial = null;
    }

    private void ApplyMaterialProperties()
    {
        if (runtimeOutlineMaterial == null)
        {
            if (outlineMaterialTemplate == null)
                return;

            RebuildRuntimeMaterial();

            if (runtimeOutlineMaterial == null)
                return;
        }

        if (runtimeOutlineMaterial.HasProperty(
                "_OutlineColor"))
        {
            runtimeOutlineMaterial.SetColor(
                "_OutlineColor",
                outlineColor);
        }

        if (runtimeOutlineMaterial.HasProperty(
                "_OutlineWidth"))
        {
            runtimeOutlineMaterial.SetFloat(
                "_OutlineWidth",
                outlineWidth);
        }
    }

    private void ApplyOutlineMaterial()
    {
        if (outlineApplied)
        {
            ApplyMaterialProperties();
            return;
        }

        RebuildRuntimeMaterial();

        if (runtimeOutlineMaterial == null)
            return;

        RefreshRendererCache();
        originalMaterials.Clear();

        foreach (Renderer renderer
                 in targetRenderers)
        {
            if (renderer == null)
                continue;

            Material[] current =
                renderer.sharedMaterials;

            if (current == null)
                current =
                    System.Array.Empty<Material>();

            originalMaterials[renderer] =
                (Material[])current.Clone();

            bool alreadyContainsOutline =
                false;

            foreach (Material material
                     in current)
            {
                if (material != runtimeOutlineMaterial)
                    continue;

                alreadyContainsOutline =
                    true;

                break;
            }

            if (alreadyContainsOutline)
                continue;

            Material[] next =
                new Material[
                    current.Length + 1];

            for (int index = 0;
                 index < current.Length;
                 index++)
            {
                next[index] =
                    current[index];
            }

            next[next.Length - 1] =
                runtimeOutlineMaterial;

            renderer.sharedMaterials =
                next;
        }

        outlineApplied = true;
    }

    private void RestoreOriginalMaterials()
    {
        if (!outlineApplied)
            return;

        foreach (KeyValuePair<Renderer, Material[]> pair
                 in originalMaterials)
        {
            if (pair.Key == null)
                continue;

            pair.Key.sharedMaterials =
                pair.Value ??
                System.Array.Empty<Material>();
        }

        originalMaterials.Clear();
        outlineApplied = false;
    }
}