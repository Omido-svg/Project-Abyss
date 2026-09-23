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
        SkillUpgradeState upgrades =
            character.BattleContext?.SkillUpgrades;

        int prestigeCost =
            definition == null
                ? 0
                : SkillUpgradeService.ResolvePrestigeCost(
                    definition,
                    upgrades,
                    definition.PrestigeCost);

        int customResourceCost =
            definition == null
                ? 0
                : SkillUpgradeService.ResolveCustomResourceCost(
                    definition,
                    upgrades,
                    definition.CustomResourceCost);

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus == null)
                return false;

            if (definition.RequireFullPrestige)
            {
                int overrideRequiredPrestige =
                    Canonical0922EmotionRuntimeHooks
                        .TryGetPrestigeStockpileThreshold(
                            character,
                            out int overrideStockpileThreshold)
                        ? overrideStockpileThreshold
                        : character.CurrentStatus?.maxPrestige ?? 0;

                if (overrideRequiredPrestige <= 0 ||
                    character.RuntimeStatus.currentPrestige <
                    overrideRequiredPrestige)
                {
                    return false;
                }
            }

            if (prestigeCost > 0 &&
                character.RuntimeStatus.currentPrestige <
                prestigeCost)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey) &&
                customResourceCost > 0 &&
                SkillResourceAccess.Get(
                    character,
                    definition.CustomResourceKey) <
                customResourceCost)
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

        int requiredPrestige =
            Canonical0922EmotionRuntimeHooks
                .TryGetPrestigeStockpileThreshold(
                    character,
                    out int stockpileThreshold)
                ? stockpileThreshold
                : character.CurrentStatus.maxPrestige;

        if (requiredPrestige <= 0)
            return false;

        return character.RuntimeStatus.currentPrestige >=
               requiredPrestige;
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
        SkillUpgradeState upgrades =
            character.BattleContext?.SkillUpgrades;

        int prestigeCost =
            definition == null
                ? 0
                : SkillUpgradeService.ResolvePrestigeCost(
                    definition,
                    upgrades,
                    definition.PrestigeCost);

        int customResourceCost =
            definition == null
                ? 0
                : SkillUpgradeService.ResolveCustomResourceCost(
                    definition,
                    upgrades,
                    definition.CustomResourceCost);

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus != null)
            {
                if (definition.ConsumeAllPrestige)
                {
                    if (Canonical0922EmotionRuntimeHooks
                            .TryGetPrestigeStockpileThreshold(
                                character,
                                out int overrideStockpileThreshold))
                    {
                        character.RuntimeStatus.currentPrestige =
                            Mathf.Max(
                                0,
                                character.RuntimeStatus.currentPrestige -
                                overrideStockpileThreshold);
                    }
                    else
                    {
                        character.RuntimeStatus.currentPrestige = 0;
                    }
                }
                else if (prestigeCost > 0)
                {
                    character.RuntimeStatus.currentPrestige =
                        Mathf.Max(
                            0,
                            character.RuntimeStatus.currentPrestige -
                            prestigeCost);
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
                else if (customResourceCost > 0)
                {
                    SkillResourceAccess.Modify(
                        character,
                        definition.CustomResourceKey,
                        -customResourceCost,
                        0,
                        int.MaxValue);
                }
            }

            return true;
        }

        if (skill.ActionType == ActionType.Prestige &&
            character.RuntimeStatus != null)
        {
            if (Canonical0922EmotionRuntimeHooks
                    .TryGetPrestigeStockpileThreshold(
                        character,
                        out int normalStockpileThreshold))
            {
                character.RuntimeStatus.currentPrestige =
                    Mathf.Max(
                        0,
                        character.RuntimeStatus.currentPrestige -
                        normalStockpileThreshold);
            }
            else
            {
                character.RuntimeStatus.currentPrestige = 0;
            }

            Debug.Log(
                $"{character.Data?.CharacterName ?? character.name} " +
                $"위세 게이지 소모 : {character.RuntimeStatus.currentPrestige}");
        }

        return true;
    }
}