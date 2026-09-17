#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase E-1 확정 콘텐츠 이관.
/// 0916 MD/XLSX에서 근거가 있는 데이터만 생성/갱신하고,
/// 기본/강화1/강화2 역산이나 아이템 실제 명칭처럼 소스가 비어 있는 항목은 Pending으로 남긴다.
/// </summary>
[InitializeOnLoad]
public static class PhaseEContentMigration
{
    public const string ManifestPath =
        "Assets/2. Data/Progression/PhaseE/PhaseEContentManifest.asset";
    public const string EnemyCatalogPath =
        "Assets/2. Data/Progression/PhaseE/PhaseEEnemyContentCatalog.asset";
    public const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";
    public const string CanonicalEmotionRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";
    public const string RunItemCatalogPath =
        "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset";

    private const string PokerFacePath =
        "Assets/2. Data/Characters/Design2026/Hifumi/Skills/포커페이스.asset";

    private const string BossAPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_A_Duel.asset";
    private const string BossBPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Duel.asset";
    private const string BossFallbackPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Fallback.asset";
    private const string BossPhase1Path =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Phase01.asset";
    private const string BossDataPath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Data.asset";
    private const string BossBundlePath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Bundle.asset";

    private sealed class EmotionRow
    {
        public readonly EmotionType Emotion;
        public readonly int Tier;
        public readonly int Index;
        public readonly string Name;
        public readonly string Description;
        public readonly string Status;

        public EmotionRow(
            EmotionType emotion,
            int tier,
            int index,
            string name,
            string description,
            string status)
        {
            Emotion = emotion;
            Tier = tier;
            Index = index;
            Name = name;
            Description = description;
            Status = status;
        }
    }

