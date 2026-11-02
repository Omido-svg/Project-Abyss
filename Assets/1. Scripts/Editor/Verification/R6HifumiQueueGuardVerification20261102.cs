#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Deferred Hifumi cannot silently invoke legacy synchronous counter when R6
/// queue rejects a request (full queue, recursion limit or unsupported cause).
/// </summary>
public static class R6HifumiQueueGuardVerification20261102
{
    [MenuItem("Game System Verification/R6 - Deferred Hifumi Queue Failure Safety (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode only");
        var main=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var enemy=main?.BattleContext?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        var hifumiSource=prefab?.GetComponent<Hifumi>();
        if(hifumiSource==null||enemy==null)
            throw new InvalidOperationException("Hifumi + enemy fixtures required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string msg="")
        {
            if(ok){pass++;Debug.Log("[R6-QueueGuard] PASS "+id+" | "+msg);}
            else{fail++;Debug.LogError("[R6-QueueGuard] FAIL "+id+" | "+msg);}
        }
        using(var fixture=GameSystemVerificationFixture.Create(
            hifumiSource,enemy,main.BattleRules))
        {
            var h=fixture.Player as Hifumi;
            var target=fixture.Enemy;
            var scheduler=fixture.Context.Services.ReactionScheduler;
            var registry=fixture.Context.Services.SpecialistReactionRegistry;
            Check("Q01 actual Hifumi character registered",
                h?.HifumiMechanic?.IsRegistered==true);
            Check("Q02 R6 startup enables deferred mode",
                R6ProductionActivation20261102.Activate(fixture.Context));
            scheduler.MaxPendingRequests=1;
            var fill=new ReactionRequest(182001,180001,180001,180002,
                180001,R4LiveSlotQuery.ActorRuntimeId(h),"FILLER",
                ReactionTimingCheckpoint.AfterClashCompleted,0,1);
            Check("Q03 one unrelated request fills pending capacity",
                scheduler.Queue(fill).Accepted&&scheduler.PendingCount==1);
            var skill=h.RuntimeSkills.First(x=>
                x?.Definition?.SkillId==HifumiSkillIds.SmallChange);
            var part=h.BodyParts.First(x=>!x.IsBroken);
            var ownerSlot=new ActionSlot{
                ActionId=180010,Owner=h,Part=part,
                TargetCharacter=target,TargetPart=null,Skill=skill,
                Phase=ActionPhase.COMBAT,SlotId="R6-HIFUMI-GUARD"};
            var otherSlot=new ActionSlot{
                ActionId=180011,Owner=target,TargetCharacter=h,
                TargetPart=part,Skill=target.RuntimeSkills.FirstOrDefault(),
                Phase=ActionPhase.COMBAT,SlotId="R6-FOE-GUARD"};
            var attack=new BattleAction{Slot=ownerSlot};
            var defend=new BattleAction{Slot=otherSlot};
            var result=new ClashResultContext{
                FirstAction=attack,SecondAction=defend,IsClash=true,
                PairedExchangeCount=1};
            result.Exchanges.Add(new ClashExchangeResult{
                ExchangeIndex=0,FirstAction=attack,SecondAction=defend,
                WinnerAction=defend,LoserAction=attack,
                WasCancelled=false,IsOneSided=false,IsTie=false});
            int hp=target.CurrentHP;
            fixture.Context._battleEvent.RaiseClashResolved(result);
            Check("Q04 queue rejection never performs immediate counter damage",
                target.CurrentHP==hp,
                "target HP before="+hp+" after="+target.CurrentHP);
            Check("Q05 rejected specialist binding rolls back without orphan",
                scheduler.PendingCount==1 && registry.PendingCount==0);
            Check("Q06 no fabricated additional counter action",
                fixture.Context.Services.ReactionActionRegistry.PendingBindings==0);
            R6ProductionActivation20261102.Deactivate(fixture.Context);
            Check("Q07 shutdown clears original fill intent",
                scheduler.PendingCount==0 && registry.PendingCount==0);
        }
        Debug.Log("[R6-QueueGuard] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 queue guard failed "+fail);
    }
}
#endif
