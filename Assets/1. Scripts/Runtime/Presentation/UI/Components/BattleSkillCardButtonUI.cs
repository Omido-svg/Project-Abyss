using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleSkillCardButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rollText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Image accentImage;

    private Skill skill;
    private int actionIndex;
    private Action<Skill, int> callback;

    public void Configure(
        Button sourceButton,
        TMP_Text title,
        TMP_Text rolls,
        TMP_Text cost,
        TMP_Text reason,
        Image accent)
    {
        button = sourceButton;
        titleText = title;
        rollText = rolls;
        costText = cost;
        reasonText = reason;
        accentImage = accent;
    }

    private void Awake()
    {
        button ??= GetComponent<Button>();
    }

    public void Bind(
        Skill value,
        int selectedActionIndex,
        bool usable,
        string reason,
        Action<Skill, int> onClicked)
    {
        skill = value;
        actionIndex = Mathf.Max(0, selectedActionIndex);
        callback = onClicked;

        if (titleText != null)
            titleText.text = skill?.SkillName ?? "스킬 없음";

        if (rollText != null)
            rollText.text = BattleSkillUiText.BuildRollSummary(skill);

        if (costText != null)
            costText.text = skill == null
                ? string.Empty
                : $"빛 {skill.EnergyCost}";

        if (reasonText != null)
        {
            reasonText.gameObject.SetActive(!usable);
            reasonText.text = usable
                ? string.Empty
                : string.IsNullOrWhiteSpace(reason)
                    ? "현재 사용 불가"
                    : reason;
        }

        if (accentImage != null)
            accentImage.color = GetActionColor(skill?.ActionType);

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = usable && skill != null;

        if (button.interactable)
            button.onClick.AddListener(InvokeClick);
    }

    private void InvokeClick()
    {
        if (skill != null)
            callback?.Invoke(skill, actionIndex);
    }

    private static Color GetActionColor(ActionType? type)
    {
        return type switch
        {
            ActionType.NormalAttack => new Color(0.18f, 0.55f, 1f, 1f),
            ActionType.Duel => new Color(0.95f, 0.22f, 0.18f, 1f),
            ActionType.Preparation => new Color(0.14f, 0.75f, 0.62f, 1f),
            ActionType.Prestige => new Color(0.95f, 0.60f, 0.12f, 1f),
            _ => Color.white
        };
    }
}
