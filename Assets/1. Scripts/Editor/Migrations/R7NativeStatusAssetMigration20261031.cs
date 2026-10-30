#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeStatusAssetMigration20261031
{
    public const string Folder=
        "Assets/2. Data/Resources/ProjectAbyss/NativeStatuses";
    public static int ResolveStacks(SkillDefinition d,int level)
    {
        var entry=d.EffectEntries.Single();
        var effect=(TempBalanceSkillProxyEffectDefinition)entry.Definition;
        int amount=entry.Overrides?.OverrideAmount==true
            ?entry.Overrides.Amount:effect.BaseAmount;
        for(int l=1;l<=level;l++)
        {
            var payloads=d.UpgradeProfile?.GetStep(l)?.EffectPayloads;
            if(payloads==null)continue;
            foreach(var p in payloads)
            {
                if(p==null||p.TargetEffectKey!=entry.UpgradeKey)continue;
                if(p.OverrideStack||p.OverrideDuration||
                   p.OverrideMinimum||p.OverrideMaximum||
                   p.OverrideFlatValue||p.OverrideMultiplier||
                   p.OverrideResourceKey||p.OverrideForceCharacterStatus||
                   p.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException(
                        "Unsupported bleeding upgrade: "+d.SkillId);
                if(p.OverrideAmount)amount=p.Amount;
            }
        }
        if(amount<=0)throw new InvalidOperationException(
            "Nonpositive native status amount "+d.SkillId);
        return amount;
    }
    private static bool Convertible(SkillDefinition d)
    {
        if(d==null||d.EffectEntries?.Count!=1||
            d.Effects?.Any(x=>x!=null)==true)return false;
        var e=d.EffectEntries[0];
        if(!(e?.Definition is TempBalanceSkillProxyEffectDefinition proxy)||
           proxy.Operation!=TempBalanceSkillProxyOperation.TargetBleeding ||
           proxy.TargetSelector!=SkillEffectTargetSelector.CurrentTarget ||
           (e.Conditions?.Count??0)>0||(proxy.Conditions?.Count??0)>0||
           e.RestrictToRoll ||
           (e.EffectiveTiming!=SkillEffectTiming.OnExecute&&
            e.EffectiveTiming!=SkillEffectTiming.OnExchangeWin))return false;
        return d.Rolls==null||d.Rolls.All(r=>r==null||
            (r.EffectEntries?.Any(x=>x?.Definition!=null)!=true&&
             r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)!=true&&
             r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)!=true));
    }

    [MenuItem("Game System Verification/R7 Native SO - Convert 9 Bleeding Effects (Edit Mode)")]
    public static void Convert()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode only");
        var source=AssetDatabase.FindAssets("t:SkillDefinition").Select(g=>new{
            Guid=g,Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g))
        }).Where(x=>x.Data!=null).ToArray();
        var duplicates=source.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).Select(g=>g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var chosen=source.Where(x=>!duplicates.Contains(x.Data.SkillId)&&
            Convertible(x.Data))
            .OrderBy(x=>x.Data.SkillId,StringComparer.Ordinal).ToArray();
        if(chosen.Length!=9)
            throw new InvalidOperationException(
                "Expected 9 unique native status candidates, found "+chosen.Length);
        var staged=new System.Collections.Generic.List<(string Path,R7NativeStatusAbilityDefinition Asset)>();
        foreach(var item in chosen)
        {
            string path=Folder+"/"+item.Data.SkillId.Replace('.','_')+".asset";
            if(File.Exists(path)||AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null)
                throw new InvalidOperationException("Refuse overwrite: "+path);
            var definition=ScriptableObject.CreateInstance<R7NativeStatusAbilityDefinition>();
            definition.name=item.Data.SkillId.Replace('.','_');
            definition.StableId=item.Data.SkillId;
            definition.SourceAssetGuid=item.Guid;
            definition.SourceAssetName=item.Data.name;
            definition.Trigger=item.Data.EffectEntries[0].EffectiveTiming==
                SkillEffectTiming.OnExchangeWin
                ?AbilityEventKind.ExchangeWon:AbilityEventKind.RollUsing;
            definition.Status=StatusEffectId.Bleeding;
            definition.Level0Stacks=ResolveStacks(item.Data,0);
            definition.Level1Stacks=ResolveStacks(item.Data,1);
            definition.Level2Stacks=ResolveStacks(item.Data,2);
            if(!definition.IsValid)throw new InvalidOperationException(
                "Invalid native status asset "+item.Data.SkillId);
            staged.Add((path,definition));
        }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        foreach(var x in staged)AssetDatabase.CreateAsset(x.Asset,x.Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        R7ProductionNativeBleedingMigration.ResetCatalogCache();
        var count=Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses").Length;
        if(count!=9)throw new InvalidOperationException(
            "Native bleeding resource index count="+count);
        Debug.Log("[R7-NativeStatusSO] CREATED=9 sourceSOmodified=0 upgrades=0,1,2");
    }
}
#endif