#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

// Idempotent data-only conversion of the seven staged mixed-card graphs.
// Legacy SkillDefinitions and effect templates are never modified.
public static class R7NativeMixedSchema2Migration20261031
{
    private const string Folder = "ProjectAbyss/NativeMixed";

    private static float Number(SerializedObject so, string property, float fallback)
    {
        var p = so.FindProperty(property);
        if (p == null || p.propertyType != SerializedPropertyType.Float)
            throw new InvalidOperationException("Invalid damage field: " + property);
        return p.floatValue;
    }
    private static int Integer(SerializedObject so, string property)
    {
        var p=so.FindProperty(property);
        if(p==null)throw new InvalidOperationException("Missing field: "+property);
        return p.intValue;
    }
    private static bool Flag(SerializedObject so, string property)
    {
        var p=so.FindProperty(property);
        if(p==null || p.propertyType!=SerializedPropertyType.Boolean)
            throw new InvalidOperationException("Missing flag: "+property);
        return p.boolValue;
    }
    private static void Guard(SkillDefinition d, R7NativeMixedAbilityDefinition graph)
    {
        if(d==null || graph==null || d.SkillId!=graph.StableId ||
            d.name!=graph.SourceAssetName ||
            d.UpgradeProfile!=null || !d.HasEffectEntries ||
            d.EffectEntries==null || graph.Operations==null ||
            d.EffectEntries.Count!=graph.Operations.Length)
            throw new InvalidOperationException("Source/graph mismatch: "+graph?.StableId);
        if(d.Rolls!=null && d.Rolls.Any(r=>r!=null &&
            (r.EffectEntries?.Any(e=>e?.Definition!=null)==true ||
             r.OnWinEffectEntries?.Any(e=>e?.Definition!=null)==true ||
             r.OnLoseEffectEntries?.Any(e=>e?.Definition!=null)==true)))
            throw new InvalidOperationException("Unconverted roll entry: "+d.SkillId);
        int bleeding=0, damages=0;
        for(int i=0;i<d.EffectEntries.Count;i++)
        {
            var e=d.EffectEntries[i];
            if(e==null || e.Definition==null ||
               e.EffectiveTiming!=SkillEffectTiming.OnExecute ||
               e.Definition.TargetSelector!=SkillEffectTargetSelector.CurrentTarget ||
               (e.Conditions?.Count??0)!=0 ||
               e.Definition.Conditions.Count!=0 ||
               e.RestrictToRoll ||
               !string.IsNullOrEmpty(e.UpgradeKey))
                throw new InvalidOperationException("Unsupported entry semantics "+d.SkillId+"["+i+"]");
            var o=e.Overrides;
            if(o==null)throw new InvalidOperationException("Null overrides "+d.SkillId);
            if(e.Definition is AddBodyPartStatusEffect status)
            {
                bleeding++;
                if(!R7NativeLegacyStatusExecutor.CanConvert(status) ||
                   o.OverrideInfiniteDuration || o.OverrideAmount ||
                   o.OverrideMinimum || o.OverrideMaximum || o.OverrideFlatValue ||
                   o.OverrideMultiplier || o.OverrideResourceKey ||
                   o.OverrideForceCharacterStatus || o.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException("Unsupported bleeding params: "+d.SkillId);
            }
            else if(e.Definition is ApplyRolledPartDamageEffect)
            {
                damages++;
                if(o.OverrideStack || o.OverrideDuration || o.OverrideInfiniteDuration ||
                   o.OverrideAmount || o.OverrideMinimum || o.OverrideMaximum ||
                   o.OverrideResourceKey || o.OverrideForceCharacterStatus ||
                   o.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException("Unsupported damage params: "+d.SkillId);
            }
            else throw new InvalidOperationException("Unsupported operation: "+d.SkillId);
        }
        if(bleeding!=1 || damages>1 || (damages==1 && d.EffectEntries.Count!=2))
            throw new InvalidOperationException("Unexpected mixed layout "+d.SkillId);
    }

    [MenuItem("Game System Verification/R7 Native Mixed - Upgrade 7 Data SO to V2 (Edit Mode)")]
    public static void Upgrade()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode only");
        var graphs=Resources.LoadAll<R7NativeMixedAbilityDefinition>(Folder);
        if(graphs.Length!=7)throw new InvalidOperationException("Expected seven graphs");
        // Validate the entire cohort before modifying any asset.
        var sources=graphs.Select(g=>new {
            Graph=g,
            Source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g.SourceAssetGuid))
        }).ToArray();
        foreach(var x in sources)Guard(x.Source,x.Graph);
        int changed=0;
        foreach(var x in sources)
        {
            var graph=x.Graph;var d=x.Source;
            graph.SourceCanBreakPart=d.CanBreakPart;
            for(int i=0;i<d.EffectEntries.Count;i++)
            {
                var entry=d.EffectEntries[i];
                var op=graph.Operations[i];
                op.EntryIndex=i;
                op.Trigger=AbilityEventKind.RollUsing;
                if(entry.Definition is AddBodyPartStatusEffect status)
                {
                    op.Kind=R7MixedOperationKind.ApplyBleeding;
                    op.StackAmount=status.ResolveAuthoredValue(entry.Overrides);
                    op.Duration=status.ResolveAuthoredDuration(entry.Overrides);
                }
                else
                {
                    op.Kind=R7MixedOperationKind.RolledPartDamage;
                    var so=new SerializedObject(entry.Definition);
                    op.DamageMultiplier=entry.Overrides.ResolveMultiplier(
                        Number(so,"powerMultiplier",1f));
                    op.DamageFlatBonus=entry.Overrides.ResolveFlatValue(
                        Integer(so,"flatBonus"));
                    op.DamageType=(DamageType)Integer(so,"damageType");
                    op.UseSkillCanBreakPart=Flag(so,"useSkillCanBreakPart");
                    op.CanBreakPart=Flag(so,"canBreakPart");
                    op.ApplyMomentum=Flag(so,"applyMomentum");
                    op.ApplyGuard=Flag(so,"applyGuard");
                    if(float.IsNaN(op.DamageMultiplier) || float.IsInfinity(op.DamageMultiplier) ||
                       op.DamageMultiplier<0f)
                        throw new InvalidOperationException("Invalid multiplier "+d.SkillId);
                }
            }
            graph.SchemaVersion=2;
            if(!graph.IsValid)throw new InvalidOperationException("Invalid upgraded SO "+d.SkillId);
            EditorUtility.SetDirty(graph);
            changed++;
        }
        AssetDatabase.SaveAssets();
        R7ProductionLegacyStatusMigration.ResetCatalogCache();
        Verify();
        Debug.Log("[R7-MixedV2] UPGRADED="+changed+" sourceSOmodified=0");
    }

    [MenuItem("Game System Verification/R7 Native Mixed - V2 Data Gate (Edit Mode)")]
    public static void Verify()
    {
        var graphs=Resources.LoadAll<R7NativeMixedAbilityDefinition>(Folder);
        if(graphs.Length!=7 || graphs.Select(x=>x.StableId).Distinct().Count()!=7)
            throw new InvalidOperationException("Seven unique V2 graphs required");
        int passed=0;
        foreach(var g in graphs)
        {
            var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g.SourceAssetGuid));
            Guard(d,g);
            if(!g.IsValid || g.SchemaVersion!=2 || g.SourceCanBreakPart!=d.CanBreakPart)
                throw new InvalidOperationException("Invalid V2 identity "+g.StableId);
            for(int i=0;i<g.Operations.Length;i++)
            {
                var a=g.Operations[i];var e=d.EffectEntries[i];
                if(a.EntryIndex!=i || a.Trigger!=AbilityEventKind.RollUsing)
                    throw new InvalidOperationException("Timing/order mismatch "+g.StableId);
                if(e.Definition is AddBodyPartStatusEffect status)
                {
                    if(a.Kind!=R7MixedOperationKind.ApplyBleeding ||
                       a.StackAmount!=status.ResolveAuthoredValue(e.Overrides) ||
                       a.Duration!=status.ResolveAuthoredDuration(e.Overrides))
                        throw new InvalidOperationException("Status mismatch "+g.StableId);
                }
                else
                {
                    var so=new SerializedObject(e.Definition);
                    if(a.Kind!=R7MixedOperationKind.RolledPartDamage ||
                       a.DamageMultiplier!=e.Overrides.ResolveMultiplier(Number(so,"powerMultiplier",1f)) ||
                       a.DamageFlatBonus!=e.Overrides.ResolveFlatValue(Integer(so,"flatBonus")) ||
                       a.DamageType!=(DamageType)Integer(so,"damageType") ||
                       a.UseSkillCanBreakPart!=Flag(so,"useSkillCanBreakPart") ||
                       a.CanBreakPart!=Flag(so,"canBreakPart") ||
                       a.ApplyMomentum!=Flag(so,"applyMomentum") ||
                       a.ApplyGuard!=Flag(so,"applyGuard"))
                        throw new InvalidOperationException("Damage mismatch "+g.StableId);
                }
            }
            passed++;
        }
        Debug.Log("[R7-MixedV2] DATA PASS="+passed+" FAIL=0");
    }
}
#endif