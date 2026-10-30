#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// G0 — 기준선 확보 · 정본 규칙 · 에셋 전수 감사.
///
/// 이 단계는 런타임 규칙을 변경하지 않는다. 다음만 수행한다.
/// 1) SkillDefinition / SkillRollData / SkillEffectEntry / SkillEffectDefinition /
///    EmotionAugmentDefinition 에셋의 GUID·경로·직렬화 값 전수 감사 (JSON/CSV)
/// 2) 트리거 사용 현황(OnDuelMatched/OnRollWin/OnRollLose/OnExecute/OnBattleStart) 통계
/// 3) Missing Script / 중복 GUID / RuntimeAfterDamage 재점화 경로 / BattleEvent 직접 구독 목록
/// 4) 변경 전 이벤트 횟수를 고정하는 Golden Baseline (Play Mode, 탐침 효과 사용)
///
/// 출력 폴더: &lt;UnityProject&gt;/G0_Ability_Audit_20261029
/// </summary>
public static class G0AbilityAuditReport20261029
{
    private const string LogPrefix = "[G0]";
    private const string OutputFolderName = "G0_Ability_Audit_20261029";

    private static readonly SkillEffectTiming[] RuntimeWakeTimings =
    {
        SkillEffectTiming.AfterDamage,
        SkillEffectTiming.OnCritical,
        SkillEffectTiming.OnKill,
        SkillEffectTiming.OnPartWeakened,
        SkillEffectTiming.OnPartBroken
    };

    private static int pass;
    private static int fail;

    //====================================================================
    // 1. 에셋 전수 감사
    //====================================================================

