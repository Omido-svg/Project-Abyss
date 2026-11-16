#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

// Idempotent data-only conversion of the seven staged mixed-card graphs.
// Legacy SkillDefinitions and effect templates are never modified.
public static class NativeMixedAssetVerification
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
    private static void Guard(SkillDefinition d, NativeMixedAbilityDefinition graph)
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
                if(!NativeStatusCompatibilityExecutor.CanConvert(status) ||
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


    public static void Verify()
    {
        var graphs=Resources.LoadAll<NativeMixedAbilityDefinition>(Folder);
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
                    if(a.Kind!=MixedOperationKind.ApplyBleeding ||
                       a.StackAmount!=status.ResolveAuthoredValue(e.Overrides) ||
                       a.Duration!=status.ResolveAuthoredDuration(e.Overrides))
                        throw new InvalidOperationException("Status mismatch "+g.StableId);
                }
                else
                {
                    var so=new SerializedObject(e.Definition);
                    if(a.Kind!=MixedOperationKind.RolledPartDamage ||
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