using System.Collections.Generic;

public class CharacterBuildController
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
        this.equippedItems = equippedItems;
        this.equippedAugments = equippedAugments;
    }

    //------------------------------------------------
    // 아이템 추가
    //------------------------------------------------

    public void AddItem(CharacterItem item)
    {
        if (item == null)
            return;

        if (equippedItems == null)
            return;

        if (equippedItems.Contains(item))
            return;

        equippedItems.Add(item);
    }

    //------------------------------------------------
    // 증강 추가
    //------------------------------------------------

    public void AddAugment(CharacterAugment augment)
    {
        if (augment == null)
            return;

        if (equippedAugments == null)
            return;

        if (equippedAugments.Contains(augment))
            return;

        equippedAugments.Add(augment);
    }

    //------------------------------------------------
    // 아이템이 스킬 목록 수정
    //------------------------------------------------

    public void ApplyItemSkillModifiers(
        IReadOnlyList<BodyPart> bodyParts)
    {
        if (owner == null)
            return;

        if (bodyParts == null)
            return;

        foreach (BodyPart part in bodyParts)
        {
            if (part == null)
                continue;

            List<Skill> skills =
                new List<Skill>(part.AvailableSkills);

            foreach (CharacterItem item in equippedItems)
            {
                if (item == null)
                    continue;

                item.ModifySkills(
                    owner,
                    part,
                    skills);
            }

            part.ReplaceSkills(skills);
        }
    }

    //------------------------------------------------
    // 아이템 / 증강이 캐릭터 스탯 수정
    //------------------------------------------------

    public void ApplyStatusModifiers(
        CurrentStatus currentStatus)
    {
        if (owner == null)
            return;

        if (currentStatus == null)
            return;

        foreach (CharacterItem item in equippedItems)
        {
            if (item == null)
                continue;

            item.ModifyStatus(
                owner,
                currentStatus);
        }

        foreach (CharacterAugment augment in equippedAugments)
        {
            if (augment == null)
                continue;

            augment.ModifyStatus(
                owner,
                currentStatus);
        }
    }

    //------------------------------------------------
    // 아이템 / 증강이 부위 수정
    //------------------------------------------------

    public void ApplyBodyPartModifiers(
        IReadOnlyList<BodyPart> bodyParts)
    {
        if (owner == null)
            return;

        if (bodyParts == null)
            return;

        foreach (BodyPart part in bodyParts)
        {
            if (part == null)
                continue;

            foreach (CharacterAugment augment in equippedAugments)
            {
                if (augment == null)
                    continue;

                augment.ModifyBodyPart(
                    owner,
                    part);
            }

            foreach (CharacterItem item in equippedItems)
            {
                if (item == null)
                    continue;

                item.ModifyBodyPart(
                    owner,
                    part);
            }
        }
    }

    //------------------------------------------------
    // 아이템 / 증강 메커닉 생성
    //------------------------------------------------

    public List<CombatMechanic> CreateMechanics()
    {
        List<CombatMechanic> result =
            new List<CombatMechanic>();

        foreach (CharacterItem item in equippedItems)
        {
            if (item == null)
                continue;

            CombatMechanic mechanic =
                item.CreateMechanic();

            if (mechanic == null)
                continue;

            result.Add(mechanic);
        }

        foreach (CharacterAugment augment in equippedAugments)
        {
            if (augment == null)
                continue;

            CombatMechanic mechanic =
                augment.CreateMechanic();

            if (mechanic == null)
                continue;

            result.Add(mechanic);
        }

        return result;
    }
}