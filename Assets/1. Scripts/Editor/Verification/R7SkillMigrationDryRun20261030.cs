#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7 read-only catalog audit. Never changes a ScriptableObject, GUID or
/// scene. Its outputs live in the project root, outside Assets/.
/// </summary>
public static class R7SkillMigrationDryRun20261030
{
    private sealed class Item
    {
        public string Guid,Path,Id,Name,Kind,Classification,Reasons;
        public int EntryCount,LegacyCount,RollCount;
        public bool Candidate;
    }

    [MenuItem("Game System Verification/R7 - All Skill Migration Readiness (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("Run R7 Dry Run only in Edit Mode");
        var records=new List<Item>();
        var known=AssetDatabase.FindAssets("t:SkillDefinition");
        var data=known.Select(g=>new{
            Guid=g,Path=AssetDatabase.GUIDToAssetPath(g),
            Skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g))
        }).Where(x=>x.Skill!=null).OrderBy(x=>x.Skill.SkillId,
            StringComparer.Ordinal).ThenBy(x=>x.Path,StringComparer.Ordinal).ToArray();
        var duplicate=data.GroupBy(x=>x.Skill.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToDictionary(g=>g.Key,g=>g.Count(),
                StringComparer.Ordinal);
        foreach(var entry in data)
        {
            var skill=entry.Skill;
            var problems=new List<string>();
            var id=skill.SkillId;
            if(string.IsNullOrWhiteSpace(id))problems.Add("MISSING_ID");
            if(duplicate.ContainsKey(id))problems.Add("DUPLICATE_ID");
            if(skill.Effects!=null&&skill.Effects.Any(x=>x!=null))
                problems.Add("LEGACY_EFFECTS");
            if(skill.EffectEntries!=null &&
                skill.EffectEntries.Any(x=>x==null||x.Definition==null))
                problems.Add("UNBOUND_EFFECT_ENTRY");
            if(id.StartsWith("hifumi.",StringComparison.Ordinal))
                problems.Add("CHARACTER_SPECIFIC_REACTIVE_LOGIC");
            if(skill.AttackWeight?.TargetingModel ==
                ProjectAbyss.Abilities.AttackWeightTargetingModel.SlotWeighted)
                problems.Add("SLOT_WEIGHT_OPT_IN_NEEDS_PARITY_GATE");
            if(skill.Rulebreaker!=null)
            {
                // A non-null authoring object alone is not an error or migration
                // claim; special policy still requires human review.
            }
            if(skill.OverrideResourceRules || skill.ResolvePrestigeInCombat ||
                skill.ConsumeAllPrestige || skill.ConsumeAllCustomResource)
                problems.Add("SPECIAL_RESOURCE_OR_PHASE_POLICY");

            int entries=skill.EffectEntries?.Count??0;
            int old=skill.Effects?.Count(x=>x!=null)??0;
            bool noEffects=entries==0&&old==0;
            // R7 inventory must use EXACTLY the same eligibility rule as
            // the opt-in runtime pilot, including per-roll/penalty effects.
            bool candidate=problems.Count==0&&noEffects &&
                ProjectAbyss.Abilities.R7SkillPilotObserver.IsMetadataOnlyNormal(skill);
            string cls=candidate?"CANDIDATE_METADATA_ONLY":
                duplicate.ContainsKey(id)?"BLOCKED_DUPLICATE_ID":
                problems.Count>0?"MANUAL_CONTRACT_REVIEW":
                noEffects?"MANUAL_NO_EFFECT_POLICY":"REQUIRES_EFFECT_MAPPING";
            records.Add(new Item {
                Guid=entry.Guid,Path=entry.Path,Id=id,Name=skill.SkillName,
                Kind=skill.ActionType.ToString(),Classification=cls,
                Reasons=string.Join(";",problems),Candidate=candidate,
                EntryCount=entries,LegacyCount=old,
                RollCount=skill.EffectiveRollCount
            });
        }

        string root=Directory.GetParent(Application.dataPath).FullName;
        string report=Path.Combine(root,"ProjectAbyss_R7_SkillDryRun_20261030.md");
        string csv=Path.Combine(root,"ProjectAbyss_R7_SkillDryRun_20261030.csv");
        var md=new StringBuilder();
        md.AppendLine("# Project Abyss — R7 145 SkillDefinition Dry Run (2026-10-30)");
        md.AppendLine();
        md.AppendLine("This is a READ-ONLY manifest. No SO, GUID, prefab, scene or combat runtime content was changed.");
        md.AppendLine("A CANDIDATE_METADATA_ONLY entry is NOT migrated, balanced, or parity-verified.");
        md.AppendLine();
        md.AppendLine("- Total SkillDefinitions: "+records.Count);
        md.AppendLine("- Total authored EffectEntries: "+records.Sum(x=>x.EntryCount));
        md.AppendLine("- SkillDefinitions with legacy effect references: "+
            records.Count(x=>x.LegacyCount>0));
        md.AppendLine("- Duplicate stable SkillId values: "+duplicate.Count);
        md.AppendLine("- Skill assets affected by duplicate SkillIds: "+
            records.Count(x=>duplicate.ContainsKey(x.Id)));
        md.AppendLine("- Simple normal no-effect metadata pilot candidates: "+
            records.Count(x=>x.Candidate));
        md.AppendLine();
        md.AppendLine("## Action type breakdown");
        foreach(var g in records.GroupBy(x=>x.Kind).OrderBy(x=>x.Key))
            md.AppendLine("- "+g.Key+": "+g.Count());
        md.AppendLine();
        md.AppendLine("## Migration classification");
        foreach(var g in records.GroupBy(x=>x.Classification).OrderBy(x=>x.Key))
            md.AppendLine("- "+g.Key+": "+g.Count());
        md.AppendLine();
        md.AppendLine("## Blocking duplicate IDs");
        foreach(var d in duplicate.OrderBy(x=>x.Key))
        {
            md.AppendLine("- "+d.Key+" ("+d.Value+" assets)");
            foreach(var r in records.Where(x=>x.Id==d.Key))
                md.AppendLine("  - "+r.Path+" ["+r.Guid+"]");
        }
        md.AppendLine();
        md.AppendLine("## Manual migration rules");
        md.AppendLine("1. Preserve asset GUIDs; do not auto-regenerate or overwrite stable skill IDs.");
        md.AppendLine("2. Resolve each duplicate with explicit authoring signoff and save compatibility plan.");
        md.AppendLine("3. Introduce AbilityRunner by vertical slice, compare original damage, statuses, resources, event timing.");
        md.AppendLine("4. Keep Hifumi chained Chinchiro reactive rolls on their specialist contract until real Golden parity.");
        md.AppendLine("5. Legacy-to-new dispatch must be exclusive per effect; never double-apply.");
        md.AppendLine("6. This audit never upgrades or migrates an asset automatically.");
        File.WriteAllText(report,md.ToString(),new UTF8Encoding(false));

        string Escape(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        var table=new StringBuilder();
        table.AppendLine("GUID,Path,SkillId,SkillName,ActionType,RollCount,EffectEntries,LegacyEffects,Class,Reasons");
        foreach(var r in records)
            table.AppendLine(string.Join(",",new[]{r.Guid,r.Path,r.Id,r.Name,
                r.Kind,r.RollCount.ToString(),r.EntryCount.ToString(),
                r.LegacyCount.ToString(),r.Classification,r.Reasons}.Select(Escape)));
        File.WriteAllText(csv,table.ToString(),new UTF8Encoding(false));

        int pass=0,fail=0;
        void Check(string label,bool yes)
        {
            if(yes){pass++;Debug.Log("[R7-Audit] PASS "+label);}
            else{fail++;Debug.LogError("[R7-Audit] FAIL "+label);}
        }
        Check("A01 enumerated all loaded SkillDefinitions",records.Count==145);
        Check("A02 written markdown and CSV in project root",
            File.Exists(report)&&File.Exists(csv));
        Check("A03 asset GUID retained for every inventory record",
            records.All(x=>!string.IsNullOrWhiteSpace(x.Guid)));
        Check("A04 every row has valid location",
            records.All(x=>x.Path.StartsWith("Assets/",StringComparison.Ordinal)));
        Check("A05 duplicate IDs explicitly classified",records.Where(x=>
            duplicate.ContainsKey(x.Id)).All(x=>x.Classification=="BLOCKED_DUPLICATE_ID"));
        Check("A06 no candidate contains legacy effects",
            records.Where(x=>x.Candidate).All(x=>x.LegacyCount==0));
        Check("A07 no candidate contains authored effects",
            records.Where(x=>x.Candidate).All(x=>x.EntryCount==0));
        Check("A08 Hifumi reactive content never auto-candidate",
            records.Where(x=>x.Id.StartsWith("hifumi.",StringComparison.Ordinal))
                .All(x=>!x.Candidate));
        Check("A09 all stable duplicate paths logged",!duplicate.Any()||
            records.Count(x=>duplicate.ContainsKey(x.Id))>=2*duplicate.Count);
        Check("A10 no SkillDefinition or SO marked dirty by audit",
            data.All(x=>!EditorUtility.IsDirty(x.Skill)));
        Debug.Log("[R7-Audit] SUMMARY total="+records.Count+
            " entryCount="+records.Sum(x=>x.EntryCount)+
            " legacySkillCount="+records.Count(x=>x.LegacyCount>0)+
            " duplicateIds="+duplicate.Count+
            " metadataCandidates="+records.Count(x=>x.Candidate)+
            " report="+report+" csv="+csv);
        Debug.Log("[R7-Audit] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 Dry Run failed "+fail);
    }
}
#endif