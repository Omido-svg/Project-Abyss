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
