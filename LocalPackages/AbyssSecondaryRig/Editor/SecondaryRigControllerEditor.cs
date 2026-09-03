#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    [CustomEditor(typeof(SecondaryRigController))]
    public sealed class SecondaryRigControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SecondaryRigController controller = (SecondaryRigController)target;

            EditorGUILayout.HelpBox(
                "Shared tuning should normally be done in the Preset Library. " +
                "Use per-chain Custom Settings only for exceptional strands/panels.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Production Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Production Setup", GUILayout.Height(28f)))
            {
                SecondaryRigValidationResult validation = SecondaryRigPrefabValidator.Validate(controller.gameObject);
                EditorUtility.DisplayDialog(
                    validation.IsValid ? "Secondary Rig Production Validation" : "Secondary Rig Validation Issues",
                    validation.BuildReport(),
                    "OK");
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Bindings"))
            {
                controller.RefreshColliders();
                bool valid = controller.ValidateBindings(out string report);
                EditorUtility.DisplayDialog(
                    valid ? "Secondary Rig Valid" : "Secondary Rig Validation",
                    report,
                    "OK");
            }

            if (GUILayout.Button("Rebuild Runtime"))
                controller.RebuildRuntime();

            if (GUILayout.Button("Reset Simulation"))
                controller.ResetSimulation();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Setup Wizard"))
                SecondaryRigSetupWindow.Open();

            SecondaryRigPresetLibrary library = controller.PresetLibrary;
            using (new EditorGUI.DisabledScope(library == null))
            {
                if (GUILayout.Button("Select Preset Library"))
                {
                    Selection.activeObject = library;
                    EditorGUIUtility.PingObject(library);
                }
            }

            Transform colliderRoot = controller.ColliderRoot;
            using (new EditorGUI.DisabledScope(colliderRoot == null))
            {
                if (GUILayout.Button("Select Colliders"))
                {
                    Selection.activeObject = colliderRoot.gameObject;
                    EditorGUIUtility.PingObject(colliderRoot.gameObject);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Auto Fit All Body Colliders"))
            {
                int fitted = SecondaryRigColliderAutoFitUtility.AutoFitAll(controller.ColliderRoot, out int failed);
                controller.RefreshColliders();
                EditorUtility.DisplayDialog(
                    "Secondary Rig Auto Fit",
                    $"Fitted: {fitted}\nSkipped/Failed: {failed}\n\nAuto Fit uses weighted skinned vertices. Always inspect the result in Scene View.",
                    "OK");
            }

            SecondaryRigStressTest stressTest = controller.GetComponent<SecondaryRigStressTest>();
            if (stressTest == null)
            {
                if (GUILayout.Button("Add Stress Test Component"))
                {
                    stressTest = Undo.AddComponent<SecondaryRigStressTest>(controller.gameObject);
                    stressTest.Controller = controller;
                    stressTest.MotionTarget = controller.transform;
                    EditorUtility.SetDirty(stressTest);
                    Selection.activeObject = stressTest;
                }
            }
            else if (GUILayout.Button("Select Stress Test Component"))
            {
                Selection.activeObject = stressTest;
            }

            if (Application.isPlaying)
            {
                SecondaryRigPerformanceStats stats = controller.PerformanceStats;
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Live Performance", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    $"LOD: {stats.LodLevel} ({stats.LodDistance:0.0}m)\n" +
                    $"Chains / Nodes: {stats.ActiveChains} / {stats.ActiveNodes}\n" +
                    $"Body Colliders / Cross Constraints: {stats.BodyColliders} / {stats.LateralConstraints}\n" +
                    $"Substeps / Iterations: {stats.Substeps} / {stats.ConstraintIterations}\n" +
                    $"Simulation: {stats.LastSimulationMs:0.000} ms  (smoothed {stats.SmoothedSimulationMs:0.000} ms)",
                    MessageType.None);

                Repaint();
            }
        }
    }
}
#endif
