#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    [CustomEditor(typeof(WeaponDefinition))]
    public sealed class WeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate Definition + Prefab"))
            {
                WeaponDefinition definition = (WeaponDefinition)target;
                bool valid = WeaponSystemValidator.ValidateDefinition(definition, out string report);
                EditorUtility.DisplayDialog(
                    valid ? "Weapon Definition PASS" : "Weapon Definition Issues",
                    report,
                    "OK");
            }
        }
    }
}
#endif
