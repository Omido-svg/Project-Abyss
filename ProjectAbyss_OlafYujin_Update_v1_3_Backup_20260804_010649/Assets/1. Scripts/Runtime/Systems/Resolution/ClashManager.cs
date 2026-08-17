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

                ApplyNakilRemainingRollRemoval(
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

        battleContext._battleEvent
            .RaiseClashResolved(result);

        return result;
    }

    private static void ApplyNakilRemainingRollRemoval(
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

        YujinMechanic mechanic =
            winner.Owner?
                .GetMechanic<YujinMechanic>();

        if (mechanic?
                .RemovesOpponentRemainingRollsOnExchangeWin(
                    winner) != true)
        {
            return;
        }

        int removed;

        if (winner == first)
        {
            removed = Mathf.Max(0, secondRemaining);
            secondRemaining = 0;
        }
        else if (winner == second)
        {
            removed = Mathf.Max(0, firstRemaining);
            firstRemaining = 0;
        }
        else
        {
            return;
        }

        if (removed <= 0)
            return;

        Debug.Log(
            $"[Nakil] {winner.Owner.name} 교환 승리 / " +
            $"상대 남은 굴림 {removed}개 제거");
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

        while (true)
        {
            clashPowerPipeline.RollForClash(first, second, exchangeIndex);
            clashPowerPipeline.RollForClash(second, first, exchangeIndex);

            if (rerolls == 0)
            {
                firstExecuted = ExecuteSkillOnce(first, ref firstSkillExecuted);
                secondExecuted = ExecuteSkillOnce(second, ref secondSkillExecuted);
            }

            if (!firstExecuted || !secondExecuted ||
                !CanContinueRoll(first) || !CanContinueRoll(second))
            {
                return new ClashExchangeResult
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
                return new ClashExchangeResult
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

        MomentumShiftResult hitMomentum =
            momentumManager.ApplyHit(
                winner.Owner);

        bool duelVsDuel =
            first.ActionType == ActionType.Duel &&
            second.ActionType == ActionType.Duel;

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

        winner.Skill?.NotifyRollResolved(
            winner, loser, exchangeIndex, true, damageContext);
        loser.Skill?.NotifyRollResolved(
            loser, winner, exchangeIndex, false, damageContext);

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

        LogExchange(exchange, isClash: true);
        battleContext._battleEvent
            .RaiseExchangeResolved(exchange);
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
        clashPowerPipeline.RollOneSided(
            action,
            exchangeIndex);

        bool executed = ExecuteSkillOnce(action, ref skillExecuted);
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

        DamagePowerResolution damagePower =
            DamagePowerResolver.ResolveOneSided(
                action);

        // 막을 대상이 없으므로 일방 단계의 수비 굴림은 소멸합니다.
        if (!damagePower.HasDamage)
        {
            action.Skill?.NotifyRollResolved(
                action,
                exhaustedOpponent,
                exchangeIndex,
                false,
                null);

            return new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                WasDefenseResolution =
                    damagePower.WasDefenseResolution,
                FirstClashPower = action.RolledPower,
                FirstRollResult =
                    action.LastRollResult?.Clone(),
                FirstRollType = action.CurrentRollType,
                MomentumBefore = momentumBefore,
                MomentumAfter = momentumBefore
            };
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

        // 최신 규칙: 일방타격은 피해와 위세 충전은 발생하지만
        // 기세 바의 Hit +5는 발생하지 않는다.
        MomentumShiftResult momentum =
            new MomentumShiftResult(
                momentumBefore,
                momentumBefore,
                0,
                MomentumShiftReason.Hit);

        if (action.ActionType != ActionType.Duel)
            action.Skill?.NotifyOneSideHit(action, damageContext);

        action.Skill?.NotifyRollResolved(
            action, exhaustedOpponent, exchangeIndex, true, damageContext);

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
            MomentumShift = 0,
            HitMomentumShift = 0,
            DuelMomentumShift = 0,
            FirstPrestigeGain = dealtGain,
            SecondPrestigeGain = takenGain
        };

        exchange.SecondaryDamageContexts.AddRange(
            secondaryDamageContexts);

        LogExchange(exchange, isClash: cameFromClash);
        battleContext._battleEvent
            .RaiseExchangeResolved(exchange);
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

        BattleAction perspective =
            exchange.WinnerAction ??
            exchange.FirstAction;

        result.ClashSteps.Add(
            exchange.CreateVisualStep(perspective));

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
        ref bool executed)
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

        action.Skill.Execute(action);
        executed = true;
        return true;
    }

    private static bool IsBoundToExchangeOpponent(
        BattleAction action,
        BattleAction opponent)
    {
        if (action == null ||
            opponent == null)
        {
            return false;
        }

        return action.Target == opponent.Owner &&
               action.TargetPart == opponent.OwnerPart;
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
            battleContext?.battleManager?.BattleLogger;

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