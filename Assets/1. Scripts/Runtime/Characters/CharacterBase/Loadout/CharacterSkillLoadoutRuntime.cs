using System;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject 원본을 변경하지 않고 런타임 장착 상태를 보관한다.
/// 기본 일반 3 / 결투 3 / 도사림 3 / 위세 1 제한과 아이템 기반 상한 확장을 한 곳에서 보장한다.
/// </summary>
public sealed class CharacterSkillLoadoutRuntime
{
    private readonly Character owner;
    private readonly CharacterCombatLoadout source;

    private readonly Dictionary<ActionType, List<SkillDefinition>>
        equippedByType = new();

    public event Action LoadoutChanged;

    public CharacterSkillLoadoutRuntime(
        Character owner,
        CharacterCombatLoadout source)
    {
        this.owner = owner;
        this.source = source;

        Initialize(ActionType.NormalAttack);
        Initialize(ActionType.Duel);
        Initialize(ActionType.Preparation);
        Initialize(ActionType.Prestige);
    }

    public bool HasSource => source != null;
    public SkillCatalog Catalog => source?.Catalog;

    public IReadOnlyList<SkillDefinition> GetEquipped(
        ActionType actionType)
    {
        return equippedByType.TryGetValue(
                actionType,
                out List<SkillDefinition> values)
            ? values
            : Array.Empty<SkillDefinition>();
    }

    public bool IsEquipped(SkillDefinition definition)
    {
        if (definition == null)
            return false;

        return GetMutableList(definition.ActionType)
            .Contains(definition);
    }

    public bool TryEquip(
        SkillDefinition definition,
        out string reason)
    {
        reason = string.Empty;

        if (!ValidateCandidate(definition, out reason))
            return false;

        List<SkillDefinition> values =
            GetMutableList(definition.ActionType);

        if (values.Contains(definition))
            return true;

        int limit = GetEquipLimit(
            definition.ActionType);

        if (values.Count >= limit)
        {
            reason =
                $"{GetCategoryName(definition.ActionType)} 장착 한도 " +
                $"{limit}개를 초과합니다.";
            return false;
        }

        values.Add(definition);
        LoadoutChanged?.Invoke();
        return true;
    }

    public bool TryUnequip(SkillDefinition definition)
    {
        if (definition == null)
            return false;

        List<SkillDefinition> values =
            GetMutableList(definition.ActionType);

        bool removed = values.Remove(definition);
        if (removed)
            LoadoutChanged?.Invoke();

        return removed;
    }

    /// <summary>
    /// 기존 스킬을 먼저 제거하지 않고 같은 슬롯에서 원자적으로 교체한다.
    /// 실패하면 기존 장착 상태는 유지된다.
    /// </summary>
    public bool TryReplace(
        SkillDefinition equipped,
        SkillDefinition replacement,
        out string reason)
    {
        reason = string.Empty;

        if (equipped == null)
        {
            reason = "교체할 기존 스킬이 없습니다.";
            return false;
        }

        if (!ValidateCandidate(replacement, out reason))
            return false;

        if (equipped.ActionType != replacement.ActionType)
        {
            reason = "서로 다른 스킬 카테고리끼리는 교체할 수 없습니다.";
            return false;
        }

        List<SkillDefinition> values =
            GetMutableList(equipped.ActionType);
        int index = values.IndexOf(equipped);

        if (index < 0)
        {
            reason = "기존 스킬이 현재 장착되어 있지 않습니다.";
            return false;
        }

        int duplicateIndex = values.IndexOf(replacement);
        if (duplicateIndex >= 0 && duplicateIndex != index)
        {
            reason = "교체 대상 스킬이 이미 다른 슬롯에 장착되어 있습니다.";
            return false;
        }

        if (ReferenceEquals(equipped, replacement))
            return true;

        values[index] = replacement;
        LoadoutChanged?.Invoke();
        return true;
    }

    public IEnumerable<SkillDefinition> EnumerateCandidates(
        ActionType actionType)
    {
        if (source == null)
            yield break;

        foreach (SkillDefinition definition
                 in source.EnumeratePool(actionType))
        {
            if (definition != null)
                yield return definition;
        }
    }

    public CharacterSkillLoadoutState CaptureState()
    {
        CharacterSkillLoadoutState state = new();
        CaptureIds(ActionType.NormalAttack, state.NormalSkillIds);
        CaptureIds(ActionType.Duel, state.DuelSkillIds);
        CaptureIds(ActionType.Preparation, state.PreparationSkillIds);
        CaptureIds(ActionType.Prestige, state.PrestigeSkillIds);
        return state;
    }

