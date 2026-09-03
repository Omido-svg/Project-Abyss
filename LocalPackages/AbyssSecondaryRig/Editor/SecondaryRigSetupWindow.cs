#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    public sealed class SecondaryRigSetupWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Project Abyss/Secondary Rig Setup";
        private const string DefaultPresetFolder = "Assets/SecondaryRig";
        private const string DefaultPresetPath = DefaultPresetFolder + "/SecondaryRigPresetLibrary.asset";

        [SerializeField] private GameObject characterRoot;
        [SerializeField] private TextAsset manifestJson;
        [SerializeField] private SecondaryRigPresetLibrary presetLibrary;
        [SerializeField] private bool autoCreateHumanoidColliders = true;
        [SerializeField] private bool replaceGeneratedColliders;
        [SerializeField] private bool preserveExistingCustomChainTuning = true;

        private Vector2 scroll;
        private string analysisText;
        private MessageType analysisType = MessageType.None;

        [MenuItem(MenuPath, false, 2050)]
        public static void Open()
        {
            SecondaryRigSetupWindow window = GetWindow<SecondaryRigSetupWindow>("Secondary Rig Setup");
            window.minSize = new Vector2(560f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            TryUseSelection();
        }

        private void OnSelectionChange()
        {
            TryUseSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Project Abyss Secondary Rig Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Blender Target/Spring JSON을 읽어 TGT/SPR Transform을 자동 연결하고, " +
                "재사용 가능한 SecondaryRigController와 선택적 Humanoid Body Collider를 구성합니다. " +
                "Model FBX 이름(.001 등)은 사용하지 않고 실제 Bone 이름으로 연결합니다.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            characterRoot = (GameObject)EditorGUILayout.ObjectField(
                "Character Root / Prefab",
                characterRoot,
                typeof(GameObject),
                true);

            manifestJson = (TextAsset)EditorGUILayout.ObjectField(
                "Secondary Rig JSON",
                manifestJson,
                typeof(TextAsset),
                false);

            presetLibrary = (SecondaryRigPresetLibrary)EditorGUILayout.ObjectField(
                "Preset Library",
                presetLibrary,
                typeof(SecondaryRigPresetLibrary),
                false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create / Load Recommended Presets"))
                presetLibrary = CreateOrLoadRecommendedPresetLibrary();

            using (new EditorGUI.DisabledScope(presetLibrary == null))
            {
                if (GUILayout.Button("Ping Presets", GUILayout.Width(100f)))
                {
                    Selection.activeObject = presetLibrary;
                    EditorGUIUtility.PingObject(presetLibrary);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);
            autoCreateHumanoidColliders = EditorGUILayout.ToggleLeft(
                "Auto-create recommended Humanoid body colliders",
                autoCreateHumanoidColliders);
            replaceGeneratedColliders = EditorGUILayout.ToggleLeft(
                "Replace existing generated collider set",
                replaceGeneratedColliders);
            preserveExistingCustomChainTuning = EditorGUILayout.ToggleLeft(
                "Preserve existing per-chain custom overrides when rebuilding",
                preserveExistingCustomChainTuning);

            EditorGUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(characterRoot == null || manifestJson == null))
            {
                if (GUILayout.Button("Analyze", GUILayout.Height(30f)))
                    Analyze();

                if (GUILayout.Button("Build / Refresh Secondary Rig", GUILayout.Height(42f)))
                    BuildOrRefresh();

                if (GUILayout.Button("Validate Production Setup", GUILayout.Height(30f)))
                    ValidateProductionSetup();
            }

            if (!string.IsNullOrWhiteSpace(analysisText))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(analysisText, analysisType);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Recommended Workflow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) Character Prefab + JSON 지정\n" +
                "2) Recommended Presets 생성\n" +
                "3) Analyze에서 Missing Bone = 0 확인\n" +
                "4) Build / Refresh\n" +
                "5) Prefab에서 SecondaryRigColliders를 Scene View로 조정\n" +
                "6) Preset Library에서 HAIR / LONG_COAT 물성 튜닝\n" +
                "7) Validate Production Setup으로 Prefab QA\n" +
                "8) Controller에서 Add Stress Test Component → Strong으로 흔들림 QA\n" +
                "9) LOD / Live Performance 확인 후 실제 애니메이션 QA",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void TryUseSelection()
        {
            if (Selection.activeGameObject == null)
                return;

            GameObject selected = Selection.activeGameObject;
            if (selected.GetComponentInChildren<Animator>(true) != null)
                characterRoot = selected;
        }

        private void Analyze()
        {
            try
            {
                SecondaryRigManifest manifest = SecondaryRigManifest.Parse(manifestJson.text);
                analysisText = SecondaryRigManifestUtility.BuildAnalysisSummary(characterRoot, manifest);
                analysisType = analysisText.Contains("Missing bone references: 0")
                    ? MessageType.Info
                    : MessageType.Warning;
            }
            catch (Exception exception)
            {
                analysisText = exception.Message;
                analysisType = MessageType.Error;
            }
        }

        private void ValidateProductionSetup()
        {
            if (characterRoot == null)
                return;

            try
            {
                SecondaryRigValidationResult validation = SecondaryRigPrefabValidator.Validate(characterRoot);
                analysisText = validation.BuildReport();
                analysisType = validation.Errors > 0
                    ? MessageType.Error
                    : validation.Warnings > 0 ? MessageType.Warning : MessageType.Info;
            }
            catch (Exception exception)
            {
                analysisText = exception.ToString();
                analysisType = MessageType.Error;
            }
        }

        private void BuildOrRefresh()
        {
            if (characterRoot == null || manifestJson == null)
                return;

            try
            {
                SecondaryRigManifest manifest = SecondaryRigManifest.Parse(manifestJson.text);
                string assetPath = AssetDatabase.GetAssetPath(characterRoot);
                bool isPrefabAsset = !string.IsNullOrWhiteSpace(assetPath) &&
                                     PrefabUtility.IsPartOfPrefabAsset(characterRoot);

                if (isPrefabAsset)
                {
                    GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        BuildOnObject(contents, manifest);
                        PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
                else
                {
                    Undo.RegisterFullObjectHierarchyUndo(characterRoot, "Build Secondary Rig");
                    BuildOnObject(characterRoot, manifest);
                    EditorUtility.SetDirty(characterRoot);
                    if (PrefabUtility.IsPartOfPrefabInstance(characterRoot))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(characterRoot);
                }

                AssetDatabase.SaveAssets();
                analysisText = "Secondary Rig build completed. Re-open/refresh the prefab and tune the generated collider proxies in Scene View.";
                analysisType = MessageType.Info;
            }
            catch (Exception exception)
            {
                analysisText = exception.ToString();
                analysisType = MessageType.Error;
                Debug.LogException(exception);
            }
        }

        private void BuildOnObject(GameObject root, SecondaryRigManifest manifest)
        {
            SecondaryRigController controller = root.GetComponent<SecondaryRigController>();
            if (controller == null)
                controller = root.AddComponent<SecondaryRigController>();

            Dictionary<string, SecondaryRigChainBinding> previousBySpringRoot = new(StringComparer.Ordinal);
            if (preserveExistingCustomChainTuning && controller.Chains != null)
            {
                for (int i = 0; i < controller.Chains.Count; i++)
                {
                    SecondaryRigChainBinding previous = controller.Chains[i];
                    if (previous?.SpringRoot != null)
                        previousBySpringRoot[previous.SpringRoot.name] = previous;
                }
            }

            List<NormalizedManifestChain> normalized = SecondaryRigManifestUtility.Normalize(manifest, out int duplicateCount);
            Dictionary<string, List<Transform>> lookup = SecondaryRigManifestUtility.BuildNameLookup(root.transform);
            List<SecondaryRigChainBinding> bindings = new();
            List<string> missing = new();
            List<string> ambiguous = new();

            for (int i = 0; i < normalized.Count; i++)
            {
                NormalizedManifestChain source = normalized[i];
                Transform[] targetBones = ResolveBones(lookup, source.Chain.targetBones, missing, ambiguous);
                Transform[] springBones = ResolveBones(lookup, source.Chain.springBones, missing, ambiguous);

                if (HasNull(targetBones) || HasNull(springBones))
                    continue;

                SecondaryRigChainBinding binding = new()
                {
                    Enabled = true,
                    PartName = source.PartName,
                    RegionName = source.RegionName,
                    PresetName = source.PresetName,
                    TargetRoot = targetBones[0],
                    SpringRoot = springBones[0],
                    TargetBones = targetBones,
                    SpringBones = springBones,
                    ManifestDefaults = source.Defaults?.Clone() ?? new SecondaryRigManifestSpringDefaults()
                };

                if (preserveExistingCustomChainTuning &&
                    previousBySpringRoot.TryGetValue(binding.SpringRoot.name, out SecondaryRigChainBinding previous))
                {
                    binding.Enabled = previous.Enabled;
                    binding.UseCustomSettings = previous.UseCustomSettings;
                    binding.CustomSettings = previous.CustomSettings?.Clone() ?? new SecondaryRigSettings();
                }

                bindings.Add(binding);
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Secondary Rig build aborted because bone references are missing:\n" +
                    string.Join("\n", missing));
            }

            if (ambiguous.Count > 0)
            {
                throw new InvalidOperationException(
                    "Secondary Rig build aborted because transform names are ambiguous. " +
                    "Bone names must be unique under the selected character root:\n" +
                    string.Join("\n", ambiguous));
            }

            if (presetLibrary == null)
                presetLibrary = CreateOrLoadRecommendedPresetLibrary();

            controller.EditorReplaceBindings(bindings, presetLibrary, root.transform);

            int colliderCreated = 0;
            if (autoCreateHumanoidColliders)
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    colliderCreated = SecondaryRigColliderBuilder.CreateRecommendedHumanoidColliders(root, replaceGeneratedColliders);
                    Transform generatedColliderRoot = root.transform.Find("SecondaryRigColliders");
                    if (generatedColliderRoot != null)
                        controller.ColliderRoot = generatedColliderRoot;
                }
            }

            controller.RefreshColliders();
            EditorUtility.SetDirty(controller);

            Debug.Log(
                $"[SecondaryRig] Build complete / Root={root.name}, Chains={bindings.Count}, " +
                $"Ignored duplicate records={duplicateCount}, New colliders={colliderCreated}, Ambiguous names={ambiguous.Count}",
                controller);
        }

        private static Transform[] ResolveBones(
            Dictionary<string, List<Transform>> lookup,
            List<string> names,
            List<string> missing,
            List<string> ambiguous)
        {
            if (names == null)
                return Array.Empty<Transform>();

            Transform[] result = new Transform[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                Transform transform = SecondaryRigManifestUtility.ResolveUnique(lookup, name, out bool isAmbiguous);
                result[i] = transform;

                if (transform == null)
                    missing.Add(name);
                else if (isAmbiguous)
                    ambiguous.Add(name);
            }

            return result;
        }

        private static bool HasNull(Transform[] values)
        {
            if (values == null || values.Length == 0)
                return true;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                    return true;
            }

            return false;
        }

        private static SecondaryRigPresetLibrary CreateOrLoadRecommendedPresetLibrary()
        {
            SecondaryRigPresetLibrary existing = AssetDatabase.LoadAssetAtPath<SecondaryRigPresetLibrary>(DefaultPresetPath);
            if (existing != null)
            {
                if (existing.AddMissingRecommendedPresets())
                {
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(DefaultPresetFolder))
                AssetDatabase.CreateFolder("Assets", "SecondaryRig");

            SecondaryRigPresetLibrary library = CreateInstance<SecondaryRigPresetLibrary>();
            library.ResetToRecommendedDefaults();
            bool defaultPathOccupied = AssetDatabase.LoadMainAssetAtPath(DefaultPresetPath) != null ||
                                       System.IO.File.Exists(DefaultPresetPath);
            string createPath = !defaultPathOccupied
                ? DefaultPresetPath
                : AssetDatabase.GenerateUniqueAssetPath(DefaultPresetFolder + "/SecondaryRigPresetLibrary_v2.asset");
            AssetDatabase.CreateAsset(library, createPath);
            AssetDatabase.SaveAssets();
            return library;
        }
    }
}
#endif
