#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Duel 연출을 2 HitFrame 계약으로 교정한다.
///
/// 핵심:
/// - Duel Visual ExpectedHitFrameCount를 최소 2로 설정
/// - HitDamageWeights를 최소 2개 양수 가중치로 보정
/// - 피해를 두 HitFrame에 분배
/// - BloodSplash Cue는 OnHitFrame + OncePerHitIndex
/// - 현재 HitFrame 피해가 0이어도 행동 전체 실제 피해가 있으면 VFX 허용
/// </summary>
[InitializeOnLoad]
public static class BattleVfxImpactTimingMigration
{
    private const string MenuPath =
        "Tools/Project Abyss/Battle Visual/Fix Duel Two Hit VFX";

    private static bool autoRunScheduled;

    static BattleVfxImpactTimingMigration()
    {
        ScheduleAutoRun();
    }

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        MigrationResult result =
            FixAllDuelVisuals(logWhenUnchanged: true);

        Debug.Log(
            "[BattleVfxImpactTimingMigration] Duel 2타 교정 완료 / " +
            $"Visual={result.ChangedVisualCount}, " +
            $"Cue={result.ChangedCueCount}");
    }

    private static void ScheduleAutoRun()
    {
        if (autoRunScheduled)
            return;

        autoRunScheduled = true;
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        autoRunScheduled = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        FixAllDuelVisuals(logWhenUnchanged: false);
    }

    private static MigrationResult FixAllDuelVisuals(
        bool logWhenUnchanged)
    {
        HashSet<SkillVisualDefinition> duelVisuals =
            CollectDuelVisuals();

        string[] guids =
            AssetDatabase.FindAssets("t:SkillVisualDefinition");

        MigrationResult result = new MigrationResult();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            SkillVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(path);

            if (visual == null ||
                !IsDuelVisual(visual, duelVisuals))
            {
                continue;
            }

            bool assetChanged = false;

            if (visual.ExpectedHitFrameCount < 2)
            {
                visual.ExpectedHitFrameCount = 2;
                assetChanged = true;
            }

            if (!visual.DistributeDamageByHitCount)
            {
                visual.DistributeDamageByHitCount = true;
                assetChanged = true;
            }

            assetChanged |= EnsurePositiveHitWeights(
                visual,
                visual.ExpectedHitFrameCount);

            if (visual.VfxCues != null)
            {
                for (int i = 0; i < visual.VfxCues.Count; i++)
                {
                    BattleVfxCue cue = visual.VfxCues[i];

                    if (cue == null || !IsBloodSplash(cue.Vfx))
                        continue;

                    bool cueChanged = false;

                    cueChanged |= SetIfDifferent(
                        ref cue.Timing,
                        BattleVfxTiming.OnHitFrame);

                    cueChanged |= SetIfDifferent(
                        ref cue.RepeatMode,
                        BattleVfxCueRepeatMode.OncePerHitIndex);

                    cueChanged |= SetIfDifferent(
                        ref cue.AnchorType,
                        BattleVfxAnchorType.TargetBodyPart);

                    if (cue.UseHitIndexFilter)
                    {
                        cue.UseHitIndexFilter = false;
                        cueChanged = true;
                    }

                    if (cue.HitIndex != 0)
                    {
                        cue.HitIndex = 0;
                        cueChanged = true;
                    }

                    if (!Mathf.Approximately(cue.Delay, 0f))
                    {
                        cue.Delay = 0f;
                        cueChanged = true;
                    }

                    // 2타 중 한 타의 분배 피해가 0이더라도
                    // 행동 전체 피해가 실제로 발생했다면 두 HitFrame 모두 VFX를 낸다.
                    if (cue.RequirePositiveDamage)
                    {
                        cue.RequirePositiveDamage = false;
                        cueChanged = true;
                    }

                    if (!cue.RequirePositiveResolvedDamage)
                    {
                        cue.RequirePositiveResolvedDamage = true;
                        cueChanged = true;
                    }

                    if (string.IsNullOrWhiteSpace(cue.CueKey))
                    {
                        cue.CueKey = "Impact.BloodSplash";
                        cueChanged = true;
                    }

                    if (!cueChanged)
                        continue;

                    assetChanged = true;
                    result.ChangedCueCount++;

                    Debug.Log(
                        "[BattleVfxImpactTimingMigration] Duel BloodSplash 수정 / " +
                        $"Visual={visual.name}, CueIndex={i}, " +
                        $"ExpectedHits={visual.ExpectedHitFrameCount}, " +
                        "Repeat=OncePerHitIndex, " +
                        "RequirePositiveResolvedDamage=True",
                        visual);
                }
            }

            if (!assetChanged)
                continue;

            result.ChangedVisualCount++;
            EditorUtility.SetDirty(visual);
        }

        if (result.ChangedVisualCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else if (logWhenUnchanged)
        {
            Debug.Log(
                "[BattleVfxImpactTimingMigration] " +
                "수정할 Duel Visual이 없습니다. 이미 2타 설정이 적용됐는지 확인하세요.");
        }

        return result;
    }

    private static bool EnsurePositiveHitWeights(
        SkillVisualDefinition visual,
        int expectedHitCount)
    {
        if (visual == null)
            return false;

        bool changed = false;
        int count = Mathf.Max(2, expectedHitCount);

        if (visual.HitDamageWeights == null)
        {
            visual.HitDamageWeights = new List<int>();
            changed = true;
        }

        while (visual.HitDamageWeights.Count < count)
        {
            visual.HitDamageWeights.Add(1);
            changed = true;
        }

        for (int i = 0; i < count; i++)
        {
            if (visual.HitDamageWeights[i] > 0)
                continue;

            visual.HitDamageWeights[i] = 1;
            changed = true;
        }

        return changed;
    }

    private static HashSet<SkillVisualDefinition> CollectDuelVisuals()
    {
        HashSet<SkillVisualDefinition> result =
            new HashSet<SkillVisualDefinition>();

        string[] profileGuids =
            AssetDatabase.FindAssets("t:SkillVisualProfile");

        foreach (string guid in profileGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            SkillVisualProfile profile =
                AssetDatabase.LoadAssetAtPath<SkillVisualProfile>(path);

            if (profile?.DuelVisual != null)
                result.Add(profile.DuelVisual);
        }

        return result;
    }

    private static bool IsDuelVisual(
        SkillVisualDefinition visual,
        HashSet<SkillVisualDefinition> duelVisuals)
    {
        if (visual == null)
            return false;

        if (duelVisuals != null && duelVisuals.Contains(visual))
            return true;

        return visual.name.IndexOf(
            "Duel",
            StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsBloodSplash(
        BattleVfxDefinition definition)
    {
        if (definition == null)
            return false;

        if (ContainsBloodSplash(definition.name))
            return true;

        return definition.EffectPrefab != null &&
               ContainsBloodSplash(definition.EffectPrefab.name);
    }

    private static bool ContainsBloodSplash(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(
                   "BloodSplash",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SetIfDifferent<T>(
        ref T current,
        T target)
        where T : struct, Enum
    {
        if (current.Equals(target))
            return false;

        current = target;
        return true;
    }

    private struct MigrationResult
    {
        public int ChangedVisualCount;
        public int ChangedCueCount;
    }
}
#endif
