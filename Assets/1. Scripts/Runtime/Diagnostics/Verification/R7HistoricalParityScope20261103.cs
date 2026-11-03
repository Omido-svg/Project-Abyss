#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-12 only: historical parity fixture for an EXACT original asset.
/// Opt-in must wrap one test invocation. Production/player builds never
/// compile this type or its dispatcher escape hatch.
/// </summary>
public sealed class R7HistoricalParityScope20261103 : IDisposable
{
    private static R7HistoricalParityScope20261103 active;
    private readonly SkillDefinition source;
    private readonly string sourceGuid;
    private bool disposed;

    private R7HistoricalParityScope20261103(SkillDefinition source)
    {
        if(!Application.isPlaying || source==null)
            throw new InvalidOperationException(
                "R7 historical comparison requires explicit PlayMode source");
        if(active!=null)
            throw new InvalidOperationException(
                "Nested historical parity scope is not permitted");
        string path=AssetDatabase.GetAssetPath(source);
        string guid=AssetDatabase.AssetPathToGUID(path);
        if(guid?.Length!=32 ||
           !path.StartsWith("Assets/",StringComparison.Ordinal) ||
           string.IsNullOrWhiteSpace(source.SkillId) ||
           !source.HasEffectEntries)
            throw new InvalidOperationException(
                "Historical source must have valid GUID and original entries");
        this.source=source;
        sourceGuid=guid;
        active=this;
    }

    // Initial R7 resource/status/gauge graphs are historical when a later
    // canonical Olaf/Yujin owner superseded their effect semantics. A
    // superseded card must NOT be compared against stale pilot numbers.
    public static bool IsSupersededSource(SkillDefinition d)
    {
        return d!=null&&(
            ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                .IsApprovedSource(d)||
            ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                .IsNativePrestigeSource(d)||
            ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                .IsNativeExplosionSource(d)||
            ProjectAbyss.Abilities.R7ProductionYujinNativeMigration20261103
                .IsKnownSource(d));
    }

    // Validates that the final authoritative R7 route/event owner is
    // present. Does not silently treat a missing graph as migrated.
    public static bool HasFinalOwner(SkillDefinition d,BattleContext battle)
    {
        if(d==null||battle==null)return false;
        if(ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
            .IsApprovedSource(d))
            return battle.Services?.R7OlafNativeMigration?.CanRoute(d)==true;
        if(ProjectAbyss.Abilities.R7ProductionYujinNativeMigration20261103
            .IsKnownSource(d))
            return battle.Services?.R7YujinNativeMigration?.CanRoute(d)==true;
        if(ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
            .IsNativePrestigeSource(d)||
           ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
            .IsNativeExplosionSource(d))
        {
            // Event-owned actions must have an actual Olaf source and
            // the authoritative observer implementation available.
            return typeof(OlafMadnessMechanic)!=null&&
                typeof(OlafRulebreakerMechanic)!=null;
        }
        return false;
    }

    public static bool Matches(SkillDefinition definition)
    {
        var scope=active;
        return scope!=null && !scope.disposed &&
            ReferenceEquals(scope.source,definition) &&
            scope.sourceGuid==AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(definition));
    }

    public static TResult ExecuteLegacy<TResult>(
        SkillDefinition source,Func<TResult> operation)
    {
        if(operation==null)throw new ArgumentNullException(nameof(operation));
        using(var scope=new R7HistoricalParityScope20261103(source))
            return operation();
    }

    public static void ExecuteLegacy(
        SkillDefinition source,Action operation)
    {
        if(operation==null)throw new ArgumentNullException(nameof(operation));
        using(var scope=new R7HistoricalParityScope20261103(source))
            operation();
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        if(ReferenceEquals(active,this)) active=null;
    }
}
#endif