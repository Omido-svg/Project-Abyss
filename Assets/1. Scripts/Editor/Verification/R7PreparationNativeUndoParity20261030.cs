#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7PreparationNativeUndoParity20261030
{
    private static ActionSlot Make(BattleContext battle,Hifumi who,
        BodyPart part,SkillDefinition definition,long id)
    {
        var runtime=definition.CreateRuntimeSkill();
        runtime.Initialize(who,battle._battleEvent);
        return new ActionSlot
        {
            Owner=who,TargetCharacter=who,TargetPart=part,Part=part,
            ActionId=id,SlotId="R7-PREP-UNDO-"+id,
            Phase=ActionPhase.PRETURN,Skill=runtime
        };
    }
    private static (int Bleed,int Burn,int BDur,int UDur) Status(BodyPart part)
    {
        var b=part?.StatusEffects?.OfType<Bleeding>().FirstOrDefault();
        var u=part?.StatusEffects?.OfType<Burn>().FirstOrDefault();
        return (b?.Stack??0,u?.Stack??0,b?.Duration??0,u?.Duration??0);
    }
    [MenuItem("Game System Verification/R7 - Preparation Status Undo Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Requires Play Mode.");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=battle?.Services?.R7PreparationStatusMigration;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath("f9c5728c648630849a9ba0afe641d02a"));
        if(battle==null||router==null||prefab==null||d==null)
            throw new InvalidOperationException("Missing prep asset or battle.");
        var oldGo=UnityEngine.Object.Instantiate(prefab);
        var newGo=UnityEngine.Object.Instantiate(prefab);
        oldGo.name="R7_PREP_OLD_TARGET";newGo.name="R7_PREP_NATIVE_TARGET";
        var old=oldGo.GetComponent<Hifumi>();
        var modern=newGo.GetComponent<Hifumi>();
        old.Initialize(battle);modern.Initialize(battle);
        var a=old.GetTargetPoints(false).First(x=>x.IsValid&&x.Part!=null).Part;
        var b=modern.GetTargetPoints(false).First(x=>x.IsValid&&x.Part!=null).Part;
        int pass=0,fail=0;
        void Check(string name,bool ok,string details="")
        {
            if(ok){pass++;Debug.Log("[R7-Prep] PASS "+name+" "+details);}
            else{fail++;Debug.LogError("[R7-Prep] FAIL "+name+" "+details);}
        }
        bool wasEnabled=router.Enabled;
        try
        {
            Check("P01 approved native preparation default enabled",
                router.Enabled&&R7ProductionPreparationStatusMigration.SkillId==d.SkillId);
            router.Enabled=false;
            Check("P02 starting status identical",Status(a)==Status(b));
            var before=Status(a);
            var firstLegacy=Make(battle,old,a,d,999310);
            var firstModern=Make(battle,modern,b,d,999311);
            router.Enabled=false;
            firstLegacy.Skill.Execute(new BattleAction{Slot=firstLegacy});
            router.Enabled=true;
            Check("P03 explicit native route enabled",router.CanRoute(d));
            int nativeCommitsAtStart=router.NativeCommits;
            firstModern.Skill.Execute(new BattleAction{Slot=firstModern});
            Check("P04 two native status Commands committed",
                router.NativeCommits==nativeCommitsAtStart+2,
                "delta="+(router.NativeCommits-nativeCommitsAtStart));
            Check("P05 Bleeding+Burn after first apply equal",
                Status(a)==Status(b)&&Status(a).Bleed>before.Bleed&&
                Status(a).Burn>before.Burn,
                "legacy="+Status(a)+" native="+Status(b));
            Check("P17 legacy setup effect (guard and clash power) still matches",
                (old.RuntimeStatus?.currentBlock??0)==
                (modern.RuntimeStatus?.currentBlock??0) &&
                old.TurnClashPowerBonus==modern.TurnClashPowerBonus,
                "block="+(old.RuntimeStatus?.currentBlock??0)+"/"+
                    (modern.RuntimeStatus?.currentBlock??0)+
                " clash="+old.TurnClashPowerBonus+"/"+modern.TurnClashPowerBonus);
            Check("P06 both first use journals record TWO statuses",
                firstLegacy.PlanningUndo.Count==2 &&
                firstModern.PlanningUndo.Count==2,
                "legacy="+firstLegacy.PlanningUndo.Count+" modern="+firstModern.PlanningUndo.Count);
            var first=Status(a);
            var secondLegacy=Make(battle,old,a,d,999312);
            var secondModern=Make(battle,modern,b,d,999313);
            router.Enabled=false;
            secondLegacy.Skill.Execute(new BattleAction{Slot=secondLegacy});
            router.Enabled=true;
            secondModern.Skill.Execute(new BattleAction{Slot=secondModern});
            Check("P07 merged statuses match after second activation",
                Status(a)==Status(b)&&Status(a).Bleed>first.Bleed,
                "legacy="+Status(a)+" native="+Status(b));
            Check("P18 third legacy setup effect remains identical after two activations",
                (old.RuntimeStatus?.currentBlock??0)==
                (modern.RuntimeStatus?.currentBlock??0) &&
                old.TurnClashPowerBonus==modern.TurnClashPowerBonus);
            Check("P08 both second journals record TWO merge undo operations",
                secondLegacy.PlanningUndo.Count==2&&
                secondModern.PlanningUndo.Count==2);
            Check("P09 four new native commits across two activations",
                router.NativeCommits==nativeCommitsAtStart+4);
            bool undoSecondLeft=secondLegacy.PlanningUndo.RollbackAll(out var reasonL);
            bool undoSecondRight=secondModern.PlanningUndo.RollbackAll(out var reasonR);
            Check("P10 merge undo completed successfully",
                undoSecondLeft&&undoSecondRight,reasonL+"/"+reasonR);
            Check("P11 merge undo restores exact stack AND duration",
                Status(a)==first && Status(b)==first,
                "legacy="+Status(a)+" native="+Status(b)+" expected="+first);
            bool undoFirstLeft=firstLegacy.PlanningUndo.RollbackAll(out reasonL);
            bool undoFirstRight=firstModern.PlanningUndo.RollbackAll(out reasonR);
            Check("P12 first undo completed successfully",
                undoFirstLeft&&undoFirstRight,reasonL+"/"+reasonR);
            Check("P13 new effects removed individually on first undo",
                Status(a)==before && Status(b)==before,
                "legacy="+Status(a)+" native="+Status(b));
            Check("P14 journals cleared after rollback",
                firstLegacy.PlanningUndo.Count==0&&
                firstModern.PlanningUndo.Count==0&&
                secondLegacy.PlanningUndo.Count==0&&
                secondModern.PlanningUndo.Count==0);

            // Real planning can reactivate the exact same slot after its Undo.
            int commitsBeforeRetry=router.NativeCommits;
            router.Enabled=false;
            firstLegacy.Skill.Execute(new BattleAction{Slot=firstLegacy});
            router.Enabled=true;
            firstModern.Skill.Execute(new BattleAction{Slot=firstModern});
            Check("P19 same slot and ActionId may legitimately reactivate",
                Status(a)==Status(b) && router.NativeCommits==commitsBeforeRetry+2,
                "legacy="+Status(a)+" native="+Status(b));
            bool resetOld=firstLegacy.PlanningUndo.RollbackAll(out var retryOld);
            bool resetNew=firstModern.PlanningUndo.RollbackAll(out var retryNew);
            Check("P20 retry rollback restores both anchors",
                resetOld&&resetNew&&Status(a)==before&&Status(b)==before,
                retryOld+"/"+retryNew);

            // Replacement ActionSlot preserves ActionId according to ActionManager.
            var replacedOld=Make(battle,old,a,d,999310);
            var replacedModern=Make(battle,modern,b,d,999311);
            commitsBeforeRetry=router.NativeCommits;
            router.Enabled=false;
            replacedOld.Skill.Execute(new BattleAction{Slot=replacedOld});
            router.Enabled=true;
            replacedModern.Skill.Execute(new BattleAction{Slot=replacedModern});
            Check("P21 replacement slot reusing ActionId commits new commands",
                Status(a)==Status(b)&&router.NativeCommits==commitsBeforeRetry+2);
            resetOld=replacedOld.PlanningUndo.RollbackAll(out retryOld);
            resetNew=replacedModern.PlanningUndo.RollbackAll(out retryNew);
            Check("P22 replacement slot rollback remains lossless",
                resetOld&&resetNew&&Status(a)==before&&Status(b)==before);
            Check("P23 prior slot journals remain empty",
                firstLegacy.PlanningUndo.Count==0&&
                firstModern.PlanningUndo.Count==0);

            Check("P15 current battle default is native enabled",
                wasEnabled==true);
            router.Enabled=false;
            Check("P16 opt-out returns to normal original dispatcher",
                !router.CanRoute(d));
        }
        finally
        {
            router.Enabled=wasEnabled;
            UnityEngine.Object.Destroy(oldGo);
            UnityEngine.Object.Destroy(newGo);
        }
        Debug.Log("[R7-Prep] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 Prep Undo parity failed "+fail);
    }
}
#endif