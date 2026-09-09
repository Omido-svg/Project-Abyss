using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class BattleUIManager : MonoBehaviour
{
    private BodyPartButtonViewModel CreateBodyPartButtonViewModel(
        BodyPartButton button)
    {
        if (button == null)
        {
            return new BodyPartButtonViewModel
            {
                PartText = "NULL",
                HpText = "HP -",
                SpeedText = "SPD -",
                SlotText = "",
                SkillText = "",
                AssignedSkills = System.Array.Empty<Skill>(),
                Interactable = false
            };
        }

        Character owner =
            button.Owner;

        BodyPart part =
            button.BodyPart;

        if (owner == null)
        {
            return new BodyPartButtonViewModel
            {
                PartText = "NULL",
                HpText = "HP -",
                SpeedText = "SPD -",
                SlotText = "",
                SkillText = "",
                AssignedSkills = System.Array.Empty<Skill>(),
                Interactable = false
            };
        }

        bool characterTarget =
            part == null &&
            owner.IsSingleHpTarget;

        return new BodyPartButtonViewModel
        {
            PartText =
                GetPartText(
                    owner,
                    part),

            HpText =
                GetHpText(button),

            SpeedText =
                GetSpeedText(owner, part),

            SlotText =
                characterTarget
                    ? ""
                    : GetActionSlotStatusText(
                        owner,
                        part),

            SkillText =
                GetSelectedSkillText(
                    owner,
                    part),

            AssignedSkills =
                GetAssignedSkills(
                    owner,
                    part),

            Interactable =
                CanInteractWithPart(
                    owner,
                    part),

            IsOwnerSelected =
                IsOwnerHighlighted(
                    owner,
                    part),

            IsTargetSelected =
                IsTargetHighlighted(
                    owner,
                    part),

            IsWeakened =
                part != null &&
                ResolveDisplayedPartState(part) ==
                    BodyPartState.Weakened,

            IsBroken =
                part != null &&
                ResolveDisplayedPartState(part) ==
                    BodyPartState.Broken,

            IsCharacterTarget = characterTarget,
            HasHpOverride = button.HasHpOverride,
            ActionCount = GetSlotsByOwnerPart(owner, part).Count,
            MaxActionSlots =
                part == null
                    ? 0
                    : GetMaxActionSlots(owner, part)
        };
    }

    private string GetSelectedSkillText(
        Character owner,
        BodyPart part)
    {
        IReadOnlyList<Skill> skills =
            GetAssignedSkills(
                owner,
                part);

        if (skills == null ||
            skills.Count == 0)
        {
            return "";
        }

        StringBuilder builder =
            new();

        for (int actionIndex = 0;
             actionIndex < skills.Count;
             actionIndex++)
        {
            Skill skill =
                skills[actionIndex];

            if (skill == null)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(
                $"<color=#FFFFFF><b>[#{actionIndex + 1}] " +
                $"{skill.SkillName}</b></color>");
        }

        return builder.ToString();
    }

    private IReadOnlyList<Skill> GetAssignedSkills(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return System.Array.Empty<Skill>();

        List<ActionSlot> slots =
            GetSlotsByOwnerPart(
                owner,
                part);

        int highestActionIndex =
            -1;

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null)
                continue;

            highestActionIndex =
                Mathf.Max(
                    highestActionIndex,
                    slot.ActionIndex);
        }

        bool hasPendingSkill =
            inputMode == BattleInputMode.SelectTarget &&
            selectedOwner == owner &&
            IsSamePart(
                selectedOwnerPart,
                part) &&
            selection.Skill != null;

        if (hasPendingSkill)
        {
            highestActionIndex =
                Mathf.Max(
                    highestActionIndex,
                    selectedActionIndex);
        }

        if (highestActionIndex < 0)
            return System.Array.Empty<Skill>();

        List<Skill> assigned =
            new(highestActionIndex + 1);

        for (int index = 0;
             index <= highestActionIndex;
             index++)
        {
            assigned.Add(null);
        }

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot?.Skill == null ||
                slot.ActionIndex < 0 ||
                slot.ActionIndex >= assigned.Count)
            {
                continue;
            }

            assigned[slot.ActionIndex] =
                slot.Skill;
        }

        // 대상 선택 전의 Pending 스킬도 즉시 공/수 패턴에 반영한다.
        if (hasPendingSkill &&
            selectedActionIndex >= 0 &&
            selectedActionIndex < assigned.Count)
        {
            assigned[selectedActionIndex] =
                selection.Skill;
        }

        return assigned;
    }

    private string GetActionSlotStatusText(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            part == null ||
            !IsPlayer(owner))
        {
            return "";
        }

        int maxSlots =
            GetMaxActionSlots(owner, part);

        int currentCount =
            GetSlotsByOwnerPart(owner, part).Count;

        if (maxSlots <= 1)
        {
            return
                selectedOwner == owner &&
                IsSamePart(selectedOwnerPart, part)
                    ? "<color=#93C5FD>행동 슬롯 #1 선택</color>"
                    : "";
        }

        string selectionText =
            selectedOwner == owner &&
            IsSamePart(selectedOwnerPart, part)
                ? $" / 편집 #{selectedActionIndex + 1}"
                : "";

        return
            $"<color=#93C5FD>행동 {currentCount}/{maxSlots}" +
            $"{selectionText}</color>";
    }

    private List<ActionSlot> GetSlotsByOwnerPart(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            battleManager?.ActionManager == null)
        {
            return new List<ActionSlot>();
        }

        return battleManager.ActionManager
            .FindSlots(owner, part);
    }

    private int GetMaxActionSlots(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            part == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            owner.GetMaxActionSlotsForPart(part));
    }

    private bool CanInteractWithPart(
        Character owner,
        BodyPart part)
    {
        if (owner == null || owner.IsDead)
            return false;

        if (IsPlayer(owner))
        {
            if (part == null)
                return false;

            return !part.IsBroken && part.IsUsable;
        }

        if (inputMode != BattleInputMode.SelectTarget)
            return false;

        return BattleTargetValidator.IsValid(
            owner,
            part,
            selection.TargetRule);
    }

    private bool IsOwnerHighlighted(
        Character owner,
        BodyPart part)
    {
        if (owner == null || part == null)
            return false;

        if (selectedOwner == owner &&
            IsSamePart(selectedOwnerPart, part))
        {
            return true;
        }

        if (battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null)
        {
            return false;
        }

        Character player =
            battleManager.BattleContext.Player;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != player)
                continue;

            if (slot.Owner == owner &&
                IsSamePart(slot.Part, part))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTargetHighlighted(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return false;

        if (selectedTarget == owner &&
            IsSameTargetPart(
                selectedTargetPart,
                part))
        {
            return true;
        }

        if (battleManager?.ActionManager == null ||
            battleManager.BattleContext == null)
        {
            return false;
        }

        Character player =
            battleManager.BattleContext.Player;

        foreach (ActionSlot slot
                 in battleManager.ActionManager.Slots)
        {
            if (slot == null ||
                slot.Owner != player)
            {
                continue;
            }

            if (slot.TargetCharacter == owner &&
                IsSameTargetPart(
                    slot.TargetPart,
                    part))
            {
                return true;
            }
        }

        return false;
    }

    private string GetPartText(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return "NULL";

        string characterName =
            GetCharacterName(owner);

        if (part == null)
        {
            return
                $"<size=72%>{characterName}</size>\n" +
                "<color=#86EFAC><b>SINGLE HP</b></color>";
        }

        BodyPartState displayedState =
            ResolveDisplayedPartState(part);

        string stateColor =
            displayedState switch
            {
                BodyPartState.Normal => "#86EFAC",
                BodyPartState.Weakened => "#FACC15",
                BodyPartState.Broken => "#F87171",
                _ => "#FFFFFF"
            };

        return
            $"<size=72%>{characterName}</size>\n" +
            $"<color={stateColor}><b>" +
            $"{part.Type} [{displayedState}]" +
            "</b></color>";
    }

    private string GetHpText(
        BodyPartButton button)
    {
        if (button?.Owner == null)
            return "HP -";

        Character owner =
            button.Owner;

        BodyPart part =
            button.BodyPart;

        int currentHp;

        if (BattlePresentationStateRegistry.TryGetHp(
                owner,
                part,
                out int presentationHp))
        {
            currentHp = presentationHp;
        }
        else
        {
            currentHp =
                button.HasHpOverride
                    ? button.HpOverrideValue
                    : part != null
                        ? Mathf.RoundToInt(
                            part.PartHP)
                        : owner.CurrentHP;
        }

        int maxHp =
            part != null
                ? Mathf.RoundToInt(
                    part.MaxPartHP)
                : Mathf.Max(
                    1,
                    owner.MaxCombatHP);

        string hpColor =
            GetHpColor(
                owner,
                part,
                currentHp,
                maxHp);

        return
            $"<color={hpColor}>HP {currentHp}/{maxHp}</color>";
    }

    private string GetHpColor(
        Character owner,
        BodyPart part,
        int displayHp,
        int maxHp)
    {
        if (owner == null)
            return "#FFFFFF";

        if (part != null)
        {
            BodyPartState displayedState =
                ResolveDisplayedPartState(part);

            if (displayedState == BodyPartState.Broken)
                return "#F87171";

            if (displayedState == BodyPartState.Weakened)
                return "#FACC15";
        }

        if (maxHp <= 0)
            return "#FFFFFF";

        float ratio =
            (float)displayHp /
            maxHp;

        if (ratio <= 0.3f)
            return "#FACC15";

        return "#86EFAC";
    }

    private static BodyPartState ResolveDisplayedPartState(
        BodyPart part)
    {
        if (part == null)
            return BodyPartState.Normal;

        return BattlePresentationStateRegistry.TryGetPartState(
                part,
                out BodyPartState presentationState)
            ? presentationState
            : part.State;
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

    private string GetSpeedText(
        Character owner,
        BodyPart part)
    {
        if (battleManager?.SpeedManager == null)
            return "SPD 0";

        int speed =
            battleManager.SpeedManager.GetSpeed(
                owner,
                part);

        return $"SPD {speed}";
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