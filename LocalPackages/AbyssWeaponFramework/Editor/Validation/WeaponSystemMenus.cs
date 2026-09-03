#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    internal static class WeaponSystemMenus
    {
        [MenuItem("Tools/Abyss Weapon Framework/Validate Selected", priority = 1550)]
        [MenuItem("Tools/Project Abyss/Weapon System/Validate Selected", priority = 1551)]
        private static void ValidateSelected()
        {
            Object selected = Selection.activeObject;

            if (selected is WeaponDefinition definition)
            {
                Show(WeaponSystemValidator.ValidateDefinition(definition, out string report), report);
                return;
            }

            if (Selection.activeGameObject != null)
            {
                WeaponMountController controller =
                    Selection.activeGameObject.GetComponent<WeaponMountController>();
                if (controller != null)
                {
                    Show(WeaponSystemValidator.ValidateController(controller, out string report), report);
                    return;
                }

                WeaponInstance instance =
                    Selection.activeGameObject.GetComponent<WeaponInstance>();
                if (instance != null)
                {
                    Show(WeaponSystemValidator.ValidateWeaponPrefab(instance.gameObject, out string report), report);
                    return;
                }
            }

            EditorUtility.DisplayDialog(
                "Abyss Weapon Framework",
                "Select a WeaponDefinition, WeaponMountController, or WeaponInstance root.",
                "OK");
        }

        private static void Show(bool valid, string report)
        {
            EditorUtility.DisplayDialog(valid ? "Weapon Framework PASS" : "Weapon Framework Issues", report, "OK");
        }
    }
}
#endif
