#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeBleedingCohortVerification20261030
{
    private static readonly string[] Cards=
    {
        "olaf.duel.catch_off_guard","olaf.duel.no_retreat",
        "olaf.duel.single_combat","olaf.duel.single_cut",
        "olaf.normal.desperate_blow","olaf.normal.flurry",
        "olaf.normal.smash","olaf.normal.split",
        "olaf.prestige.rune_carving"
    };
    private static ActionSlot Slot(BattleContext battle,Character owner,
        Character target,SkillDefinition data,long id)
    {
        var skill=data.CreateRuntimeSkill();
        skill.Initialize(owner,battle._battleEvent);
        return new ActionSlot {
            ActionId=id,Owner=owner,TargetCharacter=target,TargetPart=null,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,Phase=ActionPhase.COMBAT,SlotId="R7-BLEED-"+id
        };
    }
    [MenuItem("Game System Verification/R7 - 9 Native Bleeding Status Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=battle?.Services?.R7NativeBleedingMigration;
        var source=battle?.Player;
        var enemies=battle?.Enemies?.Where(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts)
            .Take(2).ToArray();
        if(router==null||source==null||enemies?.Length!=2)
            throw new InvalidOperationException("Live battle + two partless targets required.");
        var catalog=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(x=>x!=null).ToArray();
        var dispType=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var disp=Activator.CreateInstance(dispType,true);
        var method=dispType.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(x=>x.Name=="Execute"&&
                x.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Execute(SkillDefinition d,
            BattleAction act,SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)method.Invoke(disp,
                new object[]{d,act,timing,1,null,null,null,null,-1,null,
                    false,false,false,null});

        int pass=0,fail=0;
        void Check(string label,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-Bleed] PASS "+label+" "+detail);}
            else{fail++;Debug.LogError("[R7-Bleed] FAIL "+label+" "+detail);}
        }
        bool original=router.Enabled;
        try
        {
            Check("B00 factory initializes bleeding native cohort enabled",
                original);
            router.Enabled=false;
            Check("B01 exactly 9 native candidate IDs approved",
                R7ProductionNativeBleedingMigration.ApprovedSkillCount==9);
            Check("B02 legacy default disabled during validation",!router.Enabled);
            Check("B03 preexisting bleeding stacks are equal",
                (enemies[0].GetStatus<Bleeding>()?.Stack??0)==
                (enemies[1].GetStatus<Bleeding>()?.Stack??0));
            int commitBefore=router.NativeCommits;
            for(int i=0;i<Cards.Length;i++)
            {
                var id=Cards[i];
                var d=catalog.Single(x=>x.SkillId==id);
                var entry=d.EffectEntries.Single();
                var timing=entry.EffectiveTiming;
                int baseline=enemies[0].GetStatus<Bleeding>()?.Stack??0;
                int modern=enemies[1].GetStatus<Bleeding>()?.Stack??0;
                Check("B"+(i+1)+"a asset "+id+" parity precondition",
                    baseline==modern);
                var old=new BattleAction{Slot=Slot(battle,source,enemies[0],d,999400+i*2)};
                var newAction=new BattleAction{Slot=Slot(battle,source,enemies[1],d,999401+i*2)};
                router.Enabled=false;
                var before=Execute(d,old,timing);
                router.Enabled=true;
                Check("B"+(i+1)+"b "+id+" strict metadata route",router.CanRoute(d));
                var after=Execute(d,newAction,timing);
                int oldStack=enemies[0].GetStatus<Bleeding>()?.Stack??0;
                int newStack=enemies[1].GetStatus<Bleeding>()?.Stack??0;
                Check("B"+(i+1)+"c "+id+" status stack parity",
                    oldStack==newStack && oldStack>=baseline,
                    "legacy="+oldStack+" native="+newStack);
                Check("B"+(i+1)+"d "+id+" status applied exactly once",
                    router.NativeCommits==commitBefore+1 &&
                    before.Count==after.Count &&
                    before.Zip(after,(x,y)=>x.State==y.State).All(x=>x),
                    "commits="+(router.NativeCommits-commitBefore)+
                    " oldResult="+string.Join("|",before.Select(x=>x.State))+
                    " newResult="+string.Join("|",after.Select(x=>x.State)));
                commitBefore=router.NativeCommits;
            }
            Check("B04 9 status commands actually committed",router.NativeCommits==9);
            var duplicate=catalog.First(x=>x.SkillId=="olaf.duel.standard");
            Check("B05 ambiguous stable ID never routed",!router.CanRoute(duplicate));
            var resource=catalog.First(x=>x.SkillId=="common.preparation.crouch");
            Check("B06 resource effects not routed by bleeding migration",
                !router.CanRoute(resource));
            var gated=catalog.Single(x=>x.SkillId==Cards[0]);
            var wrong=new BattleAction{Slot=Slot(battle,source,enemies[1],gated,999460)};
            int beforeWrong=router.NativeAttempts;
            int stackBeforeWrong=enemies[1].GetStatus<Bleeding>()?.Stack??0;
            var result=Execute(gated,wrong,SkillEffectTiming.OnExecute);
            Check("B07 incorrect timing cannot apply bleeding",
                router.NativeAttempts==beforeWrong &&
                stackBeforeWrong==(enemies[1].GetStatus<Bleeding>()?.Stack??0) &&
                result.All(x=>!x.WasApplied));
            router.Enabled=false;
            Check("B08 rollback toggle excludes production native route",
                !router.CanRoute(gated));
        }
        finally{router.Enabled=original;}
        Debug.Log("[R7-Bleed] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 bleeding parity failed "+fail);
    }
}
#endif