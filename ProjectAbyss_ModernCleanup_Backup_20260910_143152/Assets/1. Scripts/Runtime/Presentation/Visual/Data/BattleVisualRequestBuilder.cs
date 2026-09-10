using UnityEngine;
using System.Collections.Generic;

public class BattleVisualRequestBuilder
{
public BattleVisualRequest Build(
        BattleAction action,
        List<ClashRollVisualStep> clashSteps,
        List<int> hitDamages,
        BattleAction opponentAction = null,
        int? targetPartHpBefore = null,
        int? targetPartHpAfter = null,
        DamageContext damageContext = null,
        bool useActionDamageContextFallback = true)
    {
        if (action == null)
            return null;

        SkillVisualDefinition visualDefinition =
            ResolveVisualDefinition(action);

        if (visualDefinition == null ||
            !visualDefinition.HasTimelineCutscene)
        {
            Debug.LogError(
                "[BattleVisualRequestBuilder] Timeline-only 요청 생성을 중단합니다. " +
                $"Skill={action.Skill?.SkillName ?? "NULL"}");
            return null;
        }

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
            damageContext ??
            (useActionDamageContextFallback
                ? action.PrimaryDamageContext
                : null);

        request.ApplyDamageContext(sourceContext);
        ApplyHitDamages(request, hitDamages, sourceContext);

        // Standalone 행동은 Action에 이미 적용된 Attack Weight 결과가 남아 있다.
        // Clash exchange는 명시적인 damageContext를 넘기므로 여기서 전체 Action의
        // 다른 교환 결과를 섞지 않는다.
        if (damageContext == null &&
            useActionDamageContextFallback)
        {
            CopyStandaloneSecondaryDamageContexts(
                request,
                action,
                sourceContext);
        }

        if (sourceContext == null &&
            useActionDamageContextFallback)
        {
            ApplyLegacySnapshots(
                request,
                action,
                targetPartHpBefore,
                targetPartHpAfter);
        }

        ApplyDamageDistribution(request);
        NormalizeTargetImpacts(request);
        ResolveWorldPosition(request);

        BattleVisualValidator.ValidateRequest(
            request,
            logWarnings: true);

        return request;
    }


