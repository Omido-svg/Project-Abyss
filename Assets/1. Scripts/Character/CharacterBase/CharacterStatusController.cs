using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterStatusController
{
    private readonly Character owner;

    private readonly List<StatusEffect> characterStatuses =
        new List<StatusEffect>();

    public IReadOnlyList<StatusEffect> CharacterStatuses =>
        characterStatuses;

    public CharacterStatusController(Character owner)
    {
        this.owner = owner;
    }

    //--------------------------------------------------
    // 캐릭터 상태이상 추가
    //--------------------------------------------------

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        if (owner == null)
            return;

        if (effect == null)
            return;

        StatusEffect existing =
            FindSameStatus(effect);

        if (existing != null)
        {
            existing.Merge(effect);
            return;
        }

        effect.Initialize(
            owner,
            source,
            null);

        effect.OnApply();

        characterStatuses.Add(effect);

        owner.BattleEvent?.RaiseStatusApplied(
            owner,
            effect);

        Debug.Log(
            $"{owner.Data.CharacterName}에게 {effect.Name} 상태 부여");
    }

    //--------------------------------------------------
    // 부위 상태이상 추가
    //--------------------------------------------------

    public void AddPartStatus(
        BodyPart part,
        StatusEffect effect,
        Character source)
    {
        if (owner == null)
            return;

        if (effect == null)
            return;

        if (part == null || part.IsBroken)
        {
            AddStatus(
                effect,
                source);

            return;
        }

        StatusEffect existing =
            FindSamePartStatus(
                part,
                effect);

        if (existing != null)
        {
            existing.Merge(effect);
            return;
        }

        effect.Initialize(
            owner,
            source,
            part);

        effect.OnApply();

        part.AddStatus(effect);

        owner.BattleEvent?.RaiseBodyPartStatusApplied(
            owner,
            part,
            effect);

        Debug.Log(
            $"{owner.Data.CharacterName} {part.Type} 부위에 {effect.Name} 상태 부여");
    }

    //--------------------------------------------------
    // 캐릭터 상태 제거
    //--------------------------------------------------

    public void RemoveStatus(StatusEffect effect)
    {
        if (effect == null)
            return;

        if (!characterStatuses.Contains(effect))
            return;

        effect.OnRemove();

        characterStatuses.Remove(effect);

        owner.BattleEvent?.RaiseStatusRemoved(
            owner,
            effect);

        Debug.Log(
            $"{owner.Data.CharacterName}의 {effect.Name} 상태 제거");
    }

    //--------------------------------------------------
    // 부위 상태 제거
    //--------------------------------------------------

    public void RemovePartStatus(
        BodyPart part,
        StatusEffect effect)
    {
        if (part == null)
            return;

        if (effect == null)
            return;

        part.RemoveStatus(effect);

        owner.BattleEvent?.RaiseBodyPartStatusRemoved(
            owner,
            part,
            effect);

        Debug.Log(
            $"{owner.Data.CharacterName} {part.Type}의 {effect.Name} 상태 제거");
    }

    //--------------------------------------------------
    // 부위 파괴 시 상태이상 이전
    //--------------------------------------------------

    public void TransferPartStatusesToCharacter(BodyPart part)
    {
        if (owner == null)
            return;

        if (part == null)
            return;

        foreach (StatusEffect effect in part.StatusEffects.ToArray())
        {
            if (effect == null)
                continue;

            Character source =
                effect.Source != null
                    ? effect.Source
                    : owner;

            part.RemoveStatus(effect);

            owner.BattleEvent?.RaiseBodyPartStatusRemoved(
                owner,
                part,
                effect);

            AddStatus(
                effect,
                source);

            Debug.Log(
                $"{owner.Data.CharacterName} {part.Type}의 {effect.Name} 상태가 캐릭터 상태로 이전됨");
        }
    }

    //--------------------------------------------------
    // 턴 종료 상태 처리
    //--------------------------------------------------

    public void OnTurnEnd()
    {
        if (owner == null)
            return;

        foreach (StatusEffect effect in characterStatuses.ToArray())
        {
            if (effect == null)
                continue;

            effect.OnTurnEnd();

            if (effect.IsExpired)
            {
                RemoveStatus(effect);
            }
        }

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            foreach (StatusEffect effect in part.StatusEffects.ToArray())
            {
                if (effect == null)
                    continue;

                effect.OnTurnEnd();

                if (effect.IsExpired)
                {
                    RemovePartStatus(
                        part,
                        effect);
                }
            }
        }
    }

    //--------------------------------------------------
    // 조회
    //--------------------------------------------------

    public T GetStatus<T>() where T : StatusEffect
    {
        foreach (StatusEffect effect in characterStatuses)
        {
            if (effect is T typedEffect)
                return typedEffect;
        }

        return null;
    }

    public T GetPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        if (part == null)
            return null;

        foreach (StatusEffect effect in part.StatusEffects)
        {
            if (effect is T typedEffect)
                return typedEffect;
        }

        return null;
    }

    public bool HasStatus<T>() where T : StatusEffect
    {
        return GetStatus<T>() != null;
    }

    public bool HasPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        return GetPartStatus<T>(part) != null;
    }

    //--------------------------------------------------
    // 내부 검색
    //--------------------------------------------------

    private StatusEffect FindSameStatus(StatusEffect effect)
    {
        if (effect == null)
            return null;

        foreach (StatusEffect existing in characterStatuses)
        {
            if (existing == null)
                continue;

            if (existing.GetType() == effect.GetType())
                return existing;
        }

        return null;
    }

    private StatusEffect FindSamePartStatus(
        BodyPart part,
        StatusEffect effect)
    {
        if (part == null)
            return null;

        if (effect == null)
            return null;

        foreach (StatusEffect existing in part.StatusEffects)
        {
            if (existing == null)
                continue;

            if (existing.GetType() == effect.GetType())
                return existing;
        }

        return null;
    }
}