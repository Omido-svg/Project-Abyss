using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ActionSlotButtonUI :
    MonoBehaviour,
    IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    private BattleUIManager uiManager;
    private ActionSlotViewModel viewModel;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(
        BattleUIManager manager,
        ActionSlotViewModel model)
    {
        uiManager = manager;
        viewModel = model;

        if (label != null)
        {
            label.text = model == null
                ? "빈 슬롯"
                : $"{model.IndexText} {model.SkillText}\n{model.TargetText}";
        }

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = model?.Slot != null;

        if (model?.Slot != null)
        {
            button.onClick.AddListener(() =>
            {
                uiManager?.SelectActionSlot(model.Slot);
            });
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Right)
        {
            return;
        }

        if (viewModel?.Slot == null)
            return;

        uiManager?.RemoveActionSlot(
            viewModel.Owner,
            viewModel.OwnerPart,
            viewModel.ActionIndex);
    }
}
