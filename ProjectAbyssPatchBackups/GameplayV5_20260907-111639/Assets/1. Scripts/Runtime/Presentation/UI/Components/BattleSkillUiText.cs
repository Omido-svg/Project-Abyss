using System.Collections.Generic;
using System.Text;

public static class BattleSkillUiText
{
    public static bool ShouldShowClashRollPattern(
        Skill skill)
    {
        return skill != null &&
               skill.CanClash;
    }

    public static string BuildRollSummary(
        Skill skill)
    {
        if (skill == null)
            return "-";

        // 공/수는 실제 합 교환에서만 의미가 있다.
        // 비합 도사림과 비합 위세가 내부 Roll 데이터를 보유하더라도
        // 전투 UI에서는 공격/수비 굴림으로 표현하지 않는다.
        if (!ShouldShowClashRollPattern(skill))
            return "합 없음";

        SkillDefinition definition =
            skill.Definition;

        if (definition?.Rolls == null ||
            definition.Rolls.Count == 0)
        {
            return
                $"{skill.ExchangeRollCount} Roll · " +
                $"{skill.MinPower}~{skill.MaxPower}";
        }

        StringBuilder builder =
            new();

        for (int index = 0;
             index < definition.Rolls.Count;
             index++)
        {
            SkillRollData roll =
                definition.Rolls[index];

            if (roll == null)
                continue;

            if (builder.Length > 0)
                builder.Append("  |  ");

            builder.Append(
                roll.Type == CombatRollType.Defense
                    ? "수비 "
                    : "공격 ");

            switch (roll.RngSource)
            {
                case RollRngSource.Coin:
                    builder.Append(
                        roll.CoinBackPower);
                    builder.Append('/');
                    builder.Append(
                        roll.CoinFrontPower);
                    break;

                case RollRngSource.Chinchiro:
                    builder.Append("친치로");
                    break;

                case RollRngSource.Slot:
                    builder.Append("슬롯 ");
                    builder.Append(roll.SlotMinimum);
                    builder.Append('~');
                    builder.Append(roll.SlotMaximum);
                    break;

                default:
                    builder.Append(
                        roll.GetDiceFinalMinPower(
                            skill.BasePower));
                    builder.Append('~');
                    builder.Append(
                        roll.GetDiceFinalMaxPower(
                            skill.BasePower));
                    break;
            }
        }

        return builder.Length > 0
            ? builder.ToString()
            : $"{skill.ExchangeRollCount} Roll";
    }

    public static string BuildDescription(
        Skill skill)
    {
        if (skill == null)
            return "선택한 스킬이 없습니다.";

        SkillDefinition definition =
            skill.Definition;

        StringBuilder builder =
            new();

        if (definition != null &&
            !string.IsNullOrWhiteSpace(
                definition.Description))
        {
            builder.AppendLine(
                definition.Description.Trim());

            builder.AppendLine();
        }

        builder.AppendLine(
            $"행동 종류: " +
            $"{GetActionTypeName(skill.ActionType)}");

        builder.AppendLine(
            $"빛 비용: {skill.EnergyCost}");

        if (skill.ActionType == ActionType.NormalAttack ||
            skill.ActionType == ActionType.Duel ||
            (skill.ActionType == ActionType.Prestige && skill.CanClash))
        {
            PhysicalDamageType physical =
                skill.Owner is Yujin yujin
                    ? yujin.ResolveWeaponPhysicalType()
                    : definition?.PhysicalType ?? PhysicalDamageType.Cut;

            builder.AppendLine($"물리 속성: {GetPhysicalTypeName(physical)}");
        }

        builder.AppendLine(
            $"합 가능: " +
            $"{(skill.CanClash ? "가능" : "불가")}");

        if (skill.AttackWeight > 1)
        {
            builder.AppendLine(
                $"공격 가중치: {skill.AttackWeight} " +
                "(메인 1 + 무작위 추가 타깃)");
        }

        if (ShouldShowClashRollPattern(skill))
        {
            builder.AppendLine(
                $"합 굴림: {BuildRollSummary(skill)}");
        }
        else
        {
            builder.AppendLine(
                "합 굴림: 없음");
        }

        if (definition?.MultiRollPenalty?.Enabled == true &&
            skill.CanClash)
        {
            builder.AppendLine(
                "다굴림 페널티가 적용됩니다.");
        }

        if (definition != null)
        {
            bool hasEffects = false;
            foreach (SkillEffectEntry entry in definition.EnumerateEffectEntries())
            {
                if (entry?.Definition != null)
                {
                    hasEffects = true;
                    break;
                }
            }

            if (hasEffects)
            {
                builder.Append("효과: ");
                bool appended = false;

                foreach (SkillEffectEntry entry in definition.EnumerateEffectEntries())
                {
                    SkillEffectDefinition effect = entry?.Definition;
                    if (effect == null)
                        continue;

                    if (appended)
                        builder.Append(", ");

                    builder.Append(BuildEffectDisplayName(entry));
                    appended = true;
                }

                if (!appended)
                    builder.Append("없음");

                builder.AppendLine();
            }
        }

        return builder
            .ToString()
            .TrimEnd();
    }

