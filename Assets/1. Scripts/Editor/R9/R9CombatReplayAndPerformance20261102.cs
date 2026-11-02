#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R9 determinism/throughput gates use the real BattleRuntimeFactory and
/// ClashManager in disposable GameSystemVerificationFixture clones.
/// The exact same seed and authoring state must yield identical roll/damage
/// fingerprints. Never saves a scene, prefab, or authoring asset.
/// </summary>
public static class R9CombatReplayAndPerformance20261102
{
    private sealed class ProbeSkill : NormalSkill
    {
        private readonly int count;
        public ProbeSkill(string id,Character actor,BattleContext battle,int power,
            int rolls)
        {
            SkillName=id;BasePower=power;count=rolls;
            Resolver=new DiceResolver(1,6);
            Initialize(actor,battle._battleEvent);
        }
        public override int ExchangeRollCount=>count;
        public override void Execute(BattleAction action){}
    }
    private sealed class Sample
    {
        public string Fingerprint;
        public double Milliseconds;
        public long ManagedDelta;
        public int Exchanges;
        public bool RealServices;
    }
    private static ActionSlot Slot(Character owner,Character target,
        BattleContext battle,long id,int power,string name)
    {
        var own=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part;
        var dest=target.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part;
        return new ActionSlot{
            Owner=owner,TargetCharacter=target,ActionId=id,
            SlotId="R9-PROBE-"+id,Part=own,TargetPart=dest,
            Phase=ActionPhase.COMBAT,
            Skill=new ProbeSkill(name,owner,battle,power,3),
            ResourceCostCommitted=true
        };
    }
    private static Sample RunSeeded(int seed)
    {
        var host=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var baseContext=host?.BattleContext;
        var player=baseContext?.Player;
        var enemy=baseContext?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        if(player==null||enemy==null)
            throw new InvalidOperationException("Battle Test Scene roster unavailable");
        var original=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(seed);
            var watch=Stopwatch.StartNew();
            long bytesBefore=GC.GetTotalMemory(false);
            using(var fixture=GameSystemVerificationFixture.Create(player,enemy,host.BattleRules))
            {
                var context=fixture.Context;
                bool real=fixture.HasProductionServiceGraph();
                var attack=Slot(fixture.Player,fixture.Enemy,context,904401,6,"R9-ATTACK");
                var defense=Slot(fixture.Enemy,fixture.Player,context,904402,2,"R9-DEFEND");
                var result=fixture.ClashManager.ResolvePair(new ClashPair(attack,defense));
                if(result?.Exchanges==null||result.Exchanges.Count==0)
                    throw new InvalidOperationException("R9 no real ClashManager exchanges");
                var sb=new StringBuilder();
                sb.Append("pair|").Append(result.Exchanges.Count).Append("|");
                foreach(var e in result.Exchanges)
                {
                    sb.Append(e.ExchangeIndex).Append(',')
                      .Append(e.FirstRollResult?.RawValue??-1).Append(',')
                      .Append(e.SecondRollResult?.RawValue??-1).Append(',')
                      .Append(e.FirstClashPower).Append(',')
                      .Append(e.SecondClashPower).Append(',')
                      .Append(e.IsTie?1:0).Append(',')
                      .Append(e.WasCancelled?1:0).Append(',')
                      .Append(e.WinnerAction==null?0:
                         e.WinnerAction.ActionId==attack.ActionId?1:2).Append(',')
                      .Append(e.TotalDamage).Append(',')
                      .Append(e.MomentumShift).Append(';');
                }
                // Absolute character runtime IDs are intentionally excluded.
                sb.Append("|endHP|").Append(fixture.Player.CurrentHP)
                  .Append('|').Append(fixture.Enemy.CurrentHP)
                  .Append("|wins|").Append(result.FirstExchangeWins)
                  .Append('|').Append(result.SecondExchangeWins)
                  .Append("|dam|").Append(result.TotalDamage);
                string digest;
                using(var sha=SHA256.Create())
                    digest=BitConverter.ToString(sha.ComputeHash(
                       Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-","");
                watch.Stop();
                return new Sample{
                    Fingerprint=digest,
                    Milliseconds=watch.Elapsed.TotalMilliseconds,
                    ManagedDelta=GC.GetTotalMemory(false)-bytesBefore,
                    Exchanges=result.Exchanges.Count,
                    RealServices=real
                };
            }
        }
        finally {UnityEngine.Random.state=original;}
    }
    private static double Percentile(IEnumerable<double> source,double q)
    {
        var s=source.OrderBy(x=>x).ToArray();
        if(s.Length==0)return 0;
        return s[(int)Math.Ceiling(q*(s.Length-1))];
    }
    private static string Folder()
    {
        var path=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ProjectAbyss_R9_Integration_20261102");
        Directory.CreateDirectory(path);
        return path;
    }
    private static void Check(string name,bool test,ref int pass,ref int fail)
    {
        if(test){pass++;UnityEngine.Debug.Log("[R9-Replay] PASS "+name);}
        else{fail++;UnityEngine.Debug.LogError("[R9-Replay] FAIL "+name);}
    }

    [MenuItem("Game System Verification/R9 - Seeded Combat Determinism (Play Mode)")]
    public static void Determinism()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode only");
        int pass=0,fail=0;
        var before=UnityEngine.Random.state;
        var one=RunSeeded(119831);
        var two=RunSeeded(119831);
        var three=RunSeeded(928311);
        Check("D01 identical seeded production services",
            one.RealServices&&two.RealServices&&three.RealServices,ref pass,ref fail);
        Check("D02 identical seed yields identical battle fingerprint",
            one.Fingerprint==two.Fingerprint,ref pass,ref fail);
        Check("D03 actual exchanges executed in all replays",
            one.Exchanges>0&&one.Exchanges==two.Exchanges&&three.Exchanges>0,
            ref pass,ref fail);
        Check("D04 RNG state exactly restored after sandbox",
            UnityEngine.JsonUtility.ToJson(before)==
                UnityEngine.JsonUtility.ToJson(UnityEngine.Random.state),
            ref pass,ref fail);
        var report="R9 seed 119831 round1="+one.Fingerprint+
            "\nR9 seed 119831 round2="+two.Fingerprint+
            "\nR9 seed 928311 round3="+three.Fingerprint+
            "\nSeconds="+(one.Milliseconds+two.Milliseconds+three.Milliseconds)/1000.0+
            "\nResult="+pass+" PASS / "+fail+" FAIL\n";
        File.WriteAllText(Path.Combine(Folder(),"R9_DETERMINISTIC_REPLAY.txt"),
            report,new UTF8Encoding(false));
        UnityEngine.Debug.Log("[R9-Replay] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R9 deterministic replay failed");
    }

