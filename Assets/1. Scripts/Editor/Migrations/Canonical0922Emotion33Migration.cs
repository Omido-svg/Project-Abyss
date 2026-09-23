#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0923 정본 불일치 #2:
/// "확정"인데 TEMP_BALANCE_V1 proxy를 사용하던 감정 증강 33장을
/// 0922 canonical runtime effect로 교체한다.
/// 미정 카드는 건드리지 않고 기존 TEMP asset 자체도 삭제하지 않는다.
/// </summary>
public static class Canonical0922Emotion33Migration
{
    private const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private const string EffectRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0922/Effects/Emotion33";

    private sealed class Spec
    {
        public string Id;
        public string AssetName;
        public Canonical0922Emotion33EffectKind Kind;
        public int Amount;
        public int Secondary;
        public int Duration = 1;
        public int MaximumTotal;
        public float Ratio;
        public float Multiplier = 1f;
        public string Description;
    }

    private static readonly Spec[] Specs =
    {
        // 연민 6
        S("emotion.compassion.t1.02","Compassion_T1_02_ImmediateHeal",
            Canonical0922Emotion33EffectKind.CompassionImmediateHeal,40,
            d:"즉시 체력 +40."),
        S("emotion.compassion.t2.01","Compassion_T2_01_ExchangeWinRecovery",
            Canonical0922Emotion33EffectKind.CompassionExchangeWinRecovery,4,2,
            d:"이긴 교환마다 체력 +4 · 흐트러짐 +2."),
        S("emotion.compassion.t2.02","Compassion_T2_02_Lifesteal",
            Canonical0922Emotion33EffectKind.CompassionLifesteal,ratio:.10f,
            d:"준 체력 피해의 10%만큼 체력을 회복한다."),
        S("emotion.compassion.t2.03","Compassion_T2_03_LastStandRecovery",
            Canonical0922Emotion33EffectKind.CompassionLastStandRecovery,12,
            d:"짓눌림으로 턴 종료 시 체력 +12."),
        S("emotion.compassion.t3.01","Compassion_T3_01_HealingConversion",
            Canonical0922Emotion33EffectKind.CompassionHealingConversion,
            d:"이후 체력·흐트러짐 회복 대신 회복했을 양만큼 무작위 적 부위에 즉시 피해."),
        S("emotion.compassion.t3.02","Compassion_T3_02_OverhealBlock",
            Canonical0922Emotion33EffectKind.CompassionOverhealToBlock,max:50,
            d:"최대 체력을 넘긴 회복량만큼 방어도 획득. 전투당 최대 50."),

        // 신의 8
        S("emotion.faith.t1.01","Faith_T1_01_TurnStartBlock",
            Canonical0922Emotion33EffectKind.FaithTurnStartBlock,3,
            d:"턴 시작 시 방어도 +3."),
        S("emotion.faith.t1.02","Faith_T1_02_EnemyAttackSlotBlock",
            Canonical0922Emotion33EffectKind.FaithEnemyAttackSlotBlock,1,
            d:"턴 시작 시 살아 있는 적 공격 슬롯 1개당 방어도 +1."),
        S("emotion.faith.t1.03","Faith_T1_03_Reflection5",
            Canonical0922Emotion33EffectKind.FaithDamageReflection,ratio:.05f,
            d:"그 턴 받은 체력 피해(방어도 흡수 전)의 5%를 다음 턴 시작 시 방어도로 얻는다."),
        S("emotion.faith.t2.01","Faith_T2_01_EscalatingBlock",
            Canonical0922Emotion33EffectKind.FaithEscalatingBlockGain,2,
            d:"매 턴 방어도 획득량 보너스가 +2씩 증가한다."),
        S("emotion.faith.t2.02","Faith_T2_02_FirstClashNullify",
            Canonical0922Emotion33EffectKind.FaithFirstClashDamageNullify,
            d:"매 턴 1회, 처음 받는 합 교환의 체력 피해를 방어도 소모 없이 0으로 만든다."),
        S("emotion.faith.t2.03","Faith_T2_03_Reflection12",
            Canonical0922Emotion33EffectKind.FaithDamageReflection,ratio:.12f,
            d:"그 턴 받은 체력 피해(방어도 흡수 전)의 12%를 다음 턴 시작 시 방어도로 얻는다."),
        S("emotion.faith.t3.02","Faith_T3_02_BlockToHeal",
            Canonical0922Emotion33EffectKind.FaithTurnEndBlockToHeal,
            d:"턴 종료 시 그 턴 실제로 얻은 방어도만큼 체력을 회복한다."),
        S("emotion.faith.t3.03","Faith_T3_03_BlockConversion",
            Canonical0922Emotion33EffectKind.FaithBlockConversion,mult:3f,
            d:"이후 방어도를 얻지 않고, 얻었을 양의 3배를 무작위 적 부위에 즉시 피해."),

        // 초연 5
        S("emotion.detachment.t1.01","Detachment_T1_01_PartMaxHp",
            Canonical0922Emotion33EffectKind.DetachmentPartMaximumHp,ratio:.05f,
            d:"모든 부위 최대체력 +5%."),
        S("emotion.detachment.t1.02","Detachment_T1_02_StaggerMax",
            Canonical0922Emotion33EffectKind.DetachmentStaggerMaximum,50,
            d:"흐트러짐 최대치 +50."),
        S("emotion.detachment.t1.03","Detachment_T1_03_SingleWeakenedSuppress",
            Canonical0922Emotion33EffectKind.DetachmentSingleWeakenedPenaltySuppression,
            d:"약화 부위가 정확히 하나일 때 그 약화 페널티를 적용하지 않는다."),
        S("emotion.detachment.t2.01","Detachment_T2_01_KillMaxHpGrowth",
            Canonical0922Emotion33EffectKind.DetachmentKillMaximumHpGrowth,2,
            d:"적 처치마다 전체 최대체력 +2. 런 동안 지속하며 부위 최대체력은 변하지 않는다."),
        S("emotion.detachment.t2.02","Detachment_T2_02_DamageDeferral",
            Canonical0922Emotion33EffectKind.DetachmentDamageDeferral,
            d:"이번 턴 실제 체력 피해의 절반을 다음 턴 시작으로 미뤄 가장 체력이 높은 비파괴 부위에 적용한다."),

        // 경외 6
        S("emotion.awe.t1.01","Awe_T1_01_FirstTurnStrength",
            Canonical0922Emotion33EffectKind.AweFirstTurnStrength,2,duration:1,
            d:"획득 후 처음 시작하는 턴에 힘 2·1."),
        S("emotion.awe.t1.03","Awe_T1_03_NextTurnSwift",
            Canonical0922Emotion33EffectKind.AweNextTurnSwift,1,duration:3,
            d:"획득 후 다음 턴부터 신속 1·3."),
        S("emotion.awe.t2.01","Awe_T2_01_HeadInfiniteSpeed",
            Canonical0922Emotion33EffectKind.AweInfiniteHeadSpeed,1,
            d:"머리 슬롯은 합 속도 보정을 항상 상한(+2)으로 받는다. 처리 순서는 바꾸지 않는다. 최대 빛 -1."),
        S("emotion.awe.t2.02","Awe_T2_02_LastStandResponse",
            Canonical0922Emotion33EffectKind.AweLastStandNextTurnStrength,2,duration:1,
            d:"짓눌림으로 턴 종료 시 다음 턴 힘 2·1."),
        S("emotion.awe.t2.03","Awe_T2_03_OverwhelmTailwind",
            Canonical0922Emotion33EffectKind.AweOverwhelmNextTurnStrength,1,duration:1,
            d:"짓누름으로 턴 종료 시 다음 턴 힘 1·1."),
        S("emotion.awe.t3.03","Awe_T3_03_ArmsInfiniteSpeed",
            Canonical0922Emotion33EffectKind.AweInfiniteArmsSpeed,1,
            d:"왼팔·오른팔 슬롯은 합 속도 보정을 항상 상한(+2)으로 받는다. 처리 순서는 바꾸지 않는다. 최대 빛 -1."),

        // 동경 6
        S("emotion.admiration.t1.01","Admiration_T1_01_HitPrestige",
            Canonical0922Emotion33EffectKind.AdmirationHitPrestige,1,
            d:"내가 이긴 합 교환마다 위세 충전 +1."),
        S("emotion.admiration.t1.02","Admiration_T1_02_TakenPrestige",
            Canonical0922Emotion33EffectKind.AdmirationTakenPrestige,1,
            d:"내가 진 합 교환마다 위세 충전 +1."),
        S("emotion.admiration.t1.03","Admiration_T1_03_ClashPrestige",
            Canonical0922Emotion33EffectKind.AdmirationClashPrestige,2,
            d:"합이 시작될 때마다 위세 충전 +2."),
        S("emotion.admiration.t2.03","Admiration_T2_03_ElapsedClashPrestige",
            Canonical0922Emotion33EffectKind.AdmirationElapsedClashPrestige,6,
            d:"획득 후 경과 턴 ×6만큼 합 시작 시 위세 충전을 추가한다."),
        S("emotion.admiration.t3.01","Admiration_T3_01_PrestigeStockpile",
            Canonical0922Emotion33EffectKind.AdmirationPrestigeStockpile,2,
            d:"위세 게이지 최대치를 현재 발동 임계의 2배로 확장하며, 발동 시 임계만큼만 소비한다."),
        S("emotion.admiration.t3.02","Admiration_T3_02_StaggerExchange",
            Canonical0922Emotion33EffectKind.AdmirationPrestigeFromStagger,
            d:"턴 시작 시 위세가 발동 임계보다 부족하면 부족분만큼 내 흐트러짐을 지불해 위세를 채운다."),

        // 그리움 2
        S("emotion.longing.t1.03","Longing_T1_03_NeutralThreshold40",
            Canonical0922Emotion33EffectKind.LongingNeutralThreshold,40,
            d:"내 기세 판정에서 우세 임계를 +30에서 +40으로 바꿔 중립 범위를 넓힌다."),
        S("emotion.longing.t2.03","Longing_T2_03_NeutralEnergy",
            Canonical0922Emotion33EffectKind.LongingNeutralTurnEndEnergy,1,
            d:"턴 종료 시 내 기세가 중립이면 빛 +1.")
    };

