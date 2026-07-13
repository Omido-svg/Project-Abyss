using System.Collections.Generic;
using UnityEngine;

public class BattleVisualRequestBuilder
{
    private readonly SkillVisualProfile defaultVisualProfile;

    public BattleVisualRequestBuilder(
        SkillVisualProfile defaultVisualProfile)
    {
        this.defaultVisualProfile = defaultVisualProfile;
    }

    public BattleVisualRequest Build(
        BattleAction action,
        List<ClashRollVisualStep> clashSteps,
        List<int> hitDamages,
        BattleAction opponentAction = null,
        int? targetPartHpBefore = null,
        int? targetPartHpAfter = null)
    {
        if (action == null)
            return null;

        SkillVisualDefinition visualDefinition =
            ResolveVisualDefinition(action);

        BattleVisualRequest request =
            BattleVisualRequest.FromAction(
                action,
                visualDefinition);

        request.OpponentAction =
            opponentAction;

        if (clashSteps != null)
        {
            foreach (ClashRollVisualStep step in clashSteps)
            {
                request.ClashSteps.Add(step);
            }
        }

        if (hitDamages != null)
        {
            foreach (int damage in hitDamages)
            {
                request.HitDamages.Add(damage);
            }
        }

        if (request.HitDamages.Count > 0)
            request.FallbackDamage = request.HitDamages[0];

        ApplyTargetPartHpSnapshot(
            request,
            action,
            targetPartHpBefore,
            targetPartHpAfter);

        ApplyDamageDistribution(request);

        return request;
    }

    private static void ApplyTargetPartHpSnapshot(
        BattleVisualRequest request,
        BattleAction action,
        int? targetPartHpBefore,
        int? targetPartHpAfter)
    {
        if (request == null)
            return;

        if (targetPartHpBefore.HasValue &&
            targetPartHpAfter.HasValue)
        {
            request.HasTargetPartHpSnapshot = true;
            request.TargetPartHpBefore = targetPartHpBefore.Value;
            request.TargetPartHpAfter = targetPartHpAfter.Value;
            return;
        }

        if (action == null ||
            !action.HasDamageLog)
        {
            return;
        }

        request.HasTargetPartHpSnapshot = true;
        request.TargetPartHpBefore = action.LoggedBeforeHP;
        request.TargetPartHpAfter = action.LoggedAfterHP;
    }
    
    private void ApplyDamageDistribution(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        SkillVisualDefinition visual =
            request.VisualDefinition;

        if (visual == null)
            return;

        if (!visual.DistributeDamageByHitCount)
            return;

        if (visual.ExpectedHitFrameCount <= 1)
            return;

        if (request.HitDamages == null ||
            request.HitDamages.Count != 1)
        {
            return;
        }

        int totalDamage =
            request.HitDamages[0];

        request.HitDamages.Clear();

        List<int> distributed =
            DamageDistributionUtility.DistributeIncreasing(
                totalDamage,
                visual.ExpectedHitFrameCount);

        foreach (int damage in distributed)
        {
            request.HitDamages.Add(damage);
        }

        if (request.HitDamages.Count > 0)
            request.FallbackDamage = request.HitDamages[0];
    }

    private SkillVisualDefinition ResolveVisualDefinition(
        BattleAction action)
    {
        if (action == null)
            return null;

        if (action.Skill is IVisualSkill visualSkill &&
            visualSkill.VisualDefinition != null)
        {
            return visualSkill.VisualDefinition;
        }

        if (defaultVisualProfile == null)
            return null;

        if (action.Skill == null)
            return null;

        return defaultVisualProfile.GetDefault(
            action.Skill.ActionType);
    }
}
