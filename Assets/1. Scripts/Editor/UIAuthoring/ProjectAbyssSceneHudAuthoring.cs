#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 런타임 생성형 고정 HUD를 현재 Scene에 한 번 배치하는 Authoring 도구.
/// 생성 후 Play Mode에서는 각 HUD 스크립트가 RectTransform 위치/크기/Anchor를 덮어쓰지 않는다.
/// </summary>
public static class ProjectAbyssSceneHudAuthoring
{
    private const string MenuRoot =
        "Tools/Project Abyss/UI/";

    private const string SceneHudRootName =
        "ProjectAbyssSceneHUD";

    [MenuItem(MenuRoot + "Convert Current Battle Scene To Scene-Authored HUD")]
    private static void ConvertCurrentScene()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss UI",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError(
                "[SceneHUD Authoring] 활성 Scene을 찾지 못했습니다.");
            return;
        }

        GameObject abyssBattleUi =
            FindSceneGameObjectByName(scene, "AbyssBattleUI");

        if (abyssBattleUi == null)
        {
            BattleScreenModeController screenMode =
                FindSceneComponent<BattleScreenModeController>(scene);
            abyssBattleUi = screenMode != null
                ? screenMode.gameObject
                : null;
        }

        if (abyssBattleUi == null)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss UI",
                "현재 Scene에서 AbyssBattleUI/BattleScreenModeController를 찾지 못했습니다.",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(
            "Convert Project Abyss HUD To Scene Authored");

        BattleSceneHudRegistry registry =
            GetOrAddComponent<BattleSceneHudRegistry>(
                abyssBattleUi);

        BattleTopStatusBarUI topStatusBar =
            FindSceneComponent<BattleTopStatusBarUI>(scene);

        GameplayV5ProgressionHudUI progression = null;
        if (topStatusBar != null)
        {
            progression =
                GetOrAddComponent<GameplayV5ProgressionHudUI>(
                    topStatusBar.gameObject);
            progression.EditorAuthorSceneView();
            EditorUtility.SetDirty(progression);
        }
        else
        {
            Debug.LogWarning(
                "[SceneHUD Authoring] BattleTopStatusBarUI가 없어 GameplayV5ProgressionHUD는 만들지 못했습니다.");
        }

        EmotionAugmentChoiceUI emotionChoice =
            GetOrAddComponent<EmotionAugmentChoiceUI>(
                abyssBattleUi);
        emotionChoice.EditorAuthorSceneView();
        EditorUtility.SetDirty(emotionChoice);

        MomentumScrollbarUI momentum =
            FindSceneComponent<MomentumScrollbarUI>(scene);
        if (momentum != null)
        {
            momentum.EditorAuthorSceneView();
            EditorUtility.SetDirty(momentum);
        }

        GameObject sceneHudRoot =
            GetOrCreateTopLevelObject(
                scene,
                SceneHudRootName,
                useRectTransform: false);

        BattleActionOrderRailUI actionOrder =
            GetOrAddComponent<BattleActionOrderRailUI>(
                GetOrCreateChild(
                    sceneHudRoot.transform,
                    "ActionOrderRailHost",
                    useRectTransform: false));
        actionOrder.EditorAuthorSceneView();
        EditorUtility.SetDirty(actionOrder);

        BattleWorldSlotArrowOverlayUI slotArrow =
            GetOrAddComponent<BattleWorldSlotArrowOverlayUI>(
                GetOrCreateChild(
                    sceneHudRoot.transform,
                    "WorldSlotArrowHost",
                    useRectTransform: false));
        slotArrow.EditorAuthorSceneView();
        EditorUtility.SetDirty(slotArrow);

        GameObject speedHost =
            GetOrCreateChild(
                sceneHudRoot.transform,
                "BattlePlaybackSpeedHUD",
                useRectTransform: true);

        BattlePlaybackSpeedUI playbackSpeed =
            GetOrAddComponent<BattlePlaybackSpeedUI>(speedHost);
        playbackSpeed.EditorAuthorSceneView();
        EditorUtility.SetDirty(playbackSpeed);

        int removedEnemyIntent =
            RemoveEnemyIntentRibbonObjects(scene);

        registry.EditorAssign(
            progression,
            emotionChoice,
            actionOrder,
            slotArrow,
            playbackSpeed,
            momentum);

        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(abyssBattleUi);
        EditorSceneManager.MarkSceneDirty(scene);

        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = abyssBattleUi;

        Debug.Log(
            "[SceneHUD Authoring] 변환 완료. 고정 HUD는 Scene에 저장됩니다. " +
            $"EnemyIntentRibbon Removed={removedEnemyIntent}. " +
            "이제 각 RectTransform 위치/크기를 Inspector에서 자유롭게 수정하세요. " +
            "Play Mode에서는 위치가 다시 덮어써지지 않습니다.");

        EditorUtility.DisplayDialog(
            "Project Abyss UI",
            "Scene-authored HUD 변환이 완료되었습니다.\n\n" +
            "Scene을 저장한 뒤 각 HUD RectTransform을 원하는 위치로 옮기세요.\n" +
            "Play Mode에서 고정 HUD 위치를 스크립트가 재설정하지 않습니다.",
            "확인");
    }


    [MenuItem(MenuRoot + "Add/Repair Action Order Maximize-Minimize Toggle")]
    private static void AddOrRepairActionOrderSizeToggle()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss UI",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError(
                "[SceneHUD Authoring] 활성 Scene을 찾지 못했습니다.");
            return;
        }

        BattleActionOrderRailUI actionOrder =
            FindSceneComponent<BattleActionOrderRailUI>(scene);

        if (actionOrder == null)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss UI",
                "현재 Scene에 BattleActionOrderRailUI가 없습니다. 먼저 Scene-authored HUD 변환을 실행하세요.",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(
            "Add Action Order Maximize-Minimize Toggle");

        actionOrder.EditorAuthorSceneView();
        EditorUtility.SetDirty(actionOrder);
        EditorSceneManager.MarkSceneDirty(scene);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = actionOrder.gameObject;

        Debug.Log(
            "[SceneHUD Authoring] 행동 순서 최대화/최소화 버튼 준비 완료. " +
            "Panel 위치는 변경하지 않았습니다.",
            actionOrder);

        EditorUtility.DisplayDialog(
            "Project Abyss UI",
            "행동 순서 패널에 최대화/최소화 버튼을 추가/복구했습니다.\n" +
            "Scene을 저장하세요. Panel 위치는 변경되지 않습니다.",
            "확인");
    }

    [MenuItem(MenuRoot + "Validate Current Scene-Authored HUD")]
    private static void ValidateCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        BattleSceneHudRegistry registry =
            FindSceneComponent<BattleSceneHudRegistry>(scene);

        if (registry == null)
        {
            Debug.LogError(
                "[SceneHUD Validate] BattleSceneHudRegistry가 없습니다. 변환 메뉴를 먼저 실행하세요.");
            return;
        }

        int missing = 0;
        missing += ValidateReference(
            registry.ProgressionHud,
            nameof(registry.ProgressionHud));
        missing += ValidateReference(
            registry.EmotionAugmentChoiceUi,
            nameof(registry.EmotionAugmentChoiceUi));
        missing += ValidateReference(
            registry.ActionOrderRail,
            nameof(registry.ActionOrderRail));
        missing += ValidateReference(
            registry.WorldSlotArrowOverlay,
            nameof(registry.WorldSlotArrowOverlay));
        missing += ValidateReference(
            registry.PlaybackSpeedUi,
            nameof(registry.PlaybackSpeedUi));
        missing += ValidateReference(
            registry.MomentumScrollbarUi,
            nameof(registry.MomentumScrollbarUi));

        if (missing == 0)
        {
            Debug.Log(
                "[SceneHUD Validate] OK - 고정 전투 HUD Registry 참조가 모두 연결되어 있습니다.",
                registry);
        }
        else
        {
            Debug.LogError(
                $"[SceneHUD Validate] FAILED - 누락 참조 {missing}개. 변환 메뉴를 다시 실행하거나 Inspector에서 연결하세요.",
                registry);
        }
    }

    private static int ValidateReference(
        UnityEngine.Object value,
        string name)
    {
        if (value != null)
            return 0;

        Debug.LogError(
            $"[SceneHUD Validate] Missing: {name}");
        return 1;
    }

    [MenuItem(MenuRoot + "Remove Enemy Intent Ribbon From Current Scene")]
    private static void RemoveEnemyIntentRibbonFromCurrentScene()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss UI",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError(
                "[SceneHUD Authoring] 활성 Scene을 찾지 못했습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(
            "Remove Enemy Intent Ribbon");

        int removed = RemoveEnemyIntentRibbonObjects(scene);

        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[SceneHUD Authoring] EnemyIntentRibbon 제거 완료 / Removed={removed}");

        EditorUtility.DisplayDialog(
            "Project Abyss UI",
            removed > 0
                ? $"'적 행동 · 부위 슬롯' 패널 {removed}개를 Scene에서 제거했습니다.\nScene을 저장하세요."
                : "현재 Scene에 제거할 '적 행동 · 부위 슬롯' 패널이 없습니다.",
            "확인");
    }

    private static int RemoveEnemyIntentRibbonObjects(Scene scene)
    {
        int removed = 0;

        while (true)
        {
            EnemyIntentRibbonUI component =
                FindSceneComponent<EnemyIntentRibbonUI>(scene);

            GameObject target =
                component != null
                    ? component.gameObject
                    : FindSceneGameObjectByName(
                        scene,
                        "EnemyIntentRibbon");

            if (target == null)
                break;

            Undo.DestroyObjectImmediate(target);
            removed++;
        }

        return removed;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
            return existing;

        return Undo.AddComponent<T>(target);
    }

    private static GameObject GetOrCreateTopLevelObject(
        Scene scene,
        string name,
        bool useRectTransform)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        GameObject created = useRectTransform
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);

        Undo.RegisterCreatedObjectUndo(
            created,
            $"Create {name}");
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject GetOrCreateChild(
        Transform parent,
        string name,
        bool useRectTransform)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject created = useRectTransform
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);

        Undo.RegisterCreatedObjectUndo(
            created,
            $"Create {name}");
        created.transform.SetParent(parent, false);
        return created;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component =
                root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static GameObject FindSceneGameObjectByName(
        Scene scene,
        string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(
        Transform root,
        string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
