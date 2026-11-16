using System;
using UnityEngine;

public partial class ClashManager
{
    /// <summary>
    /// R6-Ib opt-in actual combat frame. Each Advance() commits exactly
    /// ONE pre-existing ClashManager exchange, then returns to the coordinator.
    /// Existing roll/damage/modifier/event formulas are called unchanged.
    /// The legacy ResolvePair() remains the default production path.
    /// </summary>
    public sealed class StepwiseSession : IDisposable
    {
        private enum Phase { Paired, OneSided, Finished }
        private readonly ClashManager manager;
        private readonly BattleContext battle;
        private readonly BattleAction first;
        private readonly BattleAction second;
        private readonly bool firstStarted;
        private readonly bool secondStarted;
        private readonly bool isClash;
        private bool firstSkillExecuted;
        private bool secondSkillExecuted;
        private bool firstOneSidedStarted;
        private bool secondOneSidedStarted;
        private bool awaitingCheckpoint;
        private bool closed;
        private Phase phase;
        private int firstRemaining;
        private int secondRemaining;
        private int exchangeIndex;
        private readonly ClashResultContext result;

        public ClashResultContext Result => result;
        public int NextExchangeIndex => exchangeIndex;
        public bool AwaitingCheckpoint => awaitingCheckpoint;
        public bool IsComplete => closed;
        public bool IsClash => isClash;
        public long RootActionId => first?.ActionId ?? second?.ActionId ?? 0L;

        private StepwiseSession(ClashManager manager,ClashPair pair)
        {
            this.manager=manager;
            battle=manager.battleContext;
            first=manager.CreateValidBattleAction(pair.First);
            second=manager.CreateValidBattleAction(pair.Second);
            isClash=pair.IsClash;
            if(isClash)
                BindClashResolutionTargets(first,second);
            bool duelEligible=isClash &&
                first?.ActionType==ActionType.Duel &&
                second?.ActionType==ActionType.Duel;
            first?.SetDuelEffectsEligible(duelEligible);
            second?.SetDuelEffectsEligible(duelEligible);

            bool startedFirst=false,startedSecond=false;
            try
            {
                bool readyFirst=false,readySecond=false;
                if(manager.CanExecuteAction(first))
                {
                    first.BeginResolutionSequence();
                    battle._battleEvent.RaiseActionStart(first);
                    startedFirst=true;
                    readyFirst=first.Slot?.ResourceCostCommitted==true ||
                        !manager.clashRules.ConsumeResourceOnActionStart ||
                        manager.TryConsumeResource(first);
                }
                if(manager.CanExecuteAction(second))
                {
                    second.BeginResolutionSequence();
                    battle._battleEvent.RaiseActionStart(second);
                    startedSecond=true;
                    readySecond=second.Slot?.ResourceCostCommitted==true ||
                        !manager.clashRules.ConsumeResourceOnActionStart ||
                        manager.TryConsumeResource(second);
                }
                firstStarted=startedFirst;
                secondStarted=startedSecond;

                BattleAction liveFirst=readyFirst?first:null;
                BattleAction liveSecond=readySecond?second:null;
                bool firstCan=manager.CanContinueRoll(liveFirst);
                bool secondCan=manager.CanContinueRoll(liveSecond);
                bool paired=isClash && firstCan && secondCan;
                if(paired)
                {
                    battle._battleEvent.RaiseClashStart(first.Owner,second.Owner);
                    result=new ClashResultContext
                    {
                        IsClash=true,FirstAction=first,SecondAction=second,
                        MomentumAtStart=manager.momentumManager.CurrentMomentum
                    };
                    result.PrestigeGain+=manager.prestigeChargeService.ChargeClashStart(first.Owner,second.Owner,first);
                    result.PrestigeGain+=manager.prestigeChargeService.ChargeClashStart(second.Owner,first.Owner,second);
                    firstRemaining=first.GetEffectiveExchangeRollCount();
                    secondRemaining=second.GetEffectiveExchangeRollCount();
                    phase=Phase.Paired;
                }
                else
                {
                    // Mirrors ResolveClash's fallback to one-sided resolution:
                    // if the first participant cannot act, the other may act.
                    BattleAction single=firstCan?liveFirst:secondCan?liveSecond:null;
                    result=single==null?null:new ClashResultContext
                    {
                        IsClash=false,FirstAction=single,WinnerAction=single,
                        MomentumAtStart=manager.momentumManager.CurrentMomentum
                    };
                    firstRemaining=single?.GetEffectiveExchangeRollCount()??0;
                    secondRemaining=0;
                    phase=Phase.OneSided;
                }
            }
            catch
            {
                if(startedSecond)battle._battleEvent.RaiseActionEnd(second);
                if(startedFirst)battle._battleEvent.RaiseActionEnd(first);
                closed=true;
                throw;
            }
        }