    private static readonly EmotionRow[] EmotionRows =
    {
        new EmotionRow(EmotionType.Compassion, 1, 1, "재생", "3턴간 턴 종료 시 체력 +8", "확정"),
        new EmotionRow(EmotionType.Compassion, 1, 2, "즉발 회복", "즉시 체력 +40", "확정"),
        new EmotionRow(EmotionType.Compassion, 1, 3, "흐트러짐 재생", "3턴간 턴 종료 시 흐트러짐 +5", "확정"),
        new EmotionRow(EmotionType.Compassion, 2, 1, "교환 승리 회복", "이긴 교환마다 체력 +4 · 흐트러짐 +2", "확정"),
        new EmotionRow(EmotionType.Compassion, 2, 2, "흡혈", "준 피해의 10%만큼 체력 회복", "확정"),
        new EmotionRow(EmotionType.Compassion, 2, 3, "열세 회복", "짓눌림 구간에서 턴이 끝나면 체력 +12", "확정"),
        new EmotionRow(EmotionType.Compassion, 3, 1, "전환", "이후 회복하지 않고 회복했을 양만큼 무작위 적 부위에 즉시 피해", "확정"),
        new EmotionRow(EmotionType.Compassion, 3, 2, "오버힐", "최대치를 넘긴 회복량만큼 방어도 (전투당 최대 50)", "확정"),
        new EmotionRow(EmotionType.Compassion, 3, 3, "부위 재생", "파괴된 부위 1곳을 약화 상태로 재생 · 다시 파괴될 수 있음", "확정"),
        new EmotionRow(EmotionType.Faith, 1, 1, "기본", "턴 시작 시 방어도 +3", "확정"),
        new EmotionRow(EmotionType.Faith, 1, 2, "자세 대응", "적 공격 슬롯 1개당 방어도 +1", "확정"),
        new EmotionRow(EmotionType.Faith, 1, 3, "반사", "그 턴 받은 피해(흡수 전)의 5%를 다음 턴 시작 시 방어도로", "확정"),
        new EmotionRow(EmotionType.Faith, 2, 1, "누적", "매 턴 방어도 획득량 +2씩 증가", "확정"),
        new EmotionRow(EmotionType.Faith, 2, 2, "완전 방어", "매 턴 1회 그 턴 첫 교환 피해를 방어도 소모 없이 무효화", "확정"),
        new EmotionRow(EmotionType.Faith, 2, 3, "반사(상위)", "받은 피해의 12%를 다음 턴 방어도로", "확정"),
        new EmotionRow(EmotionType.Faith, 3, 1, "복구", "방어도가 소모될 때마다 33% 확률로 그 수치만큼 복구", "임시"),
        new EmotionRow(EmotionType.Faith, 3, 2, "회복 파생", "턴 종료 시 그 턴에 얻은 방어도만큼 체력 회복", "확정"),
        new EmotionRow(EmotionType.Faith, 3, 3, "전환", "이후 방어도를 얻지 않고 얻었을 양의 3배를 무작위 적 부위에 피해", "확정"),
        new EmotionRow(EmotionType.Detachment, 1, 1, "체력 그릇", "부위 최대체력 +5%", "확정"),
        new EmotionRow(EmotionType.Detachment, 1, 2, "자세 그릇", "흐트러짐 최대치 +50", "확정"),
        new EmotionRow(EmotionType.Detachment, 1, 3, "약화 완화", "약화된 부위가 하나일 때는 페널티 없음", "확정"),
        new EmotionRow(EmotionType.Detachment, 2, 1, "처치 성장", "적 처치마다 전체 최대체력 +2 (런 지속 · 부위는 안 오름)", "확정"),
        new EmotionRow(EmotionType.Detachment, 2, 2, "나눠 받기", "이번 턴 피해의 절반을 다음 턴 체력이 가장 많은 부위로 미룸 (전투 끝나면 소멸)", "확정"),
        new EmotionRow(EmotionType.Detachment, 2, 3, "슬롯 유지", "부위가 파괴되어도 슬롯을 잃지 않음 (전투당 1회)", "확정"),
        new EmotionRow(EmotionType.Detachment, 3, 1, "평정", "내 체력 내성이 전부 1.0", "확정"),
        new EmotionRow(EmotionType.Detachment, 3, 2, "신의 육체", "내 흐트러짐 게이지가 사라진다", "확정"),
        new EmotionRow(EmotionType.Detachment, 3, 3, "몸을 놓는다", "최대체력 −10% · 이번 전투 최대 빛 +1 · 획득 즉시 빛 최대 회복", "임시(−10%)"),
        new EmotionRow(EmotionType.Impression, 1, 1, "사례", "전투 종료 시 골드 +N", "값 미정"),
        new EmotionRow(EmotionType.Impression, 1, 2, "속결", "5턴 안에 이기면 골드 +N", "값 미정"),
        new EmotionRow(EmotionType.Impression, 1, 3, "사두다", "골드를 내고 방어도 +N/턴", "값 미정"),
        new EmotionRow(EmotionType.Impression, 2, 1, "삯", "처치 1체당 골드 +N", "값 미정"),
        new EmotionRow(EmotionType.Impression, 2, 2, "개평", "이긴 교환마다 골드 +N", "값 미정"),
        new EmotionRow(EmotionType.Impression, 2, 3, "방패값", "맞을 때마다 골드를 내고 방어도 +N", "값 미정"),
        new EmotionRow(EmotionType.Impression, 3, 1, "곳간", "보유 골드 500당 힘 +1 (상한 1)", "확정"),
        new EmotionRow(EmotionType.Impression, 3, 2, "낙수", "전투 종료 시 8% 확률로 아이템 3중 1", "임시(8%)"),
        new EmotionRow(EmotionType.Impression, 3, 3, "거둠", "처치 시 확률로 증강 (전투 종료 후 획득)", "확률 미정"),
        new EmotionRow(EmotionType.Awe, 1, 1, "강림", "획득 후 첫 턴 힘 +2", "확정"),
        new EmotionRow(EmotionType.Awe, 1, 2, "가호", "힘 +1, 그 턴 교환에 지면 잃음 (매 턴 갱신)", "확정"),
        new EmotionRow(EmotionType.Awe, 1, 3, "걸음(가칭)", "획득 후 3턴간 신속 +1", "확정"),
        new EmotionRow(EmotionType.Awe, 2, 1, "머리 무한", "머리 슬롯 속도 무한 ← 최대 빛 −1", "확정"),
        new EmotionRow(EmotionType.Awe, 2, 2, "응답", "짓눌림 구간에서 턴이 끝나면 다음 턴 힘 +2", "확정"),
        new EmotionRow(EmotionType.Awe, 2, 3, "순풍(가칭)", "짓누름 구간에서 턴이 끝나면 다음 턴 힘 +1", "확정"),
        new EmotionRow(EmotionType.Awe, 3, 1, "제물(시간)", "힘 +3 ← 3턴 뒤부터 매 턴 약화되지 않은 부위 전부 약화", "임시(+3)"),
        new EmotionRow(EmotionType.Awe, 3, 2, "제물(공간)", "머리 슬롯 +1칸(공격 · 도사림 겸용) → 공격 4칸 ← 다리 봉인", "확정"),
        new EmotionRow(EmotionType.Awe, 3, 3, "제물(자원)", "팔 두 슬롯 속도 무한 ← 최대 빛 −1", "확정"),
        new EmotionRow(EmotionType.Admiration, 1, 1, "때림", "내가 때린 교환마다 위세 충전 +1", "확정"),
        new EmotionRow(EmotionType.Admiration, 1, 2, "맞음", "내가 맞은 교환마다 위세 충전 +1", "확정"),
        new EmotionRow(EmotionType.Admiration, 1, 3, "합", "합이 붙을 때마다 위세 충전 +2", "확정"),
        new EmotionRow(EmotionType.Admiration, 2, 1, "균열", "위세 발동 시 적에게 균열 +3 (전투 동안 유지)", "확정"),
        new EmotionRow(EmotionType.Admiration, 2, 2, "즉시 풀 충전", "얻는 순간 위세 게이지 +50", "확정"),
        new EmotionRow(EmotionType.Admiration, 2, 3, "누적", "얻은 뒤 경과 턴 수 × 6만큼 합 시작 충전 증가", "확정"),
        new EmotionRow(EmotionType.Admiration, 3, 1, "비축", "게이지 최대치가 임계의 2배 — 2회분 비축", "확정"),
        new EmotionRow(EmotionType.Admiration, 3, 2, "환전", "매 턴 시작 시 게이지가 임계에 모자라면 그만큼 내 흐트러짐에서 당겨옴", "확정"),
        new EmotionRow(EmotionType.Admiration, 3, 3, "전환", "위세를 못 쓰고 동경 1 · 2티어도 없는 것으로 취급 · 대신 힘 +1 · 신속 +1 전투 동안 영구", "확정"),
        new EmotionRow(EmotionType.Longing, 1, 1, "몰이", "짓누름 고조 충전 +3 / 중립 · 우세 −3", "임시(±3)"),
        new EmotionRow(EmotionType.Longing, 1, 2, "버팀", "짓눌림 · 열세 고조 충전 +3 / 짓누름 −3", "임시(±3)"),
        new EmotionRow(EmotionType.Longing, 1, 3, "저울(가칭)", "중립 구간 확장 (우세 임계 30 → 40)", "확정"),
        new EmotionRow(EmotionType.Longing, 2, 1, "유지", "짓누름(양수) 바가 다음 턴으로 이월", "확정"),
        new EmotionRow(EmotionType.Longing, 2, 2, "잔류", "짓눌림(음수) 바가 다음 턴으로 이월", "확정"),
        new EmotionRow(EmotionType.Longing, 2, 3, "호흡(가칭)", "턴 종료 시 바가 중립이면 빛 +1", "확정"),
        new EmotionRow(EmotionType.Longing, 3, 1, "왕귀(짓누름)", "연속 짓누름 2턴 → 무조건 파괴(죽음의 저항 해제) + 적 체력 내성 전부 2.0", "확정"),
        new EmotionRow(EmotionType.Longing, 3, 2, "왕귀(짓눌림)", "연속 짓눌림 2턴 → 부활 (전체 체력 30% · 모든 부위 약화 · 전투당 1회)", "확정"),
        new EmotionRow(EmotionType.Longing, 3, 3, "왕귀(중립)", "중립 3턴 → 되풀이하는 턴 ([해결] 단계만 내 슬롯 한 번 더 · 턴 효과는 한 번씩 · 빛은 다시 냄)", "확정"),
    };

