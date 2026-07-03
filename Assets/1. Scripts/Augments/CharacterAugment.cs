using UnityEngine;

public abstract class CharacterAugment : ScriptableObject
{
    [SerializeField] private string augmentName;
    [TextArea]
    [SerializeField] private string description;

    public string AugmentName => augmentName;
    public string Description => description;

    //--------------------------------
    // 기본 역할: 스탯 수정
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
    // 특수한 증강은 메커닉도 줄 수 있음
    //--------------------------------

    public virtual CombatMechanic CreateMechanic()
    {
        return null;
    }
}