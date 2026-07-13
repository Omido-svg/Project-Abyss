using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    private const int MaxPoolSize = 16;

    [SerializeField] private DamageNumberUI damageNumberPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;

    private readonly Stack<DamageNumberUI> numberPool =
        new Stack<DamageNumberUI>();

    private readonly HashSet<DamageNumberUI> activeNumbers =
        new HashSet<DamageNumberUI>();

    private RectTransform canvasRect;
    private Action<DamageNumberUI> releaseNumberHandler;
    private bool isShuttingDown;

    private void Awake()
    {
        releaseNumberHandler = ReleaseNumber;
        ResolveReferences();
    }

    private void OnDisable()
    {
        ReleaseAllActiveNumbers();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        DestroyAllTrackedNumbers();
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
        if (!isActiveAndEnabled || isShuttingDown)
            return;

        if (damageNumberPrefab == null)
            return;

        ResolveReferences();

        if (canvas == null || canvasRect == null || worldCamera == null)
            return;

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(
                worldPosition);

        Camera uiCamera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            return;
        }

        DamageNumberUI number = GetNumber();

        if (number == null)
            return;

        RectTransform numberRect =
            number.transform as RectTransform;

        if (numberRect != null)
            numberRect.anchoredPosition = localPoint;

        number.Play(
            damage,
            color);
    }

    private void ResolveReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasRect == null && canvas != null)
            canvasRect = canvas.transform as RectTransform;

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private DamageNumberUI GetNumber()
    {
        DamageNumberUI number = null;

        while (numberPool.Count > 0 && number == null)
            number = numberPool.Pop();

        if (number == null)
        {
            number = Instantiate(
                damageNumberPrefab,
                canvasRect);
        }
        else
        {
            number.transform.SetParent(canvasRect, false);
        }

        releaseNumberHandler ??= ReleaseNumber;
        number.PrepareForUse(releaseNumberHandler);
        activeNumbers.Add(number);
        number.gameObject.SetActive(true);

        return number;
    }

    private void ReleaseNumber(DamageNumberUI number)
    {
        if (!activeNumbers.Remove(number))
            return;

        if (number == null)
            return;

        number.ResetForPool();

        if (isShuttingDown || numberPool.Count >= MaxPoolSize)
        {
            Destroy(number.gameObject);
            return;
        }

        number.gameObject.SetActive(false);
        number.transform.SetParent(transform, false);
        numberPool.Push(number);
    }

    private void ReleaseAllActiveNumbers()
    {
        while (activeNumbers.Count > 0)
        {
            DamageNumberUI number = GetFirstActiveNumber();
            ReleaseNumber(number);
        }
    }

    private DamageNumberUI GetFirstActiveNumber()
    {
        foreach (DamageNumberUI number in activeNumbers)
            return number;

        return null;
    }

    private void DestroyAllTrackedNumbers()
    {
        while (activeNumbers.Count > 0)
        {
            DamageNumberUI number = GetFirstActiveNumber();
            activeNumbers.Remove(number);
            DestroyNumber(number);
        }

        while (numberPool.Count > 0)
            DestroyNumber(numberPool.Pop());
    }

    private static void DestroyNumber(DamageNumberUI number)
    {
        if (number == null)
            return;

        number.ResetForPool();
        Destroy(number.gameObject);
    }
}
