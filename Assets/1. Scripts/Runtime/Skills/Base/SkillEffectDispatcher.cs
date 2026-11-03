using System;
using System.Collections.Generic;

/// <summary>
/// SkillDefinition의 효과 context 생성, 재진입/중복 dispatch 방지,
/// EffectEntry 실행을 담당한다. Skill은 façade와 타이밍 진입점만 유지한다.
/// </summary>
internal sealed class SkillEffectDispatcher
{
    private readonly HashSet<DispatchKey> activeDispatches = new();

    public IReadOnlyList<SkillEffectResult> Execute(
        SkillDefinition definition,
        BattleAction action,
        SkillEffectTiming timing,
        int useCountThisTurn,
        BattleAction opponentAction = null,
        DamageContext damageContext = null,
        KillEventContext killContext = null,
        ClashExchangeResult exchangeResult = null,
        int rollIndex = -1,
        RollResult rollResult = null,
        bool isClash = false,
        bool isOneSided = false,
        bool rollSucceeded = false,
        ClashResultContext clashResult = null)
    {
        if (definition == null ||
            action == null ||
            !HasExecutableEffects(definition))
        {
            return Array.Empty<SkillEffectResult>();
        }

        DispatchKey key =
            new DispatchKey(
                action.ActionId,
                timing);

        if (!activeDispatches.Add(key))
            return Array.Empty<SkillEffectResult>();

        try
        {
            SkillEffectContext context =
                new SkillEffectContext(
                    action,
                    definition,
                    timing,
                    opponentAction,
                    damageContext,
                    killContext,
                    exchangeResult,
                    useCountThisTurn,
                    rollIndex,
                    rollResult,
                    isClash,
                    isOneSided,
                    rollSucceeded,
                    clashResult);

#if UNITY_EDITOR
            // R7-12: historical parity only, strictly one GUID-bound test
            // invocation at a time. NEVER available in player builds and
            // NEVER an implicit fallback for a missing production graph.
            if(R7HistoricalParityScope20261103.Matches(definition))
                return ExecuteEntries(definition,context,timing);
#endif
            // R7-5 canonical Olaf must take priority over stale resource/
            // unique-gauge pilot graphs with matching IDs (D04/D07).
            // The event-owned 3 prestige cards run from OlafMadnessMechanic
            // exactly once; never also execute their TEMP proxy/native gauge.
            if(ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                .IsNativePrestigeSource(definition))
                return Array.Empty<SkillEffectResult>();
            var olaf=context.BattleContext?.Services?.R7OlafNativeMigration;
            if(olaf!=null && olaf.CanRoute(definition))
                return olaf.Execute(definition,context,timing);
            var yujin=context.BattleContext?.Services?.R7YujinNativeMigration;
            if(yujin!=null && yujin.CanRoute(definition))
                return yujin.Execute(definition,context,timing);
            // R7 production cohort runs via a native typed R5 resource
            // command, not TempBalanceSkillProxyEffectDefinition.Apply.
            var native = context.BattleContext?.Services?.R7NativeResourceMigration;
            if (native!=null && native.CanRoute(definition))
                return native.Execute(definition,context,timing);
            // R7 single-effect Bleeding cohort, production active after
            // target/part/status/replay parity validation.
            var bleeding = context.BattleContext?.Services?.R7NativeBleedingMigration;
            if(bleeding!=null && bleeding.CanRoute(definition))
                return bleeding.Execute(definition,context,timing);
            var nativeGauge=context.BattleContext?.Services?.R7NativeUniqueGaugeMigration;
            if(nativeGauge!=null && nativeGauge.CanRoute(definition))
                return nativeGauge.Execute(definition,context,timing);
            var legacyStatus=context.BattleContext?.Services?.R7LegacyStatusMigration;
            if(legacyStatus!=null && legacyStatus.CanRoute(definition))
                return legacyStatus.Execute(definition,context,timing);
            var prep=context.BattleContext?.Services?.R7PreparationStatusMigration;
            if(prep!=null && prep.CanRoute(definition))
                return prep.Execute(definition,context,timing);
            var generic=context.BattleContext?.Services?.R7SingleEffectMigration;
            if(generic!=null && generic.CanRoute(definition))
                return generic.Execute(definition,context,timing);
            // R7-5 standard/single-cut explosion is executed by the Olaf
            // native event mechanic, not the stale TempBalance TargetBleeding.
            if(ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                .IsNativeExplosionSource(definition) ||
                ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                    .IsNativePrestigeSource(definition))
                return Array.Empty<SkillEffectResult>();
            // R7-8: seven known enemy skill sources must NEVER fall back to
            // the legacy entry dispatcher when a native graph/service is
            // missing. This also rejects a mismatched name/entry count under
            // a known stable ID, preventing silent save-identity collisions.
            if(R7Enemy1029NativeContract20261103.HasKnownSkillId(
                definition.SkillId))
                throw new InvalidOperationException(
                    "R7-8 enemy native route missing or mismatched; legacy fallback forbidden.");
            // The approved Preparation cannot silently execute old Entry.TryApply
            // if a graph/service is missing or a native contract fails validation.
            if(ProjectAbyss.Abilities.R7ProductionPreparationStatusMigration
                    .IsAuthoredSource(definition))
                throw new InvalidOperationException(
                    "R7-1 Preparation native route unavailable; refusing legacy fallback.");
            // An authored native graph must never become a legacy migration
            // simply because its runtime route or data is unavailable.
            if(ProjectAbyss.Abilities.R7ProductionYujinNativeMigration20261103
                    .IsKnownSource(definition) ||
               IsRegisteredNativeSource(definition) ||
               ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                   .IsApprovedSource(definition))
                throw new InvalidOperationException(
                    "R7-1 production native graph unavailable; refusing legacy fallback.");
            // Editor-only legacy/pilot adapter is retained for historical tests.
            // Unmigrated sources still use ExecuteEntries until authored.
#if UNITY_EDITOR
            // R7-12: opt-in historical parity-only adapter.
            // Never compile this legacy entry hook into player builds.
            var migration = ProjectAbyss.Abilities.R7ExclusiveEntryMigrationScope.Active;
            if (migration != null && migration.CanRoute(definition))
                return migration.Execute(definition, context, timing);
#endif
            return ExecuteEntries(
                definition,
                context,
                timing);
        }
        finally
        {
            activeDispatches.Remove(key);
        }
    }

