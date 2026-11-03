#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7-0 exhaustive current-project content ledger. 145 skills, 63 augments,
/// 60 run items and explicit code-based passive/mechanic *candidates*.
/// Does not label code filename heuristics as proven gameplay passives.
/// No authoring SO, code, prefab or scene writes.
/// </summary>
public static class R7Stage0ContentLedger20261102
{
    private static string Root=>Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "ProjectAbyss_R7_0_1_20261102");
    private static string Q(object v)
    {
        string s=v==null?"":v.ToString();
        return "\""+s.Replace("\"","\"\"").Replace("\r"," ")
            .Replace("\n"," ")+"\"";
    }
    private static void Write(string filename,IEnumerable<string[]> rows)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllLines(Path.Combine(Root,filename),
            rows.Select(r=>string.Join(",",r.Select(Q))),
            new UTF8Encoding(false));
    }
    private static IEnumerable<T> Load<T>() where T:UnityEngine.Object =>
        AssetDatabase.FindAssets("t:"+typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(x=>x!=null);
    private static void Check(string id,bool good,string summary,
        ref int pass,ref int fail)
    {
        if(good){pass++;Debug.Log("[R7-0] PASS "+id+" "+summary);}
        else{fail++;Debug.LogError("[R7-0] FAIL "+id+" "+summary);}
    }
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        var skills=Load<SkillDefinition>().ToArray();
        var augments=Load<EmotionAugmentDefinition>().ToArray();
        var items=Load<RunItemDefinition>().ToArray();
        var duplicates=new HashSet<string>(skills.GroupBy(s=>s.SkillId,
            StringComparer.Ordinal).Where(g=>g.Count()>1).Select(g=>g.Key),
            StringComparer.Ordinal);
        var native=Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities").Select(x=>(x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid))
            .Concat(Resources.LoadAll<R7NativeStatusAbilityDefinition>(
                "ProjectAbyss/NativeStatuses").Select(x=>(x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid)))
            .Concat(Resources.LoadAll<R7NativeMixedAbilityDefinition>(
                "ProjectAbyss/NativeMixed").Select(x=>(x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid)))
            .Concat(Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
                "ProjectAbyss/NativeUniqueGauges").Select(x=>(x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid)))
            .ToArray();
        var nativeByGuid=native.GroupBy(x=>x.SourceAssetGuid,
            StringComparer.OrdinalIgnoreCase).ToDictionary(
                x=>x.Key,x=>x.ToArray(),StringComparer.OrdinalIgnoreCase);
        var preparation=Resources.Load<R7NativePreparationStatusGraph20261102>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Statuses");
        var setup=Resources.Load<R7NativePreparationSetupDefinition>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Setup");
        var countByStatus=new Dictionary<string,int>(StringComparer.Ordinal);
        var rows=new List<string[]>{
            new[]{"Guid","Path","SkillId","AssetName","DisplayName","ActionType",
                  "Rolls","EffectEntries","LegacyEffects","EffectTemplateTypes",
                  "EntryConditions","DefinitionConditions","RestrictedRollEntries",
                  "HasUpgradeProfile","R7Stage","NextStep"}};
        foreach(var s in skills.OrderBy(x=>x.SkillId).ThenBy(x=>AssetDatabase.GetAssetPath(x)))
        {
            string path=AssetDatabase.GetAssetPath(s);
            string guid=AssetDatabase.AssetPathToGUID(path);
            var effects=s.EffectEntries?.Where(e=>e?.Definition!=null).ToArray()
                ??Array.Empty<SkillEffectEntry>();
            var list=effects.Select(e=>e.Definition.GetType().Name)
                .Distinct().OrderBy(x=>x).ToArray();
            int legacy=s.Effects?.Count(x=>x!=null)??0;
            string stage,step;
            if(duplicates.Contains(s.SkillId))
            {stage="BLOCKED_DUPLICATE_ID";step="R7-2";}
            else if(nativeByGuid.TryGetValue(guid,out var found)&&
                    found.Length==1&&found[0].IsValid&&
                    found[0].StableId==s.SkillId&&
                    found[0].SourceAssetName==s.name)
            {stage="VERIFIED_NATIVE_GRAPH";step="R7-12 RECHECK";}
            else if(s.SkillId==R7ProductionPreparationStatusMigration.SkillId&&
                    preparation?.IsValid==true && setup?.IsValid==true)
            {stage="VERIFIED_PREPARATION_NATIVE_GRAPH";step="R7-1/12 RECHECK";}
            else if(R7SkillPilotObserver.IsMetadataOnlyNormal(s))
            {stage="METADATA_OBSERVER_ONLY";step="R7-8";}
            else if(effects.Length>0||legacy>0||
                    s.Rolls?.Any(r=>r!=null&&
                        (r.EffectEntries?.Any(e=>e?.Definition!=null)==true||
                        r.OnWinEffectEntries?.Any(e=>e?.Definition!=null)==true||
                        r.OnLoseEffectEntries?.Any(e=>e?.Definition!=null)==true))==true)
            {stage="PENDING_EFFECT_NATIVE";step=
                list.Any(x=>x.Contains("Olaf"))?"R7-5":
                list.Any(x=>x.Contains("Yujin"))?"R7-6":
                list.Any(x=>x.Contains("Hifumi"))?"R7-7":
                effects.Length>1?"R7-4":"R7-3/8";}
            else
            {stage="PENDING_NO_EFFECT_POLICY";step="R7-8";}
            countByStatus[stage]=countByStatus.TryGetValue(stage,out var n)?n+1:1;
            rows.Add(new[]{guid,path,s.SkillId,s.name,s.SkillName,
                s.ActionType.ToString(),(s.Rolls?.Count??0).ToString(),
                effects.Length.ToString(),legacy.ToString(),string.Join("|",list),
                effects.Sum(e=>e.Conditions?.Count??0).ToString(),
                effects.Sum(e=>e.Definition.Conditions?.Count??0).ToString(),
                effects.Count(e=>e.RestrictToRoll).ToString(),
                (s.UpgradeProfile!=null).ToString(),stage,step});
        }
        Write("R7_0_ALL_164_CURRENT_SKILLS.csv",rows);

        var augmentRows=new List<string[]>{
            new[]{"GUID","Path","AugmentId","DisplayName","RuntimeReadiness",
                "DesignStatus","EffectCount","RuntimeNote","NextStep"}};
        foreach(var a in augments.OrderBy(x=>x.AugmentId))
        {
            string path=AssetDatabase.GetAssetPath(a);
            augmentRows.Add(new[]{AssetDatabase.AssetPathToGUID(path),path,
                a.AugmentId,a.DisplayName,a.RuntimeReadiness.ToString(),
                a.DesignStatus.ToString(),(a.Effects?.Count??0).ToString(),
                a.RuntimeNote,"R7-10"});
        }
        Write("R7_0_ALL_63_AUGMENTS.csv",augmentRows);

        var itemRows=new List<string[]>{
            new[]{"GUID","Path","ItemId","DisplayName","Tier","CombatLinkType",
                "IsTempBalance","NextStep"}};
        foreach(var i in items.OrderBy(x=>x.ItemId))
        {
            string path=AssetDatabase.GetAssetPath(i);
            itemRows.Add(new[]{AssetDatabase.AssetPathToGUID(path),path,
                i.ItemId,i.DisplayName,i.Tier.ToString(),
                i.CombatItem?.GetType().Name??"NULL",
                (i.CombatItem is TempBalanceRunItem).ToString(),"R7-11"});
        }
        Write("R7_0_ALL_60_ITEMS.csv",itemRows);

        var candidates=Directory.GetFiles(
            Path.Combine(Application.dataPath,"1. Scripts/Runtime"),"*.cs",
            SearchOption.AllDirectories)
            .Where(x=>(Path.GetFileName(x).Contains("Passive")||
                       Path.GetFileName(x).Contains("Mechanic"))&&
                 !x.Replace('\\','/').Contains("/Presentation/")&&
                 !x.Replace('\\','/').Contains("/Planning/"))
            .OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var candidateRows=new List<string[]>{
            new[]{"ScriptGUID","Path","FileName","Classification","NextStep"}};
        foreach(var path in candidates)
        {
            var relative="Assets/"+path.Substring(Application.dataPath.Length+1)
                .Replace('\\','/');
            candidateRows.Add(new[]{AssetDatabase.AssetPathToGUID(relative),
                relative,Path.GetFileName(path),
                "CODE_MECHANIC_OR_PASSIVE_CANDIDATE_NOT_AUDITED","R7-9"});
        }
        Write("R7_0_PASSIVE_MECHANIC_SCRIPT_CANDIDATES.csv",candidateRows);

        // Trace executable adapter boundaries in project code, not just in
        // authored SOs. Results are candidate call-sites for R7-1 follow-up.
        var boundaries=new List<string[]>{
            new[]{"ScriptPath","Line","Signature","Category"}};
        var runtime=Directory.GetFiles(
            Path.Combine(Application.dataPath,"1. Scripts/Runtime"),
            "*.cs",SearchOption.AllDirectories);
        foreach(var f in runtime)
        {
            var lines=File.ReadAllLines(f);
            for(int n=0;n<lines.Length;n++)
            {
                var l=lines[n].Trim();
                if(l.StartsWith("//")||l.StartsWith("*")||
                   l.StartsWith("///"))continue;
                if(l.Contains("entry.TryApply(")||
                   l.Contains("new LegacySkillEffectEntryAdapter(")||
                   l.Contains("new R7ExclusiveEntryMigrationScope(")||
                   l.Contains("R7.LEGACY_SPECIALIST_ADAPTER"))
                {
                    boundaries.Add(new[]{
                        "Assets/"+f.Substring(Application.dataPath.Length+1).Replace('\\','/'),
                        (n+1).ToString(),l,
                        l.Contains("TryApply(")?"ENTRY_GATE":
                        l.Contains("LegacySkillEffectEntryAdapter")?"LEGACY_ADAPTER":
                        "ADAPTER_INFRASTRUCTURE"});
                }
            }
        }
        Write("R7_1_LEGACY_BOUNDARY_CALLS.csv",boundaries);

        int pass=0,fail=0;
        Check("A01 original 145 plus 19 stage1 sources",
             skills.Length==164&&R7Stage12SkillCorpus20261103.IsExactLayeredCensus(),
            skills.Length.ToString(),ref pass,ref fail);
        Check("A02 each source GUID unique",
            rows.Skip(1).Select(x=>x[0]).Distinct().Count()==164,
            "source GUIDs",ref pass,ref fail);
        Check("A03 36 exact native skill graphs",
            countByStatus.GetValueOrDefault("VERIFIED_NATIVE_GRAPH")==36,
            "36 native",ref pass,ref fail);
        Check("A04 one native Preparation whose 3 effects use native R5",
            countByStatus.GetValueOrDefault("VERIFIED_PREPARATION_NATIVE_GRAPH")==1,
            "source prep graph",ref pass,ref fail);
        Check("A05 collision 4 IDs / 8 assets",
            duplicates.Count==4&&
            countByStatus.GetValueOrDefault("BLOCKED_DUPLICATE_ID")==8,
            "4 / 8",ref pass,ref fail);
        Check("A06 63 augment definitions",augments.Length==63,
            augments.Length.ToString(),ref pass,ref fail);
        Check("A07 pending augment explicitly tracked",
            augments.Count(x=>x.RuntimeReadiness!=
                EmotionAugmentRuntimeReadiness.RuntimeConnected)==1,
            "pending=1",ref pass,ref fail);
        Check("A08 60 item definitions",items.Length==60,
            items.Length.ToString(),ref pass,ref fail);
        Check("A09 temp item debt not counted as native",
            items.Count(x=>x.CombatItem is TempBalanceRunItem)==60,
            "temporary=60",ref pass,ref fail);
        Check("A10 code-defined passive/mechanic candidates inventoried",
            candidateRows.Count>1,"candidate scripts="+(candidateRows.Count-1),
            ref pass,ref fail);
        Check("A11 all skill rows assigned exactly one group",
            countByStatus.Values.Sum()==164,
            string.Join(";",countByStatus.Select(x=>x.Key+":"+x.Value)),
            ref pass,ref fail);
        Check("A12 legacy executable callsites listed",
            boundaries.Count>1,
            "candidate callsites="+(boundaries.Count-1),ref pass,ref fail);
        string summary="# R7-0 migration source ledger\n"+
             "164 SkillDefinitions (145 original + 19 stage1) inventoried by GUID; "+
            "not a claim that every effect is fully migrated.\n\n"+
            string.Join("\n",countByStatus.OrderBy(x=>x.Key)
                .Select(x=>"- "+x.Key+": "+x.Value))+"\n\n"+
            "- Augments: "+augments.Length+" (pending canonical: 1)\n"+
            "- Run items: "+items.Length+" (temporary mechanics: 60)\n"+
            "- Passive/mechanic code *candidates*: "+(candidateRows.Count-1)+
            " (needs character-wise R7-9 review, not a proven passive count)\n"+
            "- Residual adapter callsites: "+(boundaries.Count-1)+
            " (source search candidates, not all live routes)\n"+
            "- 128/164 skills outside original 36-graph census include direct-native stages and 19 new Stage1 1029 cards; "+
            "Preparation, 8 GUID-resolved duplicate IDs and pending designs.\n"+
            "- R7-1's Preparation does not count inside the prior 36, "+
            "to avoid retroactively changing the baseline.\n"+
            "\nSee four CSVs for full source-level evidence.\n";
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root,"R7_0_INVENTORY_SUMMARY.md"),
            summary,new UTF8Encoding(false));
        Debug.Log("[R7-0] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7-0 inventory failed "+fail);
    }
}
#endif