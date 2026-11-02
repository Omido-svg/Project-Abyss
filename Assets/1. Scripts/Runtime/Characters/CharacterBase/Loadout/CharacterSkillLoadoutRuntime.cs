using System;
using System.Collections.Generic;
using System.Linq;

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
    private readonly HashSet<string> ownedSkillIds =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> ownedCollisionGuids =
        new(StringComparer.OrdinalIgnoreCase);
    private bool prestigeLockedForRun;

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

        foreach (ActionType type in new[] { ActionType.NormalAttack, ActionType.Duel, ActionType.Preparation, ActionType.Prestige })
        {
            foreach (SkillDefinition definition in GetEquipped(type))
            {
                if (definition != null && type != ActionType.Prestige)
                {
                    ownedSkillIds.Add(definition.SkillId);
                    string guid=R7SkillIdentityContract20261102.GuidFor(definition);
                    if(!string.IsNullOrEmpty(guid))
                        ownedCollisionGuids.Add(guid);
                }
            }
        }
        prestigeLockedForRun = GetEquipped(ActionType.Prestige).Count > 0;
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

    public bool Owns(SkillDefinition definition)
    {
        if(definition==null||!ownedSkillIds.Contains(definition.SkillId))
            return false;
        if(!R7SkillIdentityContract20261102.IsDuplicateId(definition.SkillId))
            return true;
        string guid=R7SkillIdentityContract20261102.GuidFor(definition);
        return !string.IsNullOrEmpty(guid) && ownedCollisionGuids.Contains(guid);
    }

    public bool TryAcquire(SkillDefinition definition, out string reason)
    {
        reason = string.Empty;
        if (definition == null)
        {
            reason = "획득할 스킬 정의가 없습니다.";
            return false;
        }
        if (definition.ActionType == ActionType.Prestige)
        {
            reason = "위세는 수집 대상이 아닙니다. 런 시작 3형 선택으로만 지정합니다.";
            return false;
        }
        if (!ValidateCandidate(definition, out reason))
            return false;
        var collisionGuid=R7SkillIdentityContract20261102.GuidFor(definition);
        if(R7SkillIdentityContract20261102.IsDuplicateId(definition.SkillId) &&
           string.IsNullOrWhiteSpace(collisionGuid))
        {
            reason="등록되지 않은 중복 SkillId GUID입니다.";
            return false;
        }
        ownedSkillIds.Add(definition.SkillId);
        if(!string.IsNullOrEmpty(collisionGuid))
            ownedCollisionGuids.Add(collisionGuid);
        return true;
    }

    public bool TryChoosePrestigeForRun(SkillDefinition definition, out string reason)
    {
        reason = string.Empty;
        if (definition == null || definition.ActionType != ActionType.Prestige)
        {
            reason = "위세 정의가 올바르지 않습니다.";
            return false;
        }
        if (prestigeLockedForRun)
        {
            IReadOnlyList<SkillDefinition> current = GetEquipped(ActionType.Prestige);
            if (current.Count == 1 && ReferenceEquals(current[0], definition))
                return true;
            reason = "위세는 런 시작 선택 후 해당 런에서 교체할 수 없습니다.";
            return false;
        }
        if (!ValidateCandidate(definition, out reason))
            return false;
        List<SkillDefinition> values = GetMutableList(ActionType.Prestige);
        values.Clear();
        values.Add(definition);
        prestigeLockedForRun = true;
        LoadoutChanged?.Invoke();
        return true;
    }

    public bool TryReplaceInMaintenance(
        SkillDefinition equipped,
        SkillDefinition replacement,
        out string reason)
    {
        if (equipped?.ActionType == ActionType.Prestige || replacement?.ActionType == ActionType.Prestige)
        {
            reason = "위세는 정비 교체 대상이 아닙니다.";
            return false;
        }
        if (!Owns(replacement))
        {
            reason = "보유하지 않은 스킬은 장착할 수 없습니다.";
            return false;
        }
        return TryReplace(equipped, replacement, out reason);
    }

    public bool TryEquip(
        SkillDefinition definition,
        out string reason)
    {
        reason = string.Empty;

        if (!ValidateCandidate(definition, out reason))
            return false;

        if (definition.ActionType == ActionType.Prestige)
        {
            reason = "위세는 TryChoosePrestigeForRun으로만 선택할 수 있습니다.";
            return false;
        }

        if (!Owns(definition))
        {
            reason = "보유하지 않은 스킬은 장착할 수 없습니다.";
            return false;
        }

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

        if (equipped.ActionType == ActionType.Prestige || replacement.ActionType == ActionType.Prestige)
        {
            reason = "위세는 런 내 교체 대상이 아닙니다.";
            return false;
        }

        if (!Owns(replacement))
        {
            reason = "보유하지 않은 스킬은 장착할 수 없습니다.";
            return false;
        }

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
        CaptureIds(ActionType.NormalAttack,state.NormalSkillIds,state.NormalSkillGuids);
        CaptureIds(ActionType.Duel,state.DuelSkillIds,state.DuelSkillGuids);
        CaptureIds(ActionType.Preparation,state.PreparationSkillIds,state.PreparationSkillGuids);
        CaptureIds(ActionType.Prestige,state.PrestigeSkillIds,state.PrestigeSkillGuids);
        state.OwnedSkillIds.Clear();
        foreach(string id in ownedSkillIds.OrderBy(x=>x,StringComparer.Ordinal))
            state.OwnedSkillIds.Add(id);
        state.OwnedSkillGuids.Clear();
        foreach(string guid in ownedCollisionGuids.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
            state.OwnedSkillGuids.Add(guid);
        return state;
    }

    /// <summary>
    /// 모든 카테고리를 먼저 검증한 뒤 한 번에 적용한다.
    /// 일부만 복원되는 중간 상태를 만들지 않는다.
    /// </summary>
    public bool TryRestoreState(CharacterSkillLoadoutState state,out string reason)
    {
        reason=string.Empty;
        if(state==null){reason="복원할 장착 상태가 없습니다.";return false;}
        var staged=new Dictionary<ActionType,List<SkillDefinition>>();
        var types=new[]{ActionType.NormalAttack,ActionType.Duel,
            ActionType.Preparation,ActionType.Prestige};
        foreach(var type in types)
        {
            if(!TryResolveStateList(type,state.GetIds(type),state.GetGuids(type),
                out var values,out reason))
                return false;
            staged[type]=values;
        }

        var available=GetResolutionCandidates();
        var ownedIds=new HashSet<string>(StringComparer.Ordinal);
        if(state.OwnedSkillIds!=null)
            foreach(var id in state.OwnedSkillIds)
                if(!string.IsNullOrWhiteSpace(id))ownedIds.Add(id);
        var ownedGuids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(state.OwnedSkillGuids!=null)
        {
            foreach(var guid in state.OwnedSkillGuids)
            {
                if(string.IsNullOrWhiteSpace(guid))continue;
                var matches=available.Where(x=>x!=null &&
                    ownedIds.Contains(x.SkillId) &&
                    R7SkillIdentityContract20261102.IsDuplicateId(x.SkillId) &&
                    string.Equals(R7SkillIdentityContract20261102.GuidFor(x),
                        guid,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(matches.Length!=1)
                {reason="저장된 소유 스킬 GUID를 후보에서 찾을 수 없습니다: "+guid;return false;}
                ownedGuids.Add(guid);
            }
        }
        foreach(var id in ownedIds)
        {
            if(!R7SkillIdentityContract20261102.IsDuplicateId(id) ||
               available.Any(x=>x!=null&&x.SkillId==id &&
                   ownedGuids.Contains(R7SkillIdentityContract20261102.GuidFor(x))))
                continue;
            // Old save: ID-only. Select the unique local candidate or the
            // explicitly recorded PhaseE default, never collection order.
            if(!R7SkillIdentityContract20261102.TryResolve(id,"",available,
                out var selected,out reason))
                return false;
            ownedGuids.Add(R7SkillIdentityContract20261102.GuidFor(selected));
        }
        foreach(var pair in staged)
        {
            if(pair.Key==ActionType.Prestige)continue;
            foreach(var d in pair.Value)
            {
                ownedIds.Add(d.SkillId);
                var guid=R7SkillIdentityContract20261102.GuidFor(d);
                if(R7SkillIdentityContract20261102.IsDuplicateId(d.SkillId) &&
                   string.IsNullOrEmpty(guid))
                {reason="장착 스킬 GUID 레지스트리가 누락됐습니다: "+d.name;return false;}
                if(!string.IsNullOrEmpty(guid))ownedGuids.Add(guid);
            }
        }
        // Transactional commit. No partially restored loadout on failures.
        foreach(var pair in staged)
        {
            var dest=GetMutableList(pair.Key);
            dest.Clear();
            dest.AddRange(pair.Value);
        }
        ownedSkillIds.Clear();
        ownedSkillIds.UnionWith(ownedIds);
        ownedCollisionGuids.Clear();
        ownedCollisionGuids.UnionWith(ownedGuids);
        prestigeLockedForRun=GetEquipped(ActionType.Prestige).Count>0;
        LoadoutChanged?.Invoke();
        return true;
    }

    private bool TryResolveStateList(ActionType actionType,
        IReadOnlyList<string> ids,IReadOnlyList<string> guids,
        out List<SkillDefinition> result,out string reason)
    {
        result=new List<SkillDefinition>();
        reason=string.Empty;
        int count=ids?.Count??0;
        if(count>GetEquipLimit(actionType))
        {reason="저장된 장착 수가 허용 한도를 초과했습니다.";return false;}
        if(guids!=null&&guids.Count!=0&&guids.Count!=count)
        {reason="SkillId/GUID 배열 길이가 서로 다릅니다.";return false;}
        var candidates=GetResolutionCandidates();
        for(int i=0;i<count;i++)
        {
            var guid=guids!=null&&guids.Count==count?guids[i]:null;
            if(!R7SkillIdentityContract20261102.TryResolve(ids[i],guid,
                candidates,out var definition,out reason))
                return false;
            if(definition.ActionType!=actionType)
            {reason="저장 슬롯과 스킬 종류가 다릅니다: "+definition.name;return false;}
            if(!ValidateCandidate(definition,out reason))return false;
            if(!result.Contains(definition))result.Add(definition);
        }
        return true;
    }

    private SkillDefinition[] GetResolutionCandidates()
    {
        // Preserve the legacy catalog lookup even when catalog pool expansion
        // is disabled. ValidateCandidate still enforces loadout restrictions.
        var local=source?.EnumerateAllDefinitions() ??
            System.Linq.Enumerable.Empty<SkillDefinition>();
        var global=source?.Catalog?.Skills ??
            (IReadOnlyList<SkillDefinition>)Array.Empty<SkillDefinition>();
        return local.Concat(global).Where(x=>x!=null).Distinct().ToArray();
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

    private void CaptureIds(ActionType actionType,List<string> ids,List<string> guids)
    {
        ids.Clear();
        guids.Clear();
        foreach(SkillDefinition definition in GetEquipped(actionType))
        {
            if(definition==null)continue;
            ids.Add(definition.SkillId);
            guids.Add(R7SkillIdentityContract20261102.GuidFor(definition));
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
