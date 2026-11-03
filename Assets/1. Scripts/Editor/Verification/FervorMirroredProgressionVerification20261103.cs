#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlayMode-only isolated FervorManager regression. Uses the existing battle's
/// characters/momentum, but does not replace live BattleContext.Services.FervorManager.
/// The scene is never saved. Stop PlayMode after running.
/// </summary>
public static class FervorMirroredProgressionVerification20261103
{
    public static void RunPlay()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Fervor mirror verification requires PlayMode.");

        var bm=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var ctx=bm?.BattleContext;
        var momentum=ctx?.Services?.MomentumManager;
        var player=ctx?.Player;
        var enemies=ctx?.Enemies?.Where(x=>x!=null&&!x.IsDead).ToArray();
        if (player==null || momentum==null || enemies==null || enemies.Length==0)
            throw new InvalidOperationException("BattleContext with living enemies required.");
        var settings=ctx.Rules.Fervor;
        if(settings.MaximumLevel!=3 ||
            settings.GetCostForNextLevel(0)!=4 ||
            settings.GetCostForNextLevel(1)!=8 ||
            settings.GetCostForNextLevel(2)!=10)
            throw new InvalidOperationException("Rules must match canonical 4/8/10 thresholds.");
        var subject=new FervorManager(ctx,momentum);
        int originalMomentum=momentum.CurrentMomentum;
        int originalPlayerMax=player.MaxEnergy;
        int[] enemyMax=enemies.Select(x=>x.MaxEnergy).ToArray();
        var records=new List<string>();
        int pass=0,fail=0,playerLevels=0,enemyLevels=0;
        void Check(string id,bool ok,string explanation)
        {
            if(ok)pass++;else fail++;
            records.Add((ok?"PASS":"FAIL")+" | "+id+" | "+explanation);
        }
        subject.LevelUp += info=>playerLevels++;
        subject.EnemyLevelUp += (owner,info)=>enemyLevels++;

        // No test action, clash, status, or SO is created or executed.
        // Only local FervorManager state and temporary PlayMode energies change.
        try
        {
            momentum.SetMomentumForDebug(-40);
            Check("F01 player loss gain is zero",subject.PreviewTurnEndGain(player)==0,
                "disadvantage => +0");
            Check("F02 enemy mirror of loss is advantage",
                enemies.All(e=>subject.PreviewTurnEndGain(e)==5),"enemy advantage +5 each");
            subject.ResolveTurnEnd(1);
            Check("F03 player did not gain under disadvantage",
                subject.Exaltation==0&&subject.FervorLevel==0,"player 0/0");
            Check("F04 every enemy independently reaches Lv1 from +5",
                enemies.All(e=>subject.GetFervorLevel(e)==1 &&
                    subject.GetExaltation(e)==1),"enemy cost 4, remainder 1");

            momentum.SetMomentumForDebug(0);
            Check("F05 neutral turn gives +2 to both perspectives",
                subject.PreviewTurnEndGain(player)==2&&
                enemies.All(e=>subject.PreviewTurnEndGain(e)==2),"neutral +2");
            subject.ResolveTurnEnd(2);
            subject.ResolveTurnEnd(3);
            Check("F06 player reaches Lv1 with exactly four neutral charge",
                subject.FervorLevel==1&&subject.Exaltation==0,"player Lv1/0");
            Check("F07 enemy per-unit balances continue",
                enemies.All(e=>subject.GetFervorLevel(e)==1&&
                subject.GetExaltation(e)==5),"enemy Lv1/5");

            momentum.SetMomentumForDebug(40);
            subject.ResolveTurnEnd(4);
            Check("F08 player advantage +5, enemy disadvantaged +0",
                subject.FervorLevel==1&&subject.Exaltation==5&&
                enemies.All(e=>subject.GetExaltation(e)==5),"player Lv1/5");

            momentum.SetMomentumForDebug(80);
            subject.ResolveTurnEnd(5);
            Check("F09 player +10 becomes Lv2, remains 7",
                subject.FervorLevel==2&&subject.Exaltation==7,"player Lv2/7");
            subject.ResolveTurnEnd(6);
            Check("F10 reaching Lv3 consumes all surplus charge",
                subject.FervorLevel==3&&subject.Exaltation==0,"player Lv3/0");
            Check("F11 each player level restores new energy maximum",
                player.MaxEnergy==originalPlayerMax+3&&
                player.CurrentEnergy==player.MaxEnergy,
                "player energy +3 and filled");

            int before=subject.Exaltation;
            subject.ResolveTurnEnd(7);
            Check("F12 Lv3 does not accumulate surplus",
                subject.Exaltation==before && subject.PreviewTurnEndGain(player)==0,
                "absorbing max level");

            momentum.SetMomentumForDebug(-80);
            subject.ResolveTurnEnd(8);
            Check("F13 mirrored enemy reach Lv2 with remainder 7",
                enemies.All(e=>subject.GetFervorLevel(e)==2&&
                subject.GetExaltation(e)==7),"per enemy Lv2/7");
            subject.ResolveTurnEnd(9);
            Check("F14 mirrored enemy reach Lv3 no surplus",
                enemies.All(e=>subject.GetFervorLevel(e)==3&&
                subject.GetExaltation(e)==0),"per enemy Lv3/0");
            subject.ResolveTurnEnd(10);
            Check("F15 enemy Lv3 no additional charge",
                enemies.All(e=>subject.GetExaltation(e)==0 &&
                    subject.PreviewTurnEndGain(e)==0),"absorbing enemy cap");
            Check("F16 enemy energy rewards apply per enemy",
                enemies.Select((e,i)=>e.MaxEnergy==enemyMax[i]+3 &&
                    e.CurrentEnergy==e.MaxEnergy).All(x=>x),
                "enemy +3 energy each / full");
            Check("F17 expected player and enemy level-up events",
                playerLevels==3&&enemyLevels==enemies.Length*3,
                "player="+playerLevels+" enemy="+enemyLevels);
            subject.ResetForBattle();
            Check("F18 reset clears all battle-local charge",
                subject.FervorLevel==0&&subject.Exaltation==0 &&
                enemies.All(e=>subject.GetFervorLevel(e)==0&&
                    subject.GetExaltation(e)==0),"reset");
        }
        finally
        {
            momentum.SetMomentumForDebug(originalMomentum);
        }

        string root=Directory.GetParent(Application.dataPath).FullName;
        string dir=Path.Combine(root,"ProjectAbyss_FervorMenuCleanup_Results_20261103");
        Directory.CreateDirectory(dir);
        var report=new StringBuilder()
            .AppendLine("# Canonical 1029 — Player / Enemy mirrored Fervor checks")
            .AppendLine("PASS="+pass+" FAIL="+fail)
            .AppendLine("Player gain: 0/0/2/5/10; mirrored enemy gain with shared momentum.")
            .AppendLine("Level costs: 4, 8, 10; cap: 3; any overflow discarded.")
            .AppendLine("PlayMode-only fixture; run in a clean play session.")
            .AppendLine("Elite/Boss fixed augment content is outside this progression contract.");
        foreach(var rec in records)report.AppendLine(rec);
        File.WriteAllText(Path.Combine(dir,"FERVOR_MIRROR_PLAY_GATE.md"),
            report.ToString(),new UTF8Encoding(false));
        Debug.Log("[FERVOR MIRROR] PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("Fervor mirror test FAIL="+fail);
    }
}
#endif