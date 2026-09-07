using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character/Combat Loadout",
    fileName = "CharacterCombatLoadout")]
public sealed class CharacterCombatLoadout : ScriptableObject
{
    public const int NormalLimit = 3;
    public const int DuelLimit = 2;
    public const int PreparationLimit = 3;
    public const int PrestigeLimit = 1;

    [Header("Equipped")]
    public List<SkillDefinition> NormalSkills = new();
    public List<SkillDefinition> DuelSkills = new();
    public List<SkillDefinition> PreparationSkills = new();
    public List<SkillDefinition> PrestigeSkills = new();

    [Header("Shared skill catalog")]
    [Tooltip("상점과 저장 시스템이 사용하는 전체 스킬 에셋 목록입니다.")]
    public SkillCatalog Catalog;

    [Tooltip("플레이어처럼 상점에서 전체 카탈로그 스킬을 교체 후보로 사용할 때 활성화합니다.")]
    public bool IncludeCatalogCandidates;

    [Header("Character skill pools")]
    [Tooltip("일반 스킬 교체 후보입니다. 비어 있으면 장착 목록 자체를 후보로 사용합니다.")]
    public List<SkillDefinition> NormalSkillPool = new();

    [Tooltip("결투 스킬 교체 후보입니다. 비어 있으면 장착 목록 자체를 후보로 사용합니다.")]
    public List<SkillDefinition> DuelSkillPool = new();

    [Tooltip("위세 스킬 교체 후보입니다. 비어 있으면 장착 목록 자체를 후보로 사용합니다.")]
    public List<SkillDefinition> PrestigeSkillPool = new();

    [Header("Dosarim pools")]
    [Tooltip("모든 캐릭터가 장착 후보로 사용할 수 있는 공용 도사림입니다.")]
    public List<SkillDefinition> CommonPreparationPool = new();

    [Tooltip("이 캐릭터만 장착 후보로 사용할 수 있는 전용 도사림입니다.")]
    public List<SkillDefinition> CharacterPreparationPool = new();

    public IEnumerable<SkillDefinition> EnumerateEquipped()
    {
        foreach (SkillDefinition definition in Enumerate(NormalSkills))
            yield return definition;

        foreach (SkillDefinition definition in Enumerate(DuelSkills))
            yield return definition;

        foreach (SkillDefinition definition in Enumerate(PreparationSkills))
            yield return definition;

        foreach (SkillDefinition definition in Enumerate(PrestigeSkills))
            yield return definition;
    }

    public IEnumerable<SkillDefinition> EnumeratePreparationPool()
    {
        HashSet<SkillDefinition> visited = new();

        foreach (SkillDefinition definition in Enumerate(CommonPreparationPool))
        {
            if (visited.Add(definition))
                yield return definition;
        }

        foreach (SkillDefinition definition in Enumerate(CharacterPreparationPool))
        {
            if (visited.Add(definition))
                yield return definition;
        }

        if (IncludeCatalogCandidates && Catalog != null)
        {
            foreach (SkillDefinition definition in Catalog.Enumerate(ActionType.Preparation))
            {
                if (visited.Add(definition))
                    yield return definition;
            }
        }

        // 기존 에셋 호환:
        // 풀을 아직 작성하지 않았어도 현재 장착 중인 도사림은 후보로 유지한다.
        foreach (SkillDefinition definition in Enumerate(PreparationSkills))
        {
            if (visited.Add(definition))
                yield return definition;
        }
    }

    public IEnumerable<SkillDefinition> EnumerateAllDefinitions()
    {
        HashSet<SkillDefinition> visited = new();

        foreach (SkillDefinition definition in EnumerateEquipped())
        {
            if (visited.Add(definition))
                yield return definition;
        }

        foreach (SkillDefinition definition in EnumeratePool(ActionType.NormalAttack))
        {
            if (visited.Add(definition))
                yield return definition;
        }

        foreach (SkillDefinition definition in EnumeratePool(ActionType.Duel))
        {
            if (visited.Add(definition))
                yield return definition;
        }

        foreach (SkillDefinition definition in EnumeratePreparationPool())
        {
            if (visited.Add(definition))
                yield return definition;
        }

        foreach (SkillDefinition definition in EnumeratePool(ActionType.Prestige))
        {
            if (visited.Add(definition))
                yield return definition;
        }
    }

    public IEnumerable<SkillDefinition> EnumeratePool(
        ActionType actionType)
    {
        if (actionType == ActionType.Preparation)
        {
            foreach (SkillDefinition definition in EnumeratePreparationPool())
                yield return definition;

            yield break;
        }

        List<SkillDefinition> explicitPool =
            actionType switch
            {
                ActionType.NormalAttack => NormalSkillPool,
                ActionType.Duel => DuelSkillPool,
                ActionType.Prestige => PrestigeSkillPool,
                _ => null
            };

        HashSet<SkillDefinition> visited = new();

        foreach (SkillDefinition definition in Enumerate(explicitPool))
        {
            if (visited.Add(definition))
                yield return definition;
        }

        if (IncludeCatalogCandidates && Catalog != null)
        {
            foreach (SkillDefinition definition in Catalog.Enumerate(actionType))
            {
                if (visited.Add(definition))
                    yield return definition;
            }
        }

        // 구형 에셋 호환: 명시적 풀이 비어 있어도 현재 장착분은 후보다.
        foreach (SkillDefinition definition in Enumerate(
                     GetMutableEquippedList(actionType)))
        {
            if (visited.Add(definition))
                yield return definition;
        }
    }

