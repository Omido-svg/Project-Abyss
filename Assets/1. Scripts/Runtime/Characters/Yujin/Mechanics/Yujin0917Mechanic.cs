using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0917 신규 유진 결투 O/P의 전투 단위 상태.
/// P(끝장을 보다)의 실제 지불 비용 4→3→2→1은 전투 동안 누적되고,
/// 위력 계산용 빛은 YujinRuntimeSkills에서 항상 2로 보정한다.
/// </summary>
public sealed class Yujin0917Mechanic : CombatMechanic
{
    private readonly Dictionary<string, int> committedUses =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public override string MechanicName => "Yujin 0917 O/P";

    public override void OnUnregister()
    {
        committedUses.Clear();
    }

    public int ResolveEnergyCost(
        SkillDefinition definition,
        int baseCost)
    {
        int safeBase = Mathf.Max(0, baseCost);

        if (definition == null ||
            !string.Equals(
                definition.SkillId,
                YujinSkillIds.FinishIt,
                StringComparison.Ordinal))
        {
            return safeBase;
        }

        SkillRulebreakerSettings rule = definition.Rulebreaker;

        if (rule?.HasDynamicCost != true)
            return safeBase;

        int used = GetCommittedUseCount(definition);
        int reduction =
            used *
            Mathf.Max(
                0,
                rule.EnergyCostReductionPerCommittedUse);

        return Mathf.Max(
            rule.ResolveMinimumEnergyCost(),
            safeBase - reduction);
    }

    public void RecordCommittedUse(
        SkillDefinition definition)
    {
        if (definition == null ||
            !string.Equals(
                definition.SkillId,
                YujinSkillIds.FinishIt,
                StringComparison.Ordinal))
        {
            return;
        }

        string key = definition.SkillId;
        committedUses[key] =
            GetCommittedUseCount(definition) + 1;
    }

    public void RollbackCommittedUse(
        SkillDefinition definition)
    {
        if (definition == null ||
            !string.Equals(
                definition.SkillId,
                YujinSkillIds.FinishIt,
                StringComparison.Ordinal))
        {
            return;
        }

        string key = definition.SkillId;
        int current = GetCommittedUseCount(definition);

        if (current <= 1)
            committedUses.Remove(key);
        else
            committedUses[key] = current - 1;
    }

    public int GetCommittedUseCount(
        SkillDefinition definition)
    {
        string key = definition?.SkillId;

        return
            !string.IsNullOrWhiteSpace(key) &&
            committedUses.TryGetValue(
                key,
                out int value)
                ? Mathf.Max(0, value)
                : 0;
    }
}
