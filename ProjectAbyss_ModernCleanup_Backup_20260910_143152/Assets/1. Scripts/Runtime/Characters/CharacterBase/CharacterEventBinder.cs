using System;
using System.Collections.Generic;

/// <summary>
/// Character가 소유한 런타임 Skill의 초기화와 BattleEvent 구독 수명을 분리해 관리한다.
/// 후보 Skill은 owner/battleEvent 참조를 유지할 수 있지만, 실제 전역 이벤트 구독은
/// 현재 장착/페이즈에서 활성인 Skill만 가진다.
/// </summary>
public sealed class CharacterEventBinder
{
    private readonly List<Skill> initializedSkills = new();
    private readonly HashSet<Skill> initializedSet = new();
    private readonly List<Skill> boundSkills = new();
    private readonly HashSet<Skill> boundSet = new();

    private Func<Skill, bool> activationPredicate;

    public IReadOnlyList<Skill> BoundSkills =>
        boundSkills;

    public void BindSkills(
        Character owner,
        BattleEvent battleEvent,
        IEnumerable<Skill> characterSkills,
        Func<Skill, bool> shouldActivate = null)
    {
        UnbindAll();

        if (owner == null || battleEvent == null)
            return;

        activationPredicate = shouldActivate;

        if (characterSkills != null)
        {
            foreach (Skill skill in characterSkills)
            {
                InitializeSkill(
                    skill,
                    owner,
                    battleEvent);
            }
        }

        RefreshActiveBindings(
            throwOnError: true);
    }

    /// <summary>
    /// 장착 변경/보스 페이즈 전환 뒤 활성 집합의 차이만 구독/해제한다.
    /// 후보 Skill 객체를 다시 생성하거나 Initialize하지 않는다.
    /// </summary>
    public void RefreshActiveBindings()
    {
        RefreshActiveBindings(
            throwOnError: false);
    }

    private void InitializeSkill(
        Skill skill,
        Character owner,
        BattleEvent battleEvent)
    {
        if (skill == null ||
            !initializedSet.Add(skill))
        {
            return;
        }

        try
        {
            skill.Initialize(
                owner,
                battleEvent);

            initializedSkills.Add(skill);
        }
        catch
        {
            initializedSet.Remove(skill);

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

    private void RefreshActiveBindings(
        bool throwOnError)
    {
        for (int i = boundSkills.Count - 1;
             i >= 0;
             i--)
        {
            Skill skill = boundSkills[i];

            if (skill != null &&
                ShouldActivate(skill))
            {
                continue;
            }

            try
            {
                skill?.Unregister();
            }
            catch (Exception exception)
            {
                if (throwOnError)
                    throw;

                UnityEngine.Debug.LogException(exception);
            }
            finally
            {
                boundSkills.RemoveAt(i);
                if (skill != null)
                    boundSet.Remove(skill);
            }
        }

        foreach (Skill skill in initializedSkills)
        {
            if (skill == null ||
                boundSet.Contains(skill) ||
                !ShouldActivate(skill))
            {
                continue;
            }

            try
            {
                skill.Register();
                boundSet.Add(skill);
                boundSkills.Add(skill);
            }
            catch (Exception exception)
            {
                try
                {
                    skill.Unregister();
                }
                catch
                {
                    // 최초 구독 예외를 보존한다.
                }

                if (throwOnError)
                    throw;

                UnityEngine.Debug.LogException(exception);
            }
        }
    }

    private bool ShouldActivate(
        Skill skill)
    {
        return skill != null &&
               (activationPredicate == null ||
                activationPredicate(skill));
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
        boundSet.Clear();
        initializedSkills.Clear();
        initializedSet.Clear();
        activationPredicate = null;
    }
}