    public BattleVisualRequest BuildClashSequence(
        ClashResultContext result)
    {
        BattleAction first = result?.FirstAction;
        BattleAction second = result?.SecondAction;

        if (first == null)
            return null;

        SkillVisualDefinition sequenceVisual =
            ResolveVisualDefinition(first) ??
            ResolveVisualDefinition(second);

        if (sequenceVisual == null ||
            !sequenceVisual.HasTimelineCutscene)
        {
            Debug.LogError(
                "[BattleVisualRequestBuilder] 합 Timeline 요청 생성을 중단합니다. " +
                $"First={first.Skill?.SkillName ?? "NULL"}, " +
                $"Second={second?.Skill?.SkillName ?? "NULL"}");
            return null;
        }

        BattleVisualRequest request =
            BattleVisualRequest.FromAction(
                first,
                sequenceVisual);

        request.OpponentAction = second;
        request.IsClashSequence = true;
        request.HasMomentumTimeline = true;
        request.MomentumAtSequenceStart =
            result.MomentumAtStart;
        request.MomentumAfterSequence =
            result.MomentumAfterResolution;

        // 합은 두 참가자를, 일방 공격은 실제 공격 대상을 전체 시퀀스 동안 고정한다.
        request.Attacker = first.Owner;
        request.AttackerPart = first.OwnerPart;
        request.Target = second != null
            ? second.Owner
            : first.Target;
        request.TargetPart = first.TargetPart ?? second?.OwnerPart;
        request.TargetPoint = new TargetPoint(
            request.Target,
            request.TargetPart);

        if (result.Exchanges != null)
        {
            foreach (ClashExchangeResult exchange
                     in result.Exchanges)
            {
                if (exchange == null)
                    continue;

                ClashRollVisualStep displayStep =
                    ClashRollVisualStepMapper.Create(
                        exchange,
                        first);

                displayStep.RoundIndex =
                    exchange.ExchangeIndex;

                request.ClashSteps.Add(displayStep);

                BattleVisualRequest attackRequest = null;

                bool hasHpDamage =
                    exchange.DamageContext != null;

                bool hasStaggerDamage =
                    exchange.StaggerDamage > 0;

                if (!exchange.WasCancelled &&
                    exchange.WinnerAction != null &&
                    (hasHpDamage || hasStaggerDamage))
                {
                    DamageContext context =
                        exchange.DamageContext;

                    List<int> hitDamages =
                        hasHpDamage
                            ? new List<int>
                            {
                                exchange.Damage
                            }
                            : new List<int>();

                    attackRequest = Build(
                        exchange.WinnerAction,
                        clashSteps: null,
                        hitDamages: hitDamages,
                        opponentAction:
                            exchange.LoserAction,
                        targetPartHpBefore:
                            context?.HasTargetPartSnapshot == true
                                ? context.TargetPartHpBefore
                                : null,
                        targetPartHpAfter:
                            context?.HasTargetPartSnapshot == true
                                ? context.TargetPartHpAfter
                                : null,
                        damageContext: context,
                        useActionDamageContextFallback:
                            hasHpDamage);

                    if (attackRequest != null)
                    {
                        attackRequest.StaggerDamage =
                            Mathf.Max(
                                0,
                                exchange.StaggerDamage);

                        attackRequest.StaggerGaugeBefore =
                            Mathf.Max(
                                0,
                                exchange.StaggerGaugeBefore);

                        attackRequest.StaggerGaugeAfter =
                            Mathf.Max(
                                0,
                                exchange.StaggerGaugeAfter);

                        if (exchange.SecondaryDamageContexts != null)
                        {
                            attackRequest.SecondaryDamageContexts
                                .AddRange(
                                    exchange
                                        .SecondaryDamageContexts);
                        }

                        // Build() 시점에는 exchange의 secondary가 아직 붙기 전이므로
                        // 추가한 뒤 다시 한 번 primary/secondary를 동일 DTO로 정규화한다.
                        NormalizeTargetImpacts(
                            attackRequest);
                    }
                }

                request.ClashExchanges.Add(
                    new BattleClashVisualExchange
                    {
                        ExchangeIndex =
                            exchange.ExchangeIndex,
                        IsOneSided =
                            exchange.IsOneSided,
                        IsTie =
                            exchange.IsTie,
                        WasCancelled =
                            exchange.WasCancelled,
                        MomentumBefore =
                            exchange.MomentumBefore,
                        MomentumAfter =
                            exchange.MomentumAfter,
                        MomentumShift =
                            exchange.MomentumShift,
                        DisplayStep =
                            displayStep,
                        WinnerAction =
                            exchange.WinnerAction,
                        LoserAction =
                            exchange.LoserAction,
                        AttackRequest =
                            attackRequest
                    });
            }
        }

        ResolveWorldPosition(request);

        BattleVisualValidator.ValidateRequest(
            request,
            logWarnings: true);

        return request;
    }

    private static void CopyStandaloneSecondaryDamageContexts(
        BattleVisualRequest request,
        BattleAction action,
        DamageContext primary)
    {
        if (request == null ||
            action?.DamageContexts == null)
        {
            return;
        }

        request.SecondaryDamageContexts.Clear();

        foreach (DamageContext context
                 in action.DamageContexts)
        {
            if (context == null ||
                ReferenceEquals(context, primary))
            {
                continue;
            }

            request.SecondaryDamageContexts.Add(
                context);
        }
    }

    private static void NormalizeTargetImpacts(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        request.TargetImpacts.Clear();

        HashSet<DamageContext> consumed =
            new HashSet<DamageContext>();

        if (request.DamageContext != null)
        {
            TargetImpactPresentation primary =
                CreateTargetImpact(
                    request,
                    request.DamageContext,
                    isPrimary: true,
                    request.HitDamages);

            if (primary != null)
            {
                request.TargetImpacts.Add(primary);
                consumed.Add(request.DamageContext);
            }
        }

        if (request.SecondaryDamageContexts == null)
            return;

        foreach (DamageContext context
                 in request.SecondaryDamageContexts)
        {
            if (context == null ||
                !consumed.Add(context))
            {
                continue;
            }

            List<int> displayHits =
                DistributeLikeRequest(
                    request,
                    context.GetDisplayDamage());

            TargetImpactPresentation impact =
                CreateTargetImpact(
                    request,
                    context,
                    isPrimary: false,
                    displayHits);

            if (impact != null)
                request.TargetImpacts.Add(impact);
        }
    }

