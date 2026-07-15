#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BattleDynamicRosterSetupTool
{
    private const string MenuPath =
        "Tools/Project Abyss/Battle/" +
        "Convert Current Scene To Dynamic Roster";

    [MenuItem(MenuPath, priority = 2100)]
    public static void ConvertCurrentScene()
    {
        BattleManager battleManager =
            Object.FindFirstObjectByType<BattleManager>();

        BattleUIManager uiManager =
            Object.FindFirstObjectByType<BattleUIManager>();

        if (battleManager == null ||
            uiManager == null)
        {
            EditorUtility.DisplayDialog(
                "Dynamic Roster Setup",
                "BattleManager와 BattleUIManager가 모두 필요합니다.",
                "확인");

            return;
        }

        Character legacyPlayer =
            battleManager.LegacyPlayerForMigration;

        List<Character> legacyEnemies =
            CopyCharacters(
                battleManager.LegacyEnemiesForMigration);

        if (legacyPlayer == null ||
            legacyEnemies.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Dynamic Roster Setup",
                "기존 Player 또는 Enemy 연결을 찾지 못했습니다.",
                "확인");

            return;
        }

        BodyPartButton playerTemplate =
            FindFirstButton(
                battleManager.LegacyPlayerButtonsForMigration);

        BodyPartButton enemyTemplate =
            FindFirstButton(
                battleManager.LegacyEnemyButtonsForMigration);

        if (playerTemplate == null ||
            enemyTemplate == null)
        {
            EditorUtility.DisplayDialog(
                "Dynamic Roster Setup",
                "기존 Player/Enemy BodyPartButton Template을 찾지 못했습니다.",
                "확인");

            return;
        }

        RectTransform playerContainer =
            playerTemplate.transform.parent as RectTransform;

        RectTransform enemyContainer =
            enemyTemplate.transform.parent as RectTransform;

        if (playerContainer == null ||
            enemyContainer == null)
        {
            EditorUtility.DisplayDialog(
                "Dynamic Roster Setup",
                "Button Container는 RectTransform이어야 합니다.",
                "확인");

            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Convert Battle To Dynamic Roster");

        try
        {
            BattleRosterController rosterController =
                GetOrAddComponent<BattleRosterController>(
                    battleManager.gameObject);

            BattleParticipantButtonFactory buttonFactory =
                GetOrAddComponent<BattleParticipantButtonFactory>(
                    uiManager.gameObject);

            Transform setupRoot =
                FindOrCreateChild(
                    battleManager.transform,
                    "BattleRuntimeSetup");

            Transform spawnRoot =
                FindOrCreateChild(
                    setupRoot,
                    "SpawnPoints");

            Transform runtimeRoot =
                FindOrCreateChild(
                    setupRoot,
                    "RuntimeParticipants");

            Transform playerSpawn =
                FindOrCreateSpawnPoint(
                    spawnRoot,
                    "PlayerSpawn",
                    legacyPlayer.transform);

            List<Transform> enemySpawns = new();

            for (int i = 0; i < legacyEnemies.Count; i++)
            {
                enemySpawns.Add(
                    FindOrCreateSpawnPoint(
                        spawnRoot,
                        $"EnemySpawn_{i}",
                        legacyEnemies[i].transform));
            }

            Undo.RecordObject(
                rosterController,
                "Configure Battle Roster");

            rosterController.ConfigureSceneRoster(
                legacyPlayer,
                legacyEnemies,
                playerSpawn,
                enemySpawns,
                runtimeRoot);

            Undo.RecordObject(
                buttonFactory,
                "Configure Dynamic Buttons");

            buttonFactory.Configure(
                playerTemplate,
                enemyTemplate,
                playerContainer,
                enemyContainer);

            RemoveExtraButtons(
                playerContainer,
                playerTemplate);

            RemoveExtraButtons(
                enemyContainer,
                enemyTemplate);

            Undo.RecordObject(
                battleManager,
                "Assign Battle Roster Controller");

            battleManager.AssignRosterController(
                rosterController);

            battleManager.SetAutoLifecycle(
                initializeOnAwake: true,
                startAutomatically: true);

            battleManager.ClearLegacyMigrationData();

            Undo.RecordObject(
                uiManager,
                "Assign Participant Button Factory");

            uiManager.AssignParticipantButtonFactory(
                buttonFactory);

            EditorUtility.SetDirty(rosterController);
            EditorUtility.SetDirty(buttonFactory);
            EditorUtility.SetDirty(battleManager);
            EditorUtility.SetDirty(uiManager);

            EditorSceneManager.MarkSceneDirty(
                battleManager.gameObject.scene);

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject =
                battleManager.gameObject;

            EditorUtility.DisplayDialog(
                "Dynamic Roster Setup Complete",
                "현재 Player/Enemy 연결과 버튼 Template을 " +
                "동적 Roster 구조로 변환했습니다.\n\n" +
                "이제 BattleRosterController에서 Scene Character 또는 " +
                "Character Prefab을 교체할 수 있습니다.",
                "확인");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static T GetOrAddComponent<T>(
        GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component != null)
            return component;

        return Undo.AddComponent<T>(gameObject);
    }

    private static Transform FindOrCreateChild(
        Transform parent,
        string childName)
    {
        Transform existing = parent.Find(childName);

        if (existing != null)
            return existing;

        GameObject child = new(childName);

        Undo.RegisterCreatedObjectUndo(
            child,
            $"Create {childName}");

        child.transform.SetParent(
            parent,
            worldPositionStays: false);

        return child.transform;
    }

    private static Transform FindOrCreateSpawnPoint(
        Transform parent,
        string pointName,
        Transform sourceTransform)
    {
        Transform point = parent.Find(pointName);

        if (point == null)
        {
            GameObject child = new(pointName);

            Undo.RegisterCreatedObjectUndo(
                child,
                $"Create {pointName}");

            point = child.transform;

            point.SetParent(
                parent,
                worldPositionStays: false);
        }

        if (sourceTransform != null)
        {
            Undo.RecordObject(
                point,
                $"Position {pointName}");

            point.SetPositionAndRotation(
                sourceTransform.position,
                sourceTransform.rotation);

            point.localScale = Vector3.one;
        }

        return point;
    }

    private static void RemoveExtraButtons(
        RectTransform container,
        BodyPartButton template)
    {
        BodyPartButton[] buttons =
            container.GetComponentsInChildren<BodyPartButton>(true);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null || button == template)
                continue;

            Undo.DestroyObjectImmediate(button.gameObject);
        }

        Undo.RecordObject(
            template,
            "Configure Button Template");

        Undo.RecordObject(
            template.gameObject,
            "Configure Button Template GameObject");

        template.name =
            container.name +
            "_ButtonTemplate";

        template.ConfigureAsTemplate();

        EditorUtility.SetDirty(template);
    }

    private static BodyPartButton FindFirstButton(
        IReadOnlyList<BodyPartButton> buttons)
    {
        if (buttons == null)
            return null;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
                return buttons[i];
        }

        return null;
    }

    private static List<Character> CopyCharacters(
        IReadOnlyList<Character> source)
    {
        List<Character> result = new();

        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];

            if (character != null)
                result.Add(character);
        }

        return result;
    }
}

#endif
