#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameplayV5UiMigration
{
    private const string MenuPath =
        "Project Abyss/Gameplay v5/Apply Gameplay v5 UI";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        BattleManager[] managers =
            Object.FindObjectsByType<BattleManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        BattleTestScenarioSwitcher[] switchers =
            Object.FindObjectsByType<BattleTestScenarioSwitcher>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        // Gameplay v5 증강 테스트가 즉시 가능한 최소 데이터 세트를 보장한다.
        // 기존 Catalog가 있으면 그대로 사용하며 경외 프로토타입 9개만 누락분을 보강한다.
        EmotionAugmentCatalog catalog =
            GameplayV5EmotionAugmentMigration
                .EnsurePrototypeAssets(
                    FindFirstCatalog());

        int changed = 0;

        foreach (BattleManager manager in managers)
        {
            if (manager == null)
                continue;

            SerializedObject serialized =
                new SerializedObject(manager);

            SerializedProperty catalogProperty =
                serialized.FindProperty("emotionAugmentCatalog");

            if (catalog != null &&
                catalogProperty != null &&
                catalogProperty.objectReferenceValue == null)
            {
                Undo.RecordObject(
                    manager,
                    "Gameplay v5 UI - Emotion Catalog");
                catalogProperty.objectReferenceValue = catalog;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
                changed++;
            }
        }

        foreach (BattleTestScenarioSwitcher switcher in switchers)
        {
            if (switcher == null)
                continue;

            SerializedObject serialized =
                new SerializedObject(switcher);

            SerializedProperty enabledProperty =
                serialized.FindProperty("configureEmotionForTest");
            SerializedProperty catalogProperty =
                serialized.FindProperty("emotionAugmentCatalog");

            bool dirty = false;

            if (enabledProperty != null &&
                !enabledProperty.boolValue)
            {
                Undo.RecordObject(
                    switcher,
                    "Gameplay v5 UI - Test Emotion");
                enabledProperty.boolValue = true;
                dirty = true;
            }

            if (catalog != null &&
                catalogProperty != null &&
                catalogProperty.objectReferenceValue == null)
            {
                if (!dirty)
                {
                    Undo.RecordObject(
                        switcher,
                        "Gameplay v5 UI - Emotion Catalog");
                }

                catalogProperty.objectReferenceValue = catalog;
                dirty = true;
            }

            if (dirty)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(switcher);
                changed++;
            }
        }

        if (changed > 0)
            EditorSceneManager.MarkAllScenesDirty();

        string catalogState =
            catalog != null
                ? AssetDatabase.GetAssetPath(catalog)
                : "NONE";

        Debug.Log(
            "[Gameplay v5 UI] 적용 완료 / " +
            $"BattleManager={managers.Length}, " +
            $"TestSwitcher={switchers.Length}, " +
            $"Changed={changed}, " +
            $"EmotionCatalog={catalogState}. " +
            "HUD와 증강 선택 Overlay는 런타임에 자동 생성됩니다.");

        if (catalog == null)
        {
            Debug.LogWarning(
                "[Gameplay v5 UI] EmotionAugmentCatalog 에셋을 찾지 못했습니다. " +
                "감정/고조/열광 HUD는 동작하지만 실제 감정 증강 3택은 Catalog를 연결하기 전까지 생성되지 않습니다.");
        }
    }

    private static EmotionAugmentCatalog FindFirstCatalog()
    {
        string guid = AssetDatabase
            .FindAssets("t:EmotionAugmentCatalog")
            .OrderBy(value => value)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(guid))
            return null;

        string path =
            AssetDatabase.GUIDToAssetPath(guid);

        return AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(path);
    }
}
#endif