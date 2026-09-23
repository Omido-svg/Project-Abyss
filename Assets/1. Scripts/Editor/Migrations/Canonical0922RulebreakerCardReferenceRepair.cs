#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0923 Core export 기반 Rulebreaker 카드 ObjectReference 복구.
///
/// 현재 Core 상태:
/// - 11개의 EmotionRulebreakerEffectDefinition SO 자체는 정상 로드됨.
/// - 아래 10개 EmotionAugmentDefinition의 SOURCE_RAW_TEXT에는 올바른 GUID가 남아 있음.
/// - 그러나 SerializedObject의 Effects.Array.data[n]은 NULL로 로드됨.
///
/// 따라서 raw YAML GUID를 다시 쓰는 대신:
/// 1) Effect SO를 강제 재임포트
/// 2) 카드 SO를 강제 재임포트
/// 3) 카드.Effects를 실제 Effect 객체로 다시 할당
/// 4) SaveAssets
/// 5) 카드를 다시 강제 재임포트
/// 6) live ObjectReference를 재검증
///
/// 미정 카드/TEMP proxy 및 Emotion33 33장에는 손대지 않는다.
/// </summary>
public static class Canonical0922RulebreakerCardReferenceRepair
{
    private const string MenuRoot =
        "Game System Verification/0922 Canonical/";

    private sealed class Binding
    {
        public readonly string AugmentId;
        public readonly string DisplayName;
        public readonly string CardPath;
        public readonly string[] EffectPaths;

        public Binding(
            string augmentId,
            string displayName,
            string cardPath,
            params string[] effectPaths)
        {
            AugmentId = augmentId;
            DisplayName = displayName;
            CardPath = cardPath;
            EffectPaths = effectPaths ?? Array.Empty<string>();
        }
    }

