#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    [CustomEditor(typeof(WeaponMountController))]
    public sealed class WeaponMountControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            WeaponMountController controller = (WeaponMountController)target;

            if (GUILayout.Button("Refresh Sockets"))
            {
                controller.RefreshSockets();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("Validate Setup"))
            {
                bool valid = WeaponSystemValidator.ValidateController(controller, out string report);
                EditorUtility.DisplayDialog(
                    valid ? "Weapon Setup PASS" : "Weapon Setup Issues",
                    report,
                    "OK");
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Sockets", controller.Sockets.Count.ToString());
                EditorGUILayout.LabelField("Equipped", controller.Equipped.Count.ToString());
            }
        }
    }
}
#endif
