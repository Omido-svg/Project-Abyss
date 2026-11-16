using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    public interface IAbilityExecutionScopeProvider
    {
        string WeaponInstanceId { get; }
        void CaptureAbilityFacts(BattleAction action,IDictionary<FactKey,int> facts);
    }
    public sealed class CapturedAbilityBoundary
    {
        internal readonly Dictionary<FactKey,int> Facts;
        private readonly Dictionary<long,Dictionary<FactKey,int>> ownerFacts=new();
        private readonly Dictionary<long,string> weapons=new();
        public string WeaponFor(long ownerId)=>weapons.TryGetValue(ownerId,out var id)?id:string.Empty;
        internal readonly IReadOnlyDictionary<(AbilityScopeKey,string),long> BattleState,RunState;
        public readonly CombatTargetRef Source, AimedTarget;
        public readonly string WeaponInstanceId;
        public readonly long SkillExecutionId,CounterExecutionId,ClashId;
        public readonly int TurnId;
        internal CapturedAbilityBoundary(BattleAbilityRuntime runtime,BattleAction action)
        {
            SkillExecutionId=action.ActionId;CounterExecutionId=action.CounterExecution?.ExecutionId??0;ClashId=action.ClashExecutionId;TurnId=runtime.CurrentTurn;
            Source=BattleAbilityRuntime.Reference(action.Owner,action.OwnerPart,action.Slot?.SlotId);
            AimedTarget=BattleAbilityRuntime.Reference(action.DeclaredTarget,action.DeclaredTargetPart);
            var scope=action.Owner?.Mechanics?.OfType<IAbilityExecutionScopeProvider>().FirstOrDefault();
            WeaponInstanceId=scope?.WeaponInstanceId??string.Empty;
            Facts=new Dictionary<FactKey,int>();
            foreach(var actor in runtime.Battle.AllCharacters.Concat(new[]{action.Owner}).Where(x=>x!=null).Distinct())
            {
                long id=LiveSlotQuery.ActorRuntimeId(actor);
                Facts[new FactKey(id,ConditionScope.Actor,ConditionFact.ResourceAmount,"hp")]=actor.CurrentHP;
                Facts[new FactKey(id,ConditionScope.Actor,ConditionFact.GameplayTag,"alive")]=actor.IsDead?0:1;
                CaptureStatuses(actor,null,Facts);
                foreach(var part in actor.BodyParts??new List<BodyPart>())
                {
                    if(part==null)continue;
                    Facts[new FactKey(id,ConditionScope.Part,ConditionFact.ResourceAmount,"hp",part.PartId)]=(int)part.PartHP;
                    Facts[new FactKey(id,ConditionScope.Part,ConditionFact.GameplayTag,"alive",part.PartId)]=actor.IsDead||part.IsBroken?0:1;
                    CaptureStatuses(actor,part,Facts);
                }
            }
            foreach(var actor in runtime.Battle.AllCharacters.Concat(new[]{action.Owner}).Where(a=>a!=null).Distinct())
            {
                var captured=new Dictionary<FactKey,int>();long id=LiveSlotQuery.ActorRuntimeId(actor);
                foreach(var provider in actor.Mechanics?.OfType<IAbilityExecutionScopeProvider>()??Enumerable.Empty<IAbilityExecutionScopeProvider>())
                {provider.CaptureAbilityFacts(action,captured);if(!weapons.ContainsKey(id))weapons.Add(id,provider.WeaponInstanceId??string.Empty);}
                ownerFacts[id]=captured;
            }
            BattleState=runtime.State.Capture();RunState=runtime.RunState.Capture();
        }
        private static void CaptureStatuses(Character actor,BodyPart part,IDictionary<FactKey,int> facts)
        {
            long id=LiveSlotQuery.ActorRuntimeId(actor);var scope=part==null?ConditionScope.Actor:ConditionScope.Part;
            foreach(StatusEffectId status in Enum.GetValues(typeof(StatusEffectId)))
            {
                bool present=StatusEffectFactory.HasStatus(actor,part,status,part==null,part!=null);
                facts[new FactKey(id,scope,ConditionFact.TimedStatusPresent,status.ToString(),part?.PartId)]=present?1:0;
                if(StatusEffectFactory.GetStorageKind(status)==StatusEffectStorageKind.NumericTimed)
                    facts[new FactKey(id,scope,ConditionFact.StatusNumericTotal,status.ToString(),part?.PartId)]=
                        StatusEffectFactory.GetNumericTotal(actor,part,status,part==null,part!=null);
            }
            var statuses=part==null?actor.StatusEffects:part.StatusEffects;
            if(statuses==null)return;
            foreach(var group in statuses.Where(x=>x is IUniqueKeywordStatus).GroupBy(x=>((IUniqueKeywordStatus)x).UniqueKeywordId))
            {
                facts[new FactKey(id,scope,ConditionFact.KeywordPresent,group.Key,part?.PartId)]=1;
                facts[new FactKey(id,scope,ConditionFact.KeywordStack,group.Key,part?.PartId)]=group.Sum(x=>x.Stack);
            }
        }
        internal ConditionSnapshot Snapshot(CombatTargetRef actual,long hitId=0,CombatTargetRef? sourceOverride=null)
        {
            var source=sourceOverride??Source;var facts=new Dictionary<FactKey,int>(Facts);
            if(ownerFacts.TryGetValue(source.ActorRuntimeId,out var mechanicFacts))foreach(var pair in mechanicFacts)facts[pair.Key]=pair.Value;
            return new ConditionSnapshot(new SubjectRef(source.ActorRuntimeId,source.PartId,source.SlotId),
                new SubjectRef(AimedTarget.ActorRuntimeId,AimedTarget.PartId,AimedTarget.SlotId),
                new SubjectRef(actual.ActorRuntimeId,actual.PartId,actual.SlotId),new SubjectRef(source.ActorRuntimeId),hitId,facts);
        }
    }
    public sealed class AbilitySnapshotStore
    {
        private readonly Dictionary<(long,AbilitySnapshotPolicy),CapturedAbilityBoundary> boundaries=new();
        public void Capture(BattleAbilityRuntime runtime,BattleAction action,AbilitySnapshotPolicy policy,bool replace=false)
        {
            if(action?.Owner==null||action.ActionId<=0)return;
            var key=(action.ActionId,policy);
            if(replace||!boundaries.ContainsKey(key))boundaries[key]=new CapturedAbilityBoundary(runtime,action);
        }
        public bool TryGet(long actionId,AbilitySnapshotPolicy policy,out CapturedAbilityBoundary boundary)=>
            boundaries.TryGetValue((actionId,policy),out boundary);
        public void Release(long actionId)
        {foreach(var key in boundaries.Keys.Where(k=>k.Item1==actionId).ToArray())boundaries.Remove(key);}
        public void Clear()=>boundaries.Clear();
    }
}