    static PhaseEContentMigration()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Project Abyss/Migrations/Phase E/Apply Confirmed Content Migration")]
    public static void ApplyFromMenu()
    {
        Apply();
        Debug.Log(
            "[Phase E TEMP_BALANCE_V1] content migration applied. " +
            "Open Game System Verification > Phase E Data Gate for PASS/PENDING details.");
    }

    private static void ApplyIfNeeded()
    {
        PhaseEContentManifest existing =
            AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(ManifestPath);

        if (existing != null &&
            existing.SchemaVersion >= PhaseEContentManifest.CurrentSchemaVersion)
        {
            return;
        }

        Apply();
    }

    public static void Apply()
    {
        EnsureFolder("Assets/2. Data/Progression/PhaseE");
        EnsureFolder(CanonicalEmotionRoot);
        EnsureFolder("Assets/2. Data/Progression/RunItems");

        PhaseEEnemyContentCatalog enemyCatalog = MigrateEnemyCatalog();
        RepairLegacyPrototypeEffectAssets();
        EmotionAugmentCatalog emotionCatalog = MigrateEmotionCatalog();
        RunItemCatalog itemCatalog = EnsureAsset<RunItemCatalog>(RunItemCatalogPath);

        bool pokerFaceSynced = MigratePokerFace();
        MigrateStage1Boss();

        PhaseEContentManifest manifest =
            EnsureAsset<PhaseEContentManifest>(ManifestPath);

        manifest.SchemaVersion = PhaseEContentManifest.CurrentSchemaVersion;
        manifest.EnemyContent = enemyCatalog;
        manifest.EmotionCatalog = emotionCatalog;
        manifest.RunItemCatalog = itemCatalog;
        manifest.ExpectedEmotionCount = 63;
        manifest.ExpectedCommonItemCount = 30;
        manifest.ExpectedClassItemCountPerCharacter = 10;
        manifest.ExpectedPlayableCharacterCount = 3;
        manifest.HifumiPokerFaceSynced = pokerFaceSynced;

        // 0916 원본의 미정 구간을 정본으로 위장하지 않고 별도 TEMP_BALANCE_V1
        // 프로필로 격리한다. 이 단계가 C-32/C-33/C-38/O-02/Y-06을 임시 폐쇄한다.
        PhaseETempBalanceMigration.Apply(emotionCatalog, itemCatalog, manifest);

        // TEMP 데이터가 생성된 직후 0916(3)에서 확정된 캐릭터 metadata/rider를 닫는다.
        // 정본 (미정) 값은 Closure에서도 채우지 않는다.
        Canonical0916ClosureMigration.Apply();

        AuditRollTextures(manifest);

        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
    }

