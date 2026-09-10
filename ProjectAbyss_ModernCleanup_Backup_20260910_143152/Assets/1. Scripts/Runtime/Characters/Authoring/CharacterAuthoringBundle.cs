using System.Collections.Generic;
using UnityEngine;

public enum CharacterAuthoringKind
{
    Olaf = 0,
    EliteEnemy = 1,
    NormalEnemy = 2,
    Custom = 3,
    Yujin = 4,
    Hifumi = 5
}

/// <summary>
/// Character Prefab 한 개를 조립하는 최상위 Authoring 에셋.
/// 런타임 스킬 원본은 CharacterCombatLoadout 하나로 통일한다.
/// </summary>
[CreateAssetMenu(menuName = "Character/Authoring/Character Bundle", fileName = "NewCharacterBundle")]
public sealed class CharacterAuthoringBundle : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private CharacterAuthoringKind kind = CharacterAuthoringKind.Custom;
    [SerializeField] private string displayName = "New Character";
    [TextArea(2, 5)] [SerializeField] private string authoringNotes;

    [Header("Runtime Core")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private CharacterCombatLoadout combatLoadout;

    [Header("Presentation")]
    [SerializeField] private Character characterPrefab;
    [SerializeField] private CharacterPresentationProfile presentationProfile;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private Avatar avatar;
    [SerializeField] private bool overrideAnimatorController;
    [SerializeField] private bool overrideAvatar;

    [Header("Default Build / Passive Loadout")]
    [SerializeField] private bool overrideLoadout = true;
    [SerializeField] private List<CharacterItem> equippedItems = new();
    [SerializeField] private List<CharacterAugment> equippedAugments = new();

    [Header("Normal Enemy")]
    [SerializeField] private bool overrideNormalEnemySingleHp = true;
    [SerializeField, Min(1)] private int normalEnemySingleMaxHp = 50;

    [Header("Elite Enemy")]
    [SerializeField] private bool useElitePostureRotation = true;
    [SerializeField] private EnemyPostureSettings elitePostureSettings = new();

    [Header("Authoring Contents")]
    [SerializeField] private List<Object> includedAssets = new();
    [SerializeField] private List<Object> supportingAssets = new();

    public CharacterAuthoringKind Kind => kind;
    public string DisplayName => displayName;
    public string AuthoringNotes => authoringNotes;
    public CharacterData CharacterData => characterData;
    public CharacterCombatLoadout CombatLoadout => combatLoadout != null ? combatLoadout : characterData?.CombatLoadout;
    public Character CharacterPrefab => characterPrefab;
    public CharacterPresentationProfile PresentationProfile => presentationProfile;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public Avatar Avatar => avatar;
    public bool OverrideAnimatorController => overrideAnimatorController;
    public bool OverrideAvatar => overrideAvatar;
    public bool OverrideLoadout => overrideLoadout;
    public IReadOnlyList<CharacterItem> EquippedItems => equippedItems;
    public IReadOnlyList<CharacterAugment> EquippedAugments => equippedAugments;
    public IReadOnlyList<CharacterAugment> EquippedPassives => equippedAugments;
    public bool OverrideNormalEnemySingleHp => overrideNormalEnemySingleHp;
    public int NormalEnemySingleMaxHp => Mathf.Max(1, normalEnemySingleMaxHp);
    public bool UseElitePostureRotation => useElitePostureRotation;
    public EnemyPostureSettings ElitePostureSettings => elitePostureSettings;
    public IReadOnlyList<Object> IncludedAssets => includedAssets;
    public IReadOnlyList<Object> SupportingAssets => supportingAssets;

    public void ConfigureCore(CharacterAuthoringKind newKind, string newDisplayName, CharacterData newCharacterData, CharacterCombatLoadout newCombatLoadout)
    {
        kind = newKind;
        displayName = string.IsNullOrWhiteSpace(newDisplayName) ? name : newDisplayName.Trim();
        characterData = newCharacterData;
        combatLoadout = newCombatLoadout ?? newCharacterData?.CombatLoadout;
        SynchronizeCharacterDataLoadout();
    }

    public void ConfigureCombatLoadout(CharacterCombatLoadout value)
    {
        combatLoadout = value;
        SynchronizeCharacterDataLoadout();
    }

    public void ConfigurePrefab(Character value) => characterPrefab = value;
    public void ConfigurePresentationProfile(CharacterPresentationProfile value) => presentationProfile = value;

    public void ConfigureLoadout(IReadOnlyList<CharacterItem> items, IReadOnlyList<CharacterAugment> augments, bool shouldOverride)
    {
        overrideLoadout = shouldOverride;
        equippedItems ??= new List<CharacterItem>();
        equippedAugments ??= new List<CharacterAugment>();
        equippedItems.Clear();
        equippedAugments.Clear();
        AddUnique(equippedItems, items);
        AddUnique(equippedAugments, augments);
    }

    public void ConfigureNormalEnemy(int hp, bool shouldOverride = true)
    {
        overrideNormalEnemySingleHp = shouldOverride;
        normalEnemySingleMaxHp = Mathf.Max(1, hp);
    }

    public void ConfigureEliteEnemy(bool usePosture, EnemyPostureSettings settings)
    {
        useElitePostureRotation = usePosture;
        elitePostureSettings = settings ?? new EnemyPostureSettings();
        elitePostureSettings.Normalize();
    }

    public void SynchronizeCharacterDataLoadout()
    {
        if (characterData != null && combatLoadout != null)
            characterData.CombatLoadout = combatLoadout;
    }

    public void RegisterIncludedAsset(Object asset) => RegisterUnique(includedAssets, asset);
    public void RegisterSupportingAsset(Object asset) => RegisterUnique(supportingAssets, asset);

    public IEnumerable<SkillDefinition> EnumerateSkillDefinitions()
    {
        CharacterCombatLoadout loadout = CombatLoadout;
        if (loadout == null) yield break;
        HashSet<SkillDefinition> visited = new();
        foreach (SkillDefinition definition in loadout.EnumerateAllDefinitions())
            if (definition != null && visited.Add(definition)) yield return definition;
    }

    public bool IsCompatibleWith(Character character, out string reason)
    {
        reason = null;
        if (character == null) { reason = "Character가 없습니다."; return false; }
        if (characterData == null) { reason = "CharacterData가 없습니다."; return false; }
        if (CombatLoadout == null) { reason = "CharacterCombatLoadout이 없습니다."; return false; }

        bool compatible = kind switch
        {
            CharacterAuthoringKind.Olaf => character is Olaf,
            CharacterAuthoringKind.EliteEnemy => character is EliteEnemy,
            CharacterAuthoringKind.NormalEnemy => character is NormalEnemy,
            CharacterAuthoringKind.Yujin => character is Yujin,
            CharacterAuthoringKind.Hifumi => character is Hifumi,
            _ => character is ICharacterAuthoringTarget
        };
        if (compatible) return true;
        reason = $"Bundle Kind={kind}, Character={character.GetType().Name} 조합 불일치";
        return false;
    }

    private static void AddUnique<T>(ICollection<T> destination, IReadOnlyList<T> source) where T : Object
    {
        if (destination == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            T value = source[i];
            if (value != null && !destination.Contains(value)) destination.Add(value);
        }
    }

    private static void RegisterUnique(ICollection<Object> list, Object value)
    {
        if (list != null && value != null && !list.Contains(value)) list.Add(value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        normalEnemySingleMaxHp = Mathf.Max(1, normalEnemySingleMaxHp);
        equippedItems ??= new List<CharacterItem>();
        equippedAugments ??= new List<CharacterAugment>();
        includedAssets ??= new List<Object>();
        supportingAssets ??= new List<Object>();
        elitePostureSettings ??= new EnemyPostureSettings();
        elitePostureSettings.Normalize();
        if (combatLoadout == null && characterData?.CombatLoadout != null) combatLoadout = characterData.CombatLoadout;
        SynchronizeCharacterDataLoadout();
    }
#endif
}
