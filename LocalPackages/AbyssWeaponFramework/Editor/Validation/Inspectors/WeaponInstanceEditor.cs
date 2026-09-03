#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    [CustomEditor(typeof(WeaponInstance))]
    public sealed class WeaponInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            WeaponInstance instance = (WeaponInstance)target;

            if (GUILayout.Button("Refresh Grip / Point Cache"))
            {
                instance.RefreshCache();
                EditorUtility.SetDirty(instance);
            }

            if (GUILayout.Button("Validate Weapon Root"))
            {
                bool valid = WeaponSystemValidator.ValidateWeaponPrefab(instance.gameObject, out string report);
                EditorUtility.DisplayDialog(
                    valid ? "Weapon Prefab PASS" : "Weapon Prefab Issues",
                    report,
                    "OK");
            }
        }
    }
}
#endif
