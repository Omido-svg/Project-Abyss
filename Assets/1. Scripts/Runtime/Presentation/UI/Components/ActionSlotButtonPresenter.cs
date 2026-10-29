using UnityEngine;

/// <summary>
/// Presenter for the planning action-slot button.
/// Converts runtime state into immutable display values and dispatches user intent.
/// Does not manipulate Unity UI components or compute combat rules.
/// </summary>
public sealed class ActionSlotButtonPresenter
{
    private BattleUIManager uiManager;
    private Character owner;
    private BodyPart ownerPart;
    private ActionSlot slot;
    private int actionIndex;

    public void Bind(
        BattleUIManager manager,
        Character slotOwner,
        BodyPart part,
        int index,
        ActionSlot boundSlot)
    {
        uiManager = manager;
        owner = slotOwner;
        ownerPart = part;
        actionIndex = Mathf.Max(0, index);
        slot = boundSlot;
    }

    public ActionSlotButtonPresentation CreatePresentation(bool selected)
    {
        string marker = selected ? "<color=#93C5FD>▶</color> " : string.Empty;
        string indexText = $"{marker}<b>행동 #{actionIndex + 1}</b>";

        string text;
        if (slot == null)
        {
            text = $"{indexText}\n<color=#A1A1AA>빈 슬롯</color>";
        }
        else
        {
            // Keep the legacy slot projection so visual labels do not change.
            ActionSlotViewModel model = ActionSlotViewModel.FromSlot(slot);
            string skillText = model?.SkillText ?? "<없음>";
            string targetText = model?.TargetText ?? "대상 없음";
            text = $"{indexText}\n{skillText}\n<size=80%>{targetText}</size>";
        }

        return new ActionSlotButtonPresentation(
            text,
            selected,
            slot != null,
            uiManager != null && owner != null && ownerPart != null);
    }

    public void OnPrimaryClick()
    {
        if (BattlePlanningCameraController.IsCameraMovementActive)
            return;

        if (uiManager != null && owner != null && ownerPart != null)
            uiManager.OpenOwnerActionSlot(owner, ownerPart, actionIndex);
    }

    public void OnSecondaryClick()
    {
        if (BattlePlanningCameraController.IsCameraMovementActive || slot == null)
            return;

        if (uiManager != null)
            uiManager.RemoveActionSlotAndContinueEditing(owner, ownerPart, actionIndex);
    }
}

/// <summary>
/// View-facing snapshot. It intentionally contains no Skill, Character, or ActionSlot.
/// </summary>
public readonly struct ActionSlotButtonPresentation
{
    public string Label { get; }
    public bool IsSelected { get; }
    public bool HasAction { get; }
    public bool CanSelect { get; }

    public ActionSlotButtonPresentation(
        string label,
        bool isSelected,
        bool hasAction,
        bool canSelect)
    {
        Label = label ?? string.Empty;
        IsSelected = isSelected;
        HasAction = hasAction;
        CanSelect = canSelect;
    }
}