    [MenuItem("Game System Verification/R9 - Production Battle Performance Baseline (Play Mode)")]
    public static void Performance()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode only");
        int pass=0,fail=0;
        // Warmup runs capture initial JIT/asset-cache overhead separately.
        var warmup=new[]{RunSeeded(44001),RunSeeded(44002)};
        var samples=new List<Sample>();
        for(int i=0;i<12;i++)samples.Add(RunSeeded(45000+i));
        Check("P01 twelve isolated production BattleRuntimeFactory/ClashManager runs",
            samples.Count==12&&samples.All(x=>x.RealServices&&x.Exchanges>0),
            ref pass,ref fail);
        Check("P02 no negative or invalid wall times",
            samples.All(x=>!double.IsNaN(x.Milliseconds)&&
                           !double.IsInfinity(x.Milliseconds)&&
                            x.Milliseconds>=0),ref pass,ref fail);
        var p50=Percentile(samples.Select(x=>x.Milliseconds),0.5);
        var p95=Percentile(samples.Select(x=>x.Milliseconds),0.95);
        Check("P03 valid p50/p95 distribution",p95>=p50&&p50>=0,
            ref pass,ref fail);
        var result="R9 baseline only — NO production SLA defined; "+
            "numbers include fixture construction, runtime service creation, "+
            "actual 3-roll ClashManager.ResolvePair, cleanup and Unity editor overhead.\n"+
            "Samples="+samples.Count+" warmup="+warmup.Length+
            "\nP50 ms="+p50.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            "\nP95 ms="+p95.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            "\nMAX ms="+samples.Max(x=>x.Milliseconds).ToString("F3",
                System.Globalization.CultureInfo.InvariantCulture)+
            "\nManagedHeapDeltaMeanBytes="+samples.Average(x=>x.ManagedDelta).ToString("F0",
                System.Globalization.CultureInfo.InvariantCulture)+
            "\nMIN exchanges="+samples.Min(x=>x.Exchanges)+
            "\nSeedSeries=45000..45011\n";
        File.WriteAllText(Path.Combine(Folder(),"R9_PERFORMANCE_BASELINE.txt"),
            result,new UTF8Encoding(false));
        UnityEngine.Debug.Log("[R9-Perf] "+result.Replace("\n","; "));
        UnityEngine.Debug.Log("[R9-Replay] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R9 perf baseline invalid");
    }
}
#endif