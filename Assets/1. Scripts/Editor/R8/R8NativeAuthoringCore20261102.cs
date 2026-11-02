#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R8 unified authoring core. Source SkillDefinition is ALWAYS read only.
/// Three proven single-effect native schemas may be created; complex mixed
/// graphs, preparation and specialist keywords are intentionally gated to
/// validators and must not be approximated or silently wrapped by adapters.
/// </summary>
public static class R8NativeAuthoringCore20261102
{
    public enum Family
    {
        Unsupported, Resource, BleedingStatus, UniqueGauge,
        ExistingMixed, ExistingPreparation
    }
    public enum Verdict { Blocked, ReadyToCreate, ExistsValid, StagedValid, ExistsMismatch }

    public sealed class Preview
    {
        public string SourcePath,SourceGuid,SkillId,SourceName,Destination,Reason;
        public Family Kind;
        public Verdict Status;
        public int L0,L1,L2;
        public AbilityEventKind Trigger;
        public TempBalanceSkillProxyOperation Operation;
        public bool Safe => Status==Verdict.ReadyToCreate||Status==Verdict.ExistsValid;
        public override string ToString() =>
            Kind+" | "+Status+" | "+SkillId+" | "+
            L0+"/"+L1+"/"+L2+" | "+Reason;
    }

    public const string Root="Assets/2. Data/Resources/ProjectAbyss";
    public static string Folder(Family f)=>
        f==Family.Resource?Root+"/NativeAbilities":
        f==Family.BleedingStatus?Root+"/NativeStatuses":
        f==Family.UniqueGauge?Root+"/NativeUniqueGauges":
        string.Empty;
    public static IReadOnlyList<SkillDefinition> Sources() =>
        AssetDatabase.FindAssets("t:SkillDefinition")
          .Select(AssetDatabase.GUIDToAssetPath)
          .OrderBy(x=>x,StringComparer.Ordinal)
          .Select(AssetDatabase.LoadAssetAtPath<SkillDefinition>)
          .Where(x=>x!=null).ToArray();

