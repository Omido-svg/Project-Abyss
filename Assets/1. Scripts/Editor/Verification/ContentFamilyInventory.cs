#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Complete R7 authored content inventory, not proof of behavioral parity.
/// Separates data linkage from native implementation and canonical pending.
/// No source SO writes, no scene save.
/// </summary>
public static class ContentFamilyInventory
{
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode only");
        var skill=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null).ToArray();
        var aug=CanonicalEmotionAugmentInventory.Load();
        var item=AssetDatabase.FindAssets("t:RunItemDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<RunItemDefinition>(
                AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null).ToArray();
        var nativeResource=Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities");
        var nativeStatus=Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses");
        var nativeMixed=Resources.LoadAll<R7NativeMixedAbilityDefinition>(
            "ProjectAbyss/NativeMixed");
        var nativeGauge=Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
            "ProjectAbyss/NativeUniqueGauges");
        var tempItem=item.Count(x=>x.CombatItem is ProvisionalBalanceRunItem);
        var linked=item.Count(x=>x.CombatItem!=null);
        var ready=aug.Count(x=>x.RuntimeReadiness==
            EmotionAugmentRuntimeReadiness.RuntimeConnected);
        var missingEffect=aug.Where(x=>x.Effects==null||x.Effects.Count==0).ToArray();
        var duplicates=skill.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToArray();
        string Csv(string s)=>"\""+(s??string.Empty).Replace("\"","\"\"")+"\"";
        var rows=new System.Collections.Generic.List<string>{
            "Domain,AssetGUID,Path,StableId,DisplayName,Readiness,Implementation"};
        foreach(var a in aug)
        {
            var path=AssetDatabase.GetAssetPath(a);
            rows.Add(string.Join(",",new[]{
                "Augment",AssetDatabase.AssetPathToGUID(path),path,
                a.AugmentId,a.DisplayName,a.RuntimeReadiness.ToString(),
                "EffectAssets="+(a.Effects?.Count??0)}.Select(Csv)));
        }
        foreach(var i in item)
        {
            var path=AssetDatabase.GetAssetPath(i);
            rows.Add(string.Join(",",new[]{
                "Item",AssetDatabase.AssetPathToGUID(path),path,
                i.ItemId,i.DisplayName,i.Tier.ToString(),
                i.CombatItem?.GetType().Name??"UNLINKED"}.Select(Csv)));
        }
        var outDir=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "R7_Expansion_20261102");
        Directory.CreateDirectory(outDir);
        File.WriteAllLines(Path.Combine(outDir,"R7_AUGMENT_ITEM_INVENTORY.csv"),
            rows,new UTF8Encoding(false));
        var report=new StringBuilder()
            .AppendLine("# R7 all-content readiness (2026-11-02)")
            .AppendLine("- Skills: "+skill.Length+
                "; exact native SO/command routes: "+(nativeResource.Length+
                nativeStatus.Length+nativeMixed.Length+nativeGauge.Length))
            .AppendLine("- Preparation setup: 1 additional native SO; two status entries keep their legacy Entry gating boundary")
            .AppendLine("- Source SkillID duplicate groups: "+duplicates.Length+
                " ("+duplicates.Sum(x=>x.Count())+" assets)")
            .AppendLine("- Emotion augment definitions: "+aug.Length)
            .AppendLine("- RuntimeConnected: "+ready+
                "; pending: "+(aug.Length-ready))
            .AppendLine("- Augment effect entries: "+aug.Sum(x=>x.Effects?.Count??0)+
                "; no effects: "+missingEffect.Length)
            .AppendLine("- Run item definitions: "+item.Length+
                "; linked CombatItem: "+linked)
            .AppendLine("- TEMP_BALANCE_V1 CombatItem links: "+tempItem+
                " (not permanent native item implementations)")
            .AppendLine()
            .AppendLine("## Canonical content blockers");
        foreach(var a in aug.Where(x=>x.RuntimeReadiness!=
            EmotionAugmentRuntimeReadiness.RuntimeConnected))
            report.AppendLine("- "+a.AugmentId+": "+a.RuntimeReadiness+
                " — "+a.RuntimeNote);
        report.AppendLine()
            .AppendLine("## What these metrics do NOT mean")
            .AppendLine("- RuntimeConnected Augment != canonical-balance parity or native command migration.")
            .AppendLine("- Linked RunItem != permanent implementation; all 60 currently link a temporary balance mechanic.")
            .AppendLine("- No PassiveDefinition SO does not imply no character passives: code-defined mechanics require their own audit.")
            .AppendLine("- G0~R6 structure validation does not prove all R7 content complete.")
            .AppendLine("- Do not invent the pending Awe T3 Strength N value; requires canonical decision.");
        File.WriteAllText(Path.Combine(outDir,"R7_ALL_CONTENT_READINESS.md"),
            report.ToString(),new UTF8Encoding(false));
        int pass=0,fail=0;
        void Check(string id,bool valid,string desc)
        {
            if(valid){pass++;Debug.Log("[R7-Content] PASS "+id+" "+desc);}
            else{fail++;Debug.LogError("[R7-Content] FAIL "+id+" "+desc);}
        }
        Check("C01 original 145 plus Stage1 19 skills",
             skill.Length==164&&SkillCorpusVerification.IsExactLayeredCensus(),"skills="+skill.Length);
        Check("C02 36 native graph-linked source skills",
            nativeResource.Length==15 && nativeStatus.Length==9 &&
            nativeMixed.Length==7 && nativeGauge.Length==5,"15+9+7+5");
        Check("C03 63 emotion augments",aug.Length==63,"aug="+aug.Length);
        Check("C04 runtime-ready vs pending explicitly separated",
            ready==62 && aug.Length-ready==1,"62 ready, 1 pending");
        Check("C05 60 item definitions",item.Length==60,"items="+item.Length);
        Check("C06 every item has combat link",linked==60,"linked="+linked);
        Check("C07 temporary item implementation not counted native",
            tempItem==60,"temporary="+tempItem);
        Check("C08 duplicate skill IDs not silently ignored",
            duplicates.Length==4&&duplicates.Sum(x=>x.Count())==8,"4/8");
        Check("C09 no authoring SO modified by audit",
            skill.All(x=>!EditorUtility.IsDirty(x))&&
            item.All(x=>!EditorUtility.IsDirty(x))&&
            aug.All(x=>!EditorUtility.IsDirty(x)),"read-only");
        Debug.Log("[R7-Content] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 content audit "+fail);
    }
}
#endif