    private static TargetImpactPresentation CreateTargetImpact(
        BattleVisualRequest request,
        DamageContext context,
        bool isPrimary,
        IReadOnlyList<int> displayHitDamages)
    {
        if (request == null ||
            context?.Target == null)
        {
            return null;
        }

        List<int> normalizedDisplayHits =
            CopyOrDistributeDisplayHits(
                request,
                context,
                displayHitDamages);

        int characterHpDelta =
            Mathf.Max(
                0,
                context.TargetHpBefore -
                context.TargetHpAfter);

        int partHpDelta =
            context.HasTargetPartSnapshot
                ? Mathf.Max(
                    0,
                    context.TargetPartHpBefore -
                    context.TargetPartHpAfter)
                : 0;

        List<int> characterHpHits =
            DistributeByDisplayShape(
                characterHpDelta,
                normalizedDisplayHits);

        List<int> partHpHits =
            DistributeByDisplayShape(
                partHpDelta,
                normalizedDisplayHits);

        return TargetImpactPresentation
            .FromDamageContext(
                context,
                isPrimary,
                normalizedDisplayHits,
                characterHpHits,
                partHpHits);
    }

    private static List<int> CopyOrDistributeDisplayHits(
        BattleVisualRequest request,
        DamageContext context,
        IReadOnlyList<int> source)
    {
        List<int> result = new List<int>();

        if (source != null && source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(
                    Mathf.Max(
                        0,
                        source[i]));
            }
        }

        if (result.Count > 0)
            return result;

        return DistributeLikeRequest(
            request,
            context?.GetDisplayDamage() ?? 0);
    }

    private static List<int> DistributeLikeRequest(
        BattleVisualRequest request,
        int total)
    {
        int safeTotal =
            Mathf.Max(
                0,
                total);

        if (request?.HitDamages != null &&
            request.HitDamages.Count > 0)
        {
            return DistributeByDisplayShape(
                safeTotal,
                request.HitDamages);
        }

        SkillVisualDefinition visual =
            request?.VisualDefinition;

        int count =
            visual != null &&
            visual.DistributeDamageByHitCount
                ? Mathf.Max(
                    1,
                    visual.ExpectedHitFrameCount)
                : 1;

        if (count <= 1)
            return new List<int> { safeTotal };

        return new List<int>(
            DamageDistributionUtility.DistributeByWeights(
                safeTotal,
                visual?.HitDamageWeights,
                count));
    }

    private static List<int> DistributeByDisplayShape(
        int total,
        IReadOnlyList<int> displayShape)
    {
        int safeTotal =
            Mathf.Max(
                0,
                total);

        int count =
            Mathf.Max(
                1,
                displayShape?.Count ?? 0);

        if (count == 1)
            return new List<int> { safeTotal };

        List<int> weights =
            new List<int>(count);

        int weightTotal = 0;

        for (int i = 0; i < count; i++)
        {
            int weight =
                Mathf.Max(
                    0,
                    displayShape[i]);

            weights.Add(weight);
            weightTotal += weight;
        }

        if (weightTotal <= 0)
        {
            weights.Clear();
            weights.Add(1);

            for (int i = 1; i < count; i++)
                weights.Add(0);
        }

        return new List<int>(
            DamageDistributionUtility.DistributeByWeights(
                safeTotal,
                weights,
                count));
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

        SkillVisualDefinition visual =
            SkillPresentationAccess.Get(
                action.Skill?.Definition);

        if (visual != null)
            return visual;

        Debug.LogError(
            "[BattleVisualRequestBuilder] Timeline-only 정책 위반: " +
            "모든 전투 스킬은 Presentation extension에 " +
            "SkillVisualDefinition을 연결해야 합니다. " +
            $"Skill={action.Skill?.SkillName ?? "NULL"}.");

        return null;
    }
}