#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectAbyssOlafYujinDesignMigration
{
    private const string MenuPath =
        "Tools/Project Abyss/Characters/Apply Olaf + Yujin TODO Design";

    private const string Root =
        "Assets/2. Data/Characters/Design2026";

    [MenuItem(MenuPath, false, 30)]
    public static void Apply()
    {
        EnsureFolder("Assets/2. Data");
        EnsureFolder("Assets/2. Data/Characters");
        EnsureFolder(Root);
        EnsureFolder(Root + "/Olaf");
        EnsureFolder(Root + "/Olaf/Skills");
        EnsureFolder(Root + "/Yujin");
        EnsureFolder(Root + "/Yujin/Skills");

        CharacterCombatLoadout olafLoadout =
            BuildOlafLoadout();

        CharacterData olafData =
            BuildCharacterData(
                Root + "/Olaf/Olaf_TODO_2026.asset",
                "올라프",
                70,
                olafLoadout);

        CharacterCombatLoadout yujinLoadout =
            BuildYujinLoadout();

        CharacterData yujinData =
            BuildCharacterData(
                Root + "/Yujin/Yujin_TODO_2026.asset",
                "유진",
                50,
                yujinLoadout);

        // 유진은 네 부위 각각에서 일반/결투/도사림/위세를 선택한다.
        // 위세 1회 제한은 CharacterCombatLoadout과 PrestigeUsePolicy가 담당한다.
        yujinData.ActionSlots =
            BuildYujinSlots();

        EditorUtility.SetDirty(yujinData);

        CharacterAuthoringBundle yujinBundle =
            LoadOrCreate<CharacterAuthoringBundle>(
                Root + "/Yujin/Yujin_TODO_Bundle.asset");

        yujinBundle.ConfigureCore(
            CharacterAuthoringKind.Yujin,
            "유진",
            yujinData,
            null,
            yujinLoadout,
            null);

        EditorUtility.SetDirty(yujinBundle);

        ApplySceneObjects(
            olafData,
            olafLoadout,
            yujinData,
            yujinLoadout,
            yujinBundle);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[ProjectAbyssOlafYujinDesignMigration] " +
            "올라프·유진 TODO형 캐릭터 데이터와 HUD 구조를 적용했습니다.");
    }

    private static CharacterCombatLoadout BuildOlafLoadout()
    {
        string folder =
            Root + "/Olaf/Skills/";

        SkillDefinition enduring =
            CreateDiceSkill(
                folder + "Olaf_EnduringSlash.asset",
                OlafSkillIds.EnduringSlash,
                "버티며 베기",
                ActionType.NormalAttack,
                0,
                "공격·수비·공격의 3굴림으로 버티면서 피해를 누적한다. " +
                "공격 굴림 승리 시 대상 부위에 출혈을 부여한다.",
                new[] { CombatRollType.Attack, CombatRollType.Defense, CombatRollType.Attack },
                14,
                19);

        SkillDefinition overhead =
            CreateDiceSkill(
                folder + "Olaf_OverheadSmash.asset",
                OlafSkillIds.OverheadSmash,
                "내려찍기",
                ActionType.NormalAttack,
                1,
                "최저값이 높은 2회 공격. 상대 결투 효과를 평타로 봉인하면서 안정적으로 받아낸다.",
                new[] { CombatRollType.Attack, CombatRollType.Attack },
                15,
                20);

        SkillDefinition wild =
            CreateDiceSkill(
                folder + "Olaf_WildHack.asset",
                OlafSkillIds.WildHack,
                "마구잡이",
                ActionType.NormalAttack,
                0,
                "공격 굴림 3회로 빠르게 출혈을 쌓는 무상 평타.",
                new[] { CombatRollType.Attack, CombatRollType.Attack, CombatRollType.Attack },
                17,
                22);

        SkillDefinition standard =
            CreateDiceSkill(
                folder + "Olaf_Standard.asset",
                OlafSkillIds.Standard,
                "표준",
                ActionType.Duel,
                1,
                "결투 대 결투의 개별 교환에서 승리하면 출혈 1과 광기 1을 추가로 얻는다. " +
                "부여 후 대상 부위 출혈이 10 이상이면 출혈×10 피해로 전량 폭발하며, " +
                "잔여 부위 체력을 소진하면 파괴한다.",
                new[] { CombatRollType.Attack, CombatRollType.Attack },
                17,
                22);

        SkillDefinition rend =
            CreateDiceSkill(
                folder + "Olaf_Rend.asset",
                OlafSkillIds.Rend,
                "난도질",
                ActionType.Duel,
                2,
                "결투 대 결투의 각 교환마다 승패와 무관하게 대상 부위 출혈 1과 광기 1을 얻는다. " +
                "이 스킬 자체에는 부위 파괴 권한이 없다.",
                new[] { CombatRollType.Attack, CombatRollType.Attack, CombatRollType.Attack },
                13,
                18);

        SkillDefinition crouch =
            CreateUtilitySkill(
                folder + "Olaf_Crouch.asset",
                OlafSkillIds.Crouch,
                "웅크리기",
                ActionType.Preparation,
                0,
                "현재 방어도를 12로 설정한다. 방어도는 누적되지 않는다.");

        SkillDefinition glare =
            CreateUtilitySkill(
                folder + "Olaf_Glare.asset",
                OlafSkillIds.Glare,
                "노려보기",
                ActionType.Preparation,
                1,
                "해당 턴 모든 합 판정값에 +1을 적용한다.");

        SkillDefinition showOff =
            CreateUtilitySkill(
                folder + "Olaf_ShowOff.asset",
                OlafSkillIds.ShowOff,
                "가오잡기",
                ActionType.Preparation,
                1,
                "정상 상태인 자신의 부위 중 체력이 가장 낮은 부위 하나를 약화시키고 광기 2를 얻는다.");

        SkillDefinition bloom =
            CreatePrestigeSkill(
                folder + "Olaf_BloomingWound.asset",
                OlafSkillIds.BloomingWound,
                "만개하는 상처",
                60,
                "조준 부위에 출혈 3 + 광기 5당 1을 부여한다. 최종 부여량은 3~5이며 즉시 폭발하지 않는다.");

        SkillDefinition burst =
            CreatePrestigeSkill(
                folder + "Olaf_BurstingMadness.asset",
                OlafSkillIds.BurstingMadness,
                "터뜨리는 광기",
                65,
                "광기 2를 얻은 뒤 현재 광기×5 + 대상 부위 출혈 스택만큼 피해를 준다. " +
                "출혈을 소비하지 않으며 부위를 파괴할 수 없다.");

        SkillDefinition backs =
            CreatePrestigeSkill(
                folder + "Olaf_BacksToWall.asset",
                OlafSkillIds.BacksToWall,
                "배수진",
                70,
                "한 턴 동안 전신·부위 체력이 1 아래로 내려가지 않고 부위가 파괴되지 않는다. " +
                "그 턴 결투의 개별 교환에서 피해를 받을 때마다 광기 1을 얻는다.");

        CharacterCombatLoadout loadout =
            LoadOrCreate<CharacterCombatLoadout>(
                Root + "/Olaf/Olaf_TODO_Loadout.asset");

        loadout.NormalSkills =
            new List<SkillDefinition>
            {
                enduring,
                overhead,
                wild
            };

        loadout.DuelSkills =
            new List<SkillDefinition>
            {
                standard,
                rend
            };

        loadout.PreparationSkills =
            new List<SkillDefinition>
            {
                crouch,
                glare,
                showOff
            };

        loadout.PrestigeSkills =
            new List<SkillDefinition>
            {
                bloom
            };

        loadout.NormalSkillPool =
            new List<SkillDefinition>(loadout.NormalSkills);

        loadout.DuelSkillPool =
            new List<SkillDefinition>(loadout.DuelSkills);

        loadout.CharacterPreparationPool =
            new List<SkillDefinition>(loadout.PreparationSkills);

        loadout.PrestigeSkillPool =
            new List<SkillDefinition>
            {
                bloom,
                burst,
                backs
            };

        EditorUtility.SetDirty(loadout);
        return loadout;
    }

    private static CharacterCombatLoadout BuildYujinLoadout()
    {
        string folder =
            Root + "/Yujin/Skills/";

        SkillDefinition inspection =
            CreateCoinSkill(
                folder + "Yujin_Inspection.asset",
                YujinSkillIds.Inspection,
                "검분",
                ActionType.NormalAttack,
                0,
                "무기에 따라 3/2/1회 코인을 굴린다. 승리한 공격 교환마다 조준 부위에 " +
                "백우 4, 적설 6, 낙일 8의 표식을 부여한다.");

        SkillDefinition breakfast =
            CreateCoinSkill(
                folder + "Yujin_Breakfast.asset",
                YujinSkillIds.Breakfast,
                "조식",
                ActionType.NormalAttack,
                2,
                "승리한 공격 교환마다 살수의 감 1을 얻는다. 이 스킬은 기본 표식을 부여하지 않는다.");

        SkillDefinition inscription =
            CreateCoinSkill(
                folder + "Yujin_Inscription.asset",
                YujinSkillIds.Inscription,
                "각인",
                ActionType.Duel,
                1,
                "결투 대 결투의 교환 승리마다 표식을 백우 8, 적설 12, 낙일 16 부여한다. " +
                "살수의 감 자동 사용을 켜면 이길 때까지 자원을 소모해 재굴림한다.");

        SkillDefinition pursuit =
            CreateCoinSkill(
                folder + "Yujin_Pursuit.asset",
                YujinSkillIds.Pursuit,
                "추격",
                ActionType.Duel,
                1,
                "결투 교환 승리 후 살수의 감을 1씩 소비해 추가 코인을 굴린다. " +
                "앞면이면 무저항 추가타를 가하고, 뒷면이면 종료한다. " +
                "교환당 상한은 백우 1, 적설 2, 낙일 5다.");

        SkillDefinition capture =
            CreateUtilitySkill(
                folder + "Yujin_Capture.asset",
                YujinSkillIds.Capture,
                "포착",
                ActionType.Preparation,
                1,
                "해당 턴 모든 코인의 앞면 확률을 10%p 높인다.");

        SkillDefinition sentencing =
            CreateUtilitySkill(
                folder + "Yujin_Sentencing.asset",
                YujinSkillIds.Sentencing,
                "양형",
                ActionType.Preparation,
                1,
                "해당 턴 모든 코인의 뒷면 판정값을 2 높인다.");

        SkillDefinition brand =
            CreatePrestigeSkill(
                folder + "Yujin_Brand.asset",
                YujinSkillIds.Brand,
                "낙인",
                50,
                "조준 부위에 현재 무기의 크리값만큼 표식을 즉시 부여하고, " +
                "해당 턴 승리한 공격 교환마다 표식 3을 추가한다. 적설은 두 부위에 각각 적용한다.");

        SkillDefinition joint =
            CreatePrestigeSkill(
                folder + "Yujin_JointLiability.asset",
                YujinSkillIds.JointLiability,
                "연좌",
                50,
                "다음 코인 하나를 확정 앞면으로 만든다. 해당 턴 처치·부위 파괴·표식 발화가 발생하면 " +
                "다음 코인도 확정 앞면이 되며 총 4회까지 이어진다.");

        SkillDefinition retrial =
            CreatePrestigeSkill(
                folder + "Yujin_Retrial.asset",
                YujinSkillIds.Retrial,
                "재심",
                50,
                "해당 턴 뒷면이 나온 각 코인을 자동으로 한 번 재굴림한다. " +
                "같은 교환에는 한 번만 적용되고 살수의 감을 소비하지 않는다.");

        CharacterCombatLoadout loadout =
            LoadOrCreate<CharacterCombatLoadout>(
                Root + "/Yujin/Yujin_TODO_Loadout.asset");

        loadout.NormalSkills =
            new List<SkillDefinition>
            {
                inspection,
                breakfast
            };

        loadout.DuelSkills =
            new List<SkillDefinition>
            {
                inscription,
                pursuit
            };

        loadout.PreparationSkills =
            new List<SkillDefinition>
            {
                capture,
                sentencing
            };

        loadout.PrestigeSkills =
            new List<SkillDefinition>
            {
                brand
            };

        loadout.NormalSkillPool =
            new List<SkillDefinition>(loadout.NormalSkills);

        loadout.DuelSkillPool =
            new List<SkillDefinition>(loadout.DuelSkills);

        loadout.CharacterPreparationPool =
            new List<SkillDefinition>(loadout.PreparationSkills);

        loadout.PrestigeSkillPool =
            new List<SkillDefinition>
            {
                brand,
                joint,
                retrial
            };

        EditorUtility.SetDirty(loadout);
        return loadout;
    }

    private static CharacterData BuildCharacterData(
        string assetPath,
        string characterName,
        int maxPrestige,
        CharacterCombatLoadout loadout)
    {
        CharacterData data =
            LoadOrCreate<CharacterData>(
                assetPath);

        data.CharacterName = characterName;
        data.CombatantTier = CombatantTier.Player;
        data.TargetMode = CharacterTargetMode.BodyParts;
        data.maxPrestige = maxPrestige;
        data.maxEnergy = 3;
        data.minSpeed = 1;
        data.maxSpeed = 6;
        data.CombatLoadout = loadout;
        data.ActionSlots = BuildPlayerSlots();

        EditorUtility.SetDirty(data);
        return data;
    }

    private static List<CharacterSlotConfig>
        BuildPlayerSlots()
    {
        return new List<CharacterSlotConfig>
        {
            CreateSlot(
                "HEAD",
                "머리",
                PartType.HEAD,
                ActionType.NormalAttack,
                ActionType.Duel,
                ActionType.Preparation,
                ActionType.Prestige),

            CreateSlot(
                "LEFT_ARM",
                "왼팔",
                PartType.LEFT_HAND,
                ActionType.NormalAttack,
                ActionType.Duel,
                ActionType.Prestige),

            CreateSlot(
                "RIGHT_ARM",
                "오른팔",
                PartType.RIGHT_HAND,
                ActionType.NormalAttack,
                ActionType.Duel,
                ActionType.Prestige),

            CreateSlot(
                "LEGS",
                "다리",
                PartType.LEGS,
                ActionType.Preparation)
        };
    }

    private static List<CharacterSlotConfig>
        BuildYujinSlots()
    {
        return new List<CharacterSlotConfig>
        {
            CreateYujinSlot(
                "HEAD",
                "머리",
                PartType.HEAD),

            CreateYujinSlot(
                "LEFT_ARM",
                "왼팔",
                PartType.LEFT_HAND),

            CreateYujinSlot(
                "RIGHT_ARM",
                "오른팔",
                PartType.RIGHT_HAND),

            CreateYujinSlot(
                "LEGS",
                "다리",
                PartType.LEGS)
        };
    }

    private static CharacterSlotConfig CreateYujinSlot(
        string id,
        string name,
        PartType part)
    {
        return CreateSlot(
            id,
            name,
            part,
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige);
    }

    private static CharacterSlotConfig CreateSlot(
        string id,
        string name,
        PartType part,
        params ActionType[] allowed)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = name,
            Enabled = true,
            HasLinkedPart = true,
            LinkedPartType = part,
            OverrideSpeedRange = false,
            AllowedActionTypes =
                new List<ActionType>(allowed)
        };
    }

    private static SkillDefinition CreateDiceSkill(
        string path,
        string id,
        string name,
        ActionType actionType,
        int energy,
        string description,
        CombatRollType[] rollTypes,
        int minPower,
        int maxPower)
    {
        SkillDefinition definition =
            LoadOrCreate<SkillDefinition>(path);

        ConfigureCommon(
            definition,
            id,
            name,
            actionType,
            energy,
            description);

        definition.ResolverType = SkillResolverType.Dice;
        definition.DiceMin = 1;
        definition.DiceMax = 6;
        definition.Rolls = new List<SkillRollData>();

        for (int i = 0;
             i < rollTypes.Length;
             i++)
        {
            definition.Rolls.Add(
                new SkillRollData
                {
                    Index = i,
                    Type = rollTypes[i],
                    MinPower = minPower,
                    MaxPower = maxPower,
                    RngSource = RollRngSource.Dice
                });
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static SkillDefinition CreateCoinSkill(
        string path,
        string id,
        string name,
        ActionType actionType,
        int energy,
        string description)
    {
        SkillDefinition definition =
            LoadOrCreate<SkillDefinition>(path);

        ConfigureCommon(
            definition,
            id,
            name,
            actionType,
            energy,
            description);

        definition.ResolverType = SkillResolverType.Coin;
        definition.CoinCount = 1;
        definition.CoinFrontChance = 0.5f;
        definition.CoinFrontValue = 1;
        definition.CoinBackValue = 0;
        definition.CoinFrontIsCritical = true;
        definition.Rolls =
            new List<SkillRollData>
            {
                new SkillRollData
                {
                    Index = 0,
                    Type = CombatRollType.Attack,
                    MinPower = 14,
                    MaxPower = 44,
                    RngSource = RollRngSource.Coin,
                    CoinFrontChance = 0.5f,
                    CoinBackPower = 14,
                    CoinFrontPower = 44,
                    CoinFrontIsCritical = true
                }
            };

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static SkillDefinition CreateUtilitySkill(
        string path,
        string id,
        string name,
        ActionType actionType,
        int energy,
        string description)
    {
        SkillDefinition definition =
            LoadOrCreate<SkillDefinition>(path);

        ConfigureCommon(
            definition,
            id,
            name,
            actionType,
            energy,
            description);

        definition.Rolls =
            new List<SkillRollData>();

        definition.ExchangeRollCount = 1;

        if (actionType == ActionType.Preparation)
            definition.PreparationTier = PreparationTier.Weak;

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static SkillDefinition CreatePrestigeSkill(
        string path,
        string id,
        string name,
        int threshold,
        string description)
    {
        SkillDefinition definition =
            CreateUtilitySkill(
                path,
                id,
                name,
                ActionType.Prestige,
                0,
                description);

        definition.OverrideResourceRules = true;
        definition.RequireFullPrestige = false;
        definition.PrestigeCost = threshold;
        definition.ConsumeAllPrestige = true;
        definition.OverridePrestigeUsePolicy = true;
        definition.PrestigeUsePolicy =
            PrestigeUsePolicy.OncePerTurn;

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void ConfigureCommon(
        SkillDefinition definition,
        string id,
        string name,
        ActionType actionType,
        int energy,
        string description)
    {
        SetSkillId(definition, id);

        definition.SkillName = name;
        definition.ActionType = actionType;
        definition.BasePower = 0;
        definition.CanBreakPart = false;
        definition.GainPrestige = true;
        definition.Description = description;
        definition.OverrideEnergyCost = true;
        definition.EnergyCost = Mathf.Max(0, energy);
        definition.OverrideCanClash = false;
        definition.ResolvePrestigeInCombat = false;
        definition.AttackWeight ??=
            new AttackWeightSettings();

        definition.AttackWeight.Weight = 1;
        definition.AttackWeight.SecondaryDamageMultiplier = 1f;
        definition.AttackWeight.SecondaryPartMode =
            AttackWeightSecondaryPartMode.RandomValidTargetPoint;
        definition.AttackWeight.AllowBrokenSecondaryParts = false;
    }

    private static void SetSkillId(
        SkillDefinition definition,
        string id)
    {
        SerializedObject serialized =
            new SerializedObject(definition);

        SerializedProperty property =
            serialized.FindProperty("skillId");

        if (property != null)
        {
            property.stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ApplySceneObjects(
        CharacterData olafData,
        CharacterCombatLoadout olafLoadout,
        CharacterData yujinData,
        CharacterCombatLoadout yujinLoadout,
        CharacterAuthoringBundle yujinBundle)
    {
        Olaf olaf =
            Object.FindFirstObjectByType<Olaf>(
                FindObjectsInactive.Include);

        if (olaf != null)
        {
            Undo.RecordObject(
                olaf,
                "Apply Olaf TODO Design");

            olaf.ConfigureAuthoringCore(
                olafData,
                olafLoadout);

            CharacterAuthoringLink link =
                olaf.GetComponent<CharacterAuthoringLink>();

            CharacterAuthoringBundle bundle =
                link?.Bundle;

            if (bundle != null)
            {
                if (bundle.SkillSet is OlafSkillSet legacySkillSet)
                {
                    legacySkillSet.NormalAttack =
                        olafLoadout.NormalSkills.Count > 0
                            ? olafLoadout.NormalSkills[0]
                            : null;

                    legacySkillSet.DuelSkill =
                        olafLoadout.DuelSkills.Count > 0
                            ? olafLoadout.DuelSkills[0]
                            : null;

                    legacySkillSet.PreparationSkill =
                        olafLoadout.PreparationSkills.Count > 0
                            ? olafLoadout.PreparationSkills[0]
                            : null;

                    legacySkillSet.PrestigeSkill =
                        olafLoadout.PrestigeSkills.Count > 0
                            ? olafLoadout.PrestigeSkills[0]
                            : null;

                    EditorUtility.SetDirty(legacySkillSet);
                }

                bundle.ConfigureCore(
                    CharacterAuthoringKind.Olaf,
                    "올라프",
                    olafData,
                    bundle.SkillSet,
                    olafLoadout,
                    bundle.VisualProfile);

                EditorUtility.SetDirty(bundle);
            }

            EditorUtility.SetDirty(olaf);
        }

        Transform charactersRoot =
            FindSceneTransform("[10] CHARACTERS");

        if (charactersRoot != null &&
            Object.FindFirstObjectByType<Yujin>(
                FindObjectsInactive.Include) == null)
        {
            GameObject placeholder =
                new GameObject(
                    "Yujin_AuthoringPlaceholder");

            Undo.RegisterCreatedObjectUndo(
                placeholder,
                "Create Yujin Authoring Placeholder");

            placeholder.transform.SetParent(
                charactersRoot,
                false);

            Yujin yujin =
                placeholder.AddComponent<Yujin>();

            yujin.ConfigureAuthoringCore(
                yujinData,
                yujinLoadout);

            CharacterAuthoringLink link =
                placeholder.AddComponent<CharacterAuthoringLink>();

            link.Configure(
                yujinBundle);

            // 모델·Animator·CharacterView가 준비되기 전에는 전투 명단에 들어가지 않게 비활성화한다.
            placeholder.SetActive(false);
            EditorUtility.SetDirty(yujin);
        }

        Transform persistentTop =
            FindSceneTransform("PersistentTopLayer");

        if (persistentTop != null &&
            Object.FindFirstObjectByType<CharacterMechanicHudUI>(
                FindObjectsInactive.Include) == null)
        {
            GameObject hud =
                new GameObject(
                    "CharacterMechanicHUD",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(CharacterMechanicHudUI));

            Undo.RegisterCreatedObjectUndo(
                hud,
                "Create Character Mechanic HUD");

            hud.transform.SetParent(
                persistentTop,
                false);
        }

        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());
        }
    }

    private static Transform FindSceneTransform(
        string objectName)
    {
        Transform[] all =
            Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform value in all)
        {
            if (value != null &&
                value.gameObject.scene.IsValid() &&
                value.name == objectName)
            {
                return value;
            }
        }

        return null;
    }

    private static T LoadOrCreate<T>(
        string assetPath)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase.LoadAssetAtPath<T>(
                assetPath);

        if (asset != null)
            return asset;

        asset =
            ScriptableObject.CreateInstance<T>();

        AssetDatabase.CreateAsset(
            asset,
            assetPath);

        return asset;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent =
            System.IO.Path.GetDirectoryName(path)
                ?.Replace('\\', '/');

        string name =
            System.IO.Path.GetFileName(path);

        if (!string.IsNullOrWhiteSpace(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(
            parent,
            name);
    }
}
#endif