        public static StepwiseSession Begin(ClashManager manager,ClashPair pair)
        {
            if(manager==null)throw new ArgumentNullException(nameof(manager));
            if(pair==null)throw new ArgumentNullException(nameof(pair));
            return new StepwiseSession(manager,pair);
        }

        /// <summary>
        /// Commits at most one true exchange. Returns true only when one
        /// exchange was recorded. All its domain events and HP writes finish
        /// before returning. A false result means finalization has run.
        /// </summary>
        public bool Advance(out ClashExchangeResult committed)
        {
            committed=null;
            if(closed)return false;
            if(awaitingCheckpoint)
                throw new InvalidOperationException("Drain AfterExchangeSettled before advancing.");
            try
            {
                if(result==null)
                {
                    Finish();
                    return false;
                }
                if(phase==Phase.Paired)
                {
                    bool canFirst=firstRemaining>0 && manager.CanContinueRoll(first);
                    bool canSecond=secondRemaining>0 && manager.CanContinueRoll(second);
                    if(canFirst && canSecond)
                    {
                        committed=manager.ResolvePairedExchange(first,second,exchangeIndex,
                            ref firstSkillExecuted,ref secondSkillExecuted);
                        firstRemaining--;
                        secondRemaining--;
                        result.PairedExchangeCount++;
                        manager.AddExchangeResult(result,committed);
                        if(committed?.WinnerAction==first)result.FirstExchangeWins++;
                        else if(committed?.WinnerAction==second)result.SecondExchangeWins++;
                        ApplyExchangeContinuationRules(committed,ref firstRemaining,ref secondRemaining);
                        ApplyExchangeRepeatRules(committed,ref firstRemaining,ref secondRemaining);
                        exchangeIndex++;
                        awaitingCheckpoint=true;
                        return true;
                    }
                    FinalizePairedSummary();
                    phase=Phase.OneSided;
                }
                if(phase==Phase.OneSided)
                {
                    // In original one-sided mode, firstRemaining belongs to
                    // result.FirstAction. In clash tail mode it belongs to first.
                    if(!result.IsClash)
                    {
                        BattleAction only=result.FirstAction;
                        if(firstRemaining>0 && manager.CanContinueRoll(only))
                        {
                            committed=manager.ResolveOneSideExchange(only,null,exchangeIndex,
                                ref firstSkillExecuted,cameFromClash:false);
                            if(committed!=null && !committed.WasCancelled &&
                               committed.WinnerAction!=null)result.OneSidedHitCount++;
                            manager.AddExchangeResult(result,committed);
                            firstRemaining--;
                            exchangeIndex++;
                            awaitingCheckpoint=true;
                            return true;
                        }
                        Finish();
                        return false;
                    }

                    bool a=firstRemaining>0 && manager.CanContinueRoll(first);
                    bool b=secondRemaining>0 && manager.CanContinueRoll(second);
                    if(a&&b)
                    {
                        Debug.LogWarning("[R6-Ib] Both clash actions still have rolls after majority finalization.");
                        Finish();
                        return false;
                    }
                    if(!a&&!b)
                    {
                        Finish();
                        return false;
                    }
                    BattleAction attacker=a?first:second;
                    BattleAction exhausted=a?second:first;
                    if(a && !firstOneSidedStarted)
                    {
                        firstOneSidedStarted=true;
                        if(firstSkillExecuted)first.Skill?.NotifyOneSidedStart(first);
                    }
                    else if(b && !secondOneSidedStarted)
                    {
                        secondOneSidedStarted=true;
                        if(secondSkillExecuted)second.Skill?.NotifyOneSidedStart(second);
                    }
                    if(a)
                    {
                        committed=manager.ResolveOneSideExchange(attacker,exhausted,
                            exchangeIndex,ref firstSkillExecuted,cameFromClash:true);
                        firstRemaining--;
                    }
                    else
                    {
                        committed=manager.ResolveOneSideExchange(attacker,exhausted,
                            exchangeIndex,ref secondSkillExecuted,cameFromClash:true);
                        secondRemaining--;
                    }
                    if(committed!=null && !committed.WasCancelled &&
                       committed.WinnerAction!=null)result.OneSidedHitCount++;
                    manager.AddExchangeResult(result,committed);
                    exchangeIndex++;
                    awaitingCheckpoint=true;
                    return true;
                }
                Finish();
                return false;
            }
            catch
            {
                FinishOnException();
                throw;
            }
        }

