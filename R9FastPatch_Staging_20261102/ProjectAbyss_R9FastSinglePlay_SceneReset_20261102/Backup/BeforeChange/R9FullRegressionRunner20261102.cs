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
/// R9 staged regression coordinator.
/// - Edit gates execute sequentially in edit mode.
/// - Every destructive Play gate executes in its OWN clean PlayMode session,
///   preventing shared HP/state/queue contamination discovered in R6.
/// - Each gate is recorded to disk as it finishes; a crash/exit is NOT PASS.
/// - Unity Scene is never saved/reloaded and no source asset is modified here.
/// </summary>
[InitializeOnLoad]
public static class R9FullRegressionRunner20261102
{
    private sealed class Gate
    {
        public readonly string Id,Type,Method;
        public Gate(string id,string type,string method="Run")
        {Id=id;Type=type;Method=method;}
    }
    private static readonly Gate[] Edit={
        new Gate("G0.Audit","G0AbilityAuditReport20261029","RunAuditFromMenu"),
        new Gate("R1.Canonical","R1CanonicalContractsVerification20261102"),
        new Gate("R2.Contracts","R2AbilityContractVerification20261030","Verify"),
        new Gate("R2.Extended","R2ExtendedContractsVerification20261102"),
        new Gate("R3.Condition","R3ConditionVerification20261030","RunFromMenu"),
        new Gate("R3.Advanced","R3AdvancedConditionVerification20261102"),
        new Gate("R4.Weights","R4SlotWeightedPlannerVerification20261030"),
        new Gate("R4.Snapshot","R4PlanningSnapshotCacheVerification20261030"),
        new Gate("R4.TargetContract","R4TargetPlanningContractVerification20261102"),
        new Gate("R4.Policy","R4TargetPolicyVerification20261102"),
        new Gate("R5.Command","R5CommandApplyVerification20261030"),
        new Gate("R5.Reaction","R5ReactionCommandVerification20261102"),
        new Gate("R6.Lifetime","R6AbilityLifetimeVerification20261030"),
        new Gate("R6.Frames","R6IbClashFrameContractVerification20261030"),
        new Gate("R6.Queue","R6ReactionSchedulerVerification20261030"),
        new Gate("R6.SpecialistQueue","R6SpecialistQueueContractVerification20261030"),
        new Gate("R6.LifetimePhase","R6LifetimeCheckpointVerification20261102"),
        new Gate("R7.NativeSO","R7NativeSODataGate20261031"),
        new Gate("R7.MixedSchema2","R7NativeMixedSchema2Migration20261031","Verify"),
        new Gate("R7.UniqueGauge","R7NativeUniqueGaugeAssetMigration20261102","Verify"),
        new Gate("R7.Preparation","R7NativePreparationSetupAssetMigration20261102","Verify"),
        new Gate("R7.Coverage","R7NativeMigrationCoverageGate20261102"),
        new Gate("R7.Stage0Ledger","R7Stage0ContentLedger20261102"),
        new Gate("R7.Stage1Boundary","R7Stage1NativeBoundaryGate20261102"),
        new Gate("R7.Stage0Ledger","R7Stage0ContentLedger20261102"),
        new Gate("R7.Stage1Boundary","R7Stage1NativeBoundaryGate20261102"),
        new Gate("R7.AllContent","R7ContentFamilyInventory20261102"),
        new Gate("R8.Converter","R8NativeAuthoringVerification20261102"),
        new Gate("R8.AuthorValidator","R8SkillAuthoringValidation20261030"),
        new Gate("R8.PilotBackup","R8PilotAssetBackupGate20261030"),
        new Gate("R8.EffectBackup","R8EffectfulBatchPreflight20261030"),
        new Gate("R9.ErrorPolicy","R9ExpectedDiagnosticPolicyVerification20261102")
    };
    // The sequence is not a shared live battle; every invocation is a fresh
    // EnteredPlayMode -> exactly ONE gate -> ExitPlayMode cycle.
    private static readonly Gate[] Play={
        new Gate("G0.Golden","G0AbilityAuditReport20261029","RunGoldenBaselineFromMenu"),
        new Gate("R1.Timing","R1AbilityTimingVerification20261030","RunFromMenu"),
        new Gate("R1.Live","R1ExtendedLiveTimingVerification20261102"),
        new Gate("R1.CancelTie","R1TieAndCancellationVerification20261102"),
        new Gate("R2.Triggered","R2TriggeredActionParityVerification20261102"),
        new Gate("R2.Mark","R2NativeYujinMarkVerification20261102"),
        new Gate("R3.LegacyReaction","R3ReactionAndLegacyVerification20261102"),
        new Gate("R4.LiveTargets","R4LiveSlotQueryVerification20261030"),
        new Gate("R4.Preview","R4PlanningPreviewParityVerification20261102"),
        new Gate("R5.MultiTarget","R5MultiTargetDamageVerification20261102"),
        new Gate("R5.MixedReaction","R5MixedDamageReactionVerification20261102"),
        new Gate("R5.Planner","R5PlanToSpecialistVerification20261030"),
        new Gate("R5.Specialist","R5SpecialistIntegrationVerification20261030"),
        new Gate("R5.Weighted","R5SlotWeightedIntegrationVerification20261030"),
        new Gate("R6.Action","R6ReactionActionExecutionVerification20261030"),
        new Gate("R6.Specialist","R6SpecialistLiveDispatcherVerification20261030"),
        new Gate("R6.HifumiStartup","R6ProductionHifumiActivationVerification20261102"),
        new Gate("R6.QueueGuard","R6HifumiQueueGuardVerification20261102"),
        new Gate("R6.Production","R6ProductionStepwiseVerification20261102"),
        new Gate("R6.Presentation","R6IcStepPresentationVerification20261030"),
        new Gate("R6.Insertion","R6IbRealExchangeInsertionVerification20261030"),
        new Gate("R6.HifumiParity","R6HifumiDeferredParityVerification20261030"),
        new Gate("R7.ResourceParity","R7NativeSOResourceParityVerification20261031"),
        new Gate("R7.StatusParity","R7NativeSOStatusParityVerification20261031"),
        new Gate("R7.MixedParity","R7LegacyMixedStatusParity20261030"),
        new Gate("R7.MixedClash","R7LegacyMixedActualClashVerification20261030"),
        new Gate("R7.GaugeParity","R7NativeUniqueGaugeParity20261102"),
        new Gate("R7.PrepUndo","R7PreparationNativeUndoParity20261030"),
        new Gate("R7.PrepPartless","R7PreparationPartlessUndoParity20261030"),
        new Gate("Damage.Roll","DamageSystemR1RollPowerVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Window","DamageSystemR2VulnerabilityWindowVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Turn","DamageSystemR3TurnEndSettlementVerification20261029","VerifyFromMenu"),
        new Gate("Damage.OneSided","DamageSystemR4OneSidedMomentumVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Stagger","DamageSystemR5StaggerAndTiePolicyVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Special","DamageSystemR6SpecialDamageAuditVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Reuse","DamageSystemR7RecoveryTieAndReuseVerification20261029","VerifyFromMenu"),
        new Gate("Damage.Final","DamageSystemG1CompletionVerification20261029","VerifyFromMenu"),
        new Gate("R9.Deterministic","R9CombatReplayAndPerformance20261102","Determinism"),
        new Gate("R9.Performance","R9CombatReplayAndPerformance20261102","Performance")
    };
    private const string ActiveKey="ProjectAbyss.R9.Active.20261102";
    private const string IndexKey="ProjectAbyss.R9.Index.20261102";
    private const string InFlightKey="ProjectAbyss.R9.InFlight.20261102";
    private const string CompletedKey="ProjectAbyss.R9.Complete.20261102";
    private static string Dir=>Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "ProjectAbyss_R9_Integration_20261102");
    private static string CsvPath=>Path.Combine(Dir,"R9_GATE_RESULTS.csv");
    public static string Status =>
        (SessionState.GetBool(ActiveKey,false)?"RUNNING":"STOPPED")+
        " PlayGates="+SessionState.GetInt(IndexKey,0)+"/"+Play.Length+
        " EditGates="+Edit.Length+" Csv="+CsvPath;
    static R9FullRegressionRunner20261102()
    {
        EditorApplication.playModeStateChanged+=OnPlayMode;
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
        var diagnostics=R9ExpectedDiagnosticPolicy20261102.Evaluate(g.Id,logs);
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
            "\nEvery Play gate starts in a fresh Play Mode.\n",
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

    [MenuItem("Game System Verification/R9 - Start FULL Isolated Regression")]
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
        SessionState.SetBool(ActiveKey,true);
        WriteSummary("EDIT_COMPLETE_PLAY_PENDING");
        EditorApplication.delayCall+=StartNextPlay;
    }

    private static void StartNextPlay()
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        int index=SessionState.GetInt(IndexKey,0);
        if(index>=Play.Length)
        {Finish("COMPLETE");return;}
        EditorApplication.isPlaying=true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall+=ExecuteCurrentPlayGate;
        else if(state==PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall+=StartNextPlay;
    }

    private static void ExecuteCurrentPlayGate()
    {
        if(!SessionState.GetBool(ActiveKey,false)||!Application.isPlaying)return;
        if(SessionState.GetBool(InFlightKey,false))return;
        SessionState.SetBool(InFlightKey,true);
        int index=SessionState.GetInt(IndexKey,0);
        if(index>=Play.Length)
        {
            SessionState.SetBool(InFlightKey,false);
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
            return;
        }
        try {InvokeGate(Play[index],"Play");}
        catch(Exception e)
        {
            Record("Play",Play[index],"FAIL",0,1,0,
                "R9 runner unexpected: "+e.Message);
        }
        finally
        {
            SessionState.SetInt(IndexKey,index+1);
            SessionState.SetBool(InFlightKey,false);
            WriteSummary(index+1==Play.Length?"AWAITING_FINAL_EXIT":"RUNNING");
            // In case the test entered its own PlayMode transition, do not
            // recursively toggle a second transition.
            EditorApplication.delayCall+=()=>
            {
                if(Application.isPlaying)
                    EditorApplication.isPlaying=false;
            };
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
            "- PlayMode isolation: exactly one destructive gate per fresh PlayMode session\n"+
            "- No hidden PASS for no-summary, exception, or skipped gate\n"+
            "- Unresolved R7 content blockers remain tracked by R8 Validator\n"+
            "- EndUTC="+DateTime.UtcNow.ToString("o")+"\n";
        File.WriteAllText(Path.Combine(Dir,"R9_INTEGRATION_SUMMARY.md"),
            text,new UTF8Encoding(false));
        return text;
    }
}
#endif