    public static List<SkillKeywordEntry> BuildKeywords(
        Skill skill)
    {
        List<SkillKeywordEntry> result =
            new();

        if (skill == null)
            return result;

        SkillDefinition definition =
            skill.Definition;

        if (definition?.Keywords != null)
        {
            foreach (SkillKeywordEntry entry
                     in definition.Keywords)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(
                        entry.Name))
                {
                    continue;
                }

                if (!skill.CanClash &&
                    IsClashOnlyKeyword(
                        entry.Name))
                {
                    continue;
                }

                AddUnique(
                    result,
                    entry.Name,
                    entry.Description);
            }
        }

        string actionTypeName =
            GetActionTypeName(
                skill.ActionType);

        AddUnique(
            result,
            actionTypeName,
            BattleKeywordGlossary.GetDescription(
                actionTypeName));

        if (skill.EnergyCost > 0)
        {
            AddUnique(
                result,
                "빛",
                BattleKeywordGlossary.GetDescription(
                    "빛"));
        }

        if (skill.CanClash)
        {
            AddUnique(
                result,
                "합",
                BattleKeywordGlossary.GetDescription(
                    "합"));

            if (definition?.Rolls != null)
            {
                foreach (SkillRollData roll
                         in definition.Rolls)
                {
                    if (roll?.Type !=
                        CombatRollType.Defense)
                    {
                        continue;
                    }

                    AddUnique(
                        result,
                        "수비",
                        BattleKeywordGlossary.GetDescription(
                            "수비"));
                }
            }
        }

        if (skill.AttackWeight > 1)
        {
            AddUnique(
                result,
                "공격 가중치",
                BattleKeywordGlossary.GetDescription(
                    "공격 가중치"));
        }

        if (definition?.CanBreakPart == true)
        {
            AddUnique(
                result,
                "부위 파괴",
                BattleKeywordGlossary.GetDescription(
                    "부위 파괴"));
        }

        if (definition != null)
        {
            foreach (SkillEffectEntry entry
                     in definition.EnumerateEffectEntries())
            {
                SkillEffectDefinition effect = entry?.Definition;
                if (effect == null)
                    continue;

                string typeName =
                    effect.GetType().Name;

                if (typeName.Contains("Bleed") ||
                    effect is AddBodyPartStatusEffect statusEffect &&
                    statusEffect.StatusEffectId == StatusEffectId.Bleeding)
                {
                    AddUnique(
                        result,
                        "혈상",
                        BattleKeywordGlossary.GetDescription(
                            "혈상"));
                }

                if (typeName.Contains("Momentum"))
                {
                    AddUnique(
                        result,
                        "기세",
                        BattleKeywordGlossary.GetDescription(
                            "기세"));
                }
            }
        }

        return result;
    }

    private static string BuildEffectDisplayName(
        SkillEffectEntry entry)
    {
        SkillEffectDefinition effect =
            entry?.Definition;

        if (effect == null)
            return "없음";

        SkillEffectOverrides overrides =
            entry.Overrides;

        if (effect is AddBodyPartStatusEffect status)
        {
            int stack =
                overrides?.ResolveStack(status.Stack) ??
                status.Stack;
            int duration =
                overrides?.ResolveDuration(status.Duration) ??
                status.Duration;

            return
                $"{status.StatusEffectId} " +
                $"{stack}스택 / {duration}턴";
        }

        if (effect is ApplyStatusIfConditionEffect conditionalStatus)
        {
            int stack =
                overrides?.ResolveStack(conditionalStatus.Stack) ??
                conditionalStatus.Stack;
            int duration =
                overrides?.ResolveDuration(conditionalStatus.Duration) ??
                conditionalStatus.Duration;

            return
                $"{conditionalStatus.StatusEffectId} " +
                $"{stack}스택 / {duration}턴";
        }

        if (effect is GainPrestigeEffect prestige)
        {
            int amount =
                overrides?.ResolveAmount(prestige.Amount) ??
                prestige.Amount;
            return $"위세 +{amount}";
        }

        if (effect is GainCustomResourceEffect resource)
        {
            string key =
                overrides?.ResolveResourceKey(resource.ResourceKey) ??
                resource.ResourceKey;
            int amount =
                overrides?.ResolveAmount(resource.Amount) ??
                resource.Amount;
            return $"{key} {(amount >= 0 ? "+" : string.Empty)}{amount}";
        }

        if (effect is ModifyResourceEffect modify)
        {
            int amount =
                overrides?.ResolveAmount(modify.Amount) ??
                modify.Amount;
            string key =
                modify.ResourceType == SkillResourceType.Prestige
                    ? "위세"
                    : overrides?.ResolveResourceKey(
                          modify.CustomResourceKey) ??
                      modify.CustomResourceKey;

            return $"{key} {(amount >= 0 ? "+" : string.Empty)}{amount}";
        }

        return effect.GetType().Name;
    }

    public static string GetPhysicalTypeName(PhysicalDamageType type) => type switch
    {
        PhysicalDamageType.Cut => "절단",
        PhysicalDamageType.Blunt => "둔격",
        PhysicalDamageType.Pierce => "관통",
        _ => "절단"
    };

    public static string GetActionTypeName(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => "일반공격",
            ActionType.Duel => "결투",
            ActionType.Preparation => "도사림",
            ActionType.Prestige => "위세",
            _ => actionType.ToString()
        };
    }

    private static bool IsClashOnlyKeyword(
        string keyword)
    {
        return string.Equals(
                   keyword,
                   "합",
                   System.StringComparison.Ordinal) ||
               string.Equals(
                   keyword,
                   "공격",
                   System.StringComparison.Ordinal) ||
               string.Equals(
                   keyword,
                   "수비",
                   System.StringComparison.Ordinal);
    }

    private static void AddUnique(
        List<SkillKeywordEntry> result,
        string name,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        foreach (SkillKeywordEntry existing
                 in result)
        {
            if (existing != null &&
                string.Equals(
                    existing.Name,
                    name,
                    System.StringComparison.Ordinal))
            {
                return;
            }
        }

        result.Add(
            new SkillKeywordEntry
            {
                Name = name,
                Description =
                    string.IsNullOrWhiteSpace(
                        description)
                        ? BattleKeywordGlossary.GetDescription(
                            name)
                        : description
            });
    }
}
