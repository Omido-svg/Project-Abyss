#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    public sealed class WeaponAnchorEditorWindow : EditorWindow
    {
        private GameObject weaponRoot;
        private Transform target;
        private WeaponGripPoint grip;

        [MenuItem("Tools/Abyss Weapon Framework/Anchor & Grip Editor", priority = 1530)]
        public static void Open() => GetWindow<WeaponAnchorEditorWindow>("Weapon Anchors");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Anchor / Grip Alignment", EditorStyles.boldLabel);
            weaponRoot = (GameObject)EditorGUILayout.ObjectField("Weapon Root", weaponRoot, typeof(GameObject), true);
            grip = (WeaponGripPoint)EditorGUILayout.ObjectField("Grip", grip, typeof(WeaponGripPoint), true);
            target = (Transform)EditorGUILayout.ObjectField("Target Transform", target, typeof(Transform), true);
            using (new EditorGUI.DisabledScope(weaponRoot == null || grip == null || target == null))
            {
                if (GUILayout.Button("Align Weapon So Grip Matches Target"))
                {
                    Undo.RecordObject(weaponRoot.transform, "Align Weapon Grip");
                    WeaponTransformUtility.AlignGripToTarget(weaponRoot.transform, grip.transform, target);
                }
                if (GUILayout.Button("Move Grip Marker To Target Pose"))
                {
                    Undo.RecordObject(grip.transform, "Move Grip Marker");
                    grip.transform.position = target.position;
                    grip.transform.rotation = target.rotation;
                }
            }
        }
    }
}
#endif
