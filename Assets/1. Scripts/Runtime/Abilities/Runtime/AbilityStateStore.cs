using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum AbilityOwnerScope { Actor, BodyPart, WeaponInstance, SkillExecution, CounterExecution, Turn, Battle, Run }

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
                  scope==AbilityOwnerScope.Turn) && executionId<=0))
                throw new ArgumentException("Scope requires its exact actor, part/weapon instance or execution identity.");
            Scope=scope; ActorId=actorId;
            InstanceId=scope==AbilityOwnerScope.BodyPart || scope==AbilityOwnerScope.WeaponInstance
                ?instanceId:string.Empty;
            ExecutionId=scope==AbilityOwnerScope.SkillExecution || scope==AbilityOwnerScope.CounterExecution ||
                scope==AbilityOwnerScope.Turn ?executionId:0;
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
        public long Get(AbilityScopeKey scope,string key)=>
            values.TryGetValue((scope,key??string.Empty),out var value)?value:0;
        public void Set(AbilityScopeKey scope,string key,long value)
        {
            if((scope.ActorId<=0 && !(scope.Scope==AbilityOwnerScope.Run && !string.IsNullOrWhiteSpace(scope.InstanceId))) || string.IsNullOrWhiteSpace(key))throw new ArgumentException("Invalid state key.");
            values[(scope,key)]=value;
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
                        key.Item1.Scope==AbilityOwnerScope.CounterExecution))keys.Add(key);
            foreach(var key in keys)values.Remove(key);
        }
    }
}
