#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BattleParticipantUILayoutSetupTool
{
    private const string MenuPath =
        "Tools/Project Abyss/Battle/" +
        "Apply Right Side Participant Button Layout";

    [MenuItem(MenuPath, priority = 2110)]
    public static void ApplyCurrentSceneLayout()
    {
        BattleParticipantButtonFactory factory =
            Object.FindFirstObjectByType<
                BattleParticipantButtonFactory>(
                    FindObjectsInactive.Include);

        if (factory == null)
        {
            EditorUtility.DisplayDialog(
                "Participant Button Layout",
                "BattleParticipantButtonFactory를 찾지 못했습니다.\n" +
                "먼저 Dynamic Roster 변환 메뉴를 실행하세요.",
                "확인");

            return;
        }

        Undo.RecordObject(
            factory,
            "Apply Participant Button Layout");

        if (factory.PlayerContainer != null)
        {
            Undo.RecordObject(
                factory.PlayerContainer,
                "Move Player Button Container");
        }

        if (factory.EnemyContainer != null)
        {
            Undo.RecordObject(
                factory.EnemyContainer,
                "Move Enemy Button Container");
        }

        factory.ApplyContainerLayoutNow();

        EditorUtility.SetDirty(factory);

        if (factory.PlayerContainer != null)
            EditorUtility.SetDirty(factory.PlayerContainer);

        if (factory.EnemyContainer != null)
            EditorUtility.SetDirty(factory.EnemyContainer);

        EditorSceneManager.MarkSceneDirty(
            factory.gameObject.scene);

        Selection.activeGameObject =
            factory.gameObject;

        EditorUtility.DisplayDialog(
            "Participant Button Layout",
            "적 버튼 컨테이너를 오른쪽 위,\n" +
            "플레이어 버튼 컨테이너를 오른쪽 아래에 배치했습니다.",
            "확인");
    }
}

#endif
