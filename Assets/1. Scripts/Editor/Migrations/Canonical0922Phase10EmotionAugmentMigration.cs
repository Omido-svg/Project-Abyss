#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase10EmotionAugmentMigration
{
    public const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    public const string CanonicalRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";

    public const string EffectRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0922/Effects";

    public const string RegistryPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";

    [MenuItem("Game System Verification/0922 Canonical/Phase 10 - Apply Emotion Augment Migration")]
    public static void ApplyFromMenu()
    {
        if (!Canonical0917EmotionAugmentMigration.ApplyCanonicalRoster(out string rosterReport))
        {
            Debug.LogError(
                "[0922 Phase10] 63-card roster repair failed.\n" + rosterReport);
            return;
        }

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (catalog?.Entries == null)
        {
            Debug.LogError("[0922 Phase10] EmotionAugmentCatalog missing.");
            return;
        }

        EnsureFolders();

        Dictionary<string, EmotionAugmentDefinition> byId =
            catalog.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AugmentId))
                .GroupBy(x => x.AugmentId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int touched = 0;
        touched += ConfigureCompassionRegen(byId);
        touched += ConfigureAweGrace(byId);
        touched += ConfigureImpressionGranary(byId);
        touched += ConfigureAdmirationRupture(byId);
        touched += ConfigureAdmirationConversion(byId);
        touched += ClearPendingAweSacrificeTime(byId);

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(RegistryPath);

        bool registryWired = false;
        if (registry != null && registry.EmotionAugmentCatalog != catalog)
        {
            registry.EmotionAugmentCatalog = catalog;
            EditorUtility.SetDirty(registry);
            registryWired = true;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[0922 Phase10] Migration applied. Catalog={catalog.Entries.Count(x => x != null)}, " +
            $"CanonicalEffectsTouched={touched}, RegistryWired={registryWired}. " +
            "(미정)은 PENDING_CANONICAL로 보존됩니다. 이제 Phase 10 Verify를 실행하세요.");
    }

    private static int ConfigureCompassionRegen(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        int touched = 0;

        if (TryGet(byId, "emotion.compassion.t1.01", out EmotionAugmentDefinition hp))
        {
            EmotionAugmentCanonical0922EffectDefinition effect =
                GetOrCreateEffect("Compassion_T1_01_TimedHpRecovery");
            effect.Kind = EmotionAugment0922EffectKind.TimedHpRecovery;
            effect.Turns = 3;
            effect.Amount = 8;
            SetSingleEffect(hp, effect,
                "3턴간 턴 종료 시 체력 +8. 증강 고유 지속효과이며 공용 상태이상 재생이 아니다.");
            touched++;
        }

        if (TryGet(byId, "emotion.compassion.t1.03", out EmotionAugmentDefinition stagger))
        {
            EmotionAugmentCanonical0922EffectDefinition effect =
                GetOrCreateEffect("Compassion_T1_03_TimedStaggerRecovery");
            effect.Kind = EmotionAugment0922EffectKind.TimedStaggerRecovery;
            effect.Turns = 3;
            effect.Amount = 5;
            SetSingleEffect(stagger, effect,
                "3턴간 턴 종료 시 흐트러짐 +5. 증강 고유 지속효과이며 공용 상태이상 재생이 아니다.");
            touched++;
        }

        return touched;
    }

    private static int ConfigureAweGrace(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        if (!TryGet(byId, "emotion.awe.t1.02", out EmotionAugmentDefinition card))
            return 0;

        EmotionAugmentCanonical0922EffectDefinition effect =
            GetOrCreateEffect("Awe_T1_02_Grace_StrengthInfinite");
        effect.Kind = EmotionAugment0922EffectKind.TurnStartGuardStrength;
        effect.PrimaryStatus = StatusEffectId.Strength;
        effect.PrimaryValue = 1;
        effect.PrimaryDuration = StatusEffect.InfiniteDuration;
        effect.HasSecondaryStatus = false;
        effect.BlockPrestige = false;

        SetSingleEffect(card, effect,
            "매 턴 시작 시 가호의 힘 1·∞ 활성화. 그 턴 교환에서 처음 지면 가호 출처의 힘 1만 제거. 다음 턴 재활성화.");
        return 1;
    }

    private static int ConfigureImpressionGranary(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        if (!TryGet(byId, "emotion.impression.t3.01", out EmotionAugmentDefinition card))
            return 0;

        EmotionAugmentCanonical0922EffectDefinition effect =
            GetOrCreateEffect("Impression_T3_01_Granary_StrengthInfinite");
        effect.Kind = EmotionAugment0922EffectKind.GoldThresholdStrength;
        effect.PrimaryStatus = StatusEffectId.Strength;
        effect.PrimaryValue = 1;
        effect.PrimaryDuration = StatusEffect.InfiniteDuration;
        effect.GoldThreshold = 500;
        effect.HasSecondaryStatus = false;
        effect.BlockPrestige = false;

        SetSingleEffect(card, effect,
            "보유 골드가 500 이상인 동안 곳간 출처의 힘 1·∞. 500 미만이면 그 Entry만 제거하며 다른 힘은 건드리지 않는다.");
        return 1;
    }

    private static int ConfigureAdmirationRupture(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        if (!TryGet(byId, "emotion.admiration.t2.01", out EmotionAugmentDefinition card))
            return 0;

        EmotionAugmentCanonical0922EffectDefinition effect =
            GetOrCreateEffect("Admiration_T2_01_Prestige_Rupture3Infinite");
        effect.Kind = EmotionAugment0922EffectKind.PrestigeTargetRuptureInfinite;
        effect.PrimaryStatus = StatusEffectId.Rupture;
        effect.PrimaryValue = 3;
        effect.PrimaryDuration = StatusEffect.InfiniteDuration;
        effect.HasSecondaryStatus = false;
        effect.BlockPrestige = false;

        SetSingleEffect(card, effect,
            "위세를 발동할 때마다 대상에게 균열 3·∞ 부여. 발동마다 독립 Entry이며 자연 감쇠하지 않는다.");
        return 1;
    }

    private static int ConfigureAdmirationConversion(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        if (!TryGet(byId, "emotion.admiration.t3.03", out EmotionAugmentDefinition card))
            return 0;

        EmotionAugmentCanonical0922EffectDefinition effect =
            GetOrCreateEffect("Admiration_T3_03_Conversion_StrengthSwiftInfinite");
        effect.Kind = EmotionAugment0922EffectKind.PermanentSelfStatus;
        effect.PrimaryStatus = StatusEffectId.Strength;
        effect.PrimaryValue = 1;
        effect.PrimaryDuration = StatusEffect.InfiniteDuration;
        effect.HasSecondaryStatus = true;
        effect.SecondaryStatus = StatusEffectId.Swift;
        effect.SecondaryValue = 1;
        effect.SecondaryDuration = StatusEffect.InfiniteDuration;
        effect.BlockPrestige = true;

        SetSingleEffect(card, effect,
            "위세를 더 이상 사용하지 못하고 힘 1·∞ · 신속 1·∞를 실제 공용 상태 Entry로 얻는다. 자연 감쇠하지 않는다.");
        return 1;
    }

    private static int ClearPendingAweSacrificeTime(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId)
    {
        if (!TryGet(byId, "emotion.awe.t3.01", out EmotionAugmentDefinition card))
            return 0;

        // 0922에서 Strength N의 N이 아직 (미정)이다. 구 TEMP proxy를 canonical로
        // 승격하지 않으며, 잘못된 고정 수치가 실행되지 않도록 효과 연결을 비운다.
        card.Effects ??= new List<EmotionAugmentEffectDefinition>();
        card.Effects.Clear();
        card.Description =
            "힘 N·∞ (N 미정). 3턴 뒤부터 매 턴 약화되지 않은 부위 전부 약화. 미정 수치는 런타임 생성하지 않음.";
        card.DesignStatus = "(미정 포함)";
        card.RuntimeReadiness = EmotionAugmentRuntimeReadiness.DesignValuePending;
        card.RuntimeNote = "[0922 Phase10] Strength N 미정. TEMP proxy 제거 / PENDING_CANONICAL.";
        EditorUtility.SetDirty(card);
        return 1;
    }

    private static void SetSingleEffect(
        EmotionAugmentDefinition card,
        EmotionAugmentCanonical0922EffectDefinition effect,
        string description)
    {
        card.Effects ??= new List<EmotionAugmentEffectDefinition>();
        card.Effects.Clear();
        card.Effects.Add(effect);
        card.Description = description;
        card.DesignStatus = "확정";
        card.RuntimeReadiness = EmotionAugmentRuntimeReadiness.RuntimeConnected;
        card.RuntimeNote = "[0922 Phase10] Canonical runtime effect connected.";
        EditorUtility.SetDirty(card);
        EditorUtility.SetDirty(effect);
    }

    private static EmotionAugmentCanonical0922EffectDefinition
        GetOrCreateEffect(string fileName)
    {
        string path = $"{EffectRoot}/{fileName}.asset";
        EmotionAugmentCanonical0922EffectDefinition effect =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCanonical0922EffectDefinition>(path);

        if (effect != null)
            return effect;

        effect = ScriptableObject.CreateInstance<EmotionAugmentCanonical0922EffectDefinition>();
        effect.name = fileName;
        AssetDatabase.CreateAsset(effect, path);
        return effect;
    }

    private static bool TryGet(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId,
        string id,
        out EmotionAugmentDefinition definition)
    {
        definition = null;
        return byId != null &&
               byId.TryGetValue(id, out definition) &&
               definition != null;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/2. Data/Progression/EmotionAugments", "Canonical0922");
        EnsureFolder("Assets/2. Data/Progression/EmotionAugments/Canonical0922", "Effects");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
