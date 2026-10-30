#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeMixedAssetMigration20261031
{
    private const string Folder = "Assets/2. Data/Resources/ProjectAbyss/NativeMixed";
    private static readonly string[] SourceIds = {
        "5fb3b00e68684389a86c92f3e0345061",
        "5a239ec7d4944c458ac5329511091871",
        "c09f804f95004e70a9864e0d02ae4509",
        "313842626a60416f97ac13a24ca171e3",
        "f8c31ca52c544f93b8d961acab0c9611",
        "35a5b1e98c8547218682cc24566c91c0",
        "cf4031e1aabf4207b52248c0d821b85a"
    };

    [MenuItem("Game System Verification/R7 Native Mixed - Stage 7 Data Assets (Edit Mode)")]
    public static void Stage()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit mode only");
        var sources = AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g => new { Guid=g, Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(g)) })
            .Where(x => x.Data != null && SourceIds.Contains(x.Data.SkillId)).ToArray();
        if (sources.Length != SourceIds.Length ||
            sources.Select(x=>x.Data.SkillId).Distinct().Count()!=SourceIds.Length)
            throw new InvalidOperationException("Missing or duplicated source skill ID");
        foreach (var source in sources)
        {
            var d=source.Data;
            if (d.EffectEntries==null || d.EffectEntries.Count<1 ||
                d.UpgradeProfile!=null || !d.HasEffectEntries)
                throw new InvalidOperationException("Unsafe authored source: "+d.SkillId);
            foreach (var e in d.EffectEntries)
            {
                if (e==null || e.EffectiveTiming!=SkillEffectTiming.OnExecute ||
                    (e.Conditions!=null && e.Conditions.Count>0))
                    throw new InvalidOperationException("Unsupported entry: "+d.SkillId);
                if (e.Definition is AddBodyPartStatusEffect status)
                {
                    if (!R7NativeLegacyStatusExecutor.CanConvert(status))
                        throw new InvalidOperationException("Unsupported status: "+d.SkillId);
                }
                else if (!(e.Definition is ApplyRolledPartDamageEffect))
                    throw new InvalidOperationException("Unsupported operation: "+d.SkillId);
            }
            string path=Folder+"/"+d.SkillId+".asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null)
                throw new InvalidOperationException("Refusing to overwrite: "+path);
        }
        if (!AssetDatabase.IsValidFolder("Assets/2. Data/Resources/ProjectAbyss"))
            throw new InvalidOperationException("Expected resource parent folder is missing");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/2. Data/Resources/ProjectAbyss","NativeMixed");
        foreach (var src in sources)
        {
            var d=src.Data;
            var obj=ScriptableObject.CreateInstance<R7NativeMixedAbilityDefinition>();
            obj.SchemaVersion=2;
            obj.StableId=d.SkillId;
            obj.SourceAssetGuid=src.Guid;
            obj.SourceAssetName=d.name;
            obj.Operations=d.EffectEntries.Select((e,index)=>
            {
                var result=new R7MixedOperationEntry {
                    EntryIndex=index, Trigger=AbilityEventKind.RollUsing
                };
                if (e.Definition is AddBodyPartStatusEffect status)
                {
                    result.Kind=R7MixedOperationKind.ApplyBleeding;
                    result.StackAmount=status.ResolveAuthoredValue(e.Overrides);
                    result.Duration=status.ResolveAuthoredDuration(e.Overrides);
                }
                else result.Kind=R7MixedOperationKind.RolledPartDamage;
                return result;
            }).ToArray();
            if (!obj.IsValid) throw new InvalidOperationException("Invalid graph "+d.SkillId);
            AssetDatabase.CreateAsset(obj,Folder+"/"+d.SkillId+".asset");
        }
        AssetDatabase.SaveAssets();
        R7NativeMixedSchema2Migration20261031.Upgrade();
        Debug.Log("[R7-NativeMixed] STAGED=7 legacySourcesModified=0 runtimeRouteChanged=0");
    }

    [MenuItem("Game System Verification/R7 Native Mixed - Verify 7 Data Assets (Edit Mode)")]
    public static void Verify()
    {
        var graphs=Resources.LoadAll<R7NativeMixedAbilityDefinition>("ProjectAbyss/NativeMixed");
        int fail=0;
        if(graphs.Length!=7)fail++;
        var ids=graphs.Select(x=>x.StableId).ToArray();
        if(ids.Distinct().Count()!=7 || SourceIds.Except(ids).Any())fail++;
        foreach(var g in graphs)
        {
            if(g==null || !g.IsValid) {fail++;continue;}
            var path=AssetDatabase.GUIDToAssetPath(g.SourceAssetGuid);
            var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if(d==null || d.SkillId!=g.StableId || d.name!=g.SourceAssetName ||
                d.EffectEntries.Count!=g.Operations.Length) {fail++;continue;}
            for(int i=0;i<g.Operations.Length;i++)
            {
                var entry=d.EffectEntries[i];var operation=g.Operations[i];
                if(entry.EffectiveTiming!=SkillEffectTiming.OnExecute ||
                   operation.Trigger!=AbilityEventKind.RollUsing ||
                   operation.EntryIndex!=i) {fail++;continue;}
                if(entry.Definition is AddBodyPartStatusEffect status)
                {
                    if(operation.Kind!=R7MixedOperationKind.ApplyBleeding ||
                       operation.StackAmount!=status.ResolveAuthoredValue(entry.Overrides) ||
                       operation.Duration!=status.ResolveAuthoredDuration(entry.Overrides))fail++;
                }
                else if (!(entry.Definition is ApplyRolledPartDamageEffect) ||
                         operation.Kind!=R7MixedOperationKind.RolledPartDamage)fail++;
            }
        }
        Debug.Log("[R7-NativeMixed] DATA PASS="+(fail==0?7:0)+" FAIL="+fail+
            " runtimeRouteChanged=0");
        if(fail>0)throw new InvalidOperationException("Native mixed data mismatch "+fail);
    }
}
#endif