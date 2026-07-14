using System;
using System.Collections.Generic;

// Character가 소유한 런타임 Skill의 BattleEvent 구독 수명을 관리한다.
// 재초기화 전에 UnbindAll을 호출하면 P7 조건부 효과 구독이 중복되지 않는다.
public sealed class CharacterEventBinder
{
    private readonly List<Skill> boundSkills = new();
    private readonly HashSet<Skill> uniqueSkills = new();

    public IReadOnlyList<Skill> BoundSkills =>
        boundSkills;

    public void BindSkills(
        Character owner,
        BattleEvent battleEvent,
        IReadOnlyList<BodyPart> bodyParts,
        IEnumerable<Skill> characterSkills)
    {
        UnbindAll();

        if (owner == null || battleEvent == null)
            return;

        if (bodyParts != null)
        {
            foreach (BodyPart part in bodyParts)
            {
                if (part?.AvailableSkills == null)
                    continue;

                foreach (Skill skill in part.AvailableSkills)
                {
                    BindSkill(
                        skill,
                        owner,
                        battleEvent);
                }
            }
        }

        if (characterSkills == null)
            return;

        foreach (Skill skill in characterSkills)
        {
            BindSkill(
                skill,
                owner,
                battleEvent);
        }
    }

    private void BindSkill(
        Skill skill,
        Character owner,
        BattleEvent battleEvent)
    {
        if (skill == null ||
            !uniqueSkills.Add(skill))
        {
            return;
        }

        try
        {
            skill.Initialize(
                owner,
                battleEvent);

            skill.Register();
            boundSkills.Add(skill);
        }
        catch
        {
            uniqueSkills.Remove(skill);

            try
            {
                skill.Unregister();
            }
            catch
            {
                // 원래 초기화 예외를 보존한다.
            }

            throw;
        }
    }

    public void UnbindAll()
    {
        for (int i = boundSkills.Count - 1;
             i >= 0;
             i--)
        {
            Skill skill = boundSkills[i];

            if (skill == null)
                continue;

            try
            {
                skill.Unregister();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        boundSkills.Clear();
        uniqueSkills.Clear();
    }
}
