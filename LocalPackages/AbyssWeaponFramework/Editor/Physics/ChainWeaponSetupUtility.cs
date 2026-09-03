#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    public static class ChainWeaponSetupUtility
    {
        [MenuItem("Tools/Abyss Weapon Framework/Chain/Build Joints From Selected Root", priority = 1560)]
        public static void BuildFromSelected()
        {
            Transform root = Selection.activeTransform;
            if (root == null) { EditorUtility.DisplayDialog("Chain Setup", "Select the chain root Transform.", "OK"); return; }
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Build Chain Weapon Physics");
            Rigidbody previous = null;
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform link = root.GetChild(i);
                Rigidbody body = link.GetComponent<Rigidbody>();
                if (body == null) body = Undo.AddComponent<Rigidbody>(link.gameObject);
                body.mass = Mathf.Max(0.01f, body.mass);
                if (previous == null)
                {
                    body.isKinematic = true;
                }
                else
                {
                    CharacterJoint joint = link.GetComponent<CharacterJoint>();
                    if (joint == null) joint = Undo.AddComponent<CharacterJoint>(link.gameObject);
                    joint.connectedBody = previous;
                    joint.enablePreprocessing = true;
                }
                previous = body;
                count++;
            }
            EditorUtility.DisplayDialog("Chain Setup", $"Configured {count} direct child links. First link is kinematic anchor; each following link connects to the previous Rigidbody.", "OK");
        }
    }
}
#endif
