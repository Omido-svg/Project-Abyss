using System.Collections.Generic;
using UnityEngine;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly MomentumManager momentumManager;
    private readonly BattleContext battleContext;
    private readonly PrestigeChargeService prestigeChargeService;
    private readonly AttackWeightTargetResolver
        attackWeightTargetResolver;
    private readonly ClashRuleSettings clashRules;
    private readonly ClashPowerPipeline clashPowerPipeline;
    private readonly RequiredExchangeReactionPipeline
        requiredExchangeReactionPipeline;

    public ClashManager(
        BattleContext battleContext,
        DamageManager damageManager,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.damageManager = damageManager;
        this.momentumManager = momentumManager;

        prestigeChargeService =
            new PrestigeChargeService(battleContext);

        attackWeightTargetResolver =
            new AttackWeightTargetResolver(
                battleContext);

        clashRules = battleContext?.Rules?.Clash ??
                     new ClashRuleSettings();
        clashRules.Normalize();

        clashPowerPipeline =
            new ClashPowerPipeline(
                clashRules);

        requiredExchangeReactionPipeline =
            new RequiredExchangeReactionPipeline(
                battleContext);
    }

    public List<ClashResultContext> Resolve(
        Queue<ClashPair> clashQueue)
    {
        List<ClashResultContext> results = new();

        if (clashQueue == null)
            return results;

        while (clashQueue.Count > 0)
        {
            ClashResultContext context =
                ResolvePair(clashQueue.Dequeue());

            if (context != null)
                results.Add(context);
        }

        return results;
    }

    public ClashResultContext ResolvePair(
        ClashPair pair)
    {
        if (pair == null)
            return null;

        BattleAction firstAction =
            CreateValidBattleAction(pair.First);

        BattleAction secondAction =
            CreateValidBattleAction(pair.Second);

        // 합에 들어온 행동은 계획 당시 다른 대상을 보고 있었더라도
        // 실제 효과·피해·연출의 주 대상을 서로의 행동 원천으로 고정한다.
        //
        // 예:
        // 올라프 HEAD 행동이 일반 적을 공격하도록 계획되어 있어도,
        // 정예 적이 올라프 HEAD를 공격해 합이 만들어졌다면
        // 올라프가 합에서 승리했을 때 피해 대상은 정예 적이어야 한다.
        if (pair.IsClash)
        {
            BindClashResolutionTargets(
                firstAction,
                secondAction);
        }

        bool firstStarted = false;
        bool secondStarted = false;
        bool firstReady = false;
        bool secondReady = false;

        try
        {
            if (CanExecuteAction(firstAction))
            {
                firstAction.BeginResolutionSequence();
                battleContext._battleEvent
                    .RaiseActionStart(firstAction);
                firstStarted = true;

                firstReady =
                    !clashRules.ConsumeResourceOnActionStart ||
                    TryConsumeResource(firstAction);
            }

            if (CanExecuteAction(secondAction))
            {
                secondAction.BeginResolutionSequence();
                battleContext._battleEvent
                    .RaiseActionStart(secondAction);
                secondStarted = true;

                secondReady =
                    !clashRules.ConsumeResourceOnActionStart ||
                    TryConsumeResource(secondAction);
            }

            BattleAction resolvedFirst =
                firstReady ? firstAction : null;

            BattleAction resolvedSecond =
                secondReady ? secondAction : null;

            if (pair.IsClash)
            {
                return ResolveClash(
                    resolvedFirst,
                    resolvedSecond);
            }

            return ResolveOneSide(resolvedFirst);
        }
        finally
        {
            if (secondStarted)
            {
                battleContext._battleEvent
                    .RaiseActionEnd(secondAction);
            }

            if (firstStarted)
            {
                battleContext._battleEvent
                    .RaiseActionEnd(firstAction);
            }
        }
    }

    private BattleAction CreateValidBattleAction(
        ActionSlot slot)
    {
        if (!IsValidSlot(slot))
            return null;

        return new BattleAction { Slot = slot };
    }

    private static void BindClashResolutionTargets(
        BattleAction first,
        BattleAction second)
    {
        if (first == null ||
            second == null)
        {
            return;
        }

        first.BindClashOpponent(second);
        second.BindClashOpponent(first);
    }

    private bool IsValidSlot(ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.Skill == null)
        {
            return false;
        }

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter != null &&
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            return false;
        }

        return true;
    }

    private ClashResultContext ResolveClash(
        BattleAction first,
        BattleAction second)
    {
        bool canFirst = CanContinueRoll(first);
        bool canSecond = CanContinueRoll(second);

        if (!canFirst && !canSecond)
            return null;

        if (canFirst && !canSecond)
            return ResolveOneSide(first);

        if (!canFirst && canSecond)
            return ResolveOneSide(second);

        battleContext._battleEvent.RaiseClashStart(
            first.Owner,
            second.Owner);

        ClashResultContext result =
            new ClashResultContext
            {
                IsClash = true,
                FirstAction = first,
                SecondAction = second,
                MomentumAtStart =
                    momentumManager.CurrentMomentum
            };

        int firstRemaining =
            first.GetEffectiveExchangeRollCount();
        int secondRemaining =
            second.GetEffectiveExchangeRollCount();

        int exchangeIndex = 0;
        bool firstSkillExecuted = false;
        bool secondSkillExecuted = false;
        bool firstOneSidedStarted = false;
        bool secondOneSidedStarted = false;

        while (firstRemaining > 0 ||
               secondRemaining > 0)
        {
            bool firstCanRoll =
                firstRemaining > 0 &&
                CanContinueRoll(first);

            bool secondCanRoll =
                secondRemaining > 0 &&
                CanContinueRoll(second);

            if (!firstCanRoll && !secondCanRoll)
                break;

            if (firstCanRoll && secondCanRoll)
            {
                ClashExchangeResult exchange =
                    ResolvePairedExchange(
                        first,
                        second,
                        exchangeIndex,
                        ref firstSkillExecuted,
                        ref secondSkillExecuted);

                firstRemaining--;
                secondRemaining--;
                result.PairedExchangeCount++;

                AddExchangeResult(result, exchange);

                if (exchange.WinnerAction == first)
                    result.FirstExchangeWins++;
                else if (exchange.WinnerAction == second)
                    result.SecondExchangeWins++;

                ApplyExchangeContinuationRules(
                    exchange,
                    first,
                    second,
                    ref firstRemaining,
                    ref secondRemaining);

                exchangeIndex++;
                continue;
            }

            BattleAction oneSideAction =
                firstCanRoll ? first : second;

            ref bool oneSideSkillExecuted = ref
                (firstCanRoll
                    ? ref firstSkillExecuted
                    : ref secondSkillExecuted);

            if (oneSideAction == first &&
                !firstOneSidedStarted)
            {
                firstOneSidedStarted = true;

                // 이미 합 굴림을 수행한 행동이 상대 굴림 소진으로
                // 일방 공격 구간에 진입하는 전환점이다.
                if (firstSkillExecuted)
                    first.Skill?.NotifyOneSidedStart(first);
            }
            else if (oneSideAction == second &&
                     !secondOneSidedStarted)
            {
                secondOneSidedStarted = true;

                if (secondSkillExecuted)
                    second.Skill?.NotifyOneSidedStart(second);
            }

            ClashExchangeResult oneSideExchange =
                ResolveOneSideExchange(
                    oneSideAction,
                    oneSideAction == first
                        ? second
                        : first,
                    exchangeIndex,
                    ref oneSideSkillExecuted,
                    cameFromClash: true);

            if (firstCanRoll)
                firstRemaining--;
            else
                secondRemaining--;

            if (oneSideExchange != null &&
                !oneSideExchange.WasCancelled &&
                oneSideExchange.WinnerAction != null)
            {
                result.OneSidedHitCount++;
            }

            AddExchangeResult(
                result,
                oneSideExchange);

            exchangeIndex++;
        }

        FinalizeClashSummary(
            result);

        result.MomentumAfterResolution =
            momentumManager.CurrentMomentum;

        first.Skill?.NotifyAttackEnd(
            first,
            second,
            isClash: true,
            isOneSided: false);
        second.Skill?.NotifyAttackEnd(
            second,
            first,
            isClash: true,
            isOneSided: false);

        battleContext._battleEvent
            .RaiseClashResolved(result);

        return result;
    }

    private static void ApplyExchangeContinuationRules(
        ClashExchangeResult exchange,
        BattleAction first,
        BattleAction second,
        ref int firstRemaining,
        ref int secondRemaining)
    {
        if (exchange == null ||
            exchange.WasCancelled ||
            exchange.IsTie ||
            exchange.IsOneSided ||
            exchange.WinnerAction == null)
        {
            return;
        }

        BattleAction winner =
            exchange.WinnerAction;

        BattleAction opponent;
        int before;
        int after;

        if (winner == first)
        {
            opponent = second;
            before = Mathf.Max(0, secondRemaining);
            after = ModifyOpponentRemainingRollCount(
                winner,
                opponent,
                before);
            secondRemaining = after;
        }
        else if (winner == second)
        {
            opponent = first;
            before = Mathf.Max(0, firstRemaining);
            after = ModifyOpponentRemainingRollCount(
                winner,
                opponent,
                before);
            firstRemaining = after;
        }
        else
        {
            return;
        }

        int removed =
            Mathf.Max(0, before - after);

        if (removed <= 0)
            return;

        Debug.Log(
            $"[ClashContinuation] {winner.Owner.name} 교환 승리 / " +
            $"상대 남은 굴림 {removed}개 제거");
    }

    private static int ModifyOpponentRemainingRollCount(
        BattleAction winner,
        BattleAction opponent,
        int currentRemainingRollCount)
    {
        int remaining =
            Mathf.Max(0, currentRemainingRollCount);

        IReadOnlyList<CombatMechanic> mechanics =
            winner?.Owner?.Mechanics;

        if (mechanics == null)
            return remaining;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is not IExchangeContinuationRule rule)
                continue;

            remaining =
                Mathf.Max(
                    0,
                    rule.ModifyOpponentRemainingRollCount(
                        winner,
                        opponent,
                        remaining));
        }

        return remaining;
    }

    private ClashExchangeResult ResolvePairedExchange(
        BattleAction first,
        BattleAction second,
        int exchangeIndex,
        ref bool firstSkillExecuted,
        ref bool secondSkillExecuted)
    {
        int momentumBefore = momentumManager.CurrentMomentum;
        int rerolls = 0;
        bool firstExecuted = true;
        bool secondExecuted = true;

        // 사용 전/사용시 효과는 첫 굴림보다 먼저 실행되어야
        // 해당 행동의 굴림 보정에도 정상적으로 영향을 줄 수 있다.
        firstExecuted =
            ExecuteSkillOnce(
                first,
                second,
                ref firstSkillExecuted,
                isClash: true,
                isOneSided: false);
        secondExecuted =
            ExecuteSkillOnce(
                second,
                first,
                ref secondSkillExecuted,
                isClash: true,
                isOneSided: false);

        if (!firstExecuted || !secondExecuted ||
            !CanContinueRoll(first) || !CanContinueRoll(second))
        {
            return new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = first,
                SecondAction = second,
                WasCancelled = true,
                FirstClashPower = first?.ClashPower ?? 0,
                SecondClashPower = second?.ClashPower ?? 0,
                FirstRollResult = first?.LastRollResult?.Clone(),
                SecondRollResult = second?.LastRollResult?.Clone(),
                FirstRollType = first?.CurrentRollType ?? CombatRollType.Attack,
                SecondRollType = second?.CurrentRollType ?? CombatRollType.Attack,
                TieRerollCount = rerolls,
                MomentumBefore = momentumBefore,
                MomentumAfter = momentumBefore
            };
        }

        first.Skill?.NotifyRollStart(
            first, second, exchangeIndex,
            isClash: true, isOneSided: false);
        second.Skill?.NotifyRollStart(
            second, first, exchangeIndex,
            isClash: true, isOneSided: false);

        while (true)
        {
            clashPowerPipeline.RollForClash(first, second, exchangeIndex);
            clashPowerPipeline.RollForClash(second, first, exchangeIndex);

            if (!CanContinueRoll(first) || !CanContinueRoll(second))
            {
                ClashExchangeResult cancelled = new ClashExchangeResult
                {
                    ExchangeIndex = exchangeIndex,
                    FirstAction = first,
                    SecondAction = second,
                    WasCancelled = true,
                    FirstClashPower = first.ClashPower,
                    SecondClashPower = second.ClashPower,
                    FirstRollResult = first.LastRollResult?.Clone(),
                    SecondRollResult = second.LastRollResult?.Clone(),
                    FirstRollType = first.CurrentRollType,
                    SecondRollType = second.CurrentRollType,
                    TieRerollCount = rerolls,
                    MomentumBefore = momentumBefore,
                    MomentumAfter = momentumBefore
                };

                first.Skill?.NotifyRollEnd(
                    first, second, exchangeIndex, false, cancelled,
                    isClash: true, isOneSided: false);
                second.Skill?.NotifyRollEnd(
                    second, first, exchangeIndex, false, cancelled,
                    isClash: true, isOneSided: false);

                return cancelled;
            }

            ApplyCharacterRerolls(
                first,
                second,
                exchangeIndex);

            ClashJudgmentResult judgment =
                clashPowerPipeline.Judge(
                    first,
                    second);

            if (!judgment.IsTie)
                break;

            rerolls++;

            // 동률 재굴림은 현재 굴림만 새로 뽑는다.
            // OncePerAction/ReuseValueAcrossAction 캐시도 이 교환에서는 무효화한다.
            first.InvalidateCachedRoll(exchangeIndex);
            second.InvalidateCachedRoll(exchangeIndex);

            if (rerolls >= clashRules.MaxTieRerolls)
            {
                first.Skill?.NotifyClashDraw(first, second);
                second.Skill?.NotifyClashDraw(second, first);

                ClashExchangeResult tie = new ClashExchangeResult
                {
                    ExchangeIndex = exchangeIndex,
                    FirstAction = first,
                    SecondAction = second,
                    IsTie = true,
                    FirstClashPower = first.ClashPower,
                    SecondClashPower = second.ClashPower,
                    FirstRollResult = first.LastRollResult?.Clone(),
                    SecondRollResult = second.LastRollResult?.Clone(),
                    FirstRollType = first.CurrentRollType,
                    SecondRollType = second.CurrentRollType,
                    TieRerollCount = rerolls,
                    MomentumBefore = momentumBefore,
                    MomentumAfter = momentumBefore
                };

                first.Skill?.NotifyRollEnd(
                    first, second, exchangeIndex, false, tie,
                    isClash: true, isOneSided: false);
                second.Skill?.NotifyRollEnd(
                    second, first, exchangeIndex, false, tie,
                    isClash: true, isOneSided: false);

                return tie;
            }
        }

        if (rerolls > 0)
        {
            if (first.LastRollResult != null) first.LastRollResult.WasRerolled = true;
            if (second.LastRollResult != null) second.LastRollResult.WasRerolled = true;
        }

        ClashJudgmentResult finalJudgment =
            clashPowerPipeline.Judge(
                first,
                second);

        BattleAction winner =
            finalJudgment.Winner;

        BattleAction loser =
            finalJudgment.Loser;

        // 개별 교환 승패는 실제 피해 적용보다 먼저 확정된다.
        // 굴림 성공/실패 효과가 이후 피해 계산에 영향을 줄 수 있도록 이 지점에서 발행한다.
        winner.Skill?.NotifyRollOutcome(
            winner, loser, exchangeIndex, true,
            isClash: true, isOneSided: false);
        loser.Skill?.NotifyRollOutcome(
            loser, winner, exchangeIndex, false,
            isClash: true, isOneSided: false);

        DamagePowerResolution damagePower =
            DamagePowerResolver.ResolvePaired(
                winner,
                loser);

        DamageContext damageContext = null;

        List<DamageContext>
            secondaryDamageContexts =
                new List<DamageContext>();

        if (damagePower.HasDamage)
        {
            if (!IsBoundToExchangeOpponent(
                    winner,
                    loser))
            {
                Debug.LogError(
                    "[ClashManager] 합 승자의 해석 타깃이 패자와 일치하지 않습니다. " +
                    $"Winner={GetActionLabel(winner)}, " +
                    $"Loser={GetActionLabel(loser)}, " +
                    $"ResolvedTarget={GetCharacterName(winner.Target)}/" +
                    $"{GetPartName(winner.TargetPart)}");

                winner.BindClashOpponent(loser);
            }

            attackWeightTargetResolver
                .ResolveTargets(
                    winner);

            damageContext =
                damageManager.ApplyDamageContext(
                    winner,
                    damagePower.PrimaryPower,
                    isClashDamage: true,
                    targetLostClash: true,
                    applyMomentum:
                        damagePower.ApplyPrimaryMomentum);

            winner.SetDamageContext(
                damageContext);

            secondaryDamageContexts =
                ApplyAttackWeightDamage(
                    winner,
                    damagePower.SecondaryPower,
                    exchangeIndex,
                    applyMomentum:
                        damagePower.ApplySecondaryMomentum);

            winner.Skill?.NotifyExchangeWin(
                winner,
                loser,
                damageContext);

            loser.Skill?.NotifyExchangeLose(
                loser,
                winner,
                damageContext);
        }

        int firstPrestigeGain =
            prestigeChargeService.ChargeExchangeParticipant(
                first.Owner,
                second.Owner,
                first);

        int secondPrestigeGain =
            prestigeChargeService.ChargeExchangeParticipant(
                second.Owner,
                first.Owner,
                second);

        bool duelVsDuel =
            first.ActionType == ActionType.Duel &&
            second.ActionType == ActionType.Duel;

        // Gameplay v5: Duel 40은 Hit 20에 추가되는 값이 아니라 교환 총 이동량이다.
        MomentumShiftResult hitMomentum =
            duelVsDuel
                ? new MomentumShiftResult(
                    momentumManager.CurrentMomentum,
                    momentumManager.CurrentMomentum,
                    0,
                    MomentumShiftReason.Hit)
                : momentumManager.ApplyHit(winner.Owner);

        MomentumShiftResult duelMomentum =
            duelVsDuel
                ? momentumManager.ApplyDuelExchangeVictory(
                    winner.Owner,
                    winner.Skill?.GetMomentumPushBonus(winner) ?? 0)
                : new MomentumShiftResult(
                    hitMomentum.After,
                    hitMomentum.After,
                    0,
                    MomentumShiftReason.DuelVictory);

        ClashExchangeResult exchange = new ClashExchangeResult
        {
            ExchangeIndex = exchangeIndex,
            FirstAction = first,
            SecondAction = second,
            FirstClashPower = first.ClashPower,
            SecondClashPower = second.ClashPower,
            FirstRollResult = first.LastRollResult?.Clone(),
            SecondRollResult = second.LastRollResult?.Clone(),
            FirstRollType = first.CurrentRollType,
            SecondRollType = second.CurrentRollType,
            TieRerollCount = rerolls,
            WasDefenseResolution =
                damagePower.WasDefenseResolution,
            WinnerAction = winner,
            LoserAction = loser,
            DamageContext = damageContext,
            IsDuelExchange = duelVsDuel,
            MomentumBefore = hitMomentum.Before,
            MomentumAfter = duelMomentum.After,
            MomentumShift =
                hitMomentum.SignedShift +
                duelMomentum.SignedShift,
            HitMomentumShift = hitMomentum.SignedShift,
            DuelMomentumShift = duelMomentum.SignedShift,
            FirstPrestigeGain = firstPrestigeGain,
            SecondPrestigeGain = secondPrestigeGain
        };

        exchange.SecondaryDamageContexts.AddRange(
            secondaryDamageContexts);

        // 기존 RollWin/RollLose 및 신규 RollSuccess/RollFailure는
        // ExchangeResolved보다 먼저 발행해 기존 효과 순서를 보존한다.
        winner.Skill?.NotifyRollResolved(
            winner, loser, exchangeIndex, true, damageContext,
            isClash: true, isOneSided: false);
        loser.Skill?.NotifyRollResolved(
            loser, winner, exchangeIndex, false, damageContext,
            isClash: true, isOneSided: false);

        // 굴림 성공/실패 효과까지 처리된 뒤, 필수 교환 규칙을 먼저 확정한다.
        // StaggerGaugeMechanic의 감소량/Before/After도 여기서 완성되므로
        // OnExchangeResolved의 모든 observer는 동일한 최종 결과를 본다.
        requiredExchangeReactionPipeline.Apply(exchange);

        LogExchange(exchange, isClash: true);
        battleContext._battleEvent
            .RaiseExchangeResolved(exchange);

        winner.Skill?.NotifyHit(
            winner, loser, exchangeIndex, exchange,
            isClash: true, isOneSided: false);

        winner.Skill?.NotifyRollEnd(
            winner, loser, exchangeIndex, true, exchange,
            isClash: true, isOneSided: false);
        loser.Skill?.NotifyRollEnd(
            loser, winner, exchangeIndex, false, exchange,
            isClash: true, isOneSided: false);

        return exchange;
    }

    private ClashResultContext ResolveOneSide(
        BattleAction action)
    {
        if (!CanContinueRoll(action))
            return null;

        ClashResultContext result =
            new ClashResultContext
            {
                IsClash = false,
                FirstAction = action,
                WinnerAction = action,
                MomentumAtStart =
                    momentumManager.CurrentMomentum
            };

        int rollCount =
            action.GetEffectiveExchangeRollCount();

        bool skillExecuted = false;

        for (int i = 0; i < rollCount; i++)
        {
            if (!CanContinueRoll(action))
                break;

            ClashExchangeResult exchange =
                ResolveOneSideExchange(
                    action,
                    null,
                    i,
                    ref skillExecuted,
                    cameFromClash: false);

            if (exchange != null &&
                !exchange.WasCancelled &&
                exchange.WinnerAction != null)
            {
                result.OneSidedHitCount++;
            }

            AddExchangeResult(result, exchange);
        }

        if (result.OneSidedHitCount <= 0)
            result.WinnerAction = null;

        result.MomentumAfterResolution =
            momentumManager.CurrentMomentum;

        action.Skill?.NotifyAttackEnd(
            action,
            null,
            isClash: false,
            isOneSided: true);

        return result;
    }

    private ClashExchangeResult ResolveOneSideExchange(
        BattleAction action,
        BattleAction exhaustedOpponent,
        int exchangeIndex,
        ref bool skillExecuted,
        bool cameFromClash)
    {
        int momentumBefore = momentumManager.CurrentMomentum;

        bool executed = ExecuteSkillOnce(
            action,
            exhaustedOpponent,
            ref skillExecuted,
            isClash: cameFromClash,
            isOneSided: true);

        if (!executed || !CanContinueRoll(action))
        {
            return new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                WasCancelled = true,
                FirstClashPower = action.RolledPower,
                FirstRollResult = action.LastRollResult?.Clone(),
                FirstRollType = action.CurrentRollType,
                MomentumBefore = momentumBefore,
                MomentumAfter = momentumBefore
            };
        }

        action.Skill?.NotifyRollStart(
            action,
            exhaustedOpponent,
            exchangeIndex,
            isClash: cameFromClash,
            isOneSided: true);

        clashPowerPipeline.RollOneSided(
            action,
            exchangeIndex);

        DamagePowerResolution damagePower =
            DamagePowerResolver.ResolveOneSided(
                action);

        bool oneSidedSucceeded =
            damagePower.HasDamage ||
            action.CurrentRollType == CombatRollType.Stagger;

        action.Skill?.NotifyRollOutcome(
            action,
            exhaustedOpponent,
            exchangeIndex,
            oneSidedSucceeded,
            isClash: cameFromClash,
            isOneSided: true);

        // Gameplay v5: 일방 Stagger 굴림도 실제 흐트러짐 공격으로 성립한다.
        // HP DamageContext/잔효과/기세 이동만 만들지 않고 교환 이벤트는 반드시 발행한다.
        if (!damagePower.HasDamage &&
            action.CurrentRollType == CombatRollType.Stagger)
        {
            ClashExchangeResult staggerExchange = new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                WasDefenseResolution = true,
                FirstClashPower = action.RolledPower,
                FirstRollResult = action.LastRollResult?.Clone(),
                FirstRollType = action.CurrentRollType,
                WinnerAction = action,
                LoserAction = exhaustedOpponent,
                MomentumBefore = momentumBefore,
                MomentumAfter = momentumBefore
            };

            action.Skill?.NotifyRollResolved(
                action, exhaustedOpponent, exchangeIndex, true, null,
                isClash: cameFromClash, isOneSided: true);

            requiredExchangeReactionPipeline.Apply(
                staggerExchange);

            LogExchange(staggerExchange, isClash: cameFromClash);
            battleContext._battleEvent.RaiseExchangeResolved(staggerExchange);

            action.Skill?.NotifyHit(
                action, exhaustedOpponent, exchangeIndex, staggerExchange,
                isClash: cameFromClash, isOneSided: true);
            action.Skill?.NotifyRollEnd(
                action, exhaustedOpponent, exchangeIndex, true, staggerExchange,
                isClash: cameFromClash, isOneSided: true);

            return staggerExchange;
        }

        if (!damagePower.HasDamage)
        {
            ClashExchangeResult failedExchange = new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                WasDefenseResolution = damagePower.WasDefenseResolution,
                FirstClashPower = action.RolledPower,
                FirstRollResult = action.LastRollResult?.Clone(),
                FirstRollType = action.CurrentRollType,
                MomentumBefore = momentumBefore,
                MomentumAfter = momentumBefore
            };

            action.Skill?.NotifyRollResolved(
                action, exhaustedOpponent, exchangeIndex, false, null,
                isClash: cameFromClash, isOneSided: true);
            action.Skill?.NotifyRollEnd(
                action, exhaustedOpponent, exchangeIndex, false, failedExchange,
                isClash: cameFromClash, isOneSided: true);

            return failedExchange;
        }

        attackWeightTargetResolver
            .ResolveTargets(
                action);

        DamageContext damageContext = damageManager.ApplyDamageContext(
            action,
            damagePower.PrimaryPower,
            isClashDamage: false,
            targetLostClash: false,
            applyMomentum:
                damagePower.ApplyPrimaryMomentum);

        action.SetDamageContext(damageContext);

        List<DamageContext>
            secondaryDamageContexts =
                ApplyAttackWeightDamage(
                    action,
                    damagePower.SecondaryPower,
                    exchangeIndex,
                    applyMomentum:
                        damagePower.ApplySecondaryMomentum);

        Character target = action.Target;

        int dealtGain =
            prestigeChargeService.ChargeOneSidedParticipant(
                action.Owner,
                target,
                action);

        int takenGain =
            prestigeChargeService.ChargeOneSidedParticipant(
                target,
                action.Owner,
                action);

        // Gameplay v5: 성공한 Attack 굴림은 합/일방 여부와 관계없이
        // 일반 HitShift를 기세에 반영한다.
        // Duel vs Duel만 paired exchange에서 총 40 이동을 사용하고,
        // 일방 Duel은 상대 Duel 교환이 아니므로 일반 적중으로 처리한다.
        MomentumShiftResult momentum =
            momentumManager.ApplyHit(
                action.Owner);

        if (action.ActionType != ActionType.Duel)
            action.Skill?.NotifyOneSideHit(action, damageContext);

        ClashExchangeResult exchange = new ClashExchangeResult
        {
            ExchangeIndex = exchangeIndex,
            FirstAction = action,
            SecondAction = exhaustedOpponent,
            IsOneSided = true,
            FirstClashPower = action.RolledPower,
            FirstRollResult = action.LastRollResult?.Clone(),
            FirstRollType = action.CurrentRollType,
            WinnerAction = action,
            LoserAction = exhaustedOpponent,
            DamageContext = damageContext,
            MomentumBefore = momentum.Before,
            MomentumAfter = momentum.After,
            MomentumShift = momentum.SignedShift,
            HitMomentumShift = momentum.SignedShift,
            DuelMomentumShift = 0,
            FirstPrestigeGain = dealtGain,
            SecondPrestigeGain = takenGain
        };

        exchange.SecondaryDamageContexts.AddRange(
            secondaryDamageContexts);

        action.Skill?.NotifyRollResolved(
            action, exhaustedOpponent, exchangeIndex, true, damageContext,
            isClash: cameFromClash, isOneSided: true);

        requiredExchangeReactionPipeline.Apply(exchange);

        LogExchange(exchange, isClash: cameFromClash);
        battleContext._battleEvent
            .RaiseExchangeResolved(exchange);

        action.Skill?.NotifyHit(
            action, exhaustedOpponent, exchangeIndex, exchange,
            isClash: cameFromClash, isOneSided: true);
        action.Skill?.NotifyRollEnd(
            action, exhaustedOpponent, exchangeIndex, true, exchange,
            isClash: cameFromClash, isOneSided: true);

        return exchange;
    }

    private List<DamageContext>
        ApplyAttackWeightDamage(
            BattleAction action,
            int rawPower,
            int exchangeIndex,
            bool applyMomentum)
    {
        List<DamageContext> results =
            new List<DamageContext>();

        if (action == null ||
            action.Skill == null ||
            action.CurrentRollType !=
                CombatRollType.Attack ||
            action.Skill.AttackWeight <= 1)
        {
            return results;
        }

        IReadOnlyList<AttackWeightTarget> targets =
            attackWeightTargetResolver
                .ResolveTargets(
                    action);

        if (targets == null ||
            targets.Count <= 1)
        {
            return results;
        }

        for (int index = 1;
             index < targets.Count;
             index++)
        {
            AttackWeightTarget target =
                targets[index];

            if (target == null ||
                target.IsPrimary ||
                !target.IsValid)
            {
                continue;
            }

            DamageContext context =
                damageManager
                    .ApplyAttackWeightDamageContext(
                        action,
                        target,
                        rawPower,
                        action.Skill
                            .SecondaryTargetDamageMultiplier,
                        applyMomentum);

            if (context == null)
                continue;

            action.AddAttackWeightHitResult(
                new AttackWeightHitResult(
                    exchangeIndex,
                    target,
                    context));

            results.Add(
                context);
        }

        return results;
    }

    private static void FinalizeClashSummary(
        ClashResultContext result)
    {
        if (result == null)
            return;

        result.Gap =
            Mathf.Abs(
                result.FirstExchangeWins -
                result.SecondExchangeWins);

        result.IsDraw =
            result.FirstExchangeWins ==
            result.SecondExchangeWins;

        // 표준 규칙은 교환별 결과만 사용합니다.
        // 전체 합 다수결 승자와 추가 보상은 존재하지 않습니다.
        result.WinnerAction = null;
        result.LoserAction = null;
    }

    private void AddExchangeResult(
        ClashResultContext result,
        ClashExchangeResult exchange)
    {
        if (result == null || exchange == null)
            return;

        result.Exchanges.Add(exchange);

        result.MomentumShift +=
            exchange.MomentumShift;

        result.PrestigeGain +=
            exchange.FirstPrestigeGain +
            exchange.SecondPrestigeGain;

        if (exchange.DamageContext == null)
            return;

        result.DamageContexts.Add(
            exchange.DamageContext);

        int damage = exchange.Damage;

        if (damage > 0)
            result.HitDamages.Add(damage);

        ApplyDamageAggregate(
            result,
            exchange.DamageContext,
            isPrimaryTarget: true);

        if (exchange.SecondaryDamageContexts == null)
            return;

        foreach (DamageContext secondaryContext
                 in exchange.SecondaryDamageContexts)
        {
            if (secondaryContext == null)
                continue;

            result.DamageContexts.Add(
                secondaryContext);

            result.SecondaryDamageContexts.Add(
                secondaryContext);

            ApplyDamageAggregate(
                result,
                secondaryContext,
                isPrimaryTarget: false);
        }
    }

    private void ApplyDamageAggregate(
        ClashResultContext result,
        DamageContext context,
        bool isPrimaryTarget)
    {
        if (result == null || context == null)
            return;

        if (isPrimaryTarget)
        {
            result.DamageContext = context;
            result.DamageResult = context.Result;
            result.DamageEventResult = context.EventResult;
        }

        result.FinalHpDamage += context.FinalHpDamage;
        result.PartHpDamage += context.PartHpDamage;
        result.DirectHpDamage += context.DirectHpDamage;

        result.WasCritical |= context.WasCritical;
        result.WasKilled |= context.WasKilled;
        result.BrokePart |= context.BrokePart;
        result.WeakenedPart |= context.WeakenedPart;

        if (!isPrimaryTarget)
            return;

        if (!result.HasTargetCharacterHpSnapshot)
        {
            result.TargetCharacterHpBefore =
                context.TargetHpBefore;
        }

        result.HasTargetCharacterHpSnapshot =
            true;

        result.TargetCharacterHpAfter =
            context.TargetHpAfter;

        if (!context.HasTargetPartSnapshot)
            return;

        if (!result.HasTargetPartHpSnapshot)
        {
            result.TargetPartHpBefore =
                context.TargetPartHpBefore;
        }

        result.HasTargetPartHpSnapshot = true;
        result.TargetPartHpAfter =
            context.TargetPartHpAfter;
    }

    private void ApplyCharacterRerolls(
        BattleAction first,
        BattleAction second,
        int exchangeIndex)
    {
        int maximum =
            Mathf.Max(
                1,
                clashRules.MaxCharacterRerollsPerExchange);

        for (int rerollIndex = 0;
             rerollIndex < maximum;
             rerollIndex++)
        {
            bool rerolled = false;

            if (TryCharacterReroll(
                    first,
                    second,
                    exchangeIndex,
                    rerollIndex))
            {
                rerolled = true;
            }

            if (TryCharacterReroll(
                    second,
                    first,
                    exchangeIndex,
                    rerollIndex))
            {
                rerolled = true;
            }

            if (!rerolled)
                break;
        }
    }

    private bool TryCharacterReroll(
        BattleAction action,
        BattleAction opponent,
        int exchangeIndex,
        int rerollIndex)
    {
        if (action?.Owner == null ||
            opponent == null)
        {
            return false;
        }

        ExchangeRerollContext context =
            new ExchangeRerollContext(
                action,
                opponent,
                exchangeIndex,
                rerollIndex);

        if (!action.Owner.TryRequestExchangeReroll(context))
            return false;

        action.InvalidateCachedRoll(exchangeIndex);
        clashPowerPipeline.RollForClash(
            action,
            opponent,
            exchangeIndex);

        if (action.LastRollResult != null)
            action.LastRollResult.WasRerolled = true;

        return true;
    }

    private bool ExecuteSkillOnce(
        BattleAction action,
        BattleAction opponentAction,
        ref bool executed,
        bool isClash,
        bool isOneSided)
    {
        if (executed)
            return true;

        if (action?.Skill == null)
            return false;

        if (!clashRules.ConsumeResourceOnActionStart &&
            !TryConsumeResource(action))
        {
            return false;
        }

        // Detailed phase order:
        // 사용 전 -> 사용시(Execute) -> 합/일방 시작 -> 공격 시작 전 -> 굴림 시작.
        action.Skill.NotifyBeforeUse(
            action,
            opponentAction,
            isClash,
            isOneSided);

        action.Skill.Execute(action);
        executed = true;

        if (!CanContinueRoll(action))
            return true;

        if (isOneSided)
        {
            action.Skill.NotifyOneSidedStart(
                action);
        }
        else if (isClash)
        {
            action.Skill.NotifyClashStart(
                action,
                opponentAction);
        }

        action.Skill.NotifyBeforeAttack(
            action,
            opponentAction,
            isClash,
            isOneSided);

        return true;
    }

    private static bool IsBoundToExchangeOpponent(
        BattleAction action,
        BattleAction opponent)
    {
        if (action == null ||
            opponent?.Owner == null)
        {
            return false;
        }

        if (action.Target != opponent.Owner)
            return false;

        BodyPart targetPart =
            action.TargetPart;

        // TargetSlot은 어떤 행동과 합하는지를 결정하고, TargetPart는
        // 그 캐릭터의 어느 부위가 피해를 받는지를 결정한다.
        // 따라서 targetPart가 상대 행동의 OwnerPart와 같을 필요는 없다.
        return targetPart == null ||
               targetPart.Owner == null ||
               targetPart.Owner == opponent.Owner;
    }

    private static string GetActionLabel(
        BattleAction action)
    {
        if (action == null)
            return "NULL";

        return
            $"{GetCharacterName(action.Owner)}/" +
            $"{GetPartName(action.OwnerPart)}/" +
            $"{action.Skill?.SkillName ?? "NULL"}";
    }

    private static string GetCharacterName(
        Character character)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               "NULL";
    }

    private static string GetPartName(
        BodyPart part)
    {
        return part == null
            ? "CHARACTER"
            : part.Type.ToString();
    }

    private bool TryConsumeResource(
        BattleAction action)
    {
        if (action?.Skill == null || action.Owner == null)
            return false;

        bool consumed =
            action.Skill.TryConsumeResource(
                action.Owner,
                action);

        if (!consumed)
        {
            Debug.LogWarning(
                $"[ClashManager] 행동 비용 부족으로 실행 취소 / " +
                $"Owner={action.Owner.Data?.CharacterName ?? action.Owner.name}, " +
                $"Skill={action.Skill.SkillName}, " +
                $"Energy={action.Owner.CurrentEnergy}/" +
                $"{action.Owner.MaxEnergy}, Cost={action.Skill.EnergyCost}");
        }

        return consumed;
    }

    private bool CanExecuteAction(
        BattleAction action)
    {
        return CanContinueRoll(action);
    }

    private bool CanContinueRoll(
        BattleAction action)
    {
        if (action == null ||
            action.Owner == null ||
            action.Skill == null)
        {
            return false;
        }

        if (action.Owner.IsDead)
            return false;

        if (action.Target != null &&
            action.Target.IsDead)
        {
            return false;
        }

        if (action.OwnerPart != null &&
            action.OwnerPart.IsBroken)
        {
            return false;
        }

        return true;
    }

    private void LogExchange(
        ClashExchangeResult exchange,
        bool isClash)
    {
        if (exchange?.WinnerAction == null)
            return;

        DamageContext context =
            exchange.DamageContext;

        int before =
            context?.GetPrimaryHpBefore() ?? 0;
        int after =
            context?.GetPrimaryHpAfter() ?? 0;
        int damage =
            context?.GetDisplayDamage() ?? 0;

        bool wasBroken =
            context?.BrokePart == true;

        BattleLogger logger =
            battleContext?.Services?.BattleLogger;

        if (logger == null)
            return;

        if (!isClash || exchange.IsOneSided)
        {
            logger.LogOneSideResult(
                exchange.WinnerAction,
                damage,
                before,
                after,
                wasBroken);
            return;
        }

        logger.LogClashResult(
            exchange.WinnerAction,
            true,
            exchange.WinnerAction == exchange.FirstAction
                ? exchange.FirstClashPower
                : exchange.SecondClashPower,
            exchange.WinnerAction == exchange.FirstAction
                ? exchange.SecondClashPower
                : exchange.FirstClashPower,
            damage,
            exchange.WinnerAction == exchange.FirstAction
                ? exchange.FirstPrestigeGain
                : exchange.SecondPrestigeGain,
            before,
            after,
            wasBroken);

        if (exchange.LoserAction != null)
        {
            logger.LogClashResult(
                exchange.LoserAction,
                false,
                exchange.LoserAction == exchange.FirstAction
                    ? exchange.FirstClashPower
                    : exchange.SecondClashPower,
                exchange.LoserAction == exchange.FirstAction
                    ? exchange.SecondClashPower
                    : exchange.FirstClashPower);
        }
    }

    private string GetActionName(
        BattleAction action)
    {
        if (action == null)
            return "NULL";

        return
            $"Id={action.ActionId}, " +
            $"Index={action.ActionIndex}, " +
            $"{action.Owner?.Data?.CharacterName} " +
            $"{action.OwnerPart?.Type.ToString() ?? "NONE"} / " +
            $"{action.Skill?.SkillName}";
    }
}