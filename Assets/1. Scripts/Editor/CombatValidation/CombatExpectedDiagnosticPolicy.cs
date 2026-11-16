#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Exact R9 test-only diagnostic policy. A game/content error is never
/// suppressed globally: only an intentionally asserted error emitted by the
/// Damage R5 fixed-tie test is expected. Unexpected errors, repeated expected
/// errors and missing expected errors fail the gate.
/// </summary>
public static class CombatExpectedDiagnosticPolicy
{
    public sealed class Verdict
    {
        public readonly int ExpectedCount,UnexpectedCount;
        public readonly bool MissingExpected;
        public readonly string Detail;
        public bool Accepted=>UnexpectedCount==0&&!MissingExpected;
        public Verdict(int expected,int unexpected,bool missing,string detail)
        {
            ExpectedCount=expected;UnexpectedCount=unexpected;
            MissingExpected=missing;Detail=detail;
        }
    }
    public static Verdict Evaluate(string gateId,IReadOnlyList<string> entries)
    {
        if(entries==null)throw new ArgumentNullException(nameof(entries));
        int expected=0,unexpected=0;
        bool testTie=string.Equals(gateId,"Damage.Stagger",StringComparison.Ordinal);
        foreach(var entry in entries)
        {
            if(string.IsNullOrEmpty(entry))continue;
            bool error=entry.StartsWith("Error ",StringComparison.Ordinal)||
                entry.StartsWith("Exception ",StringComparison.Ordinal)||
                entry.Contains("] FAIL ");
            if(!error)continue;
            bool match=testTie &&
                entry.StartsWith("Error [1029 DMG-07][CONTENT_ERROR] ",StringComparison.Ordinal) &&
                entry.Contains("first=10, second=10") &&
                entry.Contains("exchange=0") &&
                entry.Contains("rerolls=");
            if(match)expected++;
            else unexpected++;
        }
        // The fixed-tie gate itself asserts the logged diagnostic; require
        // exactly one emission as further protection against false passes.
        bool missing=testTie&&expected!=1;
        if(expected>1)unexpected+=expected-1;
        return new Verdict(expected,unexpected,missing,
            "expected="+expected+" unexpected="+unexpected+
            " missingOrRepeated="+missing);
    }
}
#endif