    private static HashSet<string> DuplicateIds(IReadOnlyList<SkillDefinition> sources)=>
        new HashSet<string>(sources.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).Select(g=>g.Key),StringComparer.Ordinal);

    public static string StagingFolder(Family family) =>
        "Assets/2. Data/R8_Staging/"+family;

    private static bool HasRollEffects(SkillDefinition source) =>
        source.Rolls!=null&&source.Rolls.Any(r=>r!=null&&
            (r.EffectEntries?.Any(x=>x?.Definition!=null)==true||
             r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true||
             r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true));

    private static bool DisallowedUpgrade(SkillUpgradeStep step,string key) =>
        step?.EffectPayloads!=null&&step.EffectPayloads.Any(x=>x!=null&&
            string.Equals(x.TargetEffectKey,key,StringComparison.Ordinal)&&
            (x.OverrideStack||x.OverrideDuration||x.OverrideMinimum||
             x.OverrideMaximum||x.OverrideFlatValue||x.OverrideMultiplier||
             x.OverrideResourceKey||x.OverrideForceCharacterStatus||
             x.OverrideGiveToSelectedTarget));

    private static bool DifferentGraph(Preview p,UnityEngine.Object native)
    {
        if(native==null)return true;
        if(native is R7NativeAbilityDefinition r)
        {
            var expected=p.Operation==TempBalanceSkillProxyOperation.GainBlock?
                R7NativeResourceKind.Block:
                p.Operation==TempBalanceSkillProxyOperation.GainPrestige?
                R7NativeResourceKind.Prestige:
                R7NativeResourceKind.TurnClashPower;
            return !r.IsValid||r.StableId!=p.SkillId||
                r.SourceAssetGuid!=p.SourceGuid||r.SourceAssetName!=p.SourceName||
                r.Trigger!=p.Trigger||r.Operation!=expected||
                r.Level0Amount!=p.L0||r.Level1Amount!=p.L1||
                r.Level2Amount!=p.L2||r.RestrictToRoll;
        }
        if(native is R7NativeStatusAbilityDefinition status)
            return !status.IsValid||status.StableId!=p.SkillId||
                status.SourceAssetGuid!=p.SourceGuid||
                status.SourceAssetName!=p.SourceName||
                status.Trigger!=p.Trigger||
                status.Status!=StatusEffectId.Bleeding||
                status.Level0Stacks!=p.L0||
                status.Level1Stacks!=p.L1||
                status.Level2Stacks!=p.L2||status.RestrictToRoll;
        if(native is R7NativeUniqueGaugeDefinition gauge)
        {
            var expected=p.Operation==TempBalanceSkillProxyOperation.OlafMadness?
                R7UniqueGaugeKind.OlafMadness:R7UniqueGaugeKind.YujinSense;
            return !gauge.IsValid||gauge.StableId!=p.SkillId||
                gauge.SourceAssetGuid!=p.SourceGuid||
                gauge.SourceAssetName!=p.SourceName||
                gauge.Trigger!=p.Trigger||gauge.Kind!=expected||
                gauge.Level0Amount!=p.L0||gauge.Level1Amount!=p.L1||
                gauge.Level2Amount!=p.L2||gauge.RestrictToRoll;
        }
        return true;
    }

    public static Preview Analyze(SkillDefinition source,
        IReadOnlyList<SkillDefinition> catalog=null)
    {
        var p=new Preview{Kind=Family.Unsupported,Status=Verdict.Blocked,
            SourcePath=source==null?"":AssetDatabase.GetAssetPath(source)};
        if(source==null){p.Reason="Missing source";return p;}
        p.SkillId=source.SkillId;p.SourceName=source.name;
        p.SourceGuid=AssetDatabase.AssetPathToGUID(p.SourcePath);
        if(string.IsNullOrWhiteSpace(p.SourceGuid)||!p.SourcePath.StartsWith("Assets/",StringComparison.Ordinal))
        {p.Reason="Invalid AssetDatabase path/GUID";return p;}
        if(string.IsNullOrWhiteSpace(p.SkillId) ||
           !System.Text.RegularExpressions.Regex.IsMatch(p.SkillId,
             @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
        {p.Reason="Invalid or unsafe stable ID/path characters";return p;}
        var all=catalog??Sources();
        if(DuplicateIds(all).Contains(source.SkillId))
        {p.Reason="DUPLICATE_STABLE_ID (manual GUID resolution required)";return p;}
        if(source.EffectEntries?.Count!=1||
           source.EffectEntries[0]?.Definition==null||
           source.Effects?.Any(x=>x!=null)==true||HasRollEffects(source))
        {p.Reason="Requires native multi-effect/roll-aware specialist";return p;}

        var entry=source.EffectEntries[0];
        if(!(entry.Definition is TempBalanceSkillProxyEffectDefinition effect))
        {p.Reason="Unsupported non-proxy effect; never use legacy adapter";return p;}
        if(effect.TargetSelector!=SkillEffectTargetSelector.CurrentTarget ||
           (effect.Conditions?.Count??0)>0||(entry.Conditions?.Count??0)>0||
           entry.RestrictToRoll||entry.Overrides?.OverrideInfiniteDuration==true)
        {p.Reason="Conditional/roll/target semantics require manual native contract";return p;}
        if(entry.EffectiveTiming!=SkillEffectTiming.OnExecute&&
           entry.EffectiveTiming!=SkillEffectTiming.OnExchangeWin)
        {p.Reason="Unsupported timing";return p;}
        if(DisallowedUpgrade(source.UpgradeProfile?.Upgrade1,entry.UpgradeKey)||
           DisallowedUpgrade(source.UpgradeProfile?.Upgrade2,entry.UpgradeKey))
        {p.Reason="Upgrade changes unsupported operation parameters";return p;}

        p.Operation=effect.Operation;
        p.Kind=effect.Operation==TempBalanceSkillProxyOperation.GainBlock||
                  effect.Operation==TempBalanceSkillProxyOperation.GainPrestige||
                  effect.Operation==TempBalanceSkillProxyOperation.TurnClashPowerBonus
                  ?Family.Resource:
               effect.Operation==TempBalanceSkillProxyOperation.TargetBleeding
                  ?Family.BleedingStatus:
               effect.Operation==TempBalanceSkillProxyOperation.OlafMadness||
               effect.Operation==TempBalanceSkillProxyOperation.YujinSense
                  ?Family.UniqueGauge:Family.Unsupported;
        if(p.Kind==Family.Unsupported)
        {p.Reason="Unsupported proxy operation";return p;}

        p.Trigger=entry.EffectiveTiming==SkillEffectTiming.OnExecute?
            AbilityEventKind.RollUsing:AbilityEventKind.ExchangeWon;
        try
        {
            if(p.Kind==Family.BleedingStatus)
            {
                p.L0=R7NativeStatusAssetMigration20261031.ResolveStacks(source,0);
                p.L1=R7NativeStatusAssetMigration20261031.ResolveStacks(source,1);
                p.L2=R7NativeStatusAssetMigration20261031.ResolveStacks(source,2);
            }
            else
            {
                p.L0=R7NativeResourceAssetMigration20261031.ResolveAmount(source,0);
                p.L1=R7NativeResourceAssetMigration20261031.ResolveAmount(source,1);
                p.L2=R7NativeResourceAssetMigration20261031.ResolveAmount(source,2);
            }
        }
        catch(Exception e){p.Reason="Amount or upgrades unsupported: "+e.Message;return p;}

        if(p.L0<=0||p.L1<=0||p.L2<=0)
        {p.Reason="Invalid level values";return p;}
        // Do not automatically create a unique gauge for an unknown actor type.
        if(p.Kind==Family.UniqueGauge&&
           !(p.SkillId.StartsWith("olaf.",StringComparison.Ordinal) &&
              p.Operation==TempBalanceSkillProxyOperation.OlafMadness ||
             p.SkillId.StartsWith("yujin.",StringComparison.Ordinal) &&
              p.Operation==TempBalanceSkillProxyOperation.YujinSense))
        {p.Reason="Unique gauge owner/operation mismatch";return p;}

        var file=p.SkillId.Replace('.','_')+".asset";
        var production=Folder(p.Kind)+"/"+file;
        var existing=AssetDatabase.LoadMainAssetAtPath(production);
        if(existing!=null||File.Exists(production))
        {
            p.Destination=production;
            p.Status=existing==null||DifferentGraph(p,existing)
                ?Verdict.ExistsMismatch:Verdict.ExistsValid;
            p.Reason=p.Status==Verdict.ExistsValid
                ?"Production native graph exactly matches source"
                :"PRODUCTION FILE MISMATCH; never overwrite";
            return p;
        }
        // Never stage a second native graph for a GUID already mapped under
        // another file or family. A GUID cannot be routed twice.
        bool ExistingGuidElsewhere()=>
            Resources.LoadAll<R7NativeAbilityDefinition>("ProjectAbyss/NativeAbilities")
                .Any(x=>x.SourceAssetGuid==p.SourceGuid) ||
            Resources.LoadAll<R7NativeStatusAbilityDefinition>("ProjectAbyss/NativeStatuses")
                .Any(x=>x.SourceAssetGuid==p.SourceGuid) ||
            Resources.LoadAll<R7NativeMixedAbilityDefinition>("ProjectAbyss/NativeMixed")
                .Any(x=>x.SourceAssetGuid==p.SourceGuid) ||
            Resources.LoadAll<R7NativeUniqueGaugeDefinition>("ProjectAbyss/NativeUniqueGauges")
                .Any(x=>x.SourceAssetGuid==p.SourceGuid) ||
            Resources.LoadAll<R7NativePreparationSetupDefinition>("ProjectAbyss/NativePreparations")
                .Any(x=>x.SourceAssetGuid==p.SourceGuid);
        if(ExistingGuidElsewhere())
        {p.Status=Verdict.Blocked;p.Reason="Native GUID already routed elsewhere";return p;}
        p.Destination=StagingFolder(p.Kind)+"/"+file;
        var staged=AssetDatabase.LoadMainAssetAtPath(p.Destination);
        if(staged!=null||File.Exists(p.Destination))
        {
            p.Status=staged==null||DifferentGraph(p,staged)
                ?Verdict.ExistsMismatch:Verdict.StagedValid;
            p.Reason=p.Status==Verdict.StagedValid
                ?"Valid staged graph (not in Resources, no runtime routing)"
                :"STAGED FILE MISMATCH; never overwrite";
            return p;
        }
        p.Status=Verdict.ReadyToCreate;
        p.Reason="Safe to stage only; requires future runtime parity approval";
        return p;
    }

    public static IReadOnlyList<Preview> AnalyzeAll()
    {
        var src=Sources();
        return src.Select(s=>Analyze(s,src)).ToArray();
    }

    public static string Sha256(string path)
    {
        using(var alg=SHA256.Create())
        using(var file=File.OpenRead(path))
            return BitConverter.ToString(alg.ComputeHash(file)).Replace("-","")
                .ToLowerInvariant();
    }

    public static void CreateMissing(IEnumerable<Preview> requested)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        if(requested==null)throw new ArgumentNullException(nameof(requested));
        // Never use an old Preview as approval: revalidate by GUID and identity.
        var sources=Sources();
        var prepared=new List<(Preview Preview,ScriptableObject Graph)>();
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in requested)
        {
            if(item==null||item.Status!=Verdict.ReadyToCreate||
               !seen.Add(item.SourceGuid))
                throw new InvalidOperationException("Only distinct explicitly-approved new previews");
            var path=AssetDatabase.GUIDToAssetPath(item.SourceGuid);
            var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            var fresh=Analyze(source,sources);
            if(fresh.Status!=Verdict.ReadyToCreate||
               fresh.SourceGuid!=item.SourceGuid||
               fresh.SkillId!=item.SkillId||
               fresh.Destination!=item.Destination||
               fresh.Kind!=item.Kind||
               fresh.L0!=item.L0||fresh.L1!=item.L1||fresh.L2!=item.L2||
               fresh.Trigger!=item.Trigger||fresh.Operation!=item.Operation)
                throw new InvalidOperationException("Stale or altered preview: "+item.SkillId);
            ScriptableObject graph;
            switch(fresh.Kind)
            {
                case Family.Resource:
                {
                    var d=ScriptableObject.CreateInstance<R7NativeAbilityDefinition>();
                    d.SchemaVersion=1;d.StableId=fresh.SkillId;
                    d.SourceAssetGuid=fresh.SourceGuid;d.SourceAssetName=fresh.SourceName;
                    d.Trigger=fresh.Trigger;
                    d.Operation=fresh.Operation==TempBalanceSkillProxyOperation.GainBlock?
                        R7NativeResourceKind.Block:
                        fresh.Operation==TempBalanceSkillProxyOperation.GainPrestige?
                        R7NativeResourceKind.Prestige:R7NativeResourceKind.TurnClashPower;
                    d.Level0Amount=fresh.L0;d.Level1Amount=fresh.L1;d.Level2Amount=fresh.L2;
                    d.RestrictToRoll=false;d.RollNumber=1;
                    if(!d.IsValid)throw new InvalidOperationException("Invalid resource graph");
                    graph=d;break;
                }
                case Family.BleedingStatus:
                {
                    var d=ScriptableObject.CreateInstance<R7NativeStatusAbilityDefinition>();
                    d.SchemaVersion=1;d.StableId=fresh.SkillId;
                    d.SourceAssetGuid=fresh.SourceGuid;d.SourceAssetName=fresh.SourceName;
                    d.Trigger=fresh.Trigger;d.Status=StatusEffectId.Bleeding;
                    d.Level0Stacks=fresh.L0;d.Level1Stacks=fresh.L1;d.Level2Stacks=fresh.L2;
                    d.RestrictToRoll=false;d.RollNumber=1;
                    if(!d.IsValid)throw new InvalidOperationException("Invalid status graph");
                    graph=d;break;
                }
                case Family.UniqueGauge:
                {
                    var d=ScriptableObject.CreateInstance<R7NativeUniqueGaugeDefinition>();
                    d.SchemaVersion=1;d.StableId=fresh.SkillId;
                    d.SourceAssetGuid=fresh.SourceGuid;d.SourceAssetName=fresh.SourceName;
                    d.Kind=fresh.Operation==TempBalanceSkillProxyOperation.OlafMadness?
                        R7UniqueGaugeKind.OlafMadness:R7UniqueGaugeKind.YujinSense;
                    d.Trigger=fresh.Trigger;
                    d.Level0Amount=fresh.L0;d.Level1Amount=fresh.L1;d.Level2Amount=fresh.L2;
                    d.RestrictToRoll=false;d.RollNumber=1;
                    if(!d.IsValid)throw new InvalidOperationException("Invalid unique gauge graph");
                    graph=d;break;
                }
                default:throw new InvalidOperationException("Unsupported native family");
            }
            prepared.Add((fresh,graph));
        }
        // All candidates, paths and source metas pass before the first create.
        var root=Directory.GetParent(Application.dataPath).FullName;
        foreach(var entry in prepared)
        {
            var original=Path.Combine(root,entry.Preview.SourcePath);
            if(File.Exists(entry.Preview.Destination)||
               AssetDatabase.LoadMainAssetAtPath(entry.Preview.Destination)!=null||
               !File.Exists(original)||!File.Exists(original+".meta"))
                throw new InvalidOperationException("Source/meta/destination guard "+entry.Preview.SkillId);
        }
        // Distinct per-file backup manifests prevent silent overwrites.
        var backup=Path.Combine(root,"ProjectAbyss_R8_ExplicitCreateBackup_20261102");
        foreach(var e in prepared)
        {
            var original=Path.Combine(root,e.Preview.SourcePath);
            var target=Path.Combine(backup,"ORIGINALS",e.Preview.SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            foreach(var suffix in new[]{"",".meta"})
            {
                if(File.Exists(target+suffix)&&Sha256(target+suffix)!=Sha256(original+suffix))
                    throw new InvalidOperationException("Backup collision "+e.Preview.SkillId);
                if(!File.Exists(target+suffix))File.Copy(original+suffix,target+suffix);
                if(Sha256(target+suffix)!=Sha256(original+suffix))
                    throw new InvalidOperationException("Backup SHA verification failed");
            }
        }
        var created=new List<string>();
        try
        {
            foreach(var entry in prepared)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.Preview.Destination));
                AssetDatabase.CreateAsset(entry.Graph,entry.Preview.Destination);
                created.Add(entry.Preview.Destination);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach(var entry in prepared)
            {
                var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(entry.Preview.SourceGuid));
                var after=Analyze(source,Sources());
                if(after.Status!=Verdict.StagedValid)
                    throw new InvalidOperationException("Post-create semantic mismatch "+entry.Preview.SkillId);
            }
        }
        catch
        {
            foreach(var path in created)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            throw;
        }
        Debug.Log("[R8-Create] STAGED "+created.Count+
            " native SOs outside Resources. No runtime routing until separately approved.");
    }
}
#endif