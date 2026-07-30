#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectAbyss.Editor.VFXAI
{
    internal sealed class AbyssVFXGeneratorWindow : EditorWindow
    {
        private TextAsset recipeAsset;
        private string recipeJson;
        private string outputFolder = "Assets/VFX/Generated/AI";
        private VisualEffectAsset seedAsset;
        private bool overwrite = true;
        private bool createPrefab = true;
        private Vector2 scroll;
        private MessageType statusType = MessageType.Info;
        private string statusMessage = "JSON Recipe를 선택하거나 붙여넣은 뒤 검증하고 생성하세요.";
        private AbyssVFXBuildResult lastResult;

        [MenuItem("Tools/Project Abyss/VFX AI/AI VFX Graph Generator", priority = 10)]
        private static void Open()
        {
            GetWindow<AbyssVFXGeneratorWindow>("AI VFX Graph Generator");
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(recipeJson))
                recipeJson = AbyssVFXRecipeUtility.ToJson(AbyssVFXRecipeUtility.CreateBlankRecipe(), true);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeader();
            DrawEnvironment();
            DrawRecipeInput();
            DrawOutput();
            DrawActions();
            EditorGUILayout.HelpBox(statusMessage, statusType);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("Project Abyss AI VFX Graph Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("schemaVersion 4.0 JSON → 수식/Noise Operator → 3D Mesh Output .vfx", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "이 창은 특정 VFX 예제나 Preset을 내장하지 않습니다. JSON이 생성 결과의 유일한 설계 원본입니다.\n" +
                "정책: Texture2D, Flipbook, Quad, Billboard, FaceCameraPlane 금지. Mesh Output과 실제 Operator 노드(Add/Multiply/Sin/Noise/Random 등), 3D Force Block, VFX Shader Graph를 사용합니다.",
                MessageType.Info);
        }

        private static void DrawEnvironment()
        {
            AbyssVFXEnvironmentStatus env = AbyssVFXVersionGuard.GetStatus();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("환경", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Unity: {env.unityVersion}");
                EditorGUILayout.LabelField($"URP: {env.urpVersion} · Active={env.isURPActive}");
                EditorGUILayout.LabelField($"VFX Graph: {env.vfxVersion}");
            }
        }

        private void DrawRecipeInput()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("JSON Recipe", EditorStyles.boldLabel);
                recipeAsset = (TextAsset)EditorGUILayout.ObjectField("JSON Asset", recipeAsset, typeof(TextAsset), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(recipeAsset == null))
                    {
                        if (GUILayout.Button("Asset 불러오기"))
                        {
                            recipeJson = recipeAsset.text;
                            SetStatus("JSON Asset을 불러왔습니다.", MessageType.Info);
                        }
                    }

                    if (GUILayout.Button("선택한 JSON 불러오기"))
                    {
                        TextAsset selected = Selection.activeObject as TextAsset;
                        if (selected == null)
                            SetStatus("Project 창에서 JSON TextAsset을 선택하세요.", MessageType.Error);
                        else
                        {
                            recipeAsset = selected;
                            recipeJson = selected.text;
                            SetStatus("선택한 JSON을 불러왔습니다.", MessageType.Info);
                        }
                    }

                    if (GUILayout.Button("클립보드 붙여넣기"))
                        recipeJson = EditorGUIUtility.systemCopyBuffer;
                    if (GUILayout.Button("JSON 복사"))
                        EditorGUIUtility.systemCopyBuffer = recipeJson;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("NodeGraph3D 검증 Recipe로 초기화"))
                        recipeJson = AbyssVFXRecipeUtility.ToJson(AbyssVFXRecipeUtility.CreateBlankRecipe(), true);
                    if (GUILayout.Button("JSON 파일로 저장"))
                        SaveJsonToProject();
                }

                recipeJson = EditorGUILayout.TextArea(recipeJson, GUILayout.MinHeight(420));
            }
        }

        private void DrawOutput()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                seedAsset = (VisualEffectAsset)EditorGUILayout.ObjectField("Seed VFX Asset", seedAsset, typeof(VisualEffectAsset), false);
                overwrite = EditorGUILayout.Toggle("Overwrite Same Name", overwrite);
                createPrefab = EditorGUILayout.Toggle("Create Prefab", createPrefab);
                EditorGUILayout.LabelField("중심 산출물은 항상 .vfx 파일입니다. Prefab은 선택 사항입니다.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("검증 및 생성", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("JSON 검증", GUILayout.Height(34)))
                        ValidateCurrentRecipe(out _);
                    if (GUILayout.Button("3D VFX Graph 생성", GUILayout.Height(34)))
                        BuildCurrentRecipe();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("호환성 진단"))
                        AbyssVFXDiagnostics.RunFromMenu();
                    if (GUILayout.Button("기존 4개 VFX를 3D로 재생성"))
                        RebuildKnownLegacyVFX();
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

        private bool ValidateCurrentRecipe(out AbyssVFXRecipe parsed)
        {
            parsed = null;
            if (!AbyssVFXRecipeUtility.TryFromJson(recipeJson, out parsed, out string parseError))
            {
                SetStatus("JSON 해석 실패: " + parseError, MessageType.Error);
                return false;
            }

            IReadOnlyList<string> errors = AbyssVFXRecipeUtility.Validate(parsed, out IReadOnlyList<string> warnings);
            recipeJson = AbyssVFXRecipeUtility.ToJson(parsed, true);

            if (errors.Count > 0)
            {
                SetStatus("검증 실패\n- " + string.Join("\n- ", errors), MessageType.Error);
                return false;
            }

            SetStatus(
                warnings.Count == 0
                    ? "Recipe 검증 성공: NodeGraph3D / Mesh Output / Texture-Free 정책 통과"
                    : "Recipe 검증 성공\n경고:\n- " + string.Join("\n- ", warnings),
                warnings.Count == 0 ? MessageType.Info : MessageType.Warning);
            return true;
        }

        private void BuildCurrentRecipe()
        {
            if (!ValidateCurrentRecipe(out AbyssVFXRecipe parsed))
                return;

            try
            {
                lastResult = AbyssVFXGraphBuilder17.Build(parsed, outputFolder, seedAsset, overwrite, createPrefab);
                string warningText = lastResult.warnings.Count > 0
                    ? "\n경고:\n- " + string.Join("\n- ", lastResult.warnings)
                    : string.Empty;
                string diagnosticText = lastResult.diagnostics.Count > 0
                    ? "\n검증:\n- " + string.Join("\n- ", lastResult.diagnostics)
                    : string.Empty;
                SetStatus(
                    $"생성 완료\nVFX: {lastResult.assetPath}\nPrefab: {lastResult.prefabPath}\nSeed: {lastResult.seedPath}{warningText}{diagnosticText}",
                    lastResult.warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
                Selection.activeObject = lastResult.asset;
                EditorGUIUtility.PingObject(lastResult.asset);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("생성 실패: " + ex.Message, MessageType.Error);
            }
        }


        private void RebuildKnownLegacyVFX()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "기존 VFX 3D 재생성",
                "BloodHit, BurnTick, Bleeding Status, DarkRed SwordWave를 같은 에셋 이름으로 다시 생성합니다. 기존 .vfx GUID와 Prefab 경로는 유지됩니다. 계속할까요?",
                "재생성",
                "취소");
            if (!confirmed) return;
            try
            {
                string report = AbyssVFXV4Migration.RebuildKnownLegacyAssets(outputFolder, seedAsset, createPrefab);
                SetStatus("Legacy VFX 3D 재생성 완료\n" + report, MessageType.Info);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("Legacy VFX 재생성 실패: " + ex.Message, MessageType.Error);
            }
        }

        private void SaveJsonToProject()
        {
            string absolutePath = EditorUtility.SaveFilePanel(
                "VFX Recipe JSON 저장",
                Application.dataPath,
                "abyss-vfx-recipe.json",
                "json");

            if (string.IsNullOrWhiteSpace(absolutePath))
                return;

            File.WriteAllText(absolutePath, recipeJson ?? string.Empty, new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            SetStatus("JSON을 저장했습니다: " + absolutePath, MessageType.Info);
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
