#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P0 Full Implementation Patch 이후 기존 직렬화 데이터를 새 계약으로 이관하는 1회성 Editor migration.
/// 반복 실행해도 같은 결과가 되도록(idempotent) 작성한다.
/// </summary>
public static class ProjectAbyssP0Migration
{
    private const string MenuRoot = "Tools/Project Abyss/P0 Migration/";

    private const string HwanhyeongAttackPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_공격.asset";
    private const string HwanhyeongDefensePath =
        "Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_수비.asset";

    private static readonly HashSet<string> LegacyHwanhyeongIds = new(StringComparer.Ordinal)
    {
        "yujin.preparation.hwanhyeong.baeku",
        "yujin.preparation.hwanhyeong.jeokseol",
        "yujin.preparation.hwanhyeong.nakil"
    };

    [MenuItem(MenuRoot + "Audit Legacy Data")]
    public static void AuditLegacyData()
    {
        if (!TryLoadModernHwanhyeong(out SkillDefinition attack, out SkillDefinition defense))
            return;

        MigrationSummary summary = new();
        AuditLoadouts(summary, attack, defense);
        AuditCatalogs(summary);
        AuditEliteBundles(summary);
        AuditElitePrefabs(summary);
        AuditSceneOwnedEliteEnemies(summary);

        Debug.Log(summary.BuildAuditMessage());
        EditorUtility.DisplayDialog(
            "Project Abyss P0 Migration Audit",
            summary.BuildAuditDialog(),
            "OK");
    }

    [MenuItem(MenuRoot + "Migrate Assets + Prefabs")]
    public static void MigrateAssetsAndPrefabs()
    {
        if (!TryLoadModernHwanhyeong(out SkillDefinition attack, out SkillDefinition defense))
            return;

        if (!EditorUtility.DisplayDialog(
                "Project Abyss P0 Migration",
                "기존 Yujin 환형 참조와 Elite/Boss 5부위 직렬화 데이터를 새 P0 계약으로 이관합니다.\n\n" +
                "- 구 환형 3종 에셋 자체는 삭제하지 않습니다.\n" +
                "- 기존에 직접 작성한 5부위 데이터는 덮어쓰지 않습니다.\n" +
                "- Scene-owned EliteEnemy는 별도 메뉴에서 이관합니다.\n\n" +
                "Git working tree가 깨끗한 상태에서 실행하는 것을 권장합니다.",
                "Migrate",
                "Cancel"))
        {
            return;
        }

        MigrationSummary summary = new();
        try
        {
            MigrateLoadouts(summary, attack, defense);
            MigrateCatalogs(summary, attack, defense);
            MigrateEliteBundles(summary);
            MigrateElitePrefabs(summary);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Project Abyss P0 Migration",
                "Migration 중 예외가 발생했습니다. Console을 확인하세요.\n\n" + exception.Message,
                "OK");
            return;
        }