    public IReadOnlyList<SkillEffectResult> ExecuteOwner(
        Character owner,
        SkillDefinition definition,
        SkillEffectTiming timing,
        int useCountThisTurn)
    {
        if (definition == null ||
            owner == null ||
            !HasExecutableEffects(definition))
        {
            return Array.Empty<SkillEffectResult>();
        }

        DispatchKey key =
            new DispatchKey(0, timing);

        if (!activeDispatches.Add(key))
            return Array.Empty<SkillEffectResult>();

        try
        {
            SkillEffectContext context =
                new SkillEffectContext(
                    owner,
                    definition,
                    timing,
                    useCountThisTurn);

            // Approved enemy source graphs have no owner-lifecycle entries.
            // Never run their retained legacy EffectEntry.TryApply on owners.
            if(R7Enemy1029NativeContract20261103.HasKnownSkillId(
                definition.SkillId))
                return Array.Empty<SkillEffectResult>();
            // Owner-lifecycle events have no Action/Slot. The approved R7-1
            // Preparation only has on-activation operations: never dispatch
            // its retained legacy entries through owner-level TryApply.
            if(ProjectAbyss.Abilities.R7ProductionPreparationStatusMigration
                    .IsAuthoredSource(definition) || IsRegisteredNativeSource(definition) ||
                ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                    .IsApprovedSource(definition) ||
                ProjectAbyss.Abilities.R7ProductionOlafNativeMigration20261103
                    .IsNativeExplosionSource(definition) ||
                ProjectAbyss.Abilities.R7ProductionYujinNativeMigration20261103
                    .IsKnownSource(definition))
                return Array.Empty<SkillEffectResult>();
#if UNITY_EDITOR
            // R7-12: opt-in historical parity-only adapter.
            // Never compile this legacy entry hook into player builds.
            var migration = ProjectAbyss.Abilities.R7ExclusiveEntryMigrationScope.Active;
            if (migration != null && migration.CanRoute(definition))
                return migration.Execute(definition, context, timing);
#endif
            return ExecuteEntries(
                definition,
                context,
                timing);
        }
        finally
        {
            activeDispatches.Remove(key);
        }
    }