    private static PhaseEEnemyContentCatalog MigrateEnemyCatalog()
    {
        PhaseEEnemyContentCatalog catalog =
            EnsureAsset<PhaseEEnemyContentCatalog>(EnemyCatalogPath);

        catalog.NormalEncounters ??= new List<CanonicalNormalEnemyEncounterProfile>();
        catalog.NormalEncounters.Clear();

        for (int stage = 1; stage <= 3; stage++)
        {
            AddNormalEncounter(catalog, stage, 3, 135);
            AddNormalEncounter(catalog, stage, 4, 101);
        }

        catalog.BossPartCount = 5;
        catalog.BossPartHp = 150;
        catalog.BossWholeHp = 1500;
        catalog.BossStaggerMax = 400;
        catalog.BossStartingEnergy = 2;
        catalog.NormalPostureSequence = "A,A,B";
        catalog.RandomTarget = true;
        catalog.FallbackBToNormalWhenEnergyInsufficient = true;
        catalog.SkillA = new CanonicalBossSkillProfile
        {
            SkillKey = "A",
            RollCount = 3,
            BasePower = 13,
            Duel = true,
            CanBreakPart = false
        };
        catalog.SkillB = new CanonicalBossSkillProfile
        {
            SkillKey = "B",
            RollCount = 2,
            BasePower = 14,
            Duel = true,
            CanBreakPart = false
        };

        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AddNormalEncounter(
        PhaseEEnemyContentCatalog catalog,
        int stage,
        int enemyCount,
        int hp)
    {
        CanonicalNormalEnemyEncounterProfile profile = new()
        {
            Stage = stage,
            EnemyCount = enemyCount,
            HpPerEnemy = hp,
            StaggerMax = 100,
            ActionSlotsPerEnemy = 1,
            RollsPerAction = 3
        };

        switch (stage)
        {
            case 1:
                profile.WeakTypeCount = 1;
                profile.NeutralTypeCount = 2;
                profile.ResistantTypeCount = 0;
                break;
            case 2:
                profile.WeakTypeCount = 1;
                profile.NeutralTypeCount = 1;
                profile.ResistantTypeCount = 1;
                break;
            default:
                profile.WeakTypeCount = 0;
                profile.NeutralTypeCount = 2;
                profile.ResistantTypeCount = 1;
                break;
        }

        catalog.NormalEncounters.Add(profile);
    }

    private static void RepairLegacyPrototypeEffectAssets()
    {
        // Phase E-1 v1 serialized EmotionAugmentPrototypeEffectDefinition from
        // EmotionAugmentPrototypeEffects.cs. v2 moved the ScriptableObject to a
        // same-name file, so the v1 asset can still point at the old MonoScript GUID.
        // Only this migration-owned canonical path used the prototype definition.
        string legacyPrototypePath =
            $@"{CanonicalEmotionRoot}/Admiration/Tier2/Effects/T2_02_Prestige50.asset";

        string existingGuid = AssetDatabase.AssetPathToGUID(legacyPrototypePath);
        EmotionAugmentPrototypeEffectDefinition typed =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentPrototypeEffectDefinition>(
                legacyPrototypePath);

        if (!string.IsNullOrWhiteSpace(existingGuid) && typed == null)
        {
            Debug.LogWarning(
                $"[Phase E-1 v3] Recreating legacy prototype effect script reference: {legacyPrototypePath}");
            AssetDatabase.DeleteAsset(legacyPrototypePath);
        }
    }

    private static EmotionAugmentCatalog MigrateEmotionCatalog()
    {
        EmotionAugmentCatalog catalog =
            EnsureAsset<EmotionAugmentCatalog>(EmotionCatalogPath);
        catalog.Entries ??= new List<EmotionAugmentDefinition>();
        catalog.Entries.Clear();

        foreach (EmotionRow row in EmotionRows)
        {
            string emotionName = row.Emotion.ToString();
            string folder =
                $@"{CanonicalEmotionRoot}/{emotionName}/Tier{row.Tier}";
            EnsureFolder(folder);

            string assetPath =
                $@"{folder}/{emotionName}_T{row.Tier}_{row.Index:00}.asset";

            EmotionAugmentDefinition definition =
                EnsureAsset<EmotionAugmentDefinition>(assetPath);

            definition.AugmentId =
                $"emotion.{emotionName.ToLowerInvariant()}.t{row.Tier}.{row.Index:00}";
            definition.DisplayName = row.Name;
            definition.Emotion = row.Emotion;
            definition.Tier = row.Tier;
            definition.OfferWeight = 1f;
            definition.Description = row.Description;
            definition.DesignStatus = row.Status;
            definition.Effects ??= new List<EmotionAugmentEffectDefinition>();
            definition.Effects.Clear();

            ConfigureEmotionRuntime(row, definition, folder);

            EditorUtility.SetDirty(definition);
            catalog.Entries.Add(definition);
        }

        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void ConfigureEmotionRuntime(
        EmotionRow row,
        EmotionAugmentDefinition definition,
        string folder)
    {
        string key = row.Emotion + ":" + row.Name;
        definition.RuntimeReadiness =
            IsDesignValuePending(row.Status)
                ? EmotionAugmentRuntimeReadiness.DesignValuePending
                : EmotionAugmentRuntimeReadiness.DataOnlyPendingRuntime;
        definition.RuntimeNote =
            definition.RuntimeReadiness == EmotionAugmentRuntimeReadiness.DesignValuePending
                ? "0916 원본 값이 미정/임시이므로 임의 하드코딩하지 않음."
                : "Phase E-1 정의 이관 완료. 공통 runtime hook 연결 대기.";

        switch (key)
        {
            case "Compassion:부위 재생":
                AddRulebreaker(
                    definition, folder, row.Index, "regenerate",
                    EmotionRulebreakerOperation.RegenerateBrokenPartAsWeakened,
                    amount: 1, value: 1f, required: 1,
                    booleanValue: true, targetPartType: PartType.HEAD, anyPart: true);
                MarkConnected(definition);
                break;

            case "Detachment:슬롯 유지":
                AddRulebreaker(
                    definition, folder, row.Index, "preserve_slot",
                    EmotionRulebreakerOperation.PreserveNextBrokenPartSlots,
                    amount: 1);
                MarkConnected(definition);
                break;

            case "Detachment:평정":
                AddRulebreaker(
                    definition, folder, row.Index, "hp_resistance_1",
                    EmotionRulebreakerOperation.OverrideHpResistance,
                    value: 1f);
                MarkConnected(definition);
                break;

            case "Detachment:신의 육체":
                AddRulebreaker(
                    definition, folder, row.Index, "no_stagger",
                    EmotionRulebreakerOperation.DisableStaggerGauge,
                    booleanValue: true);
                MarkConnected(definition);
                break;

            case "Awe:제물(공간)":
                AddRulebreaker(
                    definition, folder, row.Index, "head_slot_plus_1",
                    EmotionRulebreakerOperation.AddPartSlots,
                    amount: 1, targetPartType: PartType.HEAD);
                AddRulebreaker(
                    definition, folder, row.Index, "seal_legs",
                    EmotionRulebreakerOperation.SuppressPartSlots,
                    booleanValue: true, targetPartType: PartType.LEGS);
                MarkConnected(definition);
                break;

            case "Admiration:즉시 풀 충전":
                EmotionAugmentPrototypeEffectDefinition prestige =
                    EnsureAsset<EmotionAugmentPrototypeEffectDefinition>(
                        $@"{folder}/Effects/T{row.Tier}_{row.Index:00}_Prestige50.asset");
                prestige.GainPrestige = 50;
                prestige.GainEnergy = 0;
                prestige.RecoverStaggerRatio = 0f;
                prestige.RollBonus = 0;
                prestige.DamageDealtMultiplier = 1f;
                prestige.DamageTakenMultiplier = 1f;
                prestige.PositiveMomentumBonus = 0;
                prestige.PrestigeGainMultiplier = 1f;
                EditorUtility.SetDirty(prestige);
                definition.Effects.Add(prestige);
                MarkConnected(definition);
                break;

            case "Longing:유지":
                AddRulebreaker(
                    definition, folder, row.Index, "carry_positive",
                    EmotionRulebreakerOperation.CarryPositiveMomentum,
                    booleanValue: true);
                MarkConnected(definition);
                break;

            case "Longing:잔류":
                AddRulebreaker(
                    definition, folder, row.Index, "carry_negative",
                    EmotionRulebreakerOperation.CarryNegativeMomentum,
                    booleanValue: true);
                MarkConnected(definition);
                break;

            case "Longing:왕귀(짓누름)":
                AddRulebreaker(
                    definition, folder, row.Index, "overwhelm_ascension",
                    EmotionRulebreakerOperation.ConfigureOverwhelmAscension,
                    value: 2f, required: 2);
                MarkConnected(definition);
                break;

            case "Longing:왕귀(짓눌림)":
                AddRulebreaker(
                    definition, folder, row.Index, "laststand_revive",
                    EmotionRulebreakerOperation.ConfigureLastStandRevive,
                    amount: 1, value: 0.30f, required: 2);
                MarkConnected(definition);
                break;

            case "Longing:왕귀(중립)":
                AddRulebreaker(
                    definition, folder, row.Index, "balance_repeat",
                    EmotionRulebreakerOperation.ConfigureBalanceResolutionRepeat,
                    required: 3, booleanValue: true);
                MarkConnected(definition);
                break;
        }
    }

    private static void MarkConnected(EmotionAugmentDefinition definition)
    {
        definition.RuntimeReadiness =
            EmotionAugmentRuntimeReadiness.RuntimeConnected;
        definition.RuntimeNote =
            "Phase C rulebreaker/prototype runtime hook에 연결됨.";
    }

    private static bool IsDesignValuePending(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.IndexOf("미정", StringComparison.Ordinal) >= 0 ||
               status.IndexOf("임시", StringComparison.Ordinal) >= 0;
    }

    private static void AddRulebreaker(
        EmotionAugmentDefinition definition,
        string folder,
        int index,
        string suffix,
        EmotionRulebreakerOperation operation,
        int amount = 1,
        float value = 1f,
        int required = 1,
        bool booleanValue = true,
        PartType targetPartType = PartType.HEAD,
        bool anyPart = false)
    {
        EnsureFolder(folder + "/Effects");
        string path =
            $@"{folder}/Effects/T{definition.Tier}_{index:00}_{suffix}.asset";

        EmotionRulebreakerEffectDefinition effect =
            EnsureAsset<EmotionRulebreakerEffectDefinition>(path);

        effect.Operation = operation;
        effect.Amount = Mathf.Max(0, amount);
        effect.Value = value;
        effect.RequiredConsecutiveTurns = Mathf.Max(1, required);
        effect.BooleanValue = booleanValue;
        effect.TargetPartType = targetPartType;
        effect.AnyPart = anyPart;
        EditorUtility.SetDirty(effect);

        definition.Effects.Add(effect);
    }

    private static bool MigratePokerFace()
    {
        SkillDefinition definition =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PokerFacePath);
        if (definition == null)
            return false;

        definition.SkillName = "포커페이스";
        definition.ActionType = ActionType.Preparation;
        definition.PreparationTier = PreparationTier.Strong;
        definition.OverrideEnergyCost = true;
        definition.EnergyCost = 1;
        definition.Description =
            "받는 피해 교환당 -4 · 피격당 뼈 +4";
        EditorUtility.SetDirty(definition);
        return true;
    }

    private static void MigrateStage1Boss()
    {
        SkillDefinition a =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossAPath);
        SkillDefinition b =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossBPath);
        SkillDefinition fallback =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossFallbackPath);

        ConfigureBossSkill(a, "STAGE1_BOSS_A", "A", ActionType.Duel, 3, 13, 1);
        ConfigureBossSkill(b, "STAGE1_BOSS_B", "B", ActionType.Duel, 2, 14, 1);
        ConfigureBossSkill(
            fallback,
            "STAGE1_BOSS_B_FALLBACK",
            "B (빛 부족 평타)",
            ActionType.NormalAttack,
            2,
            14,
            0);

        BossPhaseData phase =
            AssetDatabase.LoadAssetAtPath<BossPhaseData>(BossPhase1Path);
        if (phase != null && a != null && b != null && fallback != null)
        {
            phase.PhaseId = "STAGE1_CANONICAL";
            phase.DisplayName = "Stage 1 Canonical";
            phase.MinimumTurn = 1;
            phase.EnterAtOrBelowHpRate = 1f;
            phase.RequireMomentumAtOrBelow = false;
            phase.SlotConfigs = new List<CharacterSlotConfig>
            {
                MakeBossSlot("A_01", "A", a, null),
                MakeBossSlot("A_02", "A", a, null),
                MakeBossSlot("B_01", "B", b, fallback)
            };
            phase.DuelSkillPool = new List<SkillDefinition> { a, b };
            phase.NormalSkillPool = new List<SkillDefinition> { fallback };
            phase.PreparationSkillPool = new List<SkillDefinition>();
            phase.PrestigeSkillPool = new List<SkillDefinition>();
            EditorUtility.SetDirty(phase);
        }

        CharacterData data =
            AssetDatabase.LoadAssetAtPath<CharacterData>(BossDataPath);
        if (data != null)
        {
            data.CombatantTier = CombatantTier.Boss;
            data.maxEnergy = 2;
            data.OverrideMaxStaggerGauge = true;
            data.MaxStaggerGauge = 400;
            if (phase != null)
                data.BossPhases = new List<BossPhaseData> { phase };
            EditorUtility.SetDirty(data);
        }

        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BossBundlePath);
        if (bundle?.EliteBodyPartDefinitions != null &&
            bundle.EliteBodyPartDefinitions.Count == 5)
        {
            foreach (EnemyBodyPartDefinition part in bundle.EliteBodyPartDefinitions)
            {
                if (part != null)
                    part.MaxPartHP = 150f;
            }
            EditorUtility.SetDirty(bundle);
        }
    }

    private static CharacterSlotConfig MakeBossSlot(
        string id,
        string displayName,
        SkillDefinition fixedSkill,
        SkillDefinition fallback)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = displayName,
            Enabled = true,
            HasLinkedPart = false,
            FixedSkill = fixedSkill,
            InsufficientEnergyFallbackSkill = fallback,
            TargetingPolicy = AITargetingPolicy.RandomValid,
            AllowedActionTypes = new List<ActionType>
            {
                ActionType.Duel,
                ActionType.NormalAttack
            }
        };
    }

    private static void ConfigureBossSkill(
        SkillDefinition definition,
        string skillId,
        string displayName,
        ActionType actionType,
        int rollCount,
        int power,
        int energyCost)
    {
        if (definition == null)
            return;

        SerializedObject serialized = new(definition);
        SerializedProperty id = serialized.FindProperty("skillId");
        if (id != null)
            id.stringValue = skillId;

        definition.SkillName = displayName;
        definition.ActionType = actionType;
        definition.Color = SkillColor.Unset;
        definition.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
        definition.BasePower = power;
        definition.CanBreakPart = false;
        definition.BreakMode = PartBreakMode.None;
        definition.OverrideEnergyCost = true;
        definition.EnergyCost = energyCost;
        definition.ExchangeRollCount = rollCount;
        definition.ResolverType = SkillResolverType.Dice;
        definition.DiceMin = 1;
        definition.DiceMax = 8;
        definition.Rolls = new List<SkillRollData>();

        for (int i = 0; i < rollCount; i++)
        {
            definition.Rolls.Add(new SkillRollData
            {
                Index = i,
                Type = CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                RngSource = RollRngSource.CharacterDefault
            });
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    public static void AuditRollTextures(PhaseEContentManifest manifest)
    {
        manifest.RollTextureScanned = 0;
        manifest.RollTexturePassed = 0;
        manifest.RollTexturePending = 0;
        manifest.RollTextureViolationCount = 0;
        manifest.RollTextureIssues ??= new List<string>();
        manifest.RollTextureIssues.Clear();

        string[] guids = AssetDatabase.FindAssets(
            "t:SkillDefinition",
            new[]
            {
                "Assets/2. Data/Characters/Design2026",
                "Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills"
            });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null ||
                (definition.ActionType != ActionType.NormalAttack &&
                 definition.ActionType != ActionType.Duel) ||
                definition.ResolverType != SkillResolverType.Dice ||
                definition.Rolls == null ||
                definition.Rolls.Count == 0)
            {
                continue;
            }

            manifest.RollTextureScanned++;

            int basePower = PowerFormulaService.ResolveBasePower(definition);
            if (basePower <= 0)
            {
                manifest.RollTexturePending++;
                manifest.RollTextureIssues.Add(
                    $"PENDING {path}: BasePower를 확정할 수 없음.");
                continue;
            }

            double sum = 0d;
            bool valid = true;
            for (int i = 0; i < definition.Rolls.Count; i++)
            {
                SkillRollData roll = definition.Rolls[i];
                if (roll == null)
                {
                    valid = false;
                    break;
                }

                int min = roll.GetDiceFinalMinPower(basePower);
                int max = roll.GetDiceFinalMaxPower(basePower);
                sum += (min + max) * 0.5d;
            }

            if (!valid)
            {
                manifest.RollTexturePending++;
                manifest.RollTextureIssues.Add(
                    $"PENDING {path}: null roll.");
                continue;
            }

            double actualMean = sum / definition.Rolls.Count;
            double expectedMean = basePower + 4.5d;

            if (Math.Abs(actualMean - expectedMean) <= 0.001d)
            {
                manifest.RollTexturePassed++;
            }
            else
            {
                // 0916(3) §4.3은 위치별 평균의 평균 = 기본위력 + 4.5를 정본 불변식으로 둔다.
                // TEMP_BALANCE_V1 표식으로 canonical 위반을 PASS 처리하지 않는다.
                manifest.RollTextureViolationCount++;
                manifest.RollTextureIssues.Add(
                    $"VIOLATION {path}: mean={actualMean:0.###}, expected={expectedMean:0.###}.");
            }
        }
    }

    private static T EnsureAsset<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        // Phase E-1 v1 could create a placeholder whose m_Script was missing because
        // several ScriptableObject classes did not have same-name script files.
        // These paths are migration-owned canonical assets, so replace only an
        // existing object that cannot be loaded as the requested type.
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null)
        {
            Debug.LogWarning(
                $"[Phase E-1 v2] Recreating invalid canonical asset: {path} " +
                $"(existing type={existing.GetType().Name}, expected={typeof(T).Name})");
            AssetDatabase.DeleteAsset(path);
        }

        string directory = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(directory);

        asset = ScriptableObject.CreateInstance<T>();
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];

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
