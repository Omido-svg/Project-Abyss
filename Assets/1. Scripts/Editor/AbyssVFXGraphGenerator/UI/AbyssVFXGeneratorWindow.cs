#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectAbyss.Editor.VFXAI
{
    internal sealed class AbyssVFXGeneratorWindow : EditorWindow
    {
        private AbyssVFXPreset preset = AbyssVFXPreset.ImpactBurst;
        private AbyssVFXRecipe recipe;
        private string prompt = "검은 검기가 적에게 맞을 때 붉은 파편과 어두운 연무가 짧게 폭발한다.";
        private string recipeJson = string.Empty;
        private string outputFolder = "Assets/VFX/Generated/AI";
        private VisualEffectAsset seedAsset;
        private bool overwrite = true;
        private bool createPrefab = true;
        private bool showAdvanced;
        private bool showSceneObjects;
        private bool showExposedOverrides;
        private bool showJson;
        private Vector2 scroll;
        private AbyssVFXBuildResult lastResult;
        private string statusMessage = "Preset을 선택하거나 AI Recipe JSON을 붙여넣으세요.";
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Project Abyss/VFX AI/AI VFX Graph Generator", priority = 10)]
        private static void Open()
        {
            AbyssVFXGeneratorWindow window = GetWindow<AbyssVFXGeneratorWindow>();
            window.titleContent = new GUIContent("Abyss VFX AI");
            window.minSize = new Vector2(560f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            recipe ??= AbyssVFXPresets.Create(preset);
            recipeJson = AbyssVFXRecipeUtility.ToJson(recipe, true);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeader();
            DrawEnvironment();
            EditorGUILayout.Space(8);
            DrawStep1PromptAndPreset();
            EditorGUILayout.Space(8);
            DrawStep2QuickRecipe();
            EditorGUILayout.Space(8);
            DrawStep3Output();
            EditorGUILayout.Space(8);
            DrawStep4Build();
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(statusMessage, statusType);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Project Abyss AI VFX Graph Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Unity 6000.0.56f1 · URP/VFX Graph 17.0.4 · Recipe 2.0 / Full Template Clone", EditorStyles.miniLabel);
        }

        private void DrawEnvironment()
        {
            AbyssVFXEnvironmentStatus env = AbyssVFXVersionGuard.GetStatus();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("환경", EditorStyles.boldLabel);
                DrawStatusLine("Unity", env.unityVersion, env.isExpectedUnity);
                DrawStatusLine("URP", env.urpVersion + (env.isURPActive ? " · Active" : " · Not Active"), env.isExpectedURP && env.isURPActive);
                DrawStatusLine("VFX Graph", env.vfxVersion, env.isExpectedVFX);
                DrawStatusLine("Compute / Linear", $"{env.supportsCompute} / {env.isLinearColorSpace}", env.supportsCompute && env.isLinearColorSpace);
            }
        }

        private static void DrawStatusLine(string label, string value, bool ok)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(120));
                EditorGUILayout.LabelField(value);
                GUILayout.Label(ok ? "OK" : "CHECK", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            }
        }

        private void DrawStep1PromptAndPreset()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. 효과 설명과 시작 Preset", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("설명은 규칙 기반 Preset 추천과 AI 요청문 생성에 사용합니다.", EditorStyles.wordWrappedMiniLabel);
                prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(64));

                using (new EditorGUILayout.HorizontalScope())
                {
                    preset = (AbyssVFXPreset)EditorGUILayout.EnumPopup("Preset", preset);
                    if (GUILayout.Button("설명에서 추천", GUILayout.Width(100)))
                    {
                        preset = AbyssVFXPresets.GuessFromPrompt(prompt);
                        ApplyPreset();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preset 적용"))
                        ApplyPreset();
                    if (GUILayout.Button("AI 요청문 복사"))
                    {
                        SyncRecipeFromJsonIfPossible(false);
                        EditorGUIUtility.systemCopyBuffer = AbyssVFXRecipeUtility.BuildAIRequest(prompt, recipe);
                        SetStatus("AI 요청문을 클립보드에 복사했습니다. ChatGPT/Codex에 붙여넣고 JSON 응답을 다시 붙여넣으세요.", MessageType.Info);
                    }
                    if (GUILayout.Button("클립보드 JSON 붙여넣기"))
                    {
                        recipeJson = EditorGUIUtility.systemCopyBuffer;
                        SyncRecipeFromJsonIfPossible(true);
                    }
                }
            }
        }

        private void DrawStep2QuickRecipe()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Recipe", EditorStyles.boldLabel);
                if (recipe == null)
                    recipe = AbyssVFXPresets.Create(preset);

                recipe.name = EditorGUILayout.TextField("Effect Name", recipe.name);
                recipe.description = EditorGUILayout.TextField("Description", recipe.description);

                int modeIndex = string.Equals(recipe.buildMode, "CloneTemplate", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                modeIndex = EditorGUILayout.Popup("Build Mode", modeIndex, new[] { "Structured", "CloneTemplate" });
                recipe.buildMode = modeIndex == 1 ? "CloneTemplate" : "Structured";

                if (recipe.buildMode == "CloneTemplate")
                {
                    VisualEffectAsset template = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(recipe.templateAssetPath);
                    template = (VisualEffectAsset)EditorGUILayout.ObjectField("Template VFX", template, typeof(VisualEffectAsset), false);
                    recipe.templateAssetPath = template == null ? string.Empty : AssetDatabase.GetAssetPath(template);
                    recipe.preserveTemplateGraph = EditorGUILayout.Toggle("Preserve Full Graph", recipe.preserveTemplateGraph);
                    EditorGUILayout.HelpBox(
                        "Strip, GPU Event, Trigger Event, Decal, SDF, Collision, Sample Mesh/Skinned Mesh, Multiple Outputs와 모든 Operator 연결을 그대로 복제합니다.",
                        MessageType.Info);
                }

                EditorGUILayout.LabelField($"Systems: {recipe.systems?.Count ?? 0} · Scene Mesh Objects: {recipe.sceneObjects?.Count ?? 0}");

                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "빠른 System 편집", true);
                if (showAdvanced && recipe.buildMode == "Structured" && recipe.systems != null)
                {
                    for (int i = 0; i < recipe.systems.Count; i++)
                        DrawSystem(recipe.systems[i], i);
                }

                showSceneObjects = EditorGUILayout.Foldout(
                    showSceneObjects,
                    "3D Scene Mesh Objects",
                    true);
                if (showSceneObjects)
                    DrawSceneObjects();

                showExposedOverrides = EditorGUILayout.Foldout(
                    showExposedOverrides,
                    "VFX Exposed Property Defaults",
                    true);
                if (showExposedOverrides)
                    DrawExposedOverrides();

                showJson = EditorGUILayout.Foldout(showJson, "Recipe JSON", true);
                if (showJson)
                {
                    recipeJson = EditorGUILayout.TextArea(recipeJson, GUILayout.MinHeight(190));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("UI → JSON"))
                        {
                            AbyssVFXRecipeUtility.Normalize(recipe);
                            recipeJson = AbyssVFXRecipeUtility.ToJson(recipe, true);
                            SetStatus("현재 UI 값을 JSON으로 갱신했습니다.", MessageType.Info);
                        }
                        if (GUILayout.Button("JSON → UI"))
                            SyncRecipeFromJsonIfPossible(true);
                        if (GUILayout.Button("JSON 복사"))
                        {
                            EditorGUIUtility.systemCopyBuffer = recipeJson;
                            SetStatus("Recipe JSON을 클립보드에 복사했습니다.", MessageType.Info);
                        }
                    }
                }
            }
        }

        private void DrawSystem(AbyssVFXSystemRecipe system, int index)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"System {index + 1}", EditorStyles.boldLabel);
                system.name = EditorGUILayout.TextField("Name", system.name);
                system.spawnMode = EditorGUILayout.Popup("Spawn", system.spawnMode == "Rate" ? 1 : 0, new[] { "Burst", "Rate" }) == 1 ? "Rate" : "Burst";
                system.capacity = (uint)Mathf.Max(1, EditorGUILayout.IntField("Capacity", (int)system.capacity));
                if (system.spawnMode == "Burst") system.spawnCount = EditorGUILayout.FloatField("Burst Count", system.spawnCount);
                else system.spawnRate = EditorGUILayout.FloatField("Spawn Rate", system.spawnRate);

                DrawRange("Lifetime", ref system.lifetime);
                DrawRange("Size", ref system.size);
                system.startColor = AbyssColor.From(EditorGUILayout.ColorField("Color", system.startColor.ToColor()));
                system.spawnBoxExtents = AbyssVector3.From(EditorGUILayout.Vector3Field("Spawn Extents", system.spawnBoxExtents.ToVector3()));
                system.velocityMode = EditorGUILayout.TextField("Velocity Mode", system.velocityMode);
                system.direction = AbyssVector3.From(EditorGUILayout.Vector3Field("Direction", system.direction.ToVector3()));
                DrawRange("Speed", ref system.speed);
                system.spread = EditorGUILayout.FloatField("Spread", system.spread);
                system.gravity = AbyssVector3.From(EditorGUILayout.Vector3Field("Gravity", system.gravity.ToVector3()));
                system.drag = EditorGUILayout.FloatField("Drag", system.drag);
                system.blendMode = EditorGUILayout.TextField("Blend Mode", system.blendMode);
                int outputIndex = system.outputMode == "Mesh" ? 1 : 0;
                outputIndex = EditorGUILayout.Popup("Output", outputIndex, new[] { "Quad", "Mesh" });
                system.outputMode = outputIndex == 1 ? "Mesh" : "Quad";

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(system.textureAssetPath);
                texture = (Texture)EditorGUILayout.ObjectField("Texture", texture, typeof(Texture), false);
                system.textureAssetPath = texture == null ? string.Empty : AssetDatabase.GetAssetPath(texture);

                if (system.outputMode == "Mesh")
                {
                    UnityEngine.Object meshAsset = AssetDatabase.LoadMainAssetAtPath(system.meshAssetPath);
                    meshAsset = EditorGUILayout.ObjectField("Mesh / FBX Asset", meshAsset, typeof(UnityEngine.Object), false);
                    system.meshAssetPath = meshAsset == null ? string.Empty : AssetDatabase.GetAssetPath(meshAsset);
                    system.meshSubAssetName = EditorGUILayout.TextField("Mesh Sub-Asset Name", system.meshSubAssetName);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(system.materialAssetPath);
                    material = (Material)EditorGUILayout.ObjectField("Scene Material Metadata", material, typeof(Material), false);
                    system.materialAssetPath = material == null ? string.Empty : AssetDatabase.GetAssetPath(material);

                    UnityEngine.Object shaderGraph =
                        AssetDatabase.LoadMainAssetAtPath(
                            system.shaderGraphAssetPath);
                    shaderGraph = EditorGUILayout.ObjectField(
                        "VFX Shader Graph Asset",
                        shaderGraph,
                        typeof(UnityEngine.Object),
                        false);
                    system.shaderGraphAssetPath =
                        shaderGraph == null
                            ? string.Empty
                            : AssetDatabase.GetAssetPath(shaderGraph);

                    EditorGUILayout.HelpBox(
                        "VFX Mesh Output의 Texture/Material 외형은 Output의 VFX Shader Graph와 Exposed Texture로 제어합니다. 일반 Material은 Scene Mesh Object용입니다.",
                        MessageType.Info);
                }

                system.useFlipbook = EditorGUILayout.Toggle("Flipbook", system.useFlipbook);
                if (system.useFlipbook)
                {
                    system.flipbookColumns = Mathf.Max(1, EditorGUILayout.IntField("Flipbook Columns", system.flipbookColumns));
                    system.flipbookRows = Mathf.Max(1, EditorGUILayout.IntField("Flipbook Rows", system.flipbookRows));
                    system.flipbookBlend = EditorGUILayout.Toggle("Flipbook Blend", system.flipbookBlend);
                }
            }
        }

        private void DrawSceneObjects()
        {
            recipe.sceneObjects ??= new List<AbyssVFXSceneObjectRecipe>();

            for (int i = 0; i < recipe.sceneObjects.Count; i++)
            {
                AbyssVFXSceneObjectRecipe item = recipe.sceneObjects[i];
                if (item == null)
                {
                    item = new AbyssVFXSceneObjectRecipe();
                    recipe.sceneObjects[i] = item;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"Scene Mesh Object {i + 1}",
                            EditorStyles.boldLabel);

                        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        {
                            recipe.sceneObjects.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }

                    item.name = EditorGUILayout.TextField("Name", item.name);

                    UnityEngine.Object meshAsset =
                        AssetDatabase.LoadMainAssetAtPath(
                            item.meshAssetPath);
                    meshAsset = EditorGUILayout.ObjectField(
                        "Mesh / FBX Asset",
                        meshAsset,
                        typeof(UnityEngine.Object),
                        false);
                    item.meshAssetPath =
                        meshAsset == null
                            ? string.Empty
                            : AssetDatabase.GetAssetPath(meshAsset);
                    item.meshSubAssetName = EditorGUILayout.TextField(
                        "Mesh Sub-Asset Name",
                        item.meshSubAssetName);

                    Material material =
                        AssetDatabase.LoadAssetAtPath<Material>(
                            item.materialAssetPath);
                    material = (Material)EditorGUILayout.ObjectField(
                        "Material",
                        material,
                        typeof(Material),
                        false);
                    item.materialAssetPath =
                        material == null
                            ? string.Empty
                            : AssetDatabase.GetAssetPath(material);

                    item.localPosition = AbyssVector3.From(
                        EditorGUILayout.Vector3Field(
                            "Local Position",
                            item.localPosition.ToVector3()));
                    item.localEulerAngles = AbyssVector3.From(
                        EditorGUILayout.Vector3Field(
                            "Local Rotation",
                            item.localEulerAngles.ToVector3()));
                    item.localScale = AbyssVector3.From(
                        EditorGUILayout.Vector3Field(
                            "Local Scale",
                            item.localScale.ToVector3()));
                    item.castShadows = EditorGUILayout.Toggle(
                        "Cast Shadows",
                        item.castShadows);
                    item.receiveShadows = EditorGUILayout.Toggle(
                        "Receive Shadows",
                        item.receiveShadows);
                }
            }

            if (GUILayout.Button("Add Scene Mesh Object"))
                recipe.sceneObjects.Add(new AbyssVFXSceneObjectRecipe());

            EditorGUILayout.HelpBox(
                "석상처럼 고유 3D 오브젝트는 Mesh와 Material을 직접 지정합니다. Material에 텍스처를 미리 연결할 수 있고, 런타임에 바꿀 Texture/Mesh는 아래 Exposed Property로 VFX Graph에 전달할 수 있습니다.",
                MessageType.Info);
        }

        private void DrawExposedOverrides()
        {
            recipe.exposedOverrides ??=
                new List<AbyssVFXExposedOverrideRecipe>();

            string[] types =
            {
                "Float", "Int", "Bool", "Vector3",
                "Vector4", "Color", "Texture", "Mesh"
            };

            for (int i = 0; i < recipe.exposedOverrides.Count; i++)
            {
                AbyssVFXExposedOverrideRecipe item =
                    recipe.exposedOverrides[i];

                if (item == null)
                {
                    item = new AbyssVFXExposedOverrideRecipe();
                    recipe.exposedOverrides[i] = item;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"Override {i + 1}",
                            EditorStyles.boldLabel);

                        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        {
                            recipe.exposedOverrides.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }

                    item.propertyName =
                        EditorGUILayout.TextField(
                            "Property",
                            item.propertyName);

                    int typeIndex =
                        Mathf.Max(0, Array.IndexOf(types, item.valueType));
                    typeIndex = EditorGUILayout.Popup(
                        "Type",
                        typeIndex,
                        types);
                    item.valueType = types[typeIndex];

                    switch (item.valueType)
                    {
                        case "Int":
                            item.intValue = EditorGUILayout.IntField(
                                "Value",
                                item.intValue);
                            break;
                        case "Bool":
                            item.boolValue = EditorGUILayout.Toggle(
                                "Value",
                                item.boolValue);
                            break;
                        case "Vector3":
                            item.vectorValue = AbyssVector3.From(
                                EditorGUILayout.Vector3Field(
                                    "Value",
                                    item.vectorValue.ToVector3()));
                            break;
                        case "Vector4":
                        case "Color":
                            item.colorValue = AbyssColor.From(
                                EditorGUILayout.ColorField(
                                    "Value",
                                    item.colorValue.ToColor()));
                            break;
                        case "Texture":
                        {
                            Texture texture =
                                AssetDatabase.LoadAssetAtPath<Texture>(
                                    item.assetPath);
                            texture = (Texture)EditorGUILayout.ObjectField(
                                "Texture",
                                texture,
                                typeof(Texture),
                                false);
                            item.assetPath =
                                texture == null
                                    ? string.Empty
                                    : AssetDatabase.GetAssetPath(texture);
                            break;
                        }
                        case "Mesh":
                        {
                            UnityEngine.Object meshAsset =
                                AssetDatabase.LoadMainAssetAtPath(
                                    item.assetPath);
                            meshAsset = EditorGUILayout.ObjectField(
                                "Mesh / FBX Asset",
                                meshAsset,
                                typeof(UnityEngine.Object),
                                false);
                            item.assetPath =
                                meshAsset == null
                                    ? string.Empty
                                    : AssetDatabase.GetAssetPath(meshAsset);
                            item.subAssetName =
                                EditorGUILayout.TextField(
                                    "Mesh Sub-Asset Name",
                                    item.subAssetName);
                            break;
                        }
                        default:
                            item.floatValue =
                                EditorGUILayout.FloatField(
                                    "Value",
                                    item.floatValue);
                            break;
                    }
                }
            }

            if (GUILayout.Button("Add Exposed Property Override"))
            {
                recipe.exposedOverrides.Add(
                    new AbyssVFXExposedOverrideRecipe());
            }
        }

        private static void DrawRange(string label, ref AbyssFloatRange range)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                range.min = EditorGUILayout.FloatField(range.min);
                range.max = EditorGUILayout.FloatField(range.max);
            }
        }

        private void DrawStep3Output()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("3. 출력", EditorStyles.boldLabel);
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                seedAsset = (VisualEffectAsset)EditorGUILayout.ObjectField("Seed VFX Asset", seedAsset, typeof(VisualEffectAsset), false);
                EditorGUILayout.LabelField("Seed가 비어 있으면 Package/Sample/Project에서 자동 탐색합니다.", EditorStyles.wordWrappedMiniLabel);
                overwrite = EditorGUILayout.Toggle("Overwrite Same Name", overwrite);
                createPrefab = EditorGUILayout.Toggle("Create Prefab", createPrefab);
            }
        }

        private void DrawStep4Build()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("4. 검증 및 생성", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Recipe 검증", GUILayout.Height(30)))
                        ValidateCurrentRecipe();
                    if (GUILayout.Button("VFX Graph 생성", GUILayout.Height(30)))
                        BuildCurrentRecipe();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("호환성 진단"))
                        AbyssVFXDiagnostics.RunFromMenu();
                    using (new EditorGUI.DisabledScope(lastResult?.asset == null))
                    {
                        if (GUILayout.Button("생성 Graph 열기"))
                            AssetDatabase.OpenAsset(lastResult.asset);
                        if (GUILayout.Button("Project에서 선택"))
                        {
                            Selection.activeObject = lastResult.asset;
                            EditorGUIUtility.PingObject(lastResult.asset);
                        }
                    }
                }
            }
        }

        private void ApplyPreset()
        {
            recipe = AbyssVFXPresets.Create(preset);
            recipeJson = AbyssVFXRecipeUtility.ToJson(recipe, true);
            SetStatus($"{preset} Preset을 적용했습니다.", MessageType.Info);
        }

        private bool SyncRecipeFromJsonIfPossible(bool report)
        {
            if (!AbyssVFXRecipeUtility.TryFromJson(recipeJson, out AbyssVFXRecipe parsed, out string error))
            {
                if (report) SetStatus("JSON 해석 실패: " + error, MessageType.Error);
                return false;
            }
            recipe = parsed;
            recipeJson = AbyssVFXRecipeUtility.ToJson(recipe, true);
            if (report) SetStatus("Recipe JSON을 UI에 반영했습니다.", MessageType.Info);
            return true;
        }

        private bool ValidateCurrentRecipe()
        {
            SyncRecipeFromJsonIfPossible(false);
            AbyssVFXRecipeUtility.Normalize(recipe);
            IReadOnlyList<string> errors = AbyssVFXRecipeUtility.Validate(recipe, out IReadOnlyList<string> warnings);
            if (errors.Count > 0)
            {
                SetStatus("검증 실패\n- " + string.Join("\n- ", errors), MessageType.Error);
                return false;
            }
            SetStatus(warnings.Count == 0 ? "Recipe 검증 성공" : "Recipe 검증 성공\n경고:\n- " + string.Join("\n- ", warnings), warnings.Count == 0 ? MessageType.Info : MessageType.Warning);
            recipeJson = AbyssVFXRecipeUtility.ToJson(recipe, true);
            return true;
        }

        private void BuildCurrentRecipe()
        {
            if (!ValidateCurrentRecipe())
                return;
            try
            {
                lastResult = AbyssVFXGraphBuilder17.Build(recipe, outputFolder, seedAsset, overwrite, createPrefab);
                string warningText = lastResult.warnings.Count > 0 ? "\n경고:\n- " + string.Join("\n- ", lastResult.warnings) : string.Empty;
                SetStatus($"생성 완료\nVFX: {lastResult.assetPath}\nPrefab: {lastResult.prefabPath}\nSeed: {lastResult.seedPath}{warningText}", lastResult.warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
                Selection.activeObject = lastResult.asset;
                EditorGUIUtility.PingObject(lastResult.asset);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("생성 실패: " + ex.Message + "\nConsole의 전체 Stack Trace와 호환성 진단 결과를 확인하세요.", MessageType.Error);
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }
    }
}
#endif