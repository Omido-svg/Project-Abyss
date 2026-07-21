#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ProjectAbyssCharacterWorkbench : EditorWindow
{
    private const string DefaultRoot =
        "Assets/2. Data/Characters";

    private CharacterAuthoringKind kind =
        CharacterAuthoringKind.Olaf;

    private string characterName = "New Character";
    private Character templatePrefab;
    private bool duplicateTemplatePrefab = true;
    private bool packTemplateConfiguration = true;
    private string destinationRoot = DefaultRoot;
    private Vector2 scroll;

    [MenuItem(
        "Tools/Project Abyss/Character Workbench",
        false,
        2010)]
    public static void Open()
    {
        GetWindow<ProjectAbyssCharacterWorkbench>(
            "Character Workbench");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Project Abyss Character Workbench",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "캐릭터 하나를 하나의 Character Bundle 에셋으로 관리합니다. " +
            "CharacterData, SkillSet, SkillDefinition, VisualDefinition, CameraDefinition은 " +
            "Bundle 내부 Sub-Asset으로 생성되므로 기존 런타임 기능과 연출 자유도를 유지하면서 파일 수를 줄입니다.",
            MessageType.Info);

        kind = (CharacterAuthoringKind)
            EditorGUILayout.EnumPopup(
                "Character Kind",
                kind);

        characterName =
            EditorGUILayout.TextField(
                "Character Name",
                characterName);

        templatePrefab =
            (Character)EditorGUILayout.ObjectField(
                "Visual/Runtime Template Prefab",
                templatePrefab,
                typeof(Character),
                false);

        duplicateTemplatePrefab =
            EditorGUILayout.ToggleLeft(
                "템플릿 프리팹을 새 캐릭터 프리팹으로 복제",
                duplicateTemplatePrefab);

        using (new EditorGUI.DisabledScope(templatePrefab == null))
        {
            packTemplateConfiguration =
                EditorGUILayout.ToggleLeft(
                    "기존 Character/SO 설정을 Bundle Sub-Asset 복사본으로 패킹",
                    packTemplateConfiguration);
        }

        destinationRoot =
            EditorGUILayout.TextField(
                "Destination Root",
                destinationRoot);

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(
                   string.IsNullOrWhiteSpace(characterName)))
        {
            if (GUILayout.Button(
                    "Create Self-Contained Character Bundle",
                    GUILayout.Height(34f)))
            {
                CreateBundle();
            }
        }

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "기존 캐릭터를 정리할 때는 Template Prefab을 지정하고 복제를 켜세요. " +
            "애니메이터, 모델, 카메라 포인트, VFX 앵커, CharacterView 계층은 그대로 복사되며 " +
            "새 Bundle 참조만 CharacterAuthoringLink로 연결됩니다.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void CreateBundle()
    {
        string safeName = SanitizeName(characterName);
        string root = NormalizeAssetFolder(destinationRoot);
        string characterFolder = $"{root}/{safeName}";

        if (templatePrefab != null &&
            !IsKindCompatible(templatePrefab, kind))
        {
            EditorUtility.DisplayDialog(
                "Character Kind 불일치",
                $"선택한 Prefab의 Character 타입({templatePrefab.GetType().Name})과 " +
                $"Character Kind({kind})이 맞지 않습니다.",
                "확인");
            return;
        }

        EnsureFolder(characterFolder);

        string bundlePath =
            AssetDatabase.GenerateUniqueAssetPath(
                $"{characterFolder}/{safeName}_CharacterBundle.asset");

        CharacterAuthoringBundle bundle =
            CreateInstance<CharacterAuthoringBundle>();

        bundle.name = $"{safeName}_CharacterBundle";
        AssetDatabase.CreateAsset(bundle, bundlePath);

        CharacterAuthoringCloneUtility cloneUtility = null;
        CharacterData data;
        ScriptableObject skillSet;
        SkillVisualProfile visualProfile;

        if (templatePrefab != null &&
            packTemplateConfiguration)
        {
            cloneUtility =
                new CharacterAuthoringCloneUtility(bundle);

            cloneUtility.CapturePrefab(
                templatePrefab.gameObject);
            cloneUtility.FinalizeClones();

            data =
                cloneUtility.GetClone(templatePrefab.Data) ??
                templatePrefab.Data;

            if (data == null)
            {
                data =
                    CreateSubAsset<CharacterData>(
                        bundle,
                        $"{safeName}_CharacterData");
                data.CharacterName = characterName.Trim();
                data.TargetMode = kind == CharacterAuthoringKind.NormalEnemy
                    ? CharacterTargetMode.SingleHP
                    : CharacterTargetMode.BodyParts;
                data.SingleHpMax = 50;
            }

            ScriptableObject sourceSkillSet =
                ReadObjectReference<ScriptableObject>(
                    templatePrefab,
                    "skillSet");

            skillSet =
                cloneUtility.GetClone(sourceSkillSet) ??
                sourceSkillSet;

            visualProfile =
                CreateSubAsset<SkillVisualProfile>(
                    bundle,
                    $"{safeName}_VisualProfile");

            if (skillSet == null)
            {
                skillSet =
                    CreateSkillSet(bundle, safeName);

                ConfigureDefaultSkills(
                    bundle,
                    skillSet,
                    visualProfile,
                    safeName);
            }
            else
            {
                ConfigureVisualProfileFromSkillSet(
                    visualProfile,
                    skillSet);
            }

            ConfigurePackedLoadout(
                bundle,
                templatePrefab,
                cloneUtility);

            ConfigurePackedCharacterSpecificSettings(
                bundle,
                templatePrefab);
        }
        else
        {
            data =
                CreateSubAsset<CharacterData>(
                    bundle,
                    $"{safeName}_CharacterData");

            data.CharacterName = characterName.Trim();
            data.TargetMode = kind == CharacterAuthoringKind.NormalEnemy
                ? CharacterTargetMode.SingleHP
                : CharacterTargetMode.BodyParts;
            data.SingleHpMax = 50;

            skillSet =
                CreateSkillSet(bundle, safeName);

            visualProfile =
                CreateSubAsset<SkillVisualProfile>(
                    bundle,
                    $"{safeName}_VisualProfile");

            ConfigureDefaultSkills(
                bundle,
                skillSet,
                visualProfile,
                safeName);
        }

        bundle.ConfigureCore(
            kind,
            characterName,
            data,
            skillSet,
            visualProfile);

        Character prefab =
            PreparePrefab(
                bundle,
                characterFolder,
                safeName,
                cloneUtility);

        bundle.ConfigurePrefab(prefab);

        EditorUtility.SetDirty(data);

        if (skillSet != null)
            EditorUtility.SetDirty(skillSet);

        EditorUtility.SetDirty(visualProfile);
        EditorUtility.SetDirty(bundle);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = bundle;
        EditorGUIUtility.PingObject(bundle);

        Debug.Log(
            "[CharacterWorkbench] Character Bundle 생성 완료 / " +
            $"Path={bundlePath}, Prefab={(prefab == null ? "NONE" : prefab.name)}");
    }

    private ScriptableObject CreateSkillSet(
        CharacterAuthoringBundle bundle,
        string safeName)
    {
        return kind switch
        {
            CharacterAuthoringKind.Olaf =>
                CreateSubAsset<OlafSkillSet>(
                    bundle,
                    $"{safeName}_SkillSet"),

            CharacterAuthoringKind.EliteEnemy =>
                CreateSubAsset<EliteEnemySkillSet>(
                    bundle,
                    $"{safeName}_SkillSet"),

            CharacterAuthoringKind.NormalEnemy =>
                CreateSubAsset<NormalEnemySkillSet>(
                    bundle,
                    $"{safeName}_SkillSet"),

            _ => null
        };
    }

    private void ConfigureDefaultSkills(
        CharacterAuthoringBundle bundle,
        ScriptableObject skillSet,
        SkillVisualProfile visualProfile,
        string safeName)
    {
        SkillDefinition normal =
            CreateSkill(
                bundle,
                visualProfile,
                safeName,
                ActionType.NormalAttack);

        SkillDefinition duel =
            CreateSkill(
                bundle,
                visualProfile,
                safeName,
                ActionType.Duel);

        SkillDefinition preparation =
            kind == CharacterAuthoringKind.NormalEnemy
                ? null
                : CreateSkill(
                    bundle,
                    visualProfile,
                    safeName,
                    ActionType.Preparation);

        SkillDefinition prestige =
            CreateSkill(
                bundle,
                visualProfile,
                safeName,
                ActionType.Prestige);

        switch (skillSet)
        {
            case OlafSkillSet olaf:
                olaf.NormalAttack = normal;
                olaf.DuelSkill = duel;
                olaf.PreparationSkill = preparation;
                olaf.PrestigeSkill = prestige;
                break;

            case EliteEnemySkillSet elite:
                elite.NormalAttack = normal;
                elite.DuelSkill = duel;
                elite.PreparationSkill = preparation;
                elite.PrestigeSkill = prestige;
                break;

            case NormalEnemySkillSet normalEnemy:
                normalEnemy.NormalAttack = normal;
                normalEnemy.DuelSkill = duel;
                normalEnemy.PrestigeSkill = prestige;
                break;
        }
    }

    private SkillDefinition CreateSkill(
        CharacterAuthoringBundle bundle,
        SkillVisualProfile profile,
        string safeName,
        ActionType actionType)
    {
        SkillDefinition skill =
            CreateSubAsset<SkillDefinition>(
                bundle,
                $"{safeName}_{actionType}_Skill");

        skill.SkillName =
            $"{characterName.Trim()} {GetKoreanActionName(actionType)}";
        skill.ActionType = actionType;
        skill.BasePower = 1;
        skill.ExchangeRollCount = 3;
        skill.ResolverType = SkillResolverType.Dice;
        skill.DiceMin = 1;
        skill.DiceMax = 6;
        skill.CanBreakPart = actionType != ActionType.Preparation;
        skill.GainPrestige = actionType != ActionType.Preparation;
        skill.PreparationTier = actionType == ActionType.Preparation
            ? PreparationTier.Weak
            : PreparationTier.Weak;

        SkillCameraDefinition camera =
            CreateSubAsset<SkillCameraDefinition>(
                bundle,
                $"{safeName}_{actionType}_Camera");

        SkillVisualDefinition visual =
            CreateSubAsset<SkillVisualDefinition>(
                bundle,
                $"{safeName}_{actionType}_Visual");

        visual.CameraDefinition = camera;
        visual.AllowAsProfileFallback = false;
        skill.VisualDefinition = visual;

        switch (actionType)
        {
            case ActionType.NormalAttack:
                profile.NormalAttackVisual = visual;
                break;
            case ActionType.Duel:
                profile.DuelVisual = visual;
                break;
            case ActionType.Preparation:
                profile.PreparationVisual = visual;
                break;
            case ActionType.Prestige:
                profile.PrestigeVisual = visual;
                break;
        }

        EditorUtility.SetDirty(skill);
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(profile);

        return skill;
    }

    private static void ConfigureVisualProfileFromSkillSet(
        SkillVisualProfile profile,
        ScriptableObject skillSet)
    {
        if (profile == null || skillSet == null)
            return;

        IEnumerable<SkillDefinition> definitions =
            skillSet switch
            {
                OlafSkillSet olaf => EnumerateOlaf(olaf),
                EliteEnemySkillSet elite => elite.Definitions,
                NormalEnemySkillSet normal => normal.Definitions,
                _ => Array.Empty<SkillDefinition>()
            };

        foreach (SkillDefinition definition in definitions)
        {
            if (definition == null ||
                definition.VisualDefinition == null)
            {
                continue;
            }

            switch (definition.ActionType)
            {
                case ActionType.NormalAttack:
                    profile.NormalAttackVisual =
                        definition.VisualDefinition;
                    break;
                case ActionType.Duel:
                    profile.DuelVisual =
                        definition.VisualDefinition;
                    break;
                case ActionType.Preparation:
                    profile.PreparationVisual =
                        definition.VisualDefinition;
                    break;
                case ActionType.Prestige:
                    profile.PrestigeVisual =
                        definition.VisualDefinition;
                    break;
            }
        }

        EditorUtility.SetDirty(profile);
    }

    private static IEnumerable<SkillDefinition> EnumerateOlaf(
        OlafSkillSet skillSet)
    {
        if (skillSet == null)
            yield break;

        if (skillSet.NormalAttack != null)
            yield return skillSet.NormalAttack;
        if (skillSet.DuelSkill != null)
            yield return skillSet.DuelSkill;
        if (skillSet.PreparationSkill != null)
            yield return skillSet.PreparationSkill;
        if (skillSet.PrestigeSkill != null)
            yield return skillSet.PrestigeSkill;
    }

    private static void ConfigurePackedLoadout(
        CharacterAuthoringBundle bundle,
        Character sourceCharacter,
        CharacterAuthoringCloneUtility cloneUtility)
    {
        List<CharacterItem> sourceItems =
            ReadObjectReferenceList<CharacterItem>(
                sourceCharacter,
                "equippedItems");

        List<CharacterAugment> sourceAugments =
            ReadObjectReferenceList<CharacterAugment>(
                sourceCharacter,
                "equippedAugments");

        List<CharacterItem> packedItems = new();
        List<CharacterAugment> packedAugments = new();

        for (int i = 0; i < sourceItems.Count; i++)
        {
            CharacterItem source = sourceItems[i];
            CharacterItem packed =
                cloneUtility?.GetClone(source) ?? source;

            if (packed != null)
                packedItems.Add(packed);
        }

        for (int i = 0; i < sourceAugments.Count; i++)
        {
            CharacterAugment source = sourceAugments[i];
            CharacterAugment packed =
                cloneUtility?.GetClone(source) ?? source;

            if (packed != null)
                packedAugments.Add(packed);
        }

        bundle.ConfigureLoadout(
            packedItems,
            packedAugments,
            shouldOverride: true);
    }

    private static void ConfigurePackedCharacterSpecificSettings(
        CharacterAuthoringBundle bundle,
        Character sourceCharacter)
    {
        if (sourceCharacter is NormalEnemy)
        {
            SerializedObject serialized =
                new SerializedObject(sourceCharacter);

            int singleMaxHp =
                serialized.FindProperty("singleMaxHP")?.intValue ?? 50;

            bundle.ConfigureNormalEnemy(
                singleMaxHp,
                shouldOverride: true);
        }

        if (sourceCharacter is not EliteEnemy)
            return;

        SerializedObject eliteSerialized =
            new SerializedObject(sourceCharacter);

        bool usePosture =
            eliteSerialized.FindProperty("usePostureRotation")?.boolValue ??
            true;

        SerializedProperty postureProperty =
            eliteSerialized.FindProperty("postureSettings");

        EnemyPostureSettings settings =
            new EnemyPostureSettings();

        if (postureProperty != null)
        {
            settings.MinimumTurns =
                postureProperty.FindPropertyRelative("MinimumTurns")?.intValue ??
                settings.MinimumTurns;
            settings.MaximumTurns =
                postureProperty.FindPropertyRelative("MaximumTurns")?.intValue ??
                settings.MaximumTurns;
            settings.NormalAttackSlotLimit =
                postureProperty.FindPropertyRelative("NormalAttackSlotLimit")?.intValue ??
                settings.NormalAttackSlotLimit;
            settings.CrouchingAttackSlotLimit =
                postureProperty.FindPropertyRelative("CrouchingAttackSlotLimit")?.intValue ??
                settings.CrouchingAttackSlotLimit;
            settings.OffensiveAttackSlotLimit =
                postureProperty.FindPropertyRelative("OffensiveAttackSlotLimit")?.intValue ??
                settings.OffensiveAttackSlotLimit;
            settings.ExpectedMomentumDriftPerTurn =
                postureProperty.FindPropertyRelative("ExpectedMomentumDriftPerTurn")?.intValue ??
                settings.ExpectedMomentumDriftPerTurn;
        }

        bundle.ConfigureEliteEnemy(
            usePosture,
            settings);
    }

    private static T ReadObjectReference<T>(
        UnityEngine.Object source,
        string propertyName)
        where T : UnityEngine.Object
    {
        if (source == null ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        SerializedObject serialized =
            new SerializedObject(source);

        return serialized.FindProperty(propertyName)
            ?.objectReferenceValue as T;
    }

    private static List<T> ReadObjectReferenceList<T>(
        UnityEngine.Object source,
        string propertyName)
        where T : UnityEngine.Object
    {
        List<T> result = new();

        if (source == null ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return result;
        }

        SerializedObject serialized =
            new SerializedObject(source);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return result;

        for (int i = 0; i < property.arraySize; i++)
        {
            T value =
                property.GetArrayElementAtIndex(i)
                    .objectReferenceValue as T;

            if (value != null)
                result.Add(value);
        }

        return result;
    }

    private static bool IsKindCompatible(
        Character character,
        CharacterAuthoringKind selectedKind)
    {
        if (character == null)
            return true;

        return selectedKind switch
        {
            CharacterAuthoringKind.Olaf => character is Olaf,
            CharacterAuthoringKind.EliteEnemy => character is EliteEnemy,
            CharacterAuthoringKind.NormalEnemy => character is NormalEnemy,
            _ => true
        };
    }

    private Character PreparePrefab(
        CharacterAuthoringBundle bundle,
        string characterFolder,
        string safeName,
        CharacterAuthoringCloneUtility cloneUtility)
    {
        if (templatePrefab == null)
            return null;

        string sourcePath =
            AssetDatabase.GetAssetPath(
                templatePrefab);

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            Debug.LogWarning(
                "[CharacterWorkbench] Template은 Prefab Asset이어야 합니다.");
            return null;
        }

        string targetPath = sourcePath;

        if (duplicateTemplatePrefab)
        {
            targetPath =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{characterFolder}/{safeName}_Character.prefab");

            if (!AssetDatabase.CopyAsset(
                    sourcePath,
                    targetPath))
            {
                Debug.LogError(
                    "[CharacterWorkbench] Template Prefab 복제 실패 / " +
                    sourcePath);
                return null;
            }
        }

        AssetDatabase.ImportAsset(targetPath);

        GameObject root =
            PrefabUtility.LoadPrefabContents(
                targetPath);

        if (root == null)
            return null;

        try
        {
            Character character =
                root.GetComponentInChildren<Character>(true);

            if (character == null)
            {
                Debug.LogError(
                    "[CharacterWorkbench] Prefab에 Character 컴포넌트가 없습니다. / " +
                    targetPath);
                return null;
            }

            cloneUtility?.RemapPrefab(root);

            CharacterAuthoringLink link =
                character.GetComponent<CharacterAuthoringLink>();

            if (link == null)
            {
                link =
                    character.gameObject
                        .AddComponent<CharacterAuthoringLink>();
            }

            link.Configure(bundle);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                targetPath);

        return prefabAsset == null
            ? null
            : prefabAsset.GetComponentInChildren<Character>(true);
    }

    private static T CreateSubAsset<T>(
        CharacterAuthoringBundle bundle,
        string assetName)
        where T : ScriptableObject
    {
        T asset = CreateInstance<T>();
        asset.name = assetName;

        AssetDatabase.AddObjectToAsset(
            asset,
            bundle);

        bundle.RegisterIncludedAsset(asset);
        return asset;
    }

    private static string GetKoreanActionName(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => "일반공격",
            ActionType.Duel => "결투",
            ActionType.Preparation => "도사림",
            ActionType.Prestige => "위세",
            _ => actionType.ToString()
        };
    }

    private static string SanitizeName(string value)
    {
        string result = value.Trim();

        foreach (char invalid in
                 Path.GetInvalidFileNameChars())
        {
            result = result.Replace(
                invalid,
                '_');
        }

        return string.IsNullOrWhiteSpace(result)
            ? "NewCharacter"
            : result;
    }

    private static string NormalizeAssetFolder(
        string value)
    {
        string result = string.IsNullOrWhiteSpace(value)
            ? DefaultRoot
            : value.Replace('\\', '/').TrimEnd('/');

        return result.StartsWith("Assets", StringComparison.Ordinal)
            ? result
            : DefaultRoot;
    }

    private static void EnsureFolder(string assetFolder)
    {
        string[] parts =
            assetFolder.Split('/');

        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current = next;
        }
    }
}
#endif
