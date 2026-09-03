#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    [CustomEditor(typeof(SecondaryRigStressTest))]
    public sealed class SecondaryRigStressTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SecondaryRigStressTest stressTest = (SecondaryRigStressTest)target;

            EditorGUILayout.HelpBox(
                "Secondary Rig QA용 임시 흔들기 도구입니다. 기본 실행 순서는 Animator 이후, " +
                "Collider Follower와 SecondaryRigController 이전입니다. Root 테스트가 가장 안전합니다.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Quick Targets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Use Self / Character Root"))
            {
                Undo.RecordObject(stressTest, "Set Stress Test Target");
                stressTest.MotionTarget = stressTest.transform;
                EditorUtility.SetDirty(stressTest);
            }

            if (GUILayout.Button("Use Humanoid Head"))
            {
                Animator animator = stressTest.GetComponentInChildren<Animator>(true);
                Transform head = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;

                if (head == null)
                {
                    EditorUtility.DisplayDialog(
                        "Humanoid Head Not Found",
                        "이 오브젝트 아래에서 유효한 Humanoid Head Bone을 찾지 못했습니다.",
                        "OK");
                }
                else
                {
                    Undo.RecordObject(stressTest, "Set Stress Test Head Target");
                    stressTest.MotionTarget = head;
                    EditorUtility.SetDirty(stressTest);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Stress Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawPresetButton(stressTest, "Gentle", SecondaryRigStressPreset.Gentle);
            DrawPresetButton(stressTest, "Strong", SecondaryRigStressPreset.Strong);
            DrawPresetButton(stressTest, "Extreme", SecondaryRigStressPreset.Extreme);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime Test", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                if (!stressTest.IsRunning)
                {
                    if (GUILayout.Button("Start Stress Test", GUILayout.Height(28f)))
                        stressTest.BeginTest(true);
                }
                else
                {
                    if (GUILayout.Button("Stop + Restore", GUILayout.Height(28f)))
                        stressTest.StopTest(true);
                }

                if (GUILayout.Button("Capture Baseline", GUILayout.Height(28f)))
                    stressTest.CaptureBaseline();

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode에서 Start Stress Test를 누르세요. Root 기준 Strong부터 시작한 뒤 " +
                    "Collision OFF → Spring 확인 → Collision ON 순서로 테스트하는 것을 권장합니다.",
                    MessageType.None);
            }
        }

        private static void DrawPresetButton(
            SecondaryRigStressTest stressTest,
            string label,
            SecondaryRigStressPreset preset)
        {
            if (!GUILayout.Button(label))
                return;

            Undo.RecordObject(stressTest, "Apply Secondary Rig Stress Preset");
            stressTest.ApplyPreset(preset);
            EditorUtility.SetDirty(stressTest);
        }
    }
}
#endif
