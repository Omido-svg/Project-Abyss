#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gameplay v5 감정 증강 authoring bootstrap.
///
/// 현재는 경외(Awe) 9개 프로토타입만 생성한다.
/// 이후 다른 감정 증강은 EmotionAugmentDefinition 에셋을 Root 아래에 추가하고
/// Rebuild Catalog 메뉴를 실행하면 코드 수정 없이 Catalog에 편입된다.
/// </summary>
public static class GameplayV5EmotionAugmentMigration
{
    private const string Root =
        "Assets/2. Data/Progression/EmotionAugments";
    private const string CatalogPath =
        Root + "/EmotionAugmentCatalog.asset";

    private readonly struct PrototypeSpec
    {
        public readonly string Id;
        public readonly string FileName;
        public readonly string DisplayName;
        public readonly int Tier;
        public readonly string Description;
        public readonly int GainEnergy;
        public readonly int GainPrestige;
        public readonly float RecoverStaggerRatio;
        public readonly int RollBonus;
        public readonly float DamageDealtMultiplier;
        public readonly float DamageTakenMultiplier;
        public readonly int PositiveMomentumBonus;
        public readonly float PrestigeGainMultiplier;

        public PrototypeSpec(
            string id,
            string fileName,
            string displayName,
            int tier,
            string description,
            int gainEnergy = 0,
            int gainPrestige = 0,
            float recoverStaggerRatio = 0f,
            int rollBonus = 0,
            float damageDealtMultiplier = 1f,
            float damageTakenMultiplier = 1f,
            int positiveMomentumBonus = 0,
            float prestigeGainMultiplier = 1f)
        {
            Id = id;
            FileName = fileName;
            DisplayName = displayName;
            Tier = tier;
            Description = description;
            GainEnergy = gainEnergy;
            GainPrestige = gainPrestige;
            RecoverStaggerRatio = recoverStaggerRatio;
            RollBonus = rollBonus;
            DamageDealtMultiplier = damageDealtMultiplier;
            DamageTakenMultiplier = damageTakenMultiplier;
            PositiveMomentumBonus = positiveMomentumBonus;
            PrestigeGainMultiplier = prestigeGainMultiplier;
        }
    }

    private static readonly PrototypeSpec[] AwePrototypes =
    {
        new PrototypeSpec(
            "AWE_PROTO_T1_01",
            "Awe_T1_01_SolemnFirstStep.asset",
            "엄숙한 첫걸음",
            1,
            "[프로토타입] 전투 동안 모든 굴림 결과에 +1.",
            rollBonus: 1),

        new PrototypeSpec(
            "AWE_PROTO_T1_02",
            "Awe_T1_02_Afterglow.asset",
            "경외의 여운",
            1,
            "[프로토타입] 선택 즉시 위세를 20 획득한다.",
            gainPrestige: 20),

        new PrototypeSpec(
            "AWE_PROTO_T1_03",
            "Awe_T1_03_UnshakenGaze.asset",
            "흔들림 없는 시선",
            1,
            "[프로토타입] 선택 즉시 최대 흐트러짐의 20%를 회복한다.",
            recoverStaggerRatio: 0.20f),

        new PrototypeSpec(
            "AWE_PROTO_T2_01",
            "Awe_T2_01_SublimePressure.asset",
            "숭고한 압력",
            2,
            "[프로토타입] 전투 동안 주는 피해가 10% 증가한다.",
            damageDealtMultiplier: 1.10f),

        new PrototypeSpec(
            "AWE_PROTO_T2_02",
            "Awe_T2_02_SanctuaryEcho.asset",
            "성역의 잔향",
            2,
            "[프로토타입] 전투 동안 받는 피해가 10% 감소한다.",
            damageTakenMultiplier: 0.90f),

        new PrototypeSpec(
            "AWE_PROTO_T2_03",
            "Awe_T2_03_WaveOfMajesty.asset",
            "위엄의 파동",
            2,
            "[프로토타입] 자신에게 유리한 기세 이동량이 추가로 +5 증가한다.",
            positiveMomentumBonus: 5),

        new PrototypeSpec(
            "AWE_PROTO_T3_01",
            "Awe_T3_01_OverwhelmingManifestation.asset",
            "압도적 현현",
            3,
            "[프로토타입] 전투 동안 모든 굴림 결과에 +2.",
            rollBonus: 2),

        new PrototypeSpec(
            "AWE_PROTO_T3_02",
            "Awe_T3_02_AbsoluteAwe.asset",
            "절대적 경외",
            3,
            "[프로토타입] 전투 동안 주는 피해가 20% 증가한다.",
            damageDealtMultiplier: 1.20f),

        new PrototypeSpec(
            "AWE_PROTO_T3_03",
            "Awe_T3_03_CompletedSanctuary.asset",
            "성역의 완성",
            3,
            "[프로토타입] 전투 동안 받는 피해가 20% 감소하고 위세 획득량이 50% 증가한다.",
            damageTakenMultiplier: 0.80f,
            prestigeGainMultiplier: 1.50f)
    };

    [MenuItem(
        "Project Abyss/Gameplay v5/Apply Emotion Augment Prototype")]
    public static void ApplyPrototype()
    {
        EmotionAugmentCatalog catalog =
            EnsurePrototypeAssets();

        Debug.Log(
            "[Gameplay v5 Emotion Augment] 경외 9개 프로토타입 생성/갱신 완료. " +
            $"Catalog={AssetDatabase.GetAssetPath(catalog)}");

        GameplayV5UiMigration.Apply();
    }

