using System;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7-4 native-only predicate metadata. Never consults SkillEffectEntry,
    /// SkillEffectCondition or other legacy effect authoring at runtime.
    /// </summary>
    public enum MixedAbilityConditionKind
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

    public enum MixedAbilityGateVerdict
    {
        Execute=0,
        NotScheduled=1,
        ConditionFailed=2
    }

    public static class MixedAbilityConditionGate
    {
        public static bool IsValid(MixedOperationEntry op)
        {
            if(op==null || !Enum.IsDefined(typeof(MixedAbilityConditionKind),
                op.Condition))return false;
            if(op.RestrictToRoll && op.RollNumber<1)return false;
            if(op.Condition==MixedAbilityConditionKind.TargetNumericStatusAtLeast)
                return op.RequiredNumericValue>0 &&
                    StatusEffectFactory.GetStorageKind(op.RequiredStatus)==
                        StatusEffectStorageKind.NumericTimed;
            return true;
        }

        public static MixedAbilityGateVerdict Evaluate(
            MixedOperationEntry op,SkillEffectContext context)
        {
            if(!IsValid(op))
                throw new InvalidOperationException("Invalid R7-4 native operation guard.");
            if(op.RestrictToRoll &&
                (context==null||context.RollNumber!=op.RollNumber))
                return MixedAbilityGateVerdict.NotScheduled;
            if(op.Condition==MixedAbilityConditionKind.None)
                return MixedAbilityGateVerdict.Execute;
            if(context==null)return MixedAbilityGateVerdict.ConditionFailed;
            bool meets=op.Condition switch
            {
                MixedAbilityConditionKind.RollSucceeded=>context.RollSucceeded,
                MixedAbilityConditionKind.RollFailed=>
                    context.RollNumber>0&&!context.RollSucceeded,
                MixedAbilityConditionKind.CriticalRoll=>
                    context.RollResult?.IsCritical==true,
                MixedAbilityConditionKind.ClashOnly=>context.IsClash,
                MixedAbilityConditionKind.OneSidedOnly=>context.IsOneSided,
                MixedAbilityConditionKind.TargetHasStatus=>
                    StatusEffectFactory.HasStatus(context.Target,context.TargetPart,
                        op.RequiredStatus),
                MixedAbilityConditionKind.TargetNumericStatusAtLeast=>
                    StatusEffectFactory.GetNumericTotal(context.Target,
                        context.TargetPart,op.RequiredStatus)>=op.RequiredNumericValue,
                _=>false
            };
            return meets?MixedAbilityGateVerdict.Execute:
                MixedAbilityGateVerdict.ConditionFailed;
        }
    }
}