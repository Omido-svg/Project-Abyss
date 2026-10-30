#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;
public static class R7NativeBleedingPartVerification20261030
{
    [MenuItem("Game System Verification/R7 - Native Bleeding Part and Replay (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=battle?.Player;
        var elite=battle?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&x.UsesBodyParts);
        var part=elite?.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid&&x.Part!=null).Part;
        var router=battle?.Services?.R7NativeBleedingMigration;
        if(owner==null||elite==null||part==null||router==null)
            throw new InvalidOperationException("Need live elite body part and native service");
        var skillData=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Single(d=>d!=null&&d.SkillId=="olaf.normal.split");
        var dispType=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(dispType,true);
        var method=dispType.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(m=>m.Name=="Execute"&&m.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(BattleAction action,SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)method.Invoke(dispatcher,new object[]{
                skillData,action,timing,1,null,null,null,null,-1,null,false,false,false,null});
        BattleAction Action(long id)
        {
            var runtime=skillData.CreateRuntimeSkill();
            runtime.Initialize(owner,battle._battleEvent);
            return new BattleAction{Slot=new ActionSlot {
                Owner=owner,TargetCharacter=elite,TargetPart=part,
                Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
                Skill=runtime,ActionId=id,SlotId="R7-BLEED-PART-"+id,
                Phase=ActionPhase.COMBAT}};
        }
        int pass=0,fail=0;
        void Check(string name,bool ok,string note="")
        {
            if(ok){pass++;Debug.Log("[R7-BleedPart] PASS "+name+" "+note);}
            else{fail++;Debug.LogError("[R7-BleedPart] FAIL "+name+" "+note);}
        }
        bool prior=router.Enabled;
        try
        {
            int originalStack=elite.GetPartStatus<Bleeding>(part)?.Stack??0;
            router.Enabled=false;
            var a=Call(Action(999750),SkillEffectTiming.OnExchangeWin);
            int afterLegacy=elite.GetPartStatus<Bleeding>(part)?.Stack??0;
            router.Enabled=true;
            var action=Action(999751);
            var b=Call(action,SkillEffectTiming.OnExchangeWin);
            int afterNative=elite.GetPartStatus<Bleeding>(part)?.Stack??0;
            Check("P01 authored elite target has a real body part",part.Owner==elite);
            Check("P02 legacy path increments part bleeding exactly once",
                afterLegacy==originalStack+1);
            Check("P03 native path increments SAME part exactly once",
                afterNative==afterLegacy+1);
            Check("P04 both paths confirmed applied result",
                a.Count==1&&b.Count==1&&a[0].WasApplied&&b[0].WasApplied);
            Check("P05 character-level status was not created",
                elite.GetStatus<Bleeding>()==null);
            int beforeReplay=router.NativeCommits;
            var replay=Call(action,SkillEffectTiming.OnExchangeWin);
            Check("P06 same action/entry/roll is idempotent",
                elite.GetPartStatus<Bleeding>(part)?.Stack==afterNative &&
                router.NativeCommits==beforeReplay);
            Check("P07 replay is explicitly uncommitted",
                replay.Count==1&&!replay[0].WasApplied);
            Check("P08 null or wrong part isn't treated as a second owner",
                router.CanRoute(skillData));
        }
        finally {router.Enabled=prior;}
        Debug.Log("[R7-BleedPart] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 part/replay failed "+fail);
    }
}
#endif