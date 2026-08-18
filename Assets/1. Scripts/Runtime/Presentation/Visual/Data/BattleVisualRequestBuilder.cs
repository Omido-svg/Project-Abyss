using UnityEngine;
using System.Collections.Generic;

public class BattleVisualRequestBuilder
{
    // 생성자 시그니처는 기존 호출부 호환을 위해 유지합니다.
    // Timeline-only 정책에서는 Profile fallback을 사용하지 않습니다.

    public BattleVisualRequestBuilder(
        SkillVisualProfile defaultVisualProfile)
    {
        _ = defaultVisualProfile;
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
            action.PrimaryDamageContext;

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

                if (!exchange.WasCancelled &&
                    exchange.WinnerAction != null &&
                    exchange.DamageContext != null)
                {
                    DamageContext context =
                        exchange.DamageContext;

                    attackRequest = Build(
                        exchange.WinnerAction,
                        clashSteps: null,
                        hitDamages: new List<int>
                        {
                            exchange.Damage
                        },
                        opponentAction:
                            exchange.LoserAction,
                        targetPartHpBefore:
                            context.HasTargetPartSnapshot
                                ? context.TargetPartHpBefore
                                : null,
                        targetPartHpAfter:
                            context.HasTargetPartSnapshot
                                ? context.TargetPartHpAfter
                                : null,
                        damageContext: context);

                    if (attackRequest != null &&
                        exchange.SecondaryDamageContexts != null)
                    {
                        attackRequest.SecondaryDamageContexts
                            .AddRange(
                                exchange
                                    .SecondaryDamageContexts);
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