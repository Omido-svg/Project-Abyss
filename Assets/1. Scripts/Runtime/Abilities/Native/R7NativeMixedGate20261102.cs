using System;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7-4 native-only predicate metadata. Never consults SkillEffectEntry,
    /// SkillEffectCondition or other legacy effect authoring at runtime.
    /// </summary>
    public enum R7MixedConditionKind20261102
    {
        None=0,
        RollSucceeded=1,
        RollFailed=2,
        CriticalRoll=3,
        ClashOnly=4,
        OneSidedOnly=5,
        TargetHasStatus=6,
        TargetNumericStatusAtLeast=7
    }

    public enum R7MixedGateVerdict20261102
    {
        Execute=0,
        NotScheduled=1,
        ConditionFailed=2
    }

    public static class R7NativeMixedGate20261102
    {
        public static bool IsValid(R7MixedOperationEntry op)
        {
            if(op==null || !Enum.IsDefined(typeof(R7MixedConditionKind20261102),
                op.Condition))return false;
            if(op.RestrictToRoll && op.RollNumber<1)return false;
            if(op.Condition==R7MixedConditionKind20261102.TargetNumericStatusAtLeast)
                return op.RequiredNumericValue>0 &&
                    StatusEffectFactory.GetStorageKind(op.RequiredStatus)==
                        StatusEffectStorageKind.NumericTimed;
            return true;
        }

        public static R7MixedGateVerdict20261102 Evaluate(
            R7MixedOperationEntry op,SkillEffectContext context)
        {
            if(!IsValid(op))
                throw new InvalidOperationException("Invalid R7-4 native operation guard.");
            if(op.RestrictToRoll &&
                (context==null||context.RollNumber!=op.RollNumber))
                return R7MixedGateVerdict20261102.NotScheduled;
            if(op.Condition==R7MixedConditionKind20261102.None)
                return R7MixedGateVerdict20261102.Execute;
            if(context==null)return R7MixedGateVerdict20261102.ConditionFailed;
            bool meets=op.Condition switch
            {
                R7MixedConditionKind20261102.RollSucceeded=>context.RollSucceeded,
                R7MixedConditionKind20261102.RollFailed=>
                    context.RollNumber>0&&!context.RollSucceeded,
                R7MixedConditionKind20261102.CriticalRoll=>
                    context.RollResult?.IsCritical==true,
                R7MixedConditionKind20261102.ClashOnly=>context.IsClash,
                R7MixedConditionKind20261102.OneSidedOnly=>context.IsOneSided,
                R7MixedConditionKind20261102.TargetHasStatus=>
                    StatusEffectFactory.HasStatus(context.Target,context.TargetPart,
                        op.RequiredStatus),
                R7MixedConditionKind20261102.TargetNumericStatusAtLeast=>
                    StatusEffectFactory.GetNumericTotal(context.Target,
                        context.TargetPart,op.RequiredStatus)>=op.RequiredNumericValue,
                _=>false
            };
            return meets?R7MixedGateVerdict20261102.Execute:
                R7MixedGateVerdict20261102.ConditionFailed;
        }
    }
}