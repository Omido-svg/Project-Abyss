using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R6-1 battle-local ability activation lifetime policy.
    /// Does not repurpose N/T status durations or store counters in a shared SO.
    /// A separate instance MUST be owned by each ability/actor binding.
    /// </summary>
    public readonly struct AbilityLifetimeRules
    {
        public readonly bool OncePerTurn;
        public readonly bool OncePerClash;
        public readonly bool OncePerRollUse;
        public readonly bool OncePerOriginHit;
        public readonly int FullCooldownTurns;

        public AbilityLifetimeRules(bool oncePerTurn=false,
            bool oncePerClash=false, bool oncePerRollUse=false,
            bool oncePerOriginHit=false,int fullCooldownTurns=0)
        {
            OncePerTurn=oncePerTurn;
            OncePerClash=oncePerClash;
            OncePerRollUse=oncePerRollUse;
            OncePerOriginHit=oncePerOriginHit;
            FullCooldownTurns=Math.Max(0,fullCooldownTurns);
        }
    }

    public readonly struct AbilityLifetimeEvent
    {
        public readonly long TurnId;
        public readonly long ClashId;
        public readonly long RollUseId;
        public readonly long OriginHitId;
        public readonly long CauseEventId;
        public AbilityLifetimeEvent(long turnId,long clashId,long rollUseId,
            long originHitId,long causeEventId)
        {
            TurnId=turnId;ClashId=clashId;RollUseId=rollUseId;
            OriginHitId=originHitId;CauseEventId=causeEventId;
        }
    }

    public sealed class AbilityLifetimeMemory
    {
        private readonly AbilityLifetimeRules rules;
        private readonly HashSet<long> clashes=new HashSet<long>();
        private readonly HashSet<long> rollUses=new HashSet<long>();
        private readonly HashSet<long> originHits=new HashSet<long>();
        private readonly HashSet<long> committedCauses=new HashSet<long>();
        private readonly HashSet<(long cause,long hit)> countedHitEvents=
            new HashSet<(long,long)>();
        private long lastTurn=-1;
        private long readyTurn=0;
        private int countThisTurn=0;
        private long accumulatedAppliedHpDamage=0;

        public int ActivationCountThisTurn => countThisTurn;
        public long ReadyTurn => readyTurn;
        public long AccumulatedAppliedHpDamage => accumulatedAppliedHpDamage;
        public int SuccessfulCauseCount => committedCauses.Count;

        public AbilityLifetimeMemory(AbilityLifetimeRules rules){this.rules=rules;}

        public bool CanActivate(AbilityLifetimeEvent e)
        {
            if(e.TurnId<0 || e.CauseEventId<=0 ||
               (lastTurn>=0 && e.TurnId<lastTurn) ||
               e.TurnId<readyTurn || committedCauses.Contains(e.CauseEventId))
                return false;
            if(rules.OncePerTurn && lastTurn==e.TurnId && countThisTurn>0)
                return false;
            if(rules.OncePerClash && (e.ClashId<=0 || clashes.Contains(e.ClashId)))
                return false;
            if(rules.OncePerRollUse && (e.RollUseId<=0 || rollUses.Contains(e.RollUseId)))
                return false;
            if(rules.OncePerOriginHit && (e.OriginHitId<=0 || originHits.Contains(e.OriginHitId)))
                return false;
            return true;
        }

        public bool CommitIfApplied(AbilityLifetimeEvent e,AbilityExecutionResult result)
        {
            if(!result.HasApplied || !CanActivate(e))
                return false;
            if(lastTurn!=e.TurnId)
            {
                lastTurn=e.TurnId;
                countThisTurn=0;
            }
            countThisTurn++;
            committedCauses.Add(e.CauseEventId);
            if(rules.OncePerClash)clashes.Add(e.ClashId);
            if(rules.OncePerRollUse)rollUses.Add(e.RollUseId);
            if(rules.OncePerOriginHit)originHits.Add(e.OriginHitId);
            if(rules.FullCooldownTurns>0)
            {
                // N *full* intervening turns: activation at T blocks
                // T+1 .. T+N and becomes ready on T+N+1.
                readyTurn=checked(e.TurnId+rules.FullCooldownTurns+1L);
            }
            return true;
        }

        public int RegisterCommittedHpDamage(
            AppliedDamageEvidence evidence,int threshold)
        {
            if(evidence.CauseEventId<=0 || evidence.OriginHitId<=0 ||
               evidence.OriginRollUseId<=0 ||
               evidence.AppliedHpDamage<=0 || threshold<=0)
                return 0;
            var key=(evidence.CauseEventId,evidence.OriginHitId);
            if(!countedHitEvents.Add(key))
                return 0;
            accumulatedAppliedHpDamage+=evidence.AppliedHpDamage;
            long possible=accumulatedAppliedHpDamage/threshold;
            int triggers=(int)Math.Min(possible,int.MaxValue);
            accumulatedAppliedHpDamage-=(long)triggers*threshold;
            return triggers;
        }

        public void ResetAt(AbilityLifetimeBoundary boundary)
        {
            if(boundary==AbilityLifetimeBoundary.Turn)
            {
                // Do not clear the once-per-turn guard in the SAME turn.
                // A new TurnId naturally starts a new count on Commit.
                return;
            }
            if(boundary==AbilityLifetimeBoundary.Battle ||
               boundary==AbilityLifetimeBoundary.Run)
            {
                clashes.Clear();
                rollUses.Clear();
                originHits.Clear();
                committedCauses.Clear();
                countedHitEvents.Clear();
                lastTurn=-1;
                readyTurn=0;
                countThisTurn=0;
                accumulatedAppliedHpDamage=0;
            }
        }
    }
}
