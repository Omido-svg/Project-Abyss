using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>Captured at actual action start, independent of subsequent rolls and target state changes.</summary>
    public sealed class SkillExecutionFacts
    {
        private readonly Dictionary<string,long> values;
        public long ExecutionId { get; }
        public CombatTargetRef Target { get; }
        public string WeaponInstanceId { get; }
        public SkillExecutionFacts(long executionId,CombatTargetRef target,string weaponInstanceId,
            IDictionary<string,long> observations)
        {
            if(executionId<=0)throw new ArgumentException("Skill execution identity is required.");
            ExecutionId=executionId; Target=target; WeaponInstanceId=weaponInstanceId??string.Empty;
            values=new Dictionary<string,long>(observations??new Dictionary<string,long>(),StringComparer.Ordinal);
        }
        public bool TryGet(string fact,out long value)=>values.TryGetValue(fact,out value);
    }
}
