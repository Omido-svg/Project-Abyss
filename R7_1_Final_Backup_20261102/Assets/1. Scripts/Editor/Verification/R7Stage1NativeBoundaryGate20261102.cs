#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7-1 production boundary contract: migrated Preparation uses native graph
/// data and R5 commands only; optional legacy batch scope cannot steal an
/// approved production-native card. Remaining fallback is tracked, not removed
/// while R7-3..R7-11 content is unmigrated.
/// </summary>
public static class R7Stage1NativeBoundaryGate20261102
{
    [MenuItem("Game System Verification/R7-1 - Native Boundary and Adapter Isolation")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode required");
        var native=Resources.Load<R7NativePreparationStatusGraph20261102>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Statuses");
        var setup=Resources.Load<R7NativePreparationSetupDefinition>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Setup");
        var original=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath(
                R7NativePreparationStatusAssetMigration20261102.SourceGuid));
        var a=Application.dataPath;
        var script=File.ReadAllText(Path.Combine(a,
            "1. Scripts/Runtime/Abilities/Runtime/R7ProductionPreparationStatusMigration.cs"));
        var dispatcher=File.ReadAllText(Path.Combine(a,
            "1. Scripts/Runtime/Skills/Base/SkillEffectDispatcher.cs"));
        var boundaries=File.ReadAllLines(Path.Combine(
            Directory.GetParent(a).FullName,
            "ProjectAbyss_R7_0_1_20261102",
            "R7_1_LEGACY_BOUNDARY_CALLS.csv"));
        var pass=0;var fail=0;
        void Check(string id,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-1-Gate] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R7-1-Gate] FAIL "+id+" "+detail);}
        }
        Check("B01 direct authored status graph present",native?.IsValid==true);
        Check("B02 original status/flat setup both independently native",
            setup?.IsValid==true && native?.SourceAssetGuid==setup?.SourceAssetGuid);
        Check("B03 exact source stable ID preserved",
            original!=null && native?.StableId==original.SkillId &&
            native.SourceAssetName==original.name);
        Check("B04 graph operation order Bleeding then Burn",
            native?.Operations?.Length==2 &&
            native.Operations[0].Status==StatusEffectId.Bleeding &&
            native.Operations[1].Status==StatusEffectId.Burn);
        Check("B05 native values preserve stack=1 duration=3",
            native?.Operations?.All(x=>x.Stacks==1&&x.Duration==3)==true);
        Check("B06 preparation production has NO SkillEffectEntry.TryApply call",
            !script.Contains("entry.TryApply(")&&!script.Contains(".TryApply("));
        Check("B07 preparation runtime has NO legacy effect definition/override",
            !script.Contains("AddBodyPartStatusEffect")&&
            !script.Contains("ResolveAuthoredValue")&&
            !script.Contains("SkillEffectOverrides")&&
            !script.Contains("LegacySkillEffectEntryAdapter"));
        Check("B08 native status constructor and typed R5 service",
            script.Contains("new Bleeding(value,operation.Duration)") &&
            script.Contains("new Burn(value,operation.Duration)") &&
            script.Contains("R5CombatSpecialistBackend"));
        var nativeBranch=dispatcher.IndexOf("var native = context.BattleContext",StringComparison.Ordinal);
        var prepBranch=dispatcher.IndexOf("var prep=context.BattleContext",StringComparison.Ordinal);
        var legacyScope=dispatcher.IndexOf("var migration = ProjectAbyss.Abilities.R7ExclusiveEntryMigrationScope.Active",StringComparison.Ordinal);
        Check("B09 production R7 routes outrank opt-in legacy adapter scope",
            nativeBranch>=0&&prepBranch>nativeBranch&&legacyScope>prepBranch);
        Check("B10 unmigrated legacy fallback not removed prematurely",
            dispatcher.Contains("return ExecuteEntries(")&&
            dispatcher.Contains("return migration.Execute(") &&
            boundaries.Length>1);
        Check("B11 original authored asset not dirty",
            original!=null&&!EditorUtility.IsDirty(original));
        Check("B12 legacy boundary source sites explicitly inventoried",
            boundaries.Length>=2 &&
            boundaries.Any(x=>x.Contains("R7ExclusiveEntryMigrationScope")) &&
            boundaries.Any(x=>x.Contains("SkillEffectDispatcher")));
        Debug.Log("[R7-1-Gate] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7-1 boundary failed "+fail);
    }
}
#endif