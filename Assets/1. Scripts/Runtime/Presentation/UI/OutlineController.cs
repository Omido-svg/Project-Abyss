using UnityEngine;

public class OutlineController : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Renderer targetRenderer;

    private Material originalMaterial;
    [SerializeField] private Material outlineMaterial;

    private bool isOutlined;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        originalMaterial = targetRenderer.material;

        DisableOutline();
    }

    public void EnableOutline()
    {
        if (isOutlined) return;

        targetRenderer.material = outlineMaterial;
        isOutlined = true;
    }

    public void DisableOutline()
    {
        if (!isOutlined) return;

        targetRenderer.material = originalMaterial;
        isOutlined = false;
    }
}