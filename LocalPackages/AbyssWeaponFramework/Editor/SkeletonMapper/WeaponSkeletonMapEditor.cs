#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    [CustomEditor(typeof(WeaponSkeletonMap))]
    public sealed class WeaponSkeletonMapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WeaponSkeletonMap map = (WeaponSkeletonMap)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(map.Animator == null || !map.Animator.isHuman))
            {
                if (GUILayout.Button("Auto Map Humanoid"))
                {
                    Undo.RecordObject(map, "Auto Map Humanoid Weapon Skeleton");
                    map.AutoMapHumanoid();
                    EditorUtility.SetDirty(map);
                }
            }
            if (GUILayout.Button("Validate Mapping"))
            {
                List<string> missing = new();
                WeaponBoneId[] required = { WeaponBoneId.LeftUpperArm, WeaponBoneId.LeftLowerArm, WeaponBoneId.LeftHand, WeaponBoneId.RightUpperArm, WeaponBoneId.RightLowerArm, WeaponBoneId.RightHand };
                for (int i = 0; i < required.Length; i++) if (map.GetBone(required[i]) == null) missing.Add(required[i].ToString());
                EditorUtility.DisplayDialog("Skeleton Mapping", missing.Count == 0 ? "PASS: both arms/hands are mapped." : "Missing:\n- " + string.Join("\n- ", missing), "OK");
            }
        }
    }
}
#endif