        Debug.Log(summary.BuildMigrationMessage());
        EditorUtility.DisplayDialog(
            "Project Abyss P0 Migration",
            summary.BuildMigrationDialog(),
            "OK");
    }

    [MenuItem(MenuRoot + "Migrate Scene-Owned EliteEnemy")]
    public static void MigrateSceneOwnedEliteEnemies()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!EditorUtility.DisplayDialog(
                "Scene-owned EliteEnemy Migration",
                "Assets 아래 Scene을 순회하여 Prefab instance가 아닌 EliteEnemy/Boss 객체의 빈 bodyPartDefinitions를 " +
                "호환용 5부위 데이터로 채우고 Scene을 저장합니다.\n\n" +
                "Prefab instance는 Prefab migration 결과를 상속하도록 건드리지 않습니다.",
                "Migrate Scenes",
                "Cancel"))
        {
            return;
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        MigrationSummary summary = new();

        try
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    EliteEnemy[] enemies = root.GetComponentsInChildren<EliteEnemy>(true);
                    foreach (EliteEnemy enemy in enemies)
                    {
                        if (enemy == null || PrefabUtility.IsPartOfPrefabInstance(enemy.gameObject))
                            continue;

                        MigrationResult result = EnsureEliteEnemyFivePartData(enemy);
                        if (result == MigrationResult.Changed)
                        {
                            changed = true;
                            summary.SceneEnemiesChanged++;
                        }
                        else if (result == MigrationResult.NeedsManualReview)
                        {
                            summary.SceneEnemiesNeedReview++;
                            summary.Warnings.Add($"Scene {path}: {enemy.name} bodyPartDefinitions는 비어있지 않지만 5개가 아닙니다.");
                        }
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    summary.ScenesChanged++;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Project Abyss P0 Scene Migration",
                "Scene migration 중 예외가 발생했습니다. Console을 확인하세요.\n\n" + exception.Message,
                "OK");
            return;
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(summary.BuildSceneMigrationMessage());
        EditorUtility.DisplayDialog(
            "Project Abyss P0 Scene Migration",
            summary.BuildSceneMigrationDialog(),
            "OK");
    }

    private static bool TryLoadModernHwanhyeong(
        out SkillDefinition attack,
        out SkillDefinition defense)
    {
        attack = AssetDatabase.LoadAssetAtPath<SkillDefinition>(HwanhyeongAttackPath);
        defense = AssetDatabase.LoadAssetAtPath<SkillDefinition>(HwanhyeongDefensePath);

        if (attack != null && defense != null)
            return true;

        EditorUtility.DisplayDialog(
            "Project Abyss P0 Migration",
            "환형(공격)/(수비) 에셋을 찾지 못했습니다.\n\n" +
            "먼저 Project_Abyss_P0_Full_Implementation_Patch_v1을 적용하고 Unity compile이 끝난 뒤 migration을 실행하세요.",
            "OK");
        return false;
    }

    private static void AuditLoadouts(
        MigrationSummary summary,
        SkillDefinition attack,
        SkillDefinition defense)
    {
        foreach (CharacterCombatLoadout loadout in LoadAllAssets<CharacterCombatLoadout>())
        {
            bool equippedLegacy = ContainsLegacy(loadout.PreparationSkills);
            bool poolLegacy = ContainsLegacy(loadout.CharacterPreparationPool);
            bool bothModernEquipped =
                loadout.PreparationSkills != null &&
                loadout.PreparationSkills.Contains(attack) &&
                loadout.PreparationSkills.Contains(defense);

            if (equippedLegacy || poolLegacy)
                summary.LoadoutsNeedMigration++;
            if (bothModernEquipped)
            {
                summary.LoadoutsNeedReview++;
                summary.Warnings.Add($"Loadout {AssetDatabase.GetAssetPath(loadout)}: 환형(공격)/(수비)이 동시에 장착되어 있습니다.");
            }
        }
    }

    private static void MigrateLoadouts(
        MigrationSummary summary,
        SkillDefinition attack,
        SkillDefinition defense)
    {
        foreach (CharacterCombatLoadout loadout in LoadAllAssets<CharacterCombatLoadout>())
        {
            if (loadout == null)
                continue;

            bool changed = false;
            changed |= MigrateEquippedHwanhyeong(loadout, attack, defense, summary);
            changed |= MigrateHwanhyeongPool(loadout, attack, defense);

            if (!changed)
                continue;

            EditorUtility.SetDirty(loadout);
            summary.LoadoutsChanged++;
        }
    }

    private static bool MigrateEquippedHwanhyeong(
        CharacterCombatLoadout loadout,
        SkillDefinition attack,
        SkillDefinition defense,
        MigrationSummary summary)
    {
        List<SkillDefinition> list = loadout.PreparationSkills;
        if (list == null)
            return false;

        int firstLegacyIndex = -1;
        bool hadLegacy = false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            SkillDefinition skill = list[i];
            if (!IsLegacyHwanhyeong(skill))
                continue;

            firstLegacyIndex = i;
            hadLegacy = true;
            list.RemoveAt(i);
        }

        if (!hadLegacy)
            return false;

        bool hasAttack = list.Contains(attack);
        bool hasDefense = list.Contains(defense);
        if (!hasAttack && !hasDefense)
        {
            int insertIndex = Mathf.Clamp(firstLegacyIndex, 0, list.Count);
            list.Insert(insertIndex, attack);
        }
        else if (hasAttack && hasDefense)
        {
            summary.LoadoutsNeedReview++;
            summary.Warnings.Add(
                $"Loadout {AssetDatabase.GetAssetPath(loadout)}: legacy 환형은 제거했지만 modern 공격/수비가 둘 다 장착되어 있어 자동 선택하지 않았습니다.");
        }

        RemoveDuplicateReferences(list);
        return true;
    }

    private static bool MigrateHwanhyeongPool(
        CharacterCombatLoadout loadout,
        SkillDefinition attack,
        SkillDefinition defense)
    {
        List<SkillDefinition> pool = loadout.CharacterPreparationPool;
        if (pool == null)
            return false;

        bool hadLegacy = false;
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (!IsLegacyHwanhyeong(pool[i]))
                continue;

            pool.RemoveAt(i);
            hadLegacy = true;
        }

        if (!hadLegacy)
            return false;

        AddUnique(pool, attack);
        AddUnique(pool, defense);
        RemoveDuplicateReferences(pool);
        return true;
    }

    private static void AuditCatalogs(MigrationSummary summary)
    {
        foreach (SkillCatalog catalog in LoadAllAssets<SkillCatalog>())
        {
            if (catalog?.Skills != null && catalog.Skills.Any(IsLegacyHwanhyeong))
                summary.CatalogsNeedMigration++;
        }
    }

    private static void MigrateCatalogs(
        MigrationSummary summary,
        SkillDefinition attack,
        SkillDefinition defense)
    {
        foreach (SkillCatalog catalog in LoadAllAssets<SkillCatalog>())
        {
            if (catalog?.Skills == null || !catalog.Skills.Any(IsLegacyHwanhyeong))
                continue;

            List<SkillDefinition> definitions = catalog.Skills
                .Where(skill => skill != null && !IsLegacyHwanhyeong(skill))
                .ToList();
            AddUnique(definitions, attack);
            AddUnique(definitions, defense);

            Undo.RecordObject(catalog, "P0 migrate Yujin hwanhyeong catalog");
            catalog.ReplaceAll(definitions);
            EditorUtility.SetDirty(catalog);
            summary.CatalogsChanged++;
        }
    }

    private static void AuditEliteBundles(MigrationSummary summary)
    {
        foreach (CharacterAuthoringBundle bundle in LoadAllAssets<CharacterAuthoringBundle>())
        {
            if (bundle == null || bundle.Kind != CharacterAuthoringKind.EliteEnemy)
                continue;

            int count = bundle.EliteBodyPartDefinitions?.Count ?? 0;
            if (count == 0)
                summary.EliteBundlesNeedMigration++;
            else if (count != 5)
            {
                summary.EliteBundlesNeedReview++;
                summary.Warnings.Add($"Bundle {AssetDatabase.GetAssetPath(bundle)}: EliteBodyPartDefinitions={count}, 자동 덮어쓰기하지 않습니다.");
            }
        }
    }

    private static void MigrateEliteBundles(MigrationSummary summary)
    {
        foreach (CharacterAuthoringBundle bundle in LoadAllAssets<CharacterAuthoringBundle>())
        {
            if (bundle == null || bundle.Kind != CharacterAuthoringKind.EliteEnemy)
                continue;

            int count = bundle.EliteBodyPartDefinitions?.Count ?? 0;
            if (count == 5)
                continue;

            if (count != 0)
            {
                summary.EliteBundlesNeedReview++;
                summary.Warnings.Add($"Bundle {AssetDatabase.GetAssetPath(bundle)}: EliteBodyPartDefinitions={count}, 자동 덮어쓰기하지 않습니다.");
                continue;
            }

            Undo.RecordObject(bundle, "P0 migrate elite five body parts");
            bundle.ConfigureEliteBodyParts(CreateCompatibilityFivePartTemplate());
            EditorUtility.SetDirty(bundle);
            summary.EliteBundlesChanged++;
        }
    }

    private static void AuditElitePrefabs(MigrationSummary summary)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            foreach (EliteEnemy enemy in prefab.GetComponentsInChildren<EliteEnemy>(true))
            {
                int count = enemy?.BodyPartDefinitions?.Count ?? 0;
                if (count == 0)
                    summary.ElitePrefabsNeedMigration++;
                else if (count != 5)
                {
                    summary.ElitePrefabsNeedReview++;
                    summary.Warnings.Add($"Prefab {path}: {enemy.name} BodyPartDefinitions={count}, 자동 덮어쓰기하지 않습니다.");
                }
            }
        }
    }

    private static void MigrateElitePrefabs(MigrationSummary summary)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (preview == null || preview.GetComponentInChildren<EliteEnemy>(true) == null)
                continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                EliteEnemy[] enemies = root.GetComponentsInChildren<EliteEnemy>(true);
                foreach (EliteEnemy enemy in enemies)
                {
                    // Nested prefab instance는 원본 prefab migration 결과를 상속시킨다.
                    if (PrefabUtility.IsPartOfPrefabInstance(enemy.gameObject) &&
                        PrefabUtility.GetNearestPrefabInstanceRoot(enemy.gameObject) != root)
                    {
                        continue;
                    }

                    MigrationResult result = EnsureEliteEnemyFivePartData(enemy);
                    if (result == MigrationResult.Changed)
                    {
                        changed = true;
                        summary.ElitePrefabComponentsChanged++;
                    }
                    else if (result == MigrationResult.NeedsManualReview)
                    {
                        summary.ElitePrefabsNeedReview++;
                        summary.Warnings.Add($"Prefab {path}: {enemy.name} BodyPartDefinitions가 비어있지 않지만 5개가 아닙니다.");
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    summary.ElitePrefabsChanged++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AuditSceneOwnedEliteEnemies(MigrationSummary summary)
    {
        // Audit에서 Scene을 열고 저장하지 않는다. 현재 열린 Scene만 안전하게 확인한다.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EliteEnemy enemy in root.GetComponentsInChildren<EliteEnemy>(true))
                {
                    if (enemy == null || PrefabUtility.IsPartOfPrefabInstance(enemy.gameObject))
                        continue;

                    int count = enemy.BodyPartDefinitions?.Count ?? 0;
                    if (count == 0)
                        summary.SceneEnemiesNeedMigration++;
                    else if (count != 5)
                        summary.SceneEnemiesNeedReview++;
                }
            }
        }
    }

    private static MigrationResult EnsureEliteEnemyFivePartData(EliteEnemy enemy)
    {
        if (enemy == null)
            return MigrationResult.Unchanged;

        int currentCount = enemy.BodyPartDefinitions?.Count ?? 0;
        if (currentCount == 5)
            return MigrationResult.Unchanged;
        if (currentCount != 0)
            return MigrationResult.NeedsManualReview;

        SerializedObject serialized = new(enemy);
        SerializedProperty list = serialized.FindProperty("bodyPartDefinitions");
        if (list == null || !list.isArray)
            throw new InvalidOperationException($"{enemy.name}: bodyPartDefinitions SerializedProperty를 찾지 못했습니다.");

        WriteCompatibilityFivePartTemplate(list);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enemy);
        return MigrationResult.Changed;
    }

    private static void WriteCompatibilityFivePartTemplate(SerializedProperty list)
    {
        List<EnemyBodyPartDefinition> template = CreateCompatibilityFivePartTemplate();
        list.arraySize = template.Count;

        for (int i = 0; i < template.Count; i++)
        {
            EnemyBodyPartDefinition source = template[i];
            SerializedProperty element = list.GetArrayElementAtIndex(i);

            SetString(element, "PartId", source.PartId);
            SetString(element, "DisplayName", source.DisplayName);
            SetEnum(element, "LegacyType", (int)source.LegacyType);
            SetFloat(element, "MaxPartHP", source.MaxPartHP);
            SetEnum(element, "SlotRole", (int)source.SlotRole);
            SetInt(element, "RollCountPenalty", source.RollCountPenalty);
            SetInt(element, "SpeedMaxPenalty", source.SpeedMaxPenalty);
            SetBool(element, "NormalAttackOnly", source.NormalAttackOnly);
            SetInt(element, "BrokenRollCountPenalty", source.BrokenRollCountPenalty);
            SetInt(element, "BrokenSpeedMaxPenalty", source.BrokenSpeedMaxPenalty);
            SetInt(element, "BrokenEnergyMaxPenalty", source.BrokenEnergyMaxPenalty);
            SetBool(element, "BrokenNormalAttackOnly", source.BrokenNormalAttackOnly);
            SetStringArray(element, "BrokenForbiddenSkillIds", source.BrokenForbiddenSkillIds);
            SetInt(element, "BrokenForbiddenPostures", (int)source.BrokenForbiddenPostures);
        }
    }

    private static List<EnemyBodyPartDefinition> CreateCompatibilityFivePartTemplate()
    {
        return new List<EnemyBodyPartDefinition>
        {
            new()
            {
                PartId = "head",
                DisplayName = "Head",
                LegacyType = PartType.HEAD,
                SlotRole = BodyPartSlotRole.Hybrid,
                MaxPartHP = 50f,
                BrokenEnergyMaxPenalty = 1
            },
            new()
            {
                PartId = "left_arm",
                DisplayName = "Left Arm",
                LegacyType = PartType.LEFT_HAND,
                SlotRole = BodyPartSlotRole.Attack,
                MaxPartHP = 50f,
                RollCountPenalty = 1
            },
            new()
            {
                PartId = "right_arm",
                DisplayName = "Right Arm",
                LegacyType = PartType.RIGHT_HAND,
                SlotRole = BodyPartSlotRole.Attack,
                MaxPartHP = 50f,
                RollCountPenalty = 1
            },
            new()
            {
                PartId = "legs",
                DisplayName = "Legs",
                LegacyType = PartType.LEGS,
                SlotRole = BodyPartSlotRole.Preparation,
                MaxPartHP = 50f,
                SpeedMaxPenalty = 1
            },
            new()
            {
                PartId = "extra",
                DisplayName = "Extra",
                LegacyType = PartType.CUSTOM,
                SlotRole = BodyPartSlotRole.Hybrid,
                MaxPartHP = 50f
            }
        };
    }

    private static IEnumerable<T> LoadAllAssets<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                yield return asset;
        }
    }

    private static bool ContainsLegacy(IEnumerable<SkillDefinition> skills) =>
        skills != null && skills.Any(IsLegacyHwanhyeong);

    private static bool IsLegacyHwanhyeong(SkillDefinition skill) =>
        skill != null && LegacyHwanhyeongIds.Contains(skill.SkillId);

    private static void AddUnique(List<SkillDefinition> list, SkillDefinition value)
    {
        if (list != null && value != null && !list.Contains(value))
            list.Add(value);
    }

    private static void RemoveDuplicateReferences(List<SkillDefinition> list)
    {
        if (list == null)
            return;

        HashSet<SkillDefinition> seen = new();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            SkillDefinition value = list[i];
            if (value == null || !seen.Add(value))
                list.RemoveAt(i);
        }
    }

    private static void SetString(SerializedProperty parent, string name, string value) =>
        RequireRelative(parent, name).stringValue = value;

    private static void SetInt(SerializedProperty parent, string name, int value) =>
        RequireRelative(parent, name).intValue = value;

    private static void SetFloat(SerializedProperty parent, string name, float value) =>
        RequireRelative(parent, name).floatValue = value;

    private static void SetBool(SerializedProperty parent, string name, bool value) =>
        RequireRelative(parent, name).boolValue = value;

    private static void SetEnum(SerializedProperty parent, string name, int value) =>
        RequireRelative(parent, name).enumValueIndex = value;

    private static void SetStringArray(
        SerializedProperty parent,
        string name,
        IReadOnlyList<string> values)
    {
        SerializedProperty property = RequireRelative(parent, name);
        if (!property.isArray)
            throw new InvalidOperationException($"Serialized field '{name}' is not an array: {property.propertyPath}");

        int count = values?.Count ?? 0;
        property.arraySize = count;
        for (int i = 0; i < count; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i] ?? string.Empty;
    }

    private static SerializedProperty RequireRelative(SerializedProperty parent, string name)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException($"Serialized field '{name}'를 찾지 못했습니다: {parent.propertyPath}");
        return property;
    }

    private enum MigrationResult
    {
        Unchanged,
        Changed,
        NeedsManualReview
    }

    private sealed class MigrationSummary
    {
        public int LoadoutsNeedMigration;
        public int LoadoutsChanged;
        public int LoadoutsNeedReview;
        public int CatalogsNeedMigration;
        public int CatalogsChanged;
        public int EliteBundlesNeedMigration;
        public int EliteBundlesChanged;
        public int EliteBundlesNeedReview;
        public int ElitePrefabsNeedMigration;
        public int ElitePrefabsChanged;
        public int ElitePrefabComponentsChanged;
        public int ElitePrefabsNeedReview;
        public int SceneEnemiesNeedMigration;
        public int SceneEnemiesChanged;
        public int SceneEnemiesNeedReview;
        public int ScenesChanged;
        public readonly List<string> Warnings = new();

        public string BuildAuditDialog() =>
            $"Legacy 환형 Loadout: {LoadoutsNeedMigration}\n" +
            $"Legacy 환형 Catalog: {CatalogsNeedMigration}\n" +
            $"5부위 미작성 Elite/Boss Bundle: {EliteBundlesNeedMigration}\n" +
            $"5부위 미작성 Elite/Boss Prefab component: {ElitePrefabsNeedMigration}\n" +
            $"현재 열린 Scene의 scene-owned EliteEnemy: {SceneEnemiesNeedMigration}\n" +
            $"수동 확인 필요: {TotalReviewCount}\n\n" +
            "Console에 상세 경고를 출력했습니다.";

        public string BuildMigrationDialog() =>
            $"Loadout 변경: {LoadoutsChanged}\n" +
            $"Catalog 변경: {CatalogsChanged}\n" +
            $"Elite/Boss Bundle 변경: {EliteBundlesChanged}\n" +
            $"Prefab 변경: {ElitePrefabsChanged} (component {ElitePrefabComponentsChanged})\n" +
            $"수동 확인 필요: {TotalReviewCount}\n\n" +
            "Scene-owned 객체는 별도 메뉴로 이관하세요.";

        public string BuildSceneMigrationDialog() =>
            $"Scene 변경: {ScenesChanged}\n" +
            $"scene-owned EliteEnemy 변경: {SceneEnemiesChanged}\n" +
            $"수동 확인 필요: {SceneEnemiesNeedReview}";

        public string BuildAuditMessage() =>
            "[P0 Migration Audit]\n" + BuildAuditDialog() + BuildWarnings();

        public string BuildMigrationMessage() =>
            "[P0 Migration]\n" + BuildMigrationDialog() + BuildWarnings();

        public string BuildSceneMigrationMessage() =>
            "[P0 Scene Migration]\n" + BuildSceneMigrationDialog() + BuildWarnings();

        private int TotalReviewCount =>
            LoadoutsNeedReview +
            EliteBundlesNeedReview +
            ElitePrefabsNeedReview +
            SceneEnemiesNeedReview;

        private string BuildWarnings()
        {
            if (Warnings.Count == 0)
                return string.Empty;
            return "\n\nWarnings:\n- " + string.Join("\n- ", Warnings);
        }
    }
}
#endif
