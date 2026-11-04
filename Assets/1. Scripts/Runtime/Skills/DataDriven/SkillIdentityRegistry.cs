using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// R7-2 runtime identity table: pairs the four historical SkillIds with their
/// eight immutable Unity asset GUIDs. The ScriptableObject holds direct asset
/// references, so build/runtime resolution never depends on AssetDatabase.
/// </summary>
[CreateAssetMenu(menuName="Project Abyss/R7/Skill ID Collision Registry",
    fileName="R7SkillIdentityRegistry")]
public sealed class SkillIdentityRegistry : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string SkillId;
        public string AssetGuid;
        public SkillDefinition Definition;
        public bool LegacyIdDefault;
    }
    public List<Entry> Entries=new List<Entry>();

    public Entry Find(SkillDefinition definition) =>
        Entries?.FirstOrDefault(x=>x!=null&&ReferenceEquals(x.Definition,definition));
    public Entry Find(string skillId,string guid) =>
        Entries?.FirstOrDefault(x=>x!=null&&
            string.Equals(x.SkillId,skillId,StringComparison.Ordinal)&&
            string.Equals(x.AssetGuid,guid,StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Save compatibility contract:
/// - historical SkillId remains untouched on every original source asset;
/// - new saves add per-slot GUIDs without removing the old ID lists;
/// - old ID-only saves use the sole local candidate or an explicitly declared
///   PhaseE default when more than one candidate is present;
/// - a GUID-qualified save never silently substitutes another asset.
/// </summary>
public static class SkillIdentityContract
{
    private static readonly HashSet<string> DuplicatedIds=new HashSet<string>(
        new[]{
            "olaf.duel.standard",
            "olaf.preparation.bloto",
            "yujin.preparation.hwanhyeong.attack",
            "yujin.preparation.hwanhyeong.defense"
        },StringComparer.Ordinal);

    private static SkillIdentityRegistry Registry =>
        Resources.Load<SkillIdentityRegistry>(
            "ProjectAbyss/R7/R7SkillIdentityRegistry");

    public static bool IsDuplicateId(string id) =>
        id!=null&&DuplicatedIds.Contains(id);

    public static string GuidFor(SkillDefinition definition)
    {
        if(definition==null||!IsDuplicateId(definition.SkillId))return string.Empty;
        var entry=Registry?.Find(definition);
        return entry!=null && entry.SkillId==definition.SkillId ?
            entry.AssetGuid??string.Empty:string.Empty;
    }

    public static string UniqueKey(SkillDefinition definition)
    {
        if(definition==null)return string.Empty;
        if(!IsDuplicateId(definition.SkillId))return definition.SkillId;
        var guid=GuidFor(definition);
        return string.IsNullOrEmpty(guid)?string.Empty:"guid:"+guid.ToLowerInvariant();
    }

    public static bool TryResolve(string id,string guid,
        IEnumerable<SkillDefinition> available,
        out SkillDefinition definition,out string reason)
    {
        definition=null;
        reason=string.Empty;
        if(string.IsNullOrWhiteSpace(id))
        {reason="Saved SkillId is empty.";return false;}
        var matches=(available??Enumerable.Empty<SkillDefinition>())
            .Where(x=>x!=null&&string.Equals(x.SkillId,id,StringComparison.Ordinal))
            .Distinct().ToArray();
        if(matches.Length==0)
        {reason="No skill candidate for "+id;return false;}

        if(!IsDuplicateId(id))
        {
            if(!string.IsNullOrEmpty(guid))
            {reason="Unexpected GUID for non-colliding SkillId: "+id;return false;}
            if(matches.Length!=1)
            {reason="Ambiguous unregistered SkillId: "+id;return false;}
            definition=matches[0];return true;
        }

        var registry=Registry;
        if(registry?.Entries==null)
        {reason="Missing R7-2 identity registry for "+id;return false;}

        if(!string.IsNullOrEmpty(guid))
        {
            var match=registry.Find(id,guid);
            if(match==null||match.Definition==null||!matches.Contains(match.Definition))
            {reason="GUID-qualified skill unavailable (no substitution): "+id+" / "+guid;return false;}
            definition=match.Definition;return true;
        }
        if(matches.Length==1)
        {
            var sole=registry.Find(matches[0]);
            if(sole==null||sole.SkillId!=id)
            {reason="Unknown collision candidate: "+id;return false;}
            definition=matches[0];return true;
        }
        var defaults=registry.Entries.Where(x=>x!=null&&x.SkillId==id&&
            x.LegacyIdDefault&&x.Definition!=null&&matches.Contains(x.Definition)).ToArray();
        if(defaults.Length!=1)
        {reason="Ambiguous old save with no unique approved legacy default: "+id;return false;}
        definition=defaults[0].Definition;
        return true;
    }
}
