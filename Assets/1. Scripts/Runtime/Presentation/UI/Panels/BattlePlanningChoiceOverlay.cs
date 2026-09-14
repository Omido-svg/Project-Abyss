using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prefab 의존 없이 Planning 단계의 소수 선택지를 보여주는 공용 modal.
/// P0 D-02 유진 환형의 "현재 무기가 아닌 2개 중 하나" 선택에 사용한다.
/// </summary>
public sealed class BattlePlanningChoiceOverlay : MonoBehaviour
{
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
            return;

        onChosen = chosen;
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        panel = new GameObject("PlanningChoiceOverlay", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image dim = panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject box = new GameObject("ChoiceBox", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        box.transform.SetParent(panel.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(460f, 0f);
        box.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.98f);

        VerticalLayoutGroup layout = box.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 22, 22);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateLabel(box.transform, string.IsNullOrWhiteSpace(title) ? "선택" : title, 28, 54f);
        foreach (ActionPlanningChoiceOption choice in choices)
        {
            if (choice == null) continue;
            string id = choice.Id;
            Button button = CreateButton(box.transform, choice.Label);
            button.onClick.AddListener(() => Choose(id));
        }

        Button cancel = CreateButton(box.transform, "취소");
        cancel.onClick.AddListener(Hide);
        BattleCharacterPointerRouter.BlockWorldInputForFrames(2);
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
        callback?.Invoke(id);
    }

    private static Text CreateLabel(Transform parent, string text, int fontSize, float height)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    private static Button CreateButton(Transform parent, string text)
    {
        GameObject go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f, 1f);
        go.GetComponent<LayoutElement>().preferredHeight = 56f;
        CreateLabel(go.transform, text, 22, 56f).rectTransform.anchorMin = Vector2.zero;
        Text label = go.GetComponentInChildren<Text>();
        RectTransform rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
