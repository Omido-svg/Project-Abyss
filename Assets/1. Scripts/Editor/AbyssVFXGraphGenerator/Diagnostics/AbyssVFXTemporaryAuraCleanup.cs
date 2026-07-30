#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    /// <summary>
    /// Removes temporary aura objects created by versions that used DontSaveInEditor
    /// children inside ordinary scene hierarchies. Such children can trigger Unity's
    /// persistence assertion when a scene or prefab is serialized.
    /// </summary>
    [InitializeOnLoad]
    internal static class AbyssVFXTemporaryAuraCleanup
    {
        private static readonly string[] GeneratedPrefixes =
        {
            "__AbyssShinCurtain_",
            "__AbyssAura_",
            "__AbyssShinAura_"
        };

        static AbyssVFXTemporaryAuraCleanup()
        {
            EditorApplication.delayCall += CleanupAfterReload;
        }

        [MenuItem("Tools/Project Abyss/VFX AI/Cleanup Temporary Aura Objects")]
        private static void CleanupFromMenu()
        {
            int removed = CleanupOpenScenes();
            Debug.Log($"[Project Abyss VFX] 임시 Aura 오브젝트 정리 완료: {removed}개 제거");
        }

        private static void CleanupAfterReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            int removed = CleanupOpenScenes();
            if (removed > 0)
                Debug.Log($"[Project Abyss VFX] 구버전 DontSave Aura 잔여물 {removed}개를 자동 정리했습니다.");
        }

        internal static int CleanupOpenScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return 0;

            Transform[] targets = Resources.FindObjectsOfTypeAll<Transform>()
                .Where(IsGeneratedSceneObject)
                .OrderByDescending(GetHierarchyDepth)
                .ToArray();

            HashSet<Mesh> transientMeshes = new();
            foreach (Transform target in targets)
            {
                if (target == null)
                    continue;

                MeshFilter filter = target.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null && !EditorUtility.IsPersistent(mesh))
                    transientMeshes.Add(mesh);

                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }

            foreach (Mesh mesh in transientMeshes)
            {
                if (mesh != null && !EditorUtility.IsPersistent(mesh))
                    UnityEngine.Object.DestroyImmediate(mesh);
            }

            return targets.Length;
        }

        private static bool IsGeneratedSceneObject(Transform target)
        {
            if (target == null || target.gameObject == null)
                return false;
            if (!target.gameObject.scene.IsValid() || !target.gameObject.scene.isLoaded)
                return false;

            string objectName = target.name ?? string.Empty;
            return GeneratedPrefixes.Any(prefix =>
                objectName.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static int GetHierarchyDepth(Transform target)
        {
            int depth = 0;
            for (Transform current = target; current != null; current = current.parent)
                depth++;
            return depth;
        }
    }
}
#endif
