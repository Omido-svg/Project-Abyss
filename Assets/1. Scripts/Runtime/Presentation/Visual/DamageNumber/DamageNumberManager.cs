using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField, Min(0)] private int maxPoolSize = 16;

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
    private bool isHierarchyDisabling;

    private void Awake()
    {
        releaseNumberHandler = ReleaseNumber;
        ResolveReferences();
    }

    private void OnEnable()
    {
        isHierarchyDisabling = false;
    }

    private void OnDisable()
    {
        // Canvas 또는 Scene hierarchy가 비활성화되는 동안에는
        // 자식 DamageNumber의 부모를 바꾸면 Unity가 SetParent를 거부한다.
        // 활성 숫자는 Pool로 옮기지 않고 안전하게 폐기한다.
        isHierarchyDisabling = true;
        DestroyAllActiveNumbers();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        isHierarchyDisabling = true;
        DestroyAllTrackedNumbers();
    }

    public void ShowDamage(
        Vector3 worldPosition,
        int damage)
    {
        ShowDamage(
            worldPosition,
            damage,
            BattleDamageNumberStyle.NormalHp);
    }

    public void ShowDamage(
        Vector3 worldPosition,
        int damage,
        BattleDamageNumberStyle style)
    {
        ShowDamageInternal(
            worldPosition,
            damage,
            style,
            null);
    }

    // 기존 Status/VFX 숫자 호출부 호환.
    public void ShowDamage(
        Vector3 worldPosition,
        int damage,
        Color color)
    {
        ShowDamageInternal(
            worldPosition,
            damage,
            BattleDamageNumberStyle.Custom,
            color);
    }

    private void ShowDamageInternal(
        Vector3 worldPosition,
        int damage,
        BattleDamageNumberStyle style,
        Color? customColor)
    {
        if (!isActiveAndEnabled || isShuttingDown)
            return;

        if (damageNumberPrefab == null ||
            damage <= 0)
        {
            return;
        }

        ResolveReferences();

        if (canvas == null || canvasRect == null || worldCamera == null)
            return;

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(
                worldPosition);

        if (screenPosition.z <= 0f)
            return;

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

        if (customColor.HasValue)
        {
            number.Play(
                damage,
                customColor.Value);
        }
        else
        {
            number.Play(
                damage,
                style);
        }
    }

    private void ResolveReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasRect == null && canvas != null)
            canvasRect = canvas.transform as RectTransform;

        if (worldCamera == null && canvas != null && canvas.worldCamera != null)
            worldCamera = canvas.worldCamera;

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

        bool canPool =
            !isShuttingDown &&
            !isHierarchyDisabling &&
            isActiveAndEnabled &&
            canvasRect != null &&
            canvasRect.gameObject.activeInHierarchy &&
            numberPool.Count < maxPoolSize;

        if (!canPool)
        {
            Destroy(number.gameObject);
            return;
        }

        // Pool 객체를 Canvas 아래에 그대로 둔다.
        // Release 시 SetParent를 호출하지 않으므로 부모 Canvas의
        // 활성화/비활성화 처리와 충돌하지 않는다.
        number.gameObject.SetActive(false);
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

    private void DestroyAllActiveNumbers()
    {
        while (activeNumbers.Count > 0)
        {
            DamageNumberUI number = GetFirstActiveNumber();
            activeNumbers.Remove(number);
            DestroyNumber(number);
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

