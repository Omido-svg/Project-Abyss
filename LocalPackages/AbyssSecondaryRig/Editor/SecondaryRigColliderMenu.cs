#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal static class SecondaryRigColliderMenu
    {
        [MenuItem("GameObject/Project Abyss/Secondary Rig/Sphere Collider", false, 20)]
        private static void AddSphereCollider(MenuCommand command)
        {
            GameObject gameObject = new("SecondarySphereCollider");
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            gameObject.AddComponent<SecondaryRigSphereCollider>();
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Secondary Sphere Collider");
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Capsule Collider", false, 21)]
        private static void AddCapsuleCollider(MenuCommand command)
        {
            GameObject gameObject = new("SecondaryCapsuleCollider");
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            gameObject.AddComponent<SecondaryRigCapsuleCollider>();
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Secondary Capsule Collider");
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Duplicate Selected Collider", false, 30)]
        private static void DuplicateSelectedCollider()
        {
            SecondaryRigCollider collider = FindSelectedCollider();
            if (collider != null)
                SecondaryRigColliderTools.Duplicate(collider);
        }

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Duplicate Selected Collider", true)]
        private static bool ValidateDuplicateSelectedCollider() => FindSelectedCollider() != null;

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Mirror Selected Collider L-R", false, 31)]
        private static void MirrorSelectedCollider()
        {
            SecondaryRigCollider collider = FindSelectedCollider();
            if (collider == null)
                return;

            SecondaryRigColliderTools.MirrorLeftRight(collider, out string message);
            Debug.Log($"[SecondaryRig] {message}", collider);
        }

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Mirror Selected Collider L-R", true)]
        private static bool ValidateMirrorSelectedCollider() => FindSelectedCollider() != null;

        [MenuItem("GameObject/Project Abyss/Secondary Rig/Stress Test", false, 40)]
        private static void AddStressTest(MenuCommand command)
        {
            GameObject gameObject = command.context as GameObject;
            if (gameObject == null)
                gameObject = Selection.activeGameObject;

            if (gameObject == null)
            {
                gameObject = new GameObject("SecondaryRigStressTest");
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Secondary Rig Stress Test");
            }

            SecondaryRigStressTest stressTest = gameObject.GetComponent<SecondaryRigStressTest>();
            if (stressTest == null)
                stressTest = Undo.AddComponent<SecondaryRigStressTest>(gameObject);

            SecondaryRigController controller = gameObject.GetComponent<SecondaryRigController>() ??
                                               gameObject.GetComponentInParent<SecondaryRigController>();
            if (controller != null)
                stressTest.Controller = controller;

            stressTest.MotionTarget = gameObject.transform;
            EditorUtility.SetDirty(stressTest);
            Selection.activeObject = stressTest;
        }

        private static SecondaryRigCollider FindSelectedCollider()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponent<SecondaryRigCollider>() : null;
        }
    }
}
#endif
