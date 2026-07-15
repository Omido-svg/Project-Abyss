using System.Collections.Generic;

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
        int? targetPartHpAfter = null,
        DamageContext damageContext = null)
    {
        if (action == null)
            return null;

        SkillVisualDefinition visualDefinition =
            ResolveVisualDefinition(action);

        BattleVisualRequest request =
            BattleVisualRequest.FromAction(action, visualDefinition);

        request.OpponentAction = opponentAction;

        if (clashSteps != null)
        {
            for (int i = 0; i < clashSteps.Count; i++)
            {
                ClashRollVisualStep step = clashSteps[i];
                step.RoundIndex = i;
                request.ClashSteps.Add(step);
            }
        }

        DamageContext sourceContext =
            damageContext ?? action.LastDamageContext;

        request.ApplyDamageContext(sourceContext);
        ApplyHitDamages(request, hitDamages, sourceContext);

        if (sourceContext == null)
        {
            ApplyLegacySnapshots(
                request,
                action,
                targetPartHpBefore,
                targetPartHpAfter);
        }

        ApplyDamageDistribution(request);
        ResolveWorldPosition(request);

        BattleVisualValidator.ValidateRequest(
            request,
            logWarnings: true);

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
                request.HitDamages.Add(System.Math.Max(0, damage));
        }

        if (request.HitDamages.Count == 0)
        {
            int resolvedDamage = context?.GetDisplayDamage() ?? 0;

            if (resolvedDamage > 0)
                request.HitDamages.Add(resolvedDamage);
        }

        request.FallbackDamage =
            request.HitDamages.Count > 0
                ? request.HitDamages[0]
                : 0;
    }

    private static void ApplyLegacySnapshots(
        BattleVisualRequest request,
        BattleAction action,
        int? targetPartHpBefore,
        int? targetPartHpAfter)
    {
        if (request == null || action == null)
            return;

        if (action.TargetPart != null &&
            targetPartHpBefore.HasValue &&
            targetPartHpAfter.HasValue)
        {
            request.HasTargetPartHpSnapshot = true;
            request.TargetPartHpBefore = targetPartHpBefore.Value;
            request.TargetPartHpAfter = targetPartHpAfter.Value;
        }

        if (!action.HasDamageLog)
            return;

        if (action.TargetPart != null)
        {
            request.HasTargetPartHpSnapshot = true;
            request.TargetPartHpBefore = action.LoggedBeforeHP;
            request.TargetPartHpAfter = action.LoggedAfterHP;
            return;
        }

        request.HasTargetCharacterHpSnapshot = true;
        request.TargetCharacterHpBefore = action.LoggedBeforeHP;
        request.TargetCharacterHpAfter = action.LoggedAfterHP;
        request.TargetCharacterMaxHp = action.Target?.MaxCombatHP ?? 0;
    }

    private static void ApplyDamageDistribution(
        BattleVisualRequest request)
    {
        if (request?.VisualDefinition == null)
            return;

        SkillVisualDefinition visual = request.VisualDefinition;

        if (!visual.DistributeDamageByHitCount ||
            request.HitDamages == null ||
            request.HitDamages.Count != 1)
        {
            return;
        }

        int expectedCount = System.Math.Max(
            1,
            visual.ExpectedHitFrameCount);

        IReadOnlyList<int> weights = visual.HitDamageWeights;
        int totalDamage = request.HitDamages[0];

        request.HitDamages.Clear();
        request.HitDamages.AddRange(
            DamageDistributionUtility.DistributeByWeights(
                totalDamage,
                weights,
                expectedCount));

        request.FallbackDamage =
            request.HitDamages.Count > 0
                ? request.HitDamages[0]
                : 0;
    }

    private static void ResolveWorldPosition(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        CharacterView targetView =
            BattleCameraTargetResolver.GetView(request.Target);

        UnityEngine.Transform anchor =
            BattleCameraTargetResolver.GetTargetPartAnchor(
                request.Target,
                request.TargetPart,
                targetView);

        if (anchor == null && request.Attacker != null)
        {
            CharacterView attackerView =
                BattleCameraTargetResolver.GetView(request.Attacker);

            anchor = BattleCameraTargetResolver.GetTargetPartAnchor(
                request.Attacker,
                request.AttackerPart,
                attackerView);
        }

        if (anchor == null)
            return;

        request.HasWorldPosition = true;
        request.WorldPosition = anchor.position;
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

        if (defaultVisualProfile == null || action.Skill == null)
            return null;

        return defaultVisualProfile.GetDefault(action.Skill.ActionType);
    }
}