        public bool ResumeAfterCheckpoint()
        {
            if(closed)return false;
            if(!awaitingCheckpoint)
                throw new InvalidOperationException("No committed exchange is waiting.");
            awaitingCheckpoint=false;
            // The next Advance() checks CanContinueRoll for each participant,
            // preserving death/part-break rules after a reaction.
            return true;
        }

        private void FinalizePairedSummary()
        {
            FinalizeClashSummary(result);
            first.Skill?.NotifyClashResolved(result);
            second.Skill?.NotifyClashResolved(result);
            if(!result.IsDraw && result.WinnerAction!=null &&
               result.LoserAction!=null)
            {
                battle._battleEvent.RaiseClashWin(result.WinnerAction,result.LoserAction);
                battle._battleEvent.RaiseClashLose(result.LoserAction,result.WinnerAction);
                result.PrestigeGain+=manager.prestigeChargeService.ChargeClashWinner(
                    result.WinnerAction.Owner,result.LoserAction.Owner,result.WinnerAction);
                ApplyClashWinnerContinuationRules(result,ref firstRemaining,ref secondRemaining);
            }
        }

        private void Finish()
        {
            if(closed)return;
            try
            {
                if(result!=null)
                {
                    result.MomentumAfterResolution=manager.momentumManager.CurrentMomentum;
                    if(result.IsClash)
                    {
                        if(firstSkillExecuted)first.Skill?.NotifyClashEnded(result);
                        if(secondSkillExecuted)second.Skill?.NotifyClashEnded(result);
                        first.Skill?.NotifyAttackEnd(first,second,true,false);
                        second.Skill?.NotifyAttackEnd(second,first,true,false);
                        battle._battleEvent.RaiseClashResolved(result);
                    }
                    else
                    {
                        if(result.OneSidedHitCount<=0)result.WinnerAction=null;
                        result.FirstAction?.Skill?.NotifyAttackEnd(result.FirstAction,null,false,true);
                    }
                }
            }
            finally{CloseActions();}
        }

        private void FinishOnException()
        {
            // Exception during a specialist commit may already have written HP.
            // Never run normal finalization on indeterminate partial state.
            CloseActions();
        }

        private void CloseActions()
        {
            if(closed)return;
            closed=true;
            phase=Phase.Finished;
            awaitingCheckpoint=false;
            try
            {
                if(secondStarted)battle._battleEvent.RaiseActionEnd(second);
            }
            finally
            {
                if(firstStarted)battle._battleEvent.RaiseActionEnd(first);
            }
        }

        public void Dispose()
        {
            // A cancelled coroutine must always balance ActionStart/End.
            CloseActions();
        }
    }

    public StepwiseSession BeginStepwise(ClashPair pair) =>
        StepwiseSession.Begin(this,pair);
}
