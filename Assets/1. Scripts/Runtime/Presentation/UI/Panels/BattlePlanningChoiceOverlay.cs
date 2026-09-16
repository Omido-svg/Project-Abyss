using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prefab 의존 없이 Planning 단계의 소수 선택지를 보여주는 공용 modal.
/// P0 D-02 유진 환형의 "현재 무기가 아닌 2개 중 하나" 선택에 사용한다.
///
/// 0916 v1.5:
/// SkillSelectionLayer가 별도/중첩 Canvas 정렬을 사용하는 Scene에서도
/// 선택창이 뒤에 가려지지 않도록 전용 override-sorting Canvas를 만든다.
/// </summary>
public sealed class BattlePlanningChoiceOverlay : MonoBehaviour
{
    private const int SortingOrderOffset = 1000;
    private const int MaximumSafeSortingOrder = 32000;

    private GameObject panel;
    private Action<string> onChosen;

    public bool IsOpen => panel != null;

    public void Show(
        string title,
        IReadOnlyList<ActionPlanningChoiceOption> choices,
        Action<string> chosen)
    {
        Hide();
        if (choices == null || choices.Count == 0)
        {
            Debug.LogWarning(
                "[PlanningChoice] 표시할 선택지가 없어 modal을 열지 않았습니다. " +
                $"Title={title ?? "NULL"}");
            return;
        }

        onChosen = chosen;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform parent = rootCanvas != null
            ? rootCanvas.rootCanvas.transform
            : transform;

        panel = new GameObject(
            "PlanningChoiceOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.transform.SetAsLastSibling();
        panel.layer = parent.gameObject.layer;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Canvas modalCanvas = panel.GetComponent<Canvas>();
        modalCanvas.overrideSorting = true;

        if (rootCanvas != null)
        {
            modalCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            modalCanvas.sortingOrder = Mathf.Clamp(
                rootCanvas.sortingOrder + SortingOrderOffset,
                -MaximumSafeSortingOrder,
                MaximumSafeSortingOrder);
        }
        else
        {
            modalCanvas.sortingOrder = SortingOrderOffset;
        }

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Image dim = panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        GameObject box = new GameObject(
            "ChoiceBox",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        box.transform.SetParent(panel.transform, false);
        box.layer = panel.layer;

        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(460f, 0f);

        Image boxImage = box.GetComponent<Image>();
        boxImage.color = new Color(0.08f, 0.10f, 0.13f, 0.98f);
        boxImage.raycastTarget = true;

        VerticalLayoutGroup layout = box.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 22, 22);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateLabel(
            box.transform,
            string.IsNullOrWhiteSpace(title) ? "선택" : title,
            28,
            54f,
            panel.layer);

        foreach (ActionPlanningChoiceOption choice in choices)
        {
            if (choice == null)
                continue;

            string id = choice.Id;
            Button button = CreateButton(
                box.transform,
                choice.Label,
                panel.layer);
            button.onClick.AddListener(() => Choose(id));
        }

        Button cancel = CreateButton(
            box.transform,
            "취소",
            panel.layer);
        cancel.onClick.AddListener(Hide);

        LayoutRebuilder.ForceRebuildLayoutImmediate(boxRect);
        panel.transform.SetAsLastSibling();
        BattleCharacterPointerRouter.BlockWorldInputForFrames(2);

        Debug.Log(
            $"[PlanningChoice] OPEN / Title={title}, " +
            $"Choices={choices.Count}, SortingOrder={modalCanvas.sortingOrder}");
    }

    public void Hide()
    {
        if (panel != null)
            Destroy(panel);

        panel = null;
        onChosen = null;
        BattleCharacterPointerRouter.BlockWorldInputForFrames(2);
    }

    private void Choose(string id)
    {
        Action<string> callback = onChosen;

        if (panel != null)
            Destroy(panel);

        panel = null;
        onChosen = null;
        BattleCharacterPointerRouter.BlockWorldInputForFrames(2);

        Debug.Log($"[PlanningChoice] CHOSEN / Id={id}");
        callback?.Invoke(id);
    }

    private static Text CreateLabel(
        Transform parent,
        string text,
        int fontSize,
        float height,
        int layer)
    {
        GameObject go = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(Text),
            typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.layer = layer;

        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        go.GetComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    private static Button CreateButton(
        Transform parent,
        string text,
        int layer)
    {
        GameObject go = new GameObject(
            "Choice",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.layer = layer;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.20f, 0.26f, 1f);
        image.raycastTarget = true;

        go.GetComponent<LayoutElement>().preferredHeight = 56f;

        Text label = CreateLabel(
            go.transform,
            text,
            22,
            56f,
            layer);

        RectTransform rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private void OnDestroy()
    {
        if (panel != null)
            Destroy(panel);
        panel = null;
        onChosen = null;
    }
}
