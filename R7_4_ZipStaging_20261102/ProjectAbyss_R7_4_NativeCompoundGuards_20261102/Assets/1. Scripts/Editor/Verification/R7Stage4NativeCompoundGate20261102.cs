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
/// R7-4 fast EditMode closure for the seven already-authored native mixed
/// graphs (two actual damage+status cards). No scene/PlayMode/replay test.
/// </summary>
public static class R7Stage4NativeCompoundGate20261102
{
    [MenuItem("Game System Verification/R7-4 - Compound/Conditions/Roll Gate (Edit Only)")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)
            throw new InvalidOperationException("R7-4 runs only in EditMode.");
        int pass=0;
        void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("[R7-4] FAIL "+label);
            pass++;
        }
        var graphs=Resources.LoadAll<R7NativeMixedAbilityDefinition>(
            "ProjectAbyss/NativeMixed").OrderBy(x=>x.StableId).ToArray();
        Check(graphs.Length==7,"7 registered native mixed/status graphs");
        Check(graphs.All(x=>x!=null&&x.IsValid&&x.SchemaVersion==2),
            "all legacy schema-2 graphs remain valid");
        Check(graphs.Select(x=>x.StableId).Distinct().Count()==7,
            "no mixed stable ID collision");

        var data=new List<string[]>{
            new[]{"SkillId","SourceGUID","SourcePath","SourceName",
                  "NativeGraphPath","OpIndex","OpKind","Trigger","RollFiltered",
                  "Condition","SourceEntryType","IsDamageStatusCompound"}};
        int compounds=0,statusOnly=0,operations=0;
        foreach(var g in graphs)
        {
            string sourcePath=AssetDatabase.GUIDToAssetPath(g.SourceAssetGuid);
            var s=AssetDatabase.LoadAssetAtPath<SkillDefinition>(sourcePath);
            Check(s!=null&&s.SkillId==g.StableId&&s.name==g.SourceAssetName,
                "stable identity: "+g.StableId);
            Check(!EditorUtility.IsDirty(s)&&!EditorUtility.IsDirty(g),
                "original source and native graph not dirty: "+g.StableId);
            Check(s.EffectEntries!=null &&
                  s.EffectEntries.Count==g.Operations.Length,
                "entry count and authored operation order: "+g.StableId);
            bool compound=g.Operations.Length==2 &&
                g.Operations[0].Kind==R7MixedOperationKind.RolledPartDamage &&
                g.Operations[1].Kind==R7MixedOperationKind.ApplyBleeding;
            bool single=g.Operations.Length==1 &&
                g.Operations[0].Kind==R7MixedOperationKind.ApplyBleeding;
            Check(compound||single,"supported exact layout: "+g.StableId);
            if(compound)compounds++;
            if(single)statusOnly++;
            for(int index=0;index<g.Operations.Length;index++)
            {
                var op=g.Operations[index];
                var entry=s.EffectEntries[index];
                Check(op.EntryIndex==index&&
                      op.Trigger==AbilityEventKind.RollUsing&&
                      !op.RestrictToRoll&&
                      op.Condition==R7MixedConditionKind20261102.None&&
                      R7NativeMixedGate20261102.IsValid(op),
                      "unchanged native operation semantics: "+g.StableId+"/"+index);
                Check(entry!=null&&entry.Definition!=null &&
                    (op.Kind==R7MixedOperationKind.RolledPartDamage
                        ? entry.Definition is ApplyRolledPartDamageEffect
                        : entry.Definition is AddBodyPartStatusEffect),
                    "authored operation kind order: "+g.StableId+"/"+index);
                operations++;
                data.Add(new[]{g.StableId,g.SourceAssetGuid,sourcePath,s.name,
                    AssetDatabase.GetAssetPath(g),index.ToString(),
                    op.Kind.ToString(),op.Trigger.ToString(),
                    op.RestrictToRoll.ToString(),op.Condition.ToString(),
                    entry.Definition.GetType().Name,compound.ToString()});
            }
        }
        Check(compounds==2&&statusOnly==5,
            "two damage+status cards, five single-status cards");
        Check(operations==9,"nine native effect operations");
        // Verifies every source value/override against the original native SO.
        // This EditMode-only existing gate performs no gameplay execution.
        R7NativeMixedSchema2Migration20261031.Verify();
        Check(true,"legacy-to-native value parity gate (7)");

        var hypothetical=new R7MixedOperationEntry{
            EntryIndex=0,Kind=R7MixedOperationKind.ApplyBleeding,
            StackAmount=1,Duration=3,RollNumber=2,RestrictToRoll=true,
            Condition=R7MixedConditionKind20261102.None
        };
        Check(R7NativeMixedGate20261102.IsValid(hypothetical),
            "native roll restriction is valid");
        Check(R7NativeMixedGate20261102.Evaluate(hypothetical,null)==
                  R7MixedGateVerdict20261102.NotScheduled,
            "missing/nonmatching roll is not scheduled");
        hypothetical.RestrictToRoll=false;
        Check(R7NativeMixedGate20261102.Evaluate(hypothetical,null)==
                  R7MixedGateVerdict20261102.Execute,
            "unconditional operation remains eligible");
        hypothetical.Condition=R7MixedConditionKind20261102.RollSucceeded;
        Check(R7NativeMixedGate20261102.Evaluate(hypothetical,null)==
                  R7MixedGateVerdict20261102.ConditionFailed,
            "unavailable condition context cannot count as success");
        hypothetical.Condition=R7MixedConditionKind20261102.TargetNumericStatusAtLeast;
        hypothetical.RequiredStatus=StatusEffectId.Strength;
        hypothetical.RequiredNumericValue=2;
        Check(R7NativeMixedGate20261102.IsValid(hypothetical),
            "numeric status threshold is authorable");
        hypothetical.RequiredStatus=StatusEffectId.Bleeding;
        Check(!R7NativeMixedGate20261102.IsValid(hypothetical),
            "bespoke bleeding cannot masquerade as numeric N/T");
        hypothetical.Condition=R7MixedConditionKind20261102.None;
        hypothetical.RestrictToRoll=true;hypothetical.RollNumber=0;
        Check(!R7NativeMixedGate20261102.IsValid(hypothetical),
            "invalid roll number is refused");

        var dir=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ProjectAbyss_R7_4_20261102");
        Directory.CreateDirectory(dir);
        string Quote(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        File.WriteAllLines(Path.Combine(dir,"R7_4_NATIVE_COMPOUND_OPERATION_LEDGER.csv"),
            data.Select(row=>string.Join(",",row.Select(Quote))),
            new UTF8Encoding(false));
        Debug.Log("[R7-4] EditMode DATA PASS="+pass+
            " FAIL=0 / graphs=7 compound=2 statusOnly=5 operations=9"+
            " / R9_NOT_RUN / PLAYMODE_NOT_RUN");
    }
}
#endif