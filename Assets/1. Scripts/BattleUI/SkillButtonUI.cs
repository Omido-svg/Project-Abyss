using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text buttonText;

    private Skill boundSkill;
    private int boundActionIndex;
    private Action<Skill, int> onClicked;

    public Skill BoundSkill => boundSkill;
    public int BoundActionIndex => boundActionIndex;

    private void Awake()
    {
        button ??= GetComponent<Button>();
        buttonText ??= GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(
        string label,
        Skill skill,
        bool interactable,
        Action<Skill> clickCallback)
    {
        Bind(
            label,
            skill,
            0,
            interactable,
            clickCallback == null
                ? null
                : (selectedSkill, _) =>
                    clickCallback(selectedSkill));
    }

    public void Bind(
        string label,
        Skill skill,
        int actionIndex,
        bool interactable,
        Action<Skill, int> clickCallback)
    {
        boundSkill = skill;
        boundActionIndex = Mathf.Max(0, actionIndex);
        onClicked = clickCallback;

        if (buttonText != null)
        {
            buttonText.richText = true;
            buttonText.text = label;
        }

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = interactable && boundSkill != null;

        if (!button.interactable)
            return;

        button.onClick.AddListener(InvokeBoundSkill);
    }

    private void InvokeBoundSkill()
    {
        if (boundSkill == null)
            return;

        onClicked?.Invoke(
            boundSkill,
            boundActionIndex);
    }
}
