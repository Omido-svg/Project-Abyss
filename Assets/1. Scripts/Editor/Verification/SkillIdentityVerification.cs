#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>R7-2 targeted EditMode contract check. No R9/PlayMode required.</summary>
public static class SkillIdentityVerification
{
    private const string AssetPath=
        "Assets/2. Data/Resources/ProjectAbyss/R7/R7SkillIdentityRegistry.asset";

    public static bool IsApprovedGroup(IEnumerable<SkillDefinition> group)
    {
        var sources=group?.Where(x=>x!=null).ToArray()??Array.Empty<SkillDefinition>();
        if(sources.Length!=2)return false;
        var id=sources[0].SkillId;
        if(!SkillIdentityContract.IsDuplicateId(id) ||
           sources.Any(x=>x.SkillId!=id))return false;
        var registry=AssetDatabase.LoadAssetAtPath<SkillIdentityRegistry>(AssetPath);
        if(registry?.Entries==null)return false;
        var entries=registry.Entries.Where(x=>x!=null&&x.SkillId==id).ToArray();
        if(entries.Length!=2||entries.Count(x=>x.LegacyIdDefault)!=1)return false;
        if(entries.Any(x=>x.Definition==null||
             AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.Definition))!=x.AssetGuid))
            return false;
        return sources.All(s=>entries.Count(x=>ReferenceEquals(x.Definition,s))==1);
    }

    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("R7-2 EditMode only");
        int pass=0,fail=0;
        void Check(string name,bool ok)
        {
            if(ok){pass++;Debug.Log("[R7-2] PASS "+name);}
            else {fail++;Debug.LogError("[R7-2] FAIL "+name);}
        }

        var registry=AssetDatabase.LoadAssetAtPath<SkillIdentityRegistry>(AssetPath);
        var entries=registry?.Entries?.Where(x=>x!=null).ToArray()
            ??Array.Empty<SkillIdentityRegistry.Entry>();
        var skills=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SkillDefinition>)
            .Where(x=>x!=null).ToArray();
        var groups=skills.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).OrderBy(x=>x.Key).ToArray();
        var groupIds=new HashSet<string>(groups.Select(g=>g.Key),StringComparer.Ordinal);
        var activeEntries=entries.Where(x=>groupIds.Contains(x.SkillId)).ToArray();
        Check("S01 "+SkillCorpusVerification.CensusLabel+" skills",
            skills.Length==SkillCorpusVerification.ExpectedTotalSkillCount&&
            SkillCorpusVerification.IsExactLayeredCensus());
        Check("S02 "+SkillCorpusVerification.ExpectedDuplicateIdGroups+
            " collision groups / "+SkillCorpusVerification.ExpectedDuplicateIdAssets+" assets",
            SkillCorpusVerification.IsExpectedDuplicateCensus(groups.Length,
                groups.Sum(x=>x.Count())));
        Check("S03 one GUID registry binding per collision asset",
            activeEntries.Length==groups.Sum(x=>x.Count()));
        Check("S04 each two-source group has valid GUID contract",
            groups.All(g=>IsApprovedGroup(g)));
        Check("S05 every collision asset preserved and not dirty",
            groups.SelectMany(x=>x).All(x=>!EditorUtility.IsDirty(x)));
        Check("S06 registry GUIDs unique",
            entries.Select(x=>x.AssetGuid).Distinct(StringComparer.OrdinalIgnoreCase).Count()==entries.Length);
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
        bool activeBindings=olaf!=null&&yujin!=null&&activeEntries
            .Where(x=>x.LegacyIdDefault).All(x=>
                (x.SkillId.StartsWith("olaf.",StringComparison.Ordinal)?olaf:yujin)
                .EnumerateAllDefinitions().Contains(x.Definition));
        Check("S09 fallback GUIDs match current active loadouts",activeBindings);

        bool oldDefault=true,exactGuid=true,badGuid=true;
        foreach(var g in groups)
        {
            var set=g.ToArray();
            oldDefault &= SkillIdentityContract.TryResolve(g.Key,null,set,
                out var oldSource,out var _) &&
                entries.Any(e=>e.SkillId==g.Key&&e.LegacyIdDefault&&
                    ReferenceEquals(e.Definition,oldSource));
            foreach(var e in entries.Where(x=>x.SkillId==g.Key))
                exactGuid &= SkillIdentityContract.TryResolve(g.Key,
                    e.AssetGuid,set,out var actual,out var _) &&
                    ReferenceEquals(actual,e.Definition);
            badGuid &= !SkillIdentityContract.TryResolve(g.Key,
                "00000000000000000000000000000000",set,out var _,out var _);
        }
        Check("S10 old ID-only saves pick approved default",oldDefault);
        Check("S11 GUID qualified restores exact asset",exactGuid);
        Check("S12 unknown GUID rejects instead of substituting",badGuid);

        // 11-21: 첫 충돌 그룹으로 저장 계약을 시험한다. 충돌이 없으면(11-20 중복 정리 뒤)
        // S13~S18은 해당 없음 → 통과로 두고, 대신 S19가 합쳐진 ID의 옛 GUID 저장을 본다.
        string sampleId=groups.Select(g=>g.Key).FirstOrDefault();
        var p=activeEntries.FirstOrDefault(x=>x.SkillId==sampleId&&x.LegacyIdDefault);
        var alternate=activeEntries.FirstOrDefault(x=>x.SkillId==sampleId&&!x.LegacyIdDefault);
        bool noCollisions=groups.Length==0;
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
            string na=noCollisions?" (N/A: no collision groups)":"";
            Check("S13 new-save GUID roundtrip"+na,noCollisions||roundtrip);
            Check("S14 legacy ID-only save restoration"+na,noCollisions||backward);
            Check("S15 non-default GUID can be restored"+na,noCollisions||alternative);
            Check("S16 ownership does not cross collision variants"+na,noCollisions||ownership);
            Check("S17 invalid GUID leaves loadout unchanged"+na,noCollisions||atomic);
            Check("S18 upgrade levels independent by GUID"+na,noCollisions||upgrade);
        }
        finally{UnityEngine.Object.DestroyImmediate(tmp);}

        // S19: 중복이 풀린 ID(레지스트리에 남은 옛 항목)의 GUID 붙은 옛 저장은
        // 남은 유일한 에셋으로 복원돼야 한다.
        bool mergedRestore=true;
        foreach(var e in entries.Where(x=>!groupIds.Contains(x.SkillId)))
        {
            var sole=skills.Where(x=>x.SkillId==e.SkillId).ToArray();
            mergedRestore &= sole.Length==1 &&
                SkillIdentityContract.TryResolve(e.SkillId,e.AssetGuid,skills,
                    out var restored,out var _) &&
                ReferenceEquals(restored,sole[0]);
        }
        Check("S19 merged-ID GUID saves restore the sole remaining asset",mergedRestore);

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