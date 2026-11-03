using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R7-11 canonical-bound 60-source run-item record. Source GUID, stable ID,
/// class restriction and snapshot of the TEMP source belong to this graph;
/// no unapproved power/stat/effect numbers are migrated from TEMP_BALANCE_V1.
/// All current 60 cards intentionally have no executable native combat effect.
/// </summary>
public enum R7NativeRunItemState20261103
{
    CanonicalEffectPending = 0,
    NativeReady = 1
}

public sealed class R7NativeRunItemGraph20261103 : ScriptableObject
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion = CurrentSchemaVersion;
    public string StableId;
    public string SourceGuid;
    public string SourceAssetName;
    public string SourceDisplayName;
    public RunItemTier SourceTier;
    public string CharacterKey;
    public string FormerCombatProxyGuid;
    public R7NativeRunItemState20261103 State =
        R7NativeRunItemState20261103.CanonicalEffectPending;

    // The old 6/6/6/6/6 common placeholders cannot establish which 10/8/6/4/2
    // authored final cards they are, so their tier mapping remains pending.
    public bool CanonicalTierMappingPending;
    [TextArea(2,8)] public string PendingReason;

    public bool IsValid =>
        SchemaVersion == CurrentSchemaVersion &&
        !string.IsNullOrWhiteSpace(StableId) &&
        SourceGuid?.Length == 32 &&
        !string.IsNullOrWhiteSpace(SourceAssetName) &&
        !string.IsNullOrWhiteSpace(SourceDisplayName) &&
        FormerCombatProxyGuid?.Length == 32 &&
        (SourceTier == RunItemTier.Class ||
         (SourceTier >= RunItemTier.Tier1 && SourceTier <= RunItemTier.Tier5)) &&
        (SourceTier == RunItemTier.Class
            ? !string.IsNullOrWhiteSpace(CharacterKey)
            : string.IsNullOrEmpty(CharacterKey)) &&
        // No native effects have been authored or approved in 1029 §16.1.
        State == R7NativeRunItemState20261103.CanonicalEffectPending;

    public bool Matches(RunItemDefinition source) =>
        IsValid && source != null &&
        source.ItemId == StableId &&
        source.name == SourceAssetName &&
        source.DisplayName == SourceDisplayName &&
        source.Tier == SourceTier &&
        string.Equals(source.CharacterKey ?? string.Empty,
            CharacterKey ?? string.Empty,StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// R7-11 fail-closed: there is no legitimate source-authored native
    /// combat mechanic yet, and TEMP_BALANCE must not enter the build.
    /// </summary>
    public CharacterItem ResolveApprovedCombatItem() => null;
}

public static class R7NativeRunItemCatalog20261103
{
    public const string ResourcePath = "ProjectAbyss/NativeRunItems";
    private static Dictionary<string,R7NativeRunItemGraph20261103> byId;

    public static void ResetCache() => byId = null;

    private static Dictionary<string,R7NativeRunItemGraph20261103> Load()
    {
        var values=new Dictionary<string,R7NativeRunItemGraph20261103>(
            StringComparer.Ordinal);
        foreach(var graph in Resources.LoadAll<R7NativeRunItemGraph20261103>(
                    ResourcePath))
        {
            if(graph == null || !graph.IsValid ||
               !values.TryAdd(graph.StableId,graph))
                throw new InvalidOperationException(
                    "[R7-11] invalid/duplicate native run-item graph: "+
                    (graph==null?"NULL":graph.StableId));
        }
        if(values.Count != 60)
            throw new InvalidOperationException(
                "[R7-11] expected 60 native run-item source graphs, found "+
                values.Count);
        return values;
    }

    public static IReadOnlyDictionary<string,R7NativeRunItemGraph20261103>
        Registry => byId ??= Load();

    public static bool TryResolve(RunItemDefinition source,
        out R7NativeRunItemGraph20261103 graph)
    {
        graph=null;
        return source!=null &&
            !string.IsNullOrWhiteSpace(source.ItemId) &&
            Registry.TryGetValue(source.ItemId,out graph) &&
            graph.Matches(source);
    }

    public static CharacterItem ResolveExecutableCombatItem(
        RunItemDefinition source)
    {
        // Unpublished/transient test-harness cards are not an approved
        // R7-11 source, and must never inject their TEMP proxy either.
        if(source==null||string.IsNullOrWhiteSpace(source.ItemId))
            return null;
        if(!Registry.TryGetValue(source.ItemId,out var graph))
            return null;
        if(!graph.Matches(source))
            throw new InvalidOperationException(
                "[R7-11] known source identity mismatch: "+source.ItemId);
        return graph.ResolveApprovedCombatItem();
    }

    public static int CountApprovedCombatItems(
        IEnumerable<RunItemDefinition> sources)
    {
        if(sources==null)return 0;
        int count=0;
        foreach(var source in sources)
            if(ResolveExecutableCombatItem(source)!=null)
                count++;
        return count;
    }
}
