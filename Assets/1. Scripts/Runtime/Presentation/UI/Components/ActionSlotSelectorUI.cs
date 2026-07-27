using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 부위의 최대 ActionSlot 수에 맞춰 버튼을 자동 생성한다.
/// Scene에 별도 프리팹을 배치하지 않아도 Skill 버튼의 외형을 복제해
/// 런타임 슬롯 바를 구성한다.
/// </summary>
public sealed class ActionSlotSelectorUI : MonoBehaviour
{
    [Header("Optional Manual Setup")]
    [SerializeField] private RectTransform container;
    [SerializeField] private ActionSlotButtonUI buttonTemplate;

    [Header("Runtime Fallback")]
    [SerializeField] private bool createRuntimeFallback = true;
    [SerializeField] private Vector2 fallbackButtonSize = new(170f, 72f);
    [SerializeField] private float spacing = 8f;
    [SerializeField] private Vector2 anchoredPosition = new(0f, -18f);

    private readonly List<ActionSlotButtonUI>
        generatedButtons = new();

    private Button fallbackStyleSource;

    public void ConfigureFallbackStyle(
        Button sourceButton)
    {
        fallbackStyleSource = sourceButton;
    }

    public void Rebuild(
        BattleUIManager uiManager,
        Character owner,
        BodyPart part,
        int selectedIndex,
        int maxSlots)
    {
        int safeMaxSlots = Mathf.Max(1, maxSlots);

        EnsureRuntimeSetup();
        ClearGeneratedButtons();

        if (container == null || buttonTemplate == null)
        {
            Debug.LogWarning(
                "[ActionSlotSelectorUI] 슬롯 버튼 생성에 필요한 " +
                "Container 또는 Template이 없습니다.",
                this);
            return;
        }

        for (int index = 0;
             index < safeMaxSlots;
             index++)
        {
            ActionSlotButtonUI button =
                Instantiate(
                    buttonTemplate,
                    container);

            button.name =
                $"ActionSlot_{index + 1}";

            button.gameObject.SetActive(true);

            ActionSlot slot =
                uiManager?.FindActionSlot(
                    owner,
                    part,
                    index);

            button.Bind(
                uiManager,
                owner,
                part,
                index,
                slot,
                selectedIndex == index);

            generatedButtons.Add(button);
        }

        ResizeContainer(safeMaxSlots);

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            container);
    }

    public void ClearGeneratedButtons()
    {
        for (int i = generatedButtons.Count - 1;
             i >= 0;
             i--)
        {
            ActionSlotButtonUI button =
                generatedButtons[i];

            if (button == null)
                continue;

            button.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(button.gameObject);
            else
                DestroyImmediate(button.gameObject);
        }

        generatedButtons.Clear();
    }

    private void EnsureRuntimeSetup()
    {
        if (container != null && buttonTemplate != null)
            return;

        if (!createRuntimeFallback)
            return;

        if (container == null)
            container = CreateFallbackContainer();

        if (buttonTemplate == null)
            buttonTemplate = CreateFallbackTemplate();
    }

    private RectTransform CreateFallbackContainer()
    {
        GameObject containerObject =
            new GameObject(
                "GeneratedActionSlotBar",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));

        RectTransform result =
            containerObject.GetComponent<RectTransform>();

        result.SetParent(
            transform,
            worldPositionStays: false);

        result.anchorMin = new Vector2(0.5f, 1f);
        result.anchorMax = new Vector2(0.5f, 1f);
        result.pivot = new Vector2(0.5f, 1f);
        result.anchoredPosition = anchoredPosition;
        result.sizeDelta = fallbackButtonSize;

        HorizontalLayoutGroup layout =
            containerObject.GetComponent<HorizontalLayoutGroup>();

        layout.spacing = Mathf.Max(0f, spacing);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        result.SetAsLastSibling();
        return result;
    }

    private ActionSlotButtonUI CreateFallbackTemplate()
    {
        GameObject templateObject =
            fallbackStyleSource != null
                ? Instantiate(
                    fallbackStyleSource.gameObject,
                    container)
                : CreatePlainButtonObject(container);

        templateObject.name =
            "ActionSlotButtonTemplate";

        RectTransform rect =
            templateObject.GetComponent<RectTransform>();

        if (rect != null)
            rect.sizeDelta = fallbackButtonSize;

        SkillButtonUI skillButton =
            templateObject.GetComponent<SkillButtonUI>();

        if (skillButton != null)
            skillButton.enabled = false;

        ActionSlotButtonUI slotButton =
            templateObject.GetComponent<ActionSlotButtonUI>();

        if (slotButton == null)
        {
            slotButton =
                templateObject.AddComponent<ActionSlotButtonUI>();
        }

        Button button =
            templateObject.GetComponent<Button>();

        TMP_Text label =
            templateObject.GetComponentInChildren<TMP_Text>(true);

        Image image =
            templateObject.GetComponent<Image>();

        slotButton.ConfigureReferences(
            button,
            label,
            image);

        templateObject.SetActive(false);
        return slotButton;
    }

    private GameObject CreatePlainButtonObject(
        Transform parent)
    {
        GameObject buttonObject =
            new GameObject(
                "ActionSlotButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        buttonObject.transform.SetParent(
            parent,
            worldPositionStays: false);

        GameObject textObject =
            new GameObject(
                "Text (TMP)",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        textObject.transform.SetParent(
            buttonObject.transform,
            worldPositionStays: false);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();

        text.alignment = TextAlignmentOptions.Center;
        // text.enableWordWrapping = true;
        text.fontSize = 18f;

        return buttonObject;
    }

    private void ResizeContainer(int count)
    {
        if (container == null)
            return;

        float width =
            fallbackButtonSize.x * count +
            Mathf.Max(0f, spacing) *
            Mathf.Max(0, count - 1);

        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width);

        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            fallbackButtonSize.y);
    }

    private void OnDestroy()
    {
        ClearGeneratedButtons();
    }
}
