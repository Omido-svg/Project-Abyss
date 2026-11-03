#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Isolated production composition root with Hifumi PLAYER, guaranteeing
/// auto R6 activation switches the real registered Hifumi mechanic away
/// from synchronous duplicate legacy counter execution.
/// </summary>
public static class R6ProductionHifumiActivationVerification20261102
{
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var active=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var enemy=active?.BattleContext?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        var source=prefab?.GetComponent<Hifumi>();
        if(source==null||enemy==null)
            throw new InvalidOperationException("Hifumi source + enemy fixture required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string details="")
        {
            if(ok){pass++;Debug.Log("[R6-HifumiStartup] PASS "+id+" | "+details);}
            else{fail++;Debug.LogError("[R6-HifumiStartup] FAIL "+id+" | "+details);}
        }
        using(var fixture=GameSystemVerificationFixture.Create(
            source,enemy,active.BattleRules))
        {
            var h=fixture.Player as Hifumi;
            var services=fixture.Context.Services;
            var f=typeof(HifumiMechanic).GetField("deferCountersToR6",
                BindingFlags.NonPublic|BindingFlags.Instance);
            bool Deferred() => f!=null &&
                (bool)f.GetValue(h?.HifumiMechanic);
            Check("H01 fixture uses production runtime factory and registered Hifumi",
                fixture.HasProductionServiceGraph()&&h?.HifumiMechanic?.IsRegistered==true);
            Check("H02 production service graph has all R6 registries",
                services?.ReactionScheduler!=null &&
                services.ReactionActionRegistry!=null &&
                services.SpecialistReactionRegistry!=null &&
                services.ReactionLifetimeRegistry!=null);
            bool activated=R6ProductionActivation20261102.Activate(fixture.Context);
            Check("H03 production startup actually enables stepwise clash coordinator",
                activated&&services.ActionResolver.IsR6StepwiseActive);
            Check("H04 production startup switches Hifumi to deferred ONLY",
                Deferred() && services.SpecialistReactionRegistry.PendingCount==0 &&
                services.ReactionScheduler.PendingCount==0,
                "synchronous legacy counter bypassed after registration");
            var id=services.ReactionScheduler.AllocateRequestId();
            var request=new ReactionRequest(id,9001,9001,9002,9001,
                R4LiveSlotQuery.ActorRuntimeId(h),
                "R6_HIFUMI_VALID",ReactionTimingCheckpoint.AfterClashCompleted,0,id);
            var queued=services.ReactionScheduler.Queue(request);
            Check("H05 live scheduler receives isolated Hifumi intent",
                queued.Accepted && services.ReactionScheduler.PendingCount==1);
            R6ProductionActivation20261102.Deactivate(fixture.Context);
            Check("H06 battle end deactivates stepwise and deferred mode",
                !services.ActionResolver.IsR6StepwiseActive && !Deferred());
            Check("H07 clears queues, registrations, lifetime and specialist state",
                services.ReactionScheduler.PendingCount==0 &&
                services.ReactionActionRegistry.PendingBindings==0 &&
                services.SpecialistReactionRegistry.PendingCount==0 &&
                services.ReactionLifetimeRegistry.PendingBindings==0);
            R6ProductionActivation20261102.Deactivate(fixture.Context);
            Check("H08 battle cleanup is idempotent",
                !services.ActionResolver.IsR6StepwiseActive &&
                services.ReactionScheduler.PendingCount==0);
        }
        Debug.Log("[R6-HifumiStartup] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 Hifumi production startup failed="+fail);
    }
}
#endif
