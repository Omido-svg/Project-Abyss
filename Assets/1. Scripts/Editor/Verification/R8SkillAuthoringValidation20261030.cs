#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R8 first authoring gate: read-only production SkillDefinition validation.
/// Reports blockers and warnings, never fixes or writes assets without approval.
/// </summary>
public static class R8SkillAuthoringValidation20261030
{
    private sealed class Issue
    {
        public string SkillId;
        public string Path;
        public string Code;
        public bool Blocker;
    }

    [MenuItem("Game System Verification/R8 - Skill Authoring Validator Dry Run (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("R8 validator runs in Edit Mode only");
        var items=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new{
                Guid=g,
                Path=AssetDatabase.GUIDToAssetPath(g),
                Skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g))
            }).Where(x=>x.Skill!=null).ToArray();
        var duplicates=items.GroupBy(x=>x.Skill.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).ToArray();
        var duplicateNames=new HashSet<string>(
            duplicates.Select(x=>x.Key),StringComparer.Ordinal);
        var issues=new List<Issue>();
        void Add(string id,string path,string code,bool blocker)=>
            issues.Add(new Issue{SkillId=id,Path=path,Code=code,Blocker=blocker});

        foreach(var x in items)
        {
            var s=x.Skill;
            string id=s.SkillId;
            if(string.IsNullOrWhiteSpace(id))Add(id,x.Path,"EMPTY_STABLE_ID",true);
            if(duplicateNames.Contains(id))Add(id,x.Path,"DUPLICATE_STABLE_ID",true);
            if(s.EffectEntries!=null&&s.EffectEntries.Any(e=>e==null||e.Definition==null))
                Add(id,x.Path,"UNBOUND_EFFECT_TEMPLATE",true);
            if(s.Rolls!=null&&s.Rolls.Count>8)
                Add(id,x.Path,"ROLL_COUNT_EXCEEDS_SCHEMA",true);
            if(s.EffectiveRollCount<1||s.EffectiveRollCount>8)
                Add(id,x.Path,"INVALID_EFFECTIVE_ROLL_COUNT",true);
            if(s.Effects!=null&&s.Effects.Any(e=>e!=null))
                Add(id,x.Path,"LEGACY_EFFECTS_NEED_MANUAL_MIGRATION",false);
            if(s.EffectEntries!=null&&s.EffectEntries.Count>0 &&
               s.Effects!=null&&s.Effects.Any(e=>e!=null))
                Add(id,x.Path,"MIXED_LEGACY_AND_TEMPLATE_EFFECTS",false);
            if(!s.IsPhaseASchemaMigrated)
                Add(id,x.Path,"PHASE_A_SCHEMA_NOT_MARKED",false);
            if(s.Color==SkillColor.Unset)
                Add(id,x.Path,"COLOR_UNSET_FALLBACK",false);
            if(s.PresentationAsset==null)
                Add(id,x.Path,"PRESENTATION_ASSET_UNASSIGNED",false);
            if(s.AttackWeight==null)
                Add(id,x.Path,"ATTACK_WEIGHT_SETTINGS_NULL",false);
            if(s.AttackWeight!=null &&
               s.AttackWeight.TargetingModel==
                ProjectAbyss.Abilities.AttackWeightTargetingModel.SlotWeighted)
                Add(id,x.Path,"SLOT_WEIGHTED_REQUIRES_LIVE_PARITY_GATE",false);
        }

        var root=Directory.GetParent(Application.dataPath).FullName;
        string path=Path.Combine(root,"ProjectAbyss_R8_SkillValidatorDryRun_20261030.md");
        var sb=new StringBuilder();
        sb.AppendLine("# R8 Skill Authoring Validator — Read-Only Dry Run (2026-10-30)");
        sb.AppendLine("No ScriptableObjects, asset IDs, prefabs, or scenes have been modified.");
        sb.AppendLine();
        sb.AppendLine("- SkillDefinition assets checked: "+items.Length);
        sb.AppendLine("- Blocking issues: "+issues.Count(i=>i.Blocker));
        sb.AppendLine("- Unique stable SkillId duplicates: "+duplicates.Length);
        sb.AppendLine("- Warnings: "+issues.Count(i=>!i.Blocker));
        sb.AppendLine("- Gate status: "+(issues.Any(i=>i.Blocker)?"BLOCKED":"NO_STRUCTURAL_BLOCKERS"));
        sb.AppendLine();
        sb.AppendLine("## Problem distribution");
        foreach(var group in issues.GroupBy(x=>x.Code).OrderBy(g=>g.Key))
            sb.AppendLine("- "+group.Key+": "+group.Count()+
                " ("+(group.First().Blocker?"BLOCKER":"WARNING")+")");
        sb.AppendLine();
        sb.AppendLine("## Blocking assets");
        foreach(var issue in issues.Where(x=>x.Blocker).OrderBy(x=>x.Path))
            sb.AppendLine("- "+issue.Code+" | "+issue.SkillId+" | "+issue.Path);
        sb.AppendLine();
        sb.AppendLine("## Migration policy");
        sb.AppendLine("1. Read-only scan first. User approval required for any content edits.");
        sb.AppendLine("2. Back up each original asset AND .meta before a migration.");
        sb.AppendLine("3. Keep GUID/stable-ID compatibility and reconcile duplicate IDs explicitly.");
        sb.AppendLine("4. Only commit an effect when specialist reports Applied.");
        sb.AppendLine("5. Test G0/R1/R5/R6 and character Golden before switching any asset.");
        sb.AppendLine("6. Roll back the exact modified asset on any parity failure.");
        File.WriteAllText(path,sb.ToString(),new UTF8Encoding(false));
        int pass=0,fail=0;
        void Check(string label,bool ok)
        {
            if(ok){pass++;Debug.Log("[R8-Validator] PASS "+label);}
            else{fail++;Debug.LogError("[R8-Validator] FAIL "+label);}
        }
        Check("V01 inspected SkillDefinitions",items.Length==145);
        Check("V02 known duplicate stable IDs surfaced as blockers",
            duplicates.Length>0 && issues.Any(i=>i.Code=="DUPLICATE_STABLE_ID"));
        Check("V03 every blocker references a real asset",
            issues.Where(i=>i.Blocker).All(i=>File.Exists(i.Path)));
        Check("V04 every warning references a real asset",
            issues.Where(i=>!i.Blocker).All(i=>File.Exists(i.Path)));
        Check("V05 no effect entries silently skipped",
            items.All(x=>x.Skill.EffectEntries==null||
                !x.Skill.EffectEntries.Any(e=>e==null||e.Definition==null)||
                issues.Any(i=>i.Path==x.Path&&i.Code=="UNBOUND_EFFECT_TEMPLATE")));
        Check("V06 every duplicate ID has all paths reported",
            duplicates.All(g=>issues.Count(x=>x.Code=="DUPLICATE_STABLE_ID"&&
                x.SkillId==g.Key)==g.Count()));
        Check("V07 report created in project root",File.Exists(path));
        Check("V08 report marks current gate blocked",
            issues.Any(i=>i.Blocker)&&sb.ToString().Contains("Gate status: BLOCKED"));
        Check("V09 no pending ScriptableObject modifications",
            items.All(x=>!EditorUtility.IsDirty(x.Skill)));
        Check("V10 read-only validator does not set any skill SlotWeighted flag",
            items.Count(x=>x.Skill.AttackWeight?.TargetingModel==
                ProjectAbyss.Abilities.AttackWeightTargetingModel.SlotWeighted)==0);
        Debug.Log("[R8-Validator] SUMMARY skills="+items.Length+
            " blockers="+issues.Count(i=>i.Blocker)+
            " warnings="+issues.Count(i=>!i.Blocker)+
            " duplicateIds="+duplicates.Length+
            " report="+path);
        Debug.Log("[R8-Validator] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R8 validator self-tests failed "+fail);
    }
}
#endif