    public void Clear()
    {
        activeDispatches.Clear();
    }

    // Registry uses only authored metadata; it does not modify original SOs.
    // Lazily cached to keep normal production dispatch and legacy fallback fast.
    // Resources scans happen on the Unity main thread on first use.
    private static readonly Lazy<HashSet<string>> NativeSourceKeys =
        new Lazy<HashSet<string>>(() =>
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            void Add(string id,string name)
            {
                if(!string.IsNullOrWhiteSpace(id)&&
                   !string.IsNullOrWhiteSpace(name))
                    keys.Add(id+"|"+name);
            }
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeAbilityDefinition>(
                    "ProjectAbyss/NativeAbilities"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeStatusAbilityDefinition>(
                    "ProjectAbyss/NativeStatuses"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeMixedAbilityDefinition>(
                    "ProjectAbyss/NativeMixed"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeUniqueGaugeDefinition>(
                    "ProjectAbyss/NativeUniqueGauges"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeSingleEffectDefinition20261102>(
                    "ProjectAbyss/NativeSingles"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeOlafAbilityDefinition20261103>(
                    "ProjectAbyss/NativeOlaf"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeOlafAbilityDefinition20261103>(
                    "ProjectAbyss/NativeOlafCanon"))
                Add(x.StableId,x.SourceAssetName);
            foreach(var x in UnityEngine.Resources.LoadAll<
                ProjectAbyss.Abilities.R7NativeYujinAbilityDefinition20261103>(
                    "ProjectAbyss/NativeYujin"))
                Add(x.StableId,x.SourceAssetName);
            return keys;
        });
    private static bool IsRegisteredNativeSource(SkillDefinition definition) =>
        definition!=null && NativeSourceKeys.Value.Contains(
            definition.SkillId+"|"+definition.name);

    private static bool HasExecutableEffects(
        SkillDefinition definition)
    {
        return definition != null &&
               (definition.HasEffectEntries ||
                (definition.Effects != null &&
                 definition.Effects.Count > 0));
    }

    private static IReadOnlyList<SkillEffectResult>
        ExecuteEntries(
            SkillDefinition definition,
            SkillEffectContext context,
            SkillEffectTiming timing)
    {
        List<SkillEffectResult> results = new();

        foreach (SkillEffectEntry entry
                 in definition.EnumerateEffectEntries())
        {
            if (entry?.Definition == null)
                continue;

            SkillEffectResult result =
                entry.TryApply(
                    context,
                    timing);

            if (result != null)
                results.Add(result);
        }

        return results;
    }

    private readonly struct DispatchKey :
        IEquatable<DispatchKey>
    {
        private readonly long actionId;
        private readonly SkillEffectTiming timing;

        public DispatchKey(
            long actionId,
            SkillEffectTiming timing)
        {
            this.actionId = actionId;
            this.timing = timing;
        }

        public bool Equals(DispatchKey other) =>
            actionId == other.actionId &&
            timing == other.timing;

        public override bool Equals(object obj) =>
            obj is DispatchKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (actionId.GetHashCode() * 397) ^
                       (int)timing;
            }
        }
    }
}