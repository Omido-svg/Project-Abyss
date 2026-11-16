using System;

namespace ProjectAbyss.Abilities
{
    public enum CombatFailureReason
    {
        None, TargetUnavailable, SlotDestroyed, InsufficientResource, GateNotMet,
        InvalidRequest, UnsupportedOperation, TechnicalFailure, UnknownCommit
    }
    public enum PartTransitionOutcome { None, Weakened, Broken }
    /// <summary>One committed impact; HP, part HP, stagger and absorption remain independent.</summary>
    public readonly struct CombatAppliedValues
    {
        public readonly bool HasDamageResult, HitLanded;
        public readonly int AppliedHp, AppliedPartHp, AppliedStagger;
        public readonly int HpBefore, HpAfter, PartHpBefore, PartHpAfter, StaggerBefore, StaggerAfter;
        public readonly bool HasResourceResult;
        public readonly int ResourceBefore, ResourceAfter;
        public readonly PartTransitionOutcome PartTransition;
        public readonly bool KilledActor, OpenedVulnerabilityWindow;
        private CombatAppliedValues(DamageContext result)
        {
            HasDamageResult=result?.Result!=null;
            HitLanded=HasDamageResult && (result.WasApplied || result.GuardAbsorbed>0 ||
                result.StaggerDamageApplied>0 || result.WasWeakened || result.WasBroken || result.WasKilled);
            AppliedHp=Math.Max(0,result?.AppliedHpDamage??0);
            AppliedPartHp=Math.Max(0,result?.AppliedPartDamage??0);
            AppliedStagger=Math.Max(0,result?.StaggerDamageApplied??0);
            HpBefore=result?.TargetHpBefore??0; HpAfter=result?.TargetHpAfter??0;
            PartHpBefore=result?.TargetPartHpBefore??0; PartHpAfter=result?.TargetPartHpAfter??0;
            StaggerBefore=result?.StaggerGaugeBefore??0; StaggerAfter=result?.StaggerGaugeAfter??0;
            PartTransition=result?.WasBroken==true?PartTransitionOutcome.Broken:
                result?.WasWeakened==true?PartTransitionOutcome.Weakened:PartTransitionOutcome.None;
            KilledActor=result?.WasKilled==true; OpenedVulnerabilityWindow=result?.OpenedVulnerabilityWindow==true;
            HasResourceResult=false; ResourceBefore=0; ResourceAfter=0;
        }
        private CombatAppliedValues(int before,int after)
        {
            this=default; HasResourceResult=true; ResourceBefore=before; ResourceAfter=after;
        }
        public static CombatAppliedValues FromDamage(DamageContext result)=>new CombatAppliedValues(result);
        public static CombatAppliedValues FromResource(int before,int after)=>new CombatAppliedValues(before,after);
    }
}
