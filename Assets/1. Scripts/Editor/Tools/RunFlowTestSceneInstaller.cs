#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RunFlowTestSceneInstaller
{
    public const string ScenePath = "Assets/5. Scenes/Run Flow Test.unity";

    static RunFlowTestSceneInstaller()
    {
        EditorApplication.delayCall += EnsureSceneExistsSilently;
    }

    [MenuItem("Tools/Project Abyss/Run Flow Test/Open or Create Scene")]
    public static void OpenOrCreateScene()
    {
        EnsureSceneExistsSilently();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"[RunFlowTest] Scene 생성 실패: {ScenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Selection.activeGameObject = Object.FindFirstObjectByType<RunFlowTestHarness>()?.gameObject;
    }

    [MenuItem("Tools/Project Abyss/Run Flow Test/Rebuild Scene")]
    public static void RebuildScene()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild Run Flow Test Scene",
                "Run Flow Test.unity를 테스트 Harness 기본 구성으로 다시 만듭니다. 계속할까요?",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

        CreateSceneAsset(overwrite: true);
    }

    private static void EnsureSceneExistsSilently()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;

        CreateSceneAsset(overwrite: false);
    }

    private static void CreateSceneAsset(bool overwrite)
    {
        string directory = Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);

        GameObject harnessObject = new GameObject("Run Flow Test Harness");
        harnessObject.AddComponent<RunFlowTestHarness>();
        SceneManager.MoveGameObjectToScene(harnessObject, scene);

        GameObject noteObject = new GameObject("README - Developer Integration Scene");
        SceneManager.MoveGameObjectToScene(noteObject, scene);

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.Refresh();

        if (saved)
        {
            Debug.Log(
                "[RunFlowTest] Created Assets/5. Scenes/Run Flow Test.unity. " +
                "Open via Tools > Project Abyss > Run Flow Test > Open or Create Scene.");
        }
        else
        {
            Debug.LogError($"[RunFlowTest] Scene 저장 실패: {ScenePath}");
        }
    }
}
#endif
