#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 0916(3) 정본 closure.
/// PhaseETempBalanceMigration이 만든 콘텐츠를 버리지 않고,
/// 확정된 SkillId/색/빛 비용/위세 임계와 Yujin의 명시 미구현 rider를 정본 경로로 닫는다.
/// (미정) 값은 새 canonical 상수로 만들지 않는다.
/// </summary>
[InitializeOnLoad]
public static class Canonical0916ClosureMigration
{
    public const string ProfileId = "CANONICAL_0916_CLOSURE_V1";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";
    private const string EffectRoot =
        "Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects";

    // 0916 TEMP/closure에서 새 SkillDefinition을 생성할 때 Presentation extension은
    // 자동 이관되지 않았다. 실제 판정 HP는 이미 Resolve 시점에 변경되므로,
    // Timeline Hit Event가 없으면 World HP bar가 판정보다 먼저 최종 HP를 읽는다.
    // 새 전용 연출이 authoring되기 전까지는 같은 캐릭터의 검증된 기존 Timeline을
    // fallback으로만 연결한다. 이미 완성된 개별 PresentationAsset은 절대 덮어쓰지 않는다.
    private const string OlafNormalVisualPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Cutscenes/Olaf_EnduringSlash/Olaf_EnduringSlash_Visual.asset";
    private const string OlafDuelVisualPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Cutscenes/Olaf_Standard/Olaf_Standard_Visual.asset";
    private const string OlafPreparationVisualPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Cutscenes/Olaf_Crouch/Olaf_Crouch_Visual.asset";
    private const string OlafPrestigeVisualPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Cutscenes/Olaf_BloomingWound/Olaf_BloomingWound_Visual.asset";

    private const string YujinNormalVisualPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/Cutscenes/Yujin_Inspection/Yujin_Inspection_Visual.asset";
    private const string YujinDuelVisualPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/Cutscenes/Yujin_Inscription/Yujin_Inscription_Visual.asset";
    private const string YujinPreparationVisualPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/Cutscenes/Yujin_Capture/Yujin_Capture_Visual.asset";
    private const string YujinPrestigeVisualPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/Cutscenes/Yujin_Brand/Yujin_Brand_Visual.asset";

    // Stage 1 canonical boss가 재사용하는 legacy Elite duel visual에는
    // Olaf 전용 BloodSplash VFX가 남아 있는데 해당 Definition의 Prefab이 비어 있다.
    // VFX 본체를 임의 제작하지 않고, Prefab이 실제로 비어 있을 때에만
    // canonical boss visual에서 그 깨진 참조를 제거한다.
    private const string Stage1BossLegacyDuelVisualPath =
        "Assets/2. Data/Characters/Enemies/EliteEnemy/SkillS/Cutscenes/정예병의 압박(결투)/정예병의 압박(결투)_Timeline_Visual.asset";
    private const string LegacyOlafBloodSplashVfxPath =
        "Assets/2. Data/BattleVisual/VFXDefinitions/Skills/Olaf/Olaf_Duel_BloodSplash_VFX.asset";

    private sealed class OlafC44TextureSpec
    {
        public string SkillId;
        public int[] Min;
        public int[] Max;

        public OlafC44TextureSpec(string skillId, int[] min, int[] max)
        {
            SkillId = skillId;
            Min = min;
            Max = max;
        }
    }

    // 0916(3) §4.3 우선순위: 위치별 평균의 평균 = 기본위력 + 4.5.
    // TEMP_BALANCE_V1의 15개 XLSX 보존 예외를 최소 정수 보정으로 canonical range로 닫는다.
    // 값은 SkillDefinition의 base 단계 기준이며 Upgrade1/2가 base/range를 각각 +1 하므로
    // 두 강화 단계에서도 동일한 평균 불변식이 그대로 유지된다.
    private static readonly OlafC44TextureSpec[] OlafC44Textures =
    {
        C44(OlafSkillIds.Bite,          new[]{7, 8, 9},       new[]{18, 19, 20}),
        C44(OlafSkillIds.ShieldWall,    new[]{10, 10, 7},     new[]{13, 17, 24}),
        C44(OlafSkillIds.Smash,         new[]{10, 13},        new[]{15, 20}),
        C44(OlafSkillIds.Flurry,        new[]{13, 11, 10, 8, 6}, new[]{19, 17, 15, 14, 12}),
        C44(OlafSkillIds.RiseAgain,     new[]{11, 2, 10, 2},  new[]{16, 9, 17, 9}),
        C44(OlafSkillIds.Cover,         new[]{19, 11},        new[]{26, 18}),
        C44(OlafSkillIds.ExploitGap,    new[]{11, 13, 14},    new[]{16, 20, 25}),
        C44(OlafSkillIds.FirstStrike,   new[]{14, 10},        new[]{19, 15}),
        C44(OlafSkillIds.BlockWay,      new[]{15, 11},        new[]{22, 18}),
        C44(OlafSkillIds.SliceThin,     new[]{14, 18},        new[]{17, 29}),
        C44(OlafSkillIds.SingleCut,     new[]{22},            new[]{27}),
        C44(OlafSkillIds.BloodCharge,   new[]{15, 11},        new[]{22, 18}),
        C44(OlafSkillIds.AllIn,         new[]{10, 13, 16},    new[]{17, 20, 23}),
        C44(OlafSkillIds.AbsorbBlood,   new[]{13, 14},        new[]{20, 19}),
        C44(OlafSkillIds.HonorableFight,new[]{15},            new[]{22})
    };

    private sealed class SkillMeta
    {
        public string Name;
        public string Id;
        public ActionType Type;
        public SkillColor Color;
        public int Energy;
        public int PrestigeThreshold;
        public PreparationTier PreparationTier;

        public SkillMeta(
            string name,
            string id,
            ActionType type,
            SkillColor color,
            int energy,
            int prestigeThreshold = 0,
            PreparationTier preparationTier = PreparationTier.Strong)
        {
            Name = name;
            Id = id;
            Type = type;
            Color = color;
            Energy = energy;
            PrestigeThreshold = prestigeThreshold;
            PreparationTier = preparationTier;
        }
    }

