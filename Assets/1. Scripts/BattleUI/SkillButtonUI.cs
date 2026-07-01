using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text buttonText;

    private Skill boundSkill;
    private Action<Skill> onClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buttonText == null)
            buttonText = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(
        string label,
        Skill skill,
        bool interactable,
        Action<Skill> clickCallback)
    {
        boundSkill = skill;
        onClicked = clickCallback;

        if (buttonText != null)
            buttonText.text = label;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = interactable;

            button.onClick.AddListener(() =>
            {
                if (boundSkill == null)
                    return;

                onClicked?.Invoke(boundSkill);
            });
        }
    }
}