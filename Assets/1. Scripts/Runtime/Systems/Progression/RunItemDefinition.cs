using UnityEngine;

[CreateAssetMenu(menuName = "Run/Items/Run Item Definition", fileName = "NewRunItem")]
public sealed class RunItemDefinition : ScriptableObject
{
    public string ItemId;
    public string DisplayName;
    public RunItemTier Tier = RunItemTier.Tier1;
    [Tooltip("직업 아이템이면 해당 Character 식별자/이름을 기록합니다. 공용은 비웁니다.")]
    public string CharacterKey;
    [TextArea(2, 6)] public string Description;
    [Tooltip("실제 전투 빌드에 주입할 기존 CharacterItem. 효과 값이 미정이면 비워둘 수 있습니다.")]
    public CharacterItem CombatItem;
}
