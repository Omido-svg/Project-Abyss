using UnityEngine;

public enum CharacterBuildSourceType
{
    Item = 0,
    Augment = 1
}

/// <summary>
/// 아이템/증강이 런타임 CombatMechanic을 생성할 때 받는 읽기 전용 컨텍스트.
/// ScriptableObject에는 전투 중 상태를 저장하지 않고,
/// 생성된 CombatMechanic 인스턴스가 상태와 이벤트 구독을 소유한다.
/// </summary>
public readonly struct CharacterBuildMechanicContext
{
    public Character Owner { get; }
    public BattleContext BattleContext { get; }
    public ScriptableObject SourceAsset { get; }
    public CharacterBuildSourceType SourceType { get; }
    public string SourceName { get; }

    public bool IsValid =>
        Owner != null &&
        BattleContext != null &&
        SourceAsset != null;

    private CharacterBuildMechanicContext(
        Character owner,
        BattleContext battleContext,
        ScriptableObject sourceAsset,
        CharacterBuildSourceType sourceType,
        string sourceName)
    {
        Owner = owner;
        BattleContext = battleContext;
        SourceAsset = sourceAsset;
        SourceType = sourceType;
        SourceName = string.IsNullOrWhiteSpace(sourceName)
            ? sourceAsset != null
                ? sourceAsset.name
                : "NULL_SOURCE"
            : sourceName;
    }

    public static CharacterBuildMechanicContext ForItem(
        Character owner,
        CharacterItem item)
    {
        return new CharacterBuildMechanicContext(
            owner,
            owner?.BattleContext,
            item,
            CharacterBuildSourceType.Item,
            item?.ItemName);
    }

    public static CharacterBuildMechanicContext ForAugment(
        Character owner,
        CharacterAugment augment)
    {
        return new CharacterBuildMechanicContext(
            owner,
            owner?.BattleContext,
            augment,
            CharacterBuildSourceType.Augment,
            augment?.AugmentName);
    }
}
