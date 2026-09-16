#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase E 임시 폐쇄용 콘텐츠 이관.
/// 0916 정본/XLSX에서 비어 있는 설계를 TEMP_BALANCE_V1로 명시적으로 격리한다.
/// 정식 기획 확정 뒤 이 파일이 만든 TEMP 폴더/효과만 교체하면 canonical data는 유지된다.
/// </summary>
public static class PhaseETempBalanceMigration
{
    public const string ProfileId = "TEMP_BALANCE_V1";

    private const string Root = "Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1";
    private const string CommonSkillRoot = Root + "/Skills/Common";
    private const string OlafSkillRoot = Root + "/Skills/Olaf";
    private const string YujinSkillRoot = Root + "/Skills/Yujin";
    private const string ItemRoot = "Assets/2. Data/Progression/RunItems/TEMP_BALANCE_V1";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";

    private sealed class DiceSkillRow
    {
        public string Id;
        public string Name;
        public ActionType ActionType;
        public SkillColor Color;
        public int Energy;
        public int MaxBasePower;
        public int[] Min;
        public int[] Max;
        public bool CanBreak;
        public bool C44Override;
        public TempBalanceSkillProxyOperation Proxy;
        public SkillEffectTiming Timing;
        public int MaxProxyAmount;
    }

    private sealed class SimpleSkillRow
    {
        public string Id;
        public string Name;
        public ActionType ActionType;
        public SkillColor Color;
        public int Energy;
        public PreparationTier PreparationTier;
        public TempBalanceSkillProxyOperation Proxy;
        public SkillEffectTiming Timing;
        public int BaseProxyAmount;
        public int MaxProxyAmount;
        public string Note;
    }

    public static void Apply(
        EmotionAugmentCatalog emotionCatalog,
        RunItemCatalog itemCatalog,
        PhaseEContentManifest manifest)
    {
        EnsureFolder(Root);
        CompleteEmotionRuntime(emotionCatalog, manifest);
        GenerateItems(itemCatalog, manifest);
        GenerateCommonSkills(out List<SkillDefinition> commonPreparations);
        bool olaf = GenerateOlaf(commonPreparations, manifest);
        bool yujin = GenerateYujin(commonPreparations, manifest);

        manifest.TempBalanceActive = true;
        manifest.ActiveBalanceProfile = ProfileId;
        manifest.UpgradeDecompositionComplete = olaf && yujin;
        manifest.OlafSkillPoolComplete = olaf;
        manifest.YujinSkillPoolComplete = yujin;
        manifest.PendingReasons ??= new List<string>();
        manifest.PendingReasons.Clear();
        manifest.TempBalanceNotes ??= new List<string>();
        manifest.TempBalanceNotes.Clear();
        manifest.TempBalanceNotes.Add(
            "C-38 TEMP: 공격 Dice는 XLSX 강화2 값을 보존하고 Base=max-2, U1 +1, U2 +1로 역산.");
        manifest.TempBalanceNotes.Add(
            "C-38 TEMP: 2회차 강화비 Normal 75 / Preparation 150 / Duel 200 / Prestige 225.");
        manifest.TempBalanceNotes.Add(
            "Yujin TEMP: 무기 코인 공식은 Phase D canonical runtime을 유지하고 카드 rider만 단계 강화.");
        manifest.TempBalanceNotes.Add(
            "C-32/C-33: 정식 설계가 없는 효과/아이템은 공통 flat runtime proxy로 연결. 정본 확정 시 교체 대상.");
        manifest.TempBalanceNotes.Add(
            "C-44: XLSX 자체가 평균 불변식과 어긋나는 위치 텍스처는 값을 수정하지 않고 explicit override로 기록.");
        manifest.TempBalanceNotes.Add(
            "Phase F v2 hotfix: 블로토는 Phase D native hook만 사용하며 비용 1, 광기 +1 단일 적용. TEMP proxy 중복 제거.");

        EditorUtility.SetDirty(manifest);
    }

    // ---------------------------------------------------------------------
    // C-32 — 63 emotions
    // ---------------------------------------------------------------------