    private static readonly SkillMeta[] Olaf =
    {
        // Normal 16
        M("물어뜯기", OlafSkillIds.Bite, ActionType.NormalAttack, SkillColor.Red, 0),
        M("방패벽", OlafSkillIds.ShieldWall, ActionType.NormalAttack, SkillColor.Blue, 0),
        M("쪼개기", OlafSkillIds.Split, ActionType.NormalAttack, SkillColor.Red, 1),
        M("버텨내기", OlafSkillIds.Endure, ActionType.NormalAttack, SkillColor.Blue, 1),
        M("후려치기", OlafSkillIds.Smash, ActionType.NormalAttack, SkillColor.Red, 0),
        M("받아넘기기", OlafSkillIds.Parry, ActionType.NormalAttack, SkillColor.Blue, 0),
        M("몰아치기", OlafSkillIds.Flurry, ActionType.NormalAttack, SkillColor.Red, 1),
        M("다시 일어서다", OlafSkillIds.RiseAgain, ActionType.NormalAttack, SkillColor.Blue, 1),
        M("필사의 일격", OlafSkillIds.DesperateBlow, ActionType.NormalAttack, SkillColor.Red, 2),
        M("감싸기", OlafSkillIds.Cover, ActionType.NormalAttack, SkillColor.Blue, 2),
        M("틈을 파고들다", OlafSkillIds.ExploitGap, ActionType.NormalAttack, SkillColor.Red, 1),
        M("선수치기", OlafSkillIds.FirstStrike, ActionType.NormalAttack, SkillColor.Blue, 0),
        M("약점을 노리다", OlafSkillIds.AimWeakness, ActionType.NormalAttack, SkillColor.Red, 1),
        M("숨통을 조이다", OlafSkillIds.Choke, ActionType.NormalAttack, SkillColor.Red, 1),
        M("막아서기", OlafSkillIds.BlockWay, ActionType.NormalAttack, SkillColor.Blue, 1),
        M("저며내기", OlafSkillIds.SliceThin, ActionType.NormalAttack, SkillColor.Red, 1),

        // Duel 21
        M("표준", OlafSkillIds.Standard, ActionType.Duel, SkillColor.Red, 2),
        M("끈질기게", OlafSkillIds.Tenacious, ActionType.Duel, SkillColor.Blue, 2),
        M("단칼", OlafSkillIds.SingleCut, ActionType.Duel, SkillColor.Red, 1),
        M("미쳐물어뜯기", OlafSkillIds.MadBite, ActionType.Duel, SkillColor.Blue, 1),
        M("피의돌진", OlafSkillIds.BloodCharge, ActionType.Duel, SkillColor.Red, 1),
        M("물고 늘어지기", OlafSkillIds.HoldOn, ActionType.Duel, SkillColor.Blue, 1),
        M("제살깎기", OlafSkillIds.SelfCut, ActionType.Duel, SkillColor.Blue, 2),
        M("물러서지않기", OlafSkillIds.NoRetreat, ActionType.Duel, SkillColor.Red, 1),
        M("정면승부", OlafSkillIds.HeadOn, ActionType.Duel, SkillColor.Red, 0),
        M("위세를 지피다", OlafSkillIds.StokePrestige, ActionType.Preparation, SkillColor.Unset, 1),
        M("굳히기", OlafSkillIds.Fortify, ActionType.Duel, SkillColor.Blue, 2),
        M("몸을 사리다", OlafSkillIds.ProtectSelf, ActionType.Duel, SkillColor.Blue, 2),
        M("허를 찌르다", OlafSkillIds.CatchOffGuard, ActionType.Duel, SkillColor.Red, 0),
        M("전부 걸다", OlafSkillIds.AllIn, ActionType.Duel, SkillColor.Red, 2),
        M("피의 대가", OlafSkillIds.BloodPrice, ActionType.Duel, SkillColor.Blue, 2),
        M("피를 흡수하다", OlafSkillIds.AbsorbBlood, ActionType.Duel, SkillColor.Blue, 1),
        M("닥치는 대로", OlafSkillIds.WhateverComes, ActionType.Duel, SkillColor.Red, 4),
        M("명예로운 전투", OlafSkillIds.HonorableFight, ActionType.Duel, SkillColor.Red, 3),
        M("피의 맹세", OlafSkillIds.BloodOathDuel, ActionType.Duel, SkillColor.Red, 1),
        M("전부 되갚다", OlafSkillIds.PaybackAll, ActionType.Duel, SkillColor.Red, 1),
        M("일기토", OlafSkillIds.SingleCombat, ActionType.Duel, SkillColor.Red, 1),

        // Preparation: common 2 + Olaf 7
        M("웅크리기", OlafSkillIds.Crouch, ActionType.Preparation, SkillColor.Unset, 0, preparationTier: PreparationTier.Weak),
        M("노려보기", OlafSkillIds.Glare, ActionType.Preparation, SkillColor.Unset, 1, preparationTier: PreparationTier.Weak),
        M("블로토", OlafSkillIds.Bloto, ActionType.Preparation, SkillColor.Unset, 1),
        M("피의 서약", OlafSkillIds.BloodOathPreparation, ActionType.Preparation, SkillColor.Unset, 1),
        M("피로 사는 숨", OlafSkillIds.BreathBoughtWithBlood, ActionType.Preparation, SkillColor.Unset, 0, preparationTier: PreparationTier.Weak),
        M("광기를 가라앉히다", OlafSkillIds.CalmMadness, ActionType.Preparation, SkillColor.Unset, 0),
        M("상처를 지우다", OlafSkillIds.EraseWound, ActionType.Preparation, SkillColor.Unset, 1),
        M("흐트러짐을 삼키다", OlafSkillIds.DevourStagger, ActionType.Preparation, SkillColor.Unset, 1),
        M("최후의 저항", OlafSkillIds.LastResistance, ActionType.Preparation, SkillColor.Unset, 1),

        // Prestige 3
        M("룬 새기기", OlafSkillIds.RuneCarving, ActionType.Prestige, SkillColor.Unset, 0, 60),
        M("베르세르크", OlafSkillIds.Berserk, ActionType.Prestige, SkillColor.Unset, 0, 65),
        M("라그나로크", OlafSkillIds.Ragnarok, ActionType.Prestige, SkillColor.Unset, 0, 70)
    };

