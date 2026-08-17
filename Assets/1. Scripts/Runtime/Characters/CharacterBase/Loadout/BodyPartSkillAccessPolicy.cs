using System.Collections.Generic;

/// <summary>
/// Project Abyss의 플레이어형 부위별 스킬 카테고리 계약.
///
/// HEAD       : 일반 / 결투 / 도사림 / 위세
/// LEFT_HAND  : 일반 / 결투
/// RIGHT_HAND : 일반 / 결투
/// LEGS       : 도사림
///
/// 슬롯 데이터가 과거 규칙을 가지고 있어도 런타임에서는 이 계약을 최우선한다.
/// part == null인 단일 HP 전투원은 기존 CharacterSlotConfig 규칙을 사용한다.
/// </summary>
public static class BodyPartSkillAccessPolicy
{
    public static bool Allows(
        BodyPart part,
        ActionType actionType)
    {
        if (part == null)
            return true;

        return Allows(
            part.Type,
            actionType);
    }

    public static bool Allows(
        PartType partType,
        ActionType actionType)
    {
        return partType switch
        {
            PartType.HEAD =>
                actionType == ActionType.NormalAttack ||
                actionType == ActionType.Duel ||
                actionType == ActionType.Preparation ||
                actionType == ActionType.Prestige,

            PartType.LEFT_HAND =>
                actionType == ActionType.NormalAttack ||
                actionType == ActionType.Duel,

            PartType.RIGHT_HAND =>
                actionType == ActionType.NormalAttack ||
                actionType == ActionType.Duel,

            PartType.LEGS =>
                actionType == ActionType.Preparation,

            _ => false
        };
    }

    public static IReadOnlyList<Skill> Filter(
        BodyPart part,
        IReadOnlyList<Skill> source)
    {
        if (source == null || source.Count == 0)
            return System.Array.Empty<Skill>();

        if (part == null)
            return source;

        List<Skill> result = new(source.Count);

        for (int index = 0;
             index < source.Count;
             index++)
        {
            Skill skill = source[index];

            if (skill == null ||
                !Allows(part, skill.ActionType))
            {
                continue;
            }

            result.Add(skill);
        }

        return result;
    }

    public static string GetAccessLabel(
        BodyPart part)
    {
        if (part == null)
            return "캐릭터 슬롯";

        return part.Type switch
        {
            PartType.HEAD => "전체 스킬 사용 가능",
            PartType.LEFT_HAND => "일반 · 결투 전용",
            PartType.RIGHT_HAND => "일반 · 결투 전용",
            PartType.LEGS => "도사림 전용",
            _ => "사용 가능 스킬 없음"
        };
    }
}
