#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// G-FINAL audit, intentionally non-mutating. Do not confuse passing
/// regression tests with approved final content or an architecture freeze.
/// Preserves existing script/GUID/serialized legacy compatibility.
/// </summary>
public static class GFinalArchitectureFreezeAudit20261103
{
    private sealed class Result
    {
        public string Id, Verdict, Detail;
    }

    [MenuItem("Game System Verification/G-FINAL - Full Architecture Freeze Audit (Edit Only)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("G-FINAL requires idle EditMode.");

        string root = Directory.GetParent(Application.dataPath).FullName;
        string outDir = Path.Combine(root, "ProjectAbyss_GFinal_20261103");
        Directory.CreateDirectory(outDir);
        var records = new List<Result>();
        int pass = 0, fail = 0, pending = 0;
        void Check(string id, bool condition, string detail)
        {
            records.Add(new Result { Id=id, Verdict=condition ? "PASS" : "FAIL", Detail=detail });
            if (condition) { pass++; Debug.Log("[G-FINAL] PASS "+id+" "+detail); }
            else { fail++; Debug.LogError("[G-FINAL] FAIL "+id+" "+detail); }
        }
        void Pending(string id, string detail)
        {
            pending++;
            records.Add(new Result { Id=id, Verdict="PENDING", Detail=detail });
            Debug.LogWarning("[G-FINAL] PENDING "+id+" "+detail);
        }

        Scene scene = SceneManager.GetActiveScene();
        Check("F01 Scene contract", scene.path=="Assets/5. Scenes/Battle Test Scene.unity",
            "scene="+scene.path+" dirty="+scene.isDirty);
        int missingInScene=0;
        foreach(var go in scene.GetRootGameObjects())
            foreach(var t in go.GetComponentsInChildren<Transform>(true))
                missingInScene+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        Check("F02 Loaded scene missing scripts", missingInScene==0,
            "missing="+missingInScene);

        string[] protectedCore = {
            "SkillDefinition.cs", "SkillEffectEntry.cs", "BattleEffectResolver.cs",
            "DamagePipeline.cs", "CombatMechanic.cs", "ReactiveCombatMechanic.cs",
            "DelayedEffectTriggerQueue.cs", "CharacterEventBinder.cs",
            "AbilityRunner.cs", "BattleReactionScheduler.cs",
            "R5SlotWeightedTargetBridge.cs"
        };
        var scriptNames=Directory.GetFiles(Path.Combine(Application.dataPath,"1. Scripts"),
            "*.cs",SearchOption.AllDirectories)
            .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var coreMissing=protectedCore.Where(n=>!scriptNames.Contains(n)).ToArray();
        Check("F03 Required compatibility and extension interfaces retained",
            coreMissing.Length==0,"missing="+String.Join("|",coreMissing));

        var skills=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new {Guid=g,Path=AssetDatabase.GUIDToAssetPath(g)})
            .Select(x=>new {x.Guid,x.Path,Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(x.Path)})
            .Where(x=>x.Data!=null).ToArray();
        Check("F04 145 original plus 19 enemy skill corpus",
            skills.Length==164 && R7Stage12SkillCorpus20261103.IsExactLayeredCensus(),
            "count="+skills.Length);
        var ids=skills.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToArray();
        Check("F05 Duplicate-ID groups resolved by explicit GUID/save contracts",
            ids.Length==4 && ids.Sum(g=>g.Count())==8 &&
            ids.All(g=>R7Stage2SkillIdContractGate20261102.IsApprovedGroup(g.Select(x=>x.Data))),
            "groups="+ids.Length+" collisionAssets="+ids.Sum(g=>g.Count()));
        int missingMeta=skills.Count(x=>
            String.IsNullOrWhiteSpace(x.Guid)||x.Guid.Length!=32||
            !File.Exists(Path.Combine(root,x.Path))||
            !File.Exists(Path.Combine(root,x.Path)+".meta"));
        Check("F06 Skill asset GUID and meta presence",
            missingMeta==0 &&
            skills.Select(x=>x.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count()==skills.Length,
            "missingMeta="+missingMeta);
        int legacyHybrid=skills.Count(x=>x.Data.Effects!=null&&
            x.Data.Effects.Any(e=>e!=null)&&x.Data.EffectEntries!=null&&
            x.Data.EffectEntries.Count>0);
        int originalDirty=skills.Count(x=>EditorUtility.IsDirty(x.Data));
        Check("F07 Original authored skill assets unchanged by audit",
            originalDirty==0,"dirty="+originalDirty);

        int emotions=AssetDatabase.FindAssets("t:EmotionAugmentDefinition").Length;
        int items=AssetDatabase.FindAssets("t:RunItemDefinition").Length;
        Check("F08 63 original emotion augment assets",emotions==63,"count="+emotions);
        Check("F09 60 original run item assets",items==60,"count="+items);

        string r9Dir=Path.Combine(root,"ProjectAbyss_R9_Integration_20261102");
        string csv=Path.Combine(r9Dir,"R9_GATE_RESULTS.csv");
        string summary=Path.Combine(r9Dir,"R9_INTEGRATION_SUMMARY.md");
        string[] rows=File.Exists(csv)?File.ReadAllLines(csv)
            .Skip(1).Where(x=>!String.IsNullOrWhiteSpace(x)).ToArray():Array.Empty<string>();
        int good=rows.Count(x=>x.Contains(",\"PASS\","));
        int bad=rows.Count(x=>x.Contains(",\"FAIL\","));
        int other=rows.Length-good-bad;
        Check("F10 Most recent integrated R9 suite is complete and green",
            rows.Length==69 && good==69 && bad==0 && other==0 &&
            File.Exists(summary)&&File.ReadAllText(summary).Contains("COMPLETE_ALL_GATES_PASSED"),
            "gates="+rows.Length+" pass="+good+" fail="+bad+" other="+other);
        string[] vital={"R7.ResourceParity","R7.StatusParity","R7.GaugeParity",
            "R9.Deterministic","R9.Performance","Damage.Final",
            "R6.Insertion","R5.Weighted"};
        Check("F11 Final routing, reaction, determinism, weighted path gates",
            vital.All(name=>rows.Any(r=>r.Contains("\""+name+"\"") &&
                r.Contains(",\"PASS\","))),String.Join("|",vital));
        var g0=Path.Combine(root,"G0_KIW_Baseline_20261102","G0_KIW_FREEZE.json");
        var hashes=Path.Combine(root,"G0_KIW_Baseline_20261102","G0_ASSET_HASH_MANIFEST.csv");
        Check("F12 G0 K/I/W asset and source baseline present",
            File.Exists(g0)&&File.Exists(hashes)&&
            File.ReadAllLines(hashes).Length>=600,
            "assetManifest="+(File.Exists(hashes)?File.ReadAllLines(hashes).Length-1:0));
        var saveContract=Path.Combine(root,"ProjectAbyss_R7_2_20261102",
            "R7_2_GUID_IDENTITY_CONTRACT.csv");
        Check("F13 GUID-aware save compatibility verification produced evidence",
            File.Exists(saveContract)&&File.ReadAllLines(saveContract).Length==9,
            "registryRows="+(File.Exists(saveContract)?File.ReadAllLines(saveContract).Length-1:0));
        string r8Report=Path.Combine(root,"ProjectAbyss_R8_SkillValidatorDryRun_20261030.md");
        bool r8StructuralGreen=File.Exists(r8Report) &&
            File.ReadAllText(r8Report).Contains("- Blocking issues: 0") &&
            File.ReadAllText(r8Report).Contains("Gate status: NO_STRUCTURAL_BLOCKERS");
        Check("F15 R8 authoring validator has no unapproved structural blockers",
            r8StructuralGreen,"unapprovedBlockers="+(r8StructuralGreen?0:-1));

        // These are real project/content limitations, NOT fake PASS assertions.
        Pending("P01 Canonical emotion values", "13/63 EmotionAugment graphs are design-pending; gameplay effects are intentionally inert");
        Pending("P02 Canonical run-item gameplay", "60/60 item identities mapped, 0/60 canonical combat effects approved; tier redistribution still undecided");
        Pending("P03 K keyword and policy approval", "G0 heuristic reports 5 missing keyword candidates and 10 undecided policy entries; runtime equivalence not proven");
        Pending("P04 W authored-content proof", "G0 reports zero authored SlotWeighted card use; full W-01~W-30 design and representative card validation not shown by 69-gate suite");
        Pending("P05 Real content creation extension test", "3 new normal skills, 1 conditional passive, and 1 compound emotion augment added without changing shared managers/UI: not yet demonstrated");
        Pending("P06 Full combat lifecycle and player build", "All characters, enemy classes, loadout swap, scene unload/subscription lifecycle and standalone player-build proof not established by R9");
        bool legacyEntryPriority = skills.Where(x => x.Data.Effects!=null &&
            x.Data.Effects.Any(e=>e!=null) && x.Data.EffectEntries!=null &&
            x.Data.EffectEntries.Count>0).All(x => x.Data.HasEffectEntries &&
                x.Data.EnumerateEffectEntries().All(entry=>
                    x.Data.EffectEntries.Any(source=>ReferenceEquals(source,entry))));
        Check("F14 Serialized legacy Effects are dormant under current entry enumeration",
            legacyEntryPriority,
            "hybrid="+legacyHybrid+"; legacy serialized values deliberately preserved for GUID/save compatibility");

        string state=fail>0?"FAILED":pending>0?"NOT_FROZEN_PENDING":"FROZEN";
        string q(string x)=>"\""+(x??"").Replace("\"","\"\"").Replace("\n"," ").Replace("\r"," ")+"\"";
        File.WriteAllLines(Path.Combine(outDir,"GFINAL_GATE_RESULTS.csv"),
            new[]{"Gate,Result,Detail"}.Concat(records.Select(x=>
                String.Join(",",new[]{q(x.Id),q(x.Verdict),q(x.Detail)}))),
            new UTF8Encoding(false));
        var sb=new StringBuilder()
            .AppendLine("# G-FINAL Architecture Freeze Audit — 2026-11-03")
            .AppendLine()
            .AppendLine("- Canonical source: 전투 시스템 규칙(1029)(4).md")
            .AppendLine("- Roadmap: Project_Abyss_Ability_5Element_Architecture_Refactoring_Roadmap_v4_Limbus_Slot_Weight_20261030.md §15")
            .AppendLine("- Result: **"+state+"**")
            .AppendLine("- Verified checks: "+pass+" PASS / "+fail+" FAIL")
            .AppendLine("- Unproven or awaiting canonical decisions: "+pending+" PENDING")
            .AppendLine("- R9: "+good+"/"+rows.Length+" suite gates PASS")
            .AppendLine("- SkillDefinition: "+skills.Length+", original emotion augments: "+emotions+
                ", run items: "+items)
            .AppendLine("- Historical duplicate stable IDs: "+ids.Length+
                " approved GUID-protected groups; do NOT rename without save migration")
            .AppendLine("- Hybrid legacy serialized skill assets: "+legacyHybrid+
                " (retained, NOT automatically deleted)")
            .AppendLine("- Scene dirty: "+scene.isDirty+
                "; no scene, prefab, ScriptableObject, enum or source asset was modified by this audit")
            .AppendLine("- No git commit/push; no design-undecided value was decided")
            .AppendLine()
            .AppendLine("## G-FINAL checklist and evidence");
        foreach(var x in records)
            sb.AppendLine("- "+x.Verdict+" | "+x.Id+" | "+x.Detail);
        sb.AppendLine()
          .AppendLine("## Decision")
          .AppendLine(state=="FROZEN"
              ?"All currently instrumented gates passed. Full roadmap gate still requires any non-instrumented manual acceptance."
              :"Architecture-wide final freeze is NOT declared. Regression green is insufficient to claim approved canonical gameplay or fully verified authoring.");
        File.WriteAllText(Path.Combine(outDir,"GFINAL_AUDIT.md"),sb.ToString(),
            new UTF8Encoding(false));
        Debug.Log("[G-FINAL] RESULT PASS="+pass+" FAIL="+fail+
            " PENDING="+pending+" VERDICT="+state);
        if(fail>0)
            throw new InvalidOperationException("G-FINAL failed structural verification; see GFINAL_GATE_RESULTS.csv");
    }
}
#endif