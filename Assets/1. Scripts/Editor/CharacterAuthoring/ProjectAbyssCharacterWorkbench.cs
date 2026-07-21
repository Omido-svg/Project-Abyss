#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 이전 메뉴 호환용. 새 제작 진입점은 Character Studio다.
/// </summary>
public static class ProjectAbyssCharacterWorkbench
{
    [MenuItem(
        "Tools/Project Abyss/Character Workbench (Legacy)",
        false,
        2011)]
    public static void OpenLegacy()
    {
        ProjectAbyssCharacterStudio.Open();
    }
}
#endif