    private static readonly SkillMeta[] Yujin =
    {
        // Normal 9
        M("뒷조사", YujinSkillIds.BackgroundCheck, ActionType.NormalAttack, SkillColor.Red, 0),
        M("숨 고르기", YujinSkillIds.CatchBreath, ActionType.NormalAttack, SkillColor.Blue, 2),
        M("장부 정리", YujinSkillIds.LedgerCleanup, ActionType.NormalAttack, SkillColor.Red, 0),
        M("한 발 물러서다", YujinSkillIds.StepBack, ActionType.NormalAttack, SkillColor.Blue, 0),
        M("정조준", YujinSkillIds.Aim, ActionType.NormalAttack, SkillColor.Red, 1),
        M("확인 사살", YujinSkillIds.ConfirmKill, ActionType.NormalAttack, SkillColor.Red, 1),
        M("손에 익히다", YujinSkillIds.FamiliarHand, ActionType.NormalAttack, SkillColor.Blue, 0),
        M("믿는 구석", YujinSkillIds.AceInTheHole, ActionType.NormalAttack, SkillColor.Red, 1),
        M("현상수배", YujinSkillIds.Wanted, ActionType.NormalAttack, SkillColor.Blue, 1),

        // Duel 14
        M("의뢰 대상", YujinSkillIds.ContractTarget, ActionType.Duel, SkillColor.Red, 1),
        M("잔혹한 마무리", YujinSkillIds.BrutalFinish, ActionType.Duel, SkillColor.Red, 1),
        M("목표 지정", YujinSkillIds.DesignateTarget, ActionType.Duel, SkillColor.Blue, 1),
        M("그림자 속으로", YujinSkillIds.IntoShadows, ActionType.Duel, SkillColor.Blue, 1),
        M("흔들어놓다", YujinSkillIds.ShakeUp, ActionType.Duel, SkillColor.Blue, 1),
        M("숨통을 끊다", YujinSkillIds.CutThroat, ActionType.Duel, SkillColor.Red, 1),
        M("소문을 내다", YujinSkillIds.SpreadRumor, ActionType.Duel, SkillColor.Red, 1),
        M("약점을 캐내다", YujinSkillIds.ProbeWeakness, ActionType.Duel, SkillColor.Blue, 1),
        M("퇴로 차단", YujinSkillIds.BlockRetreat, ActionType.Duel, SkillColor.Red, 1),
        M("빌려 쓰다", YujinSkillIds.BorrowWeapon, ActionType.Duel, SkillColor.Red, 2),
        M("덫을 놓다", YujinSkillIds.Trap, ActionType.Duel, SkillColor.Red, 1),
        M("시한을 정하다", YujinSkillIds.Deadline, ActionType.Duel, SkillColor.Blue, 1),
        M("모두를 노리다", YujinSkillIds.AllTargets, ActionType.Duel, SkillColor.Blue, 2),
        M("마침표를 찍다", YujinSkillIds.Period, ActionType.Duel, SkillColor.Red, 1),

        // Preparation: common 2 + Yujin 7
        M("웅크리기", YujinSkillIds.Crouch, ActionType.Preparation, SkillColor.Unset, 0, preparationTier: PreparationTier.Weak),
        M("노려보기", YujinSkillIds.Glare, ActionType.Preparation, SkillColor.Unset, 1, preparationTier: PreparationTier.Weak),
        M("노림수", YujinSkillIds.Read, ActionType.Preparation, SkillColor.Unset, 1),
        M("선금 받기", YujinSkillIds.AdvancePay, ActionType.Preparation, SkillColor.Unset, 1),
        M("환형(공격)", YujinSkillIds.HwanhyeongAttack, ActionType.Preparation, SkillColor.Unset, 1),
        M("환형(수비)", YujinSkillIds.HwanhyeongDefense, ActionType.Preparation, SkillColor.Unset, 1),
        M("기회를 엿보다", YujinSkillIds.WaitChance, ActionType.Preparation, SkillColor.Unset, 0),
        M("숨을 죽이다", YujinSkillIds.HoldBreath, ActionType.Preparation, SkillColor.Unset, 0),
        M("칼을 갈다", YujinSkillIds.Sharpen, ActionType.Preparation, SkillColor.Unset, 0),

        // Prestige 3
        M("밑작업 착수", YujinSkillIds.PrepWork, ActionType.Prestige, SkillColor.Unset, 0, 50),
        M("확실한 일처리", YujinSkillIds.CertainExecution, ActionType.Prestige, SkillColor.Unset, 0, 50),
        M("유연한 대처", YujinSkillIds.FlexibleResponse, ActionType.Prestige, SkillColor.Unset, 0, 50)
    };

    static Canonical0916ClosureMigration()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Project Abyss/Migrations/0916/Apply Canonical Character Closure")]
    public static void ApplyFromMenu()
    {
        Apply();
        Debug.Log("[0916 Closure] Olaf/Yujin canonical content + exact verification data migration applied.");
    }

    private static void ApplyIfNeeded()
    {
        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);
        CharacterCombatLoadout yujin =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(YujinLoadoutPath);

        // Phase E TEMP migration보다 먼저 domain reload callback이 도는 경우는 기다린다.
        if (!HasSkillNamed(olaf, "물어뜯기") || !HasSkillNamed(yujin, "뒷조사"))
            return;