    [MenuItem(
        "Project Abyss/Gameplay v5/Rebuild Emotion Augment Catalog")]
    public static void RebuildCatalogMenu()
    {
        EmotionAugmentCatalog catalog =
            FindOrCreateCatalog(null);

        MergeDefinitionsUnderRoot(catalog);
        LogCatalogValidation(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[Gameplay v5 Emotion Augment] Catalog rebuild complete.");
    }

    public static EmotionAugmentCatalog EnsurePrototypeAssets(
        EmotionAugmentCatalog preferredCatalog = null)
    {
        EnsureFolderPath(Root);

        EmotionAugmentCatalog catalog =
            FindOrCreateCatalog(preferredCatalog);

        for (int i = 0; i < AwePrototypes.Length; i++)
            EnsureAwePrototype(catalog, AwePrototypes[i]);

        MergeDefinitionsUnderRoot(catalog);
        LogCatalogValidation(catalog);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return catalog;
    }

    private static EmotionAugmentCatalog FindOrCreateCatalog(
        EmotionAugmentCatalog preferredCatalog)
    {
        if (preferredCatalog != null)
            return preferredCatalog;

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                CatalogPath);

        if (catalog != null)
            return catalog;

        string firstGuid = AssetDatabase
            .FindAssets("t:EmotionAugmentCatalog")
            .OrderBy(value => value)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstGuid))
        {
            string firstPath =
                AssetDatabase.GUIDToAssetPath(firstGuid);

            catalog =
                AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                    firstPath);

            if (catalog != null)
                return catalog;
        }

        EnsureFolderPath(Root);
        catalog =
            ScriptableObject.CreateInstance<EmotionAugmentCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static void EnsureAwePrototype(
        EmotionAugmentCatalog catalog,
        PrototypeSpec spec)
    {
        string tierFolder =
            $"{Root}/Awe/Tier{Mathf.Clamp(spec.Tier, 1, 3)}";

        EnsureFolderPath(tierFolder);

        string path =
            $"{tierFolder}/{spec.FileName}";

        EmotionAugmentDefinition definition =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentDefinition>(
                path);

        if (definition == null)
        {
            definition =
                ScriptableObject.CreateInstance<EmotionAugmentDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(
            definition,
            "Gameplay v5 Awe Augment Prototype");

        definition.AugmentId = spec.Id;
        definition.DisplayName = spec.DisplayName;
        definition.Emotion = EmotionType.Awe;
        definition.Tier = Mathf.Clamp(spec.Tier, 1, 3);
        definition.OfferWeight = 1f;
        definition.Description = spec.Description;
        definition.Effects ??=
            new List<EmotionAugmentEffectDefinition>();

        EmotionAugmentPrototypeEffectDefinition effect =
            definition.Effects
                .OfType<EmotionAugmentPrototypeEffectDefinition>()
                .FirstOrDefault();

        if (effect == null)
        {
            effect =
                ScriptableObject.CreateInstance<
                    EmotionAugmentPrototypeEffectDefinition>();
            effect.name = "PrototypeEffect";
            AssetDatabase.AddObjectToAsset(effect, definition);
            definition.Effects.Add(effect);
        }

        Undo.RecordObject(
            effect,
            "Gameplay v5 Awe Augment Prototype Effect");

        effect.GainEnergy = spec.GainEnergy;
        effect.GainPrestige = spec.GainPrestige;
        effect.RecoverStaggerRatio = spec.RecoverStaggerRatio;
        effect.RollBonus = spec.RollBonus;
        effect.DamageDealtMultiplier = spec.DamageDealtMultiplier;
        effect.DamageTakenMultiplier = spec.DamageTakenMultiplier;
        effect.PositiveMomentumBonus = spec.PositiveMomentumBonus;
        effect.PrestigeGainMultiplier = spec.PrestigeGainMultiplier;

        EditorUtility.SetDirty(effect);
        EditorUtility.SetDirty(definition);

        if (catalog.Entries == null)
            catalog.Entries = new List<EmotionAugmentDefinition>();

        if (!catalog.Entries.Contains(definition))
            catalog.Entries.Add(definition);
    }

    private static void MergeDefinitionsUnderRoot(
        EmotionAugmentCatalog catalog)
    {
        if (catalog == null)
            return;

        catalog.Entries ??=
            new List<EmotionAugmentDefinition>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:EmotionAugmentDefinition",
                new[] { Root });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            EmotionAugmentDefinition definition =
                AssetDatabase.LoadAssetAtPath<EmotionAugmentDefinition>(
                    path);

            if (definition != null &&
                !catalog.Entries.Contains(definition))
            {
                catalog.Entries.Add(definition);
            }
        }

        catalog.Entries.RemoveAll(
            entry => entry == null);

        catalog.Entries = catalog.Entries
            .Distinct()
            .OrderBy(entry => entry.Emotion)
            .ThenBy(entry => entry.Tier)
            .ThenBy(entry => entry.AugmentId)
            .ToList();

        EditorUtility.SetDirty(catalog);
    }

    private static void LogCatalogValidation(
        EmotionAugmentCatalog catalog)
    {
        if (catalog == null)
            return;

        foreach (EmotionType emotion
                 in System.Enum.GetValues(typeof(EmotionType)))
        {
            for (int tier = 1; tier <= 3; tier++)
            {
                int count =
                    catalog.GetCandidateCount(emotion, tier);

                if (emotion == EmotionType.Awe && count != 3)
                {
                    Debug.LogWarning(
                        $"[EmotionAugment Catalog] 경외 Tier {tier}는 " +
                        $"정확히 3개가 권장되지만 현재 {count}개입니다.");
                }
            }
        }
    }

    private static void EnsureFolderPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            return;

        string current = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
#endif
