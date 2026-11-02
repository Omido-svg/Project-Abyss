#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>R7-2 targeted EditMode contract check. No R9/PlayMode required.</summary>
public static class R7Stage2SkillIdContractGate20261102
{
    private const string AssetPath=
        "Assets/2. Data/Resources/ProjectAbyss/R7/R7SkillIdentityRegistry.asset";

    public static bool IsApprovedGroup(IEnumerable<SkillDefinition> group)
    {
        var sources=group?.Where(x=>x!=null).ToArray()??Array.Empty<SkillDefinition>();
        if(sources.Length!=2)return false;
        var id=sources[0].SkillId;
        if(!R7SkillIdentityContract20261102.IsDuplicateId(id) ||
           sources.Any(x=>x.SkillId!=id))return false;
        var registry=AssetDatabase.LoadAssetAtPath<R7SkillIdentityRegistry20261102>(AssetPath);
        if(registry?.Entries==null)return false;
        var entries=registry.Entries.Where(x=>x!=null&&x.SkillId==id).ToArray();
        if(entries.Length!=2||entries.Count(x=>x.LegacyIdDefault)!=1)return false;
        if(entries.Any(x=>x.Definition==null||
             AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.Definition))!=x.AssetGuid))
            return false;
        return sources.All(s=>entries.Count(x=>ReferenceEquals(x.Definition,s))==1);
    }

    [MenuItem("Game System Verification/R7-2 - Skill ID GUID and Save Contract (Edit Only)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("R7-2 EditMode only");
        int pass=0,fail=0;
        void Check(string name,bool ok)
        {
            if(ok){pass++;Debug.Log("[R7-2] PASS "+name);}
            else {fail++;Debug.LogError("[R7-2] FAIL "+name);}
        }

        var registry=AssetDatabase.LoadAssetAtPath<R7SkillIdentityRegistry20261102>(AssetPath);
        var entries=registry?.Entries?.Where(x=>x!=null).ToArray()
            ??Array.Empty<R7SkillIdentityRegistry20261102.Entry>();
        var skills=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SkillDefinition>)
            .Where(x=>x!=null).ToArray();
        var groups=skills.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).OrderBy(x=>x.Key).ToArray();
        Check("S01 145 source skills",skills.Length==145);
        Check("S02 4 exact collision groups / 8 assets",
            groups.Length==4&&groups.Sum(x=>x.Count())==8);
        Check("S03 8 GUID registry bindings",entries.Length==8);
        Check("S04 each two-source group has valid GUID contract",
            groups.All(g=>IsApprovedGroup(g)));
        Check("S05 every collision asset preserved and not dirty",
            groups.SelectMany(x=>x).All(x=>!EditorUtility.IsDirty(x)));
        Check("S06 registry GUIDs unique",
            entries.Select(x=>x.AssetGuid).Distinct(StringComparer.OrdinalIgnoreCase).Count()==8);
        Check("S07 all registered source GUID values match actual meta",
            entries.All(x=>x.Definition!=null&&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.Definition))==x.AssetGuid));
        Check("S08 one legacy ID fallback per group",
            groups.All(g=>entries.Count(x=>x.SkillId==g.Key&&x.LegacyIdDefault)==1));

        var olaf=AssetDatabase.FindAssets("t:CharacterCombatLoadout")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>)
            .FirstOrDefault(x=>x!=null&&x.name=="Olaf_TODO_Loadout");
        var yujin=AssetDatabase.FindAssets("t:CharacterCombatLoadout")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>)
            .FirstOrDefault(x=>x!=null&&x.name=="Yujin_TODO_Loadout");
        bool activeBindings=olaf!=null&&yujin!=null&&entries
            .Where(x=>x.LegacyIdDefault).All(x=>
                (x.SkillId.StartsWith("olaf.",StringComparison.Ordinal)?olaf:yujin)
                .EnumerateAllDefinitions().Contains(x.Definition));
        Check("S09 fallback GUIDs match current active loadouts",activeBindings);

        bool oldDefault=true,exactGuid=true,badGuid=true;
        foreach(var g in groups)
        {
            var set=g.ToArray();
            oldDefault &= R7SkillIdentityContract20261102.TryResolve(g.Key,null,set,
                out var oldSource,out var _) &&
                entries.Any(e=>e.SkillId==g.Key&&e.LegacyIdDefault&&
                    ReferenceEquals(e.Definition,oldSource));
            foreach(var e in entries.Where(x=>x.SkillId==g.Key))
                exactGuid &= R7SkillIdentityContract20261102.TryResolve(g.Key,
                    e.AssetGuid,set,out var actual,out var _) &&
                    ReferenceEquals(actual,e.Definition);
            badGuid &= !R7SkillIdentityContract20261102.TryResolve(g.Key,
                "00000000000000000000000000000000",set,out var _,out var _);
        }
        Check("S10 old ID-only saves pick approved default",oldDefault);
        Check("S11 GUID qualified restores exact asset",exactGuid);
        Check("S12 unknown GUID rejects instead of substituting",badGuid);

        var p=entries.FirstOrDefault(x=>x.SkillId=="olaf.duel.standard"&&x.LegacyIdDefault);
        var alternate=entries.FirstOrDefault(x=>x.SkillId=="olaf.duel.standard"&&!x.LegacyIdDefault);
        var tmp=ScriptableObject.CreateInstance<CharacterCombatLoadout>();
        try
        {
            bool roundtrip=false,backward=false,alternative=false,atomic=false,ownership=false,upgrade=false;
            if(p?.Definition!=null&&alternate?.Definition!=null)
            {
                tmp.DuelSkills.Add(p.Definition);
                tmp.DuelSkillPool.Add(p.Definition);
                tmp.DuelSkillPool.Add(alternate.Definition);
                var first=new CharacterSkillLoadoutRuntime(null,tmp);
                var saved=first.CaptureState();
                roundtrip=saved.DuelSkillGuids.Count==1&&saved.DuelSkillGuids[0]==p.AssetGuid &&
                    new CharacterSkillLoadoutRuntime(null,tmp).TryRestoreState(saved,out var _);

                var old=new CharacterSkillLoadoutState();
                old.DuelSkillIds.Add(p.SkillId);
                old.OwnedSkillIds.Add(p.SkillId);
                var fresh=new CharacterSkillLoadoutRuntime(null,tmp);
                backward=fresh.TryRestoreState(old,out var _) &&
                    ReferenceEquals(fresh.GetEquipped(ActionType.Duel).Single(),p.Definition);

                var alt=new CharacterSkillLoadoutState();
                alt.DuelSkillIds.Add(p.SkillId);
                alt.DuelSkillGuids.Add(alternate.AssetGuid);
                alt.OwnedSkillIds.Add(p.SkillId);
                alt.OwnedSkillGuids.Add(alternate.AssetGuid);
                alternative=fresh.TryRestoreState(alt,out var _) &&
                    ReferenceEquals(fresh.GetEquipped(ActionType.Duel).Single(),alternate.Definition);
                ownership=fresh.Owns(alternate.Definition)&&!fresh.Owns(p.Definition);

                var corrupted=new CharacterSkillLoadoutState();
                corrupted.DuelSkillIds.Add(p.SkillId);
                corrupted.DuelSkillGuids.Add("00000000000000000000000000000000");
                atomic=!fresh.TryRestoreState(corrupted,out var _) &&
                    ReferenceEquals(fresh.GetEquipped(ActionType.Duel).Single(),alternate.Definition);

                var levels=new SkillUpgradeState();
                upgrade=levels.TrySetLevel(p.Definition,2)&&
                    levels.GetLevel(p.Definition)==2 && levels.GetLevel(alternate.Definition)==0;
            }
            Check("S13 new-save GUID roundtrip",roundtrip);
            Check("S14 legacy ID-only save restoration",backward);
            Check("S15 non-default GUID can be restored",alternative);
            Check("S16 ownership does not cross collision variants",ownership);
            Check("S17 invalid GUID leaves loadout unchanged",atomic);
            Check("S18 upgrade levels independent by GUID",upgrade);
        }
        finally{UnityEngine.Object.DestroyImmediate(tmp);}

        var csv=new List<string[]>{new[]{"SkillId","AssetGUID","AssetPath","AssetName","LegacyDefault","NativeMigrated"}};
        foreach(var e in entries.OrderBy(x=>x.SkillId).ThenBy(x=>x.AssetGuid))
            csv.Add(new[]{e.SkillId,e.AssetGuid,AssetDatabase.GetAssetPath(e.Definition),
                e.Definition.name,e.LegacyIdDefault.ToString(),"False"});
        var folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ProjectAbyss_R7_2_20261102");
        Directory.CreateDirectory(folder);
        string Quote(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        File.WriteAllLines(Path.Combine(folder,"R7_2_GUID_IDENTITY_CONTRACT.csv"),
            csv.Select(row=>string.Join(",",row.Select(Quote))),new UTF8Encoding(false));
        Debug.Log("[R7-2] RESULT PASS="+pass+" FAIL="+fail+
            " (EditMode only; no R9/PlayMode)");
        if(fail>0)throw new InvalidOperationException("R7-2 gate failed "+fail);
    }
}
#endif