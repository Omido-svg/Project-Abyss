using UnityEngine;

/// <summary>
/// Skill의 비용 판정/소비를 한 곳에서 처리한다.
/// Skill은 외부 façade API를 유지하고, 실제 자원 정책은 이 서비스에 위임한다.
/// </summary>
internal static class SkillCostService
{
    public static bool CanUse(
        Skill skill,
        Character character)
    {
        if (skill == null || character == null)
            return false;

        if (!character.CanAffordEnergy(skill.EnergyCost))
            return false;

        SkillDefinition definition = skill.Definition;

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus == null)
                return false;

            if (definition.RequireFullPrestige)
            {
                if (character.CurrentStatus == null ||
                    character.CurrentStatus.maxPrestige <= 0 ||
                    character.RuntimeStatus.currentPrestige <
                    character.CurrentStatus.maxPrestige)
                {
                    return false;
                }
            }

            if (definition.PrestigeCost > 0 &&
                character.RuntimeStatus.currentPrestige <
                definition.PrestigeCost)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey) &&
                definition.CustomResourceCost > 0 &&
                SkillResourceAccess.Get(
                    character,
                    definition.CustomResourceKey) <
                definition.CustomResourceCost)
            {
                return false;
            }

            return true;
        }

        if (skill.ActionType != ActionType.Prestige)
            return true;

        if (character.CurrentStatus == null ||
            character.RuntimeStatus == null)
        {
            return false;
        }

        if (character.CurrentStatus.maxPrestige <= 0)
            return false;

        return character.RuntimeStatus.currentPrestige >=
               character.CurrentStatus.maxPrestige;
    }

    /// <summary>
    /// 호출자가 CanUseByResource까지 통과한 뒤 실제 비용을 commit한다.
    /// 에너지를 먼저 소비하는 기존 순서와 custom/prestige 규칙을 그대로 보존한다.
    /// </summary>
    public static bool TryConsume(
        Skill skill,
        Character character,
        BattleAction sourceAction)
    {
        if (skill == null || character == null)
            return false;

        if (!character.TryConsumeEnergy(
                skill.EnergyCost,
                sourceAction,
                skill))
        {
            return false;
        }

        SkillDefinition definition = skill.Definition;

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus != null)
            {
                if (definition.ConsumeAllPrestige)
                {
                    character.RuntimeStatus.currentPrestige = 0;
                }
                else if (definition.PrestigeCost > 0)
                {
                    character.RuntimeStatus.currentPrestige =
                        Mathf.Max(
                            0,
                            character.RuntimeStatus.currentPrestige -
                            definition.PrestigeCost);
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey))
            {
                if (definition.ConsumeAllCustomResource)
                {
                    SkillResourceAccess.Set(
                        character,
                        definition.CustomResourceKey,
                        0);
                }
                else if (definition.CustomResourceCost > 0)
                {
                    SkillResourceAccess.Modify(
                        character,
                        definition.CustomResourceKey,
                        -definition.CustomResourceCost,
                        0,
                        int.MaxValue);
                }
            }

            return true;
        }

        if (skill.ActionType == ActionType.Prestige &&
            character.RuntimeStatus != null)
        {
            character.RuntimeStatus.currentPrestige = 0;

            Debug.Log(
                $"{character.Data?.CharacterName ?? character.name} " +
                "위세 게이지 소모 : 0");
        }

        return true;
    }
}
