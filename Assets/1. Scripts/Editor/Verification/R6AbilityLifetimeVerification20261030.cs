#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6AbilityLifetimeVerification20261030
{
    private static AbilityLifetimeEvent E(long turn=1,long clash=20,long roll=30,
        long hit=40,long cause=50) =>
        new AbilityLifetimeEvent(turn,clash,roll,hit,cause);

    private static AbilityExecutionResult Applied(int value=1) =>
        new AbilityExecutionResult(1,value,"");

    [MenuItem("Game System Verification/R6 - Lifetime Memory (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        int pass=0,fail=0;
        void Check(string id,bool yes)
        {
            if(yes){pass++;Debug.Log("[R6-Memory] PASS "+id);}
            else{fail++;Debug.LogError("[R6-Memory] FAIL "+id);}
        }

        var onceTurn=new AbilityLifetimeMemory(new AbilityLifetimeRules(oncePerTurn:true));
        var t1=E();
        Check("M01 query is pure/read-only",onceTurn.CanActivate(t1) &&
            onceTurn.SuccessfulCauseCount==0);
        Check("M02 planned only never commits",
            !onceTurn.CommitIfApplied(t1,Applied(0)) &&
            onceTurn.CanActivate(t1));
        Check("M03 actual commit is counted once",
            onceTurn.CommitIfApplied(t1,Applied()) &&
            onceTurn.SuccessfulCauseCount==1 &&
            onceTurn.ActivationCountThisTurn==1);
        Check("M04 another cause in same turn blocked by rule",
            !onceTurn.CanActivate(E(cause:51)));
        onceTurn.ResetAt(AbilityLifetimeBoundary.Turn);
        Check("M05 premature turn reset cannot bypass once-per-turn",
            !onceTurn.CanActivate(E(cause:52)));
        Check("M06 next real TurnId permits activation",
            onceTurn.CanActivate(E(turn:2,cause:53)) &&
            onceTurn.CommitIfApplied(E(turn:2,cause:53),Applied()) &&
            onceTurn.ActivationCountThisTurn==1);
        Check("M07 historical turn cannot re-activate",
            !onceTurn.CanActivate(E(turn:1,cause:54)));

        var perClash=new AbilityLifetimeMemory(new AbilityLifetimeRules(oncePerClash:true));
        Check("M08 same clash restricted, other clash accepted",
            perClash.CommitIfApplied(E(),Applied()) &&
            !perClash.CanActivate(E(cause:59)) &&
            perClash.CanActivate(E(clash:21,cause:60)));

        var roll=new AbilityLifetimeMemory(new AbilityLifetimeRules(oncePerRollUse:true));
        Check("M09 same used roll blocks tie-reroll, next roll passes",
            roll.CommitIfApplied(E(),Applied()) &&
            !roll.CanActivate(E(cause:61)) &&
            roll.CanActivate(E(roll:31,cause:62)));

        var hit=new AbilityLifetimeMemory(new AbilityLifetimeRules(oncePerOriginHit:true));
        Check("M10 same origin hit restricted, next origin hit allowed",
            hit.CommitIfApplied(E(),Applied()) &&
            !hit.CanActivate(E(cause:63)) &&
            hit.CanActivate(E(hit:41,cause:64)));

        var cooldown=new AbilityLifetimeMemory(new AbilityLifetimeRules(fullCooldownTurns:2));
        cooldown.CommitIfApplied(E(turn:3),Applied());
        Check("M11 two full cooldown turns blocked, third ready",
            cooldown.ReadyTurn==6 &&
            !cooldown.CanActivate(E(turn:4,cause:81)) &&
            !cooldown.CanActivate(E(turn:5,cause:82)) &&
            cooldown.CanActivate(E(turn:6,cause:83)));

        var accumulation=new AbilityLifetimeMemory(new AbilityLifetimeRules());
        var d1=new AppliedDamageEvidence(99,1001,1002,false,6,false,false,false);
        var d2=new AppliedDamageEvidence(99,1001,1003,false,8,false,false,false);
        Check("M12 accumulated applied HP triggers by threshold",
            accumulation.RegisterCommittedHpDamage(d1,10)==0 &&
            accumulation.AccumulatedAppliedHpDamage==6 &&
            accumulation.RegisterCommittedHpDamage(d2,10)==1 &&
            accumulation.AccumulatedAppliedHpDamage==4);
        Check("M13 duplicate same cause/hit not counted twice",
            accumulation.RegisterCommittedHpDamage(d2,10)==0 &&
            accumulation.AccumulatedAppliedHpDamage==4);
        Check("M14 only committed identifiable damage may accumulate",
            accumulation.RegisterCommittedHpDamage(
                new AppliedDamageEvidence(0,1001,1004,false,99,false,false,false),10)==0 &&
            accumulation.RegisterCommittedHpDamage(
                new AppliedDamageEvidence(105,1001,1005,false,0,false,false,false),10)==0 &&
            accumulation.AccumulatedAppliedHpDamage==4);
        var other=new AbilityLifetimeMemory(new AbilityLifetimeRules(oncePerTurn:true));
        Check("M15 two ability instances do not share memory",
            onceTurn.SuccessfulCauseCount==2 && other.SuccessfulCauseCount==0 &&
            other.CanActivate(E()));
        onceTurn.ResetAt(AbilityLifetimeBoundary.Battle);
        cooldown.ResetAt(AbilityLifetimeBoundary.Battle);
        accumulation.ResetAt(AbilityLifetimeBoundary.Battle);
        Check("M16 battle end releases activation, cooldown and damage memory",
            onceTurn.SuccessfulCauseCount==0 &&
            cooldown.ReadyTurn==0 &&
            accumulation.AccumulatedAppliedHpDamage==0 &&
            accumulation.RegisterCommittedHpDamage(d1,10)==0);

        Debug.Log("[R6-Memory] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 memory checks failed "+fail);
    }
}
#endif
