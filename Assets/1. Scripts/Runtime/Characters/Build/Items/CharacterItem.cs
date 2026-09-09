using System.Collections.Generic;
using UnityEngine;

public abstract class CharacterItem : ScriptableObject
{
    [SerializeField] private string itemName;
    [TextArea]
    [SerializeField] private string description;

    public string ItemName => itemName;
    public string Description => description;

    /// <summary>
    /// 이 아이템이 현재 Character에 적용 가능한지 판단한다.
    /// 기존 CharacterItem은 owner가 존재하면 그대로 적용된다.
    /// </summary>
    public virtual bool CanApplyTo(
        Character owner)
    {
        return owner != null;
    }

    /// <summary>
    /// 여러 CombatMechanic을 생성할 수 있는 정식 확장점.
    /// 기본 구현은 기존 CreateMechanic()을 어댑트하므로
    /// 기존 아이템의 게임 규칙을 변경하지 않는다.
    /// </summary>
    public virtual void CreateMechanics(
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        if (output == null)
            return;

        CombatMechanic mechanic =
            CreateMechanic();

        if (mechanic != null)
            output.Add(mechanic);
    }

    //--------------------------------
    // 스킬 목록을 바꾸고 싶을 때 사용
    //--------------------------------

    public virtual void ModifySkills(
        Character owner,
        BodyPart part,
        List<Skill> skills)
    {
    }

    /// <summary>
    /// 스킬 장착 상한을 아이템이 수정하는 확장점.
    /// 기본값(일반/결투/도사림/위세 = 3/3/3/1)에 가감 또는 교체 규칙을 적용할 수 있다.
    /// </summary>
    public virtual int ModifySkillEquipLimit(
        Character owner,
        ActionType actionType,
        int currentLimit)
    {
        return currentLimit;
    }

    //--------------------------------
    // 스탯도 조금 바꾸는 아이템이면 사용
    //--------------------------------

    public virtual void ModifyStatus(
        Character owner,
        CurrentStatus status)
    {
    }
    
    public virtual void ModifyBodyPart(
        Character owner,
        BodyPart part)
    {
    }

    //--------------------------------
    // 전투 중 이벤트형 효과가 필요하면
    // CombatMechanic을 생성해서 반환
    //--------------------------------

    public virtual CombatMechanic CreateMechanic()
    {
        return null;
    }
}