#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R8 editor UI for one-at-a-time explicit native SO conversion. No auto-
/// conversion on selection, OnEnable, Validate or refresh. Complex graphs
/// remain read-only pending semantic authoring providers.
/// </summary>
public sealed class NativeAbilityAuthoringWindow : EditorWindow
{
    private SkillDefinition selected;
    private Vector2 scroll;
    private bool showBlocked;
    private string filter="";
    private NativeAbilityAuthoringCore.Preview[] previews=
        Array.Empty<NativeAbilityAuthoringCore.Preview>();
    private NativeAbilityValidation.Result scan;
    private string status="Not scanned. Press Dry Run.";
    private string ReportPath =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ProjectAbyss_R8_Authoring_20261102","R8_VALIDATOR_REPORT.md");

    [MenuItem("Project Abyss/R8/Native SO Converter + Validator")]
    public static void Open()
    {
        GetWindow<NativeAbilityAuthoringWindow>(
            "Project Abyss • R8 Native SO").Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("R8 · Native ScriptableObject Authoring",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only by default. Converts only proven single-effect resource, "+
            "bleeding and unique gauge schemas. Mixed/preparation/character-specific "+
            "operations require manual semantic parity. Existing assets are NEVER overwritten.",
            MessageType.Info);
        EditorGUILayout.Space();
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        {
            selected=(SkillDefinition)EditorGUILayout.ObjectField(
                "Source SkillDefinition",selected,typeof(SkillDefinition),false);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Dry Run / Refresh",GUILayout.Height(27)))
                RefreshAll();
            if(GUILayout.Button("Open Audit Report",GUILayout.Height(27)))
            {
                if(File.Exists(ReportPath))
                    EditorUtility.RevealInFinder(ReportPath);
                else status="Run Dry Run first.";
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
        if(Application.isPlaying)
            EditorGUILayout.HelpBox("Editing disabled in Play Mode.",MessageType.Warning);
        EditorGUILayout.Space();

        if(selected!=null)
        {
            var p=NativeAbilityAuthoringCore.Analyze(selected);
            EditorGUILayout.LabelField("Selected conversion preview",EditorStyles.boldLabel);
            using(new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Family",p.Kind.ToString());
                EditorGUILayout.LabelField("Verdict",p.Status.ToString());
                EditorGUILayout.LabelField("SkillId",p.SkillId);
                EditorGUILayout.LabelField("Source GUID",p.SourceGuid);
                EditorGUILayout.LabelField("Level 0 / 1 / 2",
                    p.L0+" / "+p.L1+" / "+p.L2);
                EditorGUILayout.LabelField("Trigger",p.Trigger.ToString());
                EditorGUILayout.LabelField("Destination",p.Destination??"-");
                EditorGUILayout.HelpBox(p.Reason??"-",
                    p.Status==NativeAbilityAuthoringCore.Verdict.ReadyToCreate
                        ?MessageType.Info:p.Status==
                        NativeAbilityAuthoringCore.Verdict.ExistsValid
                        ?MessageType.None:MessageType.Warning);
                using(new EditorGUI.DisabledScope(Application.isPlaying||
                    p.Status!=NativeAbilityAuthoringCore.Verdict.ReadyToCreate))
                {
                    if(GUILayout.Button("Stage NEW Native SO (no overwrite)",GUILayout.Height(30)))
                        CreateSelected(p);
                }
            }
        }

        if(scan!=null)
        {
            EditorGUILayout.LabelField("Audit summary",EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Skills "+scan.SkillCount+" • Native "+scan.NativeGoodCount+
                "/"+scan.NativeCount+" • Augments "+scan.AugmentCount+
                " • Items "+scan.ItemCount);
            EditorGUILayout.LabelField("Blockers "+scan.BlockerCount+
                " • Warnings "+scan.WarningCount+
                " • Ready to create "+scan.AutoConvertibleCount);
        }
        EditorGUILayout.Space();
        filter=EditorGUILayout.TextField("Filter SkillId",filter);
        showBlocked=EditorGUILayout.ToggleLeft("Show blocked/unsupported",showBlocked);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        foreach(var p in previews)
        {
            if(!showBlocked &&
                p.Status==NativeAbilityAuthoringCore.Verdict.Blocked)
                continue;
            if(!string.IsNullOrEmpty(filter)&&
                (p.SkillId==null||p.SkillId.IndexOf(filter,
                    StringComparison.OrdinalIgnoreCase)<0))
                continue;
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(p.SkillId??"(null)",GUILayout.Width(250)))
                    selected=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                        p.SourcePath);
                EditorGUILayout.LabelField(p.Kind+" / "+p.Status);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void RefreshAll()
    {
        if(Application.isPlaying){status="Edit Mode only";return;}
        previews=NativeAbilityAuthoringCore.AnalyzeAll().ToArray();
        scan=NativeAbilityValidation.Scan();
        NativeAbilityValidation.Report(scan,ReportPath);
        status="Scanned "+previews.Length+" source skills, "+
            scan.NativeGoodCount+"/"+scan.NativeCount+
            " native SOs verified; "+scan.BlockerCount+" content blockers.";
    }

    private void CreateSelected(NativeAbilityAuthoringCore.Preview p)
    {
        if(!EditorUtility.DisplayDialog("Stage new R8 native SO?",
            p.SkillId+"\n"+p.Destination+"\n\n"+
            "Source asset/.meta will be SHA-256 backed up. "+
            "No original SO or existing native graph will be changed. "+
            "Staged asset is outside Resources and NOT production-routed; gameplay route is NOT considered approved "+
            "without R7/R9 parity validation.",
            "Stage New","Cancel"))
            return;
        try
        {
            NativeAbilityAuthoringCore.CreateMissing(new[]{p});
            RefreshAll();
            var created=AssetDatabase.LoadMainAssetAtPath(p.Destination);
            if(created!=null)Selection.activeObject=created;
        }
        catch(Exception e)
        {
            Debug.LogError("[R8-Authoring] "+e);
            EditorUtility.DisplayDialog("R8 Create blocked",e.Message,"OK");
        }
    }
}
#endif