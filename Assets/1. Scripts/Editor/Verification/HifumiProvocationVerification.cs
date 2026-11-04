#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R7-7 continuation EditMode-only provocation contract.</summary>
public static class HifumiProvocationVerification
{
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("EditMode only");
        int pass=0;
        void Check(string name,bool ok)
        {
            if(!ok)throw new InvalidOperationException("[R7-7 Provocation] FAIL "+name);
            pass++;
        }
        var original=new CombatSlotRef(100,"ORIGINAL","HEAD",0);
        var first=new CombatSlotRef(201,"TAUNT-ARM","ARM",0);
        var second=new CombatSlotRef(201,"TAUNT-LEG","LEG",0);
        var attacks=new[]
        {
            new R4IncomingAim(31,12,5,original),
            new R4IncomingAim(32,8,1,original),
            new R4IncomingAim(33,10,3,original),
            new R4IncomingAim(34,4,2,original)
        };
        var taunts=new[]{
            new R4TauntTarget(first,2,true),
            new R4TauntTarget(second,1,true)
        };
        var result=R4TauntAimPlanner.Plan(attacks,taunts,20261103);
        Check("N=2 redirects 2 ATTACK ACTIONS, not rolls",
            result.Count(x=>x.WasForced&&x.ResolvedSlot.Equals(first))==2);
        Check("highest speeds 12,10 consume highest taunt first",
            result.Where(x=>x.ResolvedSlot.Equals(first))
              .Select(x=>x.ActionId).OrderBy(x=>x)
              .SequenceEqual(new long[]{31,33}));
        Check("next taunt receives next highest speed 8",
            result.Single(x=>x.ResolvedSlot.Equals(second)).ActionId==32);
        Check("surplus attack remains originally aimed",
            result.Single(x=>x.ActionId==34).ResolvedSlot.Equals(original)&&
            !result.Single(x=>x.ActionId==34).WasForced);
        var reversed=R4TauntAimPlanner.Plan(attacks.Reverse(),
            taunts.Reverse(),20261103);
        Check("stable seeded tie selection independent from input order",
            result.Select(x=>x.ActionId+":"+x.ResolvedSlot.PartId)
              .SequenceEqual(reversed.Select(x=>x.ActionId+":"+x.ResolvedSlot.PartId)));
        var invalid=R4TauntAimPlanner.Plan(attacks,
            new[]{new R4TauntTarget(first,99,false)},42);
        Check("invalid/untargetable part cannot steal attack",
            invalid.All(x=>!x.WasForced&&x.ResolvedSlot.Equals(original)));
        var nulls=R4TauntAimPlanner.Plan(Array.Empty<R4IncomingAim>(),taunts,3);
        Check("no attacking actions means no forced targets",nulls.Count==0);
        var paths=Directory.GetFiles(Application.dataPath,"*.cs",SearchOption.AllDirectories);
        var builder=File.ReadAllText(paths.Single(p=>Path.GetFileName(p)=="ClashBuilder.cs"));
        var router=File.ReadAllText(paths.Single(p=>Path.GetFileName(p)=="HifumiProvocationTargeting.cs"));
        var mechanic=File.ReadAllText(paths.Single(p=>Path.GetFileName(p)=="HifumiMechanic.cs"));
        Check("real execution queue calls native provocation",
            builder.Contains("HifumiProvocationTargeting.ApplyToExecutionSlots(combatSlots)"));
        Check("runtime reuses R4 planner and redirects TargetPart",
            router.Contains("R4TauntAimPlanner.Plan(")&&
            router.Contains("pair.Key.TargetPart=pair.Value;")&&
            router.Contains("PreviewTargetParts("));
        Check("no reassignment of unrelated targets",
            router.Contains("x.TargetCharacter==defender"));
        Check("provocation applies NEXT turn, then expires",
            mechanic.Contains("QueueProvocationNextTurn(")&&
            mechanic.Contains("ActivateQueuedProvocation();")&&
            mechanic.Contains("provocationActive.Clear();"));
        Check("original combat skill sources remain untouched",
            AssetDatabase.FindAssets("t:SkillDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkillDefinition>)
                .Where(x=>x!=null&&x.SkillId.StartsWith("hifumi.",StringComparison.Ordinal))
                .Count(x=>!EditorUtility.IsDirty(x))==20);
        Debug.Log("[R7-7 Provocation] PASS="+pass+" FAIL=0 EDIT_ONLY NO_R9 NO_PLAYMODE");
    }
}
#endif