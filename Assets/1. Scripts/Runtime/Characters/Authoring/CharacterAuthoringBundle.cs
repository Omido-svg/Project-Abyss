using System.Collections.Generic;
using UnityEngine;

public enum CharacterAuthoringKind
{
    Olaf = 0,
    EliteEnemy = 1,
    NormalEnemy = 2,
    Custom = 3
}

/// <summary>
/// 캐릭터 제작에 필요한 여러 SO와 프리팹 참조를 하나의 최상위 에셋으로 묶는다.
/// 내부 에셋은 같은 .asset 파일의 Sub-Asset으로 저장할 수 있으므로 Project 창의 파일 수를
/// 크게 줄이면서도 기존 SkillDefinition / SkillVisualDefinition / VFX / Effect 계약을 그대로 쓴다.
/// </summary>
[CreateAssetMenu(
    menuName = "Character/Authoring/Character Bundle",
    fileName = "NewCharacterBundle")]
public sealed class CharacterAuthoringBundle : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private CharacterAuthoringKind kind =
        CharacterAuthoringKind.Custom;

    [SerializeField]
    private string displayName = "New Character";

    [TextArea(2, 5)]
    [SerializeField]
    private string authoringNotes;

    [Header("Runtime Core")]
    [SerializeField]
    private CharacterData characterData;

    [Tooltip(
        "OlafSkillSet / EliteEnemySkillSet / NormalEnemySkillSet 또는 미래 캐릭터의 SkillSet SO")]
    [SerializeField]
    private ScriptableObject skillSet;

    [Header("Presentation")]
    [SerializeField]
    private Character characterPrefab;

    [SerializeField]
    private SkillVisualProfile visualProfile;

    [SerializeField]
    private RuntimeAnimatorController animatorController;

    [SerializeField]
    private Avatar avatar;

    [SerializeField]
    private bool overrideAnimatorController;

    [SerializeField]
    private bool overrideAvatar;

    [Header("Build Loadout")]
    [SerializeField]
    private bool overrideLoadout;

    [SerializeField]
    private List<CharacterItem> equippedItems = new();

    [SerializeField]
    private List<CharacterAugment> equippedAugments = new();

    [Header("Normal Enemy")]
    [SerializeField]
    private bool overrideNormalEnemySingleHp = true;

    [SerializeField, Min(1)]
    private int normalEnemySingleMaxHp = 50;

    [Header("Elite Enemy")]
    [SerializeField]
    private bool useElitePostureRotation = true;

    [SerializeField]
    private EnemyPostureSettings elitePostureSettings = new();

    [Header("Authoring Contents")]
    [Tooltip(
        "Workbench가 생성한 Skill/Visual/Camera/Effect/VFX Sub-Asset 목록입니다. " +
        "런타임은 기존 직접 참조를 사용하므로 이 목록은 정리·검증용입니다.")]
    [SerializeField]
    private List<Object> includedAssets = new();

    [SerializeField]
    private List<Object> supportingAssets = new();

    public CharacterAuthoringKind Kind => kind;
    public string DisplayName => displayName;
    public string AuthoringNotes => authoringNotes;

    public CharacterData CharacterData => characterData;
    public ScriptableObject SkillSet => skillSet;
    public Character CharacterPrefab => characterPrefab;
    public SkillVisualProfile VisualProfile => visualProfile;

    public RuntimeAnimatorController AnimatorController =>
        animatorController;

    public Avatar Avatar => avatar;
    public bool OverrideAnimatorController => overrideAnimatorController;
    public bool OverrideAvatar => overrideAvatar;

    public bool OverrideLoadout => overrideLoadout;
    public IReadOnlyList<CharacterItem> EquippedItems => equippedItems;
    public IReadOnlyList<CharacterAugment> EquippedAugments => equippedAugments;

    public bool OverrideNormalEnemySingleHp =>
        overrideNormalEnemySingleHp;

    public int NormalEnemySingleMaxHp =>
        Mathf.Max(1, normalEnemySingleMaxHp);

    public bool UseElitePostureRotation =>
        useElitePostureRotation;

    public EnemyPostureSettings ElitePostureSettings =>
        elitePostureSettings;

    public IReadOnlyList<Object> IncludedAssets => includedAssets;
    public IReadOnlyList<Object> SupportingAssets => supportingAssets;

    public void ConfigureCore(
        CharacterAuthoringKind newKind,
        string newDisplayName,
        CharacterData newCharacterData,
        ScriptableObject newSkillSet,
        SkillVisualProfile newVisualProfile)
    {
        kind = newKind;
        displayName = string.IsNullOrWhiteSpace(newDisplayName)
            ? name
            : newDisplayName.Trim();
        characterData = newCharacterData;
        skillSet = newSkillSet;
        visualProfile = newVisualProfile;
    }

    public void ConfigurePrefab(
        Character prefab)
    {
        characterPrefab = prefab;
    }

    public void ConfigureLoadout(
        IReadOnlyList<CharacterItem> items,
        IReadOnlyList<CharacterAugment> augments,
        bool shouldOverride)
    {
        overrideLoadout = shouldOverride;
        equippedItems ??= new List<CharacterItem>();
        equippedAugments ??= new List<CharacterAugment>();
        equippedItems.Clear();
        equippedAugments.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                CharacterItem item = items[i];

                if (item != null &&
                    !equippedItems.Contains(item))
                {
                    equippedItems.Add(item);
                }
            }
        }

        if (augments != null)
        {
            for (int i = 0; i < augments.Count; i++)
            {
                CharacterAugment augment = augments[i];

                if (augment != null &&
                    !equippedAugments.Contains(augment))
                {
                    equippedAugments.Add(augment);
                }
            }
        }
    }

    public void ConfigureNormalEnemy(
        int singleMaxHp,
        bool shouldOverride = true)
    {
        overrideNormalEnemySingleHp = shouldOverride;
        normalEnemySingleMaxHp = Mathf.Max(1, singleMaxHp);
    }

    public void ConfigureEliteEnemy(
        bool usePosture,
        EnemyPostureSettings settings)
    {
        useElitePostureRotation = usePosture;
        elitePostureSettings =
            settings ??
            new EnemyPostureSettings();
        elitePostureSettings.Normalize();
    }

    public void RegisterIncludedAsset(
        Object asset)
    {
        if (asset == null)
            return;

        includedAssets ??= new List<Object>();

        if (!includedAssets.Contains(asset))
            includedAssets.Add(asset);
    }

    public void RegisterSupportingAsset(
        Object asset)
    {
        if (asset == null)
            return;

        supportingAssets ??= new List<Object>();

        if (!supportingAssets.Contains(asset))
            supportingAssets.Add(asset);
    }

    public IEnumerable<SkillDefinition>
        EnumerateSkillDefinitions()
    {
        switch (skillSet)
        {
            case OlafSkillSet olaf:
                if (olaf.NormalAttack != null)
                    yield return olaf.NormalAttack;
                if (olaf.DuelSkill != null)
                    yield return olaf.DuelSkill;
                if (olaf.PreparationSkill != null)
                    yield return olaf.PreparationSkill;
                if (olaf.PrestigeSkill != null)
                    yield return olaf.PrestigeSkill;
                break;

            case EliteEnemySkillSet elite:
                foreach (SkillDefinition definition in elite.Definitions)
                    yield return definition;
                break;

            case NormalEnemySkillSet normal:
                foreach (SkillDefinition definition in normal.Definitions)
                    yield return definition;
                break;
        }
    }

    public bool IsCompatibleWith(
        Character character,
        out string reason)
    {
        reason = null;

        if (character == null)
        {
            reason = "Character가 없습니다.";
            return false;
        }

        if (characterData == null)
        {
            reason = "CharacterData가 없습니다.";
            return false;
        }

        bool compatible = kind switch
        {
            CharacterAuthoringKind.Olaf =>
                character is Olaf && skillSet is OlafSkillSet,

            CharacterAuthoringKind.EliteEnemy =>
                character is EliteEnemy && skillSet is EliteEnemySkillSet,

            CharacterAuthoringKind.NormalEnemy =>
                character is NormalEnemy && skillSet is NormalEnemySkillSet,

            _ =>
                character is ICharacterAuthoringTarget ||
                skillSet == null
        };

        if (compatible)
            return true;

        reason =
            $"Bundle Kind={kind}, Character={character.GetType().Name}, " +
            $"SkillSet={(skillSet == null ? "NULL" : skillSet.GetType().Name)} 조합이 맞지 않습니다.";

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        normalEnemySingleMaxHp =
            Mathf.Max(1, normalEnemySingleMaxHp);

        equippedItems ??= new List<CharacterItem>();
        equippedAugments ??= new List<CharacterAugment>();
        includedAssets ??= new List<Object>();
        supportingAssets ??= new List<Object>();
        elitePostureSettings ??= new EnemyPostureSettings();
        elitePostureSettings.Normalize();
    }
#endif
}
