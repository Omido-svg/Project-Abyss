#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    [CustomEditor(typeof(SecondaryRigPresetLibrary))]
    public sealed class SecondaryRigPresetLibraryEditor : UnityEditor.Editor
    {
        private string sourcePreset = "HAIR";
        private string duplicateName = "LONG_HAIR";

        public override void OnInspectorGUI()
        {
            SecondaryRigPresetLibrary library = (SecondaryRigPresetLibrary)target;

            EditorGUILayout.HelpBox(
                "Preset values are shared by every chain using the same preset name. " +
                "Use Add Missing Recommended Presets when updating the package so existing tuning is preserved.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Library Maintenance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Data Version", library.DataVersion.ToString());
            EditorGUILayout.LabelField("Preset Count", library.PresetCount.ToString());

            if (GUILayout.Button("Add Missing Recommended Presets"))
            {
                Undo.RecordObject(library, "Add Missing Secondary Rig Presets");
                if (library.AddMissingRecommendedPresets())
                    EditorUtility.SetDirty(library);
            }

            if (GUILayout.Button("Validate Preset Library"))
                EditorUtility.DisplayDialog("Secondary Rig Presets", library.ValidateLibrary(), "OK");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Duplicate Preset", EditorStyles.boldLabel);
            sourcePreset = EditorGUILayout.TextField("Source", sourcePreset);
            duplicateName = EditorGUILayout.TextField("New Name", duplicateName);
            if (GUILayout.Button("Duplicate Preset"))
            {
                Undo.RecordObject(library, "Duplicate Secondary Rig Preset");
                if (library.TryDuplicatePreset(sourcePreset, duplicateName))
                {
                    EditorUtility.SetDirty(library);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate Preset",
                        "Source preset was not found, the new name is empty, or that name already exists.",
                        "OK");
                }
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Reset ALL to Recommended Defaults"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset Preset Library",
                        "This overwrites all existing preset tuning. Continue?",
                        "Reset",
                        "Cancel"))
                {
                    Undo.RecordObject(library, "Reset Secondary Rig Presets");
                    library.ResetToRecommendedDefaults();
                    EditorUtility.SetDirty(library);
                }
            }
        }
    }
}
#endif