        if (NeedsApply(olaf, yujin))
            Apply();
    }

    public static void Apply()
    {
        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);
        CharacterCombatLoadout yujin =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(YujinLoadoutPath);

        if (olaf == null || yujin == null)
        {
            Debug.LogWarning("[0916 Closure] Olaf/Yujin loadout가 없습니다. Phase E migration을 먼저 적용하세요.");
            return;
        }

        ApplyMetadata(olaf, Olaf);
        ApplyMetadata(yujin, Yujin);
        int olafC44TexturesNormalized = 0; // 0917: legacy 15-card C44 hardcode disabled; card data owns provisional/confirmed values.
        EnsureYujinStarterPreparationLoadout(yujin);
        WireYujinCanonicalEffects(yujin);

        int olafPresentationFallbacks = WireCanonicalPresentationFallbacks(
            olaf,
            Olaf,
            OlafNormalVisualPath,
            OlafDuelVisualPath,
            OlafPreparationVisualPath,
            OlafPrestigeVisualPath,
            "Olaf");
        int yujinPresentationFallbacks = WireCanonicalPresentationFallbacks(
            yujin,
            Yujin,
            YujinNormalVisualPath,
            YujinDuelVisualPath,
            YujinPreparationVisualPath,
            YujinPrestigeVisualPath,
            "Yujin");

        int removedLegacyBossVfxReferences =
            SanitizeStage1BossLegacyBloodSplashVfx();

        // 0916 정본: 2회차 강화 가격은 (미정). TEMP_BALANCE_V1에서 임의로 둔 가격을 canonical 값처럼 남기지 않는다.
        ClearSecondUpgradeCosts(olaf);
        ClearSecondUpgradeCosts(yujin);

        PhaseEContentManifest manifest =
            AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(PhaseEContentMigration.ManifestPath);
        if (manifest != null)
        {
            manifest.TempBalanceNotes ??= new List<string>();
            manifest.TempBalanceNotes.RemoveAll(note =>
                !string.IsNullOrWhiteSpace(note) &&
                note.Contains("2회차 강화비"));
            if (!manifest.TempBalanceNotes.Contains("0916 Closure: 2회차 강화 가격은 정본 (미정)이므로 CostOverride=0(Unset) 유지."))
            {
                manifest.TempBalanceNotes.Add(
                    "0916 Closure: 2회차 강화 가격은 정본 (미정)이므로 CostOverride=0(Unset) 유지.");
            }
            if (!manifest.TempBalanceNotes.Contains("0916 Closure: Yujin 명시 미구현 rider를 data-driven runtime effect로 연결."))
            {
                manifest.TempBalanceNotes.Add(
                    "0916 Closure: Yujin 명시 미구현 rider를 data-driven runtime effect로 연결.");
            }

            const string c44NotePrefix = "0916 Closure C44:";
            manifest.TempBalanceNotes.RemoveAll(note =>
                !string.IsNullOrWhiteSpace(note) &&
                note.StartsWith(c44NotePrefix, StringComparison.Ordinal));
            manifest.TempBalanceNotes.Add(
                $"{c44NotePrefix} Olaf 15개 TEMP_OVERRIDE를 0916(3) §4.3 평균 불변식으로 정규화. " +
                $"이번 적용에서 변경된 SkillDefinition={olafC44TexturesNormalized}. TEMP bypass 제거.");

            const string hwanhyeongStarterNote =
                "0916 Closure: Yujin starter 도사림 = 노림수/선금 받기/환형(공격 TEMP). 공격/수비 모두 후보 pool 보존, 둘 중 1장 장착 계약.";
            if (!manifest.TempBalanceNotes.Contains(hwanhyeongStarterNote))
                manifest.TempBalanceNotes.Add(hwanhyeongStarterNote);

            const string presentationNotePrefix = "0916 Closure Presentation:";
            manifest.TempBalanceNotes.RemoveAll(note =>
                !string.IsNullOrWhiteSpace(note) &&
                note.StartsWith(presentationNotePrefix, StringComparison.Ordinal));
            manifest.TempBalanceNotes.Add(
                $"{presentationNotePrefix} missing/incomplete canonical PresentationAsset only -> " +
                $"character fallback Timeline wired. Olaf={olafPresentationFallbacks}, Yujin={yujinPresentationFallbacks}. " +
                "Hit Event가 HP 표시 시점을 소유하며 authored visual은 보존.");

            const string vfxNotePrefix = "0916 Closure VFX:";
            manifest.TempBalanceNotes.RemoveAll(note =>
                !string.IsNullOrWhiteSpace(note) &&
                note.StartsWith(vfxNotePrefix, StringComparison.Ordinal));
            manifest.TempBalanceNotes.Add(
                $"{vfxNotePrefix} Stage1 canonical boss legacy duel visual에서 " +
                $"Prefab 없는 Olaf_Duel_BloodSplash_VFX 참조 {removedLegacyBossVfxReferences}개 제거. " +
                "VFX Definition 자체는 향후 authoring을 위해 유지.");
            EditorUtility.SetDirty(manifest);
        }

        EditorUtility.SetDirty(olaf);
        EditorUtility.SetDirty(yujin);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ApplyMetadata(
        CharacterCombatLoadout loadout,
        IEnumerable<SkillMeta> metadata)
    {
        Dictionary<string, SkillDefinition> byName =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillName))
                .GroupBy(x => x.SkillName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (SkillMeta meta in metadata)
        {
            if (!byName.TryGetValue(meta.Name, out SkillDefinition skill) || skill == null)
            {
                Debug.LogWarning($"[0916 Closure] skill missing: {meta.Name} ({meta.Id})");
                continue;
            }

            SetSkillId(skill, meta.Id);
            skill.ActionType = meta.Type;
            if (meta.Color != SkillColor.Unset)
                skill.Color = meta.Color;
            skill.OverrideEnergyCost = true;
            skill.EnergyCost = Mathf.Max(0, meta.Energy);

            if (meta.Type == ActionType.Preparation)
                skill.PreparationTier = meta.PreparationTier;

            if (meta.Type == ActionType.Prestige && meta.PrestigeThreshold > 0)
            {
                skill.OverrideResourceRules = true;
                skill.RequireFullPrestige = false;
                skill.PrestigeCost = meta.PrestigeThreshold;
                skill.ConsumeAllPrestige = false;
            }

            if (string.IsNullOrWhiteSpace(skill.Description) ||
                !skill.Description.Contains(ProfileId))
            {
                skill.Description =
                    $"[{ProfileId}] 0916(3) canonical metadata synced.\n" +
                    (skill.Description ?? string.Empty);
            }

            EditorUtility.SetDirty(skill);
        }
    }

    private static void WireYujinCanonicalEffects(CharacterCombatLoadout loadout)
    {
        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillId))
                .GroupBy(x => x.SkillId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Native YujinMechanic이 정본 역할을 이미 수행하는 카드: TEMP proxy만 제거.
        foreach (string id in new[]
        {
            YujinSkillIds.BackgroundCheck,
            YujinSkillIds.CatchBreath,
            YujinSkillIds.BrutalFinish,
            YujinSkillIds.Read,
            YujinSkillIds.AdvancePay,
            YujinSkillIds.HwanhyeongAttack,
            YujinSkillIds.HwanhyeongDefense,
            YujinSkillIds.PrepWork,
            YujinSkillIds.CertainExecution,
            YujinSkillIds.FlexibleResponse
        })
        {
            if (byId.TryGetValue(id, out SkillDefinition skill))
                ClearEffects(skill);
        }

        Attach(byId, YujinSkillIds.LedgerCleanup,
            E("ledger_cleanup", Yujin0916EffectOperation.LedgerCleanup, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.StepBack,
            E("step_back", Yujin0916EffectOperation.StepBackBlock, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.Aim,
            E("aim_critical", Yujin0916EffectOperation.AimCriticalRider, SkillEffectTiming.OnExchangeWin));
        Attach(byId, YujinSkillIds.ConfirmKill,
            E("confirm_kill", Yujin0916EffectOperation.ConfirmKillMomentum, SkillEffectTiming.OnExchangeWin));
        Attach(byId, YujinSkillIds.FamiliarHand,
            E("familiar_hand", Yujin0916EffectOperation.FamiliarHand, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.AceInTheHole,
            E("ace_in_hole", Yujin0916EffectOperation.AceInTheHole, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.Wanted,
            E("wanted", Yujin0916EffectOperation.Wanted, SkillEffectTiming.OnExchangeWin));

        // 의뢰 대상의 표식 적립은 YujinMechanic native hook을 사용한다.
        // 기세 바 +15는 0916 정본에서 (미정)이므로 canonical effect로 굳히지 않는다.
        if (byId.TryGetValue(YujinSkillIds.ContractTarget, out SkillDefinition contractTarget))
            ClearEffects(contractTarget);
        Attach(byId, YujinSkillIds.DesignateTarget,
            E("designate", Yujin0916EffectOperation.DesignateCurrent, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.IntoShadows,
            E("into_shadows_block", Yujin0916EffectOperation.IntoShadowsBlock, SkillEffectTiming.OnExecute),
            E("into_shadows_sense", Yujin0916EffectOperation.GainSense, SkillEffectTiming.OnExchangeLose, 1));
        Attach(byId, YujinSkillIds.ShakeUp,
            E("shake_up", Yujin0916EffectOperation.GainSense, SkillEffectTiming.OnExchangeWin, 1));

        SkillEffectCondition weakened = Condition(SkillEffectConditionType.TargetWasWeakenedByThisDamage);
        SkillEffectCondition broken = Condition(SkillEffectConditionType.TargetWasBrokenByThisDamage);
        Attach(byId, YujinSkillIds.CutThroat,
            E("cut_throat_weaken", Yujin0916EffectOperation.CutThroatMark, SkillEffectTiming.OnExchangeWin, 20, weakened),
            E("cut_throat_break", Yujin0916EffectOperation.CutThroatMark, SkillEffectTiming.OnExchangeWin, 20, broken));
        Attach(byId, YujinSkillIds.SpreadRumor,
            E("spread_rumor_weaken", Yujin0916EffectOperation.SpreadRumorMark, SkillEffectTiming.OnExchangeWin, 8, Condition(SkillEffectConditionType.TargetWasWeakenedByThisDamage)),
            E("spread_rumor_break", Yujin0916EffectOperation.SpreadRumorMark, SkillEffectTiming.OnExchangeWin, 8, Condition(SkillEffectConditionType.TargetWasBrokenByThisDamage)));
        Attach(byId, YujinSkillIds.ProbeWeakness,
            E("probe_weakness", Yujin0916EffectOperation.ProbeWeakness, SkillEffectTiming.OnExchangeWin));
        Attach(byId, YujinSkillIds.Trap,
            E("trap", Yujin0916EffectOperation.Trap, SkillEffectTiming.OnExchangeWin, 20));
        Attach(byId, YujinSkillIds.Deadline,
            E("deadline", Yujin0916EffectOperation.Deadline, SkillEffectTiming.OnExchangeWin));
        Attach(byId, YujinSkillIds.AllTargets,
            E("all_targets", Yujin0916EffectOperation.DesignateAll, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.Period,
            E("period_kill", Yujin0916EffectOperation.Period, SkillEffectTiming.OnExchangeWin, 2, Condition(SkillEffectConditionType.KilledTarget)),
            E("period_break", Yujin0916EffectOperation.Period, SkillEffectTiming.OnExchangeWin, 2, Condition(SkillEffectConditionType.TargetWasBrokenByThisDamage)));

        Attach(byId, YujinSkillIds.WaitChance,
            E("wait_chance", Yujin0916EffectOperation.GainSense, SkillEffectTiming.OnExecute, 1));
        Attach(byId, YujinSkillIds.HoldBreath,
            E("hold_breath", Yujin0916EffectOperation.HoldBreath, SkillEffectTiming.OnExecute));
        Attach(byId, YujinSkillIds.Sharpen,
            E("sharpen", Yujin0916EffectOperation.Sharpen, SkillEffectTiming.OnExecute));
    }

    private static void Attach(
        Dictionary<string, SkillDefinition> byId,
        string skillId,
        params SkillEffectEntry[] entries)
    {
        if (!byId.TryGetValue(skillId, out SkillDefinition skill) || skill == null)
        {
            Debug.LogWarning($"[0916 Closure] effect target missing: {skillId}");
            return;
        }

        ClearEffects(skill);
        if (entries != null)
            skill.EffectEntries.AddRange(entries.Where(x => x?.Definition != null));
        EditorUtility.SetDirty(skill);
    }

    private static SkillEffectEntry E(
        string assetKey,
        Yujin0916EffectOperation operation,
        SkillEffectTiming timing,
        int amount = 1,
        params SkillEffectCondition[] conditions)
    {
        EnsureFolder(EffectRoot);
        string safe = Sanitize(assetKey);
        string path = $"{EffectRoot}/{safe}.asset";
        Yujin0916SkillEffectDefinition effect =
            EnsureAsset<Yujin0916SkillEffectDefinition>(path);
        effect.Operation = operation;
        effect.Amount = amount;
        effect.DurationTurns = 3;
        EditorUtility.SetDirty(effect);

        return new SkillEffectEntry
        {
            Definition = effect,
            UpgradeKey = string.Empty,
            OverrideTiming = true,
            Timing = timing,
            Conditions = conditions == null
                ? new List<SkillEffectCondition>()
                : conditions.Where(x => x != null).ToList()
        };
    }

    private static SkillEffectCondition Condition(SkillEffectConditionType type) =>
        new SkillEffectCondition { Type = type };

    private static void ClearEffects(SkillDefinition skill)
    {
        if (skill == null)
            return;
        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.EffectEntries.Clear();
        skill.Effects ??= new List<SkillEffectDefinition>();
        skill.Effects.Clear();
    }

    private static int WireCanonicalPresentationFallbacks(
        CharacterCombatLoadout loadout,
        IEnumerable<SkillMeta> metadata,
        string normalVisualPath,
        string duelVisualPath,
        string preparationVisualPath,
        string prestigeVisualPath,
        string ownerLabel)
    {
        if (loadout == null || metadata == null)
            return 0;

        SkillVisualDefinition normal = LoadCompleteVisual(normalVisualPath, ownerLabel, ActionType.NormalAttack);
        SkillVisualDefinition duel = LoadCompleteVisual(duelVisualPath, ownerLabel, ActionType.Duel);
        SkillVisualDefinition preparation = LoadCompleteVisual(preparationVisualPath, ownerLabel, ActionType.Preparation);
        SkillVisualDefinition prestige = LoadCompleteVisual(prestigeVisualPath, ownerLabel, ActionType.Prestige);

        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillId))
                .GroupBy(x => x.SkillId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int wired = 0;
        foreach (SkillMeta meta in metadata)
        {
            if (meta == null ||
                string.IsNullOrWhiteSpace(meta.Id) ||
                !byId.TryGetValue(meta.Id, out SkillDefinition skill) ||
                skill == null)
            {
                continue;
            }

            // 이미 각 스킬용 authoring이 끝난 연출은 그대로 사용한다.
            if (skill.PresentationAsset is SkillVisualDefinition existing &&
                existing.HasCompleteTimelineSet)
            {
                continue;
            }

            SkillVisualDefinition fallback = meta.Type switch
            {
                ActionType.NormalAttack => normal,
                ActionType.Duel => duel,
                ActionType.Preparation => preparation,
                ActionType.Prestige => prestige,
                _ => null
            };

            if (fallback == null)
            {
                Debug.LogWarning(
                    $"[0916 Closure Presentation] {ownerLabel}/{skill.SkillName} fallback visual missing. " +
                    $"ActionType={meta.Type}");
                continue;
            }

            skill.SetPresentationAsset(fallback);
            EditorUtility.SetDirty(skill);
            wired++;
        }

        Debug.Log(
            $"[0916 Closure Presentation] {ownerLabel} fallback wired={wired}. " +
            "기존 complete PresentationAsset은 보존했습니다.");
        return wired;
    }

    private static SkillVisualDefinition LoadCompleteVisual(
        string path,
        string ownerLabel,
        ActionType type)
    {
        SkillVisualDefinition visual =
            AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(path);

        if (visual == null)
        {
            Debug.LogError(
                $"[0916 Closure Presentation] {ownerLabel}/{type} template missing: {path}");
            return null;
        }

        if (!visual.HasCompleteTimelineSet)
        {
            Debug.LogError(
                $"[0916 Closure Presentation] {ownerLabel}/{type} template incomplete: " +
                $"{visual.name} / Missing={string.Join(", ", visual.GetMissingRequirements())}",
                visual);
            return null;
        }

        return visual;
    }

    private static bool HasMissingCanonicalPresentation(CharacterCombatLoadout loadout)
    {
        if (loadout == null)
            return true;

        return loadout.EnumerateAllDefinitions()
            .Where(x => x != null)
            .Any(x =>
                x.PresentationAsset is not SkillVisualDefinition visual ||
                !visual.HasCompleteTimelineSet);
    }

    private static int SanitizeStage1BossLegacyBloodSplashVfx()
    {
        SkillVisualDefinition visual =
            AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(
                Stage1BossLegacyDuelVisualPath);
        BattleVfxDefinition legacyVfx =
            AssetDatabase.LoadAssetAtPath<BattleVfxDefinition>(
                LegacyOlafBloodSplashVfxPath);

        if (visual == null || legacyVfx == null)
            return 0;

        // 향후 실제 Prefab이 authoring되면 기존 참조를 보존한다.
        // 지금처럼 Prefab이 비어 있는 broken placeholder만 정리 대상이다.
        if (legacyVfx.EffectPrefab != null)
            return 0;

        int removed = 0;

        if (visual.VfxCues != null)
        {
            int before = visual.VfxCues.Count;
            visual.VfxCues.RemoveAll(cue =>
                cue != null && cue.Vfx == legacyVfx);
            int cueRemoved = before - visual.VfxCues.Count;
            if (cueRemoved > 0)
            {
                removed += cueRemoved;
                EditorUtility.SetDirty(visual);
            }
        }

        foreach (TimelineAsset timeline in EnumerateVisualTimelines(visual)
                     .Where(x => x != null)
                     .Distinct())
        {
            removed += RemoveLegacyVfxTimelineClips(
                timeline,
                legacyVfx);
        }

        if (removed > 0)
        {
            Debug.Log(
                $"[0916 Closure VFX] Stage1 boss legacy visual에서 " +
                $"Prefab 없는 {legacyVfx.name} 참조 {removed}개 제거. " +
                "Definition asset은 유지했습니다.",
                visual);
        }

        return removed;
    }

    private static bool HasStage1BossLegacyBloodSplashVfx()
    {
        SkillVisualDefinition visual =
            AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(
                Stage1BossLegacyDuelVisualPath);
        BattleVfxDefinition legacyVfx =
            AssetDatabase.LoadAssetAtPath<BattleVfxDefinition>(
                LegacyOlafBloodSplashVfxPath);

        if (visual == null || legacyVfx == null || legacyVfx.EffectPrefab != null)
            return false;

        if (visual.VfxCues != null &&
            visual.VfxCues.Any(cue => cue != null && cue.Vfx == legacyVfx))
        {
            return true;
        }

        return EnumerateVisualTimelines(visual)
            .Where(x => x != null)
            .Distinct()
            .Any(timeline =>
                EnumerateTracks(timeline)
                    .OfType<SkillVfxTimelineTrack>()
                    .SelectMany(track => track.GetClips())
                    .Any(clip =>
                        clip.asset is SkillVfxTimelineClip vfxClip &&
                        vfxClip.Definition == legacyVfx));
    }

    private static IEnumerable<TimelineAsset> EnumerateVisualTimelines(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (visual.ActionTimeline != null)
            yield return visual.ActionTimeline;
        if (visual.ClashAttackTimeline != null)
            yield return visual.ClashAttackTimeline;

        foreach (TimelineAsset legacy in visual.EnumerateLegacyTimelines())
        {
            if (legacy != null)
                yield return legacy;
        }
    }

    private static int RemoveLegacyVfxTimelineClips(
        TimelineAsset timeline,
        BattleVfxDefinition legacyVfx)
    {
        if (timeline == null || legacyVfx == null)
            return 0;

        int removed = 0;
        foreach (SkillVfxTimelineTrack track in
                 EnumerateTracks(timeline).OfType<SkillVfxTimelineTrack>())
        {
            TimelineClip[] clips = track.GetClips()
                .Where(clip =>
                    clip?.asset is SkillVfxTimelineClip vfxClip &&
                    vfxClip.Definition == legacyVfx)
                .ToArray();

            if (clips.Length == 0)
                continue;

            foreach (TimelineClip clip in clips)
            {
                track.DeleteClip(clip);
                removed++;
            }

            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
        }

        return removed;
    }

    private static IEnumerable<TrackAsset> EnumerateTracks(
        TimelineAsset timeline)
    {
        if (timeline == null)
            yield break;

        foreach (TrackAsset root in timeline.GetRootTracks())
        {
            foreach (TrackAsset track in EnumerateTrackTree(root))
                yield return track;
        }
    }

    private static IEnumerable<TrackAsset> EnumerateTrackTree(
        TrackAsset track)
    {
        if (track == null)
            yield break;

        yield return track;

        foreach (TrackAsset child in track.GetChildTracks())
        {
            foreach (TrackAsset descendant in EnumerateTrackTree(child))
                yield return descendant;
        }
    }

    private static int ApplyOlafC44CanonicalTextures(CharacterCombatLoadout loadout)
    {
        if (loadout == null)
            return 0;

        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                .GroupBy(skill => skill.SkillId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        int changedCount = 0;
        string legacyMarker = $"[{PhaseETempBalanceMigration.ProfileId}:C44_OVERRIDE]";
        const string canonicalMarker = "[CANONICAL_0916_C44]";

        foreach (OlafC44TextureSpec spec in OlafC44Textures)
        {
            if (!byId.TryGetValue(spec.SkillId, out SkillDefinition skill) || skill == null)
            {
                Debug.LogWarning($"[0916 Closure C44] Olaf skill missing: {spec.SkillId}");
                continue;
            }

            if (skill.Rolls == null ||
                spec.Min == null || spec.Max == null ||
                spec.Min.Length != spec.Max.Length ||
                skill.Rolls.Count != spec.Min.Length)
            {
                Debug.LogWarning(
                    $"[0916 Closure C44] roll shape mismatch / Skill={skill.SkillName} ({spec.SkillId}) " +
                    $"Actual={skill.Rolls?.Count ?? 0}, Expected={spec.Min?.Length ?? 0}");
                continue;
            }

            bool changed = false;
            for (int i = 0; i < spec.Min.Length; i++)
            {
                SkillRollData roll = skill.Rolls[i];
                if (roll == null)
                {
                    Debug.LogWarning(
                        $"[0916 Closure C44] null roll / Skill={skill.SkillName} ({spec.SkillId}) Index={i}");
                    continue;
                }

                int min = Mathf.Max(1, spec.Min[i]);
                int max = Mathf.Max(min, spec.Max[i]);
                if (roll.MinPower != min || roll.MaxPower != max)
                {
                    roll.MinPower = min;
                    roll.MaxPower = max;
                    changed = true;
                }
            }

            string description = skill.Description ?? string.Empty;
            if (description.IndexOf(legacyMarker, StringComparison.Ordinal) >= 0)
            {
                description = description.Replace(legacyMarker, string.Empty).Trim();
                changed = true;
            }

            if (description.IndexOf(canonicalMarker, StringComparison.Ordinal) < 0)
            {
                skill.Description =
                    $"{canonicalMarker} 0916(3) §4.3: 위치별 평균의 평균 = 기본위력 + 4.5.\n" +
                    description;
                changed = true;
            }
            else
            {
                skill.Description = description;
            }

            if (changed)
            {
                changedCount++;
                EditorUtility.SetDirty(skill);
            }
        }

        if (changedCount > 0)
        {
            Debug.Log(
                $"[0916 Closure C44] Olaf roll textures normalized: {changedCount}/{OlafC44Textures.Length}");
        }

        return changedCount;
    }

    private static bool HasOlafC44Mismatch(CharacterCombatLoadout loadout)
    {
        if (loadout == null)
            return true;

        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                .GroupBy(skill => skill.SkillId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        string legacyMarker = $"[{PhaseETempBalanceMigration.ProfileId}:C44_OVERRIDE]";

        foreach (OlafC44TextureSpec spec in OlafC44Textures)
        {
            if (!byId.TryGetValue(spec.SkillId, out SkillDefinition skill) ||
                skill?.Rolls == null ||
                spec.Min == null || spec.Max == null ||
                spec.Min.Length != spec.Max.Length ||
                skill.Rolls.Count != spec.Min.Length)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(skill.Description) &&
                skill.Description.IndexOf(legacyMarker, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            for (int i = 0; i < spec.Min.Length; i++)
            {
                SkillRollData roll = skill.Rolls[i];
                if (roll == null ||
                    roll.MinPower != spec.Min[i] ||
                    roll.MaxPower != spec.Max[i])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ClearSecondUpgradeCosts(CharacterCombatLoadout loadout)
    {
        if (loadout == null)
            return;

        foreach (SkillDefinition skill in loadout.EnumerateAllDefinitions().Where(x => x != null).Distinct())
        {
            if (skill.UpgradeProfile?.Upgrade2 == null)
                continue;
            skill.UpgradeProfile.Upgrade2.CostOverride = 0;
            EditorUtility.SetDirty(skill.UpgradeProfile);
        }
    }

    /// <summary>
    /// 정본 계약: Yujin 도사림 장착 3장은 노림수 + 선금 받기 + 환형 1종.
    /// 공격/수비 중 어느 쪽으로 시작하는지는 미정이므로, 이미 유효한 장착은 보존하고
    /// 잘못된/legacy starter에만 TEMP 기본값인 환형(공격)을 넣는다.
    /// </summary>
    private static void EnsureYujinStarterPreparationLoadout(
        CharacterCombatLoadout loadout)
    {
        if (loadout == null || HasCanonicalYujinStarterPreparationLoadout(loadout))
            return;

        Dictionary<string, SkillDefinition> byId =
            loadout.EnumerateAllDefinitions()
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                .GroupBy(skill => skill.SkillId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        string[] starterIds =
        {
            YujinSkillIds.Read,
            YujinSkillIds.AdvancePay,
            YujinSkillIds.HwanhyeongAttack
        };

        List<SkillDefinition> starter = starterIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();

        if (starter.Count != starterIds.Length)
        {
            Debug.LogWarning(
                "[0916 Closure] Yujin 환형 starter 복구 실패. 누락 SkillId=" +
                string.Join(", ", starterIds.Where(id => !byId.ContainsKey(id))));
            return;
        }

        loadout.PreparationSkills = starter;
        EditorUtility.SetDirty(loadout);
        Debug.Log(
            "[0916 Closure] Yujin starter 도사림 복구: 노림수 / 선금 받기 / 환형(공격) " +
            "(환형 공격/수비 모두 후보 pool 유지)");
    }

    private static bool HasCanonicalYujinStarterPreparationLoadout(
        CharacterCombatLoadout loadout)
    {
        if (loadout?.PreparationSkills == null)
            return false;

        HashSet<string> equipped =
            new HashSet<string>(
                loadout.PreparationSkills
                    .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                    .Select(skill => skill.SkillId),
                StringComparer.Ordinal);

        int hwanhyeongCount = 0;
        if (equipped.Contains(YujinSkillIds.HwanhyeongAttack))
            hwanhyeongCount++;
        if (equipped.Contains(YujinSkillIds.HwanhyeongDefense))
            hwanhyeongCount++;

        return equipped.Count == 3 &&
               equipped.Contains(YujinSkillIds.Read) &&
               equipped.Contains(YujinSkillIds.AdvancePay) &&
               hwanhyeongCount == 1;
    }

    private static bool NeedsApply(
        CharacterCombatLoadout olaf,
        CharacterCombatLoadout yujin)
    {
        HashSet<string> olafIds = IdSet(olaf);
        HashSet<string> yujinIds = IdSet(yujin);

        bool olafClosed =
            new HashSet<string>(
                OlafSkillIds.CanonicalNormal
                    .Concat(OlafSkillIds.CanonicalDuel)
                    .Concat(OlafSkillIds.CanonicalPreparation)
                    .Concat(OlafSkillIds.CanonicalPrestige),
                StringComparer.Ordinal).SetEquals(olafIds);

        bool yujinClosed =
            new HashSet<string>(
                YujinSkillIds.CanonicalNormal
                    .Concat(YujinSkillIds.CanonicalDuel)
                    .Concat(YujinSkillIds.CanonicalPreparation)
                    .Concat(YujinSkillIds.CanonicalPrestige),
                StringComparer.Ordinal).SetEquals(yujinIds);

        if (!olafClosed || !yujinClosed)
            return true;

        // 0917: legacy C44 exact-array check is intentionally disabled.
        // Specific card data and the 0917 verifier own the known C44-vs-card authority conflict.

        if (!HasCanonicalYujinStarterPreparationLoadout(yujin))
            return true;

        if (HasMissingCanonicalPresentation(olaf) ||
            HasMissingCanonicalPresentation(yujin))
        {
            return true;
        }

        if (HasStage1BossLegacyBloodSplashVfx())
            return true;

        if (olaf.EnumerateAllDefinitions().Concat(yujin.EnumerateAllDefinitions())
            .Where(x => x?.UpgradeProfile?.Upgrade2 != null)
            .Any(x => x.UpgradeProfile.Upgrade2.CostOverride != 0))
            return true;

        SkillDefinition ledger = yujin.EnumerateAllDefinitions()
            .FirstOrDefault(x => x?.SkillId == YujinSkillIds.LedgerCleanup);
        return ledger == null ||
               ledger.EffectEntries == null ||
               !ledger.EffectEntries.Any(x => x?.Definition is Yujin0916SkillEffectDefinition);
    }

    private static HashSet<string> IdSet(CharacterCombatLoadout loadout) =>
        new HashSet<string>(
            loadout?.EnumerateAllDefinitions()
                .Where(x => x != null)
                .Select(x => x.SkillId) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

    private static bool HasSkillNamed(CharacterCombatLoadout loadout, string name) =>
        loadout?.EnumerateAllDefinitions()
            .Any(x => x != null && string.Equals(x.SkillName, name, StringComparison.Ordinal)) == true;

    private static OlafC44TextureSpec C44(string skillId, int[] min, int[] max) =>
        new OlafC44TextureSpec(skillId, min, max);

    private static SkillMeta M(
        string name,
        string id,
        ActionType type,
        SkillColor color,
        int energy,
        int prestigeThreshold = 0,
        PreparationTier preparationTier = PreparationTier.Strong) =>
        new SkillMeta(name, id, type, color, energy, prestigeThreshold, preparationTier);

    private static void SetSkillId(SkillDefinition definition, string id)
    {
        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty property = serialized.FindProperty("skillId");
        if (property != null)
            property.stringValue = id;
        SerializedProperty schema = serialized.FindProperty("phaseASchemaVersion");
        if (schema != null)
            schema.intValue = SkillDefinition.CurrentPhaseASchemaVersion;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T EnsureAsset<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        string directory = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(directory);
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "effect";
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
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