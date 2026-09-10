using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// BodyPartButton이 표시할 데이터를 조립한다.
/// BattleUIManager는 refresh/interaction orchestration만 담당하고,
/// 표시 문자열과 highlight/HP/slot projection은 이 factory가 담당한다.
/// </summary>
internal static class BattleBodyPartButtonViewModelFactory
{
    public static BodyPartButtonViewModel Create(
        BodyPartButton button,
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        TargetSelectionViewModel selection)
    {
        if (button == null ||
            button.Owner == null)
        {
            return Empty();
        }

        Character owner = button.Owner;
        BodyPart part = button.BodyPart;

        bool characterTarget =
            part == null &&
            owner.IsSingleHpTarget;

        BodyPartState displayedState =
            ResolveDisplayedPartState(part);

        return new BodyPartButtonViewModel
        {
            PartText =
                GetPartText(
                    owner,
                    part,
                    displayedState),

            HpText =
                GetHpText(
                    button,
                    displayedState),

            SpeedText =
                GetSpeedText(
                    battleManager,
                    owner,
                    part),

            SlotText =
                characterTarget
                    ? string.Empty
                    : GetActionSlotStatusText(
                        battleManager,
                        state,
                        owner,
                        part),

            SkillText =
                GetSelectedSkillText(
                    battleManager,
                    state,
                    selection,
                    owner,
                    part),

            AssignedSkills =
                GetAssignedSkills(
                    battleManager,
                    state,
                    selection,
                    owner,
                    part),

            Interactable =
                CanInteractWithPart(
                    battleManager,
                    state,
                    selection,
                    owner,
                    part),

            IsOwnerSelected =
                IsOwnerHighlighted(
                    battleManager,
                    state,
                    owner,
                    part),

            IsTargetSelected =
                IsTargetHighlighted(
                    battleManager,
                    state,
                    owner,
                    part),

            IsWeakened =
                part != null &&
                displayedState == BodyPartState.Weakened,

            IsBroken =
                part != null &&
                displayedState == BodyPartState.Broken,

            IsCharacterTarget = characterTarget,
            HasHpOverride = button.HasHpOverride,
            ActionCount =
                GetSlotsByOwnerPart(
                    battleManager,
                    owner,
                    part).Count,
            MaxActionSlots =
                part == null
                    ? 0
                    : GetMaxActionSlots(owner, part)
        };
    }

