using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterBuildController
{
    private readonly Character owner;
    private readonly List<CharacterItem> equippedItems;
    private readonly List<CharacterAugment> equippedAugments;

    public IReadOnlyList<CharacterItem> EquippedItems =>
        equippedItems;

    public IReadOnlyList<CharacterAugment> EquippedAugments =>
        equippedAugments;

    public CharacterBuildController(
        Character owner,
        List<CharacterItem> equippedItems,
        List<CharacterAugment> equippedAugments)
    {
        this.owner = owner;

        this.equippedItems =
            equippedItems ??
            new List<CharacterItem>();

        this.equippedAugments =
            equippedAugments ??
            new List<CharacterAugment>();
    }

    public void AddItem(CharacterItem item)
    {
        if (item == null ||
            equippedItems.Contains(item))
        {
            return;
        }

        equippedItems.Add(item);
    }

    public void AddAugment(CharacterAugment augment)
    {
        if (augment == null ||
            equippedAugments.Contains(augment))
        {
            return;
        }

        equippedAugments.Add(augment);
    }

    public void ApplyItemSkillModifiers(
        IReadOnlyList<BodyPart> bodyParts)
    {
        if (owner == null ||
            bodyParts == null)
        {
            return;
        }

        CharacterItem[] itemSnapshot =
            equippedItems.ToArray();

        foreach (BodyPart part in bodyParts)
        {
            if (part == null)
                continue;

            List<Skill> skills =
                new(part.AvailableSkills);

            foreach (CharacterItem item in itemSnapshot)
            {
                if (!CanApply(item))
                    continue;

                TryExecuteBuildStep(
                    () => item.ModifySkills(
                        owner,
                        part,
                        skills),
                    $"Item={GetItemName(item)}, Step=ModifySkills, Part={part.Type}");
            }

            part.ReplaceSkills(skills);
        }
    }

    public void ApplyStatusModifiers(
        CurrentStatus currentStatus)
    {
        if (owner == null ||
            currentStatus == null)
        {
            return;
        }

        foreach (CharacterItem item in equippedItems.ToArray())
        {
            if (!CanApply(item))
                continue;

            TryExecuteBuildStep(
                () => item.ModifyStatus(
                    owner,
                    currentStatus),
                $"Item={GetItemName(item)}, Step=ModifyStatus");
        }

        foreach (CharacterAugment augment in equippedAugments.ToArray())
        {
            if (!CanApply(augment))
                continue;

            TryExecuteBuildStep(
                () => augment.ModifyStatus(
                    owner,
                    currentStatus),
                $"Augment={augment.AugmentName}, Step=ModifyStatus");
        }

        currentStatus.Clamp();
    }

    public void ApplyBodyPartModifiers(
        IReadOnlyList<BodyPart> bodyParts)
    {
        if (owner == null ||
            bodyParts == null)
        {
            return;
        }

        CharacterItem[] items =
            equippedItems.ToArray();

        CharacterAugment[] augments =
            equippedAugments.ToArray();

        foreach (BodyPart part in bodyParts)
        {
            if (part == null)
                continue;

            foreach (CharacterAugment augment in augments)
            {
                if (!CanApply(augment))
                    continue;

                TryExecuteBuildStep(
                    () => augment.ModifyBodyPart(
                        owner,
                        part),
                    $"Augment={augment.AugmentName}, Step=ModifyBodyPart, Part={part.Type}");
            }

            foreach (CharacterItem item in items)
            {
                if (!CanApply(item))
                    continue;

                TryExecuteBuildStep(
                    () => item.ModifyBodyPart(
                        owner,
                        part),
                    $"Item={GetItemName(item)}, Step=ModifyBodyPart, Part={part.Type}");
            }
        }
    }

    public List<CombatMechanic> CreateMechanics()
    {
        List<CombatMechanic> result =
            new();

        HashSet<CombatMechanic> unique =
            new();

        List<CombatMechanic> buffer =
            new();

        foreach (CharacterItem item in equippedItems.ToArray())
        {
            if (!CanApply(item))
                continue;

            buffer.Clear();

            CharacterBuildMechanicContext context =
                CharacterBuildMechanicContext.ForItem(
                    owner,
                    item);

            TryExecuteBuildStep(
                () => CreateItemMechanicsCompatible(
                    item,
                    context,
                    buffer),
                $"Item={GetItemName(item)}, Step=CreateMechanics");

            AppendMechanics(
                buffer,
                unique,
                result,
                context);
        }

        foreach (CharacterAugment augment in equippedAugments.ToArray())
        {
            if (!CanApply(augment))
                continue;

            buffer.Clear();

            CharacterBuildMechanicContext context =
                CharacterBuildMechanicContext.ForAugment(
                    owner,
                    augment);

            TryExecuteBuildStep(
                () => augment.CreateMechanics(
                    context,
                    buffer),
                $"Augment={augment.AugmentName}, Step=CreateMechanics");

            AppendMechanics(
                buffer,
                unique,
                result,
                context);
        }

        return result;
    }

    private static void CreateItemMechanicsCompatible(
        CharacterItem item,
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        if (item == null ||
            output == null)
        {
            return;
        }

        item.CreateMechanics(
            context,
            output);
    }

    private bool CanApply(CharacterItem item)
    {
        if (item == null ||
            owner == null)
        {
            return false;
        }

        try
        {
            return item.CanApplyTo(owner);
        }
        catch (Exception exception)
        {
            LogBuildException(
                $"Item={GetItemName(item)}, Step=CanApplyTo",
                exception);

            return false;
        }
    }

    private bool CanApply(CharacterAugment augment)
    {
        if (augment == null ||
            owner == null)
        {
            return false;
        }

        try
        {
            return augment.CanApplyTo(owner);
        }
        catch (Exception exception)
        {
            LogBuildException(
                $"Augment={augment.AugmentName}, Step=CanApplyTo",
                exception);

            return false;
        }
    }

    private void AppendMechanics(
        IReadOnlyList<CombatMechanic> source,
        HashSet<CombatMechanic> unique,
        List<CombatMechanic> output,
        CharacterBuildMechanicContext context)
    {
        if (source == null ||
            unique == null ||
            output == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CombatMechanic mechanic =
                source[i];

            if (mechanic == null)
                continue;

            if (mechanic.IsInitialized ||
                mechanic.IsRegistered)
            {
                Debug.LogWarning(
                    $"[CharacterBuildController] 이미 사용 중인 CombatMechanic 인스턴스 거부 / " +
                    $"Owner={GetOwnerName()}, " +
                    $"Source={context.SourceType}:{context.SourceName}, " +
                    $"Mechanic={mechanic.MechanicName}");

                continue;
            }

            if (!unique.Add(mechanic))
            {
                Debug.LogWarning(
                    $"[CharacterBuildController] 중복 CombatMechanic 인스턴스 무시 / " +
                    $"Owner={GetOwnerName()}, " +
                    $"Source={context.SourceType}:{context.SourceName}, " +
                    $"Mechanic={mechanic.MechanicName}");

                continue;
            }

            output.Add(mechanic);
        }
    }

    private void TryExecuteBuildStep(
        Action action,
        string label)
    {
        if (action == null)
            return;

        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            LogBuildException(
                label,
                exception);
        }
    }

    private void LogBuildException(
        string label,
        Exception exception)
    {
        Debug.LogError(
            $"[CharacterBuildController] 빌드 효과 실행 실패 / " +
            $"Owner={GetOwnerName()}, {label}");

        Debug.LogException(exception);
    }

    private static string GetItemName(
        CharacterItem item)
    {
        if (item == null)
            return "NULL_ITEM";

        return string.IsNullOrWhiteSpace(item.ItemName)
            ? item.name
            : item.ItemName;
    }

    private string GetOwnerName()
    {
        return owner?.Data?.CharacterName ??
               owner?.name ??
               "NULL_OWNER";
    }
}