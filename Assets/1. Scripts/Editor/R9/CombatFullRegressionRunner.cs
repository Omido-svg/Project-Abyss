#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R9 fast, single-session regression coordinator.
/// - Edit gates run once in EditMode; Play gates run sequentially in ONE PlayMode.
/// - Gate assertions, diagnostics, and per-gate logs are recorded independently.
/// - Shared runtime state is NOT reset by a scene reload between Play gates:
///   fixture-owning gates must clean up. Cross-gate failures are reported.
/// - The existing unsaved Battle Test Scene is never explicitly saved/reloaded.
/// </summary>
[InitializeOnLoad]
public static class CombatFullRegressionRunner
{
    private sealed class Gate
    {
        public readonly string Id,Type,Method;
        public Gate(string id,string type,string method="Run")
        {Id=id;Type=type;Method=method;}
    }
    private static readonly Gate[] Edit={
        new Gate("G0.Audit","AbilityBaselineAuditReport","RunAuditFromMenu"),
        new Gate("R1.Canonical","CanonicalContractsVerification"),
        new Gate("R2.Contracts","AbilityContractVerification","Verify"),
        new Gate("R2.Extended","ExtendedContractsVerification"),
        new Gate("R3.Condition","ConditionVerification","RunFromMenu"),
        new Gate("R3.Advanced","AdvancedConditionVerification"),
        new Gate("R4.Weights","SlotWeightedPlannerVerification"),
        new Gate("R4.Snapshot","PlanningSnapshotCacheVerification"),
        new Gate("R4.TargetContract","TargetPlanningContractVerification"),
        new Gate("R4.Policy","TargetPolicyVerification"),
        new Gate("R5.Command","CommandApplyVerification"),
        new Gate("R5.Reaction","ReactionCommandVerification"),
        new Gate("R6.Lifetime","AbilityLifetimeVerification"),
        new Gate("R6.Frames","IbClashFrameContractVerification"),
        new Gate("R6.Queue","ReactionSchedulerVerification"),
        new Gate("R6.SpecialistQueue","SpecialistQueueContractVerification"),
        new Gate("R6.LifetimePhase","LifetimeCheckpointVerification"),
        new Gate("R7.NativeSO","NativeSODataGate"),
        new Gate("R7.MixedSchema2","NativeMixedSchema2Migration","Verify"),
        new Gate("R7.UniqueGauge","NativeUniqueGaugeAssetMigration","Verify"),
        new Gate("R7.Preparation","NativePreparationSetupAssetMigration","Verify"),
        new Gate("R7.Coverage","NativeMigrationCoverageGate"),
        new Gate("R7.Stage0Ledger","ContentMigrationLedger"),
        new Gate("R7.Stage1Boundary","NativeAbilityBoundaryVerification"),
        new Gate("R7.AllContent","ContentFamilyInventory"),
        new Gate("R8.Converter","NativeAbilityAuthoringVerification"),
        new Gate("R8.AuthorValidator","SkillAuthoringValidation"),
        new Gate("R8.PilotIntegrity","SkillPilotIntegrityVerification"),
        new Gate("R8.EffectInventory","SkillEffectInventoryVerification"),
        new Gate("R9.ErrorPolicy","CombatDiagnosticPolicyVerification")
    };
    // Each Play gate runs on a separate editor tick within the same PlayMode.
    // No Enter/ExitPlayMode between gates; per-gate fixture cleanup is required.
    private static readonly Gate[] Play={
        new Gate("G0.Golden","AbilityBaselineAuditReport","RunGoldenBaselineFromMenu"),
        new Gate("R1.Timing","AbilityTimingVerification","RunFromMenu"),
        new Gate("R1.Live","ExtendedLiveTimingVerification"),
        new Gate("R1.CancelTie","TieAndCancellationVerification"),
        new Gate("R2.Triggered","TriggeredActionParityVerification"),
        new Gate("R2.Mark","NativeYujinMarkVerification"),
        new Gate("R3.LegacyReaction","ReactionAndLegacyVerification"),
        new Gate("R4.LiveTargets","LiveSlotQueryVerification"),
        new Gate("R4.Preview","PlanningPreviewParityVerification"),
        new Gate("R5.MultiTarget","MultiTargetDamageVerification"),
        new Gate("R5.MixedReaction","MixedDamageReactionVerification"),
        new Gate("R5.Planner","PlanToSpecialistVerification"),
        new Gate("R5.Specialist","SpecialistIntegrationVerification"),
        new Gate("R5.Weighted","SlotWeightedIntegrationVerification"),
        new Gate("R6.Action","ReactionActionExecutionVerification"),
        new Gate("R6.Specialist","SpecialistLiveDispatcherVerification"),
        new Gate("R6.HifumiStartup","ProductionHifumiActivationVerification"),
        new Gate("R6.QueueGuard","HifumiQueueGuardVerification"),
        new Gate("R6.Production","ProductionStepwiseVerification"),
        new Gate("R6.Presentation","IcStepPresentationVerification"),
        new Gate("R6.Insertion","IbRealExchangeInsertionVerification"),
        new Gate("R6.HifumiParity","HifumiDeferredParityVerification"),
        new Gate("R7.ResourceParity","NativeSOResourceParityVerification"),
        new Gate("R7.StatusParity","NativeSOStatusParityVerification"),
        new Gate("R7.MixedParity","CompatibilityMixedStatusParity"),
        new Gate("R7.MixedClash","CompatibilityMixedActualClashVerification"),
        new Gate("R7.GaugeParity","NativeUniqueGaugeParity"),
        new Gate("R7.PrepUndo","PreparationNativeUndoParity"),
        new Gate("R7.PrepPartless","PreparationPartlessUndoParity"),
        new Gate("Damage.Roll","DamageRollPowerVerification","VerifyFromMenu"),
        new Gate("Damage.Window","DamageVulnerabilityWindowVerification","VerifyFromMenu"),
        new Gate("Damage.Turn","DamageTurnEndSettlementVerification","VerifyFromMenu"),
        new Gate("Damage.OneSided","DamageOneSidedMomentumVerification","VerifyFromMenu"),
        new Gate("Damage.Stagger","DamageStaggerAndTiePolicyVerification","VerifyFromMenu"),
        new Gate("Damage.Special","DamageSpecialDamageAuditVerification","VerifyFromMenu"),
        new Gate("Damage.Reuse","DamageRecoveryTieAndReuseVerification","VerifyFromMenu"),
        new Gate("Damage.Final","DamageIntegrationCompletionVerification","VerifyFromMenu"),
        new Gate("R9.Deterministic","CombatReplayPerformanceVerification","Determinism"),
        new Gate("R9.Performance","CombatReplayPerformanceVerification","Performance")
    };
    private const string ActiveKey="ProjectAbyss.R9.Active.20261102";
    private const string IndexKey="ProjectAbyss.R9.Index.20261102";
    private const string InFlightKey="ProjectAbyss.R9.InFlight.20261102";
    private const string CompletedKey="ProjectAbyss.R9.Complete.20261102";
    private const string EnteredPlayKey="ProjectAbyss.R9.EnteredPlay.Fast.20261102";
    private const string ResetKey="ProjectAbyss.R9.RuntimeReset.Fast.20261102";
    private const string ResetCountKey="ProjectAbyss.R9.SceneResetCount.Fast.20261102";
    private static int warmupTicks;
    private static bool exitRequested;
    private static string Dir=>Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "ProjectAbyss_R9_Integration_20261102");
    private static string CsvPath=>Path.Combine(Dir,"R9_GATE_RESULTS.csv");
    public static string Status =>
        (SessionState.GetBool(ActiveKey,false)?"RUNNING":"STOPPED")+
        " PlayGates="+SessionState.GetInt(IndexKey,0)+"/"+Play.Length+
        " EditGates="+Edit.Length+" Csv="+CsvPath;
    static CombatFullRegressionRunner()
    {
        EditorApplication.playModeStateChanged+=OnPlayMode;
        EditorApplication.update+=OnEditorUpdate;
    }
    private static void EnsureReport()
    {
        Directory.CreateDirectory(Dir);
        if(!File.Exists(CsvPath))
            File.WriteAllText(CsvPath,
              "Phase,Gate,Status,AssertionsPass,AssertionsFail,ElapsedMs,Detail\n",
              new UTF8Encoding(false));
    }
    private static string Csv(string x)=>"\""+(x??"").Replace("\"","\"\"")
        .Replace("\r"," ").Replace("\n"," ")+"\"";
    private static void Record(string phase,Gate g,string status,int pass,
        int fail,double duration,string detail)
    {
        EnsureReport();
        File.AppendAllText(CsvPath,string.Join(",",new[]{
            phase,g.Id,status,pass.ToString(),fail.ToString(),
            duration.ToString("F2",CultureInfo.InvariantCulture),detail
        }.Select(Csv))+"\n",new UTF8Encoding(false));
        UnityEngine.Debug.Log("[R9-Suite] "+phase+" "+g.Id+" "+status+
            " pass="+pass+" fail="+fail+" ms="+duration.ToString("F2",
            CultureInfo.InvariantCulture)+" "+detail);
    }
    private static void InvokeGate(Gate g,string phase)
    {
        var logs=new List<string>();
        string thrown=null;
        Application.LogCallback cb=(message,trace,kind)=>
        {
            if(kind==LogType.Exception||kind==LogType.Error||
               message.Contains("RESULT")||message.Contains("SUMMARY")||
               message.Contains("DATA PASS=")||message.Contains("] FAIL "))
                logs.Add(kind+" "+message);
        };
        var watch=Stopwatch.StartNew();
        Application.logMessageReceived+=cb;
        try
        {
            var type=AppDomain.CurrentDomain.GetAssemblies()
                .Select(x=>x.GetType(g.Type)).FirstOrDefault(x=>x!=null);
            var method=type?.GetMethod(g.Method,
                BindingFlags.Static|BindingFlags.Public);
            if(method==null)throw new MissingMethodException(g.Type,g.Method);
            if(method.GetParameters().Length!=0)
                throw new InvalidOperationException("R9 Gate requires args: "+g.Id);
            method.Invoke(null,null);
        }
        catch(Exception e){thrown=(e.InnerException??e).ToString();}
        finally {Application.logMessageReceived-=cb;watch.Stop();}
        // Must see explicit assertion summary. A nonthrowing/no-assertion gate
        // is PENDING, never silently counted as PASS.
        int passed=0,failed=0;
        var matches=Regex.Matches(string.Join("\n",logs),
            @"(?:RESULT|DATA)\s+PASS=(\d+)\s+FAIL=(\d+)",
            RegexOptions.IgnoreCase);
        foreach(Match m in matches)
        {
            passed+=int.Parse(m.Groups[1].Value);
            failed+=int.Parse(m.Groups[2].Value);
        }
        var diagnostics=CombatExpectedDiagnosticPolicy.Evaluate(g.Id,logs);
        var status=thrown!=null||failed>0||!diagnostics.Accepted?"FAIL":
            matches.Count==0||passed<=0?"PENDING":"PASS";
        string detail=thrown??(matches.Count==0?"NO_ASSERTION_SUMMARY":
            !diagnostics.Accepted?"Diagnostic mismatch "+diagnostics.Detail:
            "verified "+matches.Count+" result summaries; "+diagnostics.Detail);
        Record(phase,g,status,passed,failed,watch.Elapsed.TotalMilliseconds,detail);
        File.WriteAllLines(Path.Combine(Dir,"gate_"+phase+"_"+g.Id.Replace('.','_')+".log"),
            logs,new UTF8Encoding(false));
    }
    private static void RunEdits()
    {
        foreach(var g in Edit)
            InvokeGate(g,"Edit");
    }
    private static void Preflight()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Start R9 in EditMode");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/5. Scenes/Battle Test Scene.unity")
            throw new InvalidOperationException("R9 only runs against Battle Test Scene");
        if(!scene.isDirty)
            UnityEngine.Debug.LogWarning("[R9-Suite] Scene is not dirty; nothing will be saved.");
        EnsureReport();
        var path=Path.Combine(Dir,"R9_SCENE_INITIAL.txt");
        File.WriteAllText(path,"Scene="+scene.path+"\nDirty="+scene.isDirty+
            "\nStartUTC="+DateTime.UtcNow.ToString("o")+
            "\nEditGates="+Edit.Length+" PlayGates="+Play.Length+
            "\nOne PlayMode session for all Play gates.\n",
            new UTF8Encoding(false));
    }

    [MenuItem("Game System Verification/R9 - Run EditMode Regression")]
    public static void RunEditOnly()
    {
        Preflight();
        if(SessionState.GetBool(ActiveKey,false))
            throw new InvalidOperationException("R9 already running");
        File.WriteAllText(CsvPath,"Phase,Gate,Status,AssertionsPass,AssertionsFail,ElapsedMs,Detail\n",
            new UTF8Encoding(false));
        RunEdits();
        WriteSummary("EDIT_ONLY");
    }

    [MenuItem("Game System Verification/R9 - Start FULL Fast Single-Play Regression")]
    public static void StartFull()
    {
        Preflight();
        if(SessionState.GetBool(ActiveKey,false))
            throw new InvalidOperationException("R9 already running");
        File.WriteAllText(CsvPath,"Phase,Gate,Status,AssertionsPass,AssertionsFail,ElapsedMs,Detail\n",
            new UTF8Encoding(false));
        RunEdits();
        SessionState.SetInt(IndexKey,0);
        SessionState.SetBool(InFlightKey,false);
        SessionState.SetBool(CompletedKey,false);
        SessionState.SetBool(EnteredPlayKey,false);
        SessionState.SetBool(ResetKey,false);
        SessionState.SetInt(ResetCountKey,0);
        warmupTicks=0;
        exitRequested=false;
        SessionState.SetBool(ActiveKey,true);
        WriteSummary("EDIT_COMPLETE_PLAY_PENDING");
        EditorApplication.delayCall+=StartNextPlay;
    }

    private static void OnEditorUpdate()
    {
        if(exitRequested && EditorApplication.isPlaying &&
           !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            exitRequested=false;
            EditorApplication.isPlaying=false;
            return;
        }

        if(!SessionState.GetBool(ActiveKey,false))return;
        if(SessionState.GetBool(InFlightKey,false))return;

        if(!EditorApplication.isPlaying)
        {
            if(!SessionState.GetBool(EnteredPlayKey,false) &&
               !EditorApplication.isPlayingOrWillChangePlaymode)
                StartNextPlay();
            return;
        }

        if(SessionState.GetBool(ResetKey,false))
        {
            SessionState.SetBool(ResetKey,false);
            try
            {
                // Reload only the RUNTIME scene, not the unsaved Editor scene.
                // This re-creates BattleManager/roster without exiting PlayMode
                // or causing the expensive assembly/domain reload.
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    "Battle Test Scene",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
                SessionState.SetInt(ResetCountKey,
                    SessionState.GetInt(ResetCountKey,0)+1);
                warmupTicks=3;
            }
            catch(Exception e)
            {
                int index=SessionState.GetInt(IndexKey,0);
                if(index<Play.Length)
                    Record("Play",Play[index],"FAIL",0,1,0,
                        "Runtime scene reset failed: "+e);
                SessionState.SetBool(ActiveKey,false);
                WriteSummary("ABORTED_RUNTIME_SCENE_RESET");
                exitRequested=true;
            }
            return;
        }

        if(warmupTicks>0){warmupTicks--;return;}
        ExecuteCurrentPlayGate();
    }

    private static void StartNextPlay()
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SessionState.GetInt(IndexKey,0)>=Play.Length)
        {
            Finish("COMPLETE");
            return;
        }
        // One entry into PlayMode, irrespective of the number of Play gates.
        EditorApplication.isPlaying=true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            // One PlayMode session; run once the runtime scene is initialized.
            SessionState.SetBool(EnteredPlayKey,true);
            warmupTicks=3;
        }
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            // Unexpected exit is an interruption, never resume/re-enter silently.
            SessionState.SetBool(ActiveKey,false);
            SessionState.SetBool(InFlightKey,false);
            WriteSummary("INTERRUPTED_PLAYMODE_EXIT");
            UnityEngine.Debug.LogError("[R9-Suite] PlayMode left before all Play gates completed.");
        }
    }

    private static void ExecuteCurrentPlayGate()
    {
        if(!SessionState.GetBool(ActiveKey,false)||!Application.isPlaying)return;
        if(SessionState.GetBool(InFlightKey,false))return;
        int index=SessionState.GetInt(IndexKey,0);
        if(index>=Play.Length)
        {
            Finish("COMPLETE");
            EditorApplication.delayCall+=()=>
            {
                if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            };
            return;
        }

        SessionState.SetBool(InFlightKey,true);
        try {InvokeGate(Play[index],"Play");}
        catch(Exception e)
        {
            Record("Play",Play[index],"FAIL",0,1,0,
                "R9 runner unexpected: "+e);
        }
        finally
        {
            SessionState.SetInt(IndexKey,index+1);
            SessionState.SetBool(InFlightKey,false);
            WriteSummary(index+1==Play.Length?"FINALIZING":"RUNNING_SINGLE_PLAYMODE");

            // The next gate runs after rebuilding the runtime scene fixture.
            // There is still only ONE EnteredPlayMode and ONE ExitedPlayMode.
            if(index+1<Play.Length)
                SessionState.SetBool(ResetKey,true);
            else
            {
                Finish("COMPLETE");
                exitRequested=true;
            }
        }
    }

    [MenuItem("Game System Verification/R9 - Abort FULL Regression")]
    public static void Abort()
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        SessionState.SetBool(ActiveKey,false);
        SessionState.SetBool(InFlightKey,false);
        WriteSummary("ABORTED");
        if(Application.isPlaying)EditorApplication.isPlaying=false;
    }

    private static void Finish(string state)
    {
        if(SessionState.GetBool(CompletedKey,false))return;
        SessionState.SetBool(CompletedKey,true);
        SessionState.SetBool(ActiveKey,false);
        SessionState.SetBool(InFlightKey,false);
        EnsureReport();
        var allRows=File.ReadAllLines(CsvPath).Skip(1).ToArray();
        bool anyFailure=allRows.Any(x=>x.Contains("\"FAIL\"")||
            x.Contains("\"PENDING\"")||x.Contains("\"NOT_RUN\""));
        WriteSummary(anyFailure?"COMPLETE_WITH_FAILURES":"COMPLETE_ALL_GATES_PASSED");
    }
    public static string WriteSummary(string state="STATUS")
    {
        EnsureReport();
        var rows=File.ReadAllLines(CsvPath).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x))
            .ToArray();
        // CSV fields are always quoted, including escaped error details.
        var parsed=rows.Select(x=>Regex.Matches(x,
            "\"((?:[^\"]|\"\")*)\"").Cast<Match>()
            .Select(m=>m.Groups[1].Value.Replace("\"\"","\"")).ToArray())
            .Where(x=>x.Length>=7).ToArray();
        int passed=parsed.Count(x=>x[2]=="PASS"),
            failed=parsed.Count(x=>x[2]=="FAIL"),
            pending=parsed.Count(x=>x[2]=="PENDING");
        int checks=parsed.Sum(x=>int.TryParse(x[3],out var n)?n:0);
        int checkFails=parsed.Sum(x=>int.TryParse(x[4],out var n)?n:0);
        string text="# R9 integrated regression\n"+
            "- State: "+state+"\n"+
            "- Completed gates: "+parsed.Length+
            " / "+(Edit.Length+Play.Length)+"\n"+
            "- Gate PASS="+passed+" FAIL="+failed+" PENDING="+pending+
            " NOT_RUN="+Math.Max(0,Edit.Length+Play.Length-parsed.Length)+"\n"+
            "- Assertions PASS="+checks+" FAIL="+checkFails+"\n"+
            "- PlayMode entries: 1 for "+Play.Length+" Play gates (single domain reload)\n"+
            "- Runtime scene resets (no PlayMode exit): "+SessionState.GetInt(ResetCountKey,0)+"\n"+
            "- Runtime reset source: saved Battle Test Scene; unsaved editor scene is preserved\n"+
            "- Cross-gate contamination is checked by each gate's independent diagnostics\n"+
            "- No hidden PASS for no-summary, exception, or skipped gate\n"+
            "- Unresolved R7 content blockers remain tracked by R8 Validator\n"+
            "- EndUTC="+DateTime.UtcNow.ToString("o")+"\n";
        File.WriteAllText(Path.Combine(Dir,"R9_INTEGRATION_SUMMARY.md"),
            text,new UTF8Encoding(false));
        return text;
    }
}
#endif