    [MenuItem("Game System Verification/G0 - Ability Asset Audit (Edit Mode)")]
    public static void RunAuditFromMenu()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException(
                "G0 에셋 감사는 Edit Mode에서 실행해야 합니다.");
        }

        string outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        List<EntryRow> rows = CollectEntryRows();
        List<EffectDefinitionInfo> effectDefinitions =
            CollectEffectDefinitions();
        List<string> skillPaths =
            FindAssetPaths("t:SkillDefinition");
        List<string> augmentPaths =
            FindAssetPaths("t:EmotionAugmentDefinition");

        HashSet<string> referencedDefinitionGuids =
            new(
                rows.Where(row => !string.IsNullOrEmpty(row.EffectDefinitionGuid))
                    .Select(row => row.EffectDefinitionGuid),
                StringComparer.OrdinalIgnoreCase);

        List<EffectDefinitionInfo> orphanDefinitions =
            effectDefinitions
                .Where(info =>
                    !string.IsNullOrEmpty(info.Guid) &&
                    !referencedDefinitionGuids.Contains(info.Guid))
                .ToList();

        List<MissingScriptInfo> missingScripts = ScanMissingScripts();
        List<string> fatalMissingScriptAssets = missingScripts
            .Where(info => string.Equals(
                info.Classification,
                "FATAL",
                StringComparison.Ordinal))
            .Select(info => info.Path)
            .ToList();
        List<string> warningMissingScriptAssets = missingScripts
            .Where(info => string.Equals(
                info.Classification,
                "WARNING",
                StringComparison.Ordinal))
            .Select(info => info.Path)
            .ToList();
        List<DuplicateGuidGroup> duplicateGuids = ScanDuplicateGuids();
        Dictionary<int, int> rawTimingHistogram =
            ScanRawTimingHistogram();
        Dictionary<int, int> effectiveTimingHistogram =
            BuildHistogram(rows.Select(row => row.EffectiveTimingValue));
        Dictionary<string, int> scopeHistogram =
            BuildStringHistogram(rows.Select(row => row.SourceKind));
        List<BattleEventSubscription> battleEventSubscriptions =
            ScanBattleEventSubscriptions();
        List<string> canWakeOverrides =
            ScanSourceMatches(@"override\s+bool\s+CanWakeFromRuntimeEvent");
        List<string> runtimeAfterDamageReferences =
            ScanSourceMatches(@"RuntimeAfterDamage");

        List<G0KeywordRow> keywordRows = ScanKeywordAudit();
        List<G0KeywordGlossaryRow> glossaryRows = ScanGlossaryKeywords();
        List<G0SkillKeywordRow> skillKeywordRows =
            ScanSkillKeywordAuthoring();
        List<G0CombatResourceRow> combatResourceRows =
            ScanCombatResources();
        List<G0RequiredKeywordRow> requiredKeywordRows =
            ScanRequiredKeywordCoverage();
        List<G0CallGraphEdge> callGraphEdges = ScanCallGraph();
        List<G0CounterKindRow> counterKinds = ScanCounterKinds();
        List<string> attackWeightUiReferences =
            ScanSourceMatches(@"AttackWeight");
        List<G0AttackWeightRow> attackWeightRows =
            ScanAttackWeights();
        List<G0AttackWeightProtectionRow> weightProtectionRows =
            BuildWeightProtectionRows(attackWeightRows);

        List<EntryRow> manualReview =
            rows.Where(row => row.NeedsManualReview).ToList();
        List<EntryRow> runtimeWakeEntries =
            rows.Where(row => row.RuntimeEventWake).ToList();

        Dictionary<string, int> canonicalUsage =
            new(StringComparer.Ordinal)
            {
                ["OnExecute(0)"] = rows.Count(row => row.EffectiveTimingValue == 0),
                ["OnDuelMatched(31)"] = rows.Count(row => row.EffectiveTimingValue == 31),
                ["OnRollWin(14)"] = rows.Count(row => row.EffectiveTimingValue == 14),
                ["OnRollLose(15)"] = rows.Count(row => row.EffectiveTimingValue == 15),
                ["OnRollSuccess(23)"] = rows.Count(row => row.EffectiveTimingValue == 23),
                ["OnRollFailure(24)"] = rows.Count(row => row.EffectiveTimingValue == 24),
                ["OnBattleStart(16)"] = rows.Count(row => row.EffectiveTimingValue == 16),
                ["OnExchangeWin(7)"] = rows.Count(row => row.EffectiveTimingValue == 7),
                ["OnExchangeLose(8)"] = rows.Count(row => row.EffectiveTimingValue == 8),
                ["OnClashEnd(34)"] = rows.Count(row => row.EffectiveTimingValue == 34),
                ["AfterDamage(3)"] = rows.Count(row => row.EffectiveTimingValue == 3)
            };

        List<SharedTemplateUsage> sharedTemplates =
            rows.Where(row => !string.IsNullOrEmpty(row.EffectDefinitionGuid))
                .GroupBy(row => row.EffectDefinitionGuid, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => new SharedTemplateUsage
                {
                    EffectDefinitionGuid = group.Key,
                    EntryCount = group.Count(),
                    SkillCount = group.Select(row => row.AssetGuid)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()
                })
                .OrderByDescending(item => item.EntryCount)
                .ToList();

        List<DuplicateIdentity> duplicateIdentities =
            rows.GroupBy(
                    row => $"{row.AssetGuid}|{row.EntryIndex}|{row.RollIndexOrNone}",
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => new DuplicateIdentity
                {
                    Identity = group.Key,
                    Count = group.Count()
                })
                .ToList();

        AuditSummary summary = new()
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            UnityVersion = Application.unityVersion,
            ProjectPath = Directory.GetParent(Application.dataPath)?.FullName,
            Branch = TryReadGitBranch(),
            Commit = TryReadGitCommit(),
            SkillAssetCount = skillPaths.Count,
            EntryCount = rows.Count,
            EffectDefinitionAssetCount = effectDefinitions.Count,
            OrphanEffectDefinitionCount = orphanDefinitions.Count,
            EmotionAugmentAssetCount = augmentPaths.Count,
            MissingScriptAssetCount = fatalMissingScriptAssets.Count,
            MissingScriptWarningAssetCount = warningMissingScriptAssets.Count,
            DuplicateGuidGroupCount = duplicateGuids.Count,
            NeedsManualReviewCount = manualReview.Count,
            RuntimeEventWakeEntryCount = runtimeWakeEntries.Count,
            KeywordRowCount = keywordRows.Count,
            KeywordManualReviewCount =
                keywordRows.Count(row => row.NeedsManualReview),
            RequiredKeywordCount = requiredKeywordRows.Count,
            RequiredKeywordMissingCount =
                requiredKeywordRows.Count(row =>
                    row.CoverageStatus == "Missing"),
            RequiredKeywordUndecidedCount =
                requiredKeywordRows.Count(row =>
                    !string.IsNullOrEmpty(row.DecisionNote)),
            GlossaryKeywordCount = glossaryRows.Count,
            SkillKeywordEntryCount = skillKeywordRows.Count,
            CombatResourceCount = combatResourceRows.Count,
            CallGraphEdgeCount = callGraphEdges.Count,
            AttackWeightedSkillCount =
                attackWeightRows.Count(row => row.Weight > 1),
            AttackWeightProtectedSkillCount =
                weightProtectionRows.Count,
            RawTimingHistogram = ToTimingCounts(rawTimingHistogram),
            EffectiveTimingHistogram = ToTimingCounts(effectiveTimingHistogram),
            ScopeHistogram = scopeHistogram
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ScopeCount
                {
                    Scope = pair.Key,
                    Count = pair.Value
                })
                .ToList(),
            CanonicalTriggerUsage = canonicalUsage
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TriggerCount
                {
                    Name = pair.Key,
                    Count = pair.Value
                })
                .ToList(),
            MissingScriptAssets = fatalMissingScriptAssets,
            MissingScriptWarnings = warningMissingScriptAssets,
            DuplicateGuidGroups = duplicateGuids,
            BattleEventSubscriptions = battleEventSubscriptions,
            CanWakeOverrides = canWakeOverrides,
            RuntimeAfterDamageReferences = runtimeAfterDamageReferences,
            OrphanEffectDefinitions = orphanDefinitions,
            SharedTemplates = sharedTemplates,
            DuplicateIdentities = duplicateIdentities,
            ManualReviewEntries = manualReview,
            KeywordRows = keywordRows,
            RequiredKeywordRows = requiredKeywordRows,
            GlossaryKeywords = glossaryRows,
            SkillKeywordEntries = skillKeywordRows,
            CombatResources = combatResourceRows,
            CallGraphEdges = callGraphEdges,
            CounterKinds = counterKinds,
            AttackWeightRows = attackWeightRows,
            AttackWeightProtectionRows = weightProtectionRows,
            AttackWeightUiReferences = attackWeightUiReferences
        };

        WriteCsv(
            Path.Combine(outputDirectory, "G0_ENTRIES.csv"),
            rows);
        WriteCsv(
            Path.Combine(outputDirectory, "G0_TRIGGER_STATS.csv"),
            BuildTriggerStatRows(rawTimingHistogram, effectiveTimingHistogram, canonicalUsage));
        WriteCsv(
            Path.Combine(outputDirectory, "G0_K_KEYWORDS.csv"),
            "KeywordId,ClassName,OwnerCharacterId,CarrierScope,StorageKind," +
            "ApplyPhase,EffectSource,RemovePolicy,OwnedRule,CurrentCodePath," +
            "SerializedRefs,NeedsManualReview",
            keywordRows,
            row => new object[]
            {
                row.KeywordId, row.ClassName, row.OwnerCharacterId,
                row.CarrierScope, row.StorageKind, row.ApplyPhase,
                row.EffectSource, row.RemovePolicy, row.OwnedRule,
                row.CurrentCodePath, row.SerializedRefs,
                row.NeedsManualReview
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_K_REQUIRED.csv"),
            "KeywordId,OwnerCharacterId,CarrierScope,StorageKind," +
            "CoverageStatus,ObservedIdentifiers,DecisionNote",
            requiredKeywordRows,
            row => new object[]
            {
                row.KeywordId, row.OwnerCharacterId, row.CarrierScope,
                row.StorageKind, row.CoverageStatus,
                row.ObservedIdentifiers, row.DecisionNote
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_K_GLOSSARY.csv"),
            "Keyword,DescriptionPreview,Source",
            glossaryRows,
            row => new object[]
            {
                row.Keyword, row.DescriptionPreview, row.Source
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_K_SKILL_KEYWORDS.csv"),
            "AssetPath,AssetGuid,SkillId,KeywordIndex,KeywordName,Description",
            skillKeywordRows,
            row => new object[]
            {
                row.AssetPath, row.AssetGuid, row.SkillId, row.KeywordIndex,
                row.KeywordName, row.Description
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_K_RESOURCES.csv"),
            "AssetPath,AssetGuid,CharacterName,Index,Key,InitialValue,MaxValue",
            combatResourceRows,
            row => new object[]
            {
                row.AssetPath, row.AssetGuid, row.CharacterName, row.Index,
                row.Key, row.InitialValue, row.MaxValue
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_I_CALL_GRAPH.csv"),
            "CallerFile,CallerMethod,Callee,Category,Line,Source",
            callGraphEdges,
            row => new object[]
            {
                row.CallerFile, row.CallerMethod, row.Callee, row.Category,
                row.Line, row.Source
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_I_COUNTER_KINDS.csv"),
            "Kind,OccurrenceCount,MappingNote,ObservedIdentifiers,SampleSites",
            counterKinds,
            row => new object[]
            {
                row.Kind, row.OccurrenceCount, row.MappingNote,
                row.ObservedIdentifiers,
                string.Join(" | ", row.SampleSites)
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_W_ATTACK_WEIGHTS.csv"),
            "AssetPath,AssetGuid,SkillId,SkillName,ActionType,Weight," +
            "SecondaryPartMode,SecondaryDamageMultiplier," +
            "AllowBrokenSecondaryParts,RollCount,EffectEntryCount," +
            "KeywordCount,UsesAnotherPartOnPrimaryCharacter,NeedsProtection",
            attackWeightRows,
            row => new object[]
            {
                row.AssetPath, row.AssetGuid, row.SkillId, row.SkillName,
                row.ActionType, row.Weight, row.SecondaryPartMode,
                row.SecondaryDamageMultiplier,
                row.AllowBrokenSecondaryParts, row.RollCount,
                row.EffectEntryCount, row.KeywordCount,
                row.UsesAnotherPartOnPrimaryCharacter, row.NeedsProtection
            });
        WriteCsv(
            Path.Combine(outputDirectory, "G0_W_PROTECTED.csv"),
            "AssetPath,SkillId,ActionType,Weight,SecondaryPartMode,ProtectionReason",
            weightProtectionRows,
            row => new object[]
            {
                row.AssetPath, row.SkillId, row.ActionType, row.Weight,
                row.SecondaryPartMode, row.ProtectionReason
            });
        File.WriteAllText(
            Path.Combine(outputDirectory, "G0_AUDIT.json"),
            JsonUtility.ToJson(summary, true),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(outputDirectory, "G0_SUMMARY_KO.md"),
            BuildMarkdown(summary, outputDirectory),
            new UTF8Encoding(false));

        Debug.Log(
            $"{LogPrefix} RESULT PASS={1} FAIL={0} | " +
            $"skills={summary.SkillAssetCount} entries={summary.EntryCount} " +
            $"effects={summary.EffectDefinitionAssetCount} " +
            $"orphans={summary.OrphanEffectDefinitionCount} " +
            $"augments={summary.EmotionAugmentAssetCount} " +
            $"missingScripts={summary.MissingScriptAssetCount} " +
            $"missingScriptWarnings={summary.MissingScriptWarningAssetCount} " +
            $"duplicateGuids={summary.DuplicateGuidGroupCount} " +
            $"keywords={summary.KeywordRowCount} " +
            $"requiredMissing={summary.RequiredKeywordMissingCount} " +
            $"glossary={summary.GlossaryKeywordCount} " +
            $"callEdges={summary.CallGraphEdgeCount} " +
            $"weightedSkills={summary.AttackWeightedSkillCount} " +
            $"manualReview={summary.NeedsManualReviewCount}");
        Debug.Log($"{LogPrefix} OUTPUT {outputDirectory}");
    }

    private static List<EntryRow> CollectEntryRows()
    {
        List<EntryRow> rows = new();
        int globalIndex = 0;

        foreach (string path in FindAssetPaths("t:SkillDefinition"))
        {
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null)
                continue;

            string guid = AssetDatabase.AssetPathToGUID(path);

            if (definition.Rolls != null)
            {
                for (int rollIndex = 0; rollIndex < definition.Rolls.Count; rollIndex++)
                {
                    SkillRollData roll = definition.Rolls[rollIndex];

                    if (roll == null)
                        continue;

                    AddEntryRows(
                        rows,
                        definition,
                        path,
                        guid,
                        roll.EffectEntries,
                        rollIndex,
                        "RollEffectEntries",
                        ref globalIndex);

                    AddEntryRows(
                        rows,
                        definition,
                        path,
                        guid,
                        roll.OnWinEffectEntries,
                        rollIndex,
                        "RollOnWinEffectEntries",
                        ref globalIndex);

                    AddEntryRows(
                        rows,
                        definition,
                        path,
                        guid,
                        roll.OnLoseEffectEntries,
                        rollIndex,
                        "RollOnLoseEffectEntries",
                        ref globalIndex);

                    AddLegacyRows(
                        rows,
                        definition,
                        path,
                        guid,
                        roll.OnWinEffects,
                        rollIndex,
                        "RollLegacyOnWinEffects",
                        ref globalIndex);

                    AddLegacyRows(
                        rows,
                        definition,
                        path,
                        guid,
                        roll.OnLoseEffects,
                        rollIndex,
                        "RollLegacyOnLoseEffects",
                        ref globalIndex);
                }
            }

            AddEntryRows(
                rows,
                definition,
                path,
                guid,
                definition.EffectEntries,
                -1,
                "DefinitionEffectEntries",
                ref globalIndex);

            AddLegacyRows(
                rows,
                definition,
                path,
                guid,
                definition.Effects,
                -1,
                "LegacyEffects",
                ref globalIndex);
        }

        return rows;
    }

    private static void AddEntryRows(
        List<EntryRow> rows,
        SkillDefinition skill,
        string path,
        string guid,
        IReadOnlyList<SkillEffectEntry> entries,
        int rollIndex,
        string sourceKind,
        ref int globalIndex)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            SkillEffectEntry entry = entries[i];

            if (entry?.Definition == null)
                continue;

            rows.Add(CreateRow(
                skill,
                path,
                guid,
                entry,
                rollIndex,
                sourceKind,
                isLegacy: false,
                globalIndex++));
        }
    }

    private static void AddLegacyRows(
        List<EntryRow> rows,
        SkillDefinition skill,
        string path,
        string guid,
        IReadOnlyList<SkillEffectDefinition> effects,
        int rollIndex,
        string sourceKind,
        ref int globalIndex)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            SkillEffectDefinition effect = effects[i];

            if (effect == null)
                continue;

            SkillEffectEntry entry = SkillEffectEntry.FromLegacy(effect);

            rows.Add(CreateRow(
                skill,
                path,
                guid,
                entry,
                rollIndex,
                sourceKind,
                isLegacy: true,
                globalIndex++));
        }
    }

    private static EntryRow CreateRow(
        SkillDefinition skill,
        string path,
        string guid,
        SkillEffectEntry entry,
        int rollIndex,
        string sourceKind,
        bool isLegacy,
        int entryIndex)
    {
        SkillEffectDefinition effect = entry.Definition;
        EffectDefinitionInfo info = InspectEffectDefinition(effect);

        List<string> conditionTypes = new(entry.Conditions?.Count ?? 0);
        if (entry.Conditions != null)
        {
            foreach (SkillEffectCondition condition in entry.Conditions)
            {
                if (condition != null)
                    conditionTypes.Add(condition.Type.ToString());
            }
        }

        conditionTypes.AddRange(info.ConditionTypes);

        bool runtimeWake = false;

        for (int i = 0; i < RuntimeWakeTimings.Length; i++)
        {
            if (effect.CanWakeFromRuntimeEvent(RuntimeWakeTimings[i]))
            {
                runtimeWake = true;
                break;
            }
        }

        int effectiveTiming = (int)entry.EffectiveTiming;
        bool isAuthoring = SkillEffectTimingCatalog.IsAuthoringTiming(entry.EffectiveTiming);

        bool needsManualReview =
            isLegacy ||
            !isAuthoring ||
            effectiveTiming == (int)SkillEffectTiming.OnDuelMatched ||
            effectiveTiming == (int)SkillEffectTiming.OnRollWin ||
            effectiveTiming == (int)SkillEffectTiming.OnRollLose ||
            effectiveTiming == (int)SkillEffectTiming.OnRollSuccess ||
            effectiveTiming == (int)SkillEffectTiming.OnRollFailure ||
            effectiveTiming == (int)SkillEffectTiming.OnBattleStart ||
            runtimeWake;

        return new EntryRow
        {
            AssetPath = path,
            AssetGuid = guid,
            SkillId = skill.SkillId,
            SkillName = skill.SkillName,
            ActionType = skill.ActionType.ToString(),
            IsDuel = skill.ActionType == ActionType.Duel,
            EntryIndex = entryIndex,
            RollIndexOrNone = rollIndex,
            RollNumberRestriction =
                entry.RestrictToRoll
                    ? entry.RollNumber.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
            EffectDefinitionGuid = info.Guid,
            EffectDefinitionPath = info.Path,
            LegacyOrNew = isLegacy ? "Legacy" : "New",
            SourceKind = sourceKind,
            SerializedTimingValue = (int)entry.Timing,
            EffectiveTimingValue = effectiveTiming,
            DisplayTiming = SkillEffectTimingCatalog.GetDisplayName(entry.EffectiveTiming),
            ConditionTypes = string.Join("|", conditionTypes),
            TargetSelector = info.TargetSelector,
            OperationType = info.TypeName,
            MayProduceDamage = info.MayProduceDamage,
            HasLifetimeState = info.HasLifetimeState,
            RuntimeEventWake = runtimeWake,
            NeedsManualReview = needsManualReview
        };
    }

    private static EffectDefinitionInfo InspectEffectDefinition(
        SkillEffectDefinition effect)
    {
        string path = AssetDatabase.GetAssetPath(effect);
        string guid = AssetDatabase.AssetPathToGUID(path);
        SerializedObject serialized = new(effect);

        string targetSelector = "Unknown";
        SerializedProperty targetProperty =
            serialized.FindProperty("targetSelector");
        if (targetProperty != null)
        {
            targetSelector = targetProperty.enumValueIndex >= 0 &&
                             targetProperty.enumValueIndex < targetProperty.enumDisplayNames.Length
                ? targetProperty.enumDisplayNames[targetProperty.enumValueIndex]
                : targetProperty.intValue.ToString(CultureInfo.InvariantCulture);
        }

        List<string> conditions = new();
        SerializedProperty conditionProperty =
            serialized.FindProperty("conditions");
        if (conditionProperty != null && conditionProperty.isArray)
        {
            for (int i = 0; i < conditionProperty.arraySize; i++)
            {
                SerializedProperty element =
                    conditionProperty.GetArrayElementAtIndex(i);
                SerializedProperty type =
                    element?.FindPropertyRelative("Type");

                if (type == null)
                    continue;

                conditions.Add(
                    type.enumDisplayNames != null &&
                    type.enumValueIndex >= 0 &&
                    type.enumValueIndex < type.enumDisplayNames.Length
                        ? type.enumDisplayNames[type.enumValueIndex]
                        : type.intValue.ToString(CultureInfo.InvariantCulture));
            }
        }

        bool mayProduceDamage =
            effect.GetType().Name.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasLifetimeState = false;

        SerializedProperty iterator = serialized.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            string propertyPath = iterator.propertyPath ?? string.Empty;

            if (!mayProduceDamage &&
                propertyPath.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mayProduceDamage = true;
            }

            if (!hasLifetimeState &&
                (propertyPath.IndexOf("duration", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 propertyPath.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 propertyPath.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 propertyPath.IndexOf("lifetime", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                hasLifetimeState = true;
            }
        }

        return new EffectDefinitionInfo
        {
            Path = path,
            Guid = guid,
            TypeName = effect.GetType().Name,
            TargetSelector = targetSelector,
            ConditionTypes = conditions,
            MayProduceDamage = mayProduceDamage,
            HasLifetimeState = hasLifetimeState
        };
    }

    private static List<EffectDefinitionInfo> CollectEffectDefinitions()
    {
        List<EffectDefinitionInfo> results = new();

        foreach (string path in FindAssetPaths("t:SkillEffectDefinition"))
        {
            SkillEffectDefinition effect =
                AssetDatabase.LoadAssetAtPath<SkillEffectDefinition>(path);

            if (effect == null)
                continue;

            results.Add(InspectEffectDefinition(effect));
        }

        return results;
    }

    private static List<string> FindAssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(filter)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    //====================================================================
    // 2. 정적 위험 스캔
    //====================================================================

    /// <summary>
    /// `m_Script: {fileID: 0}` 는 두 종류가 있다.
    /// 1) FATAL — m_EditorClassIdentifier 까지 비어 있어 실제로 타입을 복원할 수 없다.
    /// 2) WARNING — `m_EditorClassIdentifier` 가 채워져 있어 Unity가 타입을 복원하는
    ///    기존 직렬화 특성(감정 증강 정의 등). 실제 로드는 정상이며 G0 PASS를 막지 않는다.
    /// </summary>
    private static List<MissingScriptInfo> ScanMissingScripts()
    {
        List<MissingScriptInfo> results = new();
        string assetsRoot = Application.dataPath;

        foreach (string file in Directory.EnumerateFiles(
                     assetsRoot,
                     "*.*",
                     SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);

            if (!string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            string[] lines = text.Split('\n');
            bool fileHasEntry = false;
            bool fileHasFatal = false;
            string identifierSample = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(
                        "m_Script: {fileID: 0}",
                        StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                fileHasEntry = true;
                string identifier = string.Empty;

                for (int lookAhead = i + 1;
                     lookAhead < lines.Length && lookAhead <= i + 12;
                     lookAhead++)
                {
                    string trimmed = lines[lookAhead].Trim();

                    if (trimmed.StartsWith(
                            "m_EditorClassIdentifier:",
                            StringComparison.Ordinal))
                    {
                        identifier = trimmed
                            .Substring("m_EditorClassIdentifier:".Length)
                            .Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(identifier))
                {
                    fileHasFatal = true;
                }
                else if (string.IsNullOrEmpty(identifierSample))
                {
                    identifierSample = identifier;
                }
            }

            if (!fileHasEntry)
                continue;

            results.Add(new MissingScriptInfo
            {
                Path = ToProjectRelativePath(file),
                Classification = fileHasFatal ? "FATAL" : "WARNING",
                EditorClassIdentifierSample = identifierSample
            });
        }

        return results
            .GroupBy(
                info => info.Path + "|" + info.Classification,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(info => info.Classification, StringComparer.Ordinal)
            .ThenBy(info => info.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static List<DuplicateGuidGroup> ScanDuplicateGuids()
    {
        Dictionary<string, List<string>> map =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string meta in Directory.EnumerateFiles(
                     Application.dataPath,
                     "*.meta",
                     SearchOption.AllDirectories))
        {
            string guid = null;

            using (StreamReader reader = new(meta))
            {
                for (int i = 0; i < 5; i++)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                        break;

                    if (line.StartsWith("guid:", StringComparison.Ordinal))
                    {
                        guid = line.Substring(5).Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(guid))
                continue;

            if (!map.TryGetValue(guid, out List<string> list))
            {
                list = new List<string>();
                map[guid] = list;
            }

            list.Add(ToProjectRelativePath(meta));
        }

        return map
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new DuplicateGuidGroup
            {
                Guid = pair.Key,
                Paths = pair.Value
            })
            .OrderBy(group => group.Guid, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<int, int> ScanRawTimingHistogram()
    {
        Dictionary<int, int> histogram = new();
        Regex regex = new(@"(?m)^\s*timing:\s*(-?\d+)\s*$");

        foreach (string file in Directory.EnumerateFiles(
                     Application.dataPath,
                     "*.asset",
                     SearchOption.AllDirectories))
        {
            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            foreach (Match match in regex.Matches(text))
            {
                if (!int.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value))
                {
                    continue;
                }

                histogram.TryGetValue(value, out int count);
                histogram[value] = count + 1;
            }
        }

        return histogram;
    }

    private static List<BattleEventSubscription> ScanBattleEventSubscriptions()
    {
        List<string> eventNames = ReadBattleEventNames();
        if (eventNames.Count == 0)
            return new List<BattleEventSubscription>();

        Regex subscription = new(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(" +
            string.Join("|", eventNames.Select(Regex.Escape)) +
            @")\s*\+=",
            RegexOptions.Compiled);

        List<BattleEventSubscription> results = new();

        foreach (string file in EnumerateRuntimeSources())
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = subscription.Match(lines[i]);

                if (!match.Success)
                    continue;

                results.Add(new BattleEventSubscription
                {
                    Path = ToProjectRelativePath(file),
                    Line = i + 1,
                    EventName = match.Groups[2].Value,
                    Source = lines[i].Trim()
                });
            }
        }

        return results;
    }

    private static List<string> ReadBattleEventNames()
    {
        string path = Path.Combine(
            Application.dataPath,
            "1. Scripts/Runtime/Battle/Events/BattleEvent.cs");

        if (!File.Exists(path))
            return new List<string>();

        string text = File.ReadAllText(path);
        MatchCollection matches = Regex.Matches(
            text,
            @"public\s+event\s+[^;]+?(\b[A-Za-z_][A-Za-z0-9_]*)\s*;",
            RegexOptions.Singleline);

        return matches
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ScanSourceMatches(string pattern)
    {
        Regex regex = new(pattern, RegexOptions.Compiled);
        List<string> results = new();

        foreach (string file in EnumerateRuntimeSources())
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (!regex.IsMatch(lines[i]))
                    continue;

                results.Add(
                    $"{ToProjectRelativePath(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        return results;
    }

    private static IEnumerable<string> EnumerateRuntimeSources()
    {
        string root = Path.Combine(Application.dataPath, "1. Scripts");

        if (!Directory.Exists(root))
            yield break;

        foreach (string file in Directory.EnumerateFiles(
                     root,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static IEnumerable<string> EnumerateProductionRuntimeSources()
    {
        foreach (string file in EnumerateRuntimeSources())
        {
            string relative = ToProjectRelativePath(file);

            if (relative.IndexOf(
                    "/Editor/",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                relative.IndexOf(
                    "/Diagnostics/",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            yield return file;
        }
    }

    //====================================================================
    // 2-K. G0-K — 캐릭터 전용 키워드 / 소스 / 상태 저장 실사
    //====================================================================

    private static readonly string[] StatusBaseTypes =
    {
        "StatusEffect",
        "NumericTimedStatus",
        "PresenceTimedStatus",
        "DamageStatus",
        "CrowdControlStatus",
        "BrokenPartStatus",
        "PartDisabledStatus",
        "DeferredStatusEffect"
    };

    private static readonly string[] StatusHookNames =
    {
        "OnApply",
        "OnTurnStart",
        "OnTurnEnd",
        "OnRemove",
        "ModifyRoll",
        "ModifyExchangeRollCount",
        "ModifyDamage",
        "ModifyDamageTaken",
        "ModifyHealing",
        "ModifySpeed",
        "CanUseSkill",
        "CanAct",
        "GetStaggerDamageTakenFlatModifier"
    };

    private static Dictionary<string, string> assetTextCache;

    private static Dictionary<string, string> GetAssetTextCache()
    {
        if (assetTextCache != null)
            return assetTextCache;

        assetTextCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string dataRoot = Path.Combine(Application.dataPath, "2. Data");

        if (!Directory.Exists(dataRoot))
            return assetTextCache;

        foreach (string file in Directory.EnumerateFiles(
                     dataRoot,
                     "*.asset",
                     SearchOption.AllDirectories))
        {
            try
            {
                assetTextCache[ToProjectRelativePath(file)] =
                    File.ReadAllText(file);
            }
            catch
            {
                // 감사 캐시는 실패한 파일을 조용히 건너뛴다.
            }
        }

        return assetTextCache;
    }

    private static List<G0KeywordRow> ScanKeywordAudit()
    {
        List<G0KeywordRow> rows = new();
        Regex classRegex = new(
            @"\b((?:(?:sealed|abstract|public|internal|static)\s+)*)class\s+([A-Za-z_]\w*)\s*:\s*([A-Za-z_][\w<>,\. ]*)",
            RegexOptions.Compiled);

        List<(string Path, string Text)> sources = new();

        foreach (string file in EnumerateRuntimeSources())
        {
            try
            {
                sources.Add((file, File.ReadAllText(file)));
            }
            catch
            {
                // 읽기 실패 파일은 감사에서 제외한다.
            }
        }

        foreach ((string path, string text) in sources)
        {
            MatchCollection matches = classRegex.Matches(text);

            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                string modifiers = match.Groups[1].Value;
                string className = match.Groups[2].Value;
                string baseList = match.Groups[3].Value;

                if (modifiers.IndexOf(
                        "abstract",
                        StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                bool isStatusClass = StatusBaseTypes.Any(baseType =>
                    Regex.IsMatch(
                        baseList,
                        @"\b" + Regex.Escape(baseType) + @"\b"));

                if (!isStatusClass)
                    continue;

                int bodyEnd =
                    index + 1 < matches.Count
                        ? matches[index + 1].Index
                        : text.Length;

                string body = text.Substring(
                    match.Index,
                    Math.Max(0, bodyEnd - match.Index));

                string keywordId = ExtractKeywordId(body);
                string ownerCharacterId =
                    ResolveCharacterIdFromPath(path);
                string carrierScope = ResolveCarrierScope(
                    className,
                    baseList,
                    body);
                string storageKind = ResolveStorageKind(
                    baseList,
                    body);
                string applyPhase = string.Join(
                    "+",
                    StatusHookNames
                        .Where(hook => Regex.IsMatch(
                            body,
                            @"override\s+[^;{]*\b" + hook + @"\s*\("))
                        .Distinct(StringComparer.Ordinal));
                string removePolicy = ResolveRemovePolicy(body);
                string ownedRule =
                    baseList.IndexOf(
                        "IUniqueKeywordStatus",
                        StringComparison.Ordinal) >= 0
                        ? "UniqueKeyword"
                        : "Generic";
                string codePath = string.Join(
                    " | ",
                    FindStatusCodePaths(className).Take(5));
                string serializedRefs = string.Join(
                    " | ",
                    FindSerializedKeywordRefs(className, keywordId).Take(8));

                rows.Add(new G0KeywordRow
                {
                    KeywordId = string.IsNullOrEmpty(keywordId)
                        ? className
                        : keywordId,
                    ClassName = className,
                    OwnerCharacterId = ownerCharacterId,
                    CarrierScope = carrierScope,
                    StorageKind = storageKind,
                    ApplyPhase = string.IsNullOrEmpty(applyPhase)
                        ? "None"
                        : applyPhase,
                    EffectSource = ToProjectRelativePath(path),
                    RemovePolicy = removePolicy,
                    OwnedRule = ownedRule,
                    CurrentCodePath = codePath,
                    SerializedRefs = serializedRefs,
                    NeedsManualReview =
                        ownedRule == "UniqueKeyword" &&
                        string.IsNullOrEmpty(keywordId)
                });
            }
        }

        return rows
            .OrderBy(row => row.OwnerCharacterId, StringComparer.Ordinal)
            .ThenBy(row => row.KeywordId, StringComparer.Ordinal)
            .ToList();
    }

    private static string ExtractKeywordId(string body)
    {
        Match direct = Regex.Match(
            body,
            @"UniqueKeywordId\s*=>\s*""([^""]+)""");

        if (direct.Success)
            return direct.Groups[1].Value;

        Match property = Regex.Match(
            body,
            @"UniqueKeywordId\s*\{[^}]*?return\s+""([^""]+)""",
            RegexOptions.Singleline);

        if (property.Success)
            return property.Groups[1].Value;

        return string.Empty;
    }

    private static string ResolveCharacterIdFromPath(string path)
    {
        string[] segments = ToProjectRelativePath(path)
            .Split('/');

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(
                    segments[i],
                    "Characters",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    segments[i],
                    "CharacterSpecific",
                    StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }

        return "Shared";
    }

    private static string ResolveCarrierScope(
        string className,
        string baseList,
        string body)
    {
        if (className.IndexOf("Part", StringComparison.OrdinalIgnoreCase) >= 0 ||
            baseList.IndexOf("BrokenPartStatus", StringComparison.Ordinal) >= 0 ||
            baseList.IndexOf("PartDisabledStatus", StringComparison.Ordinal) >= 0 ||
            body.IndexOf("IsPartEffect", StringComparison.Ordinal) >= 0)
        {
            return "Part";
        }

        if (className.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Slot";

        return "Actor";
    }

    private static string ResolveStorageKind(
        string baseList,
        string body)
    {
        Match overridden = Regex.Match(
            body,
            @"StorageKind\s*=>\s*StatusEffectStorageKind\.(\w+)");

        if (overridden.Success)
            return overridden.Groups[1].Value;

        if (baseList.IndexOf("NumericTimedStatus", StringComparison.Ordinal) >= 0)
            return "NumericTimed";

        if (baseList.IndexOf("PresenceTimedStatus", StringComparison.Ordinal) >= 0)
            return "Presence";

        if (baseList.IndexOf("DeferredStatusEffect", StringComparison.Ordinal) >= 0)
            return "Deferred";

        return "Bespoke";
    }

    private static string ResolveRemovePolicy(string body)
    {
        List<string> parts = new();

        Match durationPolicy = Regex.Match(
            body,
            @"DurationPolicy\s*=>\s*([^;]+);",
            RegexOptions.Singleline);

        if (durationPolicy.Success)
        {
            parts.Add(
                "DurationPolicy=" +
                durationPolicy.Groups[1].Value
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim());
        }

        Match transfer = Regex.Match(
            body,
            @"TransferToCharacterOnPartBreak\s*=>\s*(true|false)");

        if (transfer.Success)
            parts.Add("TransferOnPartBreak=" + transfer.Groups[1].Value);

        if (Regex.IsMatch(body, @"override\s+void\s+OnRemove\s*\("))
            parts.Add("OnRemove=override");

        return parts.Count == 0
            ? "Default(DurationPolicy/Manual)"
            : string.Join("; ", parts);
    }

    private static List<string> FindStatusCodePaths(string className)
    {
        Regex callSite = new(
            @"new\s+" + Regex.Escape(className) + @"\s*\(" +
            @"|GetStatus<" + Regex.Escape(className) + @">" +
            @"|GetPartStatus<" + Regex.Escape(className) + @">" +
            @"|HasStatus<" + Regex.Escape(className) + @">" +
            @"|HasPartStatus<" + Regex.Escape(className) + @">",
            RegexOptions.Compiled);

        List<string> results = new();

        foreach (string file in EnumerateRuntimeSources())
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (!callSite.IsMatch(lines[i]))
                    continue;

                results.Add(
                    $"{ToProjectRelativePath(file)}:{i + 1}");
            }
        }

        return results;
    }

    private static List<string> FindSerializedKeywordRefs(
        string className,
        string keywordId)
    {
        List<string> results = new();

        foreach (KeyValuePair<string, string> pair in GetAssetTextCache())
        {
            bool containsClass = pair.Value.IndexOf(
                className,
                StringComparison.Ordinal) >= 0;
            bool containsKeyword =
                !string.IsNullOrEmpty(keywordId) &&
                pair.Value.IndexOf(
                    keywordId,
                    StringComparison.Ordinal) >= 0;

            if (containsClass || containsKeyword)
                results.Add(pair.Key);
        }

        return results
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static List<G0KeywordGlossaryRow> ScanGlossaryKeywords()
    {
        string path = Path.Combine(
            Application.dataPath,
            "1. Scripts/Runtime/Presentation/UI/Components/BattleKeywordGlossary.cs");

        if (!File.Exists(path))
            return new List<G0KeywordGlossaryRow>();

        string text = File.ReadAllText(path);
        MatchCollection matches = Regex.Matches(
            text,
            @"\[\s*""([^""]+)""\s*\]\s*=\s*\r?\n?\s*""((?:[^""\\]|\\.)*)""",
            RegexOptions.Singleline);

        return matches
            .Select(match => new G0KeywordGlossaryRow
            {
                Keyword = UnescapeCSharpString(match.Groups[1].Value),
                DescriptionPreview = UnescapeCSharpString(
                    match.Groups[2].Value),
                Source = "BattleKeywordGlossary.cs"
            })
            .GroupBy(row => row.Keyword, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.Keyword, StringComparer.Ordinal)
            .ToList();
    }

    private static string UnescapeCSharpString(string value)
    {
        return Regex.Replace(
            value,
            @"\\u([0-9A-Fa-f]{4})",
            match => ((char)Convert.ToInt32(
                match.Groups[1].Value,
                16)).ToString());
    }

    private static List<G0SkillKeywordRow> ScanSkillKeywordAuthoring()
    {
        List<G0SkillKeywordRow> rows = new();

        foreach (string path in FindAssetPaths("t:SkillDefinition"))
        {
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null ||
                definition.Keywords == null ||
                definition.Keywords.Count == 0)
            {
                continue;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);

            for (int index = 0; index < definition.Keywords.Count; index++)
            {
                SkillKeywordEntry keyword = definition.Keywords[index];

                if (keyword == null)
                    continue;

                rows.Add(new G0SkillKeywordRow
                {
                    AssetPath = path,
                    AssetGuid = guid,
                    SkillId = definition.SkillId,
                    KeywordIndex = index,
                    KeywordName = keyword.Name,
                    Description = keyword.Description
                });
            }
        }

        return rows
            .OrderBy(row => row.AssetPath, StringComparer.Ordinal)
            .ThenBy(row => row.KeywordIndex)
            .ToList();
    }

    private static List<G0CombatResourceRow> ScanCombatResources()
    {
        List<G0CombatResourceRow> rows = new();

        foreach (string path in FindAssetPaths("t:CharacterData"))
        {
            CharacterData data =
                AssetDatabase.LoadAssetAtPath<CharacterData>(path);

            if (data?.InitialResources == null)
                continue;

            string guid = AssetDatabase.AssetPathToGUID(path);

            for (int index = 0; index < data.InitialResources.Count; index++)
            {
                CombatResourceDefinition resource =
                    data.InitialResources[index];

                if (resource == null)
                    continue;

                rows.Add(new G0CombatResourceRow
                {
                    AssetPath = path,
                    AssetGuid = guid,
                    CharacterName = data.CharacterName,
                    Index = index,
                    Key = resource.Key,
                    InitialValue = resource.InitialValue,
                    MaxValue = resource.MaxValue
                });
            }
        }

        return rows
            .OrderBy(row => row.AssetPath, StringComparer.Ordinal)
            .ThenBy(row => row.Index)
            .ToList();
    }

    private static readonly
        (string Keyword, string Owner, string Carrier, string Storage,
         string[] Patterns, string DecisionNote)[]
        RequiredKeywordSpecs =
        {
            ("Bleed", "Olaf", "Actor", "Bespoke",
                new[] { "Bleeding" }, string.Empty),
            ("Fear", "Olaf", "Actor", "Presence",
                new[] { "OlafFearStatus" }, string.Empty),
            ("Madness", "Olaf", "Actor", "Bespoke",
                new[] { "OlafMadnessMechanic", "AddMadness" },
                "광기 '받는 피해 +1'의 정확한 피해 채널(HP/흐트러짐/고정) 미정"),
            ("Mark", "Yujin", "Actor", "Bespoke",
                new[] { "ApplyMarkGain", "AddMark" },
                "예약 표식 발화 시 무기 판정 기준 미정"),
            ("Designation", "Yujin", "Actor", "NumericTimed",
                new[] { "YujinDesignationStatus" }, string.Empty),
            ("AssassinSense", "Yujin", "Actor", "Bespoke",
                new[] { "AssassinSense" },
                "런타임 구현 없음 — 신규 콘텐츠"),
            ("BloodPrice", "Hifumi(문서) / Olaf(코드)", "Actor", "Bespoke",
                new[] { "BloodPrice" },
                "문서상 히후미 키워드이나 코드에는 Olaf 스킬 ID만 존재 — 소유/의미 충돌 미정"),
            ("Taunt", "Hifumi", "Actor", "Bespoke",
                new[] { "Taunt" },
                "런타임 구현 없음 — 은신과의 목표 충돌 우선순위 미정"),
            ("Bone", "Hifumi", "Actor", "Bespoke",
                new[] { "AdvanceBone", "AddBone" }, string.Empty),
            ("Advance", "Hifumi", "Actor", "Bespoke",
                new[] { "AdvanceBone" },
                "'가불' 의미와 턴 종료 정산 시점은 기획 확인 필요"),
            ("Stealth", "Yujin", "Actor", "Bespoke",
                new[] { "Stealth" },
                "런타임 구현 없음 — 도발과의 충돌 규칙 미정"),
            ("ExplosiveTrap", "Yujin", "Part", "Bespoke",
                new[] { "ExplosiveTrap" },
                "현재 YujinMarkTrap 단일 프록시만 존재 — 3종 분리 미정"),
            ("ResonanceTrap", "Yujin", "Part", "Bespoke",
                new[] { "ResonanceTrap" },
                "런타임 구현 없음 — §18.4 확장 검증 대상"),
            ("ContagionTrap", "Yujin", "Part", "Bespoke",
                new[] { "ContagionTrap" },
                "런타임 구현 없음 — §18.7 확장 검증 대상")
        };

    private static List<G0RequiredKeywordRow>
        ScanRequiredKeywordCoverage()
    {
        List<(string Path, string[] Lines)> sources = new();

        foreach (string file in EnumerateProductionRuntimeSources())
        {
            try
            {
                sources.Add((
                    ToProjectRelativePath(file),
                    File.ReadAllLines(file)));
            }
            catch
            {
                // 읽기 실패 파일은 감사에서 제외한다.
            }
        }

        List<G0RequiredKeywordRow> rows = new();

        foreach ((string keyword, string owner, string carrier,
                  string storage, string[] patterns,
                  string decisionNote) in RequiredKeywordSpecs)
        {
            List<string> found = new();

            foreach (string pattern in patterns)
            {
                Regex regex = new(
                    @"\b" + Regex.Escape(pattern) + @"\b",
                    RegexOptions.Compiled);
                bool matched = false;

                foreach ((string path, string[] lines) in sources)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (!regex.IsMatch(lines[i]))
                            continue;

                        found.Add($"{path}:{i + 1}");
                        matched = true;
                    }
                }

                if (!matched)
                {
                    found.Add($"(코드에 '{pattern}' 없음)");
                }
            }

            bool exists = found.Any(entry =>
                !entry.StartsWith("(코드에", StringComparison.Ordinal));

            rows.Add(new G0RequiredKeywordRow
            {
                KeywordId = keyword,
                OwnerCharacterId = owner,
                CarrierScope = carrier,
                StorageKind = storage,
                CoverageStatus = exists
                    ? (string.IsNullOrEmpty(decisionNote)
                        ? "Found"
                        : "FoundButPolicyUndecided")
                    : "Missing",
                ObservedIdentifiers = string.Join(" | ", found.Take(4)),
                DecisionNote = decisionNote
            });
        }

        return rows;
    }

    //====================================================================
    // 2-I. G0-I — 실행 구조 · 반격 · 연출 순서 기준선
    //====================================================================
    private static readonly (string Name, string Category)[] CallGraphTargets =
    {
        ("ResolveClashQueue", "Resolution"),
        ("ResolvePair", "Resolution"),
        ("ResolvePairedExchange", "Resolution"),
        ("ResolveOneSideExchange", "Resolution"),
        ("ExecuteSkillOnce", "SkillExecution"),
        ("ExecuteSkill", "SkillExecution"),
        ("RaiseExchangeResolved", "EventRaise"),
        ("RaiseClashResolved", "EventRaise"),
        ("OnExchangeResolved", "CounterHook"),
        ("OnClashResolved", "CounterHook"),
        ("ResolveCounterSequence", "CounterSequence"),
        ("NotifyRollOutcome", "OutcomeSignal"),
        ("OnDamageEventResolved", "DamageSignal"),
        ("ApplyDamageContext", "DamageCommit"),
        ("NotifyHit", "HitSignal")
    };

    private static List<G0CallGraphEdge> ScanCallGraph()
    {
        List<G0CallGraphEdge> edges = new();
        Regex methodDeclaration = new(
            @"^\s{0,12}(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|partial|extern|unsafe)\s+)+[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)\s*\(",
            RegexOptions.Compiled);

        foreach (string file in EnumerateProductionRuntimeSources())
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue;
            }

            string caller = "(file)";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    continue;

                Match declaration = methodDeclaration.Match(line);

                if (declaration.Success &&
                    !IsControlFlowKeyword(declaration.Groups[1].Value))
                {
                    caller = declaration.Groups[1].Value;
                }

                foreach ((string name, string category) in CallGraphTargets)
                {
                    if (!Regex.IsMatch(
                            line,
                            @"\b" + Regex.Escape(name) + @"\s*\("))
                    {
                        continue;
                    }

                    // 선언부 자체는 호출 간선이 아니다.
                    if (declaration.Success &&
                        string.Equals(
                            declaration.Groups[1].Value,
                            name,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    edges.Add(new G0CallGraphEdge
                    {
                        CallerFile = ToProjectRelativePath(file),
                        CallerMethod = caller,
                        Callee = name,
                        Category = category,
                        Line = i + 1,
                        Source = line.Trim()
                    });
                }
            }
        }

        return edges
            .OrderBy(edge => edge.CallerFile, StringComparer.Ordinal)
            .ThenBy(edge => edge.Line)
            .ToList();
    }

    private static bool IsControlFlowKeyword(string value)
    {
        return value == "if" ||
               value == "for" ||
               value == "foreach" ||
               value == "while" ||
               value == "switch" ||
               value == "catch" ||
               value == "using" ||
               value == "lock" ||
               value == "return" ||
               value == "new" ||
               value == "typeof" ||
               value == "nameof" ||
               value == "base";
    }

    private static readonly
        (string Kind, string MappingNote, string[] Patterns)[]
        CounterKindMappings =
        {
            (
                "HifumiCounterRoll",
                "HifumiMechanic.ResolveCounterSequence / BuildCounterPreviewForVerification / IsCounterSkill",
                new[]
                {
                    "ResolveCounterSequence",
                    "HifumiCounterPreview",
                    "IsCounterSkill"
                }),
            (
                "EnemyCounterRoll",
                "BattleReactiveRollSourceKind.Counter — 적 반격은 별도 클래스 없이 반응 굴림으로 표현",
                new[]
                {
                    "BattleReactiveRollSourceKind.Counter"
                }),
            (
                "SecondaryHit",
                "AttackWeightHitResult / ClashResultContext.SecondaryDamageContexts — 공격 가중치 추가 피격",
                new[]
                {
                    "AttackWeightHitResult",
                    "SecondaryDamageContexts"
                }),
            (
                "FollowUpRoll",
                "BattleReactiveRollSourceKind.FollowUp — 후속 굴림",
                new[]
                {
                    "BattleReactiveRollSourceKind.FollowUp"
                }),
            (
                "TriggeredSkillAction",
                "R1-I 신규 반응 스킬 삽입 지점. 현재 런타임 코드 없음(신규 기능)",
                Array.Empty<string>())
        };

    private static List<G0CounterKindRow> ScanCounterKinds()
    {
        List<G0CounterKindRow> rows = new();
        List<(string Path, string[] Lines)> sources = new();

        foreach (string file in EnumerateProductionRuntimeSources())
        {
            try
            {
                sources.Add((
                    ToProjectRelativePath(file),
                    File.ReadAllLines(file)));
            }
            catch
            {
                // 읽기 실패 파일은 감사에서 제외한다.
            }
        }

        foreach ((string kind, string mappingNote, string[] patterns)
                 in CounterKindMappings)
        {
            List<string> sites = new();

            foreach (string pattern in patterns)
            {
                Regex regex = new(
                    @"\b" + Regex.Escape(pattern) + @"\b",
                    RegexOptions.Compiled);

                foreach ((string path, string[] lines) in sources)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (!regex.IsMatch(lines[i]))
                            continue;

                        sites.Add($"{path}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            rows.Add(new G0CounterKindRow
            {
                Kind = kind,
                OccurrenceCount = sites.Count,
                MappingNote = mappingNote,
                ObservedIdentifiers = string.Join(" | ", patterns),
                SampleSites = sites
                    .Distinct(StringComparer.Ordinal)
                    .Take(6)
                    .ToList()
            });
        }

        return rows;
    }

    //====================================================================
    // 2-W. G0-W — 공격 가중치 기준선 및 실제 에셋 감사
    //====================================================================

    private static List<G0AttackWeightRow> ScanAttackWeights()
    {
        List<G0AttackWeightRow> rows = new();

        foreach (string path in FindAssetPaths("t:SkillDefinition"))
        {
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null)
                continue;

            AttackWeightSettings settings =
                definition.AttackWeight ??
                new AttackWeightSettings();

            string guid = AssetDatabase.AssetPathToGUID(path);
            int rollCount =
                definition.Rolls != null && definition.Rolls.Count > 0
                    ? definition.Rolls.Count
                    : definition.ExchangeRollCount;

            rows.Add(new G0AttackWeightRow
            {
                AssetPath = path,
                AssetGuid = guid,
                SkillId = definition.SkillId,
                SkillName = definition.SkillName,
                ActionType = definition.ActionType.ToString(),
                Weight = settings.EffectiveWeight,
                SecondaryPartMode = settings.SecondaryPartMode.ToString(),
                SecondaryDamageMultiplier =
                    settings.SecondaryDamageMultiplier,
                AllowBrokenSecondaryParts =
                    settings.AllowBrokenSecondaryParts,
                RollCount = rollCount,
                EffectEntryCount =
                    definition.EffectEntries?.Count ?? 0,
                KeywordCount =
                    definition.Keywords?.Count ?? 0,
                UsesAnotherPartOnPrimaryCharacter =
                    settings.SecondaryPartMode ==
                    AttackWeightSecondaryPartMode.AnotherPartOnPrimaryCharacter,
                NeedsProtection =
                    settings.EffectiveWeight > 1 ||
                    settings.SecondaryPartMode ==
                    AttackWeightSecondaryPartMode.AnotherPartOnPrimaryCharacter
            });
        }

        return rows
            .OrderByDescending(row => row.Weight)
            .ThenBy(row => row.AssetPath, StringComparer.Ordinal)
            .ToList();
    }

    private static List<G0AttackWeightProtectionRow>
        BuildWeightProtectionRows(
            IReadOnlyList<G0AttackWeightRow> rows)
    {
        List<G0AttackWeightProtectionRow> result = new();

        foreach (G0AttackWeightRow row in rows)
        {
            if (!row.NeedsProtection)
                continue;

            List<string> reasons = new();

            if (row.Weight > 1)
            {
                reasons.Add(
                    $"LegacyWeight={row.Weight} (캐릭터 수 기반)");
            }

            if (row.UsesAnotherPartOnPrimaryCharacter)
            {
                reasons.Add(
                    "AnotherPartOnPrimaryCharacter — 2부위 동시 타격 규칙 분리 필요");
            }

            result.Add(new G0AttackWeightProtectionRow
            {
                AssetPath = row.AssetPath,
                SkillId = row.SkillId,
                ActionType = row.ActionType,
                Weight = row.Weight,
                SecondaryPartMode = row.SecondaryPartMode,
                ProtectionReason = string.Join("; ", reasons)
            });
        }

        return result;
    }

    //====================================================================
    // 3. Golden Baseline (Play Mode)
    //====================================================================

    [MenuItem("Game System Verification/G0 - Golden Baseline (Play Mode)")]
    public static void RunGoldenBaselineFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "G0 Golden Baseline은 Play Mode에서 실행해야 합니다.");
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        if (manager?.BattleContext?.Player == null)
        {
            throw new InvalidOperationException(
                "초기화된 BattleManager와 Player가 필요합니다.");
        }

        pass = 0;
        fail = 0;
        G0ProbeEffectDefinition.Reset();
        G0GoldenScenario.ResetRegistry();

        List<G0GoldenScenarioResult> results = new();
        List<SkillDefinition> createdDefinitions = new();
        List<SkillEffectDefinition> createdEffects = new();

        try
        {
            Character enemySource =
                manager.BattleContext.Enemies != null &&
                manager.BattleContext.Enemies.Count > 0
                    ? manager.BattleContext.Enemies[0]
                    : null;

            if (enemySource == null)
                throw new InvalidOperationException("Enemy가 필요합니다.");

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G01",
                "3굴림 평타 vs 3굴림 평타 (전부 맞붙음)",
                rollCountFirst: 3,
                rollCountSecond: 3,
                firstActionType: ActionType.NormalAttack,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G02",
                "1굴림 평타 vs 1굴림 평타",
                rollCountFirst: 1,
                rollCountSecond: 1,
                firstActionType: ActionType.NormalAttack,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G03",
                "결투 3굴림 vs 결투 3굴림",
                rollCountFirst: 3,
                rollCountSecond: 3,
                firstActionType: ActionType.Duel,
                secondActionType: ActionType.Duel,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G04",
                "결투 1굴림 vs 평타 1굴림",
                rollCountFirst: 1,
                rollCountSecond: 1,
                firstActionType: ActionType.Duel,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G05",
                "평타 3굴림 일방타격 (상대 행동 없음)",
                rollCountFirst: 3,
                rollCountSecond: 0,
                firstActionType: ActionType.NormalAttack,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: true,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G06",
                "평타 3굴림 vs 평타 1굴림 (남은 2굴림 일방 전환)",
                rollCountFirst: 3,
                rollCountSecond: 1,
                firstActionType: ActionType.NormalAttack,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 0,
                createdDefinitions,
                createdEffects));

            results.Add(RunScenario(
                manager,
                enemySource,
                "G0-G07",
                "원본 피해 1회 + 추가타 2회 (AfterDamage 재점화 측정)",
                rollCountFirst: 1,
                rollCountSecond: 1,
                firstActionType: ActionType.NormalAttack,
                secondActionType: ActionType.NormalAttack,
                firstPower: 10,
                secondPower: 1,
                oneSided: false,
                afterDamageSecondaries: 2,
                createdDefinitions,
                createdEffects));

            results.AddRange(RunWeightScenarios(
                manager,
                enemySource,
                createdDefinitions,
                createdEffects));

            RunBaselineChecks(results);
        }
        finally
        {
            foreach (SkillEffectDefinition effect in createdEffects)
            {
                if (effect != null)
                    UnityEngine.Object.DestroyImmediate(effect);
            }

            foreach (SkillDefinition definition in createdDefinitions)
            {
                if (definition != null)
                    UnityEngine.Object.DestroyImmediate(definition);
            }

            G0ProbeEffectDefinition.Reset();
        }

        string outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        G0GoldenBaselineReport report = new()
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            UnityVersion = Application.unityVersion,
            Branch = TryReadGitBranch(),
            Commit = TryReadGitCommit(),
            Scenarios = results
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "G0_GOLDEN_BASELINE.json"),
            JsonUtility.ToJson(report, true),
            new UTF8Encoding(false));

        Debug.Log($"{LogPrefix} GOLDEN RESULT PASS={pass} FAIL={fail}");
        Debug.Log($"{LogPrefix} GOLDEN OUTPUT {outputDirectory}");
    }

    private static G0GoldenScenarioResult RunScenario(
        BattleManager manager,
        Character enemySource,
        string id,
        string description,
        int rollCountFirst,
        int rollCountSecond,
        ActionType firstActionType,
        ActionType secondActionType,
        int firstPower,
        int secondPower,
        bool oneSided,
        int afterDamageSecondaries,
        List<SkillDefinition> createdDefinitions,
        List<SkillEffectDefinition> createdEffects)
    {
        using GameSystemVerificationFixture fixture =
            GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,
                enemySource,
                manager.BattleRules);

        CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
        CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);
        fixture.MomentumManager?.Reset();

        G0ProbeEffectDefinition.ResetCounters();
        G0ProbeEffectDefinition.SecondaryBudget = afterDamageSecondaries;

        List<string> probeLabels = new()
        {
            "OnExecute",
            "OnExchangeWin",
            "OnExchangeLose",
            "OnDuelMatched",
            "OnClashEnd",
            "AfterDamage"
        };

        List<SkillEffectEntry> firstEntries = new();
        List<SkillEffectEntry> secondEntries = new();

        foreach (string label in probeLabels)
        {
            SkillEffectTiming timing = ParseProbeTiming(label);
            firstEntries.Add(CreateEntry(
                label,
                timing,
                "First",
                createdEffects));
            secondEntries.Add(CreateEntry(
                label,
                timing,
                "Second",
                createdEffects));
        }

        Skill firstSkill = CreateProbeSkill(
            fixture.Player,
            firstPower,
            rollCountFirst,
            firstActionType,
            firstEntries,
            createdDefinitions);

        Skill secondSkill = CreateProbeSkill(
            fixture.Enemy,
            secondPower,
            rollCountSecond,
            secondActionType,
            secondEntries,
            createdDefinitions);

        firstSkill.Register();
        secondSkill.Register();

        int damageEventCount = 0;
        int damagePositiveCount = 0;

        void OnDamageEvent(DamageEventResult eventResult)
        {
            damageEventCount++;

            if (eventResult?.Context != null &&
                eventResult.Context.GetDisplayDamage() > 0)
            {
                damagePositiveCount++;
            }
        }

        fixture.Context._battleEvent.OnDamageEventResolved += OnDamageEvent;

        try
        {
            ActionSlot firstSlot = CreateSlot(
                fixture.Player,
                fixture.Enemy,
                firstSkill,
                2026102900 + rollCountFirst * 10 + rollCountSecond);

            ClashPair pair;

            if (oneSided)
            {
                pair = new ClashPair(firstSlot);
            }
            else
            {
                ActionSlot secondSlot = CreateSlot(
                    fixture.Enemy,
                    fixture.Player,
                    secondSkill,
                    2026102950 + rollCountFirst * 10 + rollCountSecond);

                pair = new ClashPair(firstSlot, secondSlot);
            }

            ClashResultContext result =
                fixture.ClashManager.ResolvePair(pair);

            G0GoldenScenarioResult scenario = new()
            {
                Id = id,
                Description = description,
                FirstActionType = firstActionType.ToString(),
                SecondActionType = oneSided ? "None" : secondActionType.ToString(),
                FirstRollCount = rollCountFirst,
                SecondRollCount = oneSided ? 0 : rollCountSecond,
                IsClash = result?.IsClash == true,
                ExchangeCount = result?.Exchanges?.Count ?? 0,
                OneSidedHitCount = result?.OneSidedHitCount ?? 0,
                DamageEventCount = damageEventCount,
                PositiveDamageEventCount = damagePositiveCount,
                ProbeCounts = new List<G0ProbeCount>()
            };

            foreach (string label in probeLabels)
            {
                scenario.ProbeCounts.Add(new G0ProbeCount
                {
                    Label = label,
                    FirstOwnerCount = G0ProbeEffectDefinition.GetCount(label + ":First"),
                    SecondOwnerCount = G0ProbeEffectDefinition.GetCount(label + ":Second")
                });
            }

            scenario.Trace = new List<string>(G0ProbeEffectDefinition.Trace);
            return scenario;
        }
        finally
        {
            fixture.Context._battleEvent.OnDamageEventResolved -= OnDamageEvent;
            firstSkill.Unregister();
            secondSkill.Unregister();
        }
    }

    private static List<G0GoldenScenarioResult> RunWeightScenarios(
        BattleManager manager,
        Character enemySource,
        List<SkillDefinition> createdDefinitions,
        List<SkillEffectDefinition> createdEffects)
    {
        List<G0GoldenScenarioResult> results = new();

        foreach (int weight in new[] { 1, 2, 3 })
        {
            results.Add(RunWeightScenario(
                manager,
                enemySource,
                weight,
                createdDefinitions,
                createdEffects));
        }

        return results;
    }

    /// <summary>
    /// Legacy 가중치 기준선. 메인 1 + 서브 (Weight-1) 피해가 실제로 적용되는지,
    /// 각 피격이 DamageContext/Result를 몇 개 만드는지 고정한다.
    /// 이 단계는 게임플레이를 바꾸지 않고 현재 동작만 기록한다.
    /// </summary>
    private static G0GoldenScenarioResult RunWeightScenario(
        BattleManager manager,
        Character enemySource,
        int weight,
        List<SkillDefinition> createdDefinitions,
        List<SkillEffectDefinition> createdEffects)
    {
        using GameSystemVerificationFixture fixture =
            GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,
                enemySource,
                manager.BattleRules);

        CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
        CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);
        fixture.MomentumManager?.Reset();

        List<Character> extraEnemies = new();

        while (fixture.Context.Enemies.Count < 3)
        {
            GameObject clone = UnityEngine.Object.Instantiate(
                enemySource.gameObject,
                fixture.Context.Player.transform.parent,
                false);

            clone.name =
                enemySource.name + "_G0WeightEnemy" +
                (fixture.Context.Enemies.Count + 1);
            clone.hideFlags =
                HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;

            Character extra = clone.GetComponent<Character>();

            if (extra == null)
            {
                UnityEngine.Object.DestroyImmediate(clone);
                break;
            }

            extra.Initialize(fixture.Context);
            fixture.Context.Enemies.Add(extra);
            extraEnemies.Add(extra);
        }

        Skill skill = CreateProbeSkill(
            fixture.Player,
            10,
            1,
            ActionType.NormalAttack,
            new List<SkillEffectEntry>(),
            createdDefinitions,
            attackWeight: weight);

        skill.Register();

        int damageEventCount = 0;
        int damagePositiveCount = 0;

        void OnDamageEvent(DamageEventResult eventResult)
        {
            damageEventCount++;

            if (eventResult?.Context != null &&
                eventResult.Context.GetDisplayDamage() > 0)
            {
                damagePositiveCount++;
            }
        }

        fixture.Context._battleEvent.OnDamageEventResolved += OnDamageEvent;

        try
        {
            ActionSlot firstSlot = CreateSlot(
                fixture.Player,
                fixture.Enemy,
                skill,
                2026103000 + weight);

            UnityEngine.Random.InitState(
                20261030 + weight);

            ClashResultContext result =
                fixture.ClashManager.ResolvePair(
                    new ClashPair(firstSlot));

            BattleAction action =
                result?.FirstAction;

            List<AttackWeightHitResult> hits =
                action?.AttackWeightHitResults?
                    .ToList() ??
                new List<AttackWeightHitResult>();

            G0GoldenScenarioResult scenario = new()
            {
                Id = "G0-W" + weight.ToString("00", CultureInfo.InvariantCulture),
                Description =
                    $"Legacy 공격 가중치 {weight} (메인 1 + 서브 {weight - 1})",
                FirstActionType = ActionType.NormalAttack.ToString(),
                SecondActionType = "None",
                FirstRollCount = 1,
                SecondRollCount = 0,
                IsClash = result?.IsClash == true,
                ExchangeCount = result?.Exchanges?.Count ?? 0,
                OneSidedHitCount = result?.OneSidedHitCount ?? 0,
                DamageEventCount = damageEventCount,
                PositiveDamageEventCount = damagePositiveCount,
                AttackWeight = weight,
                PrimaryDamage = result?.PrimaryDamage ?? 0,
                SecondaryHitCount =
                    result?.SecondaryDamageContexts?.Count ?? 0,
                SecondaryDamageTotal =
                    result?.AttackWeightDamage ?? 0,
                HitTargets = hits.Select(hit =>
                        (hit.Target?.IsPrimary == true ? "MAIN " : "SUB ") +
                        (hit.Target?.TargetCharacter?.name ?? "null") +
                        "/" +
                        (hit.Target?.TargetPart?.PartId ?? "null") +
                        "=" +
                        hit.Damage)
                    .ToList(),
                ProbeCounts = new List<G0ProbeCount>()
            };

            scenario.Trace = new List<string>
            {
                $"weight={weight}",
                $"hits={hits.Count}",
                $"secondaryContexts={scenario.SecondaryHitCount}",
                $"primaryDamage={scenario.PrimaryDamage}",
                $"secondaryDamage={scenario.SecondaryDamageTotal}",
                $"damageEvents={damageEventCount}"
            };

            return scenario;
        }
        finally
        {
            fixture.Context._battleEvent.OnDamageEventResolved -= OnDamageEvent;
            skill.Unregister();
        }
    }

    private static SkillEffectEntry CreateEntry(
        string label,
        SkillEffectTiming timing,
        string ownerSuffix,
        List<SkillEffectDefinition> createdEffects)
    {
        G0ProbeEffectDefinition probe =
            ScriptableObject.CreateInstance<G0ProbeEffectDefinition>();
        probe.name = $"G0Probe_{label}_{ownerSuffix}";
        probe.ProbeLabel = label + ":" + ownerSuffix;
        SetEffectTiming(probe, timing);

        createdEffects.Add(probe);

        return new SkillEffectEntry
        {
            Definition = probe,
            OverrideTiming = true,
            Timing = timing,
            Conditions = new List<SkillEffectCondition>(),
            RestrictToRoll = false,
            RollNumber = 1
        };
    }

    private static void SetEffectTiming(
        SkillEffectDefinition effect,
        SkillEffectTiming timing)
    {
        SerializedObject serialized = new(effect);
        SerializedProperty property = serialized.FindProperty("timing");

        if (property != null)
        {
            property.intValue = (int)timing;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static SkillEffectTiming ParseProbeTiming(string label) => label switch
    {
        "OnExecute" => SkillEffectTiming.OnExecute,
        "OnExchangeWin" => SkillEffectTiming.OnExchangeWin,
        "OnExchangeLose" => SkillEffectTiming.OnExchangeLose,
        "OnDuelMatched" => SkillEffectTiming.OnDuelMatched,
        "OnClashEnd" => SkillEffectTiming.OnClashEnd,
        "AfterDamage" => SkillEffectTiming.AfterDamage,
        _ => SkillEffectTiming.OnExecute
    };

    private static Skill CreateProbeSkill(
        Character owner,
        int power,
        int rollCount,
        ActionType actionType,
        List<SkillEffectEntry> entries,
        List<SkillDefinition> createdDefinitions,
        int attackWeight = 1)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name =
            $"G0Golden_{actionType}_{rollCount}_{power}";
        definition.SkillName = definition.name;
        definition.ActionType = actionType;
        definition.Color = SkillColor.Red;
        definition.ResolverType = SkillResolverType.Dice;
        definition.ExchangeRollCount = rollCount;
        definition.AttackWeight =
            new AttackWeightSettings
            {
                Weight = attackWeight
            };
        definition.Rolls = new List<SkillRollData>();

        for (int i = 0; i < rollCount; i++)
        {
            SkillRollData roll = new()
            {
                Index = i,
                Type = CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                RngSource = RollRngSource.CharacterDefault,
                ReuseValueAcrossAction = false
            };
            roll.Sanitize(i);
            definition.Rolls.Add(roll);
        }

        definition.EffectEntries = new List<SkillEffectEntry>(entries);
        createdDefinitions.Add(definition);

        Skill skill = definition.CreateRuntimeSkill();
        skill.Initialize(owner, owner.BattleEvent);
        return skill;
    }

    private static ActionSlot CreateSlot(
        Character owner,
        Character target,
        Skill skill,
        long actionId)
    {
        BodyPart ownerPart =
            owner.BodyParts != null && owner.BodyParts.Count > 0
                ? owner.BodyParts[0]
                : null;

        BodyPart targetPart =
            target.BodyParts != null && target.BodyParts.Count > 0
                ? target.BodyParts[0]
                : null;

        return new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = ownerPart,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = "VERIFY:G0",
            Phase = ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = targetPart,
            ResourceCostCommitted = true
        };
    }

    private static void RunBaselineChecks(
        IReadOnlyList<G0GoldenScenarioResult> results)
    {
        Check("G0-G01 baseline captured",
            results.Any(item => item.Id == "G0-G01" && item.ExchangeCount > 0),
            "G0-G01");

        Check("G0-G02 baseline captured",
            results.Any(item => item.Id == "G0-G02" && item.ExchangeCount > 0),
            "G0-G02");

        Check("G0-G03 duel-vs-duel captured",
            results.Any(item => item.Id == "G0-G03" && item.IsClash),
            "G0-G03");

        Check("G0-G04 duel-vs-normal captured",
            results.Any(item => item.Id == "G0-G04" && item.IsClash),
            "G0-G04");

        Check("G0-G05 one-sided captured",
            results.Any(item =>
                item.Id == "G0-G05" &&
                !item.IsClash &&
                item.OneSidedHitCount > 0),
            "G0-G05");

        Check("G0-G06 mismatch-to-one-sided captured",
            results.Any(item => item.Id == "G0-G06" && item.ExchangeCount > 1),
            "G0-G06");

        Check("G0-G07 additional-hit baseline captured",
            results.Any(item => item.Id == "G0-G07" && item.DamageEventCount >= 3),
            "G0-G07");

        Check("G0-W01 legacy weight 1 captured",
            results.Any(item =>
                item.Id == "G0-W01" &&
                item.AttackWeight == 1 &&
                item.SecondaryHitCount == 0 &&
                item.PrimaryDamage > 0),
            "G0-W01");

        Check("G0-W02 legacy weight 2 captured",
            results.Any(item =>
                item.Id == "G0-W02" &&
                item.AttackWeight == 2 &&
                item.SecondaryHitCount == 1 &&
                item.PrimaryDamage > 0),
            "G0-W02");

        Check("G0-W03 legacy weight 3 captured",
            results.Any(item =>
                item.Id == "G0-W03" &&
                item.AttackWeight == 3 &&
                item.SecondaryHitCount == 2 &&
                item.PrimaryDamage > 0),
            "G0-W03");

        G0GoldenScenarioResult g01 = results.FirstOrDefault(item => item.Id == "G0-G01");
        G0GoldenScenarioResult g03 = results.FirstOrDefault(item => item.Id == "G0-G03");
        G0GoldenScenarioResult g05 = results.FirstOrDefault(item => item.Id == "G0-G05");
        G0GoldenScenarioResult g07 = results.FirstOrDefault(item => item.Id == "G0-G07");

        Debug.Log(
            $"{LogPrefix} BEFORE G0-G01 사용시(OnExecute)=" +
            $"{GetProbe(g01, "OnExecute", first: true)} " +
            $"교환승리={GetProbe(g01, "OnExchangeWin", first: true)} " +
            $"합종료={GetProbe(g01, "OnClashEnd", first: true)}");

        Debug.Log(
            $"{LogPrefix} BEFORE G0-G03 매칭시(OnDuelMatched)=" +
            $"{GetProbe(g03, "OnDuelMatched", first: true)} " +
            $"사용시={GetProbe(g03, "OnExecute", first: true)}");

        Debug.Log(
            $"{LogPrefix} BEFORE G0-G05 일방 평타 교환승리=" +
            $"{GetProbe(g05, "OnExchangeWin", first: true)} " +
            $"합종료={GetProbe(g05, "OnClashEnd", first: true)}");

        Debug.Log(
            $"{LogPrefix} BEFORE G0-G07 AfterDamage 발동=" +
            $"{GetProbe(g07, "AfterDamage", first: true)} " +
            $"DamageEvent={g07?.DamageEventCount}");

        Debug.Log(
            $"{LogPrefix} CONTRACT AFTER  G0-G01 사용시=3 / " +
            $"G0-G03 매칭시=0(게이트로 대체) / G0-G05 일방 승리시=0 / " +
            $"G0-G07 원본 AfterDamage=1");
    }

    private static int GetProbe(
        G0GoldenScenarioResult result,
        string label,
        bool first)
    {
        G0ProbeCount count = result?.ProbeCounts?
            .FirstOrDefault(item => item.Label == label);

        if (count == null)
            return -1;

        return first
            ? count.FirstOwnerCount
            : count.SecondOwnerCount;
    }

    //====================================================================
    // 4. 출력
    //====================================================================

    private static string ResolveOutputDirectory()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Application.dataPath;

        return Path.Combine(projectRoot, OutputFolderName);
    }

    private static void WriteCsv(string path, IReadOnlyList<EntryRow> rows)
    {
        StringBuilder builder = new();

        builder.AppendLine(
            "AssetPath,AssetGuid,SkillId,SkillName,ActionType,IsDuel," +
            "EntryIndex,RollIndexOrNone,SourceKind,EffectDefinitionGuid," +
            "EffectDefinitionPath,LegacyOrNew,SerializedTimingValue," +
            "EffectiveTimingValue,DisplayTiming,RollNumberRestriction," +
            "ConditionTypes,TargetSelector,OperationType,MayProduceDamage," +
            "HasLifetimeState,RuntimeEventWake,NeedsManualReview");

        foreach (EntryRow row in rows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(row.AssetPath),
                Csv(row.AssetGuid),
                Csv(row.SkillId),
                Csv(row.SkillName),
                Csv(row.ActionType),
                Csv(row.IsDuel),
                Csv(row.EntryIndex),
                Csv(row.RollIndexOrNone),
                Csv(row.SourceKind),
                Csv(row.EffectDefinitionGuid),
                Csv(row.EffectDefinitionPath),
                Csv(row.LegacyOrNew),
                Csv(row.SerializedTimingValue),
                Csv(row.EffectiveTimingValue),
                Csv(row.DisplayTiming),
                Csv(row.RollNumberRestriction),
                Csv(row.ConditionTypes),
                Csv(row.TargetSelector),
                Csv(row.OperationType),
                Csv(row.MayProduceDamage),
                Csv(row.HasLifetimeState),
                Csv(row.RuntimeEventWake),
                Csv(row.NeedsManualReview)
            }));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    private static void WriteCsv(
        string path,
        IReadOnlyList<G0TriggerStatRow> rows)
    {
        StringBuilder builder = new();
        builder.AppendLine("Scope,TimingValue,DisplayTiming,Count");

        foreach (G0TriggerStatRow row in rows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(row.Scope),
                Csv(row.TimingValue),
                Csv(row.DisplayTiming),
                Csv(row.Count)
            }));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    private static void WriteCsv<T>(
        string path,
        string header,
        IReadOnlyList<T> rows,
        Func<T, object[]> selector)
    {
        StringBuilder builder = new();
        builder.AppendLine(header);

        foreach (T row in rows)
        {
            builder.AppendLine(string.Join(",", selector(row).Select(Csv)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    private static List<G0TriggerStatRow> BuildTriggerStatRows(
        Dictionary<int, int> raw,
        Dictionary<int, int> effective,
        Dictionary<string, int> canonical)
    {
        List<G0TriggerStatRow> rows = new();

        foreach (KeyValuePair<int, int> pair in raw.OrderBy(item => item.Key))
        {
            rows.Add(new G0TriggerStatRow
            {
                Scope = "RawAssetTimingField",
                TimingValue = pair.Key,
                DisplayTiming = SkillEffectTimingCatalog.GetDisplayName(
                    (SkillEffectTiming)pair.Key),
                Count = pair.Value
            });
        }

        foreach (KeyValuePair<int, int> pair in effective.OrderBy(item => item.Key))
        {
            rows.Add(new G0TriggerStatRow
            {
                Scope = "EffectiveEntryTiming",
                TimingValue = pair.Key,
                DisplayTiming = SkillEffectTimingCatalog.GetDisplayName(
                    (SkillEffectTiming)pair.Key),
                Count = pair.Value
            });
        }

        foreach (KeyValuePair<string, int> pair in canonical.OrderBy(item => item.Key))
        {
            rows.Add(new G0TriggerStatRow
            {
                Scope = "CanonicalContractWatch",
                TimingValue = -1,
                DisplayTiming = pair.Key,
                Count = pair.Value
            });
        }

        return rows;
    }

    private static string BuildMarkdown(
        AuditSummary summary,
        string outputDirectory)
    {
        StringBuilder builder = new();

        builder.AppendLine("# G0 — 능력 5요소 아키텍처 기준선 감사");
        builder.AppendLine();
        builder.AppendLine($"- 생성 시각(UTC): `{summary.GeneratedAtUtc}`");
        builder.AppendLine($"- Unity: `{summary.UnityVersion}`");
        builder.AppendLine($"- 브랜치: `{summary.Branch}`");
        builder.AppendLine($"- 커밋: `{summary.Commit}`");
        builder.AppendLine($"- 프로젝트: `{summary.ProjectPath}`");
        builder.AppendLine();

        builder.AppendLine("## 1. 요약");
        builder.AppendLine();
        builder.AppendLine("| 항목 | 값 |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| SkillDefinition 에셋 | {summary.SkillAssetCount} |");
        builder.AppendLine($"| 감사 대상 EffectEntry | {summary.EntryCount} |");
        builder.AppendLine($"| SkillEffectDefinition 에셋 | {summary.EffectDefinitionAssetCount} |");
        builder.AppendLine($"| 미참조 EffectDefinition | {summary.OrphanEffectDefinitionCount} |");
        builder.AppendLine($"| EmotionAugmentDefinition 에셋 | {summary.EmotionAugmentAssetCount} |");
        builder.AppendLine($"| Missing Script 에셋/씬/프리팹 (FATAL) | {summary.MissingScriptAssetCount} |");
        builder.AppendLine($"| Missing Script 경고(m_EditorClassIdentifier 복원 가능) | {summary.MissingScriptWarningAssetCount} |");
        builder.AppendLine($"| 중복 GUID 그룹 | {summary.DuplicateGuidGroupCount} |");
        builder.AppendLine($"| 수동 검토 필요 Entry | {summary.NeedsManualReviewCount} |");
        builder.AppendLine($"| RuntimeEventWake Entry | {summary.RuntimeEventWakeEntryCount} |");
        builder.AppendLine($"| 키워드/상태 저장 감사 행 | {summary.KeywordRowCount} |");
        builder.AppendLine($"| 키워드 수동 검토 대상 | {summary.KeywordManualReviewCount} |");
        builder.AppendLine($"| 필수 키워드 목록 | {summary.RequiredKeywordCount} |");
        builder.AppendLine($"| 필수 키워드 미구현 | {summary.RequiredKeywordMissingCount} |");
        builder.AppendLine($"| 필수 키워드 정책 미정 | {summary.RequiredKeywordUndecidedCount} |");
        builder.AppendLine($"| 용어집 키워드 | {summary.GlossaryKeywordCount} |");
        builder.AppendLine($"| 스킬 저작 키워드 Entry | {summary.SkillKeywordEntryCount} |");
        builder.AppendLine($"| 커스텀 자원 정의 | {summary.CombatResourceCount} |");
        builder.AppendLine($"| 실행 호출 그래프 간선 | {summary.CallGraphEdgeCount} |");
        builder.AppendLine($"| 가중치 2 이상 스킬 | {summary.AttackWeightedSkillCount} |");
        builder.AppendLine($"| 가중치 보호 목록 스킬 | {summary.AttackWeightProtectedSkillCount} |");
        builder.AppendLine();

        builder.AppendLine("## 2. 정본 트리거 사용 현황");
        builder.AppendLine();
        builder.AppendLine("| 트리거 | Entry 수 |");
        builder.AppendLine("|---|---|");

        foreach (TriggerCount pair in summary.CanonicalTriggerUsage)
        {
            builder.AppendLine($"| {pair.Name} | {pair.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("### 원본 에셋에 기록된 `timing:` 필드 분포");
        builder.AppendLine();
        builder.AppendLine("| 값 | 표시 | 개수 |");
        builder.AppendLine("|---|---|---|");

        foreach (TimingCount pair in summary.RawTimingHistogram)
        {
            builder.AppendLine(
                $"| {pair.TimingValue} | {pair.DisplayTiming} | {pair.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 3. Entry 출처(Scope) 분포");
        builder.AppendLine();
        builder.AppendLine("| Scope | 개수 |");
        builder.AppendLine("|---|---|");

        foreach (ScopeCount pair in summary.ScopeHistogram)
        {
            builder.AppendLine($"| {pair.Scope} | {pair.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 4. 위험 경로");
        builder.AppendLine();

        builder.AppendLine($"### 4.1 RuntimeAfterDamage / CanWakeFromRuntimeEvent");
        builder.AppendLine();
        builder.AppendLine(
            "기존 구조는 Authoring Trigger 5종 외에 결과 사건 조건이 " +
            "`CanWakeFromRuntimeEvent()`를 통해 같은 Entry를 다시 깨운다. " +
            "아래 Entry는 R1에서 원본 굴림 Result 연결 여부를 반드시 확인해야 한다.");
        builder.AppendLine();

        if (summary.RuntimeEventWakeEntryCount == 0)
        {
            builder.AppendLine("- 해당 Entry 없음");
        }
        else
        {
            builder.AppendLine("| Asset | Entry | Timing | Condition |");
            builder.AppendLine("|---|---|---|---|");

            foreach (EntryRow row in summary.ManualReviewEntries
                         .Where(row => row.RuntimeEventWake)
                         .Take(40))
            {
                builder.AppendLine(
                    $"| `{row.AssetPath}` | {row.EntryIndex} | {row.DisplayTiming} | {row.ConditionTypes} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("소스 참조:");
        foreach (string reference in summary.RuntimeAfterDamageReferences)
        {
            builder.AppendLine($"- `{reference}`");
        }

        builder.AppendLine();
        builder.AppendLine("### 4.2 캐릭터 메커닉 / 시스템의 BattleEvent 직접 구독");
        builder.AppendLine();
        builder.AppendLine(
            "아래 목록은 `BattleEvent`를 직접 구독하는 코드다. " +
            "한 사건에서 두 구독자가 같은 DamageRequest를 만들지 G0 감사 대상이다.");
        builder.AppendLine();
        builder.AppendLine("| 파일 | 행 | 이벤트 |");
        builder.AppendLine("|---|---|---|");

        foreach (BattleEventSubscription subscription in summary.BattleEventSubscriptions)
        {
            builder.AppendLine(
                $"| `{subscription.Path}` | {subscription.Line} | `{subscription.EventName}` |");
        }

        builder.AppendLine();
        builder.AppendLine("### 4.3 공유 Effect Template");
        builder.AppendLine();

        if (summary.SharedTemplates.Count == 0)
        {
            builder.AppendLine("- 공유 템플릿 없음");
        }
        else
        {
            builder.AppendLine("| EffectDefinition GUID | Entry 수 | 사용 Skill 수 |");
            builder.AppendLine("|---|---|---|");

            foreach (SharedTemplateUsage usage in summary.SharedTemplates.Take(40))
            {
                builder.AppendLine(
                    $"| `{usage.EffectDefinitionGuid}` | {usage.EntryCount} | {usage.SkillCount} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 4.4 미참조 EffectDefinition");
        builder.AppendLine();

        if (summary.OrphanEffectDefinitions.Count == 0)
        {
            builder.AppendLine("- 없음");
        }
        else
        {
            foreach (EffectDefinitionInfo info in summary.OrphanEffectDefinitions.Take(60))
            {
                builder.AppendLine(
                    $"- `{info.Path}` ({info.TypeName})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 5. Missing Script / 중복 GUID");
        builder.AppendLine();

        if (summary.MissingScriptAssets.Count == 0)
        {
            builder.AppendLine("- Missing Script(FATAL): 없음");
        }
        else
        {
            foreach (string path in summary.MissingScriptAssets)
            {
                builder.AppendLine($"- Missing Script(FATAL): `{path}`");
            }
        }

        if (summary.MissingScriptWarnings.Count > 0)
        {
            builder.AppendLine(
                $"- Warning {summary.MissingScriptWarnings.Count}건: " +
                "`m_Script: {fileID: 0}` 이지만 `m_EditorClassIdentifier`가 채워진 " +
                "기존 직렬화 특성. 실제 로드는 정상이며 FATAL이 아니다.");
        }

        if (summary.DuplicateGuidGroups.Count == 0)
        {
            builder.AppendLine("- 중복 GUID: 없음");
        }
        else
        {
            foreach (DuplicateGuidGroup group in summary.DuplicateGuidGroups)
            {
                builder.AppendLine(
                    $"- 중복 GUID `{group.Guid}`: {string.Join(", ", group.Paths)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 6. G0-K — 캐릭터 전용 키워드 / 소스 / 상태 저장");
        builder.AppendLine();
        builder.AppendLine(
            "`StatusEffect` 계열 클래스에서 파생한 실행 상태를 소스 기준으로 실사했다. " +
            "`OwnerCharacterId=Shared` 이거나 안정 `KeywordId`가 없는 항목은 수동 검토 대상이다.");
        builder.AppendLine();
        builder.AppendLine("| 소유 | 키워드(안정 ID) | Carrier | Storage | ApplyPhase | RemovePolicy |");
        builder.AppendLine("|---|---|---|---|---|---|");

        foreach (G0KeywordRow row in summary.KeywordRows)
        {
            builder.AppendLine(
                $"| {row.OwnerCharacterId} | `{row.KeywordId}` | {row.CarrierScope} | " +
                $"{row.StorageKind} | {row.ApplyPhase} | {row.RemovePolicy} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"용어집 키워드 {summary.GlossaryKeywordCount}건, " +
            $"스킬 저작 키워드 Entry {summary.SkillKeywordEntryCount}건, " +
            $"커스텀 자원 정의 {summary.CombatResourceCount}건은 각 CSV 참조.");
        builder.AppendLine();
        builder.AppendLine("### 필수 키워드 커버리지 (문서 정본 대조)");
        builder.AppendLine();
        builder.AppendLine("| 키워드 | 소유(문서/코드) | Carrier | Storage | 상태 | 관측 식별자 |");
        builder.AppendLine("|---|---|---|---|---|---|");

        foreach (G0RequiredKeywordRow row in summary.RequiredKeywordRows)
        {
            builder.AppendLine(
                $"| `{row.KeywordId}` | {row.OwnerCharacterId} | {row.CarrierScope} | " +
                $"{row.StorageKind} | {row.CoverageStatus} | " +
                $"{row.ObservedIdentifiers.Replace("|", "\\|")} |");
        }

        builder.AppendLine();
        builder.AppendLine("### 미정/미구현 — 구현 차단 항목");
        builder.AppendLine();

        List<G0RequiredKeywordRow> undecided =
            summary.RequiredKeywordRows
                .Where(row => !string.IsNullOrEmpty(row.DecisionNote))
                .ToList();

        if (undecided.Count == 0)
        {
            builder.AppendLine("- 없음");
        }
        else
        {
            foreach (G0RequiredKeywordRow row in undecided)
            {
                builder.AppendLine(
                    $"- `{row.KeywordId}` ({row.CoverageStatus}) — {row.DecisionNote}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 수동 검토 대상(고유 키워드 안정 ID 없음)");
        builder.AppendLine();

        List<G0KeywordRow> keywordReview =
            summary.KeywordRows.Where(row => row.NeedsManualReview).ToList();

        if (keywordReview.Count == 0)
        {
            builder.AppendLine("- 없음");
        }
        else
        {
            foreach (G0KeywordRow row in keywordReview)
            {
                builder.AppendLine(
                    $"- `{row.KeywordId}` ({row.OwnerCharacterId}) — " +
                    "고유 키워드이지만 안정 ID(`UniqueKeywordId`)가 없어 구현 전 기획 확인 필요.");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 7. G0-I — 실행 구조 / 반격 / 연출 순서");
        builder.AppendLine();
        builder.AppendLine(
            "호출 그래프는 `ActionResolver → ClashManager → BattleEvent → Mechanic` " +
            "순서를 소스 간선으로 고정한 것이다. 실제 적용 시간과 UI 재생 시간은 분리해 " +
            "Golden Baseline의 Trace로 고정한다.");
        builder.AppendLine();
        builder.AppendLine("| 구분 | 간선 수 |");
        builder.AppendLine("|---|---|");

        foreach (IGrouping<string, G0CallGraphEdge> group in
                 summary.CallGraphEdges
                     .GroupBy(edge => edge.Category, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {group.Key} | {group.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("### 반격/추가타 종류");
        builder.AppendLine();
        builder.AppendLine("| 종류 | 출현 수 | 현재 코드 대응 | 관측 식별자 |");
        builder.AppendLine("|---|---|---|---|");

        foreach (G0CounterKindRow row in summary.CounterKinds)
        {
            builder.AppendLine(
                $"| {row.Kind} | {row.OccurrenceCount} | {row.MappingNote} | " +
                $"{row.ObservedIdentifiers.Replace("|", "\\|")} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "정본 구분: `HifumiCounterRoll`(합 종료 직후 반격 묶음), " +
            "`EnemyCounterRoll`(일방 피격 후 반격), `SecondaryHit`(추가타), " +
            "`FollowUpRoll`(후속 굴림), `TriggeredSkillAction`(R1-I 신규 삽입). " +
            "서로 다른 발생 시점을 하나의 '즉시' 이벤트로 합치지 않는다.");
        builder.AppendLine();
        builder.AppendLine("## 8. G0-W — 공격 가중치 기준선 / 보호 목록");
        builder.AppendLine();
        builder.AppendLine(
            "현재 `AttackWeightSettings.Weight`는 **메인 포함 서로 다른 캐릭터 수**다. " +
            "v4의 `AttackWeightBudget`(슬롯 예산)과 의미가 다르므로 Legacy 결과를 보존한 채 분리한다.");
        builder.AppendLine();
        builder.AppendLine("| SkillId | ActionType | Weight | SecondaryPartMode | 배율 | RollCount | 보호 |");
        builder.AppendLine("|---|---|---|---|---|---|---|");

        foreach (G0AttackWeightRow row in summary.AttackWeightRows
                     .Where(row => row.NeedsProtection)
                     .Take(80))
        {
            builder.AppendLine(
                $"| `{row.SkillId}` | {row.ActionType} | {row.Weight} | " +
                $"{row.SecondaryPartMode} | {row.SecondaryDamageMultiplier} | " +
                $"{row.RollCount} | {(row.NeedsProtection ? "O" : "-")} |");
        }

        if (summary.AttackWeightRows.All(row => !row.NeedsProtection))
        {
            builder.AppendLine("| - | - | 1 | - | - | - | - |");
        }

        builder.AppendLine();
        builder.AppendLine("### 보호 목록");
        builder.AppendLine();

        if (summary.AttackWeightProtectionRows.Count == 0)
        {
            builder.AppendLine("- 없음");
        }
        else
        {
            foreach (G0AttackWeightProtectionRow row
                     in summary.AttackWeightProtectionRows.Take(80))
            {
                builder.AppendLine(
                    $"- `{row.SkillId}` — {row.ProtectionReason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            $"`AttackWeight` 소스 참조 {summary.AttackWeightUiReferences.Count}건 " +
            "(UI 표시/자동 계획/Undo 갱신 여부는 G0-W 5항목에서 별도 확인).");
        builder.AppendLine();
        builder.AppendLine("## 9. G0 판정");
        builder.AppendLine();

        bool g0Pass =
            summary.SkillAssetCount > 0 &&
            summary.EntryCount > 0 &&
            summary.MissingScriptAssetCount == 0 &&
            summary.DuplicateGuidGroupCount == 0;

        builder.AppendLine(g0Pass
            ? "- **PASS** — 감사표 생성 완료, FATAL Missing Script/중복 GUID 없음."
            : "- **FAIL/중단** — FATAL Missing Script 또는 중복 GUID가 있어 현재 기준선을 확보할 수 없음.");
        builder.AppendLine();
        builder.AppendLine(
            $"- G0-K: {(summary.KeywordRowCount > 0 ? "PASS" : "FAIL")} " +
            $"(키워드/상태 행 {summary.KeywordRowCount}, 수동 검토 {summary.KeywordManualReviewCount})");
        builder.AppendLine(
            $"- G0-I: {(summary.CallGraphEdgeCount > 0 ? "PASS" : "FAIL")} " +
            $"(호출 간선 {summary.CallGraphEdgeCount}, 반격 종류 {summary.CounterKinds.Count})");
        builder.AppendLine(
            $"- G0-W: {(summary.AttackWeightRows.Count > 0 ? "PASS" : "FAIL")} " +
            $"(가중치 스킬 {summary.AttackWeightRows.Count}, 보호 {summary.AttackWeightProtectedSkillCount})");
        builder.AppendLine();
        builder.AppendLine("## 10. 산출물");
        builder.AppendLine();
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_AUDIT.json")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_ENTRIES.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_TRIGGER_STATS.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_K_KEYWORDS.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_K_REQUIRED.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_K_GLOSSARY.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_K_SKILL_KEYWORDS.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_K_RESOURCES.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_I_CALL_GRAPH.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_I_COUNTER_KINDS.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_W_ATTACK_WEIGHTS.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_W_PROTECTED.csv")}`");
        builder.AppendLine($"- `{PathCombine(outputDirectory, "G0_GOLDEN_BASELINE.json")}` (Play Mode 실행 시)");

        return builder.ToString();
    }

    private static string Csv(object value)
    {
        string text = value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static Dictionary<int, int> BuildHistogram(IEnumerable<int> values)
    {
        Dictionary<int, int> histogram = new();

        foreach (int value in values)
        {
            histogram.TryGetValue(value, out int count);
            histogram[value] = count + 1;
        }

        return histogram;
    }

    private static List<TimingCount> ToTimingCounts(
        Dictionary<int, int> histogram)
    {
        return histogram
            .OrderBy(pair => pair.Key)
            .Select(pair => new TimingCount
            {
                TimingValue = pair.Key,
                DisplayTiming = SkillEffectTimingCatalog.GetDisplayName(
                    (SkillEffectTiming)pair.Key),
                Count = pair.Value
            })
            .ToList();
    }

    private static Dictionary<string, int> BuildStringHistogram(
        IEnumerable<string> values)
    {
        Dictionary<string, int> histogram = new(StringComparer.Ordinal);

        foreach (string value in values)
        {
            string key = value ?? string.Empty;
            histogram.TryGetValue(key, out int count);
            histogram[key] = count + 1;
        }

        return histogram
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrEmpty(projectRoot))
            return absolutePath;

        string normalized = absolutePath.Replace('\\', '/');
        string root = projectRoot.Replace('\\', '/');

        return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(root.Length).TrimStart('/')
            : normalized;
    }

    private static string PathCombine(string directory, string file) =>
        Path.Combine(directory, file).Replace('\\', '/');

    private static string TryReadGitBranch()
    {
        string head = TryReadGitHead();

        if (head != null &&
            head.StartsWith("ref: refs/heads/", StringComparison.Ordinal))
        {
            return head.Substring("ref: refs/heads/".Length);
        }

        return head ?? "unknown";
    }

    private static string TryReadGitCommit()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrEmpty(projectRoot))
            return "unknown";

        string gitDirectory = Path.Combine(projectRoot, ".git");

        if (!Directory.Exists(gitDirectory) && !File.Exists(gitDirectory))
            return "unknown";

        string head = TryReadGitHead();

        if (head != null &&
            head.StartsWith("ref: ", StringComparison.Ordinal))
        {
            string reference = head.Substring(5);
            string referencePath = Path.Combine(
                gitDirectory,
                reference.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(referencePath))
                return File.ReadAllText(referencePath).Trim();
        }

        return head ?? "unknown";
    }

    private static string TryReadGitHead()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrEmpty(projectRoot))
            return null;

        string headPath = Path.Combine(projectRoot, ".git", "HEAD");

        if (!File.Exists(headPath))
            return null;

        return File.ReadAllText(headPath).Trim();
    }

    private static void Check(
        string label,
        bool condition,
        string detail = null)
    {
        if (condition)
        {
            pass++;
            Debug.Log($"{LogPrefix} PASS: {label}");
            return;
        }

        fail++;
        Debug.LogError(
            $"{LogPrefix} FAIL: {label}" +
            (string.IsNullOrEmpty(detail) ? string.Empty : $" ({detail})"));
    }

    //====================================================================
    // 데이터 모델
    //====================================================================

    [Serializable]
    private sealed class EntryRow
    {
        public string AssetPath;
        public string AssetGuid;
        public string SkillId;
        public string SkillName;
        public string ActionType;
        public bool IsDuel;
        public int EntryIndex;
        public int RollIndexOrNone;
        public string RollNumberRestriction;
        public string EffectDefinitionGuid;
        public string EffectDefinitionPath;
        public string LegacyOrNew;
        public string SourceKind;
        public int SerializedTimingValue;
        public int EffectiveTimingValue;
        public string DisplayTiming;
        public string ConditionTypes;
        public string TargetSelector;
        public string OperationType;
        public bool MayProduceDamage;
        public bool HasLifetimeState;
        public bool RuntimeEventWake;
        public bool NeedsManualReview;
    }

    [Serializable]
    private sealed class EffectDefinitionInfo
    {
        public string Path;
        public string Guid;
        public string TypeName;
        public string TargetSelector;
        public List<string> ConditionTypes;
        public bool MayProduceDamage;
        public bool HasLifetimeState;
    }

    [Serializable]
    private sealed class DuplicateGuidGroup
    {
        public string Guid;
        public List<string> Paths;
    }

    [Serializable]
    private sealed class BattleEventSubscription
    {
        public string Path;
        public int Line;
        public string EventName;
        public string Source;
    }

    [Serializable]
    private sealed class SharedTemplateUsage
    {
        public string EffectDefinitionGuid;
        public int EntryCount;
        public int SkillCount;
    }

    [Serializable]
    private sealed class DuplicateIdentity
    {
        public string Identity;
        public int Count;
    }

    [Serializable]
    private sealed class MissingScriptInfo
    {
        public string Path;
        public string Classification;
        public string EditorClassIdentifierSample;
    }

    [Serializable]
    private sealed class G0KeywordRow
    {
        public string KeywordId;
        public string ClassName;
        public string OwnerCharacterId;
        public string CarrierScope;
        public string StorageKind;
        public string ApplyPhase;
        public string EffectSource;
        public string RemovePolicy;
        public string OwnedRule;
        public string CurrentCodePath;
        public string SerializedRefs;
        public bool NeedsManualReview;
    }

    [Serializable]
    private sealed class G0RequiredKeywordRow
    {
        public string KeywordId;
        public string OwnerCharacterId;
        public string CarrierScope;
        public string StorageKind;
        public string CoverageStatus;
        public string ObservedIdentifiers;
        public string DecisionNote;
    }

    [Serializable]
    private sealed class G0KeywordGlossaryRow
    {
        public string Keyword;
        public string DescriptionPreview;
        public string Source;
    }

    [Serializable]
    private sealed class G0SkillKeywordRow
    {
        public string AssetPath;
        public string AssetGuid;
        public string SkillId;
        public int KeywordIndex;
        public string KeywordName;
        public string Description;
    }

    [Serializable]
    private sealed class G0CombatResourceRow
    {
        public string AssetPath;
        public string AssetGuid;
        public string CharacterName;
        public int Index;
        public string Key;
        public int InitialValue;
        public int MaxValue;
    }

    [Serializable]
    private sealed class G0CallGraphEdge
    {
        public string CallerFile;
        public string CallerMethod;
        public string Callee;
        public string Category;
        public int Line;
        public string Source;
    }

    [Serializable]
    private sealed class G0CounterKindRow
    {
        public string Kind;
        public int OccurrenceCount;
        public string MappingNote;
        public string ObservedIdentifiers;
        public List<string> SampleSites;
    }

    [Serializable]
    private sealed class G0AttackWeightRow
    {
        public string AssetPath;
        public string AssetGuid;
        public string SkillId;
        public string SkillName;
        public string ActionType;
        public int Weight;
        public string SecondaryPartMode;
        public float SecondaryDamageMultiplier;
        public bool AllowBrokenSecondaryParts;
        public int RollCount;
        public int EffectEntryCount;
        public int KeywordCount;
        public bool UsesAnotherPartOnPrimaryCharacter;
        public bool NeedsProtection;
    }

    [Serializable]
    private sealed class G0AttackWeightProtectionRow
    {
        public string AssetPath;
        public string SkillId;
        public string ActionType;
        public int Weight;
        public string SecondaryPartMode;
        public string ProtectionReason;
    }

    [Serializable]
    private sealed class AuditSummary
    {
        public string GeneratedAtUtc;
        public string UnityVersion;
        public string ProjectPath;
        public string Branch;
        public string Commit;
        public int SkillAssetCount;
        public int EntryCount;
        public int EffectDefinitionAssetCount;
        public int OrphanEffectDefinitionCount;
        public int EmotionAugmentAssetCount;
        public int MissingScriptAssetCount;
        public int MissingScriptWarningAssetCount;
        public int DuplicateGuidGroupCount;
        public int NeedsManualReviewCount;
        public int RuntimeEventWakeEntryCount;
        public int KeywordRowCount;
        public int KeywordManualReviewCount;
        public int RequiredKeywordCount;
        public int RequiredKeywordMissingCount;
        public int RequiredKeywordUndecidedCount;
        public int GlossaryKeywordCount;
        public int SkillKeywordEntryCount;
        public int CombatResourceCount;
        public int CallGraphEdgeCount;
        public int AttackWeightedSkillCount;
        public int AttackWeightProtectedSkillCount;
        public List<TimingCount> RawTimingHistogram;
        public List<TimingCount> EffectiveTimingHistogram;
        public List<ScopeCount> ScopeHistogram;
        public List<TriggerCount> CanonicalTriggerUsage;
        public List<string> MissingScriptAssets;
        public List<string> MissingScriptWarnings;
        public List<DuplicateGuidGroup> DuplicateGuidGroups;
        public List<BattleEventSubscription> BattleEventSubscriptions;
        public List<string> CanWakeOverrides;
        public List<string> RuntimeAfterDamageReferences;
        public List<EffectDefinitionInfo> OrphanEffectDefinitions;
        public List<SharedTemplateUsage> SharedTemplates;
        public List<DuplicateIdentity> DuplicateIdentities;
        public List<EntryRow> ManualReviewEntries;
        public List<G0KeywordRow> KeywordRows;
        public List<G0RequiredKeywordRow> RequiredKeywordRows;
        public List<G0KeywordGlossaryRow> GlossaryKeywords;
        public List<G0SkillKeywordRow> SkillKeywordEntries;
        public List<G0CombatResourceRow> CombatResources;
        public List<G0CallGraphEdge> CallGraphEdges;
        public List<G0CounterKindRow> CounterKinds;
        public List<G0AttackWeightRow> AttackWeightRows;
        public List<G0AttackWeightProtectionRow> AttackWeightProtectionRows;
        public List<string> AttackWeightUiReferences;
    }

    [Serializable]
    private sealed class TimingCount
    {
        public int TimingValue;
        public string DisplayTiming;
        public int Count;
    }

    [Serializable]
    private sealed class ScopeCount
    {
        public string Scope;
        public int Count;
    }

    [Serializable]
    private sealed class TriggerCount
    {
        public string Name;
        public int Count;
    }

    [Serializable]
    private sealed class G0TriggerStatRow
    {
        public string Scope;
        public int TimingValue;
        public string DisplayTiming;
        public int Count;
    }

    [Serializable]
    private sealed class G0GoldenScenarioResult
    {
        public string Id;
        public string Description;
        public string FirstActionType;
        public string SecondActionType;
        public int FirstRollCount;
        public int SecondRollCount;
        public bool IsClash;
        public int ExchangeCount;
        public int OneSidedHitCount;
        public int DamageEventCount;
        public int PositiveDamageEventCount;
        public int AttackWeight;
        public int PrimaryDamage;
        public int SecondaryHitCount;
        public int SecondaryDamageTotal;
        public List<string> HitTargets;
        public List<G0ProbeCount> ProbeCounts;
        public List<string> Trace;
    }

    [Serializable]
    private sealed class G0ProbeCount
    {
        public string Label;
        public int FirstOwnerCount;
        public int SecondOwnerCount;
    }

    [Serializable]
    private sealed class G0GoldenBaselineReport
    {
        public string GeneratedAtUtc;
        public string UnityVersion;
        public string Branch;
        public string Commit;
        public List<G0GoldenScenarioResult> Scenarios;
    }

    private static class G0GoldenScenario
    {
        public static void ResetRegistry()
        {
        }
    }
}

/// <summary>
/// G0 Golden Baseline 전용 탐침 효과.
/// 실제 게임 효과가 아니라 "몇 번 발동했는가"만 기록한다.
/// AfterDamage에서 추가타를 만들면 기존 구조의 재점화 횟수를 그대로 측정할 수 있다.
/// </summary>
public sealed class G0ProbeEffectDefinition : SkillEffectDefinition
{
    public string ProbeLabel;

    public static int SecondaryBudget;

    private static readonly Dictionary<string, int> ApplyCounts =
        new(StringComparer.Ordinal);

    private static readonly List<string> TraceLines = new();

    public static IReadOnlyList<string> Trace => TraceLines;

    public static void Reset()
    {
        ResetCounters();
        SecondaryBudget = 0;
    }

    public static void ResetCounters()
    {
        ApplyCounts.Clear();
        TraceLines.Clear();
    }

    public static int GetCount(string label)
    {
        return label != null &&
               ApplyCounts.TryGetValue(label, out int count)
            ? count
            : 0;
    }

    public override void Apply(SkillEffectContext context)
    {
        string key = string.IsNullOrEmpty(ProbeLabel)
            ? name
            : ProbeLabel;

        ApplyCounts.TryGetValue(key, out int count);
        ApplyCounts[key] = count + 1;

        TraceLines.Add(
            $"{key}@{(int)context.Timing}:roll={context.RollNumber}:" +
            $"clash={context.IsClash}:one={context.IsOneSided}:" +
            $"dmg={context.DamageResult?.AppliedDamage ?? 0}");

        if (!key.StartsWith("AfterDamage", StringComparison.Ordinal) ||
            SecondaryBudget <= 0 ||
            context.Action == null)
        {
            return;
        }

        // G0-G07: 원본 피해 처리 후 1회 검사에서 추가타를 예산만큼 한 번에 요청한다.
        // 추가타 자신은 AfterDamage를 다시 깨우지 않아야 한다(예산을 먼저 0으로 소진).
        int remaining = SecondaryBudget;
        SecondaryBudget = 0;

        for (int i = 0; i < remaining; i++)
        {
            context.ApplyDamage(
                DamageRequest.SkillPart(
                    context.Action,
                    1,
                    canBreakPart: false));
        }
    }
}
#endif
