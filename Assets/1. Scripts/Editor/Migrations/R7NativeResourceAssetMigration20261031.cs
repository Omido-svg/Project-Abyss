#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// One-time OFFLINE migration; source SkillDefinition/SkillUpgradeProfile
/// are read-only. The generated native SO has no old effect data references.
/// Preflight rejects ambiguous IDs, conditions or unsupported upgrades.
/// </summary>
public static class R7NativeResourceAssetMigration20261031
{
    public const string Folder =
        "Assets/2. Data/Resources/ProjectAbyss/NativeAbilities";

    public static int ResolveAmount(SkillDefinition source,int level)
    {
        var entry=source.EffectEntries.Single();
        var proxy=(TempBalanceSkillProxyEffectDefinition)entry.Definition;
        int amount=entry.Overrides?.OverrideAmount==true
            ?entry.Overrides.Amount:proxy.BaseAmount;
        for(int i=1;i<=level;i++)
        {
            var step=source.UpgradeProfile?.GetStep(i);
            if(step?.EffectPayloads==null)continue;
            foreach(var payload in step.EffectPayloads)
            {
                if(payload==null||!string.Equals(payload.TargetEffectKey,
                    entry.UpgradeKey,StringComparison.Ordinal))continue;
                if(payload.OverrideStack||payload.OverrideDuration||
                   payload.OverrideMinimum||payload.OverrideMaximum||
                   payload.OverrideFlatValue||payload.OverrideMultiplier||
                   payload.OverrideResourceKey||
                   payload.OverrideForceCharacterStatus||
                   payload.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException(
                        "Unsupported resource upgrade: "+source.SkillId);
                if(payload.OverrideAmount)amount=payload.Amount;
            }
        }
        if(amount<=0)throw new InvalidOperationException(
            "Native resource amount must be positive "+source.SkillId+" lv"+level);
        return amount;
    }

    private static bool IsConvertible(SkillDefinition d)
    {
        if(d==null||d.EffectEntries?.Count!=1||d.Effects?.Any(x=>x!=null)==true)
            return false;
        var e=d.EffectEntries[0];
        if(!(e?.Definition is TempBalanceSkillProxyEffectDefinition p)||
           (p.Operation!=TempBalanceSkillProxyOperation.GainBlock&&
            p.Operation!=TempBalanceSkillProxyOperation.GainPrestige&&
            p.Operation!=TempBalanceSkillProxyOperation.TurnClashPowerBonus)||
           p.TargetSelector!=SkillEffectTargetSelector.CurrentTarget||
           (p.Conditions?.Count??0)!=0||
           (e.Conditions?.Count??0)!=0||e.RestrictToRoll)
            return false;
        if(e.EffectiveTiming!=SkillEffectTiming.OnExecute&&
           e.EffectiveTiming!=SkillEffectTiming.OnExchangeWin)
            return false;
        if(d.Rolls!=null && d.Rolls.Any(r=>r!=null&&
            (r.EffectEntries?.Any(x=>x?.Definition!=null)==true||
             r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true||
             r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true)))
            return false;
        return true;
    }

    [MenuItem("Game System Verification/R7 Native SO - Convert 15 Resource Effects (Edit Mode)")]
    public static void Convert()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
        var items=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new{
                Guid=g,
                Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g))
            }).Where(x=>x.Data!=null).ToArray();
        var duplicateIds=items.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).Select(g=>g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var selected=items.Where(x=>!duplicateIds.Contains(x.Data.SkillId)
            &&IsConvertible(x.Data))
            .OrderBy(x=>x.Data.SkillId,StringComparer.Ordinal).ToArray();
        if(selected.Length!=15)
            throw new InvalidOperationException(
                "Conversion set changed; expected 15, found "+selected.Length);
        var staged=new List<(string Path,R7NativeAbilityDefinition Data)>();
        foreach(var x in selected)
        {
            var d=x.Data;
            string filename=d.SkillId.Replace('.','_')+".asset";
            string path=Folder+"/"+filename;
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null||
                File.Exists(path))
                throw new InvalidOperationException("Refusing existing native SO overwrite: "+path);
            var e=d.EffectEntries[0];
            var p=(TempBalanceSkillProxyEffectDefinition)e.Definition;
            var data=ScriptableObject.CreateInstance<R7NativeAbilityDefinition>();
            data.name=d.SkillId.Replace('.','_');
            data.StableId=d.SkillId;
            data.SourceAssetGuid=x.Guid;
            data.SourceAssetName=d.name;
            data.Trigger=e.EffectiveTiming==SkillEffectTiming.OnExecute
                ?AbilityEventKind.RollUsing:AbilityEventKind.ExchangeWon;
            data.Operation=p.Operation==TempBalanceSkillProxyOperation.GainBlock
                ?R7NativeResourceKind.Block:
                p.Operation==TempBalanceSkillProxyOperation.GainPrestige
                ?R7NativeResourceKind.Prestige:
                R7NativeResourceKind.TurnClashPower;
            data.Level0Amount=ResolveAmount(d,0);
            data.Level1Amount=ResolveAmount(d,1);
            data.Level2Amount=ResolveAmount(d,2);
            data.RestrictToRoll=false;
            if(!data.IsValid)throw new InvalidOperationException(
                "Invalid native conversion "+d.SkillId);
            staged.Add((path,data));
        }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        foreach(var item in staged)
            AssetDatabase.CreateAsset(item.Data,item.Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        R7ProductionNativeResourceMigration.ResetCatalogCache();
        int loaded=Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities").Length;
        if(loaded!=15)throw new InvalidOperationException(
            "Created but Resources index invalid: "+loaded);
        Debug.Log("[R7-NativeSO] CREATED="+staged.Count+
            " sourceSOmodified=0 amountLevels=0,1,2 destination="+Folder);
    }
}
#endif