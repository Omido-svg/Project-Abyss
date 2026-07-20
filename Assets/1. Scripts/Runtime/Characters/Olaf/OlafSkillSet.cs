using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Olaf Skill Set",
    fileName = "OlafSkillSet")]
public class OlafSkillSet : ScriptableObject
{
    [Header("Definitions")]
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PreparationSkill;
    public SkillDefinition PrestigeSkill;

    public Skill CreateNormalAttack()
    {
        return CreateExpectedSkill(
            NormalAttack,
            ActionType.NormalAttack);
    }

    public Skill CreateDuelSkill()
    {
        if (!IsExpectedType(
                DuelSkill,
                ActionType.Duel))
        {
            return null;
        }

        // DataDuelSkill을 그대로 만들면
        // OlafMadnessMechanic.GetDuelPushBonus()가
        // Skill.GetMomentumPushBonus()까지 연결되지 않는다.
        return new OlafDuelRuntimeSkill(
            DuelSkill);
    }

    public Skill CreatePreparationSkill()
    {
        if (!IsExpectedType(
                PreparationSkill,
                ActionType.Preparation))
        {
            return null;
        }

        // 올라프의 고유 도사림은 확정 강한 도사림이다.
        // SO 마이그레이션 누락과 무관하게 에너지 기본 비용 1을 사용한다.
        return new OlafPreparationRuntimeSkill(
            PreparationSkill);
    }

    public Skill CreatePrestigeSkill()
    {
        return CreateExpectedSkill(
            PrestigeSkill,
            ActionType.Prestige);
    }

    private Skill CreateExpectedSkill(
        SkillDefinition definition,
        ActionType expectedType)
    {
        if (!IsExpectedType(
                definition,
                expectedType))
        {
            return null;
        }

        return definition.CreateRuntimeSkill();
    }

    private bool IsExpectedType(
        SkillDefinition definition,
        ActionType expectedType)
    {
        if (definition == null)
            return false;

        if (definition.ActionType == expectedType)
            return true;

        Debug.LogWarning(
            $"{name}: {definition.name}의 ActionType이 " +
            $"{definition.ActionType}입니다. " +
            $"Olaf 슬롯은 {expectedType}을 요구합니다.",
            this);

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateDefinition(
            NormalAttack,
            ActionType.NormalAttack,
            nameof(NormalAttack));

        ValidateDefinition(
            DuelSkill,
            ActionType.Duel,
            nameof(DuelSkill));

        ValidateDefinition(
            PreparationSkill,
            ActionType.Preparation,
            nameof(PreparationSkill));

        ValidateDefinition(
            PrestigeSkill,
            ActionType.Prestige,
            nameof(PrestigeSkill));
    }

    private void ValidateDefinition(
        SkillDefinition definition,
        ActionType expectedType,
        string fieldName)
    {
        if (definition == null ||
            definition.ActionType == expectedType)
        {
            return;
        }

        Debug.LogWarning(
            $"{name}.{fieldName}: " +
            $"{definition.name}의 ActionType은 " +
            $"{expectedType}이어야 합니다.",
            this);
    }
#endif
}

// 올라프 결투 전용 런타임 어댑터.
// 전역 DataDuelSkill이나 ClashManager를 올라프 때문에 수정하지 않는다.
public sealed class OlafDuelRuntimeSkill : DataDuelSkill
{
    public OlafDuelRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override int GetMomentumPushBonus(
        BattleAction action)
    {
        // 결투 푸시는 전역 규칙의 고정값만 사용한다.
        // 캐릭터 고유 강화가 필요하면 별도 SkillEffect/Augment에서 명시한다.
        return 0;
    }
}

/// <summary>
/// 올라프 고유 도사림의 런타임 계약.
/// PreparationTier.Strong이므로 별도 OverrideEnergyCost가 없으면 에너지 1을 소비한다.
/// </summary>
public sealed class OlafPreparationRuntimeSkill : DataPreparationSkill
{
    public OlafPreparationRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override PreparationTier PreparationTier =>
        PreparationTier.Strong;
}

