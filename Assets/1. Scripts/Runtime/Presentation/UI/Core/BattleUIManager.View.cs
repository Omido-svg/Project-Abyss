using System.Collections.Generic;
using UnityEngine;

public partial class BattleUIManager : MonoBehaviour
{
    private BodyPartButtonViewModel CreateBodyPartButtonViewModel(
        BodyPartButton button)
    {
        return BattleBodyPartButtonViewModelFactory.Create(
            button,
            battleManager,
            planningState,
            selection);
    }

    private List<ActionSlot> GetSlotsByOwnerPart(
        Character owner,
        BodyPart part)
    {
        return BattleBodyPartButtonViewModelFactory
            .GetSlotsByOwnerPart(
                battleManager,
                owner,
                part);
    }

    private int GetMaxActionSlots(
        Character owner,
        BodyPart part)
    {
        return BattleBodyPartButtonViewModelFactory
            .GetMaxActionSlots(
                owner,
                part);
    }

    public void SetBodyPartHpOverride(
        Character character,
        BodyPart part,
        int hp)
    {
        SetTargetHpOverride(
            character,
            part,
            hp);
    }

    public void SetTargetHpOverride(
        Character character,
        BodyPart part,
        int hp)
    {
        BodyPartButton button =
            FindBodyPartButton(
                character,
                part);

        button?.SetHpOverride(hp);
    }

    public void ClearBodyPartHpOverride(
        Character character,
        BodyPart part)
    {
        ClearTargetHpOverride(
            character,
            part);
    }

    public void ClearTargetHpOverride(
        Character character,
        BodyPart part)
    {
        BodyPartButton button =
            FindBodyPartButton(
                character,
                part);

        button?.ClearHpOverride();
    }

    private BodyPartButton FindBodyPartButton(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return null;

        return bodyPartButtonRegistry?.Find(
            character,
            part,
            requireActive: false);
    }

    public void RefreshAllUI()
    {
        RefreshAllBodyPartButtons();
        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshCharacterUI(Character character)
    {
        if (character == null)
            return;

        bodyPartButtonRegistry?.CopyCharacterButtonsTo(
            character,
            buttonScratch,
            requireActive: false);

        RefreshButtons(buttonScratch);
        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshBodyPartUI(BodyPart part)
    {
        if (part == null)
            return;

        RefreshTargetUI(part.Owner, part);
    }

    public void RefreshTargetUI(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return;

        RefreshButton(
            FindBodyPartButton(character, part));

        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshButton(BodyPartButton button)
    {
        if (button == null)
            return;

        button.ApplyViewModel(
            CreateBodyPartButtonViewModel(button));
    }

    private void RefreshButtons(
        IReadOnlyList<BodyPartButton> buttons)
    {
        if (buttons == null)
            return;

        foreach (BodyPartButton button in buttons)
            RefreshButton(button);
    }

    public bool ShowCharacterDetails(Character owner)
    {
        EnsureReferences();

        if (owner == null || characterDetailPanel == null)
            return false;

        characterDetailPanel.Show(owner);
        return true;
    }

    public void RefreshAllBodyPartButtons()
    {
        if (bodyPartButtonRegistry == null)
            return;

        bodyPartButtonRegistry.CopyAllTo(
            buttonScratch,
            requireActive: false);

        RefreshButtons(buttonScratch);
    }

}