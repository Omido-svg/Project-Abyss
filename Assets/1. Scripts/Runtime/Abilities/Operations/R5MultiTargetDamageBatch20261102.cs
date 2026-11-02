using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    public sealed class R5DamageHitResult
    {
        public CombatCommandEnvelope Command { get; }
        public CombatCommandReceipt Receipt { get; }
        public DamageContext Damage { get; }
        public bool IsPrimary { get; }
        public long TargetActorRuntimeId => Command.Target.ActorRuntimeId;
        public string TargetPartId => Command.Target.PartId;
        public int ActualHpDamage => Damage?.AppliedHpDamage ?? 0;
        public int ActualPartDamage => Damage?.AppliedPartDamage ?? 0;
        public int ActualStaggerDamage => Damage?.StaggerDamageApplied ?? 0;
        public bool Weakened => Damage?.WasWeakened == true;
        public bool Broken => Damage?.WasBroken == true;
        public bool Killed => Damage?.WasKilled == true;
        internal R5DamageHitResult(CombatCommandEnvelope command,
            CombatCommandReceipt receipt, DamageContext result, bool primary)
        {
            Command=command;Receipt=receipt;Damage=result;IsPrimary=primary;
        }

        /// <summary>
        /// DamageSettled evidence uses the ACTUAL result, plus two real event
        /// lineage IDs supplied by the caller. Do not fabricate lineage.
        /// </summary>
        public bool TryCreateCommittedEvidence(long originRollUseId,
            long originHitId, out AppliedDamageEvidence evidence)
        {
            evidence=default;
            if(Damage?.Result==null || !Receipt.EffectCommitted ||
               Receipt.Outcome!=CombatCommandOutcome.Applied ||
               Command.CauseEventId<=0 || originRollUseId<=0 || originHitId<=0)
                return false;
            evidence=new AppliedDamageEvidence(Command.CauseEventId,
                originRollUseId,originHitId,!IsPrimary,
                ActualHpDamage,Weakened,Broken,Killed,
                TargetActorRuntimeId,TargetPartId);
            return true;
        }
    }

    public sealed class R5DamageBatchResult
    {
        public CombatCommandBatchResult CommandBatch { get; }
        public IReadOnlyList<R5DamageHitResult> Hits { get; }
        public int RequestedHitCount { get; }
        public int ProcessedHitCount => Hits.Count;
        public int UnprocessedHitCount => Math.Max(0,RequestedHitCount-ProcessedHitCount);
        public bool HasUnknownCommit => CommandBatch.HasUnknownCommit;
        public int TotalHpApplied => Hits.Sum(h=>h.ActualHpDamage);
        public int TotalPartApplied => Hits.Sum(h=>h.ActualPartDamage);
        public int TotalStaggerApplied => Hits.Sum(h=>h.ActualStaggerDamage);
        internal R5DamageBatchResult(CombatCommandBatchResult applied,
            IReadOnlyList<R5DamageHitResult> hits,int requested)
        {
            CommandBatch=applied;RequestedHitCount=requested;
            Hits=Array.AsReadOnly(hits.ToArray());
        }
    }

    /// <summary>
    /// R5-W opt-in multi-physical-target damage submission. Does NOT call the
    /// original ClashManager automatically, or invent a separate damage formula.
    /// All payloads are validated and bound before ANY gameplay mutation.
    /// Actual calculation/results are owned by DamageManager and R5 specialist.
    /// No replay after an unknown commit; never retry partial batches blindly.
    /// </summary>
    public sealed class R5MultiTargetDamageBatchService
    {
        private readonly CombatCommandApplyService apply =
            new CombatCommandApplyService();

        public bool TryExecute(AbilityInstance ability,AbilityEventContext cause,
            BattleAction action,IReadOnlyList<AttackWeightTarget> physicalTargets,
            Func<AttackWeightTarget,DamageRequest> requestFactory,
            string batchId,out R5DamageBatchResult result,out string error)
        {
            result=null;error=null;
            if(ability==null || action?.Owner==null || physicalTargets==null ||
               physicalTargets.Count==0 || requestFactory==null ||
               string.IsNullOrWhiteSpace(batchId) ||
               cause.ActionId<=0||cause.CauseId<=0 ||
               cause.ActionId!=action.ActionId ||
               cause.OwnerActorRuntimeId!=R4LiveSlotQuery.ActorRuntimeId(action.Owner)||
               cause.OwnerActorRuntimeId!=ability.Identity.OwnerActorRuntimeId)
            {
                error="Invalid action/cause/owner/ability or target batch.";
                return false;
            }
            if(!physicalTargets[0].IsPrimary ||
               physicalTargets[0].TargetCharacter != action.Target ||
               physicalTargets[0].TargetPart != action.TargetPart)
            {
                error="Main target must be the first original action target.";
                return false;
            }
            if(physicalTargets.Count>Math.Max(1,apply.MaxCommandsPerBatch))
            {
                error="Damage batch exceeds R5 safe command count.";
                return false;
            }

            var backendContext=action.Owner.BattleContext;
            if(backendContext==null || backendContext.ResolveDamageManager()==null)
            {
                error="Original battle DamageManager unavailable.";
                return false;
            }
            var backend=new R5CombatSpecialistBackend(backendContext);
            var commands=new List<CombatCommandEnvelope>();
            var seen=new HashSet<HitTargetKey>();
            try
            {
                for(int i=0;i<physicalTargets.Count;i++)
                {
                    var t=physicalTargets[i];
                    var actor=t.TargetCharacter;
                    var part=t.TargetPart;
                    if(!t.IsValid || actor==null || actor.IsDead ||
                       (part!=null && (part.Owner!=actor || part.IsBroken ||
                           !actor.IsValidTargetPart(part))) ||
                       (actor.UsesBodyParts && part==null))
                        throw new ArgumentException("Invalid/broken/dead physical target at index "+i);
                    if(i>0 && t.IsPrimary)
                        throw new ArgumentException("Multiple primary targets are invalid.");
                    var hit=new HitTargetKey(R4LiveSlotQuery.ActorRuntimeId(actor),part?.PartId);
                    if(!seen.Add(hit))
                        throw new ArgumentException("Physical actor+part hit duplicated at index "+i);
                    var request=requestFactory(t);
                    if(request.TargetCharacter!=actor || request.TargetPart!=part ||
                       request.SourceCharacter!=action.Owner ||
                       request.Damage<0 || request.RawPower<0 ||
                       (request.SourceAction!=null && request.SourceAction!=action))
                        throw new ArgumentException("DamageRequest payload disagrees with the frozen physical hit at "+i);
                    var cmd=new CombatCommandEnvelope(batchId+":"+i.ToString("D4"),
                        ability.Identity,CombatCommandKind.DealDamage,
                        cause.ActionId,cause.CauseId,
                        new CombatTargetRef(hit.ActorRuntimeId,hit.PartId,null),
                        request.Damage,0,0,0,i);
                    backend.RegisterDamage(cmd,request);
                    commands.Add(cmd);
                }
            }
            catch(Exception e) when(e is ArgumentException || e is InvalidOperationException)
            {
                backend.ClearBindings();
                error="Preflight rejected without damage: "+e.Message;
                return false;
            }

            // Single R5 application call for all actual physical targets.
            // It preserves stable order and commits one AbilityActivation only.
            var applied=apply.Apply(ability,cause,commands,backend);
            var hits=new List<R5DamageHitResult>();
            for(int i=0;i<applied.Records.Count;i++)
            {
                var record=applied.Records[i];
                backend.TryGetDamageContext(record.Request.CommandId,out var damage);
                hits.Add(new R5DamageHitResult(record.Request,record.Receipt,
                    damage,i==0));
            }
            result=new R5DamageBatchResult(applied,hits,commands.Count);
            backend.ClearBindings();
            return !applied.HasUnknownCommit;
        }

        public void ClearForBattleEnd()=>apply.ResetForBattle();
    }
}
