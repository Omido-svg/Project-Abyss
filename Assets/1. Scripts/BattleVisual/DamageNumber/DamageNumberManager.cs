using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    [SerializeField] private DamageNumberUI damageNumberPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;

    private RectTransform canvasRect;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    public void ShowDamage(
        Vector3 worldPosition,
        int damage)
    {
        ShowDamage(
            worldPosition,
            damage,
            Color.white);
    }

    public void ShowDamage(
        Vector3 worldPosition,
        int damage,
        Color color)
    {
        if (damageNumberPrefab == null)
            return;

        if (canvas == null)
            return;

        if (canvasRect == null)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(
                worldPosition);

        Camera uiCamera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            uiCamera,
            out Vector2 localPoint);

        DamageNumberUI number =
            Instantiate(
                damageNumberPrefab,
                canvasRect);

        RectTransform numberRect =
            number.transform as RectTransform;

        if (numberRect != null)
            numberRect.anchoredPosition = localPoint;

        number.Play(
            damage,
            color);
    }
}