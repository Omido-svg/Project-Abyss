#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ProjectAbyssTestEncounterSetup
{
    private const string MenuRoot =
        "Tools/Project Abyss/Test Encounters/";

    private const string Root =
        "Assets/2. Data/TestEncounters";

    private const string PrefabRoot =
        Root + "/Prefabs";

    private const string DataRoot =
        Root + "/Data";

    private const string SkillRoot =
        Root + "/Skills";

    private const string AnimationRoot =
        Root + "/Animation";

    private const string MaterialRoot =
        Root + "/Materials";

    private const string YujinBundlePath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Bundle.asset";

    private const string YujinDataPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_2026.asset";

    private const string OlafDesignRoot =
        "Assets/2. Data/Characters/Design2026/Olaf";

    private const string YujinDesignRoot =
        "Assets/2. Data/Characters/Design2026/Yujin";

    [MenuItem(MenuRoot + "Build All Test Prefabs + Scene Switcher", false, 10)]
    public static void BuildAll()
    {
        try
        {
            EnsureFolders();

            // TODO형 올라프·유진 데이터가 아직 없다면 먼저 생성한다.
            ProjectAbyssOlafYujinDesignMigration.Apply();

            Olaf sceneOlaf =
                Object.FindFirstObjectByType<Olaf>(
                    FindObjectsInactive.Include);

            EliteEnemy sceneElite =
                Object.FindFirstObjectByType<EliteEnemy>(
                    FindObjectsInactive.Include);

            if (sceneOlaf == null)
            {
                throw new InvalidOperationException(
                    "현재 Scene에서 Olaf를 찾지 못했습니다. CameraTest Scene을 연 뒤 실행하세요.");
            }

            if (sceneElite == null)
            {
                throw new InvalidOperationException(
                    "현재 Scene에서 EliteEnemy를 찾지 못했습니다. CameraTest Scene을 연 뒤 실행하세요.");
            }

            TestAnimationAssets animationAssets =
                BuildAnimationAssets();

            Material yujinMaterial =
                CreateOrUpdateMaterial(
                    MaterialRoot + "/Yujin_Test.mat",
                    new Color(0.16f, 0.22f, 0.34f, 1f),
                    new Color(0.08f, 0.2f, 0.5f, 1f));

            Material normalMaterial =
                CreateOrUpdateMaterial(
                    MaterialRoot + "/NormalEnemy_Test.mat",
                    new Color(0.18f, 0.38f, 0.16f, 1f),
                    new Color(0.04f, 0.15f, 0.03f, 1f));

            Material bossMaterial =
                CreateOrUpdateMaterial(
                    MaterialRoot + "/BossEnemy_Test.mat",
                    new Color(0.38f, 0.06f, 0.08f, 1f),
                    new Color(0.8f, 0.02f, 0.02f, 1f));

            Character olafPrefab =
                SaveSceneCharacterAsTestPrefab(
                    sceneOlaf,
                    PrefabRoot + "/Olaf_Test.prefab",
                    "Olaf_Test");

            Character elitePrefab =
                SaveSceneCharacterAsTestPrefab(
                    sceneElite,
                    PrefabRoot + "/EliteEnemy_Test.prefab",
                    "EliteEnemy_Test");

            Character yujinPrefab =
                BuildYujinPrefab(
                    animationAssets,
                    yujinMaterial);

            NormalEnemy normalEnemyPrefab =
                BuildNormalEnemyPrefab(
                    animationAssets,
                    normalMaterial);

            EliteEnemy bossPrefab =
                BuildBossPrefab(
                    sceneElite,
                    animationAssets,
                    bossMaterial);

            int repairedVisualCount =
                EnsureGeneratedSkillTimelines(
                    olafPrefab,
                    yujinPrefab,
                    normalEnemyPrefab,
                    elitePrefab,
                    bossPrefab,
                    animationAssets.AttackClip);

            SetupCurrentScene(
                olafPrefab,
                yujinPrefab,
                normalEnemyPrefab,
                elitePrefab,
                bossPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Selection.activeObject =
                Object.FindFirstObjectByType<BattleTestScenarioSwitcher>(
                    FindObjectsInactive.Include);

            EditorUtility.DisplayDialog(
                "Project Abyss 테스트 전투",
                "생성 완료\n\n" +
                "- 올라프 테스트 Prefab\n" +
                "- 유진 간이 모델 + Animator Prefab\n" +
                "- 일반 적 Prefab\n" +
                "- 정예 테스트 Prefab\n" +
                "- 보스 Prefab\n" +
                "- 일반/혼합/보스 전투 Switcher\n" +
                $"- Timeline 연출 보정 스킬 {repairedVisualCount}개\n\n" +
                "Play 후 좌측 상단 패널에서 플레이어와 전투 종류를 바꿀 수 있습니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "테스트 전투 생성 실패",
                exception.Message,
                "확인");
        }
    }

    [MenuItem(MenuRoot + "Repair Generated Skill Timelines", false, 20)]
    public static void RepairGeneratedSkillTimelines()
    {
        try
        {
            EnsureFolders();

            Character olafPrefab =
                LoadCharacterPrefab(
                    PrefabRoot + "/Olaf_Test.prefab");

            Character yujinPrefab =
                LoadCharacterPrefab(
                    PrefabRoot + "/Yujin_Test.prefab");

            Character normalEnemyPrefab =
                LoadCharacterPrefab(
                    PrefabRoot + "/NormalEnemy_Test.prefab");

            Character eliteEnemyPrefab =
                LoadCharacterPrefab(
                    PrefabRoot + "/EliteEnemy_Test.prefab");

            Character bossEnemyPrefab =
                LoadCharacterPrefab(
                    PrefabRoot + "/BossEnemy_Test.prefab");

            if (yujinPrefab == null ||
                normalEnemyPrefab == null)
            {
                throw new InvalidOperationException(
                    "테스트 Prefab을 찾지 못했습니다. 먼저 " +
                    "'Build All Test Prefabs + Scene Switcher'를 실행하세요.");
            }

            TestAnimationAssets animationAssets =
                BuildAnimationAssets();

            RepairGeneratedAnimationReferences(
                animationAssets);

            int repairedCount =
                EnsureGeneratedSkillTimelines(
                    olafPrefab,
                    yujinPrefab,
                    normalEnemyPrefab,
                    eliteEnemyPrefab,
                    bossEnemyPrefab,
                    animationAssets.AttackClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceUpdate);

            EditorUtility.DisplayDialog(
                "테스트 스킬 Timeline 복구",
                $"전용 SkillVisualDefinition과 Timeline을 확인한 스킬: {repairedCount}개\n\n" +
                "이제 Play Mode에서 검분·달려들기 등의 Timeline-only 오류가 " +
                "발생하지 않아야 합니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "테스트 스킬 Timeline 복구 실패",
                exception.Message,
                "확인");
        }
    }

    [MenuItem(MenuRoot + "Select Scenario Switcher", false, 30)]
    public static void SelectSwitcher()
    {
        Selection.activeObject =
            Object.FindFirstObjectByType<BattleTestScenarioSwitcher>(
                FindObjectsInactive.Include);
    }

    private static TestAnimationAssets BuildAnimationAssets()
    {
        AnimationClip idle =
            CreateClip(
                AnimationRoot + "/Test_Idle.anim",
                true,
                clip =>
                {
                    SetCurve(
                        clip,
                        "Torso",
                        "m_LocalPosition.y",
                        Curve(
                            (0f, 1.35f),
                            (0.6f, 1.41f),
                            (1.2f, 1.35f)));
                });

        AnimationClip hit =
            CreateClip(
                AnimationRoot + "/Test_Hit.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "Torso",
                        "localEulerAnglesRaw.z",
                        Curve(
                            (0f, 0f),
                            (0.08f, -18f),
                            (0.25f, 0f)));
                });

        AnimationClip death =
            CreateClip(
                AnimationRoot + "/Test_Death.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "Torso",
                        "localEulerAnglesRaw.z",
                        Curve(
                            (0f, 0f),
                            (0.8f, 82f)));

                    SetCurve(
                        clip,
                        "Torso",
                        "m_LocalPosition.y",
                        Curve(
                            (0f, 1.35f),
                            (0.8f, 0.5f)));
                });

        AnimationClip enter =
            CreateClip(
                AnimationRoot + "/Test_ClashEnter.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "LeftArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, 8f), (0.32f, 35f)));

                    SetCurve(
                        clip,
                        "RightArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, -8f), (0.32f, -42f)));
                });

        AnimationClip contest =
            CreateClip(
                AnimationRoot + "/Test_ClashContest.anim",
                true,
                clip =>
                {
                    SetCurve(
                        clip,
                        "WeaponPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, -15f), (0.3f, -8f), (0.6f, -15f)));
                });

        AnimationClip advantage =
            CreateClip(
                AnimationRoot + "/Test_ClashAdvantage.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "WeaponPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, -55f), (0.16f, 65f), (0.38f, 15f)));

                    SetCurve(
                        clip,
                        "Torso",
                        "localEulerAnglesRaw.y",
                        Curve((0f, 0f), (0.2f, 20f), (0.38f, 0f)));
                });

        AnimationClip disadvantage =
            CreateClip(
                AnimationRoot + "/Test_ClashDisadvantage.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "Torso",
                        "localEulerAnglesRaw.z",
                        Curve((0f, 0f), (0.12f, -24f), (0.35f, 0f)));
                });

        AnimationClip tie =
            CreateClip(
                AnimationRoot + "/Test_ClashTie.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "Torso",
                        "localEulerAnglesRaw.z",
                        Curve((0f, 0f), (0.1f, 12f), (0.2f, -12f), (0.34f, 0f)));
                });

        AnimationClip reengage =
            CreateClip(
                AnimationRoot + "/Test_ClashReengage.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "LeftArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, 20f), (0.25f, 35f)));

                    SetCurve(
                        clip,
                        "RightArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, -20f), (0.25f, -42f)));
                });

        AnimationClip exit =
            CreateClip(
                AnimationRoot + "/Test_ClashExit.anim",
                false,
                clip =>
                {
                    SetCurve(
                        clip,
                        "LeftArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, 35f), (0.3f, 8f)));

                    SetCurve(
                        clip,
                        "RightArmPivot",
                        "localEulerAnglesRaw.z",
                        Curve((0f, -42f), (0.3f, -8f)));
                });

        AnimatorController controller =
            CreateAnimatorController(
                AnimationRoot + "/TestCharacter.controller",
                idle,
                hit,
                death);

        CharacterPresentationProfile profile =
            LoadOrCreate<CharacterPresentationProfile>(
                AnimationRoot + "/TestCharacterPresentation.asset");

        profile.ClashEnter = enter;
        profile.ClashContest = contest;
        profile.ClashAdvantage = advantage;
        profile.ClashDisadvantage = disadvantage;
        profile.ClashTie = tie;
        profile.ClashReengage = reengage;
        profile.ClashExit = exit;
        profile.LightHit = hit;
        profile.HeavyHit = hit;
        profile.Knockback = hit;
        profile.Knockdown = death;
        profile.PartBreak = hit;
        profile.Death = death;
        EditorUtility.SetDirty(profile);

        return new TestAnimationAssets(
            controller,
            profile,
            advantage);
    }

    private static Character BuildYujinPrefab(
        TestAnimationAssets animations,
        Material material)
    {
        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(
                YujinBundlePath);

        CharacterData data =
            AssetDatabase.LoadAssetAtPath<CharacterData>(
                YujinDataPath);

        if (bundle == null || data == null)
        {
            throw new InvalidOperationException(
                "유진 TODO 데이터가 생성되지 않았습니다. " +
                "Apply Olaf + Yujin TODO Design 실행 결과를 확인하세요.");
        }

        string prefabPath =
            PrefabRoot + "/Yujin_Test.prefab";

        GameObject root =
            CreatePrimitiveCharacter<Yujin>(
                "Yujin_Test",
                "Yujin_View",
                animations.Controller,
                material,
                PrimitiveCharacterStyle.Yujin,
                1f);

        try
        {
            Yujin yujin = root.GetComponent<Yujin>();
            yujin.ConfigureAuthoringCore(
                data,
                data.CombatLoadout);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Character prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                ?.GetComponent<Yujin>();

        bundle.ConfigurePrefab(prefab);
        bundle.ConfigurePresentationProfile(animations.Profile);
        ConfigureBundleAnimator(
            bundle,
            animations.Controller,
            null);

        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();

        CharacterPrefabAssemblyUtility.Assemble(
            bundle,
            ensureStandardComponents: true,
            createStandardHierarchy: true);

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(prefabPath)
            ?.GetComponent<Yujin>();
    }

    private static NormalEnemy BuildNormalEnemyPrefab(
        TestAnimationAssets animations,
        Material material)
    {
        SkillDefinition normalAttack =
            CreateDiceSkill(
                SkillRoot + "/NormalEnemy_Swipe.asset",
                "test_normal_swipe",
                "할퀴기",
                ActionType.NormalAttack,
                0,
                3,
                11,
                17,
                "균등 D6 기반 3회 공격이다.");

        SkillDefinition duel =
            CreateDiceSkill(
                SkillRoot + "/NormalEnemy_Lunge.asset",
                "test_normal_lunge",
                "달려들기",
                ActionType.Duel,
                1,
                3,
                13,
                19,
                "결투 대 결투에서 사용하는 일반 적의 돌진 공격이다.");

        CharacterCombatLoadout loadout =
            LoadOrCreate<CharacterCombatLoadout>(
                DataRoot + "/NormalEnemy_Test_Loadout.asset");

        loadout.NormalSkills =
            new List<SkillDefinition> { normalAttack };
        loadout.DuelSkills =
            new List<SkillDefinition> { duel };
        loadout.PreparationSkills =
            new List<SkillDefinition>();
        loadout.PrestigeSkills =
            new List<SkillDefinition>();
        loadout.NormalSkillPool =
            new List<SkillDefinition>(loadout.NormalSkills);
        loadout.DuelSkillPool =
            new List<SkillDefinition>(loadout.DuelSkills);
        EditorUtility.SetDirty(loadout);

        NormalEnemySkillSet skillSet =
            LoadOrCreate<NormalEnemySkillSet>(
                DataRoot + "/NormalEnemy_Test_SkillSet.asset");

        skillSet.NormalAttack = normalAttack;
        skillSet.DuelSkill = duel;
        skillSet.PrestigeSkill = null;
        EditorUtility.SetDirty(skillSet);

        CharacterData data =
            LoadOrCreate<CharacterData>(
                DataRoot + "/NormalEnemy_Test_Data.asset");

        data.CharacterName = "훈련용 일반 적";
        data.RoleName = "일반 적";
        data.UiSummary = "일반전투와 혼합전투 검증용 단일 체력 적.";
        data.CombatantTier = CombatantTier.NormalEnemy;
        data.TargetMode = CharacterTargetMode.SingleHP;
        data.SingleHpMax = 60;
        data.maxPrestige = 50;
        data.maxEnergy = 3;
        data.minSpeed = 1;
        data.maxSpeed = 6;
        data.CombatLoadout = loadout;
        data.ActionSlots =
            new List<CharacterSlotConfig>
            {
                CreateSingleHpEnemySlot()
            };
        EditorUtility.SetDirty(data);

        CharacterAuthoringBundle bundle =
            LoadOrCreate<CharacterAuthoringBundle>(
                DataRoot + "/NormalEnemy_Test_Bundle.asset");

        bundle.ConfigureCore(
            CharacterAuthoringKind.NormalEnemy,
            "훈련용 일반 적",
            data,
            skillSet,
            loadout,
            null);

        bundle.ConfigureNormalEnemy(60, true);
        bundle.ConfigurePresentationProfile(animations.Profile);
        ConfigureBundleAnimator(bundle, animations.Controller, null);
        EditorUtility.SetDirty(bundle);

        string prefabPath =
            PrefabRoot + "/NormalEnemy_Test.prefab";

        GameObject root =
            CreatePrimitiveCharacter<NormalEnemy>(
                "NormalEnemy_Test",
                "NormalEnemy_View",
                animations.Controller,
                material,
                PrimitiveCharacterStyle.NormalEnemy,
                0.9f);

        try
        {
            NormalEnemy enemy =
                root.GetComponent<NormalEnemy>();

            enemy.ConfigureAuthoringCore(
                data,
                loadout);

            CharacterAuthoringLink link =
                root.AddComponent<CharacterAuthoringLink>();

            link.Configure(bundle);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        NormalEnemy prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                ?.GetComponent<NormalEnemy>();

        bundle.ConfigurePrefab(prefab);
        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();

        CharacterPrefabAssemblyUtility.Assemble(
            bundle,
            ensureStandardComponents: true,
            createStandardHierarchy: true);

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(prefabPath)
            ?.GetComponent<NormalEnemy>();
    }

    private static EliteEnemy BuildBossPrefab(
        EliteEnemy sceneElite,
        TestAnimationAssets animations,
        Material bossMaterial)
    {
        CharacterAuthoringLink sourceLink =
            sceneElite.GetComponent<CharacterAuthoringLink>();

        CharacterAuthoringBundle sourceBundle =
            sourceLink?.Bundle;

        EliteEnemySkillSet sourceSkillSet =
            sourceBundle?.SkillSet as EliteEnemySkillSet;

        if (sourceSkillSet == null)
        {
            SerializedObject eliteSerialized =
                new SerializedObject(sceneElite);

            sourceSkillSet =
                eliteSerialized.FindProperty("skillSet")
                    ?.objectReferenceValue as EliteEnemySkillSet;
        }

        if (sourceSkillSet == null)
        {
            throw new InvalidOperationException(
                "현재 정예 적에서 EliteEnemySkillSet을 찾지 못해 보스 Prefab을 만들 수 없습니다.");
        }

        CharacterCombatLoadout sourceLoadout =
            sourceBundle?.CombatLoadout ??
            sceneElite.Data?.CombatLoadout;

        CharacterData bossData =
            LoadOrCreate<CharacterData>(
                DataRoot + "/BossEnemy_Test_Data.asset");

        bossData.CharacterName = "훈련용 보스";
        bossData.RoleName = "보스";
        bossData.UiSummary = "보스전투 검증용 단독 보스. 현재 정예 스킬셋을 재사용한다.";
        bossData.CombatantTier = CombatantTier.Boss;
        bossData.TargetMode = CharacterTargetMode.BodyParts;
        bossData.maxPrestige = 100;
        bossData.maxEnergy = 3;
        bossData.minSpeed = 2;
        bossData.maxSpeed = 6;
        bossData.CombatLoadout = sourceLoadout;
        bossData.ActionSlots =
            CloneSlots(sceneElite.Data?.ActionSlots);

        BossPhaseData phaseOne =
            BuildBossPhase(
                DataRoot + "/BossEnemy_Test_Phase01.asset",
                "PHASE_01",
                "위압",
                1f,
                sourceLoadout,
                1f,
                1f,
                0.35f,
                0.8f);

        BossPhaseData phaseTwo =
            BuildBossPhase(
                DataRoot + "/BossEnemy_Test_Phase02.asset",
                "PHASE_02",
                "격노",
                0.5f,
                sourceLoadout,
                1.2f,
                1.25f,
                0.1f,
                1.4f);

        bossData.BossPhases =
            new List<BossPhaseData>
            {
                phaseOne,
                phaseTwo
            };

        EditorUtility.SetDirty(bossData);

        CharacterAuthoringBundle bossBundle =
            LoadOrCreate<CharacterAuthoringBundle>(
                DataRoot + "/BossEnemy_Test_Bundle.asset");

        bossBundle.ConfigureCore(
            CharacterAuthoringKind.EliteEnemy,
            "훈련용 보스",
            bossData,
            sourceSkillSet,
            sourceLoadout,
            sourceBundle?.VisualProfile);

        bossBundle.ConfigureEliteEnemy(
            true,
            sourceBundle?.ElitePostureSettings);

        CharacterPresentationProfile presentation =
            sourceBundle?.PresentationProfile ??
            sceneElite.GetComponent<CharacterView>()?.PresentationProfile ??
            animations.Profile;

        RuntimeAnimatorController controller =
            sourceBundle?.AnimatorController ??
            sceneElite.GetComponentInChildren<Animator>(true)
                ?.runtimeAnimatorController ??
            animations.Controller;

        Avatar avatar =
            sourceBundle?.Avatar ??
            sceneElite.GetComponentInChildren<Animator>(true)
                ?.avatar;

        bossBundle.ConfigurePresentationProfile(presentation);
        ConfigureBundleAnimator(bossBundle, controller, avatar);
        EditorUtility.SetDirty(bossBundle);

        string prefabPath =
            PrefabRoot + "/BossEnemy_Test.prefab";

        GameObject clone =
            Object.Instantiate(sceneElite.gameObject);

        try
        {
            clone.name = "BossEnemy_Test";
            clone.SetActive(true);
            clone.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            clone.transform.localScale =
                sceneElite.transform.localScale * 1.3f;

            ApplyMaterialToRenderers(
                clone,
                bossMaterial);

            EliteEnemy boss =
                clone.GetComponent<EliteEnemy>();

            boss.ConfigureAuthoringCore(
                bossData,
                sourceLoadout);

            CharacterAuthoringLink link =
                clone.GetComponent<CharacterAuthoringLink>() ??
                clone.AddComponent<CharacterAuthoringLink>();

            link.Configure(bossBundle);

            PrefabUtility.SaveAsPrefabAsset(
                clone,
                prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }

        EliteEnemy prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                ?.GetComponent<EliteEnemy>();

        bossBundle.ConfigurePrefab(prefab);
        EditorUtility.SetDirty(bossBundle);
        AssetDatabase.SaveAssets();

        CharacterPrefabAssemblyUtility.Assemble(
            bossBundle,
            ensureStandardComponents: true,
            createStandardHierarchy: true);

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(prefabPath)
            ?.GetComponent<EliteEnemy>();
    }

    private static BossPhaseData BuildBossPhase(
        string path,
        string id,
        string displayName,
        float hpThreshold,
        CharacterCombatLoadout loadout,
        float normalWeight,
        float duelWeight,
        float preparationWeight,
        float prestigeWeight)
    {
        BossPhaseData phase =
            LoadOrCreate<BossPhaseData>(path);

        phase.PhaseId = id;
        phase.DisplayName = displayName;
        phase.EnterAtOrBelowHpRate =
            Mathf.Clamp01(hpThreshold);
        phase.MinimumTurn = 1;
        phase.RequireMomentumAtOrBelow = false;
        phase.SlotConfigs = new List<CharacterSlotConfig>();
        phase.NormalSkillPool =
            CopyDefinitions(loadout?.NormalSkillPool, loadout?.NormalSkills);
        phase.DuelSkillPool =
            CopyDefinitions(loadout?.DuelSkillPool, loadout?.DuelSkills);
        phase.PreparationSkillPool =
            CopyDefinitions(
                loadout?.CharacterPreparationPool,
                loadout?.PreparationSkills);
        phase.PrestigeSkillPool =
            CopyDefinitions(loadout?.PrestigeSkillPool, loadout?.PrestigeSkills);
        phase.NormalWeight = normalWeight;
        phase.DuelWeight = duelWeight;
        phase.PreparationWeight = preparationWeight;
        phase.PrestigeWeight = prestigeWeight;
        phase.QueuedActionPolicy =
            BossPhaseQueuedActionPolicy.KeepAlreadyPlanned;
        EditorUtility.SetDirty(phase);
        return phase;
    }

    private static List<SkillDefinition> CopyDefinitions(
        IReadOnlyList<SkillDefinition> preferred,
        IReadOnlyList<SkillDefinition> fallback)
    {
        IReadOnlyList<SkillDefinition> source =
            preferred != null && preferred.Count > 0
                ? preferred
                : fallback;

        List<SkillDefinition> result = new();

        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            SkillDefinition definition = source[i];

            if (definition != null && !result.Contains(definition))
                result.Add(definition);
        }

        return result;
    }

    private static Character SaveSceneCharacterAsTestPrefab(
        Character source,
        string prefabPath,
        string prefabName)
    {
        if (source == null)
            return null;

        GameObject clone =
            Object.Instantiate(source.gameObject);

        try
        {
            clone.name = prefabName;
            clone.SetActive(true);
            clone.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            PrefabUtility.SaveAsPrefabAsset(
                clone,
                prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(prefabPath)
            ?.GetComponent<Character>();
    }

    private static GameObject CreatePrimitiveCharacter<T>(
        string rootName,
        string viewName,
        RuntimeAnimatorController controller,
        Material material,
        PrimitiveCharacterStyle style,
        float scale)
        where T : Character
    {
        GameObject root = new(rootName);
        root.AddComponent<T>();

        CapsuleCollider collider =
            root.AddComponent<CapsuleCollider>();

        collider.center = new Vector3(0f, 1.1f, 0f);
        collider.height = 2.35f;
        collider.radius = 0.42f;

        GameObject view = new(viewName);
        view.transform.SetParent(root.transform, false);

        GameObject model = new("Model");
        model.transform.SetParent(view.transform, false);
        model.transform.localScale = Vector3.one * scale;

        Animator animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CreatePrimitivePart(
            model.transform,
            "Torso",
            PrimitiveType.Capsule,
            new Vector3(0f, 1.35f, 0f),
            Quaternion.identity,
            new Vector3(0.72f, 0.78f, 0.46f),
            material);

        CreatePrimitivePart(
            model.transform,
            "Head",
            PrimitiveType.Sphere,
            new Vector3(0f, 2.25f, 0f),
            Quaternion.identity,
            Vector3.one * 0.48f,
            material);

        Transform leftArmPivot =
            CreatePivot(
                model.transform,
                "LeftArmPivot",
                new Vector3(-0.62f, 1.78f, 0f),
                Quaternion.Euler(0f, 0f, 8f));

        CreatePrimitivePart(
            leftArmPivot,
            "LeftArm",
            PrimitiveType.Capsule,
            new Vector3(-0.35f, -0.12f, 0f),
            Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.28f, 0.62f, 0.28f),
            material);

        Transform rightArmPivot =
            CreatePivot(
                model.transform,
                "RightArmPivot",
                new Vector3(0.62f, 1.78f, 0f),
                Quaternion.Euler(0f, 0f, -8f));

        CreatePrimitivePart(
            rightArmPivot,
            "RightArm",
            PrimitiveType.Capsule,
            new Vector3(0.35f, -0.12f, 0f),
            Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.28f, 0.62f, 0.28f),
            material);

        Transform leftLegPivot =
            CreatePivot(
                model.transform,
                "LeftLegPivot",
                new Vector3(-0.24f, 0.72f, 0f),
                Quaternion.identity);

        CreatePrimitivePart(
            leftLegPivot,
            "LeftLeg",
            PrimitiveType.Capsule,
            new Vector3(0f, -0.42f, 0f),
            Quaternion.identity,
            new Vector3(0.33f, 0.65f, 0.33f),
            material);

        Transform rightLegPivot =
            CreatePivot(
                model.transform,
                "RightLegPivot",
                new Vector3(0.24f, 0.72f, 0f),
                Quaternion.identity);

        CreatePrimitivePart(
            rightLegPivot,
            "RightLeg",
            PrimitiveType.Capsule,
            new Vector3(0f, -0.42f, 0f),
            Quaternion.identity,
            new Vector3(0.33f, 0.65f, 0.33f),
            material);

        Transform weaponPivot =
            CreatePivot(
                model.transform,
                "WeaponPivot",
                style == PrimitiveCharacterStyle.Yujin
                    ? new Vector3(0.95f, 1.45f, 0f)
                    : new Vector3(0.9f, 1.4f, 0f),
                Quaternion.Euler(0f, 0f, -15f));

        GameObject weapon =
            CreatePrimitivePart(
                weaponPivot,
                "Weapon",
                PrimitiveType.Cube,
                new Vector3(0f, 0.35f, 0f),
                Quaternion.identity,
                style == PrimitiveCharacterStyle.Yujin
                    ? new Vector3(0.08f, 0.75f, 0.12f)
                    : new Vector3(0.14f, 0.62f, 0.18f),
                material);

        CreateAnchors(view.transform);
        CreateCameraPoints(view.transform);
        return root;
    }

    private static Transform CreatePivot(
        Transform parent,
        string name,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        GameObject pivot = new(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = localPosition;
        pivot.transform.localRotation = localRotation;
        return pivot.transform;
    }

    private static GameObject CreatePrimitivePart(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject part =
            GameObject.CreatePrimitive(primitiveType);

        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider primitiveCollider =
            part.GetComponent<Collider>();

        if (primitiveCollider != null)
            Object.DestroyImmediate(primitiveCollider);

        Renderer renderer =
            part.GetComponent<Renderer>();

        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return part;
    }

    private static void CreateAnchors(Transform visualRoot)
    {
        Transform root =
            EnsureChild(visualRoot, "Anchors");

        SetLocalPose(
            EnsureChild(root, "HEAD_Anchor"),
            new Vector3(0f, 2.25f, 0f));

        SetLocalPose(
            EnsureChild(root, "LEFT_HAND_Anchor"),
            new Vector3(-1f, 1.45f, 0f));

        SetLocalPose(
            EnsureChild(root, "RIGHT_HAND_Anchor"),
            new Vector3(1f, 1.45f, 0f));

        SetLocalPose(
            EnsureChild(root, "LEGS_Anchor"),
            new Vector3(0f, 0.35f, 0f));
    }

    private static void CreateCameraPoints(Transform visualRoot)
    {
        Transform root =
            EnsureChild(visualRoot, "CameraPoints");

        SetLocalPose(
            EnsureChild(root, "Root"),
            Vector3.zero);

        SetLocalPose(
            EnsureChild(root, "Center"),
            new Vector3(0f, 1.25f, 0f));

        SetLocalPose(
            EnsureChild(root, "Head"),
            new Vector3(0f, 2.25f, 0f));

        SetLocalPose(
            EnsureChild(root, "Chest"),
            new Vector3(0f, 1.55f, 0f));

        SetLocalPose(
            EnsureChild(root, "LeftHand"),
            new Vector3(-1f, 1.45f, 0f));

        SetLocalPose(
            EnsureChild(root, "RightHand"),
            new Vector3(1f, 1.45f, 0f));

        SetLocalPose(
            EnsureChild(root, "WeaponMain"),
            new Vector3(1.1f, 1.6f, 0f));

        SetLocalPose(
            EnsureChild(root, "WeaponSub"),
            new Vector3(-1.1f, 1.6f, 0f));

        SetLocalPose(
            EnsureChild(root, "Feet"),
            new Vector3(0f, 0.05f, 0f));

        SetLocalPose(
            EnsureChild(root, "LookAtPoint"),
            new Vector3(0f, 1.55f, 0f));

        SetLocalPose(
            EnsureChild(root, "CloseCameraPoint"),
            new Vector3(1.4f, 1.7f, -2.2f));

        SetLocalPose(
            EnsureChild(root, "OverShoulderCameraPoint"),
            new Vector3(1.1f, 1.8f, -2.8f));

        SetLocalPose(
            EnsureChild(root, "HitImpactCameraPoint"),
            new Vector3(0.7f, 1.5f, -1.7f));

        SetLocalPose(
            EnsureChild(root, "SideCameraPoint"),
            new Vector3(2.8f, 1.5f, 0f));

        SetLocalPose(
            EnsureChild(root, "ClashRollCameraPoint"),
            new Vector3(1.7f, 1.55f, -2.4f));

        SetLocalPose(
            EnsureChild(root, "DetailCameraPoint"),
            new Vector3(0.9f, 1.65f, -1.5f));
    }

    private static Transform EnsureChild(
        Transform parent,
        string name)
    {
        Transform child =
            parent.Find(name);

        if (child != null)
            return child;

        GameObject created = new(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void SetLocalPose(
        Transform target,
        Vector3 localPosition)
    {
        if (target == null)
            return;

        target.localPosition = localPosition;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void SetupCurrentScene(
        Character olafPrefab,
        Character yujinPrefab,
        Character normalEnemyPrefab,
        Character eliteEnemyPrefab,
        Character bossEnemyPrefab)
    {
        BattleManager battleManager =
            Object.FindFirstObjectByType<BattleManager>(
                FindObjectsInactive.Include);

        if (battleManager == null)
        {
            throw new InvalidOperationException(
                "현재 Scene에서 BattleManager를 찾지 못했습니다.");
        }

        BattleRosterController roster =
            battleManager.GetComponent<BattleRosterController>();

        if (roster == null)
            roster = Undo.AddComponent<BattleRosterController>(battleManager.gameObject);

        Transform spawnRoot =
            FindSceneTransform("SpawnPoints");

        Transform playerSpawn =
            FindSceneTransform("PlayerSpawn");

        Transform enemySpawn0 =
            FindSceneTransform("EnemySpawn_0");

        Transform runtimeRoot =
            FindSceneTransform("RuntimeParticipants");

        if (spawnRoot == null ||
            playerSpawn == null ||
            enemySpawn0 == null)
        {
            throw new InvalidOperationException(
                "BattleRuntimeSetup의 SpawnPoints/PlayerSpawn/EnemySpawn_0을 찾지 못했습니다.");
        }

        Transform enemySpawn1 =
            EnsureSceneSpawn(
                spawnRoot,
                "EnemySpawn_1",
                enemySpawn0,
                2.1f);

        Transform enemySpawn2 =
            EnsureSceneSpawn(
                spawnRoot,
                "EnemySpawn_2",
                enemySpawn0,
                -2.1f);

        BattleTestScenarioSwitcher switcher =
            battleManager.GetComponent<BattleTestScenarioSwitcher>();

        if (switcher == null)
        {
            switcher =
                Undo.AddComponent<BattleTestScenarioSwitcher>(
                    battleManager.gameObject);
        }

        Character[] sceneCharacters =
            Object.FindObjectsByType<Character>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value =>
                value != null &&
                value.gameObject.scene.IsValid())
            .ToArray();

        switcher.ConfigureAssets(
            olafPrefab,
            yujinPrefab,
            normalEnemyPrefab,
            eliteEnemyPrefab,
            bossEnemyPrefab);

        switcher.ConfigureScene(
            battleManager,
            roster,
            playerSpawn,
            new[]
            {
                enemySpawn0,
                enemySpawn1,
                enemySpawn2
            },
            runtimeRoot,
            sceneCharacters);

        battleManager.AssignRosterController(roster);
        battleManager.SetAutoLifecycle(
            initializeOnAwake: true,
            startAutomatically: true);

        EditorUtility.SetDirty(roster);
        EditorUtility.SetDirty(switcher);
        EditorUtility.SetDirty(battleManager);
        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());
    }

    private static Transform EnsureSceneSpawn(
        Transform parent,
        string name,
        Transform center,
        float lateralOffset)
    {
        Transform existing =
            FindSceneTransform(name);

        if (existing == null)
        {
            GameObject created = new(name);
            Undo.RegisterCreatedObjectUndo(
                created,
                "Create " + name);

            created.transform.SetParent(parent, true);
            existing = created.transform;
        }

        Vector3 lateral =
            center != null
                ? center.right * lateralOffset
                : Vector3.forward * lateralOffset;

        existing.position =
            (center != null ? center.position : parent.position) +
            lateral;

        existing.rotation =
            center != null
                ? center.rotation
                : parent.rotation;

        existing.localScale = Vector3.one;
        return existing;
    }

    private static Transform FindSceneTransform(
        string objectName)
    {
        Transform[] transforms =
            Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform value = transforms[i];

            if (value != null &&
                value.gameObject.scene.IsValid() &&
                value.name == objectName)
            {
                return value;
            }
        }

        return null;
    }

    private static Character LoadCharacterPrefab(
        string prefabPath)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);

        return prefab != null
            ? prefab.GetComponent<Character>()
            : null;
    }

    private static int EnsureGeneratedSkillTimelines(
        Character olafPrefab,
        Character yujinPrefab,
        Character normalEnemyPrefab,
        Character eliteEnemyPrefab,
        Character bossEnemyPrefab,
        AnimationClip attackClip)
    {
        string[] searchRoots =
        {
            SkillRoot,
            OlafDesignRoot,
            YujinDesignRoot
        };

        HashSet<SkillDefinition> skills =
            new HashSet<SkillDefinition>();

        for (int rootIndex = 0;
             rootIndex < searchRoots.Length;
             rootIndex++)
        {
            string root = searchRoots[rootIndex];

            if (!AssetDatabase.IsValidFolder(root))
                continue;

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:SkillDefinition",
                    new[] { root });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                SkillDefinition skill =
                    AssetDatabase.LoadAssetAtPath<
                        SkillDefinition>(path);

                if (skill != null)
                    skills.Add(skill);
            }
        }

        int completedCount = 0;

        foreach (SkillDefinition skill in skills)
        {
            string path =
                AssetDatabase.GetAssetPath(skill)
                    .Replace('\\', '/');

            Character attacker =
                ResolveSkillAttackerPrefab(
                    path,
                    olafPrefab,
                    yujinPrefab,
                    normalEnemyPrefab,
                    eliteEnemyPrefab,
                    bossEnemyPrefab);

            Character target =
                attacker is Enemy
                    ? yujinPrefab ?? olafPrefab
                    : normalEnemyPrefab ??
                      eliteEnemyPrefab ??
                      bossEnemyPrefab;

            SkillVisualDefinition visual =
                SkillCutsceneAssetBuilder
                    .EnsureForSkill(
                        skill,
                        attacker,
                        target);

            if (visual == null)
            {
                throw new InvalidOperationException(
                    $"SkillVisualDefinition 생성 실패: {path}");
            }

            if (visual.AttackerAnimation == null &&
                attackClip != null)
            {
                visual.AttackerAnimation = attackClip;
                EditorUtility.SetDirty(visual);

                // 첫 Ensure에서 생성된 빈 Animation Track에
                // 테스트 공격 Clip을 실제로 삽입한다.
                visual =
                    SkillCutsceneAssetBuilder
                        .EnsureForSkill(
                            skill,
                            attacker,
                            target);
            }

            if (!visual.HasTimelineCutscene)
            {
                string missing =
                    string.Join(
                        ", ",
                        visual.GetMissingRequirements());

                throw new InvalidOperationException(
                    $"불완전한 SkillVisualDefinition: {path} / Missing={missing}");
            }

            completedCount++;
        }

        return completedCount;
    }

    private static Character ResolveSkillAttackerPrefab(
        string assetPath,
        Character olafPrefab,
        Character yujinPrefab,
        Character normalEnemyPrefab,
        Character eliteEnemyPrefab,
        Character bossEnemyPrefab)
    {
        if (ContainsPathToken(
                assetPath,
                "/Yujin/"))
        {
            return yujinPrefab;
        }

        if (ContainsPathToken(
                assetPath,
                "/Olaf/"))
        {
            return olafPrefab;
        }

        if (ContainsPathToken(
                assetPath,
                "Boss"))
        {
            return bossEnemyPrefab ??
                   eliteEnemyPrefab ??
                   normalEnemyPrefab;
        }

        return normalEnemyPrefab ??
               eliteEnemyPrefab ??
               bossEnemyPrefab;
    }

    private static bool ContainsPathToken(
        string source,
        string token)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               !string.IsNullOrWhiteSpace(token) &&
               source.IndexOf(
                   token,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static SkillDefinition CreateDiceSkill(
        string path,
        string skillId,
        string displayName,
        ActionType actionType,
        int energyCost,
        int rollCount,
        int minimumPower,
        int maximumPower,
        string description)
    {
        SkillDefinition definition =
            LoadOrCreate<SkillDefinition>(path);

        SetSkillId(definition, skillId);
        definition.SkillName = displayName;
        definition.ActionType = actionType;
        definition.BasePower = 0;
        definition.CanBreakPart = false;
        definition.GainPrestige = true;
        definition.Description = description;
        definition.OverrideEnergyCost = true;
        definition.EnergyCost = Mathf.Max(0, energyCost);
        definition.ResolverType = SkillResolverType.Dice;
        definition.DiceMin = 1;
        definition.DiceMax = 6;
        definition.ExchangeRollCount =
            Mathf.Clamp(rollCount, 1, 8);
        definition.RollReusePolicy =
            SkillRollReusePolicy.RollEachExchange;
        definition.Rolls = new List<SkillRollData>();

        for (int i = 0; i < definition.ExchangeRollCount; i++)
        {
            definition.Rolls.Add(
                new SkillRollData
                {
                    Index = i,
                    Type = CombatRollType.Attack,
                    MinPower = minimumPower,
                    MaxPower = maximumPower,
                    RngSource = RollRngSource.Dice
                });
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static CharacterSlotConfig CreateSingleHpEnemySlot()
    {
        return new CharacterSlotConfig
        {
            SlotId = "BODY",
            DisplayName = "몸체",
            Enabled = true,
            HasLinkedPart = false,
            OverrideSpeedRange = false,
            AllowedActionTypes =
                new List<ActionType>
                {
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige
                }
        };
    }

    private static List<CharacterSlotConfig> CloneSlots(
        IReadOnlyList<CharacterSlotConfig> source)
    {
        List<CharacterSlotConfig> result = new();

        if (source == null || source.Count == 0)
        {
            result.Add(
                CreateBodyPartSlot(
                    "HEAD",
                    "머리",
                    PartType.HEAD,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Preparation,
                    ActionType.Prestige));

            result.Add(
                CreateBodyPartSlot(
                    "LEFT_ARM",
                    "왼팔",
                    PartType.LEFT_HAND,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige));

            result.Add(
                CreateBodyPartSlot(
                    "RIGHT_ARM",
                    "오른팔",
                    PartType.RIGHT_HAND,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige));

            result.Add(
                CreateBodyPartSlot(
                    "LEGS",
                    "다리",
                    PartType.LEGS,
                    ActionType.Preparation));

            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSlotConfig value = source[i];

            if (value == null)
                continue;

            result.Add(
                new CharacterSlotConfig
                {
                    SlotId = value.SlotId,
                    DisplayName = value.DisplayName,
                    Enabled = value.Enabled,
                    HasLinkedPart = value.HasLinkedPart,
                    LinkedPartType = value.LinkedPartType,
                    OverrideSpeedRange = value.OverrideSpeedRange,
                    MinSpeed = value.MinSpeed,
                    MaxSpeed = value.MaxSpeed,
                    AllowedActionTypes =
                        value.AllowedActionTypes != null
                            ? new List<ActionType>(value.AllowedActionTypes)
                            : new List<ActionType>()
                });
        }

        return result;
    }

    private static CharacterSlotConfig CreateBodyPartSlot(
        string id,
        string displayName,
        PartType part,
        params ActionType[] allowed)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = displayName,
            Enabled = true,
            HasLinkedPart = true,
            LinkedPartType = part,
            AllowedActionTypes =
                new List<ActionType>(allowed)
        };
    }

    private static void SetSkillId(
        SkillDefinition definition,
        string skillId)
    {
        if (definition == null)
            return;

        SerializedObject serialized =
            new SerializedObject(definition);

        SerializedProperty property =
            serialized.FindProperty("skillId");

        if (property != null)
        {
            property.stringValue = skillId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static AnimationClip CreateClip(
        string path,
        bool loop,
        Action<AnimationClip> configure)
    {
        AnimationClip clip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        bool created =
            clip == null;

        if (created)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            // 기존 GUID를 유지해 Prefab, Timeline, Bundle 참조가 끊기지 않게 한다.
            clip.ClearCurves();
        }

        clip.name =
            Path.GetFileNameWithoutExtension(path);
        clip.frameRate = 30f;
        clip.legacy = false;

        configure?.Invoke(clip);

        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);

        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(
            clip,
            settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateAnimatorController(
        string path,
        AnimationClip idle,
        AnimationClip hit,
        AnimationClip death)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        if (controller == null)
        {
            controller =
                AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        // Asset을 삭제하지 않고 기존 Controller를 갱신한다.
        // 이렇게 해야 Bundle과 Prefab의 RuntimeAnimatorController 참조 GUID가 유지된다.
        EnsureAnimatorParameter(
            controller,
            "VisualState",
            AnimatorControllerParameterType.Float);

        EnsureAnimatorParameter(
            controller,
            "Hit",
            AnimatorControllerParameterType.Trigger);

        EnsureAnimatorParameter(
            controller,
            "Dead",
            AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine =
            controller.layers[0].stateMachine;

        AnimatorState idleState =
            FindOrCreateState(
                stateMachine,
                "Idle");
        idleState.motion = idle;
        stateMachine.defaultState = idleState;

        AnimatorState hitState =
            FindOrCreateState(
                stateMachine,
                "Hit");
        hitState.motion = hit;

        AnimatorState deadState =
            FindOrCreateState(
                stateMachine,
                "Dead");
        deadState.motion = death;

        EnsureAnyStateTriggerTransition(
            stateMachine,
            hitState,
            "Hit",
            0.02f);

        EnsureStateTransition(
            hitState,
            idleState,
            0.95f,
            0.04f);

        EnsureAnyStateTriggerTransition(
            stateMachine,
            deadState,
            "Dead",
            0.03f);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureAnimatorParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(
                parameter =>
                    parameter != null &&
                    parameter.name == name))
        {
            return;
        }

        controller.AddParameter(
            name,
            type);
    }

    private static AnimatorState FindOrCreateState(
        AnimatorStateMachine stateMachine,
        string name)
    {
        ChildAnimatorState[] states =
            stateMachine.states;

        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state =
                states[i].state;

            if (state != null &&
                state.name == name)
            {
                return state;
            }
        }

        return stateMachine.AddState(name);
    }

    private static void EnsureAnyStateTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState target,
        string trigger,
        float duration)
    {
        AnimatorStateTransition[] transitions =
            stateMachine.anyStateTransitions;

        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition =
                transitions[i];

            if (transition == null ||
                transition.destinationState != target)
            {
                continue;
            }

            AnimatorCondition[] conditions =
                transition.conditions;

            if (conditions.Any(
                    condition =>
                        condition.parameter == trigger))
            {
                return;
            }
        }

        AnimatorStateTransition created =
            stateMachine.AddAnyStateTransition(target);

        created.hasExitTime = false;
        created.duration = duration;
        created.canTransitionToSelf = false;
        created.AddCondition(
            AnimatorConditionMode.If,
            0f,
            trigger);
    }

    private static void EnsureStateTransition(
        AnimatorState source,
        AnimatorState target,
        float exitTime,
        float duration)
    {
        AnimatorStateTransition[] transitions =
            source.transitions;

        for (int i = 0; i < transitions.Length; i++)
        {
            if (transitions[i] != null &&
                transitions[i].destinationState == target)
            {
                return;
            }
        }

        AnimatorStateTransition created =
            source.AddTransition(target);

        created.hasExitTime = true;
        created.exitTime = exitTime;
        created.duration = duration;
    }

    private static void SetCurve(
        AnimationClip clip,
        string path,
        string property,
        AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(
                path,
                typeof(Transform),
                property),
            curve);
    }

    private static AnimationCurve Curve(
        params (float time, float value)[] keys)
    {
        Keyframe[] frames =
            new Keyframe[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            frames[i] =
                new Keyframe(
                    keys[i].time,
                    keys[i].value);
        }

        return new AnimationCurve(frames);
    }

    private static Material CreateOrUpdateMaterial(
        string path,
        Color color,
        Color emission)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "테스트 Material에 사용할 Shader를 찾지 못했습니다.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.45f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(
        Material material,
        Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ApplyMaterialToRenderers(
        GameObject root,
        Material material)
    {
        if (root == null || material == null)
            return;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            Material[] materials =
                renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
                materials[j] = material;

            renderer.sharedMaterials = materials;
        }
    }

    private static void RepairGeneratedAnimationReferences(
        TestAnimationAssets animations)
    {
        if (animations.Controller == null)
            return;

        string[] bundlePaths =
        {
            YujinBundlePath,
            DataRoot + "/NormalEnemy_Test_Bundle.asset"
        };

        for (int i = 0; i < bundlePaths.Length; i++)
        {
            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(
                        bundlePaths[i]);

            if (bundle == null)
                continue;

            bundle.ConfigurePresentationProfile(
                animations.Profile);

            ConfigureBundleAnimator(
                bundle,
                animations.Controller,
                bundle.Avatar);

            EditorUtility.SetDirty(bundle);

            if (bundle.CharacterPrefab != null)
            {
                CharacterPrefabAssemblyUtility.Assemble(
                    bundle,
                    ensureStandardComponents: true,
                    createStandardHierarchy: true);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureBundleAnimator(
        CharacterAuthoringBundle bundle,
        RuntimeAnimatorController controller,
        Avatar avatar)
    {
        if (bundle == null)
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        SetObjectProperty(
            serialized,
            "animatorController",
            controller);

        SetObjectProperty(
            serialized,
            "avatar",
            avatar);

        SetBoolProperty(
            serialized,
            "overrideAnimatorController",
            controller != null);

        SetBoolProperty(
            serialized,
            "overrideAvatar",
            avatar != null);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectProperty(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBoolProperty(
        SerializedObject serialized,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    private static T LoadOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/2. Data");
        EnsureFolder(Root);
        EnsureFolder(PrefabRoot);
        EnsureFolder(DataRoot);
        EnsureFolder(SkillRoot);
        EnsureFolder(AnimationRoot);
        EnsureFolder(MaterialRoot);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent =
            Path.GetDirectoryName(path)
                ?.Replace('\\', '/');

        string name =
            Path.GetFileName(path);

        if (!string.IsNullOrWhiteSpace(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private readonly struct TestAnimationAssets
    {
        public RuntimeAnimatorController Controller { get; }
        public CharacterPresentationProfile Profile { get; }
        public AnimationClip AttackClip { get; }

        public TestAnimationAssets(
            RuntimeAnimatorController controller,
            CharacterPresentationProfile profile,
            AnimationClip attackClip)
        {
            Controller = controller;
            Profile = profile;
            AttackClip = attackClip;
        }
    }

    private enum PrimitiveCharacterStyle
    {
        Yujin = 0,
        NormalEnemy = 1
    }
}
#endif