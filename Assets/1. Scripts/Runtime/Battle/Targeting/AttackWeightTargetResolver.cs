using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 가중치의 추가 타깃을 실제 행동 단위로 한 번만 결정한다.
/// 메인 타깃은 기존 ActionSlot 선택을 그대로 사용하고,
/// 추가 타깃은 메인 타깃과 같은 진영의 살아 있는 캐릭터 중 무작위로 고른다.
/// </summary>
public sealed class AttackWeightTargetResolver
{
    private readonly BattleContext battleContext;

    public AttackWeightTargetResolver(
        BattleContext battleContext)
    {
        this.battleContext =
            battleContext;
    }

    public IReadOnlyList<AttackWeightTarget>
        ResolveTargets(
            BattleAction action)
    {
        if (action == null)
            return Array.Empty<AttackWeightTarget>();

        if (action.HasResolvedAttackWeightTargets)
            return action.AttackWeightTargets;

        List<AttackWeightTarget> resolved =
            new List<AttackWeightTarget>();

        if (action.Target == null)
        {
            action.SetAttackWeightTargets(
                resolved);

            return action.AttackWeightTargets;
        }

        resolved.Add(
            new AttackWeightTarget(
                0,
                action.Target,
                action.TargetPart,
                isPrimary: true));

        int requestedWeight =
            Mathf.Max(
                1,
                action.Skill?.AttackWeight ?? 1);

        if (requestedWeight <= 1)
        {
            action.SetAttackWeightTargets(
                resolved);

            return action.AttackWeightTargets;
        }

        List<Character> candidates =
            BuildSameSideCandidatePool(
                action);

        Shuffle(
            candidates);

        int requiredSecondaryCount =
            requestedWeight - 1;

        foreach (Character candidate
                 in candidates)
        {
            if (resolved.Count - 1 >=
                requiredSecondaryCount)
            {
                break;
            }

            TargetPoint point =
                ResolveSecondaryTargetPoint(
                    action,
                    candidate);

            if (!point.IsValid)
                continue;

            resolved.Add(
                new AttackWeightTarget(
                    resolved.Count,
                    point.Character,
                    point.Part,
                    isPrimary: false));
        }

        action.SetAttackWeightTargets(
            resolved);

        return action.AttackWeightTargets;
    }

    private List<Character> BuildSameSideCandidatePool(
        BattleAction action)
    {
        List<Character> candidates =
            new List<Character>();

        Character primaryTarget =
            action?.Target;

        if (primaryTarget == null ||
            battleContext == null)
        {
            return candidates;
        }

        IReadOnlyList<Character> roster =
            battleContext.AllCharacters;

        if (roster == null ||
            !ContainsCharacter(
                roster,
                primaryTarget))
        {
            // 전투 명단 밖 대상을 자동 확산 대상으로 해석하지 않는다.
            return candidates;
        }

        bool primaryIsEnemySide =
            IsEnemySide(
                primaryTarget);

        foreach (Character candidate
                 in roster)
        {
            if (candidate == null ||
                IsEnemySide(candidate) !=
                primaryIsEnemySide)
            {
                continue;
            }

            AddCandidate(
                candidates,
                action,
                candidate);
        }

        // 현재 BattleContext는 Player 1명 + Enemies 구조지만,
        // 향후 AllCharacters에 아군 파티가 포함되면 이 코드 변경 없이
        // 같은 플레이어 진영의 추가 타깃을 선택할 수 있다.
        return candidates;
    }

    private bool IsEnemySide(
        Character character)
    {
        return
            character != null &&
            battleContext?.Enemies != null &&
            battleContext.Enemies.Contains(
                character);
    }

    private static bool ContainsCharacter(
        IReadOnlyList<Character> roster,
        Character target)
    {
        if (roster == null ||
            target == null)
        {
            return false;
        }

        foreach (Character character
                 in roster)
        {
            if (character == target)
                return true;
        }

        return false;
    }

    private static void AddCandidate(
        ICollection<Character> candidates,
        BattleAction action,
        Character candidate)
    {
        if (candidates == null ||
            candidate == null ||
            action == null)
        {
            return;
        }

        if (candidate == action.Owner ||
            candidate == action.Target ||
            candidate.IsDead ||
            candidates.Contains(candidate))
        {
            return;
        }

        candidates.Add(
            candidate);
    }

    private static TargetPoint
        ResolveSecondaryTargetPoint(
            BattleAction action,
            Character candidate)
    {
        if (action?.Skill == null ||
            candidate == null ||
            candidate.IsDead)
        {
            return default;
        }

        AttackWeightSecondaryPartMode mode =
            action.Skill
                .SecondaryAttackWeightPartMode;

        if (mode ==
            AttackWeightSecondaryPartMode
                .CharacterLevelDirect)
        {
            return TargetPoint.ForCharacter(
                candidate);
        }

        TargetSelectionRule rule =
            new TargetSelectionRule(
                action.Skill
                    .AllowBrokenAttackWeightParts);

        IReadOnlyList<TargetPoint> points =
            BattleTargetValidator.GetTargetPoints(
                candidate,
                rule);

        if (points == null ||
            points.Count == 0)
        {
            return default;
        }

        if (mode ==
                AttackWeightSecondaryPartMode
                    .MatchPrimaryPartType &&
            action.TargetPart != null)
        {
            foreach (TargetPoint point
                     in points)
            {
                if (point.IsValid &&
                    point.Part != null &&
                    point.Part.Type ==
                    action.TargetPart.Type)
                {
                    return point;
                }
            }
        }

        List<TargetPoint> validPoints =
            new List<TargetPoint>();

        foreach (TargetPoint point
                 in points)
        {
            if (point.IsValid)
            {
                validPoints.Add(
                    point);
            }
        }

        if (validPoints.Count == 0)
            return default;

        return validPoints[
            UnityEngine.Random.Range(
                0,
                validPoints.Count)];
    }

    private static void Shuffle<T>(
        IList<T> list)
    {
        if (list == null)
            return;

        for (int index = list.Count - 1;
             index > 0;
             index--)
        {
            int swapIndex =
                UnityEngine.Random.Range(
                    0,
                    index + 1);

            T temporary =
                list[index];

            list[index] =
                list[swapIndex];

            list[swapIndex] =
                temporary;
        }
    }
}
