#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R5-W real DamageManager and typed per-physical-target result parity.</summary>
public static class R5MultiTargetDamageVerification20261102
{
    private sealed class ProbeSkill : NormalSkill
    {
        public ProbeSkill(){SkillName="R5_DAMAGE_BATCH_VIRTUAL";BasePower=2;Resolver=new DiceResolver(1,2);}
        public override void Execute(BattleAction action){}
    }
    [MenuItem("Game System Verification/R5 - Multi Target Damage Result Batch (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var context=manager?.BattleContext;
        var player=context?.Player;
        var normals=context?.Enemies?.Where(c=>c!=null&&!c.IsDead&&!c.UsesBodyParts).Take(2).ToArray();
        var elite=context?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead&&c.UsesBodyParts);
        var elitePart=elite?.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part;
        if(player==null||normals==null||normals.Length<2||elitePart==null||
           context.ResolveDamageManager()==null)
            throw new InvalidOperationException("R5 PlayMode needs player, two normals, elite part, DamageManager");
        int pass=0,fail=0;
        void Check(string id,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R5-Batch] PASS "+id+" | "+detail);}
            else{fail++;Debug.LogError("[R5-Batch] FAIL "+id+" | "+detail);}
        }
        var skill=new ProbeSkill();
        var slot=new ActionSlot{
            ActionId=980001,Owner=player,Skill=skill,SlotId="R5-BATCH-ONLY",
            TargetCharacter=normals[0],TargetPart=null,Phase=ActionPhase.COMBAT,
            ResourceCostCommitted=true
        };
        var action=new BattleAction{Slot=slot};
        var identity=new AbilityIdentity("R5-MULTI-NATIVE",
            R4LiveSlotQuery.ActorRuntimeId(player),"R5-BATCH-ONLY",0);
        var ability=new AbilityInstance(new AbilityDefinitionRef("R5-MULTI-NATIVE","VIRTUAL"),
            identity);
        var e=new AbilityEventContext(AbilityEventKind.RollUsing,980001,990001,
            identity.OwnerActorRuntimeId,0,true,false);
        var targets=new[]{
            new AttackWeightTarget(0,normals[0],null,true),
            new AttackWeightTarget(1,normals[1],null,false),
            new AttackWeightTarget(2,elite,elitePart,false)
        };
        DamageRequest Request(AttackWeightTarget target)=>
            DamageRequest.Custom(target.TargetPart==null?DamageType.Direct:DamageType.SkillPart,
                player,target.TargetCharacter,target.TargetPart,3,1f,
                canBreakPart:false,applyMomentum:false,applyGuard:true,sourceAction:action);
        var beforeHp=targets.Select(t=>t.TargetCharacter.CurrentHP).ToArray();
        var beforeRandom=JsonUtility.ToJson(UnityEngine.Random.state);
        var service=new R5MultiTargetDamageBatchService();
        bool accepted=service.TryExecute(ability,e,action,targets,Request,
            "R5-BATCH-RUNTIME",out var result,out var reason);
        Check("B01 all three commands validated before mutation",
            accepted&&result!=null&&result.RequestedHitCount==3&&
            result.ProcessedHitCount==3&&result.UnprocessedHitCount==0,
            reason);
        Check("B02 each physical target owns independent result and part",
            result!=null&&result.Hits.Count==3&&
            result.Hits.Select(h=>h.TargetActorRuntimeId+":"+h.TargetPartId)
                .Distinct().Count()==3&&
            result.Hits[0].IsPrimary&&result.Hits.Skip(1).All(h=>!h.IsPrimary),
            "distinct actor+part results");
        Check("B03 real DamageManager result associated with every command",
            result!=null&&result.Hits.All(h=>h.Damage!=null&&h.Damage.Result!=null &&
                h.Damage.Target==targets[Array.IndexOf(result.Hits.ToArray(),h)].TargetCharacter),
            "no synthetic damage result");
        Check("B04 applied HP and part totals sum real contexts once",
            result!=null&&result.TotalHpApplied==result.Hits.Sum(x=>x.Damage.AppliedHpDamage)&&
            result.TotalPartApplied==result.Hits.Sum(x=>x.Damage.AppliedPartDamage)&&
            result.TotalStaggerApplied==result.Hits.Sum(x=>x.Damage.StaggerDamageApplied),
            "HP="+result?.TotalHpApplied+" part="+result?.TotalPartApplied);
        Check("B05 source action commit happens at most once for 3 targets",
            result!=null&&result.CommandBatch.CommittedCount<=3 &&
            ability.RuntimeState.SuccessfulActivationCount<=1 &&
            result.CommandBatch.Records.Count==3,
            "committed="+result?.CommandBatch.CommittedCount);
        Check("B06 actual actor HP never increases under damage batch",
            targets.Select((t,i)=>t.TargetCharacter.CurrentHP<=beforeHp[i]).All(x=>x));
        Check("B07 real returned damage result has correct actor+part identity",
            result!=null&&Enumerable.Range(0,3).All(i=>
                result.Hits[i].Damage.Target==targets[i].TargetCharacter &&
                result.Hits[i].Damage.TargetPart==targets[i].TargetPart));
        Check("B08 each known committed hit yields correct cause/roll/hit evidence",
            result!=null&&Enumerable.Range(0,3).All(i=>{
                var hit=result.Hits[i];
                if(!hit.Receipt.EffectCommitted)return true;
                return hit.TryCreateCommittedEvidence(880001,880010+i,out var evidence) &&
                    evidence.CauseEventId==e.CauseId &&
                    evidence.OriginRollUseId==880001 &&
                    evidence.OriginHitId==880010+i &&
                    evidence.TargetActorRuntimeId==hit.TargetActorRuntimeId &&
                    evidence.TargetPartId==hit.TargetPartId &&
                    evidence.IsAdditionalHit==(i>0);
            }),"real result, not current HP inferred");
        Check("B09 zero lineage is refused even after committed damage",
            result!=null&&result.Hits.All(hit=>
                !hit.TryCreateCommittedEvidence(0,0,out _)));
        var hpAfter=targets.Select(t=>t.TargetCharacter.CurrentHP).ToArray();
        bool retry=service.TryExecute(ability,e,action,targets,Request,
            "R5-BATCH-RUNTIME",out var duplicate,out _);
        Check("B10 replay commands cannot deal damage again",
            retry&&duplicate!=null&&duplicate.CommandBatch.CommittedCount==0&&
            duplicate.CommandBatch.Records.All(x=>
                x.Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate) &&
            targets.Select((t,i)=>t.TargetCharacter.CurrentHP==hpAfter[i]).All(x=>x),
            "same command IDs reused");
        var duplicatePhysical=new[]{
            new AttackWeightTarget(0,normals[0],null,true),
            new AttackWeightTarget(1,normals[0],null,false)
        };
        bool rejected=!service.TryExecute(ability,new AbilityEventContext(
                AbilityEventKind.RollUsing,980001,990002,identity.OwnerActorRuntimeId,0,true,false),
            action,duplicatePhysical,Request,"R5-REJECT-DUP",out _,out var rejectReason);
        Check("B11 duplicate physical actor hit preflight rejects without mutation",
            rejected&&!string.IsNullOrWhiteSpace(rejectReason)&&
            targets.Select((t,i)=>t.TargetCharacter.CurrentHP==hpAfter[i]).All(x=>x));
        var wrongRequest=new Func<AttackWeightTarget,DamageRequest>(
            t=>DamageRequest.Direct(player,elite,3,action));
        Check("B12 request payload targeting wrong actor fails before damage",
            !service.TryExecute(ability,new AbilityEventContext(
                    AbilityEventKind.RollUsing,980001,990003,identity.OwnerActorRuntimeId,0,true,false),
                action,targets,wrongRequest,"R5-INVALID-TGT",out _,out _) &&
            targets.Select((t,i)=>t.TargetCharacter.CurrentHP==hpAfter[i]).All(x=>x));
        Check("B13 damage path never draws Unity RNG in batch command planning",
            beforeRandom==JsonUtility.ToJson(UnityEngine.Random.state),
            "existing damage modifiers may use RNG separately; check exact scene result");
        service.ClearForBattleEnd();
        Debug.Log("[R5-Batch] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 multi-target verification failed "+fail);
    }
}
#endif
