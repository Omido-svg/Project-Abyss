using System.Collections.Generic;

public class BattleVisualRequestBuilder
{
    private readonly SkillVisualProfile
        defaultVisualProfile;

    public BattleVisualRequestBuilder(
        SkillVisualProfile defaultVisualProfile)
    {
        this.defaultVisualProfile =
            defaultVisualProfile;
    }

    public BattleVisualRequest Build(
        BattleAction action,
        List<ClashRollVisualStep> clashSteps,
        List<int> hitDamages,
        BattleAction opponentAction = null,
        int? targetPartHpBefore = null,
        int? targetPartHpAfter = null,
        DamageContext damageContext = null)
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
            request.ClashSteps.AddRange(
                clashSteps);
        }

        DamageContext sourceContext =
            damageContext ??
            action.LastDamageContext;

        request.ApplyDamageContext(
            sourceContext);

        ApplyHitDamages(
            request,
            hitDamages,
            sourceContext);

        // 구버전 호출부를 위한 폴백이다.
        // DamageContext가 있으면 이 값은 사용하지 않는다.
        if (sourceContext == null)
        {
            ApplyLegacySnapshots(
                request,
                action,
                targetPartHpBefore,
                targetPartHpAfter);
        }

        ApplyDamageDistribution(
            request);

        return request;
    }

    private static void ApplyHitDamages(
        BattleVisualRequest request,
        List<int> hitDamages,
        DamageContext context)
    {
        if (request == null)
            return;

        if (hitDamages != null)
        {
            foreach (int damage in hitDamages)
            {
                if (damage > 0)
                    request.HitDamages.Add(damage);
            }
        }

        if (request.HitDamages.Count == 0)
        {
            int resolvedDamage =
                context?.GetDisplayDamage() ?? 0;

            if (resolvedDamage > 0)
            {
                request.HitDamages.Add(
                    resolvedDamage);
            }
        }

        if (request.HitDamages.Count > 0)
        {
            request.FallbackDamage =
                request.HitDamages[0];
        }
    }

    private static void ApplyLegacySnapshots(
        BattleVisualRequest request,
        BattleAction action,
        int? targetPartHpBefore,
        int? targetPartHpAfter)
    {
        if (request == null ||
            action == null)
        {
            return;
        }

        if (action.TargetPart != null &&
            targetPartHpBefore.HasValue &&
            targetPartHpAfter.HasValue)
        {
            request.HasTargetPartHpSnapshot =
                true;

            request.TargetPartHpBefore =
                targetPartHpBefore.Value;

            request.TargetPartHpAfter =
                targetPartHpAfter.Value;
        }

        if (!action.HasDamageLog)
            return;

        if (action.TargetPart != null)
        {
            request.HasTargetPartHpSnapshot =
                true;

            request.TargetPartHpBefore =
                action.LoggedBeforeHP;

            request.TargetPartHpAfter =
                action.LoggedAfterHP;

            return;
        }

        request.HasTargetCharacterHpSnapshot =
            true;

        request.TargetCharacterHpBefore =
            action.LoggedBeforeHP;

        request.TargetCharacterHpAfter =
            action.LoggedAfterHP;

        request.TargetCharacterMaxHp =
            action.Target?.MaxCombatHP ?? 0;
    }

    private void ApplyDamageDistribution(
        BattleVisualRequest request)
    {
        if (request?.VisualDefinition == null)
            return;

        SkillVisualDefinition visual =
            request.VisualDefinition;

        if (!visual.DistributeDamageByHitCount ||
            visual.ExpectedHitFrameCount <= 1 ||
            request.HitDamages == null ||
            request.HitDamages.Count != 1)
        {
            return;
        }

        int totalDamage =
            request.HitDamages[0];

        request.HitDamages.Clear();

        request.HitDamages.AddRange(
            DamageDistributionUtility
                .DistributeIncreasing(
                    totalDamage,
                    visual.ExpectedHitFrameCount));

        if (request.HitDamages.Count > 0)
        {
            request.FallbackDamage =
                request.HitDamages[0];
        }
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

        if (defaultVisualProfile == null ||
            action.Skill == null)
        {
            return null;
        }

        return defaultVisualProfile.GetDefault(
            action.Skill.ActionType);
    }
}
