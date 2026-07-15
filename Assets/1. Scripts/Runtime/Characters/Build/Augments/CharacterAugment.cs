using System.Collections.Generic;
using UnityEngine;

public abstract class CharacterAugment : ScriptableObject
{
    [SerializeField] private string augmentName;

    [TextArea]
    [SerializeField] private string description;

    public string AugmentName =>
        string.IsNullOrWhiteSpace(augmentName)
            ? name
            : augmentName;

    public string Description => description;

    /// <summary>
    /// 캐릭터별 장착 제한이 필요한 증강이 오버라이드한다.
    /// </summary>
    public virtual bool CanApplyTo(Character owner)
    {
        return owner != null;
    }

    //--------------------------------
    // 초기 빌드 수정
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
    // 런타임 조건부 효과
    //--------------------------------

    /// <summary>
    /// 기존 호출부 호환용 단일 메커닉 팩토리.
    /// 새로운 증강은 CreateMechanics를 오버라이드해서 여러 메커닉을 만들 수 있다.
    /// 반드시 매 호출마다 새로운 CombatMechanic 인스턴스를 반환해야 한다.
    /// </summary>
    public virtual CombatMechanic CreateMechanic()
    {
        return null;
    }

    /// <summary>
    /// 조건부 증강 효과를 CombatMechanic으로 연결하는 표준 진입점.
    /// ScriptableObject 자체에는 런타임 스택/턴/ActionId를 저장하지 않는다.
    /// </summary>
    public virtual void CreateMechanics(
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        if (output == null ||
            !context.IsValid ||
            !CanApplyTo(context.Owner))
        {
            return;
        }

        CombatMechanic mechanic =
            CreateMechanic();

        if (mechanic != null)
            output.Add(mechanic);
    }
}
