#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>R9's expected diagnostic is exact and scoped to a single gate.
/// Every unexpected Error/Exception must still fail, even when all assertion
/// counters in an old gate show PASS.</summary>
public static class R9ExpectedDiagnosticPolicyVerification20261102
{
    [MenuItem("Game System Verification/R9 - Expected Diagnostic Fail-Closed Policy (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode");
        const string expected="Error [1029 DMG-07][CONTENT_ERROR] "+
            "고정 굴림이 항상 동률입니다 (first=10, second=10) "+
            "/ first=올라프, second=정예 몹, exchange=0, rerolls=1";
        var pass=0;var fail=0;
        void Check(string name,bool ok)
        {
            if(ok){pass++;Debug.Log("[R9-Policy] PASS "+name);}
            else{fail++;Debug.LogError("[R9-Policy] FAIL "+name);}
        }
        var clear=R9ExpectedDiagnosticPolicy20261102.Evaluate(
            "R5.Command",new[]{"Log [R5] RESULT PASS=12 FAIL=0"});
        Check("P01 normal PASS summary accepted",clear.Accepted);
        var good=R9ExpectedDiagnosticPolicy20261102.Evaluate(
            "Damage.Stagger",new[]{expected,"Log [Damage R5] RESULT PASS=35 FAIL=0"});
        Check("P02 one exact expected fixed tie content error accepted",
            good.Accepted && good.ExpectedCount==1 && good.UnexpectedCount==0);
        Check("P03 missing expected diagnostic fails closed",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Stagger",Array.Empty<string>()).Accepted);
        Check("P04 same log is NOT exempted at another gate",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Roll",new[]{expected}).Accepted);
        Check("P05 repeated expected error fails closed",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Stagger",new[]{expected,expected}).Accepted);
        Check("P06 unrelated error alongside expected fails closed",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Stagger",new[]{expected,"Error NullReferenceException"}).Accepted);
        Check("P07 unexpected exception always fails",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Stagger",new[]{expected,"Exception stacktrace"}).Accepted);
        Check("P08 near-match without fixed-tie signature fails",
            !R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "Damage.Stagger",new[]{
                    expected.Replace("first=10, second=10","first=10, second=11")}).Accepted);
        Check("P09 error-free other gate stays accepted",
            R9ExpectedDiagnosticPolicy20261102.Evaluate(
                "R7.GaugeParity",Array.Empty<string>()).Accepted);
        Debug.Log("[R9-Policy] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R9 diagnostic policy invalid");
    }
}
#endif