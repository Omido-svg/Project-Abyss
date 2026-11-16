using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum AbilityOwnerScope { Actor, BodyPart, WeaponInstance, SkillExecution, CounterExecution, Turn, Battle, Run, ClashExecution }

    public readonly struct AbilityScopeKey : IEquatable<AbilityScopeKey>
    {
        public readonly AbilityOwnerScope Scope;
        public readonly long ActorId, ExecutionId;
        public readonly string InstanceId;
        public AbilityScopeKey(AbilityOwnerScope scope,long actorId,string instanceId=null,long executionId=0)
        {
            if(!Enum.IsDefined(typeof(AbilityOwnerScope),scope) || actorId<=0 ||
                ((scope==AbilityOwnerScope.BodyPart || scope==AbilityOwnerScope.WeaponInstance) &&
                    string.IsNullOrWhiteSpace(instanceId)) ||
                ((scope==AbilityOwnerScope.SkillExecution || scope==AbilityOwnerScope.CounterExecution ||
                  scope==AbilityOwnerScope.ClashExecution || scope==AbilityOwnerScope.Turn) && executionId<=0))
                throw new ArgumentException("Scope requires its exact actor, part/weapon instance or execution identity.");
            Scope=scope; ActorId=actorId;
            InstanceId=scope==AbilityOwnerScope.BodyPart || scope==AbilityOwnerScope.WeaponInstance
                ?instanceId:string.Empty;
            ExecutionId=scope==AbilityOwnerScope.SkillExecution || scope==AbilityOwnerScope.CounterExecution ||
                scope==AbilityOwnerScope.ClashExecution || scope==AbilityOwnerScope.Turn ?executionId:0;
        }
        public static AbilityScopeKey ForRun(string stableOwnerId)
        {
            if(string.IsNullOrWhiteSpace(stableOwnerId))throw new ArgumentException("Stable run owner ID is required.");
            return new AbilityScopeKey(stableOwnerId);
        }
        private AbilityScopeKey(string stableOwnerId)
        { Scope=AbilityOwnerScope.Run; ActorId=0; ExecutionId=0; InstanceId=stableOwnerId; }
        public bool Equals(AbilityScopeKey other) => Scope==other.Scope && ActorId==other.ActorId &&
            ExecutionId==other.ExecutionId && string.Equals(InstanceId,other.InstanceId,StringComparison.Ordinal);
        public override bool Equals(object obj)=>obj is AbilityScopeKey other && Equals(other);
        public override int GetHashCode()=>HashCode.Combine((int)Scope,ActorId,ExecutionId,InstanceId);
    }

    /// <summary>Owned by an ability binding, never a shared definition asset. Run state is explicitly retained.</summary>
    public sealed class AbilityStateStore
    {
        private readonly Dictionary<(AbilityScopeKey,string),long> values=new();
        public int Count=>values.Count;
        public IReadOnlyDictionary<(AbilityScopeKey,string),long> Capture()=>
            new System.Collections.ObjectModel.ReadOnlyDictionary<(AbilityScopeKey,string),long>(new Dictionary<(AbilityScopeKey,string),long>(values));
        public long Get(AbilityScopeKey scope,string key)=>
            values.TryGetValue((scope,key??string.Empty),out var value)?value:0;
        public void Set(AbilityScopeKey scope,string key,long value)
        {
            if((scope.ActorId<=0 && !(scope.Scope==AbilityOwnerScope.Run && !string.IsNullOrWhiteSpace(scope.InstanceId))) || string.IsNullOrWhiteSpace(key))throw new ArgumentException("Invalid state key.");
            values[(scope,key)]=value;
        }
        public AbilityRunStateSnapshot CaptureRunSnapshot()
        {
            var snapshot=new AbilityRunStateSnapshot();
            foreach(var pair in values)
                if(pair.Key.Item1.Scope==AbilityOwnerScope.Run && !string.IsNullOrWhiteSpace(pair.Key.Item1.InstanceId))
                    snapshot.Entries.Add(new AbilityRunStateEntry{OwnerId=pair.Key.Item1.InstanceId,Key=pair.Key.Item2,Value=pair.Value});
            snapshot.Entries.Sort((a,b)=>{int owner=StringComparer.Ordinal.Compare(a.OwnerId,b.OwnerId);return owner!=0?owner:StringComparer.Ordinal.Compare(a.Key,b.Key);});
            return snapshot;
        }
        public bool TryRestoreRunSnapshot(AbilityRunStateSnapshot snapshot)
        {
            if(snapshot==null||snapshot.SchemaVersion!=1||snapshot.Entries==null)return false;
            var restored=new Dictionary<(AbilityScopeKey,string),long>();
            foreach(var entry in snapshot.Entries)
            {
                if(entry==null||string.IsNullOrWhiteSpace(entry.OwnerId)||string.IsNullOrWhiteSpace(entry.Key))return false;
                if(!restored.TryAdd((AbilityScopeKey.ForRun(entry.OwnerId),entry.Key),entry.Value))return false;
            }
            var remove=new List<(AbilityScopeKey,string)>();foreach(var key in values.Keys)if(key.Item1.Scope==AbilityOwnerScope.Run)remove.Add(key);
            foreach(var key in remove)values.Remove(key);foreach(var pair in restored)values.Add(pair.Key,pair.Value);return true;
        }
        public void Release(AbilityScopeKey scope)
        {
            var keys=new List<(AbilityScopeKey,string)>();
            foreach(var key in values.Keys)if(key.Item1.Equals(scope))keys.Add(key);
            foreach(var key in keys)values.Remove(key);
        }
        public void ResetAt(AbilityLifetimeBoundary boundary)
        {
            var keys=new List<(AbilityScopeKey,string)>();
            foreach(var key in values.Keys)
                if(boundary==AbilityLifetimeBoundary.Run ||
                   boundary==AbilityLifetimeBoundary.Battle && key.Item1.Scope!=AbilityOwnerScope.Run ||
                   boundary==AbilityLifetimeBoundary.Turn &&
                       (key.Item1.Scope==AbilityOwnerScope.Turn || key.Item1.Scope==AbilityOwnerScope.SkillExecution ||
                        key.Item1.Scope==AbilityOwnerScope.CounterExecution || key.Item1.Scope==AbilityOwnerScope.ClashExecution))keys.Add(key);
            foreach(var key in keys)values.Remove(key);
        }
    }
}
