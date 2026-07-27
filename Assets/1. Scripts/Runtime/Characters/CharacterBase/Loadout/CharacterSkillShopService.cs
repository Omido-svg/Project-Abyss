using System;
using System.Collections.Generic;

/// <summary>
/// 상점 UI와 전투 코어 사이의 얇은 어댑터다.
/// 가격 지불과 소유권 판정은 상점 계층에서 끝낸 후 이 서비스에 교체를 요청한다.
/// </summary>
public sealed class CharacterSkillShopService
{
    private readonly Character character;

    public CharacterSkillShopService(Character character)
    {
        this.character = character;
    }

    public IEnumerable<SkillDefinition> EnumerateOffers(
        ActionType actionType)
    {
        CharacterCombatRulesRuntime rules =
            character?.CombatRulesRuntime;

        if (rules == null)
            yield break;

        foreach (SkillDefinition definition
                 in rules.EnumerateSkillCandidates(actionType))
        {
            if (definition != null)
                yield return definition;
        }
    }

    public bool TryReplace(
        SkillDefinition equipped,
        SkillDefinition purchased,
        out string reason)
    {
        CharacterCombatRulesRuntime rules =
            character?.CombatRulesRuntime;

        if (rules == null)
        {
            reason = "캐릭터의 런타임 전투 규칙이 초기화되지 않았습니다.";
            return false;
        }

        return rules.TryReplaceSkill(
            equipped,
            purchased,
            out reason);
    }

    public CharacterSkillLoadoutState CaptureState()
    {
        return character?.CombatRulesRuntime?.CaptureLoadoutState();
    }

    public bool TryRestoreState(
        CharacterSkillLoadoutState state,
        out string reason)
    {
        CharacterCombatRulesRuntime rules =
            character?.CombatRulesRuntime;

        if (rules == null)
        {
            reason = "캐릭터의 런타임 전투 규칙이 초기화되지 않았습니다.";
            return false;
        }

        return rules.TryRestoreLoadoutState(state, out reason);
    }
}