    /// <summary>
    /// 모든 카테고리를 먼저 검증한 뒤 한 번에 적용한다.
    /// 일부만 복원되는 중간 상태를 만들지 않는다.
    /// </summary>
    public bool TryRestoreState(
        CharacterSkillLoadoutState state,
        out string reason)
    {
        reason = string.Empty;

        if (state == null)
        {
            reason = "복원할 장착 상태가 없습니다.";
            return false;
        }

        Dictionary<ActionType, List<SkillDefinition>> staged = new();
        ActionType[] types =
        {
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige
        };

        for (int i = 0; i < types.Length; i++)
        {
            ActionType type = types[i];
            if (!TryResolveStateList(
                    type,
                    state.GetIds(type),
                    out List<SkillDefinition> values,
                    out reason))
            {
                return false;
            }

            staged[type] = values;
        }

        foreach (KeyValuePair<ActionType, List<SkillDefinition>> pair in staged)
        {
            List<SkillDefinition> destination =
                GetMutableList(pair.Key);
            destination.Clear();
            destination.AddRange(pair.Value);
        }

        LoadoutChanged?.Invoke();
        return true;
    }

    private bool TryResolveStateList(
        ActionType actionType,
        IReadOnlyList<string> ids,
        out List<SkillDefinition> result,
        out string reason)
    {
        result = new List<SkillDefinition>();
        reason = string.Empty;

        int count = ids?.Count ?? 0;
        int limit = GetEquipLimit(actionType);
        if (count > limit)
        {
            reason =
                $"{GetCategoryName(actionType)} 저장 데이터가 한도 {limit}개를 초과합니다.";
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            string id = ids[i];
            SkillDefinition definition = ResolveDefinition(id);
            if (definition == null)
            {
                reason = $"스킬 ID '{id}'를 카탈로그에서 찾지 못했습니다.";
                return false;
            }

            if (definition.ActionType != actionType)
            {
                reason = $"스킬 '{definition.SkillName}'의 카테고리가 저장 슬롯과 다릅니다.";
                return false;
            }

            if (!ValidateCandidate(definition, out reason))
                return false;

            if (!result.Contains(definition))
                result.Add(definition);
        }

        return true;
    }

    private SkillDefinition ResolveDefinition(string skillId)
    {
        SkillDefinition fromCatalog =
            source?.Catalog?.FindById(skillId);
        if (fromCatalog != null)
            return fromCatalog;

        if (source == null)
            return null;

        foreach (SkillDefinition definition in source.EnumerateAllDefinitions())
        {
            if (definition != null &&
                string.Equals(
                    definition.SkillId,
                    skillId,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private bool ValidateCandidate(
        SkillDefinition definition,
        out string reason)
    {
        reason = string.Empty;

        if (definition == null)
        {
            reason = "스킬 정의가 없습니다.";
            return false;
        }

        int limit = GetEquipLimit(
            definition.ActionType);
        if (limit <= 0)
        {
            reason = "지원하지 않는 스킬 카테고리입니다.";
            return false;
        }

        if (source != null && !source.IsCandidate(definition))
        {
            reason =
                definition.ActionType == ActionType.Preparation
                    ? "공용/캐릭터 도사림 풀에 없는 스킬입니다."
                    : "해당 캐릭터의 스킬 풀 또는 카탈로그에 없는 스킬입니다.";
            return false;
        }

        return true;
    }

    private void CaptureIds(
        ActionType actionType,
        List<string> destination)
    {
        destination.Clear();
        IReadOnlyList<SkillDefinition> values =
            GetEquipped(actionType);

        for (int i = 0; i < values.Count; i++)
        {
            SkillDefinition definition = values[i];
            if (definition != null)
                destination.Add(definition.SkillId);
        }
    }

    private void Initialize(ActionType actionType)
    {
        List<SkillDefinition> destination =
            GetMutableList(actionType);

        if (source == null)
            return;

        IReadOnlyList<SkillDefinition> values =
            source.GetEquipped(actionType);
        int limit = GetEquipLimit(actionType);

        for (int i = 0; i < values.Count; i++)
        {
            SkillDefinition definition = values[i];
            if (definition == null ||
                definition.ActionType != actionType ||
                destination.Contains(definition))
            {
                continue;
            }

            if (destination.Count >= limit)
                break;

            destination.Add(definition);
        }
    }

    public int GetEquipLimit(ActionType actionType)
    {
        int limit = CharacterCombatLoadout.GetEquipLimit(actionType);

        if (owner?.EquippedItems != null)
        {
            foreach (CharacterItem item in owner.EquippedItems)
            {
                if (item == null)
                    continue;

                limit = item.ModifySkillEquipLimit(owner, actionType, limit);
            }
        }

        return System.Math.Max(0, limit);
    }

    private List<SkillDefinition> GetMutableList(ActionType actionType)
    {
        if (!equippedByType.TryGetValue(
                actionType,
                out List<SkillDefinition> values))
        {
            values = new List<SkillDefinition>();
            equippedByType.Add(actionType, values);
        }

        return values;
    }

    private static string GetCategoryName(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => "일반 공격",
            ActionType.Duel => "결투",
            ActionType.Preparation => "도사림",
            ActionType.Prestige => "위세",
            _ => actionType.ToString()
        };
    }
}
