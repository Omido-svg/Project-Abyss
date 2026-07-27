#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectAbyssAutoPlanClashHudHotfixV441
{
    private const string MenuPath =
        "Tools/Project Abyss/" +
        "Apply Auto Plan Toggle + Clash HUD Hotfix v4.4.1 (One Time)";

    [MenuItem(MenuPath)]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Project Abyss v4.4.1",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        Scene scene =
            SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Project Abyss v4.4.1",
                "저장된 전투 Scene을 연 뒤 실행하세요.",
                "확인");
            return;
        }

        try
        {
            BackupScene(scene);

            Transform abyssRoot =
                FindSceneTransform(
                    scene,
                    "AbyssBattleUI");

            if (abyssRoot == null)
            {
                throw new InvalidOperationException(
                    "AbyssBattleUI를 찾지 못했습니다.");
            }

            Transform persistentTopLayer =
                FindDirectChild(
                    abyssRoot,
                    "PersistentTopLayer");

            if (persistentTopLayer == null)
            {
                throw new InvalidOperationException(
                    "PersistentTopLayer를 찾지 못했습니다.");
            }

            persistentTopLayer
                .gameObject
                .SetActive(true);

            BattleResolutionUiController resolutionController =
                abyssRoot.GetComponent<
                    BattleResolutionUiController>();

            if (resolutionController == null)
            {
                throw new InvalidOperationException(
                    "BattleResolutionUiController가 없습니다. " +
                    "v4.3 합 연출 Installer를 먼저 적용하세요.");
            }

            BattleClashRollPresentationUI presentation =
                UnityEngine.Object
                    .FindFirstObjectByType<
                        BattleClashRollPresentationUI>(
                            FindObjectsInactive.Include);

            if (presentation == null)
            {
                throw new InvalidOperationException(
                    "BattleClashRollPresentationUI가 없습니다. " +
                    "v4.3 합 연출 Installer를 먼저 적용하세요.");
            }

            // 새 Runtime 구조로 재생성하면 ClashRollPanel에
            // 검은 배경 Image가 다시 만들어지지 않는다.
            presentation.RebuildHierarchy();

            Transform clashPanel =
                presentation.transform.Find(
                    "ClashRollPanel");

            Image legacyBackground =
                clashPanel != null
                    ? clashPanel.GetComponent<Image>()
                    : null;

            if (legacyBackground != null)
            {
                Undo.DestroyObjectImmediate(
                    legacyBackground);
            }

            BattleAutoPlanButtonPanel autoPlanPanel =
                UnityEngine.Object
                    .FindFirstObjectByType<
                        BattleAutoPlanButtonPanel>(
                            FindObjectsInactive.Include);

            if (autoPlanPanel == null)
            {
                throw new InvalidOperationException(
                    "BattleAutoPlanButtonPanel이 없습니다. " +
                    "v4.4 자동 합 지정 Installer를 먼저 적용하세요.");
            }

            EditorUtility.SetDirty(
                autoPlanPanel);

            EditorUtility.SetDirty(
                presentation);

            EditorUtility.SetDirty(
                resolutionController);

            EditorUtility.SetDirty(
                persistentTopLayer.gameObject);

            EditorSceneManager.MarkSceneDirty(
                scene);

            EditorSceneManager.SaveScene(
                scene);

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh(
                ImportAssetOptions
                    .ForceSynchronousImport);

            Selection.activeGameObject =
                autoPlanPanel.gameObject;

            EditorUtility.DisplayDialog(
                "Project Abyss v4.4.1 완료",
                "수정 사항을 적용했습니다.\n\n" +
                "- 선택된 자동 지정 버튼은 밝게 표시\n" +
                "- 반대 버튼은 어둡게 표시\n" +
                "- 같은 버튼을 다시 누르면 아군 행동 전체 취소\n" +
                "- 다른 모드를 누르면 즉시 모드 전환\n" +
                "- 합 굴림 UI의 검은 배경 제거\n" +
                "- 합 연출 중 PersistentTopLayer/TopStatusBar 유지\n\n" +
                "Scene 저장까지 완료했습니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);

            EditorUtility.DisplayDialog(
                "Project Abyss v4.4.1 실패",
                exception.Message,
                "확인");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApply()
    {
        return
            !EditorApplication
                .isPlayingOrWillChangePlaymode;
    }

    private static Transform FindSceneTransform(
        Scene scene,
        string objectName)
    {
        foreach (GameObject root
                 in scene.GetRootGameObjects())
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(
                    true);

            foreach (Transform transform
                     in transforms)
            {
                if (transform != null &&
                    transform.name == objectName)
                {
                    return transform;
                }
            }
        }

        return null;
    }

    private static Transform FindDirectChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
            return null;

        for (int index = 0;
             index < parent.childCount;
             index++)
        {
            Transform child =
                parent.GetChild(index);

            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static void BackupScene(
        Scene scene)
    {
        string scenePath =
            scene.path;

        string directory =
            Path.GetDirectoryName(
                scenePath)
                ?.Replace(
                    '\\',
                    '/');

        string sceneName =
            Path.GetFileNameWithoutExtension(
                scenePath);

        if (string.IsNullOrWhiteSpace(directory) ||
            string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string backupDirectory =
            directory +
            "/_UI_Backups";

        if (!AssetDatabase.IsValidFolder(
                backupDirectory))
        {
            AssetDatabase.CreateFolder(
                directory,
                "_UI_Backups");
        }

        string timestamp =
            DateTime.Now.ToString(
                "yyyyMMdd_HHmmss");

        string backupPath =
            $"{backupDirectory}/" +
            $"{sceneName}_before_ui_v441_" +
            $"{timestamp}.unity";

        if (!AssetDatabase.CopyAsset(
                scenePath,
                backupPath))
        {
            throw new InvalidOperationException(
                "Scene 백업 생성에 실패했습니다: " +
                backupPath);
        }

        AssetDatabase.ImportAsset(
            backupPath,
            ImportAssetOptions
                .ForceSynchronousImport);
    }
}
#endif
