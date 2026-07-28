#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>
/// Project Abyss의 Character Studio와 Skill Cutscene Studio 기반 기능을 한 번에 검증할 수 있도록
/// 완성형 올라프 샘플을 생성한다.
///
/// 생성 결과는 기존 올라프 원본을 수정하지 않고
/// Assets/2. Data/Characters/Olaf/Generated Complete 아래에만 저장된다.
/// </summary>
public static class OlafCompleteCharacterBuilder
{
    private const string GeneratedRoot =
        "Assets/2. Data/Characters/Olaf/Generated Complete";

    private const string PrefabFolder =
        GeneratedRoot + "/Prefabs";

    private const string DataFolder =
        GeneratedRoot + "/Data";

    private const string SkillFolder =
        GeneratedRoot + "/Skills";

    private const string BuildFolder =
        GeneratedRoot + "/Build";

    private const string AnimationFolder =
        GeneratedRoot + "/Camera Motion";

    private const string ReportPath =
        GeneratedRoot + "/Olaf_Complete_Build_Report.md";

    private const string BundlePath =
        GeneratedRoot + "/Olaf_Complete_CharacterBundle.asset";

    private const string PrefabPath =
        PrefabFolder + "/Olaf_Complete.prefab";

    private const string TargetPrefabPath =
        PrefabFolder + "/EliteEnemy_CutscenePreview.prefab";

    private const string GeneratedPrestigeSkillPath =
        SkillFolder + "/Olaf_ImmortalFrenzy_Complete.asset";

    private const string OriginalDataPath =
        "Assets/2. Data/Characters/Olaf/Olaf Status Data.asset";

    private const string OriginalNormalPath =
        "Assets/2. Data/Characters/Olaf/Skills/피의 일격(일반공격).asset";

    private const string OriginalDuelPath =
        "Assets/2. Data/Characters/Olaf/Skills/광전사의 결투(결투).asset";

    private const string OriginalPreparationPath =
        "Assets/2. Data/Characters/Olaf/Skills/광기의 난도질(도사림).asset";

    private const string OriginalPrestigePath =
        "Assets/2. Data/Characters/Olaf/Skills/불사의 광란(위세).asset";

    private const string VisualProfilePath =
        "Assets/2. Data/BattleVisual/SkillVisualProfiles/Olaf Skill VP.asset";

    private const string BloodVfxPath =
        "Assets/2. Data/BattleVisual/VFXDefinitions/Skills/Olaf/Olaf_Duel_BloodSplash_VFX.asset";

    private const string AnimatorControllerPath =
        "Assets/2. Data/Characters/Olaf/AnimatorControllers/OlafAnimatorController.controller";

    private const string ModelPath =
        "Assets/2. Data/Characters/Olaf/Models/Olaf_Model.fbx";

    private const string NormalAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_NormalAttack.anim";

    private const string DuelAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Duel.anim";

    private const string PreparationAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Preparation.anim";

    private const string PrestigeAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Prestige.anim";

    private const string IdleAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Idle.anim";

    private const string HitAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_HitMotion.anim";

    private const string BrokenAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_BrokenIdle.anim";

    private const string DeathAnimationPath =
        "Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Death.anim";

    [MenuItem(
        "Tools/Project Abyss/Samples/Build Complete Olaf (One Click)",
        false,
        2030)]
    public static void BuildFromMenu()
    {
        bool proceed =
            !AssetDatabase.IsValidFolder(GeneratedRoot) ||
            EditorUtility.DisplayDialog(
                "완성형 올라프 다시 생성",
                "Generated Complete 폴더를 삭제한 뒤 완성형 올라프를 다시 생성합니다.\n" +
                "기존 원본 올라프 데이터는 수정하지 않습니다.",
                "다시 생성",
                "취소");

        if (!proceed)
            return;

        try
        {
            OlafCompleteBuildResult result =
                BuildCompleteOlaf();

            if (!result.Success)
            {
                EditorUtility.DisplayDialog(
                    "완성형 올라프 생성 실패",
                    result.Summary,
                    "확인");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            bool createPreview =
                EditorUtility.DisplayDialog(
                    "완성형 올라프 생성 완료",
                    result.Summary +
                    "\n\n불사의 광란 Preview Scene을 생성하고 Timeline을 열까요?",
                    "Preview와 Studio 열기",
                    "Studio만 열기");

            Debug.Log(
                "[OlafCompleteCharacterBuilder] " +
                result.Summary);

            EditorApplication.delayCall +=
                () => OpenGeneratedResult(
                    createPreview);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "완성형 올라프 생성 중 예외",
                exception.ToString(),
                "확인");
        }
    }

