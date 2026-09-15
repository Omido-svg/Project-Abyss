#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rules 2026-09 정본 트리거 계약으로 Skill Effect 데이터를 점검/이관한다.
///
/// 자동 이관은 표현이 명확한 구 승패 타이밍과 Legacy Roll OnWin/OnLose만 한다.
/// OnHit/BeforeAttack/ActionEnd 같은 과거 phase는 카드 문구에 따라 의미가 달라질 수 있으므로
/// 절대 추측해서 바꾸지 않고 Manual Review로 남긴다.
/// </summary>
public static class SkillEffectTimingMigration
{
    private const string MenuRoot =
        "Tools/Project Abyss/Skill Effect Timing/";

    [MenuItem(MenuRoot + "Audit Timing Contract")]
    public static void AuditTimingContract()
    {
        TimingAudit summary = new();
        AuditEffectDefinitions(summary, migrate: false);
        AuditSkills(summary, migrate: false);
        Finish(summary, "Skill Effect Timing Audit");
    }

    [MenuItem(MenuRoot + "Migrate Canonical Legacy Timings")]
    public static void MigrateCanonicalLegacyTimings()
    {
        if (!EditorUtility.DisplayDialog(
                "Skill Effect Timing Migration",
                "Rules 2026-09 정본 Trigger로 기계적으로 옮길 수 있는 값만 이관합니다.\n\n" +
                "- OnRollSuccess / OnRollWin → 승리시\n" +
                "- OnRollFailure / OnRollLose → 패배시\n" +
                "- OnClashWin/Lose → 합 종료시 + Clash 결과 Condition\n" +
                "- AfterDamage/Critical/Kill/Part 전이 → 정본 Trigger + Runtime Event Condition\n" +
                "- Roll의 구 OnWin/OnLose 리스트 → Roll EffectEntries\n\n" +
                "합 시작/적중/행동 종료 같은 과거 phase와, " +
                "정본 카드 문구를 보고 판단해야 하는 값은 자동 변경하지 않습니다.\n\n" +
                "Git working tree가 깨끗한 상태에서 실행하세요.",
                "Migrate Canonical Values",
                "Cancel"))
        {
            return;
        }

        TimingAudit summary = new();
        AuditEffectDefinitions(summary, migrate: true);
        AuditSkills(summary, migrate: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Finish(summary, "Skill Effect Timing Migration");
    }

    private static void Finish(
        TimingAudit summary,
        string title)
    {
        Debug.Log(summary.BuildReport());
        EditorUtility.DisplayDialog(
            title,
            summary.BuildDialog(),
            "OK");
    }

    private static void AuditEffectDefinitions(
        TimingAudit summary,
        bool migrate)
    {
        foreach (SkillEffectDefinition effect in
                 LoadAllAssets<SkillEffectDefinition>())
        {
            if (effect == null)
                continue;

            SkillEffectTiming timing = effect.Timing;
            if (SkillEffectTimingCatalog.IsAuthoringTiming(timing))
            {
                summary.CanonicalCount++;
                continue;
            }

            if (SkillEffectTimingCatalog.TryGetCanonicalMigration(
                    timing,
                    out SkillEffectTiming replacement) &&
                replacement != timing)
            {
                summary.MigratableCount++;
                summary.Messages.Add(
                    $"MIGRATABLE Effect {AssetDatabase.GetAssetPath(effect)}: " +
                    $"{timing} -> {replacement}");

                if (!migrate)
                    continue;

                SerializedObject serialized = new(effect);
                SerializedProperty timingProperty =
                    serialized.FindProperty("timing");

                if (timingProperty == null)
                {
                    summary.ManualReviewCount++;
                    summary.Messages.Add(
                        $"MANUAL Effect {AssetDatabase.GetAssetPath(effect)}: timing property missing");
                    continue;
                }

                Undo.RecordObject(effect, "Migrate skill effect trigger");
                timingProperty.intValue = (int)replacement;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(effect);

                summary.MigratedCount++;
                continue;
            }

            summary.ManualReviewCount++;
            summary.Messages.Add(
                $"MANUAL Effect {AssetDatabase.GetAssetPath(effect)}: {timing}");
        }
    }

    private static void AuditSkills(
        TimingAudit summary,
        bool migrate)
    {
        foreach (SkillDefinition skill in
                 LoadAllAssets<SkillDefinition>())
        {
            if (skill == null)
                continue;

            bool changed = false;
            string path = AssetDatabase.GetAssetPath(skill);

            int legacySkillEffectCount = CountValidDefinitions(skill.Effects);
            if (legacySkillEffectCount > 0 &&
                (skill.EffectEntries == null || skill.EffectEntries.Count == 0))
            {
                summary.LegacySkillListCount += legacySkillEffectCount;
                summary.Messages.Add(
                    $"LEGACY SKILL EFFECT LIST {path}: {legacySkillEffectCount} definitions");

                if (migrate)
                {
                    Undo.RecordObject(skill, "Migrate legacy skill effect list");
                    skill.EffectEntries ??= new List<SkillEffectEntry>();
                    foreach (SkillEffectDefinition effect in skill.Effects)
                    {
                        if (effect != null)
                            skill.EffectEntries.Add(SkillEffectEntry.FromLegacy(effect));
                    }
                    skill.Effects.Clear();
                    changed = true;
                    summary.MigratedCount += legacySkillEffectCount;
                }
            }

            changed |= ProcessEntries(
                skill.EffectEntries,
                $"{path} / Skill EffectEntries",
                SkillEffectTimingCatalog.AuthoringTimings,
                summary,
                migrate);

            if (skill.Rolls != null)
            {
                for (int i = 0; i < skill.Rolls.Count; i++)
                {
                    SkillRollData roll = skill.Rolls[i];
                    if (roll == null)
                        continue;

                    changed |= ProcessEntries(
                        roll.EffectEntries,
                        $"{path} / Roll[{i + 1}] EffectEntries",
                        SkillEffectTimingCatalog.RollAuthoringTimings,
                        summary,
                        migrate);

                    int legacyCount =
                        CountValidEntries(roll.OnWinEffectEntries) +
                        CountValidEntries(roll.OnLoseEffectEntries) +
                        CountValidDefinitions(roll.OnWinEffects) +
                        CountValidDefinitions(roll.OnLoseEffects);

                    if (legacyCount <= 0)
                        continue;

                    summary.LegacyRollListCount += legacyCount;
                    summary.Messages.Add(
                        $"LEGACY ROLL LIST {path} / Roll[{i + 1}]: {legacyCount} entries");

                    if (!migrate)
                        continue;

                    Undo.RecordObject(skill, "Migrate legacy roll effect lists");
                    MigrateLegacyRollLists(roll);
                    changed = true;
                    summary.MigratedCount += legacyCount;
                }
            }

            if (!changed || !migrate)
                continue;

            EditorUtility.SetDirty(skill);
        }
    }

    private static bool ProcessEntries(
        List<SkillEffectEntry> entries,
        string location,
        IReadOnlyList<SkillEffectTiming> allowedTimings,
        TimingAudit summary,
        bool migrate)
    {
        if (entries == null)
            return false;

        bool changed = false;

        for (int i = 0; i < entries.Count; i++)
        {
            SkillEffectEntry entry = entries[i];
            if (entry?.Definition == null)
                continue;

            SkillEffectTiming timing = entry.EffectiveTiming;
            if (ContainsTiming(allowedTimings, timing))
            {
                summary.CanonicalCount++;
                continue;
            }

            if (TryGetEntryMigration(
                    timing,
                    out SkillEffectTiming replacement,
                    out SkillEffectConditionType? conditionType) &&
                ContainsTiming(allowedTimings, replacement))
            {
                summary.MigratableCount++;
                summary.Messages.Add(
                    $"MIGRATABLE {location}[{i}]: {timing} -> {replacement}" +
                    (conditionType.HasValue
                        ? $" + Condition({conditionType.Value})"
                        : string.Empty));

                if (!migrate)
                    continue;

                entry.OverrideTiming = true;
                entry.Timing = replacement;
                entry.Conditions ??= new List<SkillEffectCondition>();

                if (conditionType.HasValue &&
                    !HasCondition(entry.Conditions, conditionType.Value))
                {
                    entry.Conditions.Add(
                        new SkillEffectCondition
                        {
                            Type = conditionType.Value,
                            Subject = SkillEffectConditionSubject.Target
                        });
                }

                changed = true;
                summary.MigratedCount++;
                continue;
            }

            summary.ManualReviewCount++;
            summary.Messages.Add(
                $"MANUAL {location}[{i}]: effective {timing}" +
                (!entry.OverrideTiming
                    ? " from Effect Definition"
                    : string.Empty));
        }

        return changed;
    }

    private static bool TryGetEntryMigration(
        SkillEffectTiming legacy,
        out SkillEffectTiming replacement,
        out SkillEffectConditionType? conditionType)
    {
        conditionType = null;

        switch (legacy)
        {
            case SkillEffectTiming.OnRollSuccess:
            case SkillEffectTiming.OnRollWin:
                replacement = SkillEffectTiming.OnExchangeWin;
                return true;

            case SkillEffectTiming.OnRollFailure:
            case SkillEffectTiming.OnRollLose:
                replacement = SkillEffectTiming.OnExchangeLose;
                return true;

            case SkillEffectTiming.OnClashWin:
                replacement = SkillEffectTiming.OnClashEnd;
                conditionType = SkillEffectConditionType.ClashWon;
                return true;

            case SkillEffectTiming.OnClashLose:
                replacement = SkillEffectTiming.OnClashEnd;
                conditionType = SkillEffectConditionType.ClashLost;
                return true;

            case SkillEffectTiming.AfterDamage:
                replacement = SkillEffectTiming.OnExchangeWin;
                conditionType = SkillEffectConditionType.RuntimeAfterDamage;
                return true;

            case SkillEffectTiming.OnCritical:
                replacement = SkillEffectTiming.OnExchangeWin;
                conditionType = SkillEffectConditionType.WasCritical;
                return true;

            case SkillEffectTiming.OnKill:
                replacement = SkillEffectTiming.OnExchangeWin;
                conditionType = SkillEffectConditionType.KilledTarget;
                return true;

            case SkillEffectTiming.OnPartWeakened:
                replacement = SkillEffectTiming.OnExchangeWin;
                conditionType = SkillEffectConditionType.TargetWasWeakenedByThisDamage;
                return true;

            case SkillEffectTiming.OnPartBroken:
                replacement = SkillEffectTiming.OnExchangeWin;
                conditionType = SkillEffectConditionType.TargetWasBrokenByThisDamage;
                return true;

            default:
                replacement = legacy;
                return SkillEffectTimingCatalog.IsAuthoringTiming(legacy);
        }
    }

    private static bool HasCondition(
        IReadOnlyList<SkillEffectCondition> conditions,
        SkillEffectConditionType type)
    {
        if (conditions == null)
            return false;

        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i]?.Type == type)
                return true;
        }

        return false;
    }

    private static int CountValidEntries(
        IReadOnlyList<SkillEffectEntry> values)
    {
        if (values == null)
            return 0;

        int count = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i]?.Definition != null)
                count++;
        }

        return count;
    }

    private static int CountValidDefinitions(
        IReadOnlyList<SkillEffectDefinition> values)
    {
        if (values == null)
            return 0;

        int count = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] != null)
                count++;
        }

        return count;
    }

    private static bool ContainsTiming(
        IReadOnlyList<SkillEffectTiming> timings,
        SkillEffectTiming timing)
    {
        if (timings == null)
            return false;

        for (int i = 0; i < timings.Count; i++)
        {
            if (timings[i] == timing)
                return true;
        }

        return false;
    }

    private static void MigrateLegacyRollLists(
        SkillRollData roll)
    {
        roll.EffectEntries ??= new List<SkillEffectEntry>();

        AppendLegacyEntries(
            roll.EffectEntries,
            roll.OnWinEffectEntries,
            SkillEffectTiming.OnExchangeWin);
        AppendLegacyEntries(
            roll.EffectEntries,
            roll.OnLoseEffectEntries,
            SkillEffectTiming.OnExchangeLose);
        AppendLegacyDefinitions(
            roll.EffectEntries,
            roll.OnWinEffects,
            SkillEffectTiming.OnExchangeWin);
        AppendLegacyDefinitions(
            roll.EffectEntries,
            roll.OnLoseEffects,
            SkillEffectTiming.OnExchangeLose);

        roll.OnWinEffectEntries?.Clear();
        roll.OnLoseEffectEntries?.Clear();
        roll.OnWinEffects?.Clear();
        roll.OnLoseEffects?.Clear();
    }

    private static void AppendLegacyEntries(
        List<SkillEffectEntry> destination,
        List<SkillEffectEntry> source,
        SkillEffectTiming timing)
    {
        if (source == null)
            return;

        foreach (SkillEffectEntry entry in source)
        {
            if (entry?.Definition == null)
                continue;

            destination.Add(CloneEntry(entry, timing));
        }
    }

    private static void AppendLegacyDefinitions(
        List<SkillEffectEntry> destination,
        List<SkillEffectDefinition> source,
        SkillEffectTiming timing)
    {
        if (source == null)
            return;

        foreach (SkillEffectDefinition effect in source)
        {
            if (effect == null)
                continue;

            destination.Add(new SkillEffectEntry
            {
                Definition = effect,
                Overrides = new SkillEffectOverrides(),
                OverrideTiming = true,
                Timing = timing,
                Conditions = new List<SkillEffectCondition>(),
                RestrictToRoll = false,
                RollNumber = 1
            });
        }
    }

    private static SkillEffectEntry CloneEntry(
        SkillEffectEntry source,
        SkillEffectTiming timing)
    {
        return new SkillEffectEntry
        {
            Definition = source.Definition,
            Overrides = CloneOverrides(source.Overrides),
            OverrideTiming = true,
            Timing = timing,
            Conditions = CloneConditions(source.Conditions),
            RestrictToRoll = source.RestrictToRoll,
            RollNumber = source.RollNumber
        };
    }

    private static List<SkillEffectCondition> CloneConditions(
        IReadOnlyList<SkillEffectCondition> source)
    {
        List<SkillEffectCondition> result = new();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            SkillEffectCondition condition = source[i];
            if (condition == null)
                continue;

            result.Add(new SkillEffectCondition
            {
                Type = condition.Type,
                Invert = condition.Invert,
                Subject = condition.Subject,
                StatusEffectId = condition.StatusEffectId,
                CheckCharacterStatus = condition.CheckCharacterStatus,
                CheckPartStatus = condition.CheckPartStatus,
                AnyOwnerPart = condition.AnyOwnerPart,
                OwnerPartType = condition.OwnerPartType,
                Threshold = condition.Threshold,
                Minimum = condition.Minimum,
                Maximum = condition.Maximum,
                Ratio = condition.Ratio,
                MinimumRatio = condition.MinimumRatio,
                MaximumRatio = condition.MaximumRatio,
                ResourceKey = condition.ResourceKey,
                MomentumState = condition.MomentumState
            });
        }

        return result;
    }

    private static SkillEffectOverrides CloneOverrides(
        SkillEffectOverrides source)
    {
        if (source == null)
            return new SkillEffectOverrides();

        return new SkillEffectOverrides
        {
            OverrideStack = source.OverrideStack,
            Stack = source.Stack,
            OverrideDuration = source.OverrideDuration,
            Duration = source.Duration,
            OverrideAmount = source.OverrideAmount,
            Amount = source.Amount,
            OverrideMinimum = source.OverrideMinimum,
            Minimum = source.Minimum,
            OverrideMaximum = source.OverrideMaximum,
            Maximum = source.Maximum,
            OverrideFlatValue = source.OverrideFlatValue,
            FlatValue = source.FlatValue,
            OverrideMultiplier = source.OverrideMultiplier,
            Multiplier = source.Multiplier,
            OverrideResourceKey = source.OverrideResourceKey,
            ResourceKey = source.ResourceKey,
            OverrideForceCharacterStatus = source.OverrideForceCharacterStatus,
            ForceCharacterStatus = source.ForceCharacterStatus,
            OverrideGiveToSelectedTarget = source.OverrideGiveToSelectedTarget,
            GiveToSelectedTarget = source.GiveToSelectedTarget
        };
    }

    private static IEnumerable<T> LoadAllAssets<T>()
        where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(
            $"t:{typeof(T).Name}",
            new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                yield return asset;
        }
    }

    private sealed class TimingAudit
    {
        public int CanonicalCount;
        public int MigratableCount;
        public int ManualReviewCount;
        public int LegacyRollListCount;
        public int LegacySkillListCount;
        public int MigratedCount;
        public readonly List<string> Messages = new();

        public string BuildDialog() =>
            $"정본 Trigger 항목: {CanonicalCount}\n" +
            $"자동 이관 가능 구형 Trigger: {MigratableCount}\n" +
            $"구형 Skill Effects 항목: {LegacySkillListCount}\n" +
            $"구형 Roll OnWin/OnLose 항목: {LegacyRollListCount}\n" +
            $"수동 검토 필요: {ManualReviewCount}\n" +
            $"이번 실행에서 이관: {MigratedCount}\n\n" +
            "수동 검토가 0이어야 Timing Contract 이관이 끝난 것입니다.\n" +
            "단, 카드 자체가 정본의 어느 Trigger를 써야 하는지는 콘텐츠 스펙과 별도 의미 검증이 필요합니다.";

        public string BuildReport()
        {
            string header =
                "[Skill Effect Timing Contract — Rules 2026-09]\n" +
                BuildDialog();

            if (Messages.Count == 0)
                return header + "\n\nCanonical timing contract clean.";

            return header +
                   "\n\n- " +
                   string.Join("\n- ", Messages);
        }
    }
}
#endif