    private static void CompleteEmotionRuntime(
        EmotionAugmentCatalog catalog,
        PhaseEContentManifest manifest)
    {
        manifest.TempEmotionProxyCount = 0;
        if (catalog?.Entries == null)
            return;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            EmotionAugmentDefinition definition = catalog.Entries[i];
            if (definition == null ||
                definition.RuntimeReadiness == EmotionAugmentRuntimeReadiness.RuntimeConnected)
            {
                continue;
            }

            int indexInTier = catalog.Entries
                .Take(i + 1)
                .Count(x => x != null && x.Emotion == definition.Emotion && x.Tier == definition.Tier);

            string folder =
                $"Assets/2. Data/Progression/EmotionAugments/Canonical0916/{definition.Emotion}/Tier{definition.Tier}/Effects";
            EnsureFolder(folder);

            string path =
                $"{folder}/T{definition.Tier}_{indexInTier:00}_{ProfileId}_Proxy.asset";
            TempBalanceEmotionProxyEffectDefinition proxy =
                EnsureAsset<TempBalanceEmotionProxyEffectDefinition>(path);
            ConfigureEmotionProxy(proxy, definition.Emotion, definition.Tier, indexInTier);
            EditorUtility.SetDirty(proxy);

            definition.Effects ??= new List<EmotionAugmentEffectDefinition>();
            definition.Effects.Add(proxy);
            definition.RuntimeReadiness = EmotionAugmentRuntimeReadiness.RuntimeConnected;
            definition.RuntimeNote =
                $"[{ProfileId}] 정식 전용 hook 확정 전 flat proxy 연결. 기존 이름/설명/DesignStatus는 0916 원본 보존.";
            EditorUtility.SetDirty(definition);
            manifest.TempEmotionProxyCount++;
        }
    }

    private static void ConfigureEmotionProxy(
        TempBalanceEmotionProxyEffectDefinition proxy,
        EmotionType emotion,
        int tier,
        int index)
    {
        proxy.GainEnergy = 0;
        proxy.GainPrestige = 0;
        proxy.GainBlock = 0;
        proxy.RecoverStaggerRatio = 0f;
        proxy.RollBonus = 0;
        proxy.DamageDealtFlatBonus = 0;
        proxy.DamageTakenFlatReduction = 0;
        proxy.PositiveMomentumBonus = 0;
        proxy.PrestigeGainFlatBonus = 0;

        // 정식 효과가 아닌 임시 전투가치 proxy. 곱연산은 사용하지 않는다.
        switch (emotion)
        {
            case EmotionType.Compassion:
                if (index == 1) proxy.RecoverStaggerRatio = 0.02f * tier;
                else if (index == 2) proxy.GainBlock = 4 * tier;
                else proxy.DamageTakenFlatReduction = Mathf.Min(2, tier);
                break;

            case EmotionType.Faith:
                proxy.GainBlock = 3 * tier + index;
                if (tier >= 2 && index == 3)
                    proxy.DamageTakenFlatReduction = 1;
                break;

            case EmotionType.Detachment:
                proxy.DamageTakenFlatReduction = tier >= 3 ? 2 : 1;
                if (index == 2) proxy.RecoverStaggerRatio = 0.02f * tier;
                break;

            case EmotionType.Impression:
                // 경제 보상은 전투 종료/런 계층 전용 구현이 생기기 전까지 기세 proxy로만 둔다.
                proxy.PositiveMomentumBonus = tier + index;
                break;

            case EmotionType.Awe:
                if (tier == 1 && index == 1) proxy.RollBonus = 1;
                else proxy.PositiveMomentumBonus = 2 * tier;
                break;

            case EmotionType.Admiration:
                proxy.PrestigeGainFlatBonus = tier;
                if (index == 2) proxy.GainPrestige = 5 * tier;
                break;

            case EmotionType.Longing:
                proxy.PositiveMomentumBonus = 2 * tier + index;
                if (tier >= 2 && index == 3) proxy.GainBlock = 4 * tier;
                break;
        }
    }

    // ---------------------------------------------------------------------
    // C-33 — 30 common + 10 x 3 class items
    // ---------------------------------------------------------------------

    private static void GenerateItems(
        RunItemCatalog catalog,
        PhaseEContentManifest manifest)
    {
        if (catalog == null)
            return;

        catalog.Items ??= new List<RunItemDefinition>();
        catalog.Items.Clear();

        manifest.TempCommonItemCount = 0;
        manifest.TempClassItemCount = 0;

        for (int tier = 1; tier <= 5; tier++)
        {
            for (int index = 1; index <= 6; index++)
            {
                RunItemDefinition item = CreateTempItem(
                    $"common.t{tier}.{index:00}",
                    $"TEMP 공용 T{tier}-{index:00}",
                    (RunItemTier)tier,
                    string.Empty,
                    tier,
                    index,
                    false);
                catalog.Items.Add(item);
                manifest.TempCommonItemCount++;
            }
        }

        string[] characters = { "Olaf", "Yujin", "Hifumi" };
        foreach (string character in characters)
        {
            for (int index = 1; index <= 10; index++)
            {
                RunItemDefinition item = CreateTempItem(
                    $"class.{character.ToLowerInvariant()}.{index:00}",
                    $"TEMP {character} 직업-{index:00}",
                    RunItemTier.Class,
                    character,
                    4,
                    index,
                    true);
                catalog.Items.Add(item);
                manifest.TempClassItemCount++;
            }
        }

        EditorUtility.SetDirty(catalog);
    }

    private static RunItemDefinition CreateTempItem(
        string suffix,
        string displayName,
        RunItemTier tier,
        string characterKey,
        int strength,
        int index,
        bool classItem)
    {
        string group = classItem ? $"Class/{characterKey}" : $"Common/T{strength}";
        string folder = $"{ItemRoot}/{group}";
        EnsureFolder(folder + "/Combat");

        string safe = suffix.Replace('.', '_');
        RunItemDefinition item =
            EnsureAsset<RunItemDefinition>($"{folder}/{safe}.asset");
        TempBalanceRunItem combat =
            EnsureAsset<TempBalanceRunItem>($"{folder}/Combat/{safe}_Combat.asset");

        ConfigureRunItemCombat(combat, strength, index, characterKey);
        EditorUtility.SetDirty(combat);

        item.ItemId = $"temp_balance_v1.{suffix}";
        item.DisplayName = displayName;
        item.Tier = tier;
        item.CharacterKey = characterKey ?? string.Empty;
        item.Description =
            $"[{ProfileId}] 정식 60종 아이템 설계 확정 전 임시 flat 전투 효과. " +
            "가격/등장확률/판매율은 canonical ItemEconomySettings 사용.";
        item.CombatItem = combat;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void ConfigureRunItemCombat(
        TempBalanceRunItem item,
        int strength,
        int index,
        string character)
    {
        item.MaxEnergyBonus = 0;
        item.MaxSpeedBonus = 0;
        item.RollBonus = 0;
        item.DamageDealtFlatBonus = 0;
        item.DamageTakenFlatReduction = 0;
        item.PositiveMomentumBonus = 0;
        item.PrestigeGainFlatBonus = 0;

        int pattern = (index - 1) % 6;
        int safeStrength = Mathf.Clamp(strength, 1, 5);

        switch (pattern)
        {
            case 0:
                item.DamageDealtFlatBonus = safeStrength;
                break;
            case 1:
                item.DamageTakenFlatReduction = Mathf.Max(1, (safeStrength + 1) / 2);
                break;
            case 2:
                item.PositiveMomentumBonus = safeStrength * 2;
                break;
            case 3:
                item.PrestigeGainFlatBonus = Mathf.Max(1, safeStrength - 1);
                break;
            case 4:
                item.MaxSpeedBonus = safeStrength >= 3 ? 1 : 0;
                item.DamageDealtFlatBonus = safeStrength >= 3 ? 1 : 0;
                break;
            case 5:
                item.RollBonus = safeStrength >= 4 ? 1 : 0;
                item.MaxEnergyBonus = safeStrength >= 5 ? 1 : 0;
                break;
        }

        // 직업 아이템은 같은 900G Class bucket 안에서 캐릭터 결만 조금 강화한다.
        if (string.Equals(character, "Olaf", StringComparison.OrdinalIgnoreCase))
        {
            item.DamageDealtFlatBonus += (index % 3 == 0) ? 1 : 0;
            item.PositiveMomentumBonus += (index % 3 == 1) ? 2 : 0;
        }
        else if (string.Equals(character, "Yujin", StringComparison.OrdinalIgnoreCase))
        {
            item.MaxSpeedBonus += (index % 4 == 0) ? 1 : 0;
            item.PrestigeGainFlatBonus += (index % 3 == 0) ? 1 : 0;
        }
        else if (string.Equals(character, "Hifumi", StringComparison.OrdinalIgnoreCase))
        {
            item.DamageTakenFlatReduction += (index % 4 == 0) ? 1 : 0;
            item.PositiveMomentumBonus += (index % 3 == 0) ? 2 : 0;
        }
    }

    // ---------------------------------------------------------------------
    // Common preparations
    // ---------------------------------------------------------------------

    private static void GenerateCommonSkills(out List<SkillDefinition> common)
    {
        EnsureFolder(CommonSkillRoot);
        common = new List<SkillDefinition>();

        SkillDefinition crouch = ConfigureSimpleSkill(
            CommonSkillRoot, "Common_Crouch", new SimpleSkillRow
            {
                Id = "common.preparation.crouch",
                Name = "웅크리기",
                ActionType = ActionType.Preparation,
                Color = SkillColor.Blue,
                Energy = 0,
                PreparationTier = PreparationTier.Weak,
                Proxy = TempBalanceSkillProxyOperation.GainBlock,
                Timing = SkillEffectTiming.OnExecute,
                BaseProxyAmount = 8,
                MaxProxyAmount = 12,
                Note = "canonical max: 방어도 +12"
            }, 150);
        common.Add(crouch);

        SkillDefinition glare = ConfigureSimpleSkill(
            CommonSkillRoot, "Common_Glare", new SimpleSkillRow
            {
                Id = "common.preparation.glare",
                Name = "노려보기",
                ActionType = ActionType.Preparation,
                Color = SkillColor.Blue,
                Energy = 1,
                PreparationTier = PreparationTier.Weak,
                Proxy = TempBalanceSkillProxyOperation.TurnClashPowerBonus,
                Timing = SkillEffectTiming.OnExecute,
                BaseProxyAmount = 1,
                MaxProxyAmount = 1,
                Note = "canonical max: 그 턴 합 판정값 +1; 강화 rider는 값 유지"
            }, 150);
        common.Add(glare);
    }

    // ---------------------------------------------------------------------
    // O-02 Olaf 16 / 21 / 9 / 3
    // ---------------------------------------------------------------------

    private static bool GenerateOlaf(
        List<SkillDefinition> commonPreparations,
        PhaseEContentManifest manifest)
    {
        EnsureFolder(OlafSkillRoot);
        List<SkillDefinition> normals = new();
        List<SkillDefinition> duels = new();
        List<SkillDefinition> preps = new(commonPreparations);
        List<SkillDefinition> prestiges = new();

        DiceSkillRow[] normalRows = BuildOlafNormals();
        for (int i = 0; i < normalRows.Length; i++)
            normals.Add(ConfigureDiceSkill(OlafSkillRoot + "/Normal", $"Olaf_N_{i + 1:00}", normalRows[i]));

        DiceSkillRow[] duelRows = BuildOlafDuels();
        for (int i = 0; i < duelRows.Length; i++)
            duels.Add(ConfigureDiceSkill(OlafSkillRoot + "/Duel", $"Olaf_D_{i + 1:00}", duelRows[i]));

        SimpleSkillRow[] prepRows =
        {
            S(OlafSkillIds.Bloto, "블로토", ActionType.Preparation, SkillColor.Blue, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.OlafMadness, 1, 1, "Phase D native hook 전용 — TEMP proxy 중복 적용 금지"),
            S("olaf.preparation.blood_oath", "피의 서약", ActionType.Preparation, SkillColor.Red, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.TargetBleeding, 1, 3, "출혈 계열"),
            S("olaf.preparation.breath_bought_with_blood", "피로 사는 숨", ActionType.Preparation, SkillColor.Blue, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.GainPrestige, 2, 4, "빛 환율 정식 hook 전 위세 proxy — 무료 빛 생성 금지"),
            S("olaf.preparation.calm_madness", "광기를 가라앉히다", ActionType.Preparation, SkillColor.Blue, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.GainBlock, 8, 12, "광기 소비 정식 hook 전 방어 proxy"),
            S("olaf.preparation.erase_wound", "상처를 지우다", ActionType.Preparation, SkillColor.Blue, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.GainBlock, 8, 12, "회복 정식 hook 전 방어 proxy"),
            S("olaf.preparation.devour_stagger", "흐트러짐을 삼키다", ActionType.Preparation, SkillColor.Blue, 1, PreparationTier.Strong, TempBalanceSkillProxyOperation.GainPrestige, 2, 4, "흐트러짐 교환 정식 hook 전 위세 proxy"),
            S("olaf.preparation.last_resistance", "최후의 저항", ActionType.Preparation, SkillColor.Red, 2, PreparationTier.Strong, TempBalanceSkillProxyOperation.OlafMadness, 1, 3, "위기형 TEMP rider")
        };
        for (int i = 0; i < prepRows.Length; i++)
            preps.Add(ConfigureSimpleSkill(OlafSkillRoot + "/Preparation", $"Olaf_P_{i + 1:00}", prepRows[i], 150));

        SimpleSkillRow[] prestigeRows =
        {
            S(OlafSkillIds.BloomingWound, "룬 새기기", ActionType.Prestige, SkillColor.Red, 0, PreparationTier.Weak, TempBalanceSkillProxyOperation.TargetBleeding, 1, 3, "기존 BloomingWound ID/hook 재사용"),
            S(OlafSkillIds.BurstingMadness, "베르세르크", ActionType.Prestige, SkillColor.Red, 0, PreparationTier.Weak, TempBalanceSkillProxyOperation.OlafMadness, 1, 3, "기존 BurstingMadness ID/hook 재사용"),
            S(OlafSkillIds.BacksToWall, "라그나로크", ActionType.Prestige, SkillColor.Blue, 0, PreparationTier.Weak, TempBalanceSkillProxyOperation.GainBlock, 8, 16, "기존 BacksToWall ID/hook 재사용")
        };
        for (int i = 0; i < prestigeRows.Length; i++)
            prestiges.Add(ConfigureSimpleSkill(OlafSkillRoot + "/Prestige", $"Olaf_R_{i + 1:00}", prestigeRows[i], 225));

        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);
        if (loadout == null)
            return false;

        ApplyLoadout(loadout, normals, duels, commonPreparations, preps.Skip(2).ToList(), prestiges);
        manifest.OlafPoolCount = normals.Count + duels.Count + preps.Count + prestiges.Count;
        return normals.Count == 16 && duels.Count == 21 && preps.Count == 9 && prestiges.Count == 3 &&
               ValidateTempUpgradeProfiles(normals.Concat(duels).Concat(preps).Concat(prestiges));
    }

    private static DiceSkillRow[] BuildOlafNormals()
    {
        return new[]
        {
            D(OlafSkillIds.EnduringSlash,"물어뜯기",ActionType.NormalAttack,SkillColor.Red,0,11,new[]{9,10,10},new[]{20,21,21},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D(OlafSkillIds.OverheadSmash,"방패벽",ActionType.NormalAttack,SkillColor.Blue,0,11,new[]{12,12,10},new[]{15,19,28},false,true,TempBalanceSkillProxyOperation.GainBlock),
            D(OlafSkillIds.WildHack,"쪼개기",ActionType.NormalAttack,SkillColor.Red,1,16,new[]{17,17},new[]{22,26},false,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.endure","버텨내기",ActionType.NormalAttack,SkillColor.Blue,1,14,new[]{22,7},new[]{27,18},false,false,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.normal.smash","후려치기",ActionType.NormalAttack,SkillColor.Red,0,12,new[]{13,16},new[]{18,23},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.parry","받아넘기기",ActionType.NormalAttack,SkillColor.Blue,0,14,new[]{15,16},new[]{22,21},false,false,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.normal.flurry","몰아치기",ActionType.NormalAttack,SkillColor.Red,1,10,new[]{15,13,12,10,8},new[]{22,19,17,16,14},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.rise_again","다시 일어서다",ActionType.NormalAttack,SkillColor.Blue,1,7,new[]{13,3,12,4},new[]{18,10,19,11},false,true,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.normal.desperate_blow","필사의 일격",ActionType.NormalAttack,SkillColor.Red,2,24,new[]{26},new[]{31},false,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.cover","감싸기",ActionType.NormalAttack,SkillColor.Blue,2,16,new[]{22,14},new[]{29,21},false,true,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.normal.exploit_gap","틈을 파고들다",ActionType.NormalAttack,SkillColor.Red,1,14,new[]{13,15,17},new[]{18,22,29},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.first_strike","선수치기",ActionType.NormalAttack,SkillColor.Red,0,12,new[]{17,12},new[]{22,17},false,true,TempBalanceSkillProxyOperation.GainPrestige),
            D("olaf.normal.aim_weakness","약점을 노리다",ActionType.NormalAttack,SkillColor.Red,1,13,new[]{12,18,13},new[]{17,25,20},false,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.choke","숨통을 조이다",ActionType.NormalAttack,SkillColor.Red,1,16,new[]{17,18},new[]{24,23},false,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.normal.block_way","막아서기",ActionType.NormalAttack,SkillColor.Blue,1,14,new[]{18,13},new[]{25,20},false,true,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.normal.slice_thin","저며내기",ActionType.NormalAttack,SkillColor.Red,1,17,new[]{16,20},new[]{19,32},false,true,TempBalanceSkillProxyOperation.TargetBleeding)
        };
    }

    private static DiceSkillRow[] BuildOlafDuels()
    {
        return new[]
        {
            D(OlafSkillIds.Standard,"표준",ActionType.Duel,SkillColor.Red,2,12,new[]{13,14},new[]{20,19},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D(OlafSkillIds.Rend,"끈질기게",ActionType.Duel,SkillColor.Blue,2,14,new[]{15,16,15},new[]{22,21,22},false,false,TempBalanceSkillProxyOperation.OlafMadness),
            D("olaf.duel.single_cut","단칼",ActionType.Duel,SkillColor.Red,1,22,new[]{22},new[]{27},true,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.mad_bite","미쳐물어뜯기",ActionType.Duel,SkillColor.Blue,1,13,new[]{14,14,14},new[]{21,21,21},false,false,TempBalanceSkillProxyOperation.OlafMadness),
            D("olaf.duel.blood_charge","피의돌진",ActionType.Duel,SkillColor.Red,1,14,new[]{16,12},new[]{23,19},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.hold_on","물고 늘어지기",ActionType.Duel,SkillColor.Blue,1,13,new[]{12,14,16},new[]{19,21,23},false,false,TempBalanceSkillProxyOperation.OlafMadness),
            D("olaf.duel.self_cut","제살깎기",ActionType.Duel,SkillColor.Blue,2,16,new[]{17,18},new[]{24,23},false,false,TempBalanceSkillProxyOperation.OlafMadness),
            D("olaf.duel.no_retreat","물러서지않기",ActionType.Duel,SkillColor.Red,1,14,new[]{12,13},new[]{25,24},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.head_on","정면승부",ActionType.Duel,SkillColor.Red,0,15,new[]{16,17},new[]{23,22},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.stoke_prestige","위세를 지피다",ActionType.Duel,SkillColor.Blue,1,14,new[]{15,16},new[]{22,21},false,false,TempBalanceSkillProxyOperation.GainPrestige),
            D("olaf.duel.fortify","굳히기",ActionType.Duel,SkillColor.Blue,2,15,new[]{16,17,16},new[]{23,22,23},false,false,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.duel.protect_self","몸을 사리다",ActionType.Duel,SkillColor.Blue,2,16,new[]{17,18},new[]{24,23},false,false,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.duel.catch_off_guard","허를 찌르다",ActionType.Duel,SkillColor.Red,0,12,new[]{13,14},new[]{20,19},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.all_in","전부 걸다",ActionType.Duel,SkillColor.Red,2,14,new[]{12,15,19},new[]{19,22,26},false,true,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.blood_price","피의 대가",ActionType.Duel,SkillColor.Blue,2,16,new[]{14,21},new[]{19,28},false,false,TempBalanceSkillProxyOperation.OlafMadness),
            D("olaf.duel.absorb_blood","피를 흡수하다",ActionType.Duel,SkillColor.Blue,1,14,new[]{14,15},new[]{21,20},false,true,TempBalanceSkillProxyOperation.GainBlock),
            D("olaf.duel.rampage","닥치는 대로",ActionType.Duel,SkillColor.Red,4,16,new[]{17,18,17,18,17},new[]{24,23,24,23,24},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.honorable_combat","명예로운 전투",ActionType.Duel,SkillColor.Red,3,16,new[]{17},new[]{23},true,true,TempBalanceSkillProxyOperation.GainPrestige),
            D("olaf.duel.blood_vow","피의 맹세",ActionType.Duel,SkillColor.Red,1,12,new[]{13,14},new[]{20,19},true,false,TempBalanceSkillProxyOperation.TargetBleeding),
            D("olaf.duel.repay_all","전부 되갚다",ActionType.Duel,SkillColor.Red,1,14,new[]{15,16},new[]{22,21},false,false,TempBalanceSkillProxyOperation.GainPrestige),
            D("olaf.duel.single_combat","일기토",ActionType.Duel,SkillColor.Red,1,13,new[]{14,15,14},new[]{21,20,21},true,false,TempBalanceSkillProxyOperation.TargetBleeding)
        };
    }

    // ---------------------------------------------------------------------
    // Y-06 Yujin 9 / 14 / 9 / 3
    // ---------------------------------------------------------------------

    private static bool GenerateYujin(
        List<SkillDefinition> commonPreparations,
        PhaseEContentManifest manifest)
    {
        EnsureFolder(YujinSkillRoot);
        List<SkillDefinition> normals = new();
        List<SkillDefinition> duels = new();
        List<SkillDefinition> preps = new(commonPreparations);
        List<SkillDefinition> prestiges = new();

        SimpleSkillRow[] normalRows =
        {
            S(YujinSkillIds.Inspection,"뒷조사",ActionType.NormalAttack,SkillColor.Red,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,6,"기존 Inspection 무기별 표식 hook 재사용"),
            S(YujinSkillIds.Breakfast,"숨 고르기",ActionType.NormalAttack,SkillColor.Blue,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,1,"기존 Sense hook 재사용"),
            S("yujin.normal.ledger_cleanup","장부 정리",ActionType.NormalAttack,SkillColor.Red,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,2,"표식20 소비→빛 효과는 정식 hook 전 감 proxy — 무료 빛 생성 금지"),
            S("yujin.normal.step_back","한 발 물러서다",ActionType.NormalAttack,SkillColor.Blue,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainBlock,6,10,"canonical max 방어도 +10"),
            S("yujin.normal.aim","정조준",ActionType.NormalAttack,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,4,"무기별 크리 rider TEMP"),
            S("yujin.normal.confirm_kill","확인 사살",ActionType.NormalAttack,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainPrestige,2,4,"지정 기세 효과 TEMP"),
            S("yujin.normal.familiar_hand","손에 익히다",ActionType.NormalAttack,SkillColor.Blue,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,1,"무기교체 후 감각 TEMP"),
            S("yujin.normal.ace_in_hole","믿는 구석",ActionType.NormalAttack,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainPrestige,2,4,"감 소비 강제크리 정식 hook 전 TEMP"),
            S("yujin.normal.wanted","현상수배",ActionType.NormalAttack,SkillColor.Blue,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,4,"광역 무기별 debuff 정식 hook 전 TEMP")
        };
        for (int i = 0; i < normalRows.Length; i++)
            normals.Add(ConfigureYujinSkill(YujinSkillRoot + "/Normal", $"Yujin_N_{i + 1:00}", normalRows[i], 75));

        SimpleSkillRow[] duelRows =
        {
            S(YujinSkillIds.Inscription,"의뢰 대상",ActionType.Duel,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,6,"기존 Inscription hook"),
            S(YujinSkillIds.Pursuit,"잔혹한 마무리",ActionType.Duel,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,2,"기존 Pursuit hook"),
            S("yujin.duel.c.designate","목표 지정",ActionType.Duel,SkillColor.Blue,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,5,"지정 proxy"),
            S("yujin.duel.d.shadow","그림자 속으로",ActionType.Duel,SkillColor.Blue,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainBlock,9,15,"방어 proxy"),
            S("yujin.duel.e.shake","흔들어놓다",ActionType.Duel,SkillColor.Blue,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,2,"감 proxy"),
            S("yujin.duel.f.finish","숨통을 끊다",ActionType.Duel,SkillColor.Red,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,3,6,"부위 조건 hook 전 표식 proxy"),
            S("yujin.duel.g.spread_word","소문을 내다",ActionType.Duel,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainPrestige,2,5,"광역 rider TEMP"),
            S("yujin.duel.h.probe_weakness","약점을 캐내다",ActionType.Duel,SkillColor.Blue,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,4,"무기 debuff TEMP"),
            S("yujin.duel.i.block_escape","퇴로 차단",ActionType.Duel,SkillColor.Red,1,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainPrestige,2,4,"조건부 빛은 정식 대가 hook 전 위세 proxy"),
            S("yujin.duel.j.borrow","빌려 쓰다",ActionType.Duel,SkillColor.Blue,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,2,"무기 차용 정식 hook 전 TEMP"),
            S("yujin.duel.k.trap","덫을 놓다",ActionType.Duel,SkillColor.Red,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMarkTrap,12,20,"MarkTrap 기존 런타임 API"),
            S("yujin.duel.l.deadline","시한을 정하다",ActionType.Duel,SkillColor.Red,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMarkDeadline,1,1,"3턴/2배 deadline 기존 API"),
            S("yujin.duel.m.all_targets","모두를 노리다",ActionType.Duel,SkillColor.Blue,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,2,5,"광역 지정 정식 hook 전 현재대상 proxy"),
            S("yujin.duel.n.period","마침표를 찍다",ActionType.Duel,SkillColor.Red,2,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,3,"종결 rider TEMP")
        };
        for (int i = 0; i < duelRows.Length; i++)
            duels.Add(ConfigureYujinSkill(YujinSkillRoot + "/Duel", $"Yujin_D_{i + 1:00}", duelRows[i], 200));

        SimpleSkillRow[] prepRows =
        {
            S(YujinSkillIds.Capture,"노림수",ActionType.Preparation,SkillColor.Red,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.GainPrestige,2,4,"기존 Capture 앞면 확률 hook"),
            S(YujinSkillIds.Sentencing,"선금 받기",ActionType.Preparation,SkillColor.Blue,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.GainPrestige,2,4,"기존 Sentencing 뒷면 hook"),
            S(YujinSkillIds.HwanhyeongAttack,"환형(공격)",ActionType.Preparation,SkillColor.Red,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.YujinSense,1,2,"기존 환형 공격 hook"),
            S(YujinSkillIds.HwanhyeongDefense,"환형(수비)",ActionType.Preparation,SkillColor.Blue,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.GainBlock,6,10,"기존 환형 수비 hook"),
            S("yujin.preparation.wait_chance","기회를 엿보다",ActionType.Preparation,SkillColor.Blue,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.YujinSense,1,2,"TEMP"),
            S("yujin.preparation.hold_breath","숨을 죽이다",ActionType.Preparation,SkillColor.Blue,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.GainBlock,6,10,"TEMP"),
            S("yujin.preparation.sharpen","칼을 갈다",ActionType.Preparation,SkillColor.Red,1,PreparationTier.Strong,TempBalanceSkillProxyOperation.YujinMark,2,5,"TEMP")
        };
        for (int i = 0; i < prepRows.Length; i++)
            preps.Add(ConfigureYujinSkill(YujinSkillRoot + "/Preparation", $"Yujin_P_{i + 1:00}", prepRows[i], 150));

        SimpleSkillRow[] prestigeRows =
        {
            S(YujinSkillIds.Brand,"밑작업 착수",ActionType.Prestige,SkillColor.Red,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinMark,3,8,"기존 Brand hook"),
            S(YujinSkillIds.JointLiability,"확실한 일처리",ActionType.Prestige,SkillColor.Red,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.GainPrestige,3,8,"기존 JointLiability hook"),
            S(YujinSkillIds.Retrial,"유연한 대처",ActionType.Prestige,SkillColor.Blue,0,PreparationTier.Weak,TempBalanceSkillProxyOperation.YujinSense,1,3,"기존 Retrial hook")
        };
        for (int i = 0; i < prestigeRows.Length; i++)
            prestiges.Add(ConfigureYujinSkill(YujinSkillRoot + "/Prestige", $"Yujin_R_{i + 1:00}", prestigeRows[i], 225));

        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(YujinLoadoutPath);
        if (loadout == null)
            return false;

        ApplyLoadout(loadout, normals, duels, commonPreparations, preps.Skip(2).ToList(), prestiges);
        manifest.YujinPoolCount = normals.Count + duels.Count + preps.Count + prestiges.Count;
        return normals.Count == 9 && duels.Count == 14 && preps.Count == 9 && prestiges.Count == 3 &&
               ValidateTempUpgradeProfiles(normals.Concat(duels).Concat(preps).Concat(prestiges));
    }

    // ---------------------------------------------------------------------
    // Skill authoring helpers
    // ---------------------------------------------------------------------

    private static SkillDefinition ConfigureDiceSkill(
        string folder,
        string assetName,
        DiceSkillRow row)
    {
        EnsureFolder(folder);
        SkillDefinition skill = EnsureAsset<SkillDefinition>($"{folder}/{assetName}.asset");
        SetSkillId(skill, row.Id);
        skill.SkillName = row.Name;
        skill.ActionType = row.ActionType;
        skill.Color = row.Color;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, row.Energy);
        skill.BasePowerRule = SkillBasePowerRule.LegacyExplicit;
        skill.BasePower = Mathf.Max(1, row.MaxBasePower - 2);
        skill.BasePowerFlatAdjustment = 0;
        skill.ResolverType = SkillResolverType.Dice;
        skill.DiceMin = 1;
        skill.DiceMax = 8;
        skill.ExchangeRollCount = Mathf.Clamp(row.Min?.Length ?? 1, 1, 8);
        skill.CanBreakPart = row.CanBreak;
        skill.BreakMode = row.CanBreak ? PartBreakMode.WeakenedOnly : PartBreakMode.None;
        skill.PhysicalType = PhysicalForStableId(row.Id);
        skill.Rolls ??= new List<SkillRollData>();
        skill.Rolls.Clear();

        int count = Math.Min(row.Min?.Length ?? 0, row.Max?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            int min = Mathf.Max(1, row.Min[i] - 2);
            int max = Mathf.Max(min, row.Max[i] - 2);
            skill.Rolls.Add(new SkillRollData
            {
                Index = i,
                Type = row.Color == SkillColor.Blue ? CombatRollType.Stagger : CombatRollType.Attack,
                OverridePhysicalType = true,
                PhysicalType = skill.PhysicalType,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = min,
                MaxPower = max,
                RngSource = RollRngSource.CharacterDefault
            });
        }

        skill.Description =
            $"[{ProfileId}] 0916 강화2 목표 보존. Base 범위/기본위력은 max-2, U1 +1, U2 +1." +
            (row.C44Override ? $" [{ProfileId}:C44_OVERRIDE] XLSX 위치 평균을 그대로 보존." : string.Empty);

        AttachSingleProxy(skill, folder, assetName, row.Proxy, row.Timing, 1, Mathf.Max(1, row.MaxProxyAmount));
        skill.UpgradeProfile = ConfigureUpgradeProfile(
            folder, assetName, row.ActionType, true, 1, Mathf.Max(1, row.MaxProxyAmount));
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition ConfigureSimpleSkill(
        string folder,
        string assetName,
        SimpleSkillRow row,
        int secondUpgradeCost)
    {
        EnsureFolder(folder);
        SkillDefinition skill = EnsureAsset<SkillDefinition>($"{folder}/{assetName}.asset");
        SetSkillId(skill, row.Id);
        skill.SkillName = row.Name;
        skill.ActionType = row.ActionType;
        skill.Color = row.Color;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, row.Energy);
        skill.BasePowerRule = SkillBasePowerRule.FixedBase;
        skill.BasePower = row.ActionType == ActionType.Preparation || row.ActionType == ActionType.Prestige ? 0 : 11;
        skill.ResolverType = SkillResolverType.Dice;
        skill.DiceMin = 1;
        skill.DiceMax = 8;
        skill.ExchangeRollCount = 1;
        skill.Rolls ??= new List<SkillRollData>();
        skill.Rolls.Clear();
        if (row.ActionType == ActionType.NormalAttack || row.ActionType == ActionType.Duel)
        {
            skill.Rolls.Add(new SkillRollData
            {
                Index = 0,
                Type = row.Color == SkillColor.Blue ? CombatRollType.Stagger : CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = 13,
                MaxPower = 20,
                RngSource = RollRngSource.CharacterDefault
            });
        }
        skill.PreparationTier = row.PreparationTier;
        skill.CanBreakPart = false;
        skill.BreakMode = PartBreakMode.None;
        skill.Description = $"[{ProfileId}] {row.Note}";

        bool nativeHookOnly = string.Equals(row.Id, OlafSkillIds.Bloto, StringComparison.Ordinal);
        if (nativeHookOnly)
        {
            // 블로토는 OlafMadnessMechanic이 Strong Preparation의 광기 +1과
            // 최저 정상 부위 약화 + 적 공포를 이미 canonical하게 처리한다.
            // TEMP proxy를 함께 붙이면 광기가 +2가 되는 중복 실행이 발생한다.
            RemoveTempProxy(skill, folder, assetName);
            skill.Description =
                $"[{ProfileId}] Phase D native hook 사용. TEMP proxy 없음. Cost=1; Madness/Fear/Weaken은 OlafMadnessMechanic 단일 소스.";
            skill.UpgradeProfile = ConfigureUpgradeProfile(
                folder, assetName, row.ActionType, false,
                0, 0, secondUpgradeCost, includeEffectPayload: false);
        }
        else
        {
            AttachSingleProxy(
                skill, folder, assetName, row.Proxy, row.Timing,
                Mathf.Max(0, row.BaseProxyAmount), Mathf.Max(0, row.MaxProxyAmount));
            skill.UpgradeProfile = ConfigureUpgradeProfile(
                folder, assetName, row.ActionType, false,
                Mathf.Max(0, row.BaseProxyAmount), Mathf.Max(0, row.MaxProxyAmount), secondUpgradeCost);
        }
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition ConfigureYujinSkill(
        string folder,
        string assetName,
        SimpleSkillRow row,
        int secondUpgradeCost)
    {
        SkillDefinition skill = ConfigureSimpleSkill(folder, assetName, row, secondUpgradeCost);

        if (row.ActionType == ActionType.NormalAttack || row.ActionType == ActionType.Duel)
        {
            // YujinRuntimeSkills/YujinWeaponProfile가 실제 코인 수/앞면 확률/13·18·20·44 공식을 공급한다.
            // C-38 때문에 여기서 power를 -2 하면 Phase D 계약을 깨므로 카드 rider만 강화한다.
            skill.BasePowerRule = SkillBasePowerRule.FixedBase;
            skill.BasePower = 11;
            skill.ResolverType = SkillResolverType.Coin;
            skill.Rolls.Clear();
            skill.ExchangeRollCount = 1;
        }

        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static void RemoveTempProxy(
        SkillDefinition skill,
        string folder,
        string assetName)
    {
        if (skill == null)
            return;

        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.EffectEntries.Clear();
        skill.Effects ??= new List<SkillEffectDefinition>();
        skill.Effects.Clear();

        string effectPath = $"{folder}/Effects/{assetName}_{ProfileId}_Effect.asset";
        if (AssetDatabase.LoadMainAssetAtPath(effectPath) != null)
            AssetDatabase.DeleteAsset(effectPath);
    }

    private static void AttachSingleProxy(
        SkillDefinition skill,
        string folder,
        string assetName,
        TempBalanceSkillProxyOperation operation,
        SkillEffectTiming timing,
        int baseAmount,
        int maxAmount)
    {
        EnsureFolder(folder + "/Effects");
        TempBalanceSkillProxyEffectDefinition effect =
            EnsureAsset<TempBalanceSkillProxyEffectDefinition>(
                $"{folder}/Effects/{assetName}_{ProfileId}_Effect.asset");
        effect.Operation = operation;
        effect.BaseAmount = Mathf.Max(0, baseAmount);
        effect.DurationTurns = 3;
        effect.Multiplier = 2;
        EditorUtility.SetDirty(effect);

        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.EffectEntries.Clear();
        skill.Effects ??= new List<SkillEffectDefinition>();
        skill.Effects.Clear();
        skill.EffectEntries.Add(new SkillEffectEntry
        {
            Definition = effect,
            UpgradeKey = "temp_proxy",
            OverrideTiming = true,
            Timing = timing,
            Conditions = new List<SkillEffectCondition>()
        });
    }

    private static SkillUpgradeProfile ConfigureUpgradeProfile(
        string folder,
        string assetName,
        ActionType actionType,
        bool shiftDicePower,
        int baseProxyAmount,
        int maxProxyAmount,
        int explicitSecondCost = 0,
        bool includeEffectPayload = true)
    {
        EnsureFolder(folder + "/Upgrades");
        SkillUpgradeProfile profile = EnsureAsset<SkillUpgradeProfile>(
            $"{folder}/Upgrades/{assetName}_{ProfileId}_Upgrade.asset");

        profile.BaseCostOverride = 0;
        profile.Upgrade1 ??= new SkillUpgradeStep();
        profile.Upgrade2 ??= new SkillUpgradeStep();
        ResetUpgradeStep(profile.Upgrade1);
        ResetUpgradeStep(profile.Upgrade2);

        profile.Upgrade1.DesignNote =
            $"[{ProfileId}] max-level 0916 target를 보존하기 위한 1단계 임시 역산.";
        profile.Upgrade2.DesignNote =
            $"[{ProfileId}] max-level 0916 target 도달. 정식 분해 규칙 확정 시 교체.";

        if (shiftDicePower)
        {
            profile.Upgrade1.BasePowerDelta = 1;
            profile.Upgrade1.AllRollMinPowerDelta = 1;
            profile.Upgrade1.AllRollMaxPowerDelta = 1;
            profile.Upgrade2.BasePowerDelta = 1;
            profile.Upgrade2.AllRollMinPowerDelta = 1;
            profile.Upgrade2.AllRollMaxPowerDelta = 1;
        }

        profile.Upgrade2.CostOverride = explicitSecondCost > 0
            ? explicitSecondCost
            : SecondUpgradeCost(actionType);

        if (includeEffectPayload)
        {
            int u1Amount = InterpolateUpgradeAmount(baseProxyAmount, maxProxyAmount, 1);
            int u2Amount = Mathf.Max(baseProxyAmount, maxProxyAmount);
            profile.Upgrade1.EffectPayloads.Add(MakeAmountPayload("temp_proxy", u1Amount));
            profile.Upgrade2.EffectPayloads.Add(MakeAmountPayload("temp_proxy", u2Amount));
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static int InterpolateUpgradeAmount(int baseAmount, int maxAmount, int level)
    {
        if (maxAmount <= baseAmount)
            return baseAmount;
        float t = Mathf.Clamp01(level / 2f);
        return Mathf.RoundToInt(Mathf.Lerp(baseAmount, maxAmount, t));
    }

    private static SkillUpgradeEffectPayload MakeAmountPayload(string key, int amount)
    {
        return new SkillUpgradeEffectPayload
        {
            TargetEffectKey = key,
            OverrideAmount = true,
            Amount = Mathf.Max(0, amount)
        };
    }

    private static void ResetUpgradeStep(SkillUpgradeStep step)
    {
        step.CostOverride = 0;
        step.BasePowerDelta = 0;
        step.AllRollMinPowerDelta = 0;
        step.AllRollMaxPowerDelta = 0;
        step.PerRollPowerDelta ??= new List<int>();
        step.PerRollPowerDelta.Clear();
        step.EnergyCostDelta = 0;
        step.PrestigeCostDelta = 0;
        step.CustomResourceCostDelta = 0;
        step.EffectPayloads ??= new List<SkillUpgradeEffectPayload>();
        step.EffectPayloads.Clear();
    }

    private static int SecondUpgradeCost(ActionType type) => type switch
    {
        ActionType.NormalAttack => 75,
        ActionType.Preparation => 150,
        ActionType.Duel => 200,
        ActionType.Prestige => 225,
        _ => 0
    };

    private static void ApplyLoadout(
        CharacterCombatLoadout loadout,
        List<SkillDefinition> normals,
        List<SkillDefinition> duels,
        List<SkillDefinition> commonPreps,
        List<SkillDefinition> uniquePreps,
        List<SkillDefinition> prestiges)
    {
        loadout.IncludeCatalogCandidates = false;
        loadout.NormalSkillPool = new List<SkillDefinition>(normals);
        loadout.DuelSkillPool = new List<SkillDefinition>(duels);
        loadout.CommonPreparationPool = new List<SkillDefinition>(commonPreps);
        loadout.CharacterPreparationPool = new List<SkillDefinition>(uniquePreps);
        loadout.PrestigeSkillPool = new List<SkillDefinition>(prestiges);

        // 현재 장착분도 새 pool 안의 안정된 starter 세트로 맞춰 legacy candidate가 섞이지 않게 한다.
        loadout.NormalSkills = normals.Take(3).ToList();
        loadout.DuelSkills = duels.Take(3).ToList();
        loadout.PreparationSkills = commonPreps.Concat(uniquePreps).Take(3).ToList();
        loadout.PrestigeSkills = prestiges.Take(1).ToList();
        EditorUtility.SetDirty(loadout);
    }

    private static bool ValidateTempUpgradeProfiles(IEnumerable<SkillDefinition> skills)
    {
        foreach (SkillDefinition skill in skills)
        {
            if (skill == null || skill.UpgradeProfile == null)
                return false;
            if (!SkillUpgradeService.TryGetUpgradeCost(skill, 2, out int second) || second <= 0)
                return false;
        }
        return true;
    }

    private static DiceSkillRow D(
        string id, string name, ActionType type, SkillColor color, int energy,
        int maxBasePower, int[] min, int[] max, bool canBreak, bool c44,
        TempBalanceSkillProxyOperation proxy)
    {
        return new DiceSkillRow
        {
            Id = id,
            Name = name,
            ActionType = type,
            Color = color,
            Energy = energy,
            MaxBasePower = maxBasePower,
            Min = min,
            Max = max,
            CanBreak = canBreak,
            C44Override = c44,
            Proxy = proxy,
            Timing = type == ActionType.Duel ? SkillEffectTiming.OnExchangeWin : SkillEffectTiming.OnExchangeWin,
            MaxProxyAmount = 3
        };
    }

    private static SimpleSkillRow S(
        string id, string name, ActionType type, SkillColor color, int energy,
        PreparationTier tier, TempBalanceSkillProxyOperation proxy,
        int baseAmount, int maxAmount, string note)
    {
        return new SimpleSkillRow
        {
            Id = id,
            Name = name,
            ActionType = type,
            Color = color,
            Energy = energy,
            PreparationTier = tier,
            Proxy = proxy,
            Timing = type == ActionType.Preparation || type == ActionType.Prestige
                ? SkillEffectTiming.OnExecute
                : SkillEffectTiming.OnExchangeWin,
            BaseProxyAmount = baseAmount,
            MaxProxyAmount = maxAmount,
            Note = note
        };
    }

    private static PhysicalDamageType PhysicalForStableId(string id)
    {
        int hash = 17;
        if (!string.IsNullOrEmpty(id))
            for (int i = 0; i < id.Length; i++)
                hash = unchecked(hash * 31 + id[i]);
        int value = Math.Abs(hash % 3);
        return value switch
        {
            1 => PhysicalDamageType.Pierce,
            2 => PhysicalDamageType.Blunt,
            _ => PhysicalDamageType.Cut
        };
    }

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
