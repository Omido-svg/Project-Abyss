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
        if (actionType == ActionType.Prestige)
            yield break;

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

    /// <summary>상점은 소유권만 추가한다. 장착 교체는 정비에서만 가능하다.</summary>
    public bool TryPurchase(SkillDefinition purchased, out string reason)
    {
        CharacterSkillLoadoutRuntime loadout = character?.CombatRulesRuntime?.Loadout;
        if (loadout == null)
        {
            reason = "캐릭터의 런타임 전투 규칙이 초기화되지 않았습니다.";
            return false;
        }
        return loadout.TryAcquire(purchased, out reason);
    }

    [Obsolete("0915: 상점에서 즉시 장착 교체 금지. TryPurchase 후 Maintenance service를 사용하세요.")]
    public bool TryReplace(
        SkillDefinition equipped,
        SkillDefinition purchased,
        out string reason)
    {
        reason = "0915 규칙: 상점에서는 획득만 가능하고 장착 교체는 정비에서만 가능합니다.";
        return false;
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