    private static void OpenGeneratedResult(
        bool createPreview)
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall +=
                () => OpenGeneratedResult(
                    createPreview);
            return;
        }

        try
        {
            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(
                        BundlePath);

            SkillDefinition prestige =
                AssetDatabase.LoadAssetAtPath<
                    SkillDefinition>(
                        GeneratedPrestigeSkillPath);

            Olaf attacker =
                ReloadPrefabCharacter<Olaf>(
                    PrefabPath);

            Character target =
                ReloadPrefabCharacter<Character>(
                    TargetPrefabPath) ??
                attacker;

            if (bundle == null ||
                prestige == null ||
                attacker == null ||
                target == null)
            {
                Debug.LogError(
                    "[OlafCompleteCharacterBuilder] 생성 결과를 에셋 경로에서 다시 읽지 못했습니다. " +
                    "Generated Complete 폴더를 확인하세요.");
                return;
            }

            Selection.activeObject =
                bundle;

            EditorGUIUtility.PingObject(
                bundle);

            if (createPreview)
            {
                SkillCutsceneDefinition definition =
                    prestige.VisualDefinition?
                        .CutsceneDefinition;

                if (definition != null)
                {
                    SkillCutsceneAssetBuilder
                        .CreatePreviewScene(
                            prestige,
                            definition,
                            attacker,
                            target);
                }
                else
                {
                    Debug.LogWarning(
                        "[OlafCompleteCharacterBuilder] 불사의 광란 CutsceneDefinition이 없어 " +
                        "Preview Scene 생성을 건너뜁니다.",
                        prestige);
                }
            }

            ProjectAbyssCharacterStudio.Open(
                bundle);

            ProjectAbyssSkillCutsceneStudio.Open(
                prestige,
                bundle);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem(
        "Tools/Project Abyss/Samples/Open Generated Olaf in Studios",
        false,
        2031)]
    public static void OpenGeneratedInStudios()
    {
        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<
                CharacterAuthoringBundle>(
                    BundlePath);

        if (bundle == null)
        {
            EditorUtility.DisplayDialog(
                "Generated Olaf 없음",
                "먼저 Build Complete Olaf를 실행하세요.",
                "확인");
            return;
        }

        ProjectAbyssCharacterStudio.Open(
            bundle);

        SkillDefinition prestige =
            bundle.CombatLoadout?
                .PrestigeSkills?
                .FirstOrDefault();

        if (prestige != null)
        {
            ProjectAbyssSkillCutsceneStudio.Open(
                prestige,
                bundle);
        }
    }

    [MenuItem(
        "Tools/Project Abyss/Samples/Validate Generated Olaf",
        false,
        2032)]
    public static void ValidateGenerated()
    {
        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<
                CharacterAuthoringBundle>(
                    BundlePath);

        List<string> issues =
            ValidateBundle(bundle);

        string message =
            issues.Count == 0
                ? "완성형 올라프 정적 검증 PASS"
                : string.Join("\n", issues);

        Debug.Log(
            "[OlafCompleteCharacterBuilder] " +
            message,
            bundle);

        EditorUtility.DisplayDialog(
            issues.Count == 0
                ? "검증 PASS"
                : "검증 이슈",
            message,
            "확인");
    }

    public static OlafCompleteBuildResult BuildCompleteOlaf()
    {
        PrepareForGeneratedRootRebuild();
        DeleteGeneratedRoot();
        EnsureGeneratedFolders();

        List<string> log =
            new List<string>();

        Olaf sourceOlaf =
            FindOlafTemplate();

        if (sourceOlaf == null)
        {
            return OlafCompleteBuildResult.Fail(
                "Olaf Prefab 또는 현재 Scene의 Olaf를 찾지 못했습니다. " +
                "CameraTest Scene을 열고 다시 실행하세요.");
        }

        Olaf generatedPrefab =
            CreateCharacterPrefab(
                sourceOlaf,
                PrefabPath,
                "Olaf_Complete");

        if (generatedPrefab == null)
        {
            return OlafCompleteBuildResult.Fail(
                "Olaf_Complete.prefab을 생성하지 못했습니다.");
        }

        Character previewTarget =
            CreatePreviewTargetPrefab(
                generatedPrefab);

        SkillDefinition normal =
            CreateNormalSkill();

        SkillDefinition duel =
            CreateDuelSkill();

        SkillDefinition preparation =
            CreatePreparationSkill();

        SkillDefinition prestige =
            CreatePrestigeSkill();

        SkillCatalog catalog =
            CreateAsset<SkillCatalog>(
                DataFolder +
                "/Olaf_Complete_SkillCatalog.asset");

        catalog.ReplaceAll(
            new[]
            {
                normal,
                duel,
                preparation,
                prestige
            });

        CharacterCombatLoadout loadout =
            CreateAsset<
                CharacterCombatLoadout>(
                    DataFolder +
                    "/Olaf_Complete_CombatLoadout.asset");

        ConfigureLoadout(
            loadout,
            catalog,
            normal,
            duel,
            preparation,
            prestige);

        CharacterData data =
            CreateCharacterData(
                loadout);

        OlafSkillSet skillSet =
            CreateAsset<OlafSkillSet>(
                DataFolder +
                "/Olaf_Complete_LegacySkillSet.asset");

        skillSet.NormalAttack = normal;
        skillSet.DuelSkill = duel;
        skillSet.PreparationSkill = preparation;
        skillSet.PrestigeSkill = prestige;
        EditorUtility.SetDirty(skillSet);

        OlafBloodyAxeItem bloodyAxe =
            CreateBloodyAxe();

        BattleInstinctAugment battleInstinct =
            CreateBattleInstinct();

        ToughBodyAugment toughBody =
            CreateToughBody();

        EnergyCapacityAugment energyCapacity =
            CreateEnergyCapacity();

        CharacterAuthoringBundle bundle =
            CreateAsset<CharacterAuthoringBundle>(
                BundlePath);

        bundle.ConfigureCore(
            CharacterAuthoringKind.Olaf,
            "올라프 - 완성형 Timeline 샘플",
            data,
            skillSet,
            loadout,
            null);

        bundle.ConfigurePrefab(
            generatedPrefab);

        bundle.ConfigureLoadout(
            new CharacterItem[]
            {
                bloodyAxe
            },
            new CharacterAugment[]
            {
                battleInstinct,
                toughBody,
                energyCapacity
            },
            true);

        ConfigureBundlePresentation(
            bundle);

        CharacterPrefabAssemblyReport firstAssembly =
            CharacterPrefabAssemblyUtility.Assemble(
                bundle,
                ensureStandardComponents: true,
                createStandardHierarchy: true);

        log.Add(
            "[Character Studio Assembly 1]\n" +
            firstAssembly.BuildSummary());

        generatedPrefab =
            bundle.CharacterPrefab as Olaf;

        if (generatedPrefab == null)
        {
            generatedPrefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(PrefabPath)?
                    .GetComponentInChildren<
                        Olaf>(true);
        }

        BuildAllCutscenes(
            data,
            generatedPrefab,
            previewTarget ?? generatedPrefab,
            normal,
            duel,
            preparation,
            prestige,
            log);

        SkillVisualProfile visualProfile =
            CreateAsset<SkillVisualProfile>(
                DataFolder +
                "/Olaf_Complete_VisualProfile.asset");

        visualProfile.NormalAttackVisual =
            normal.VisualDefinition;

        visualProfile.DuelVisual =
            duel.VisualDefinition;

        visualProfile.PreparationVisual =
            preparation.VisualDefinition;

        visualProfile.PrestigeVisual =
            prestige.VisualDefinition;

        EditorUtility.SetDirty(
            visualProfile);

        bundle.ConfigureCore(
            CharacterAuthoringKind.Olaf,
            "올라프 - 완성형 Timeline 샘플",
            data,
            skillSet,
            loadout,
            visualProfile);

        RegisterBundleAssets(
            bundle,
            new Object[]
            {
                data,
                loadout,
                catalog,
                skillSet,
                visualProfile,
                normal,
                duel,
                preparation,
                prestige,
                normal.VisualDefinition,
                duel.VisualDefinition,
                preparation.VisualDefinition,
                prestige.VisualDefinition,
                bloodyAxe,
                battleInstinct,
                toughBody,
                energyCapacity
            });

        CharacterPrefabAssemblyReport finalAssembly =
            CharacterPrefabAssemblyUtility.Assemble(
                bundle,
                ensureStandardComponents: true,
                createStandardHierarchy: true);

        log.Add(
            "[Character Studio Assembly 2]\n" +
            finalAssembly.BuildSummary());

        generatedPrefab =
            bundle.CharacterPrefab as Olaf ??
            AssetDatabase.LoadAssetAtPath<
                GameObject>(PrefabPath)?
                .GetComponentInChildren<
                    Olaf>(true);

        PositionGeneratedBindingPoints(
            generatedPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        bundle =
            AssetDatabase.LoadAssetAtPath<
                CharacterAuthoringBundle>(
                    BundlePath) ??
            bundle;

        generatedPrefab =
            ReloadPrefabCharacter<Olaf>(
                PrefabPath) ??
            generatedPrefab;

        previewTarget =
            ReloadPrefabCharacter<Character>(
                TargetPrefabPath) ??
            generatedPrefab;

        List<string> issues =
            ValidateBundle(
                bundle);

        WriteBuildReport(
            bundle,
            generatedPrefab,
            previewTarget,
            new[]
            {
                normal,
                duel,
                preparation,
                prestige
            },
            log,
            issues);

        AssetDatabase.ImportAsset(
            ReportPath,
            ImportAssetOptions.ForceUpdate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        bundle =
            AssetDatabase.LoadAssetAtPath<
                CharacterAuthoringBundle>(
                    BundlePath) ??
            bundle;

        generatedPrefab =
            ReloadPrefabCharacter<Olaf>(
                PrefabPath) ??
            generatedPrefab;

        previewTarget =
            ReloadPrefabCharacter<Character>(
                TargetPrefabPath) ??
            generatedPrefab;

        prestige =
            AssetDatabase.LoadAssetAtPath<
                SkillDefinition>(
                    GeneratedPrestigeSkillPath) ??
            prestige;

        string summary =
            $"Prefab: {PrefabPath}\n" +
            $"Bundle: {BundlePath}\n" +
            $"Skills: 4\n" +
            $"Timeline Segments: 20\n" +
            $"Validation Issues: {issues.Count}\n" +
            $"Report: {ReportPath}";

        if (issues.Count > 0)
        {
            summary +=
                "\n\n" +
                string.Join("\n", issues);
        }

        return OlafCompleteBuildResult.Ok(
            bundle,
            generatedPrefab,
            previewTarget,
            prestige,
            summary);
    }

    private static SkillDefinition CreateNormalSkill()
    {
        SkillDefinition skill =
            CloneSkill(
                OriginalNormalPath,
                SkillFolder +
                "/Olaf_BloodStrike_Complete.asset",
                "피의 일격",
                ActionType.NormalAttack);

        skill.Description =
            "도끼로 세 번 베어 출혈을 누적한다. 마지막 타격은 Slot 굴림을 사용하며, " +
            "공격 가중치 2로 보조 대상에게 65% 피해를 전파한다.";

        skill.BasePower = 5;
        skill.CanBreakPart = false;
        skill.GainPrestige = true;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = 0;

        skill.AttackWeight =
            new AttackWeightSettings
            {
                Weight = 2,
                SecondaryPartMode =
                    AttackWeightSecondaryPartMode
                        .MatchPrimaryPartType,
                SecondaryDamageMultiplier = 0.65f,
                AllowBrokenSecondaryParts = false
            };

        skill.Rolls =
            new List<SkillRollData>
            {
                DiceRoll(0, CombatRollType.Attack, 5, 8),
                CoinRoll(1, CombatRollType.Attack, 4, 10, true),
                SlotRoll(2, CombatRollType.Attack, 2, 4)
            };

        skill.MultiRollPenalty =
            new MultiRollPenaltyData
            {
                Enabled = false,
                PreviewText = "3타 기본 연계",
                Timing = MultiRollPenaltyTiming.OnActionEnd,
                TriggerAfterRollIndex = 2,
                Effects = new List<SkillEffectDefinition>()
            };

        skill.Keywords =
            Keywords(
                ("출혈", "일반공격 적중 후 출혈을 추가한다."),
                ("공격 가중치 2", "메인 타깃 외 다른 캐릭터 1명에게 65% 피해를 준다."),
                ("혼합 RNG", "Dice, Coin, Slot 굴림을 순서대로 사용한다."));

        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition CreateDuelSkill()
    {
        SkillDefinition skill =
            CloneSkill(
                OriginalDuelPath,
                SkillFolder +
                "/Olaf_BerserkerDuel_Complete.asset",
                "광전사의 결투",
                ActionType.Duel);

        skill.Description =
            "공격-수비-공격-친치로의 4교환 결투. 수비 굴림으로 한 차례를 버티고 " +
            "마지막 친치로 결과로 승부를 뒤집는다. 4굴림의 대가로 행동 종료 시 자해 위험이 있다.";

        skill.BasePower = 8;
        skill.CanBreakPart = false;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = 1;
        skill.OverrideCanClash = true;
        skill.CanClashValue = true;
        skill.AttackWeight = new AttackWeightSettings();

        skill.Rolls =
            new List<SkillRollData>
            {
                CoinRoll(0, CombatRollType.Attack, 7, 11, false),
                DiceRoll(1, CombatRollType.Defense, 6, 10, 1),
                DiceRoll(2, CombatRollType.Attack, 8, 13, 1),
                ChinchiroRoll(3, CombatRollType.Attack, -3, 4, 9, 18, 24)
            };

        SkillEffectDefinition risk =
            AssetDatabase.LoadAssetAtPath<
                SkillEffectDefinition>(
                    "Assets/2. Data/SkillEffects/Olaf/" +
                    "OlafBreakOwnPartEffect.asset");

        skill.MultiRollPenalty =
            new MultiRollPenaltyData
            {
                Enabled = risk != null,
                PreviewText =
                    "4번째 굴림까지 사용하면 행동 종료 후 자신의 부위를 약화시키고 광기를 얻는다.",
                Timing = MultiRollPenaltyTiming.OnActionEnd,
                TriggerAfterRollIndex = 3,
                Effects = risk == null
                    ? new List<SkillEffectDefinition>()
                    : new List<SkillEffectDefinition>
                    {
                        risk
                    }
            };

        skill.Keywords =
            Keywords(
                ("결투", "상대 스킬과 굴림을 맞부딪힌다."),
                ("수비 굴림", "두 번째 교환에서 승리하면 피해를 0으로 만든다."),
                ("친치로", "마지막 굴림은 3주사위 조합으로 위력이 결정된다."),
                ("고굴림 위험", "4굴림의 대가로 자해성 후속 효과가 발생할 수 있다."));

        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition CreatePreparationSkill()
    {
        SkillDefinition skill =
            CloneSkill(
                OriginalPreparationPath,
                SkillFolder +
                "/Olaf_MadnessFlurry_Complete.asset",
                "광기의 난도질",
                ActionType.Preparation);

        skill.Description =
            "강한 도사림. 자신의 부위를 약화시켜 광기를 확보한 뒤 네 번 난도질한다. " +
            "공격 가중치 3으로 다수의 적을 압박한다.";

        skill.BasePower = 3;
        skill.CanBreakPart = false;
        skill.PreparationTier = PreparationTier.Strong;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = 1;

        skill.AttackWeight =
            new AttackWeightSettings
            {
                Weight = 3,
                SecondaryPartMode =
                    AttackWeightSecondaryPartMode
                        .RandomValidTargetPoint,
                SecondaryDamageMultiplier = 0.5f,
                AllowBrokenSecondaryParts = true
            };

        skill.Rolls =
            new List<SkillRollData>
            {
                DiceRoll(0, CombatRollType.Attack, 3, 6),
                CoinRoll(1, CombatRollType.Attack, 2, 7, false),
                SlotRoll(2, CombatRollType.Attack, 1, 4),
                DiceRoll(3, CombatRollType.Attack, 5, 9, 2)
            };

        skill.MultiRollPenalty =
            new MultiRollPenaltyData
            {
                Enabled = false,
                PreviewText =
                    "자해와 광기 획득은 스킬의 기존 Effect 목록에서 처리한다.",
                Timing = MultiRollPenaltyTiming.OnActionStart,
                TriggerAfterRollIndex = 0,
                Effects = new List<SkillEffectDefinition>()
            };

        skill.Keywords =
            Keywords(
                ("강한 도사림", "기본 에너지 1을 소비하는 캐릭터 전용 도사림이다."),
                ("광기", "자신의 부위를 약화시키는 대신 광기 자원을 확보한다."),
                ("공격 가중치 3", "최대 세 캐릭터에게 피해를 분산한다."),
                ("4연타", "네 개의 독립 굴림과 네 개의 Hit Event를 사용한다."));

        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition CreatePrestigeSkill()
    {
        SkillDefinition skill =
            CloneSkill(
                OriginalPrestigePath,
                SkillFolder +
                "/Olaf_ImmortalFrenzy_Complete.asset",
                "불사의 광란",
                ActionType.Prestige);

        skill.Description =
            "기세가 가득 찼을 때 발동하는 올라프의 최종기. 네 종류의 RNG를 연속 사용하고, " +
            "부위 파괴와 사망 결과에 따라 별도 Timeline Segment로 분기한다.";

        skill.BasePower = 20;
        skill.CanBreakPart = true;
        skill.GainPrestige = false;
        skill.ResolvePrestigeInCombat = true;
        skill.OverrideCanClash = true;
        skill.CanClashValue = true;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = 0;
        skill.OverrideResourceRules = true;
        skill.RequireFullPrestige = true;
        skill.PrestigeCost = 100;
        skill.ConsumeAllPrestige = true;
        skill.OverridePrestigeUsePolicy = true;
        skill.PrestigeUsePolicy = PrestigeUsePolicy.OncePerTurn;

        skill.AttackWeight =
            new AttackWeightSettings
            {
                Weight = 3,
                SecondaryPartMode =
                    AttackWeightSecondaryPartMode
                        .CharacterLevelDirect,
                SecondaryDamageMultiplier = 0.75f,
                AllowBrokenSecondaryParts = true
            };

        skill.Rolls =
            new List<SkillRollData>
            {
                CoinRoll(0, CombatRollType.Attack, 16, 24, true),
                DiceRoll(1, CombatRollType.Attack, 18, 26, 2),
                SlotRoll(2, CombatRollType.Attack, 3, 6),
                ChinchiroRoll(3, CombatRollType.Attack, -4, 12, 22, 34, 42)
            };

        skill.MultiRollPenalty =
            new MultiRollPenaltyData
            {
                Enabled = false,
                PreviewText =
                    "위세 자원을 전부 소모하며 한 턴에 한 번만 사용할 수 있다.",
                Timing = MultiRollPenaltyTiming.OnActionEnd,
                TriggerAfterRollIndex = 3,
                Effects = new List<SkillEffectDefinition>()
            };

        skill.Keywords =
            Keywords(
                ("위세", "최대 위세를 요구하고 사용 후 전부 소비한다."),
                ("부위 파괴", "CanBreakPart가 활성화되어 결과에 따라 Part Break Segment가 재생된다."),
                ("결과 분기", "부위 파괴, 사망, 생존 복귀를 별도 Timeline으로 재생한다."),
                ("전 RNG", "Coin, Dice, Slot, Chinchiro를 한 스킬에서 모두 사용한다."));

        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static void BuildAllCutscenes(
        CharacterData generatedData,
        Character attacker,
        Character target,
        SkillDefinition normal,
        SkillDefinition duel,
        SkillDefinition preparation,
        SkillDefinition prestige,
        ICollection<string> log)
    {
        AnimationClip hit =
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(HitAnimationPath);

        AnimationClip broken =
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(BrokenAnimationPath);

        AnimationClip death =
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(DeathAnimationPath);

        AnimationClip idle =
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(IdleAnimationPath);

        BuildSkillCutscene(
            generatedData,
            attacker,
            target,
            normal,
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(NormalAnimationPath),
            hit,
            broken,
            death,
            idle,
            new OlafCutscenePlan(
                durationFrames: 96,
                hitFrames: new[] { 26 },
                slowMotionScale: 0.82f,
                useLegacyMovement: true,
                cameraVariant: 0),
            log);

        BuildSkillCutscene(
            generatedData,
            attacker,
            target,
            duel,
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(DuelAnimationPath),
            hit,
            broken,
            death,
            idle,
            new OlafCutscenePlan(
                durationFrames: 126,
                hitFrames: new[] { 27, 50 },
                slowMotionScale: 0.72f,
                useLegacyMovement: false,
                cameraVariant: 1),
            log);

        BuildSkillCutscene(
            generatedData,
            attacker,
            target,
            preparation,
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(PreparationAnimationPath),
            hit,
            broken,
            death,
            idle,
            new OlafCutscenePlan(
                durationFrames: 144,
                hitFrames: new[] { 40 },
                slowMotionScale: 0.78f,
                useLegacyMovement: true,
                cameraVariant: 2),
            log);

        BuildSkillCutscene(
            generatedData,
            attacker,
            target,
            prestige,
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(PrestigeAnimationPath),
            hit,
            broken,
            death,
            idle,
            new OlafCutscenePlan(
                durationFrames: 180,
                hitFrames: new[] { 50 },
                slowMotionScale: 0.58f,
                useLegacyMovement: false,
                cameraVariant: 3),
            log);
    }

    private static void BuildSkillCutscene(
        CharacterData generatedData,
        Character attacker,
        Character target,
        SkillDefinition skill,
        AnimationClip attackerAnimation,
        AnimationClip targetHitAnimation,
        AnimationClip brokenAnimation,
        AnimationClip deathAnimation,
        AnimationClip returnAnimation,
        OlafCutscenePlan plan,
        ICollection<string> log)
    {
        if (skill == null || attacker == null || target == null)
            return;

        SkillVisualDefinition sourceVisual =
            FindOriginalSkill(
                skill.ActionType)?
                .VisualDefinition;

        SkillVisualDefinition visual =
            CloneAsset(
                sourceVisual,
                SkillFolder +
                $"/{SanitizeFileName(skill.name)}_Visual.asset");

        if (visual == null)
        {
            visual =
                CreateAsset<
                    SkillVisualDefinition>(
                        SkillFolder +
                        $"/{SanitizeFileName(skill.name)}_Visual.asset");
        }

        visual.name =
            skill.name + "_Visual";

        visual.AllowAsProfileFallback = false;
        visual.CutsceneDefinition = null;
        visual.HasHitFrameDamage = true;
        visual.ApplyDamageIfNoHitFrame = false;
        visual.ExpectedHitFrameCount =
            Mathf.Max(1, plan.HitFrames.Length);
        visual.HitDamageWeights =
            Enumerable.Repeat(1, plan.HitFrames.Length)
                .ToList();
        visual.DistributeDamageByHitCount = true;
        visual.UseHitCameraShake = true;
        visual.HitShake ??= new BattleCameraShakeSettings();
        visual.HitShake.UseImpulse = true;
        visual.HitShake.ImpulseForce =
            skill.ActionType == ActionType.Prestige
                ? 2.2f
                : 1.2f;
        visual.ShowsActionAnnouncement = true;
        visual.ActionAnnouncementDuration = 0.6f;
        visual.BeforeActionDelay = 0.05f;
        visual.AfterActionDelay = 0.08f;
        visual.AfterReturnDelay = 0.12f;
        visual.MovesToTarget = plan.UseLegacyMovement;
        visual.ReturnPositionAfterAction = true;
        visual.FaceEachOther = true;
        visual.ReturnFacingAfterAction = true;
        visual.MoveSettings ??= new CharacterActionMoveSettings();
        visual.MoveSettings.UseMove = plan.UseLegacyMovement;
        visual.MoveSettings.ApproachDistance = 1.65f;
        visual.MoveSettings.OverrideMoveSpeed = true;
        visual.MoveSettings.MoveSpeed = 10f;
        ConfigureVisualVfx(
            visual,
            generatedData,
            plan.HitFrames.Length);

        skill.VisualDefinition = visual;
        EditorUtility.SetDirty(skill);
        EditorUtility.SetDirty(visual);
        AssetDatabase.SaveAssets();

        SkillCutsceneDefinition definition =
            SkillCutsceneAssetBuilder.EnsureForSkill(
                skill,
                attacker,
                target);

        if (definition == null)
            return;

        definition.AttackerAnimation =
            attackerAnimation;
        // 공격 대상은 런타임마다 달라지므로 특정 타깃 Hit Clip을
        // 공격자 스킬 Timeline에 고정하지 않는다.
        definition.TargetAnimation =
            null;
        definition.AuthoringFrameRate = 30d;
        definition.DefaultDurationFrames =
            plan.DurationFrames;
        definition.PrepareFacing = true;
        definition.PrepareMovement =
            plan.UseLegacyMovement;
        definition.RestoreMovement = true;
        definition.RestoreFacing = true;
        definition.RestoreOverview = true;
        definition.RestoreTimeScale = true;

        EnsureRageOrbitCamera(
            definition);

        TimelineAsset action =
            definition.ActionTimeline;

        TimelineAsset clash =
            SkillCutsceneAssetBuilder.EnsureSegment(
                definition,
                SkillCutsceneSegment.ClashAttack);

        TimelineAsset partBreak =
            SkillCutsceneAssetBuilder.EnsureSegment(
                definition,
                SkillCutsceneSegment.PartBreak);

        TimelineAsset kill =
            SkillCutsceneAssetBuilder.EnsureSegment(
                definition,
                SkillCutsceneSegment.Kill);

        TimelineAsset returned =
            SkillCutsceneAssetBuilder.EnsureSegment(
                definition,
                SkillCutsceneSegment.Return);

        SkillCutsceneAssetBuilder.EnsureTimelineStructure(
            definition,
            action,
            addDefaultClips: true);

        SkillCutsceneAssetBuilder.EnsureTimelineStructure(
            definition,
            clash,
            addDefaultClips: true);

        ConfigurePrimaryTimeline(
            definition,
            action,
            attackerAnimation,
            targetHitAnimation,
            plan,
            isClash: false);

        ConfigurePrimaryTimeline(
            definition,
            clash,
            attackerAnimation,
            targetHitAnimation,
            plan,
            isClash: true);

        ConfigureResultTimeline(
            definition,
            partBreak,
            returnAnimation,
            brokenAnimation,
            48,
            SkillCutsceneSegment.PartBreak,
            plan.CameraVariant);

        ConfigureResultTimeline(
            definition,
            kill,
            returnAnimation,
            deathAnimation,
            66,
            SkillCutsceneSegment.Kill,
            plan.CameraVariant);

        ConfigureResultTimeline(
            definition,
            returned,
            returnAnimation,
            null,
            42,
            SkillCutsceneSegment.Return,
            plan.CameraVariant);

        EditorUtility.SetDirty(definition);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();

        log?.Add(
            $"[Cutscene] {skill.SkillName}: " +
            "Action/Clash/PartBreak/Kill/Return 생성");
    }

    private static void ConfigurePrimaryTimeline(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        AnimationClip attackerAnimation,
        AnimationClip targetAnimation,
        OlafCutscenePlan plan,
        bool isClash)
    {
        if (timeline == null)
            return;

        timeline.durationMode =
            TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration =
            plan.DurationFrames /
            definition.FrameRate;

        EnsureAnimationTrackClip(
            timeline,
            SkillCutsceneAssetBuilder.AttackerTrackName,
            attackerAnimation,
            0d,
            timeline.fixedDuration);

        ClearAnimationTrackClips(
            timeline,
            SkillCutsceneAssetBuilder.TargetTrackName);

        ConfigureCameraSequence(
            definition,
            timeline,
            plan.DurationFrames,
            plan.CameraVariant,
            isClash);

        ConfigureEventSequence(
            definition,
            timeline,
            plan,
            isClash);

        CreateCameraMotionAnimation(
            definition,
            timeline,
            skillKey:
                timeline.name,
            cameraKey:
                "CM_RageOrbit",
            durationFrames:
                plan.DurationFrames,
            intensity:
                isClash ? 1.25f : 1f);

        EditorUtility.SetDirty(timeline);
    }

    private static void ConfigureResultTimeline(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        AnimationClip attackerAnimation,
        AnimationClip targetAnimation,
        int durationFrames,
        SkillCutsceneSegment segment,
        int cameraVariant)
    {
        if (timeline == null)
            return;

        timeline.durationMode =
            TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration =
            durationFrames /
            definition.FrameRate;

        EnsureAnimationTrackClip(
            timeline,
            SkillCutsceneAssetBuilder.AttackerTrackName,
            attackerAnimation,
            0d,
            timeline.fixedDuration);

        EnsureAnimationTrackClip(
            timeline,
            SkillCutsceneAssetBuilder.TargetTrackName,
            targetAnimation,
            0d,
            timeline.fixedDuration);

        TimelineClip camera =
            SkillCutsceneAssetBuilder.AddCameraClip(
                definition,
                timeline,
                0d,
                segment == SkillCutsceneSegment.Kill
                    ? "CM_Impact"
                    : segment == SkillCutsceneSegment.Return
                        ? "CM_Overview"
                        : "CM_RageOrbit");

        if (camera != null)
        {
            camera.duration =
                timeline.fixedDuration;

            SkillCameraTimelineClip asset =
                camera.asset as
                    SkillCameraTimelineClip;

            if (asset != null)
            {
                asset.PositionBinding =
                    segment switch
                    {
                        SkillCutsceneSegment.PartBreak =>
                            SkillCameraPositionBinding.TargetAnchor,
                        SkillCutsceneSegment.Kill =>
                            SkillCameraPositionBinding.TargetRoot,
                        _ =>
                            SkillCameraPositionBinding.AttackerRoot
                    };

                asset.PositionAnchorKey =
                    segment == SkillCutsceneSegment.PartBreak
                        ? "Chest"
                        : "Center";

                asset.AimBinding =
                    segment switch
                    {
                        SkillCutsceneSegment.Return =>
                            SkillCameraAimBinding.CombatFrame,
                        SkillCutsceneSegment.Kill =>
                            SkillCameraAimBinding.TargetVisualRoot,
                        _ =>
                            SkillCameraAimBinding.TargetAnchor
                    };

                asset.AimAnchorKey = "Chest";
                asset.CapturedPosition =
                    segment switch
                    {
                        SkillCutsceneSegment.PartBreak =>
                            new Vector3(1.25f, 1.4f, -2.4f),
                        SkillCutsceneSegment.Kill =>
                            new Vector3(-0.8f, 1.6f, -2.7f),
                        _ =>
                            new Vector3(0f, 2.3f, -6.2f)
                    };

                asset.PositionDamping =
                    segment == SkillCutsceneSegment.Return
                        ? 0.16f
                        : 0.05f;

                asset.RotationDamping = 0.05f;
                EditorUtility.SetDirty(asset);
            }
        }

        if (segment == SkillCutsceneSegment.PartBreak)
        {
            AddConfiguredEvent(
                definition,
                timeline,
                8,
                SkillCutsceneEventType.CameraShake);

            AddConfiguredEvent(
                definition,
                timeline,
                10,
                SkillCutsceneEventType.Vfx,
                vfxTiming:
                    BattleVfxTiming.OnPartBroken);

            AddConfiguredEvent(
                definition,
                timeline,
                durationFrames - 5,
                SkillCutsceneEventType.Custom,
                customKey:
                    $"OLAF_PART_BREAK_V{cameraVariant}");
        }
        else if (segment == SkillCutsceneSegment.Kill)
        {
            AddConfiguredEvent(
                definition,
                timeline,
                6,
                SkillCutsceneEventType.SetTimeScale,
                timeScale: 0.45f);

            AddConfiguredEvent(
                definition,
                timeline,
                12,
                SkillCutsceneEventType.Vfx,
                vfxTiming:
                    BattleVfxTiming.OnKill);

            AddConfiguredEvent(
                definition,
                timeline,
                14,
                SkillCutsceneEventType.CameraShake);

            AddConfiguredEvent(
                definition,
                timeline,
                durationFrames - 10,
                SkillCutsceneEventType.RestoreTimeScale);

            AddConfiguredEvent(
                definition,
                timeline,
                durationFrames - 5,
                SkillCutsceneEventType.Custom,
                customKey:
                    "OLAF_KILL_FINISH");
        }
        else
        {
            AddConfiguredEvent(
                definition,
                timeline,
                4,
                SkillCutsceneEventType.RestoreTimeScale);

            AddConfiguredEvent(
                definition,
                timeline,
                durationFrames - 4,
                SkillCutsceneEventType.ReturnOverview);

            AddConfiguredEvent(
                definition,
                timeline,
                durationFrames - 2,
                SkillCutsceneEventType.Custom,
                customKey:
                    "OLAF_RETURN_COMPLETE");
        }

        EditorUtility.SetDirty(timeline);
    }

    private static void ConfigureCameraSequence(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        int durationFrames,
        int variant,
        bool isClash)
    {
        List<TimelineClip> cameras =
            timeline.GetOutputTracks()
                .OfType<
                    SkillCameraTimelineTrack>()
                .SelectMany(
                    track =>
                        track.GetClips())
                .OrderBy(
                    clip => clip.start)
                .ToList();

        while (cameras.Count < 3)
        {
            cameras.Add(
                SkillCutsceneAssetBuilder.AddCameraClip(
                    definition,
                    timeline,
                    0d,
                    cameras.Count == 0
                        ? "CM_Overview"
                        : cameras.Count == 1
                            ? "CM_Follow"
                            : "CM_Impact"));
        }

        int wideEnd =
            Mathf.RoundToInt(durationFrames * 0.34f);
        int followStart =
            Mathf.RoundToInt(durationFrames * 0.26f);
        int followEnd =
            Mathf.RoundToInt(durationFrames * 0.70f);
        int orbitStart =
            Mathf.RoundToInt(durationFrames * 0.60f);
        int orbitEnd =
            Mathf.RoundToInt(durationFrames * 0.88f);
        int impactStart =
            Mathf.RoundToInt(durationFrames * 0.80f);

        ConfigureCameraClip(
            cameras[0],
            definition,
            0,
            wideEnd,
            "CM_Overview",
            WideBinding(variant),
            WideAim(variant),
            variant % 2 == 0
                ? new Vector3(0f, 2.5f, -7f)
                : new Vector3(-1.2f, 2.3f, -6.4f),
            "Center",
            "Chest",
            0.08f,
            0.06f);

        ConfigureCameraClip(
            cameras[1],
            definition,
            followStart,
            followEnd - followStart,
            "CM_Follow",
            FollowBinding(variant),
            FollowAim(variant),
            isClash
                ? new Vector3(2.5f, 1.65f, -3.8f)
                : new Vector3(2.1f, 1.8f, -4.2f),
            variant == 1 ? "WeaponMain" : "Center",
            "Chest",
            0.12f,
            0.08f);

        TimelineClip orbit =
            SkillCutsceneAssetBuilder.AddCameraClip(
                definition,
                timeline,
                orbitStart /
                definition.FrameRate,
                "CM_RageOrbit");

        ConfigureCameraClip(
            orbit,
            definition,
            orbitStart,
            orbitEnd - orbitStart,
            "CM_RageOrbit",
            OrbitBinding(variant),
            OrbitAim(variant),
            new Vector3(-2.1f, 2.0f, -3.1f),
            variant == 3 ? "WeaponMain" : "Center",
            variant == 2 ? "Head" : "Chest",
            0.04f,
            0.04f);

        ConfigureCameraClip(
            cameras[2],
            definition,
            impactStart,
            durationFrames - impactStart,
            "CM_Impact",
            ImpactBinding(variant),
            ImpactAim(variant),
            isClash
                ? new Vector3(0.55f, 1.45f, -1.95f)
                : new Vector3(0.85f, 1.55f, -2.35f),
            "Chest",
            variant == 3 ? "Head" : "Chest",
            0.02f,
            0.02f);
    }

    private static void ConfigureCameraClip(
        TimelineClip timelineClip,
        SkillCutsceneDefinition definition,
        int startFrame,
        int durationFrames,
        string cameraKey,
        SkillCameraPositionBinding positionBinding,
        SkillCameraAimBinding aimBinding,
        Vector3 position,
        string positionAnchor,
        string aimAnchor,
        float positionDamping,
        float rotationDamping)
    {
        if (timelineClip == null)
            return;

        timelineClip.start =
            Mathf.Max(0, startFrame) /
            definition.FrameRate;

        timelineClip.duration =
            Mathf.Max(1, durationFrames) /
            definition.FrameRate;

        timelineClip.displayName =
            cameraKey;

        // mixInDuration은 Timeline이 Ease 또는 인접 Clip과의 Overlap을 바탕으로
        // 계산하는 읽기 전용 값이다. 겹치지 않는 Clip의 진입 보간만 설정한다.
        timelineClip.easeInDuration =
            Math.Min(
                6d /
                definition.FrameRate,
                timelineClip.duration * 0.4d);

        timelineClip.ConformEaseValues();

        SkillCameraTimelineClip asset =
            timelineClip.asset as
                SkillCameraTimelineClip;

        if (asset == null)
            return;

        asset.CameraKey = cameraKey;
        asset.PositionBinding = positionBinding;
        asset.PositionAnchorKey = positionAnchor;
        asset.AimBinding = aimBinding;
        asset.AimAnchorKey = aimAnchor;
        asset.CapturedPosition = position;
        asset.AimOffset = new Vector3(0f, 0.8f, 0f);
        asset.CapturedEuler = Vector3.zero;
        asset.PositionDamping = positionDamping;
        asset.RotationDamping = rotationDamping;
        asset.UseTimelineMixAsBlend = true;
        asset.FallbackBlendFrames = 6;
        asset.SnapPoseOnEnter = true;
        EditorUtility.SetDirty(asset);
    }

    private static void ConfigureEventSequence(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        OlafCutscenePlan plan,
        bool isClash)
    {
        List<SkillCutsceneEventClip> existingHits =
            timeline.GetOutputTracks()
                .OfType<
                    SkillCutsceneEventTrack>()
                .SelectMany(
                    track =>
                        track.GetClips())
                .Where(
                    clip =>
                        clip.asset is
                            SkillCutsceneEventClip eventClip &&
                        eventClip.EventType ==
                            SkillCutsceneEventType.Hit)
                .Select(
                    clip =>
                        clip.asset as
                            SkillCutsceneEventClip)
                .Where(asset => asset != null)
                .ToList();

        AddConfiguredEvent(
            definition,
            timeline,
            6,
            SkillCutsceneEventType.SetTimeScale,
            timeScale: plan.SlowMotionScale);

        AddConfiguredEvent(
            definition,
            timeline,
            12,
            SkillCutsceneEventType.Vfx,
            vfxTiming:
                BattleVfxTiming.BeforeAttackAnimation);

        for (int index = 0;
             index < plan.HitFrames.Length;
             index++)
        {
            int frame =
                plan.HitFrames[index];

            if (index == 0 &&
                existingHits.Count > 0)
            {
                TimelineClip firstHitClip =
                    timeline.GetOutputTracks()
                        .OfType<
                            SkillCutsceneEventTrack>()
                        .SelectMany(
                            track =>
                                track.GetClips())
                        .FirstOrDefault(
                            clip =>
                                clip.asset ==
                                existingHits[0]);

                if (firstHitClip != null)
                {
                    firstHitClip.start =
                        frame /
                        definition.FrameRate;

                    firstHitClip.duration =
                        1d /
                        definition.FrameRate;

                    firstHitClip.displayName =
                        "Hit 1";

                    existingHits[0].HitIndex = 0;
                    EditorUtility.SetDirty(existingHits[0]);
                }
            }
            else
            {
                AddConfiguredEvent(
                    definition,
                    timeline,
                    frame,
                    SkillCutsceneEventType.Hit,
                    hitIndex: index);
            }

            // Hit Event 하나가 피격 모션, OnHit VFX, CameraShake,
            // CameraImpactPulse를 원자적으로 실행한다. 별도 중복 Event를 만들지 않는다.
        }

        AddConfiguredEvent(
            definition,
            timeline,
            plan.DurationFrames - 12,
            SkillCutsceneEventType.RestoreTimeScale);

        AddConfiguredEvent(
            definition,
            timeline,
            plan.DurationFrames - 8,
            SkillCutsceneEventType.Custom,
            customKey:
                isClash
                    ? "OLAF_CLASH_ATTACK_END"
                    : "OLAF_ACTION_END");

        AddConfiguredEvent(
            definition,
            timeline,
            plan.DurationFrames - 3,
            SkillCutsceneEventType.ReturnOverview);
    }

    private static TimelineClip AddConfiguredEvent(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        int frame,
        SkillCutsceneEventType type,
        int hitIndex = -1,
        BattleVfxTiming vfxTiming =
            BattleVfxTiming.OnHitFrame,
        float timeScale = 1f,
        string customKey = null)
    {
        TimelineClip clip =
            SkillCutsceneAssetBuilder.AddEventClip(
                definition,
                timeline,
                Mathf.Max(0, frame) /
                definition.FrameRate,
                type);

        SkillCutsceneEventClip asset =
            clip?.asset as
                SkillCutsceneEventClip;

        if (asset == null)
            return clip;

        asset.HitIndex = hitIndex;
        asset.VfxTiming = vfxTiming;
        asset.TimeScale = Mathf.Max(0.01f, timeScale);
        asset.CustomEventKey = customKey ?? string.Empty;

        clip.displayName =
            type switch
            {
                SkillCutsceneEventType.Hit =>
                    $"Hit {hitIndex + 1}",
                SkillCutsceneEventType.Vfx =>
                    $"VFX {vfxTiming}" +
                    (hitIndex >= 0
                        ? $" #{hitIndex + 1}"
                        : string.Empty),
                SkillCutsceneEventType.Custom =>
                    "Custom " + asset.CustomEventKey,
                _ =>
                    type.ToString()
            };

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(timeline);
        return clip;
    }

    private static void CreateCameraMotionAnimation(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        string skillKey,
        string cameraKey,
        int durationFrames,
        float intensity)
    {
        AnimationTrack track =
            SkillCutsceneAssetBuilder
                .EnsureCameraMotionTrack(
                    timeline,
                    cameraKey);

        if (track == null ||
            track.GetClips().Any())
        {
            return;
        }

        string clipPath =
            AssetDatabase.GenerateUniqueAssetPath(
                AnimationFolder +
                "/" +
                SanitizeFileName(skillKey) +
                "_" +
                cameraKey +
                "_Motion.anim");

        AnimationClip motion =
            new AnimationClip
            {
                name =
                    SanitizeFileName(skillKey) +
                    "_" + cameraKey +
                    "_Motion",
                frameRate =
                    (float)definition.FrameRate
            };

        float duration =
            Mathf.Max(1, durationFrames) /
            (float)definition.FrameRate;

        motion.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.x",
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(duration * 0.5f, 0.45f * intensity),
                new Keyframe(duration, 0f)));

        motion.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.y",
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(duration * 0.5f, 0.18f * intensity),
                new Keyframe(duration, 0f)));

        motion.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.z",
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(duration * 0.5f, 0.35f * intensity),
                new Keyframe(duration, 0f)));

        AssetDatabase.CreateAsset(
            motion,
            clipPath);

        TimelineClip clip =
            track.CreateClip(motion);

        clip.start = 0d;
        clip.duration = duration;
        clip.displayName =
            cameraKey + " Local Motion";

        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
    }


    private static void ClearAnimationTrackClips(
        TimelineAsset timeline,
        string trackName)
    {
        if (timeline == null ||
            string.IsNullOrWhiteSpace(
                trackName))
        {
            return;
        }

        AnimationTrack track =
            timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            trackName,
                            StringComparison.OrdinalIgnoreCase));

        if (track == null)
            return;

        foreach (TimelineClip clip in
                 track.GetClips().ToArray())
        {
            timeline.DeleteClip(
                clip);
        }

        EditorUtility.SetDirty(
            track);
        EditorUtility.SetDirty(
            timeline);
    }

    private static void EnsureAnimationTrackClip(
        TimelineAsset timeline,
        string trackName,
        AnimationClip animation,
        double start,
        double maximumDuration)
    {
        if (timeline == null || animation == null)
            return;

        AnimationTrack track =
            timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            trackName,
                            StringComparison.OrdinalIgnoreCase)) ??
            timeline.CreateTrack<AnimationTrack>(
                null,
                trackName);

        if (track.GetClips().Any())
            return;

        TimelineClip clip =
            track.CreateClip(animation);

        clip.start = Mathf.Max(0f, (float)start);
        clip.duration =
            Math.Max(
                1d / 30d,
                Math.Min(
                    animation.length,
                    maximumDuration));
        clip.displayName = animation.name;
        EditorUtility.SetDirty(track);
    }

    private static void EnsureRageOrbitCamera(
        SkillCutsceneDefinition definition)
    {
        if (definition?.CameraRigPrefab == null)
            return;

        bool exists =
            definition.CameraRigPrefab
                .GetComponentsInChildren<
                    CinemachineCamera>(true)
                .Any(
                    camera =>
                        camera.name ==
                        "CM_RageOrbit");

        if (!exists)
        {
            SkillCutsceneAssetBuilder.AddCameraToRig(
                definition,
                "CM_RageOrbit");
        }
    }

    private static void ConfigureVisualVfx(
        SkillVisualDefinition visual,
        CharacterData generatedData,
        int hitCount)
    {
        if (visual == null)
            return;

        BattleVfxDefinition blood =
            AssetDatabase.LoadAssetAtPath<
                BattleVfxDefinition>(
                    BloodVfxPath);

        visual.VfxCues ??=
            new List<BattleVfxCue>();

        if (blood == null)
            return;

        visual.VfxCues.Clear();

        visual.VfxCues.Add(
            new BattleVfxCue
            {
                CueKey = "OLAF_START",
                Timing = BattleVfxTiming.BeforeAttackAnimation,
                Vfx = blood,
                AnchorType = BattleVfxAnchorType.AttackerLookAt,
                RepeatMode = BattleVfxCueRepeatMode.OncePerRequest,
                RequiredAttackerData = generatedData,
                Delay = 0f
            });

        for (int index = 0;
             index < Mathf.Max(1, hitCount);
             index++)
        {
            visual.VfxCues.Add(
                new BattleVfxCue
                {
                    CueKey = "OLAF_HIT",
                    Timing = BattleVfxTiming.OnHitFrame,
                    Vfx = blood,
                    AnchorType = BattleVfxAnchorType.TargetBodyPart,
                    RepeatMode = BattleVfxCueRepeatMode.OncePerHitIndex,
                    UseHitIndexFilter = true,
                    HitIndex = index,
                    RequiredAttackerData = generatedData,
                    RequirePositiveResolvedDamage = true,
                    Delay = 0f
                });
        }

        visual.VfxCues.Add(
            new BattleVfxCue
            {
                CueKey = "OLAF_KILL",
                Timing = BattleVfxTiming.OnKill,
                Vfx = blood,
                AnchorType = BattleVfxAnchorType.TargetLookAt,
                RepeatMode = BattleVfxCueRepeatMode.OncePerRequest,
                RequiredAttackerData = generatedData,
                RequirePositiveResolvedDamage = true,
                Delay = 0f
            });
    }

    private static CharacterData CreateCharacterData(
        CharacterCombatLoadout loadout)
    {
        CharacterData source =
            AssetDatabase.LoadAssetAtPath<
                CharacterData>(OriginalDataPath);

        CharacterData data =
            CloneAsset(
                source,
                DataFolder +
                "/Olaf_Complete_CharacterData.asset") ??
            CreateAsset<CharacterData>(
                DataFolder +
                "/Olaf_Complete_CharacterData.asset");

        data.name = "Olaf_Complete_CharacterData";
        data.CharacterName = "올라프";
        data.CombatantTier = CombatantTier.Player;
        data.RoleName = "광전사 / 출혈·광기 압박";
        data.UiSummary =
            "부위를 희생해 광기를 얻고 다단 공격과 출혈로 전장을 압박하는 광전사. " +
            "부위가 하나만 남으면 불사의 분노가 발동한다.";
        data.TargetMode = CharacterTargetMode.BodyParts;
        data.SingleHpMax = 1;
        data.maxPrestige = 100;
        data.maxEnergy = 3;
        data.damageMultiplier = 1f;
        data.defensePenetration = 0.08f;
        data.minSpeed = 7;
        data.maxSpeed = 15;
        data.CombatLoadout = loadout;
        data.BossPhases ??= new List<BossPhaseData>();
        data.InitialResources ??=
            new List<CombatResourceDefinition>();

        data.ActionSlots =
            new List<CharacterSlotConfig>
            {
                Slot(
                    "OLAF_HEAD",
                    "머리 행동",
                    PartType.HEAD,
                    8,
                    15,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Preparation,
                    ActionType.Prestige),

                Slot(
                    "OLAF_LEFT_HAND",
                    "왼손 행동",
                    PartType.LEFT_HAND,
                    7,
                    14,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige),

                Slot(
                    "OLAF_RIGHT_HAND",
                    "오른손 행동",
                    PartType.RIGHT_HAND,
                    7,
                    14,
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige),

                Slot(
                    "OLAF_LEGS",
                    "다리 행동",
                    PartType.LEGS,
                    9,
                    15,
                    ActionType.Preparation,
                    ActionType.Prestige)
            };

        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ConfigureLoadout(
        CharacterCombatLoadout loadout,
        SkillCatalog catalog,
        SkillDefinition normal,
        SkillDefinition duel,
        SkillDefinition preparation,
        SkillDefinition prestige)
    {
        loadout.Catalog = catalog;
        loadout.IncludeCatalogCandidates = true;
        loadout.NormalSkills =
            ListOf(normal);
        loadout.DuelSkills =
            ListOf(duel);
        loadout.PreparationSkills =
            ListOf(preparation);
        loadout.PrestigeSkills =
            ListOf(prestige);
        loadout.NormalSkillPool =
            ListOf(normal);
        loadout.DuelSkillPool =
            ListOf(duel);
        loadout.CommonPreparationPool =
            new List<SkillDefinition>();
        loadout.CharacterPreparationPool =
            ListOf(preparation);
        loadout.PrestigeSkillPool =
            ListOf(prestige);
        EditorUtility.SetDirty(loadout);
    }

    private static OlafBloodyAxeItem CreateBloodyAxe()
    {
        OlafBloodyAxeItem item =
            CreateAsset<OlafBloodyAxeItem>(
                BuildFolder +
                "/Olaf_BloodyAxe_Complete.asset");

        SetSerializedString(item, "itemName", "피 묻은 도끼");
        SetSerializedString(
            item,
            "description",
            "일반공격으로 실제 피해를 주면 행동당 한 번 출혈 1을 추가한다.");
        SetSerializedInt(item, "bleedingAmount", 1);
        SetSerializedBool(item, "requireNormalAttack", true);
        SetSerializedBool(item, "requirePositiveDamage", true);
        SetSerializedBool(item, "ignoreStatusDamage", true);
        SetSerializedBool(item, "triggerOncePerAction", true);
        SetSerializedBool(item, "applyToCharacterWhenPartUnavailable", true);
        return item;
    }

    private static BattleInstinctAugment CreateBattleInstinct()
    {
        BattleInstinctAugment augment =
            CreateAsset<BattleInstinctAugment>(
                BuildFolder +
                "/Olaf_BattleInstinct.asset");

        ConfigureAugmentIdentity(
            augment,
            "전투 본능",
            "공격력과 위세 획득량을 높이는 기본 공격형 증강.");
        SetSerializedInt(augment, "bonusAttack", 2);
        SetSerializedFloat(augment, "prestigeGainBonusRate", 0.15f);
        return augment;
    }

    private static ToughBodyAugment CreateToughBody()
    {
        ToughBodyAugment augment =
            CreateAsset<ToughBodyAugment>(
                BuildFolder +
                "/Olaf_ToughBody.asset");

        ConfigureAugmentIdentity(
            augment,
            "강인한 육체",
            "모든 부위 최대 체력과 방어력을 높여 자해형 운영을 보조한다.");
        SetSerializedFloat(augment, "bodyPartHpBonusRate", 0.15f);
        SetSerializedInt(augment, "bonusDefense", 2);
        return augment;
    }

    private static EnergyCapacityAugment CreateEnergyCapacity()
    {
        EnergyCapacityAugment augment =
            CreateAsset<EnergyCapacityAugment>(
                BuildFolder +
                "/Olaf_EnergyCapacity.asset");

        ConfigureAugmentIdentity(
            augment,
            "광란의 여유",
            "최대 에너지를 1 증가시켜 도사림과 결투를 연계한다.");
        SetSerializedInt(augment, "bonusMaxEnergy", 1);
        return augment;
    }

    private static void ConfigureAugmentIdentity(
        CharacterAugment augment,
        string name,
        string description)
    {
        SetSerializedString(augment, "augmentName", name);
        SetSerializedString(augment, "description", description);
    }

    private static void ConfigureBundlePresentation(
        CharacterAuthoringBundle bundle)
    {
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<
                RuntimeAnimatorController>(
                    AnimatorControllerPath);

        Avatar avatar =
            AssetDatabase.LoadAllAssetsAtPath(
                    ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();
        SetObject(serialized, "animatorController", controller);
        SetObject(serialized, "avatar", avatar);
        SetBool(serialized, "overrideAnimatorController", controller != null);
        SetBool(serialized, "overrideAvatar", avatar != null);
        SetString(
            serialized,
            "authoringNotes",
            "v5.1.5 완성형 올라프 샘플. Character Studio의 Modern Loadout, Legacy Adapter, " +
            "Item/Augment, Animator/Avatar, 표준 Component/Hierarchy와 Skill Cutscene Studio의 " +
            "Action/Clash/PartBreak/Kill/Return, Camera/Event/Motion Track을 모두 예시로 구성한다.");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bundle);
    }

    private static Olaf CreateCharacterPrefab(
        Olaf source,
        string path,
        string name)
    {
        if (source == null)
            return null;

        GameObject clone =
            Object.Instantiate(
                source.gameObject);

        clone.name = name;
        clone.SetActive(true);

        bool savedSuccessfully = false;

        try
        {
            savedSuccessfully =
                PrefabUtility.SaveAsPrefabAsset(
                    clone,
                    path) != null;
        }
        finally
        {
            if (clone != null)
            {
                Object.DestroyImmediate(
                    clone);
            }
        }

        if (!savedSuccessfully)
            return null;

        // SaveAsPrefabAsset의 반환값이 임시 Scene 인스턴스와 연결되는
        // Editor 상황을 피하기 위해 파괴 이후 AssetDatabase에서 다시 읽는다.
        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(
                path)?
            .GetComponentInChildren<Olaf>(
                true);
    }

    private static Character CreatePreviewTargetPrefab(
        Character fallback)
    {
        // Preview Target은 임시 EliteEnemy 인스턴스를 만들고 DestroyImmediate하지 않는다.
        // 일부 Unity 6 Editor 상황에서는 SaveAsPrefabAsset의 반환 Component가
        // 방금 파괴한 임시 인스턴스를 계속 가리켜 MissingReferenceException이 발생한다.
        string sourcePrefabPath =
            FindPrefabCharacterPath<
                EliteEnemy>();

        if (!string.IsNullOrWhiteSpace(
                sourcePrefabPath))
        {
            AssetDatabase.DeleteAsset(
                TargetPrefabPath);

            if (AssetDatabase.CopyAsset(
                    sourcePrefabPath,
                    TargetPrefabPath))
            {
                Character copied =
                    ReloadPrefabCharacter<
                        Character>(
                            TargetPrefabPath);

                if (copied != null)
                    return copied;
            }
        }

        EliteEnemy sceneSource =
            Object.FindFirstObjectByType<
                EliteEnemy>(
                    FindObjectsInactive.Include);

        if (sceneSource == null)
            return fallback;

        // Scene 오브젝트 자체를 Prefab으로 저장할 뿐 원본이나 복제본을 파괴하지 않는다.
        GameObject saved =
            PrefabUtility.SaveAsPrefabAsset(
                sceneSource.gameObject,
                TargetPrefabPath);

        if (saved == null)
            return fallback;

        return ReloadPrefabCharacter<
                   Character>(
                       TargetPrefabPath) ??
               fallback;
    }

    private static T ReloadPrefabCharacter<T>(
        string path)
        where T : Character
    {
        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        return AssetDatabase
            .LoadAssetAtPath<GameObject>(
                path)?
            .GetComponentInChildren<T>(
                true);
    }

    private static Olaf FindOlafTemplate()
    {
        Olaf prefab =
            FindPrefabCharacter<Olaf>();

        if (prefab != null)
            return prefab;

        return Object.FindFirstObjectByType<
            Olaf>(
                FindObjectsInactive.Include);
    }

    private static T FindPrefabCharacter<T>()
        where T : Character
    {
        string path =
            FindPrefabCharacterPath<T>();

        return string.IsNullOrWhiteSpace(
                path)
            ? null
            : AssetDatabase
                .LoadAssetAtPath<GameObject>(
                    path)?
                .GetComponentInChildren<T>(
                    true);
    }

    private static string FindPrefabCharacterPath<T>()
        where T : Character
    {
        foreach (string guid in
                 AssetDatabase.FindAssets(
                     "t:Prefab"))
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            // 방금 삭제했거나 다시 만드는 Generated Complete의 이전 결과는
            // 원본 템플릿 후보로 사용하지 않는다.
            if (path.StartsWith(
                    GeneratedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        path);

            T character =
                prefab?
                    .GetComponentInChildren<T>(
                        true);

            if (character != null)
                return path;
        }

        return string.Empty;
    }

    private static SkillDefinition CloneSkill(
        string sourcePath,
        string targetPath,
        string displayName,
        ActionType actionType)
    {
        SkillDefinition source =
            AssetDatabase.LoadAssetAtPath<
                SkillDefinition>(sourcePath);

        SkillDefinition skill =
            CloneAsset(source, targetPath) ??
            CreateAsset<SkillDefinition>(
                targetPath);

        skill.name =
            Path.GetFileNameWithoutExtension(
                targetPath);
        skill.SkillName = displayName;
        skill.ActionType = actionType;
        skill.VisualDefinition = null;
        skill.AttackWeight ??=
            new AttackWeightSettings();
        skill.MultiRollPenalty ??=
            new MultiRollPenaltyData();
        skill.Effects ??=
            new List<SkillEffectDefinition>();
        skill.Keywords ??=
            new List<SkillKeywordEntry>();

        SerializedObject serialized =
            new SerializedObject(skill);
        serialized.Update();
        SetString(
            serialized,
            "skillId",
            Guid.NewGuid().ToString("N"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition FindOriginalSkill(
        ActionType type)
    {
        string path =
            type switch
            {
                ActionType.NormalAttack =>
                    OriginalNormalPath,
                ActionType.Duel =>
                    OriginalDuelPath,
                ActionType.Preparation =>
                    OriginalPreparationPath,
                ActionType.Prestige =>
                    OriginalPrestigePath,
                _ => null
            };

        return string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<
                SkillDefinition>(path);
    }

    private static T CloneAsset<T>(
        T source,
        string targetPath)
        where T : ScriptableObject
    {
        if (source == null)
            return null;

        T clone =
            ScriptableObject.CreateInstance<T>();

        EditorUtility.CopySerialized(
            source,
            clone);

        clone.name =
            Path.GetFileNameWithoutExtension(
                targetPath);

        AssetDatabase.CreateAsset(
            clone,
            targetPath);
        EditorUtility.SetDirty(clone);
        return clone;
    }

    private static T CreateAsset<T>(
        string path)
        where T : ScriptableObject
    {
        T asset =
            ScriptableObject.CreateInstance<T>();

        asset.name =
            Path.GetFileNameWithoutExtension(
                path);

        AssetDatabase.CreateAsset(
            asset,
            path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void PositionGeneratedBindingPoints(
        Character prefab)
    {
        if (prefab == null)
            return;

        string path =
            AssetDatabase.GetAssetPath(prefab);

        if (string.IsNullOrWhiteSpace(path))
            return;

        GameObject root =
            PrefabUtility.LoadPrefabContents(
                path);

        try
        {
            Transform visualRoot =
                FindDescendant(
                    root.transform,
                    "Olaf_View") ??
                FindDescendant(
                    root.transform,
                    "VisualRoot") ??
                root.transform;

            Transform model =
                visualRoot.GetComponentInChildren<
                    Animator>(true)?
                    .transform;

            Dictionary<string, Transform> bones =
                BuildBoneLookup(model);

            BindPoint(
                visualRoot,
                "HEAD_Anchor",
                FindBone(bones, "Head"),
                new Vector3(0f, 0.08f, 0f));

            BindPoint(
                visualRoot,
                "LEFT_HAND_Anchor",
                FindBone(bones, "LeftHand"),
                Vector3.zero);

            BindPoint(
                visualRoot,
                "RIGHT_HAND_Anchor",
                FindBone(bones, "RightHand"),
                Vector3.zero);

            BindPoint(
                visualRoot,
                "LEGS_Anchor",
                FindBone(bones, "Hips"),
                new Vector3(0f, -0.75f, 0f));

            BindPoint(
                visualRoot,
                "Head",
                FindBone(bones, "Head"),
                new Vector3(0f, 0.08f, 0f));

            BindPoint(
                visualRoot,
                "Chest",
                FindBone(bones, "Spine2") ??
                FindBone(bones, "Spine"),
                new Vector3(0f, 0.12f, 0f));

            BindPoint(
                visualRoot,
                "LeftHand",
                FindBone(bones, "LeftHand"),
                Vector3.zero);

            BindPoint(
                visualRoot,
                "RightHand",
                FindBone(bones, "RightHand"),
                Vector3.zero);

            BindPoint(
                visualRoot,
                "WeaponMain",
                FindBone(bones, "RightHand"),
                new Vector3(0f, 0f, 0.35f));

            BindPoint(
                visualRoot,
                "WeaponSub",
                FindBone(bones, "LeftHand"),
                new Vector3(0f, 0f, 0.25f));

            BindPoint(
                visualRoot,
                "Feet",
                FindBone(bones, "Hips"),
                new Vector3(0f, -0.9f, 0f));

            SetLocalPoint(
                visualRoot,
                "Root",
                new Vector3(0f, 0f, 0f));
            SetLocalPoint(
                visualRoot,
                "Center",
                new Vector3(0f, 1.0f, 0f));
            SetLocalPoint(
                visualRoot,
                "LookAtPoint",
                new Vector3(0f, 1.35f, 0f));
            SetLocalPoint(
                visualRoot,
                "CloseCameraPoint",
                new Vector3(1.5f, 1.6f, -2.8f));
            SetLocalPoint(
                visualRoot,
                "OverShoulderCameraPoint",
                new Vector3(1.2f, 1.65f, -2.2f));
            SetLocalPoint(
                visualRoot,
                "HitImpactCameraPoint",
                new Vector3(0.7f, 1.45f, -1.6f));
            SetLocalPoint(
                visualRoot,
                "SideCameraPoint",
                new Vector3(3.2f, 1.5f, 0f));
            SetLocalPoint(
                visualRoot,
                "ClashRollCameraPoint",
                new Vector3(0f, 2.0f, -4.2f));
            SetLocalPoint(
                visualRoot,
                "DetailCameraPoint",
                new Vector3(1.3f, 1.55f, -2.4f));

            PrefabUtility.SaveAsPrefabAsset(
                root,
                path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                root);
        }

        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceUpdate);
    }

    private static Dictionary<string, Transform> BuildBoneLookup(
        Transform model)
    {
        Dictionary<string, Transform> result =
            new Dictionary<string, Transform>(
                StringComparer.OrdinalIgnoreCase);

        if (model == null)
            return result;

        foreach (Transform item in
                 model.GetComponentsInChildren<
                     Transform>(true))
        {
            string key =
                item.name.Contains(":")
                    ? item.name.Substring(
                        item.name.LastIndexOf(':') + 1)
                    : item.name;

            if (!result.ContainsKey(key))
                result.Add(key, item);
        }

        return result;
    }

    private static Transform FindBone(
        IReadOnlyDictionary<string, Transform> bones,
        string key)
    {
        if (bones == null || string.IsNullOrWhiteSpace(key))
            return null;

        return bones.TryGetValue(key, out Transform value)
            ? value
            : null;
    }

    private static void BindPoint(
        Transform visualRoot,
        string pointName,
        Transform bone,
        Vector3 localOffset)
    {
        Transform point =
            FindDescendant(
                visualRoot,
                pointName);

        if (point == null)
            return;

        if (bone != null)
        {
            // Character Studio가 만든 Anchors/CameraPoints 컨테이너 구조는 보존한다.
            // 참조 Transform을 본 아래로 옮기지 않고 본의 현재 월드 포즈만 샘플링한다.
            point.SetPositionAndRotation(
                bone.TransformPoint(localOffset),
                bone.rotation);
        }
        else
        {
            point.SetPositionAndRotation(
                visualRoot.TransformPoint(localOffset),
                visualRoot.rotation);
        }
    }

    private static void SetLocalPoint(
        Transform visualRoot,
        string pointName,
        Vector3 localPosition)
    {
        Transform point =
            FindDescendant(
                visualRoot,
                pointName);

        if (point == null)
            return;

        // 원래 부모(Anchors/CameraPoints)를 유지한 채 VisualRoot 기준 포즈만 지정한다.
        point.SetPositionAndRotation(
            visualRoot.TransformPoint(localPosition),
            visualRoot.rotation);
    }

    private static Transform FindDescendant(
        Transform root,
        string name)
    {
        if (root == null)
            return null;

        return root.GetComponentsInChildren<
                Transform>(true)
            .FirstOrDefault(
                item => item.name == name);
    }

    private static List<string> ValidateBundle(
        CharacterAuthoringBundle bundle)
    {
        List<string> issues =
            new List<string>();

        if (bundle == null)
        {
            issues.Add("CharacterAuthoringBundle이 없습니다.");
            return issues;
        }

        if (bundle.CharacterData == null)
            issues.Add("CharacterData가 없습니다.");

        if (bundle.CombatLoadout == null)
            issues.Add("CharacterCombatLoadout이 없습니다.");

        if (bundle.SkillSet is not OlafSkillSet)
            issues.Add("OlafSkillSet Legacy Adapter가 없습니다.");

        issues.AddRange(
            CharacterPrefabAssemblyUtility
                .ValidatePrefab(bundle));

        foreach (SkillDefinition skill in
                 bundle.EnumerateSkillDefinitions())
        {
            if (skill == null)
                continue;

            SkillVisualDefinition visual =
                skill.VisualDefinition;

            if (visual == null)
            {
                issues.Add(
                    $"{skill.name}: SkillVisualDefinition 없음");
                continue;
            }

            SkillCutsceneDefinition definition =
                visual.CutsceneDefinition;

            if (definition == null)
            {
                issues.Add(
                    $"{skill.name}: SkillCutsceneDefinition 없음");
                continue;
            }

            if (!definition.HasCompleteTimelineSet)
            {
                issues.Add(
                    $"{skill.name}: 필수 Timeline 세트 누락 (" +
                    string.Join(", ", definition.GetMissingRequirements()) +
                    ")");
            }

            foreach (SkillCutsceneSegment segment in
                     Enum.GetValues(
                         typeof(
                             SkillCutsceneSegment)))
            {
                TimelineAsset timeline =
                    segment == SkillCutsceneSegment.ClashAttack
                        ? definition.ClashAttackTimeline
                        : definition.GetTimeline(segment);

                if (timeline == null)
                {
                    issues.Add(
                        $"{skill.name}: {segment} Timeline 없음");
                    continue;
                }

                bool hasCamera =
                    timeline.GetOutputTracks()
                        .Any(
                            track =>
                                track is
                                    SkillCameraTimelineTrack);

                bool hasEvents =
                    timeline.GetOutputTracks()
                        .Any(
                            track =>
                                track is
                                    SkillCutsceneEventTrack);

                if (!hasCamera)
                {
                    issues.Add(
                        $"{skill.name}/{segment}: Camera Track 없음");
                }

                if (!hasEvents)
                {
                    issues.Add(
                        $"{skill.name}/{segment}: Event Track 없음");
                }
            }
        }

        return issues.Distinct().ToList();
    }

    private static void WriteBuildReport(
        CharacterAuthoringBundle bundle,
        Character prefab,
        Character previewTarget,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyCollection<string> log,
        IReadOnlyCollection<string> issues)
    {
        List<string> lines =
            new List<string>
            {
                "# Olaf Complete Character Build Report",
                string.Empty,
                $"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"- Bundle: `{BundlePath}`",
                $"- Prefab: `{PrefabPath}`",
                $"- Preview Target: `{AssetDatabase.GetAssetPath(previewTarget)}`",
                $"- Character: `{prefab?.name ?? "NULL"}`",
                string.Empty,
                "## Character Studio 결과",
                string.Empty,
                "- CharacterData + 4 linked ActionSlots",
                "- CharacterCombatLoadout + SkillCatalog",
                "- OlafSkillSet Legacy Adapter",
                "- OlafBloodyAxeItem",
                "- BattleInstinct / ToughBody / EnergyCapacity Augment",
                "- AnimatorController + Avatar",
                "- CharacterAuthoringLink / CharacterView / EventBinder / Facing / Mover",
                "- Outline / World Click / Random Debug",
                "- Body Anchors + 16 Camera/Binding Points",
                string.Empty,
                "## Skill Cutscene Studio 결과",
                string.Empty,
                "각 스킬마다 Action, ClashAttack, PartBreak, Kill, Return Segment를 생성했습니다.",
                "Camera Track, Battle Events Track, Attacker/Target Animation Track, " +
                "[AbyssRig] CM_RageOrbit Motion Track을 포함합니다.",
                string.Empty,
                "### Skills"
            };

        if (skills != null)
        {
            foreach (SkillDefinition skill in skills)
            {
                SkillCutsceneDefinition definition =
                    skill?
                        .VisualDefinition?
                        .CutsceneDefinition;

                lines.Add(
                    $"- `{skill?.SkillName ?? "NULL"}` / " +
                    $"Action={definition?.ActionTimeline?.name ?? "NULL"} / " +
                    $"Clash={definition?.ClashAttackTimeline?.name ?? "NULL"} / " +
                    $"Break={definition?.PartBreakTimeline?.name ?? "NULL"} / " +
                    $"Kill={definition?.KillTimeline?.name ?? "NULL"} / " +
                    $"Return={definition?.ReturnTimeline?.name ?? "NULL"}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Assembly Log");
        lines.Add(string.Empty);

        if (log != null)
        {
            foreach (string entry in log)
            {
                lines.Add("```text");
                lines.Add(entry ?? string.Empty);
                lines.Add("```");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Validation");
        lines.Add(string.Empty);

        if (issues == null || issues.Count == 0)
        {
            lines.Add("- Static validation: PASS");
            lines.Add("- Unity Compile/PlayMode는 프로젝트에서 별도 실행 필요");
        }
        else
        {
            foreach (string issue in issues)
                lines.Add("- " + issue);
        }

        File.WriteAllLines(
            ReportPath,
            lines);
    }

    private static void RegisterBundleAssets(
        CharacterAuthoringBundle bundle,
        IEnumerable<Object> assets)
    {
        if (bundle == null || assets == null)
            return;

        foreach (Object asset in assets)
        {
            if (asset != null)
                bundle.RegisterIncludedAsset(asset);
        }

        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<
                RuntimeAnimatorController>(
                    AnimatorControllerPath);

        AnimationClip[] animations =
        {
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(NormalAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(DuelAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(PreparationAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(PrestigeAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(HitAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(BrokenAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(DeathAnimationPath),
            AssetDatabase.LoadAssetAtPath<
                AnimationClip>(IdleAnimationPath)
        };

        bundle.RegisterSupportingAsset(controller);

        foreach (AnimationClip animation in animations)
            bundle.RegisterSupportingAsset(animation);

        bundle.RegisterSupportingAsset(
            AssetDatabase.LoadAssetAtPath<
                BattleVfxDefinition>(BloodVfxPath));
        EditorUtility.SetDirty(bundle);
    }

    private static CharacterSlotConfig Slot(
        string id,
        string displayName,
        PartType part,
        int minSpeed,
        int maxSpeed,
        params ActionType[] allowed)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = displayName,
            Enabled = true,
            HasLinkedPart = true,
            LinkedPartType = part,
            OverrideSpeedRange = true,
            MinSpeed = minSpeed,
            MaxSpeed = maxSpeed,
            AllowedActionTypes =
                allowed?
                    .Distinct()
                    .ToList() ??
                new List<ActionType>()
        };
    }

    private static SkillRollData DiceRoll(
        int index,
        CombatRollType type,
        int min,
        int max,
        int judgment = 0)
    {
        return new SkillRollData
        {
            Index = index,
            Type = type,
            MinPower = min,
            MaxPower = max,
            RngSource = RollRngSource.Dice,
            JudgmentModifier = judgment,
            OnWinEffects = new List<SkillEffectDefinition>(),
            OnLoseEffects = new List<SkillEffectDefinition>()
        };
    }

    private static SkillRollData CoinRoll(
        int index,
        CombatRollType type,
        int back,
        int front,
        bool critical)
    {
        return new SkillRollData
        {
            Index = index,
            Type = type,
            MinPower = Mathf.Min(back, front),
            MaxPower = Mathf.Max(back, front),
            RngSource = RollRngSource.Coin,
            CoinFrontChance = 0.5f,
            CoinBackPower = back,
            CoinFrontPower = front,
            CoinFrontIsCritical = critical,
            OnWinEffects = new List<SkillEffectDefinition>(),
            OnLoseEffects = new List<SkillEffectDefinition>()
        };
    }

    private static SkillRollData SlotRoll(
        int index,
        CombatRollType type,
        int minimum,
        int maximum)
    {
        return new SkillRollData
        {
            Index = index,
            Type = type,
            MinPower = minimum * minimum,
            MaxPower = maximum * maximum,
            RngSource = RollRngSource.Slot,
            SlotMinimum = minimum,
            SlotMaximum = maximum,
            OnWinEffects = new List<SkillEffectDefinition>(),
            OnLoseEffects = new List<SkillEffectDefinition>()
        };
    }

    private static SkillRollData ChinchiroRoll(
        int index,
        CombatRollType type,
        int hifumi,
        int blank,
        int moku,
        int shigoro,
        int arashi)
    {
        return new SkillRollData
        {
            Index = index,
            Type = type,
            MinPower = Mathf.Max(0, hifumi),
            MaxPower = Mathf.Max(
                arashi,
                Mathf.Max(moku, shigoro)),
            RngSource = RollRngSource.Chinchiro,
            ChinchiroHifumiPower = hifumi,
            ChinchiroBlankPower = blank,
            ChinchiroMokuPower = moku,
            ChinchiroShigoroPower = shigoro,
            ChinchiroArashiPower = arashi,
            OnWinEffects = new List<SkillEffectDefinition>(),
            OnLoseEffects = new List<SkillEffectDefinition>()
        };
    }

    private static List<SkillKeywordEntry> Keywords(
        params (string Name, string Description)[] values)
    {
        List<SkillKeywordEntry> result =
            new List<SkillKeywordEntry>();

        if (values == null)
            return result;

        foreach ((string name, string description) in values)
        {
            result.Add(
                new SkillKeywordEntry
                {
                    Name = name,
                    Description = description
                });
        }

        return result;
    }

    private static List<SkillDefinition> ListOf(
        SkillDefinition skill)
    {
        return skill == null
            ? new List<SkillDefinition>()
            : new List<SkillDefinition>
            {
                skill
            };
    }

    private static SkillCameraPositionBinding WideBinding(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraPositionBinding.AttackerRoot,
            2 => SkillCameraPositionBinding.World,
            3 => SkillCameraPositionBinding.CombatFrameFollow,
            _ => SkillCameraPositionBinding.RigRootFixed
        };
    }

    private static SkillCameraAimBinding WideAim(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraAimBinding.TargetRoot,
            2 => SkillCameraAimBinding.CombatFrame,
            3 => SkillCameraAimBinding.AttackerTargetMidpoint,
            _ => SkillCameraAimBinding.AttackerTargetMidpoint
        };
    }

    private static SkillCameraPositionBinding FollowBinding(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraPositionBinding.AttackerAnchor,
            2 => SkillCameraPositionBinding.TargetRoot,
            3 => SkillCameraPositionBinding.AttackerVisualRoot,
            _ => SkillCameraPositionBinding.AttackerVisualRoot
        };
    }

    private static SkillCameraAimBinding FollowAim(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraAimBinding.TargetAnchor,
            2 => SkillCameraAimBinding.AttackerRoot,
            3 => SkillCameraAimBinding.TargetVisualRoot,
            _ => SkillCameraAimBinding.TargetVisualRoot
        };
    }

    private static SkillCameraPositionBinding OrbitBinding(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraPositionBinding.AttackerTargetMidpoint,
            2 => SkillCameraPositionBinding.TargetVisualRoot,
            3 => SkillCameraPositionBinding.AttackerAnchor,
            _ => SkillCameraPositionBinding.TargetAnchor
        };
    }

    private static SkillCameraAimBinding OrbitAim(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraAimBinding.AttackerVisualRoot,
            2 => SkillCameraAimBinding.TargetAnchor,
            3 => SkillCameraAimBinding.TargetAnchor,
            _ => SkillCameraAimBinding.TargetAnchor
        };
    }

    private static SkillCameraPositionBinding ImpactBinding(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraPositionBinding.TargetVisualRoot,
            2 => SkillCameraPositionBinding.TargetAnchor,
            3 => SkillCameraPositionBinding.TargetAnchor,
            _ => SkillCameraPositionBinding.CombatFrameFollow
        };
    }

    private static SkillCameraAimBinding ImpactAim(
        int variant)
    {
        return variant switch
        {
            1 => SkillCameraAimBinding.TargetRoot,
            2 => SkillCameraAimBinding.TargetAnchor,
            3 => SkillCameraAimBinding.TargetAnchor,
            _ => SkillCameraAimBinding.TargetVisualRoot
        };
    }

    private static T LoadFirstAssetByName<T>(
        string name)
        where T : Object
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"{name} t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            T asset =
                AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(
                        guid));

            if (asset != null)
                return asset;
        }

        return null;
    }

    private static void PrepareForGeneratedRootRebuild()
    {
        // EditorWindow가 이전 Generated Complete 에셋 Component를 직렬화 필드로
        // 보유한 채 폴더를 삭제하면 다음 Repaint에서 MissingReferenceException이 난다.
        foreach (ProjectAbyssSkillCutsceneStudio window in
                 Resources.FindObjectsOfTypeAll<
                     ProjectAbyssSkillCutsceneStudio>())
        {
            window.Close();
        }

        foreach (ProjectAbyssCharacterStudio window in
                 Resources.FindObjectsOfTypeAll<
                     ProjectAbyssCharacterStudio>())
        {
            window.Close();
        }

        if (Selection.activeObject != null)
        {
            string selectionPath =
                AssetDatabase.GetAssetPath(
                    Selection.activeObject);

            if (!string.IsNullOrWhiteSpace(
                    selectionPath) &&
                selectionPath.StartsWith(
                    GeneratedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                Selection.activeObject = null;
            }
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            string.IsNullOrWhiteSpace(
                activeScene.path) ||
            !activeScene.path.StartsWith(
                GeneratedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (activeScene.isDirty &&
            !EditorSceneManager
                .SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new OperationCanceledException(
                "열려 있는 Generated Complete Preview Scene의 저장 처리가 취소되었습니다.");
        }

        const string fallbackScenePath =
            "Assets/5. Scenes/CameraTest.unity";

        if (AssetDatabase.LoadAssetAtPath<
                SceneAsset>(
                fallbackScenePath) != null)
        {
            EditorSceneManager.OpenScene(
                fallbackScenePath,
                OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }
    }

    private static void DeleteGeneratedRoot()
    {
        if (!AssetDatabase.IsValidFolder(
                GeneratedRoot))
        {
            return;
        }

        if (!AssetDatabase.DeleteAsset(
                GeneratedRoot))
        {
            throw new InvalidOperationException(
                $"기존 생성 폴더를 삭제하지 못했습니다: {GeneratedRoot}");
        }

        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureGeneratedFolders()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);
        EnsureFolder(SkillFolder);
        EnsureFolder(BuildFolder);
        EnsureFolder(AnimationFolder);
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent =
            Path.GetDirectoryName(path)?
                .Replace('\\', '/');

        string name =
            Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(parent) ||
            string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static string SanitizeFileName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unnamed";

        char[] invalid =
            Path.GetInvalidFileNameChars();

        return new string(
            value.Select(
                    character =>
                        invalid.Contains(character)
                            ? '_'
                            : character)
                .ToArray())
            .Replace('/', '_')
            .Replace('\\', '_');
    }

    private static void SetSerializedString(
        Object target,
        string propertyName,
        string value)
    {
        SerializedObject serialized =
            new SerializedObject(target);
        serialized.Update();
        SetString(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedInt(
        Object target,
        string propertyName,
        int value)
    {
        SerializedObject serialized =
            new SerializedObject(target);
        serialized.Update();
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedFloat(
        Object target,
        string propertyName,
        float value)
    {
        SerializedObject serialized =
            new SerializedObject(target);
        serialized.Update();
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedBool(
        Object target,
        string propertyName,
        bool value)
    {
        SerializedObject serialized =
            new SerializedObject(target);
        serialized.Update();
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property =
            serialized?.FindProperty(propertyName);

        if (property != null &&
            property.propertyType ==
                SerializedPropertyType.ObjectReference)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            serialized?.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serialized?.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value ?? string.Empty;
    }

    private readonly struct OlafCutscenePlan
    {
        public OlafCutscenePlan(
            int durationFrames,
            int[] hitFrames,
            float slowMotionScale,
            bool useLegacyMovement,
            int cameraVariant)
        {
            DurationFrames =
                Mathf.Max(30, durationFrames);
            HitFrames =
                hitFrames ?? Array.Empty<int>();
            SlowMotionScale =
                Mathf.Max(0.01f, slowMotionScale);
            UseLegacyMovement =
                useLegacyMovement;
            CameraVariant =
                Mathf.Max(0, cameraVariant);
        }

        public int DurationFrames { get; }
        public int[] HitFrames { get; }
        public float SlowMotionScale { get; }
        public bool UseLegacyMovement { get; }
        public int CameraVariant { get; }
    }
}

public sealed class OlafCompleteBuildResult
{
    private OlafCompleteBuildResult()
    {
    }

    public bool Success { get; private set; }
    public string Summary { get; private set; }
    public CharacterAuthoringBundle Bundle { get; private set; }
    public Olaf Prefab { get; private set; }
    public Character PreviewTarget { get; private set; }
    public SkillDefinition PrestigeSkill { get; private set; }

    public static OlafCompleteBuildResult Ok(
        CharacterAuthoringBundle bundle,
        Olaf prefab,
        Character previewTarget,
        SkillDefinition prestigeSkill,
        string summary)
    {
        return new OlafCompleteBuildResult
        {
            Success = true,
            Bundle = bundle,
            Prefab = prefab,
            PreviewTarget = previewTarget,
            PrestigeSkill = prestigeSkill,
            Summary = summary
        };
    }

    public static OlafCompleteBuildResult Fail(
        string summary)
    {
        return new OlafCompleteBuildResult
        {
            Success = false,
            Summary = summary
        };
    }
}
#endif
