using System.Collections.Generic;
using UnityEngine;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly MomentumManager momentumManager;
    private readonly BattleContext battleContext;
    private readonly PrestigeChargeService prestigeChargeService;
    private readonly ClashRuleSettings clashRules;

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

        clashRules = battleContext?.Rules?.Clash ??
                     new ClashRuleSettings();
        clashRules.Normalize();
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

        int firstStartPrestige =
            prestigeChargeService.ChargeClashStart(
                first.Owner,
                second.Owner,
                first);

        int secondStartPrestige =
            prestigeChargeService.ChargeClashStart(
                second.Owner,
                first.Owner,
                second);

        ClashResultContext result =
            new ClashResultContext
            {
                IsClash = true,
                FirstAction = first,
                SecondAction = second,
                PrestigeGain =
                    firstStartPrestige +
                    secondStartPrestige
            };

        int firstRemaining =
            Mathf.Max(1, first.Skill.ExchangeRollCount);
        int secondRemaining =
            Mathf.Max(1, second.Skill.ExchangeRollCount);

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

        FinalizeClashMajority(
            result,
            first,
            second);

        battleContext._battleEvent
            .RaiseClashResolved(result);

        return result;
    }

    private ClashExchangeResult ResolvePairedExchange(
        BattleAction first,
        BattleAction second,
        int exchangeIndex,
        ref bool firstSkillExecuted,
        ref bool secondSkillExecuted)
    {
        RollClashPower(
            first,
            second,
            exchangeIndex);

        RollClashPower(
            second,
            first,
            exchangeIndex);

        // OnExecute는 첫 교환에서 이긴 쪽만의 보상이 아니다.
        // 양쪽 행동이 실제로 굴림을 시작했다면 승패 비교 전에
        // 각각 정확히 한 번 실행되어야 한다.
        bool firstExecuted = ExecuteSkillOnce(
            first,
            ref firstSkillExecuted);

        bool secondExecuted = ExecuteSkillOnce(
            second,
            ref secondSkillExecuted);

        int firstPower = first.ClashPower;
        int secondPower = second.ClashPower;

        ClashExchangeResult exchange =
            new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = first,
                SecondAction = second,
                FirstClashPower = firstPower,
                SecondClashPower = secondPower,
                FirstRollResult =
                    first.LastRollResult?.Clone(),
                SecondRollResult =
                    second.LastRollResult?.Clone(),
                IsTie = firstPower == secondPower
            };

        Debug.Log(
            $"[Clash Exchange] Index={exchangeIndex}, " +
            $"First={GetActionName(first)} / {firstPower}, " +
            $"Second={GetActionName(second)} / {secondPower}");

        // 히후미 자해나 OnExecute 효과로 행동자/대상이 사망하거나
        // 행동 부위가 파괴되었다면 이미 만든 굴림만 소모하고
        // 이번 교환의 피해·기세·승리 수는 발생시키지 않는다.
        if (!firstExecuted ||
            !secondExecuted ||
            !CanContinueRoll(first) ||
            !CanContinueRoll(second))
        {
            exchange.WasCancelled = true;
            return exchange;
        }

        if (exchange.IsTie)
        {
            first.Skill?.NotifyClashDraw(first, second);
            second.Skill?.NotifyClashDraw(second, first);
            return exchange;
        }

        bool firstWon = firstPower > secondPower;
        BattleAction winner = firstWon ? first : second;
        BattleAction loser = firstWon ? second : first;

        DamageContext damageContext =
            damageManager.ApplyDamageContext(
                winner,
                isClashDamage: true,
                targetLostClash: true);

        winner.SetDamageContext(damageContext);

        MomentumShiftResult momentum =
            momentumManager.ApplyHit(winner.Owner);

        int dealtGain =
            prestigeChargeService.ChargeHitDealt(
                winner.Owner,
                loser.Owner,
                winner);

        int takenGain =
            prestigeChargeService.ChargeHitTaken(
                loser.Owner,
                winner.Owner,
                winner);

        exchange.WinnerAction = winner;
        exchange.LoserAction = loser;
        exchange.DamageContext = damageContext;
        exchange.MomentumShift = momentum.SignedShift;
        exchange.PrestigeDealtGain = dealtGain;
        exchange.PrestigeTakenGain = takenGain;

        winner.Skill?.NotifyExchangeWin(
            winner,
            loser,
            damageContext);

        loser.Skill?.NotifyExchangeLose(
            loser,
            winner,
            damageContext);

        LogExchange(
            exchange,
            isClash: true);

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
                WinnerAction = action
            };

        int rollCount = Mathf.Max(
            1,
            action.Skill.ExchangeRollCount);

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

        FinalizeCompatibilityFields(result);
        return result;
    }

    private ClashExchangeResult ResolveOneSideExchange(
        BattleAction action,
        BattleAction exhaustedOpponent,
        int exchangeIndex,
        ref bool skillExecuted,
        bool cameFromClash)
    {
        action.RollPowerForExchange(exchangeIndex);
        action.ClearClashModifiers();

        bool executed = ExecuteSkillOnce(
            action,
            ref skillExecuted);

        if (!executed ||
            !CanContinueRoll(action))
        {
            return new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                WasCancelled = true,
                FirstClashPower = action.RolledPower,
                SecondClashPower = 0,
                FirstRollResult =
                    action.LastRollResult?.Clone()
            };
        }

        DamageContext damageContext =
            damageManager.ApplyDamageContext(
                action,
                isClashDamage: false,
                targetLostClash: false);

        action.SetDamageContext(damageContext);

        MomentumShiftResult momentum =
            momentumManager.ApplyHit(action.Owner);

        Character target = action.Target;

        int dealtGain =
            prestigeChargeService.ChargeHitDealt(
                action.Owner,
                target,
                action);

        int takenGain =
            prestigeChargeService.ChargeHitTaken(
                target,
                action.Owner,
                action);

        ClashExchangeResult exchange =
            new ClashExchangeResult
            {
                ExchangeIndex = exchangeIndex,
                FirstAction = action,
                SecondAction = exhaustedOpponent,
                IsOneSided = true,
                FirstClashPower = action.RolledPower,
                SecondClashPower = 0,
                FirstRollResult =
                    action.LastRollResult?.Clone(),
                WinnerAction = action,
                LoserAction = exhaustedOpponent,
                DamageContext = damageContext,
                MomentumShift = momentum.SignedShift,
                PrestigeDealtGain = dealtGain,
                PrestigeTakenGain = takenGain
            };

        action.Skill?.NotifyOneSideHit(
            action,
            damageContext);

        LogExchange(
            exchange,
            isClash: cameFromClash);

        return exchange;
    }

    private void FinalizeClashMajority(
        ClashResultContext result,
        BattleAction first,
        BattleAction second)
    {
        if (result == null)
            return;

        if (result.FirstExchangeWins ==
            result.SecondExchangeWins)
        {
            result.IsDraw = true;
            result.WinnerAction = null;
            result.LoserAction = null;
            FinalizeCompatibilityFields(result);
            return;
        }

        bool firstWon =
            result.FirstExchangeWins >
            result.SecondExchangeWins;

        BattleAction winner = firstWon
            ? first
            : second;
        BattleAction loser = firstWon
            ? second
            : first;

        result.WinnerAction = winner;
        result.LoserAction = loser;
        result.IsDraw = false;
        result.Gap = Mathf.Abs(
            result.FirstExchangeWins -
            result.SecondExchangeWins);

        result.WinnerMomentumStateBefore =
            momentumManager.GetState(winner.Owner);

        // 다수결 기록상 승자여도 남은 일방 공격이나 자해로
        // 사망했다면 승리 보상·결투 푸시·승패 이벤트를 받지 않는다.
        if (!CanReceiveClashVictoryEffects(winner))
        {
            result.WinnerMomentumStateAfter =
                result.WinnerMomentumStateBefore;

            FinalizeCompatibilityFields(result);
            return;
        }

        int victoryPrestige =
            prestigeChargeService.ChargeClashVictory(
                winner.Owner,
                loser.Owner,
                winner);

        result.PrestigeGain += victoryPrestige;

        bool duelVsDuel =
            winner.ActionType == ActionType.Duel &&
            loser.ActionType == ActionType.Duel;

        if (duelVsDuel)
        {
            int skillBonus =
                winner.Skill?.GetMomentumPushBonus(
                    winner) ?? 0;

            MomentumShiftResult push =
                momentumManager.ApplyDuelVictory(
                    winner.Owner,
                    skillBonus);

            result.MomentumShift +=
                push.SignedShift;
        }

        result.WinnerMomentumStateAfter =
            momentumManager.GetState(winner.Owner);

        battleContext._battleEvent.RaiseClashWin(
            winner,
            loser);

        battleContext._battleEvent.RaiseClashLose(
            loser,
            winner);

        FinalizeCompatibilityFields(result);
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
            exchange.PrestigeDealtGain +
            exchange.PrestigeTakenGain;

        if (exchange.DamageContext == null)
            return;

        result.DamageContexts.Add(
            exchange.DamageContext);

        int damage = exchange.Damage;

        if (damage > 0)
            result.HitDamages.Add(damage);

        ApplyDamageAggregate(
            result,
            exchange.DamageContext);
    }

    private void ApplyDamageAggregate(
        ClashResultContext result,
        DamageContext context)
    {
        if (result == null || context == null)
            return;

        result.DamageContext = context;
        result.DamageResult = context.Result;
        result.DamageEventResult = context.EventResult;

        result.FinalHpDamage += context.FinalHpDamage;
        result.PartHpDamage += context.PartHpDamage;
        result.DirectHpDamage += context.DirectHpDamage;

        result.WasCritical |= context.WasCritical;
        result.WasKilled |= context.WasKilled;
        result.BrokePart |= context.BrokePart;
        result.WeakenedPart |= context.WeakenedPart;

        result.HasTargetCharacterHpSnapshot = true;

        if (result.DamageContexts.Count == 1)
            result.TargetCharacterHpBefore = context.TargetHpBefore;

        result.TargetCharacterHpAfter = context.TargetHpAfter;

        if (!context.HasTargetPartSnapshot)
            return;

        if (!result.HasTargetPartHpSnapshot)
            result.TargetPartHpBefore = context.TargetPartHpBefore;

        result.HasTargetPartHpSnapshot = true;
        result.TargetPartHpAfter = context.TargetPartHpAfter;
    }

    private void FinalizeCompatibilityFields(
        ClashResultContext result)
    {
        if (result == null)
            return;

        int firstTotal = 0;
        int secondTotal = 0;

        foreach (ClashExchangeResult exchange
                 in result.Exchanges)
        {
            firstTotal += exchange?.FirstClashPower ?? 0;
            secondTotal += exchange?.SecondClashPower ?? 0;
        }

        bool winnerIsFirst =
            result.WinnerAction != null &&
            result.WinnerAction == result.FirstAction;

        result.WinnerClashPower = winnerIsFirst
            ? firstTotal
            : secondTotal;

        result.LoserClashPower = winnerIsFirst
            ? secondTotal
            : firstTotal;

        if (result.WinnerAction == null)
        {
            result.WinnerClashPower = firstTotal;
            result.LoserClashPower = secondTotal;
        }

        result.WinnerWasCritical =
            result.WinnerAction?.RollHistory.Exists(
                roll => roll != null && roll.IsCritical) == true;

        result.LoserWasCritical =
            result.LoserAction?.RollHistory.Exists(
                roll => roll != null && roll.IsCritical) == true;
    }

    private void RollClashPower(
        BattleAction action,
        BattleAction opponent,
        int exchangeIndex)
    {
        if (action == null)
            return;

        action.RollPowerForExchange(exchangeIndex);

        int speedModifier =
            CalculateSpeedModifier(
                action,
                opponent);

        int preparationModifier =
            action.Owner?.TurnClashPowerBonus ?? 0;

        // 기세는 합 수치가 아니라 피해 배율과 히트 이동에만 사용한다.
        // 도사림은 같은 턴 전체 합에 flat 보정으로 적용한다.
        action.ApplyClashModifiers(
            speedModifier,
            0,
            preparationModifier);
    }

    private int CalculateSpeedModifier(
        BattleAction self,
        BattleAction opponent)
    {
        if (self == null || opponent == null)
            return 0;

        int speedGap = Mathf.Max(
            0,
            self.Speed - opponent.Speed);

        // 단계식 속도 보정
        // 0~2  : +0
        // 3~5  : +1
        // 6~8  : +2
        // 9 이상: +3
        //
        // SpeedWeight는 기존 세이브/디버그 계약을 유지하기 위한
        // 최종 배율로만 사용한다. 기본값 1에서는 위 표 그대로 동작한다.
        int tierModifier = speedGap switch
        {
            <= 2 => 0,
            <= 5 => 1,
            <= 8 => 2,
            _ => 3
        };

        return tierModifier * clashRules.SpeedWeight;
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

    private static bool CanReceiveClashVictoryEffects(
        BattleAction action)
    {
        return action?.Owner != null &&
               !action.Owner.IsDead &&
               action.Skill != null;
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
            exchange.PrestigeDealtGain,
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