    // 2026-09-23 21:30 KST Core export에서 실제 GUID/경로를 역추적한 10장.
    // 경외 T3 제물(공간)만 Effect 2개이므로 총 11개 Effect 참조다.
    private static readonly Binding[] Bindings =
    {
        new Binding(
            "emotion.compassion.t3.03",
            "연민 T3 부위 재생",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Compassion/Tier3/Compassion_T3_03.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Compassion/Tier3/Effects/T3_03_regenerate.asset"),

        new Binding(
            "emotion.detachment.t2.03",
            "초연 T2 슬롯 유지",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier2/Detachment_T2_03.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier2/Effects/T2_03_preserve_slot.asset"),

        new Binding(
            "emotion.detachment.t3.01",
            "초연 T3 평정",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier3/Detachment_T3_01.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier3/Effects/T3_01_hp_resistance_1.asset"),

        new Binding(
            "emotion.detachment.t3.02",
            "초연 T3 신의 육체",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier3/Detachment_T3_02.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Detachment/Tier3/Effects/T3_02_no_stagger.asset"),

        new Binding(
            "emotion.awe.t3.02",
            "경외 T3 제물(공간)",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Awe/Tier3/Awe_T3_02.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Awe/Tier3/Effects/T3_02_head_slot_plus_1.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Awe/Tier3/Effects/T3_02_seal_legs.asset"),

        new Binding(
            "emotion.longing.t2.01",
            "그리움 T2 유지",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier2/Longing_T2_01.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier2/Effects/T2_01_carry_positive.asset"),

        new Binding(
            "emotion.longing.t2.02",
            "그리움 T2 잔류",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier2/Longing_T2_02.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier2/Effects/T2_02_carry_negative.asset"),

        new Binding(
            "emotion.longing.t3.01",
            "그리움 T3 왕귀(짓누름)",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Longing_T3_01.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Effects/T3_01_overwhelm_ascension.asset"),

        new Binding(
            "emotion.longing.t3.02",
            "그리움 T3 왕귀(짓눌림)",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Longing_T3_02.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Effects/T3_02_laststand_revive.asset"),

        new Binding(
            "emotion.longing.t3.03",
            "그리움 T3 왕귀(중립)",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Longing_T3_03.asset",
            "Assets/2. Data/Progression/EmotionAugments/Canonical0916/Longing/Tier3/Effects/T3_03_balance_repeat.asset")
    };

    [MenuItem(
        MenuRoot +
        "Emotion 50 - Repair Rulebreaker Card References")]
    public static void ApplyFromMenu()
    {
        List<string> failures = new List<string>();

        // 1) 먼저 실제 Effect SO를 현재 script/type 정보로 동기화한다.
        foreach (Binding binding in Bindings)
        {
            foreach (string effectPath in binding.EffectPaths)
            {
                AssetDatabase.ImportAsset(
                    effectPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);

                EmotionRulebreakerEffectDefinition effect =
                    AssetDatabase.LoadAssetAtPath
                        <EmotionRulebreakerEffectDefinition>(
                            effectPath);

                if (effect == null)
                {
                    failures.Add(
                        $"{binding.AugmentId}: Effect load failed: " +
                        effectPath);
                }
            }
        }

        if (failures.Count > 0)
        {
            PrintFailure(failures);
            return;
        }

        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);

        int touchedCards = 0;
        int assignedReferences = 0;

        // 2) 카드 SO의 typed Effects 리스트를 실제 객체로 재배선한다.
        foreach (Binding binding in Bindings)
        {
            AssetDatabase.ImportAsset(
                binding.CardPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);

            EmotionAugmentDefinition card =
                AssetDatabase.LoadAssetAtPath
                    <EmotionAugmentDefinition>(
                        binding.CardPath);

            if (card == null)
            {
                failures.Add(
                    $"{binding.AugmentId}: Card load failed: " +
                    binding.CardPath);
                continue;
            }

            if (!string.Equals(
                    card.AugmentId,
                    binding.AugmentId,
                    StringComparison.Ordinal))
            {
                failures.Add(
                    $"{binding.CardPath}: AugmentId mismatch. " +
                    $"actual={card.AugmentId}, " +
                    $"expected={binding.AugmentId}");
                continue;
            }

            List<EmotionAugmentEffectDefinition> expected =
                new List<EmotionAugmentEffectDefinition>();

            bool effectFailure = false;

            foreach (string effectPath in binding.EffectPaths)
            {
                EmotionRulebreakerEffectDefinition effect =
                    AssetDatabase.LoadAssetAtPath
                        <EmotionRulebreakerEffectDefinition>(
                            effectPath);

                if (effect == null)
                {
                    failures.Add(
                        $"{binding.AugmentId}: " +
                        $"Effect became null: {effectPath}");
                    effectFailure = true;
                    break;
                }

                expected.Add(effect);
            }

            if (effectFailure)
                continue;

            Undo.RecordObject(
                card,
                "Repair 0922 Rulebreaker emotion references");

            if (card.Effects == null)
            {
                card.Effects =
                    new List<EmotionAugmentEffectDefinition>();
            }
            else
            {
                card.Effects.Clear();
            }

            foreach (EmotionAugmentEffectDefinition effect
                     in expected)
            {
                card.Effects.Add(effect);
                assignedReferences++;
            }

            EditorUtility.SetDirty(card);
            touchedCards++;
        }

        AssetDatabase.SaveAssets();

        // 3) 저장된 카드 파일 자체를 다시 import하여
        //    디스크 -> Unity ObjectReference round-trip을 강제로 확인한다.
        foreach (Binding binding in Bindings)
        {
            AssetDatabase.ImportAsset(
                binding.CardPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);

        // 4) 최종 live-reference verification.
        int verifiedCards = 0;
        int verifiedReferences = 0;

        foreach (Binding binding in Bindings)
        {
            EmotionAugmentDefinition card =
                AssetDatabase.LoadAssetAtPath
                    <EmotionAugmentDefinition>(
                        binding.CardPath);

            if (card == null)
            {
                failures.Add(
                    $"{binding.AugmentId}: Card null after reimport.");
                continue;
            }

            if (card.Effects == null ||
                card.Effects.Count !=
                binding.EffectPaths.Length)
            {
                failures.Add(
                    $"{binding.AugmentId}: Effects count=" +
                    $"{card.Effects?.Count ?? -1}, expected=" +
                    binding.EffectPaths.Length);
                continue;
            }

            bool cardPassed = true;

            for (int i = 0;
                 i < binding.EffectPaths.Length;
                 i++)
            {
                EmotionAugmentEffectDefinition effect =
                    card.Effects[i];

                if (effect == null)
                {
                    failures.Add(
                        $"{binding.AugmentId}: " +
                        $"Effects[{i}] is still NULL.");
                    cardPassed = false;
                    continue;
                }

                if (!(effect is
                        EmotionRulebreakerEffectDefinition))
                {
                    failures.Add(
                        $"{binding.AugmentId}: " +
                        $"Effects[{i}] type=" +
                        $"{effect.GetType().Name}, expected " +
                        "EmotionRulebreakerEffectDefinition.");
                    cardPassed = false;
                    continue;
                }

                string actualPath =
                    AssetDatabase.GetAssetPath(effect);

                if (!string.Equals(
                        actualPath,
                        binding.EffectPaths[i],
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{binding.AugmentId}: " +
                        $"Effects[{i}] path={actualPath}, expected=" +
                        binding.EffectPaths[i]);
                    cardPassed = false;
                    continue;
                }

                verifiedReferences++;
            }

            if (cardPassed)
                verifiedCards++;
        }

        if (failures.Count > 0)
        {
            PrintFailure(failures);
            return;
        }

        Debug.Log(
            "[0923 Rulebreaker Card Reference Repair] PASS. " +
            $"TouchedCards={touchedCards}/10, " +
            $"AssignedRefs={assignedReferences}/11, " +
            $"VerifiedCards={verifiedCards}/10, " +
            $"VerifiedRefs={verifiedReferences}/11. " +
            "카드 SO의 live Effects ObjectReference를 " +
            "실제 Rulebreaker SO로 재저장했습니다.");
    }

    private static void PrintFailure(
        IReadOnlyList<string> failures)
    {
        Debug.LogError(
            "[0923 Rulebreaker Card Reference Repair] FAIL\n- " +
            string.Join("\n- ", failures));
    }
}
#endif
