#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    public sealed class HandPoseEditorWindow : EditorWindow
    {
        private WeaponSkeletonMap skeletonMap;
        private HandPoseDefinition pose;
        private WeaponHand hand = WeaponHand.Right;

        [MenuItem("Tools/Abyss Weapon Framework/Hand Pose Editor", priority = 1520)]
        public static void Open() => GetWindow<HandPoseEditorWindow>("Hand Pose");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Hand Pose Capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Pose the fingers in Scene/Animation mode, then capture their local rotations into a reusable HandPoseDefinition.", MessageType.Info);
            skeletonMap = (WeaponSkeletonMap)EditorGUILayout.ObjectField("Skeleton Map", skeletonMap, typeof(WeaponSkeletonMap), true);
            pose = (HandPoseDefinition)EditorGUILayout.ObjectField("Pose Asset", pose, typeof(HandPoseDefinition), false);
            hand = (WeaponHand)EditorGUILayout.EnumPopup("Hand", hand);
            if (hand == WeaponHand.Any) hand = WeaponHand.Right;

            using (new EditorGUI.DisabledScope(skeletonMap == null))
            {
                if (GUILayout.Button("Create New Pose Asset")) CreatePose();
            }
            using (new EditorGUI.DisabledScope(skeletonMap == null || pose == null))
            {
                if (GUILayout.Button("Capture Current Finger Pose"))
                {
                    Undo.RecordObject(pose, "Capture Hand Pose");
                    int count = pose.EditorCapture(skeletonMap, hand);
                    EditorUtility.SetDirty(pose);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Hand Pose", $"Captured {count} finger bones for {hand}.", "OK");
                }
                if (GUILayout.Button("Apply Pose Preview")) ApplyPreview();
            }
        }

        private void CreatePose()
        {
            string folder = "Assets/AbyssWeaponFramework/HandPoses";
            WeaponSystemSetupWindow.EnsureAssetFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/HandPose_{hand}.asset");
            pose = CreateInstance<HandPoseDefinition>();
            AssetDatabase.CreateAsset(pose, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = pose;
        }

        private void ApplyPreview()
        {
            var data = pose.Get(hand);
            for (int i = 0; i < data.Count; i++)
            {
                Transform bone = skeletonMap.GetBone(data[i].Bone);
                if (bone == null) continue;
                Undo.RecordObject(bone, "Preview Hand Pose");
                bone.localRotation = data[i].LocalRotation;
            }
        }
    }
}
#endif