    public bool IsCandidate(
        SkillDefinition definition)
    {
        if (definition == null)
            return false;

        bool hasExplicitPool =
            definition.ActionType switch
            {
                ActionType.NormalAttack =>
                    NormalSkillPool != null &&
                    NormalSkillPool.Count > 0,

                ActionType.Duel =>
                    DuelSkillPool != null &&
                    DuelSkillPool.Count > 0,

                ActionType.Preparation =>
                    (CommonPreparationPool != null &&
                     CommonPreparationPool.Count > 0) ||
                    (CharacterPreparationPool != null &&
                     CharacterPreparationPool.Count > 0),

                ActionType.Prestige =>
                    PrestigeSkillPool != null &&
                    PrestigeSkillPool.Count > 0,

                _ => false
            };

        bool catalogRestricts =
            IncludeCatalogCandidates &&
            Catalog != null;

        if (!hasExplicitPool && !catalogRestricts)
            return true;

        foreach (SkillDefinition candidate
                 in EnumeratePool(definition.ActionType))
        {
            if (candidate == definition)
                return true;
        }

        return false;
    }

    public bool IsPreparationCandidate(
        SkillDefinition definition)
    {
        return definition != null &&
               definition.ActionType == ActionType.Preparation &&
               IsCandidate(definition);
    }

    public IReadOnlyList<SkillDefinition> GetEquipped(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalSkills,
            ActionType.Duel => DuelSkills,
            ActionType.Preparation => PreparationSkills,
            ActionType.Prestige => PrestigeSkills,
            _ => System.Array.Empty<SkillDefinition>()
        };
    }

    private List<SkillDefinition> GetMutableEquippedList(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalSkills,
            ActionType.Duel => DuelSkills,
            ActionType.Preparation => PreparationSkills,
            ActionType.Prestige => PrestigeSkills,
            _ => null
        };
    }

    public static int GetEquipLimit(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalLimit,
            ActionType.Duel => DuelLimit,
            ActionType.Preparation => PreparationLimit,
            ActionType.Prestige => PrestigeLimit,
            _ => 0
        };
    }

    public List<Skill> CreateRuntimeSkills()
    {
        return CreateRuntimeSkills(
            EnumerateEquipped());
    }

    public List<Skill> CreateAllRuntimeSkills()
    {
        return CreateRuntimeSkills(
            EnumerateAllDefinitions());
    }

    private static List<Skill> CreateRuntimeSkills(
        IEnumerable<SkillDefinition> definitions)
    {
        List<Skill> result = new();

        if (definitions == null)
            return result;

        foreach (SkillDefinition definition in definitions)
        {
            Skill skill = definition?.CreateRuntimeSkill();

            if (skill != null)
                result.Add(skill);
        }

        return result;
    }

    private static IEnumerable<SkillDefinition> Enumerate(
        List<SkillDefinition> definitions)
    {
        if (definitions == null)
            yield break;

        foreach (SkillDefinition definition in definitions)
        {
            if (definition != null)
                yield return definition;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalSkills ??= new List<SkillDefinition>();
        DuelSkills ??= new List<SkillDefinition>();
        PreparationSkills ??= new List<SkillDefinition>();
        PrestigeSkills ??= new List<SkillDefinition>();
        NormalSkillPool ??= new List<SkillDefinition>();
        DuelSkillPool ??= new List<SkillDefinition>();
        PrestigeSkillPool ??= new List<SkillDefinition>();
        CommonPreparationPool ??= new List<SkillDefinition>();
        CharacterPreparationPool ??= new List<SkillDefinition>();

        Trim(NormalSkills, NormalLimit);
        Trim(DuelSkills, DuelLimit);
        Trim(PreparationSkills, PreparationLimit);
        Trim(PrestigeSkills, PrestigeLimit);

        WarnWrongType(NormalSkills, ActionType.NormalAttack, "일반");
        WarnWrongType(DuelSkills, ActionType.Duel, "결투");
        WarnWrongType(PreparationSkills, ActionType.Preparation, "도사림");
        WarnWrongType(PrestigeSkills, ActionType.Prestige, "위세");
        WarnWrongType(NormalSkillPool, ActionType.NormalAttack, "일반 스킬 풀");
        WarnWrongType(DuelSkillPool, ActionType.Duel, "결투 스킬 풀");
        WarnWrongType(PrestigeSkillPool, ActionType.Prestige, "위세 스킬 풀");
        WarnWrongType(CommonPreparationPool, ActionType.Preparation, "공용 도사림 풀");
        WarnWrongType(CharacterPreparationPool, ActionType.Preparation, "전용 도사림 풀");
    }

    private static void Trim(
        List<SkillDefinition> definitions,
        int limit)
    {
        while (definitions.Count > limit)
            definitions.RemoveAt(definitions.Count - 1);
    }

    private void WarnWrongType(
        List<SkillDefinition> definitions,
        ActionType expected,
        string label)
    {
        if (definitions == null)
            return;

        foreach (SkillDefinition definition in definitions)
        {
            if (definition == null ||
                definition.ActionType == expected)
            {
                continue;
            }

            Debug.LogWarning(
                $"[{name}] {label} 목록의 {definition.name}은 " +
                $"ActionType={definition.ActionType}입니다. " +
                $"Expected={expected}",
                this);
        }
    }
#endif
}