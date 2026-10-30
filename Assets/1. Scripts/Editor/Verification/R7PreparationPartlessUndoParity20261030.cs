#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7PreparationPartlessUndoParity20261030
{
    private static ActionSlot Make(BattleContext battle,Character owner,
        Character enemy,SkillDefinition definition,long actionId)
    {
        var skill=definition.CreateRuntimeSkill();
        skill.Initialize(owner,battle._battleEvent);
        return new ActionSlot{
            Owner=owner,TargetCharacter=enemy,TargetPart=null,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,ActionId=actionId,Phase=ActionPhase.PRETURN,
            SlotId="R7-PREP-PARTLESS-"+actionId};
    }
    private static (int Bleed,int Burn,int BleedDuration,int BurnDuration) Snapshot(Character c)
    {
        var b=c.GetStatus<Bleeding>();var f=c.GetStatus<Burn>();
        return (b?.Stack??0,f?.Stack??0,b?.Duration??0,f?.Duration??0);
    }
    [MenuItem("Game System Verification/R7 - Preparation Partless Undo Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Play Mode required.");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=battle?.Services?.R7PreparationStatusMigration;
        var enemies=battle?.Enemies?.Where(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts)
            .Take(2).ToArray();
        var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath("f9c5728c648630849a9ba0afe641d02a"));
        var owner=battle?.Player;
        if(router==null||enemies?.Length!=2||owner==null||d==null)
            throw new InvalidOperationException("Need two partless enemies.");
        bool enabled=router.Enabled;
        int pass=0,fail=0;
        void Check(string name,bool ok,string info="")
        {
            if(ok){pass++;Debug.Log("[R7-PrepNone] PASS "+name+" "+info);}
            else{fail++;Debug.LogError("[R7-PrepNone] FAIL "+name+" "+info);}
        }
        try
        {
            Check("N01 production service enabled",enabled);
            var a=enemies[0];var b=enemies[1];
            var baselineA=Snapshot(a);var baselineB=Snapshot(b);
            Check("N02 initial partless states are equal",baselineA==baselineB);
            var original=Make(battle,owner,a,d,999620);
            var modern=Make(battle,owner,b,d,999621);
            router.Enabled=false;
            original.Skill.Execute(new BattleAction{Slot=original});
            router.Enabled=true;
            int beforeCommit=router.NativeCommits;
            modern.Skill.Execute(new BattleAction{Slot=modern});
            Check("N03 both partless statuses match",
                Snapshot(a)==Snapshot(b)&&Snapshot(a).Bleed>baselineA.Bleed &&
                Snapshot(a).Burn>baselineA.Burn,
                "legacy="+Snapshot(a)+" native="+Snapshot(b));
            Check("N04 exactly two native R5 commands committed",
                router.NativeCommits-beforeCommit==2);
            Check("N05 both journals recorded status undo",
                original.PlanningUndo.Count==2&&modern.PlanningUndo.Count==2);
            Check("N06 status is on Character anchor rather than Part",
                a.GetStatus<Bleeding>()!=null&&b.GetStatus<Bleeding>()!=null);
            var undoA=original.PlanningUndo.RollbackAll(out var errorA);
            var undoB=modern.PlanningUndo.RollbackAll(out var errorB);
            Check("N07 both character undo operations succeeded",
                undoA&&undoB,errorA+" / "+errorB);
            Check("N08 character status removed/restored exactly",
                Snapshot(a)==baselineA && Snapshot(b)==baselineB,
                "legacy="+Snapshot(a)+" native="+Snapshot(b));
            router.Enabled=false;
            Check("N09 manual disable preserves legacy eligibility",
                !router.CanRoute(d));
        }
        finally{router.Enabled=enabled;}
        Debug.Log("[R7-PrepNone] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("Partless prep parity failed "+fail);
    }
}
#endif