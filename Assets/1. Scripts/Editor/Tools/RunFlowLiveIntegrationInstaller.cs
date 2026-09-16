#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RunFlowLiveIntegrationInstaller
{
    public const string RegistryAssetPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";

    private const string RunItemCatalogPath =
        "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset";

    private const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private const string RunScenePath =
        "Assets/5. Scenes/Run Flow Test.unity";

    private const string BattleScenePath =
        "Assets/5. Scenes/Battle Test Scene.unity";

    static RunFlowLiveIntegrationInstaller()
    {
        EditorApplication.delayCall += InstallSilently;
    }

    [MenuItem("Tools/Project Abyss/Run Flow Test/Install Live Integration")]
    public static void InstallFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[RunFlowLiveInstaller] Play Mode에서는 설치할 수 없습니다.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ApplyInstallation(logResult: true, saveLoadedDirtyScenes: true);
    }

    [MenuItem("Tools/Project Abyss/Run Flow Test/Validate Live Integration")]
    public static void ValidateFromMenu()
    {
        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryAssetPath);

        bool registryOk =
            registry != null &&
            registry.RunItemCatalog != null &&
            registry.EmotionAugmentCatalog != null &&
            registry.OlafPrefab != null &&
            registry.YujinPrefab != null &&
            registry.HifumiPrefab != null;

        int items = registry?.CountValidRunItems() ?? 0;
        int linked = registry?.CountCombatLinkedRunItems() ?? 0;

        bool runBuild = IsSceneInBuild(RunScenePath);
        bool battleBuild = IsSceneInBuild(BattleScenePath);
        bool runBridge = SceneContainsComponent<RunFlowLiveIntegrationOverlay>(RunScenePath);
        bool battleBridge = SceneContainsComponent<RunFlowLiveBattleBridge>(BattleScenePath);

        string report =
            "[RunFlowLiveInstaller] VALIDATION\n" +
            $"Registry={(registryOk ? "PASS" : "FAIL")}\n" +
            $"RunItems={items}, CombatLinked={linked}\n" +
            $"RunScene Build={(runBuild ? "PASS" : "FAIL")}, Overlay={(runBridge ? "PASS" : "FAIL")}\n" +
            $"BattleScene Build={(battleBuild ? "PASS" : "FAIL")}, Bridge={(battleBridge ? "PASS" : "FAIL")}";

        if (registryOk && items > 0 && linked == items &&
            runBuild && battleBuild && runBridge && battleBridge)
        {
            Debug.Log(report + "\nRESULT=PASS");
        }
        else
        {
            Debug.LogWarning(report + "\nRESULT=NOT READY");
        }
    }

    private static void InstallSilently()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ApplyInstallation(logResult: false, saveLoadedDirtyScenes: false);
    }

    private static void ApplyInstallation(
        bool logResult,
        bool saveLoadedDirtyScenes)
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/ProjectAbyss");

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryAssetPath);

        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<RunFlowLiveContentRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryAssetPath);
        }

        registry.RunItemCatalog =
            AssetDatabase.LoadAssetAtPath<RunItemCatalog>(
                RunItemCatalogPath);

        registry.EmotionAugmentCatalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                EmotionCatalogPath);

        EnsureRunSceneOverlay(saveLoadedDirtyScenes);
        EnsureBattleSceneBridgeAndRegistry(
            registry,
            saveLoadedDirtyScenes);
        EnsureBuildSettings();

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            Debug.Log(
                "[RunFlowLiveInstaller] 설치 완료. " +
                "Run Flow Test에서 전투 노드 진입 후 우측 LIVE 패널의 ENTER LIVE 버튼을 사용하세요.");
        }
    }

    private static void EnsureRunSceneOverlay(bool saveLoadedDirtyScenes)
    {
        WithScene(
            RunScenePath,
            saveLoadedDirtyScenes,
            scene =>
            {
                RunFlowLiveIntegrationOverlay overlay =
                    FindInScene<RunFlowLiveIntegrationOverlay>(scene);

                if (overlay != null)
                    return false;

                GameObject go =
                    new GameObject("Run Flow Live Integration");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<RunFlowLiveIntegrationOverlay>();
                return true;
            });
    }

    private static void EnsureBattleSceneBridgeAndRegistry(
        RunFlowLiveContentRegistry registry,
        bool saveLoadedDirtyScenes)
    {
        WithScene(
            BattleScenePath,
            saveLoadedDirtyScenes,
            scene =>
            {
                bool changed = false;

                BattleTestScenarioSwitcher switcher =
                    FindInScene<BattleTestScenarioSwitcher>(scene);

                if (switcher != null)
                {
                    registry.OlafPrefab = switcher.OlafPrefab;
                    registry.YujinPrefab = switcher.YujinPrefab;
                    registry.HifumiPrefab = switcher.HifumiPrefab;
                    EditorUtility.SetDirty(registry);
                }
                else
                {
                    Debug.LogWarning(
                        "[RunFlowLiveInstaller] Battle Test Scene에서 " +
                        "BattleTestScenarioSwitcher를 찾지 못했습니다.");
                }

                RunFlowLiveBattleBridge bridge =
                    FindInScene<RunFlowLiveBattleBridge>(scene);

                if (bridge == null)
                {
                    GameObject go =
                        new GameObject("Run Flow Live Battle Bridge");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<RunFlowLiveBattleBridge>();
                    changed = true;
                }

                return changed;
            });
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>());

        EnsureBuildScene(scenes, RunScenePath);
        EnsureBuildScene(scenes, BattleScenePath);

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureBuildScene(
        List<EditorBuildSettingsScene> scenes,
        string path)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (!string.Equals(
                    scenes[i].path,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!scenes[i].enabled)
                scenes[i] = new EditorBuildSettingsScene(path, true);
            return;
        }

        scenes.Add(
            new EditorBuildSettingsScene(
                path,
                true));
    }

    private static bool IsSceneInBuild(string path)
    {
        EditorBuildSettingsScene[] scenes =
            EditorBuildSettings.scenes;

        if (scenes == null)
            return false;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled &&
                string.Equals(
                    scenes[i].path,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SceneContainsComponent<T>(string path)
        where T : Component
    {
        bool openedHere = false;
        Scene scene = SceneManager.GetSceneByPath(path);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Additive);
            openedHere = true;
        }

        bool found = FindInScene<T>(scene) != null;

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);

        return found;
    }

    private static void WithScene(
        string path,
        bool saveLoadedDirtyScenes,
        Func<Scene, bool> mutate)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogWarning(
                $"[RunFlowLiveInstaller] Scene 없음: {path}");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere =
            !scene.IsValid() || !scene.isLoaded;

        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Additive);
        }

        bool wasDirty = scene.isDirty;
        bool changed = mutate?.Invoke(scene) == true;

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);

            if (openedHere || !wasDirty || saveLoadedDirtyScenes)
            {
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.LogWarning(
                    $"[RunFlowLiveInstaller] {path}에 bridge를 추가했지만 " +
                    "기존 미저장 변경이 있어 자동 저장하지 않았습니다. Scene을 저장하거나 Install 메뉴를 다시 실행하세요.");
            }
        }

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static T FindInScene<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component =
                roots[i].GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent =
            System.IO.Path.GetDirectoryName(folderPath)?
                .Replace('\\', '/');

        string name =
            System.IO.Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        if (!string.IsNullOrWhiteSpace(parent))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
