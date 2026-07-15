using System.Collections.Generic;
using UnityEngine;

public abstract class CharacterItem : ScriptableObject
{
    [SerializeField] private string itemName;
    [TextArea]
    [SerializeField] private string description;

    public string ItemName => itemName;
    public string Description => description;

    //--------------------------------
    // 스킬 목록을 바꾸고 싶을 때 사용
    //--------------------------------

    public virtual void ModifySkills(
        Character owner,
        BodyPart part,
        List<Skill> skills)
    {
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