    public static List<ActionSlot> GetSlotsByOwnerPart(
        BattleManager battleManager,
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

    public static int GetMaxActionSlots(
        Character owner,
        BodyPart part)
    {
        if (owner == null || part == null)
            return 0;

        return Mathf.Max(
            0,
            owner.GetMaxActionSlotsForPart(part));
    }

    private static BodyPartButtonViewModel Empty()
    {
        return new BodyPartButtonViewModel
        {
            PartText = "NULL",
            HpText = "HP -",
            SpeedText = "SPD -",
            SlotText = string.Empty,
            SkillText = string.Empty,
            AssignedSkills = System.Array.Empty<Skill>(),
            Interactable = false
        };
    }

    private static string GetSelectedSkillText(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        TargetSelectionViewModel selection,
        Character owner,
        BodyPart part)
    {
        IReadOnlyList<Skill> skills =
            GetAssignedSkills(
                battleManager,
                state,
                selection,
                owner,
                part);

        if (skills == null || skills.Count == 0)
            return string.Empty;

        StringBuilder builder = new();

        for (int actionIndex = 0;
             actionIndex < skills.Count;
             actionIndex++)
        {
            Skill skill = skills[actionIndex];

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

    private static IReadOnlyList<Skill> GetAssignedSkills(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        TargetSelectionViewModel selection,
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return System.Array.Empty<Skill>();

        List<ActionSlot> slots =
            GetSlotsByOwnerPart(
                battleManager,
                owner,
                part);

        int highestActionIndex = -1;

        foreach (ActionSlot slot in slots)
        {
            if (slot != null)
            {
                highestActionIndex =
                    Mathf.Max(
                        highestActionIndex,
                        slot.ActionIndex);
            }
        }

        bool hasPendingSkill =
            state != null &&
            state.InputMode == BattleInputMode.SelectTarget &&
            state.SelectedOwner == owner &&
            IsSamePart(
                state.SelectedOwnerPart,
                part) &&
            selection?.Skill != null;

        if (hasPendingSkill)
        {
            highestActionIndex =
                Mathf.Max(
                    highestActionIndex,
                    state.SelectedActionIndex);
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

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Skill == null ||
                slot.ActionIndex < 0 ||
                slot.ActionIndex >= assigned.Count)
            {
                continue;
            }

            assigned[slot.ActionIndex] = slot.Skill;
        }

        if (hasPendingSkill &&
            state.SelectedActionIndex >= 0 &&
            state.SelectedActionIndex < assigned.Count)
        {
            assigned[state.SelectedActionIndex] =
                selection.Skill;
        }

        return assigned;
    }

    private static string GetActionSlotStatusText(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            part == null ||
            !IsPlayer(battleManager, owner))
        {
            return string.Empty;
        }

        int maxSlots =
            GetMaxActionSlots(owner, part);

        int currentCount =
            GetSlotsByOwnerPart(
                battleManager,
                owner,
                part).Count;

        bool selected =
            state != null &&
            state.SelectedOwner == owner &&
            IsSamePart(
                state.SelectedOwnerPart,
                part);

        if (maxSlots <= 1)
        {
            return selected
                ? "<color=#93C5FD>행동 슬롯 #1 선택</color>"
                : string.Empty;
        }

        string selectionText =
            selected
                ? $" / 편집 #{state.SelectedActionIndex + 1}"
                : string.Empty;

        return
            $"<color=#93C5FD>행동 {currentCount}/{maxSlots}" +
            $"{selectionText}</color>";
    }

    private static bool CanInteractWithPart(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        TargetSelectionViewModel selection,
        Character owner,
        BodyPart part)
    {
        if (owner == null || owner.IsDead)
            return false;

        if (IsPlayer(battleManager, owner))
        {
            return part != null &&
                   !part.IsBroken &&
                   part.IsUsable;
        }

        if (state?.InputMode != BattleInputMode.SelectTarget)
            return false;

        return BattleTargetValidator.IsValid(
            owner,
            part,
            selection?.TargetRule ??
            TargetSelectionRule.StandardAttack);
    }

    private static bool IsOwnerHighlighted(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        Character owner,
        BodyPart part)
    {
        if (owner == null || part == null)
            return false;

        if (state != null &&
            state.SelectedOwner == owner &&
            IsSamePart(
                state.SelectedOwnerPart,
                part))
        {
            return true;
        }

        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null ||
            battleManager?.ActionManager?.Slots == null)
        {
            return false;
        }

        foreach (ActionSlot slot in
                 battleManager.ActionManager.Slots)
        {
            if (slot?.Owner != player)
                continue;

            if (slot.Owner == owner &&
                IsSamePart(slot.Part, part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTargetHighlighted(
        BattleManager battleManager,
        BattlePlanningSelectionState state,
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return false;

        if (state != null &&
            state.SelectedTarget == owner &&
            IsSamePart(
                state.SelectedTargetPart,
                part))
        {
            return true;
        }

        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null ||
            battleManager?.ActionManager?.Slots == null)
        {
            return false;
        }

        foreach (ActionSlot slot in
                 battleManager.ActionManager.Slots)
        {
            if (slot?.Owner != player)
                continue;

            if (slot.TargetCharacter == owner &&
                IsSamePart(
                    slot.TargetPart,
                    part))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPartText(
        Character owner,
        BodyPart part,
        BodyPartState displayedState)
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

    private static string GetHpText(
        BodyPartButton button,
        BodyPartState displayedState)
    {
        Character owner = button?.Owner;

        if (owner == null)
            return "HP -";

        BodyPart part = button.BodyPart;
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
                        ? Mathf.RoundToInt(part.PartHP)
                        : owner.CurrentHP;
        }

        int maxHp =
            part != null
                ? Mathf.RoundToInt(part.MaxPartHP)
                : Mathf.Max(1, owner.MaxCombatHP);

        string hpColor =
            GetHpColor(
                displayedState,
                part,
                currentHp,
                maxHp);

        return
            $"<color={hpColor}>HP {currentHp}/{maxHp}</color>";
    }

    private static string GetHpColor(
        BodyPartState displayedState,
        BodyPart part,
        int displayHp,
        int maxHp)
    {
        if (part != null)
        {
            if (displayedState == BodyPartState.Broken)
                return "#F87171";

            if (displayedState == BodyPartState.Weakened)
                return "#FACC15";
        }

        if (maxHp <= 0)
            return "#FFFFFF";

        float ratio =
            (float)displayHp / maxHp;

        return ratio <= 0.3f
            ? "#FACC15"
            : "#86EFAC";
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

    private static string GetSpeedText(
        BattleManager battleManager,
        Character owner,
        BodyPart part)
    {
        if (battleManager?.SpeedManager == null)
            return "SPD 0";

        return
            $"SPD {battleManager.SpeedManager.GetSpeed(owner, part)}";
    }

    private static bool IsPlayer(
        BattleManager battleManager,
        Character character)
    {
        return character != null &&
               battleManager?.BattleContext?.Player == character;
    }

    private static string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data == null
            ? character.name
            : character.Data.CharacterName;
    }

    private static bool IsSamePart(
        BodyPart first,
        BodyPart second)
    {
        if (first == null || second == null)
        {
            return first == null && second == null;
        }

        return first == second ||
               first.Type == second.Type;
    }
}
