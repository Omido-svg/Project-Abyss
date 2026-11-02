using System;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject 참조 대신 안정적인 SkillId를 저장하는 세이브 친화적 장착 상태다.
/// </summary>
[Serializable]
public sealed class CharacterSkillLoadoutState
{
    public List<string> NormalSkillIds = new();
    public List<string> DuelSkillIds = new();
    public List<string> PreparationSkillIds = new();
    public List<string> PrestigeSkillIds = new();

    // Additive GUID fields preserve historical SkillId-only JSON saves.
    public List<string> NormalSkillGuids = new();
    public List<string> DuelSkillGuids = new();
    public List<string> PreparationSkillGuids = new();
    public List<string> PrestigeSkillGuids = new();

    // 0915: 상점 획득(보유)과 현재 장착을 분리한다. 위세는 이 풀에 들어가지 않는다.
    public List<string> OwnedSkillIds = new();
    public List<string> OwnedSkillGuids = new();

    public IReadOnlyList<string> GetGuids(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalSkillGuids,
            ActionType.Duel => DuelSkillGuids,
            ActionType.Preparation => PreparationSkillGuids,
            ActionType.Prestige => PrestigeSkillGuids,
            _ => Array.Empty<string>()
        };
    }

    public IReadOnlyList<string> GetIds(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalSkillIds,
            ActionType.Duel => DuelSkillIds,
            ActionType.Preparation => PreparationSkillIds,
            ActionType.Prestige => PrestigeSkillIds,
            _ => Array.Empty<string>()
        };
    }
}