    [MenuItem("Game System Verification/0922 Canonical/Emotion 33 - Apply Canonical Runtime Patch")]
    public static void ApplyFromMenu()
    {
        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (catalog?.Entries == null)
        {
            Debug.LogError("[0923 Emotion33] EmotionAugmentCatalog missing.");
            return;
        }

        EnsureFolders();

        Dictionary<string, EmotionAugmentDefinition> byId =
            catalog.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AugmentId))
                .GroupBy(x => x.AugmentId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int touched = 0;
        List<string> missing = new();

        foreach (Spec spec in Specs)
        {
            if (!byId.TryGetValue(spec.Id, out EmotionAugmentDefinition card) ||
                card == null)
            {
                missing.Add(spec.Id);
                continue;
            }

            Canonical0922Emotion33EffectDefinition effect =
                GetOrCreate(spec.AssetName);

            effect.Kind = spec.Kind;
            effect.Amount = spec.Amount;
            effect.SecondaryAmount = spec.Secondary;
            effect.Duration = spec.Duration;
            effect.MaximumTotal = spec.MaximumTotal;
            effect.Ratio = spec.Ratio;
            effect.Multiplier = spec.Multiplier;

            card.Effects ??= new List<EmotionAugmentEffectDefinition>();
            card.Effects.Clear();
            card.Effects.Add(effect);
            card.Description = spec.Description;
            card.DesignStatus = "확정";
            card.RuntimeReadiness =
                EmotionAugmentRuntimeReadiness.RuntimeConnected;
            card.RuntimeNote =
                "[0923 Emotion33] 0922 canonical runtime effect connected; TEMP_BALANCE_V1 removed.";

            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(card);
            touched++;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (missing.Count > 0)
        {
            Debug.LogError(
                $"[0923 Emotion33] Migration PARTIAL. Touched={touched}/33, " +
                $"Missing={string.Join(", ", missing)}");
            return;
        }

        Debug.Log(
            $"[0923 Emotion33] Migration applied. Touched={touched}/33. " +
            "확정 33장만 canonical effect로 교체했으며 미정 TEMP proxy asset은 보존했습니다. " +
            "이제 Emotion 33 - Verify Canonical Runtime Patch를 실행하세요.");
    }

    private static Spec S(
        string id,
        string assetName,
        Canonical0922Emotion33EffectKind kind,
        int amount = 0,
        int secondary = 0,
        int duration = 1,
        int max = 0,
        float ratio = 0f,
        float mult = 1f,
        string d = "")
    {
        return new Spec
        {
            Id = id,
            AssetName = assetName,
            Kind = kind,
            Amount = amount,
            Secondary = secondary,
            Duration = duration,
            MaximumTotal = max,
            Ratio = ratio,
            Multiplier = mult,
            Description = d
        };
    }

    private static Canonical0922Emotion33EffectDefinition
        GetOrCreate(string assetName)
    {
        string path = $"{EffectRoot}/{assetName}.asset";
        Canonical0922Emotion33EffectDefinition effect =
            AssetDatabase.LoadAssetAtPath<Canonical0922Emotion33EffectDefinition>(path);

        if (effect != null)
            return effect;

        effect =
            ScriptableObject.CreateInstance<Canonical0922Emotion33EffectDefinition>();
        effect.name = assetName;
        AssetDatabase.CreateAsset(effect, path);
        return effect;
    }

    private static void EnsureFolders()
    {
        EnsureFolder(
            "Assets/2. Data/Progression/EmotionAugments",
            "Canonical0922");
        EnsureFolder(
            "Assets/2. Data/Progression/EmotionAugments/Canonical0922",
            "Effects");
        EnsureFolder(
            "Assets/2. Data/Progression/EmotionAugments/Canonical0922/Effects",
            "Emotion33");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
