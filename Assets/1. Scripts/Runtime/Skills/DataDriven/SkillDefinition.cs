using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill/Skill Definition",
    fileName = "NewSkillDefinition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Basic")]
    public string SkillName;
    public ActionType ActionType;
    public int BasePower;
    public bool CanBreakPart;
    public bool GainPrestige = true;

    [Header("Resolver")]
    public SkillResolverType ResolverType;
    public int DiceMin;
    public int DiceMax;
    public int CoinCount;

    [Header("Effects")]
    public List<SkillEffectDefinition> Effects = new();

    [Header("Resource Rules")]
    [Tooltip("false면 기존 위세 규칙을 그대로 사용합니다.")]
    public bool OverrideResourceRules;

    public bool RequireFullPrestige;
    [Min(0)] public int PrestigeCost;
    public bool ConsumeAllPrestige;

    public string CustomResourceKey;
    [Min(0)] public int CustomResourceCost;
    public bool ConsumeAllCustomResource;

    [Header("Prestige Use Policy")]
    public bool OverridePrestigeUsePolicy;
    public PrestigeUsePolicy PrestigeUsePolicy =
        PrestigeUsePolicy.OncePerTurn;

    [Header("Visual")]
    public SkillVisualDefinition VisualDefinition;

    public Skill CreateRuntimeSkill()
    {
        return ActionType switch
        {
            ActionType.NormalAttack =>
                new DataNormalSkill(this),
            ActionType.Duel =>
                new DataDuelSkill(this),
            ActionType.Preparation =>
                new DataPreparationSkill(this),
            ActionType.Prestige =>
                new DataPrestigeSkill(this),
            _ => CreateUnsupportedSkill()
        };
    }

    private Skill CreateUnsupportedSkill()
    {
        Debug.LogError(
            $"지원하지 않는 ActionType입니다 : {ActionType}");
        return null;
    }

    public SkillResolver CreateResolver()
    {
        return ResolverType switch
        {
            SkillResolverType.Dice =>
                new DiceResolver(DiceMin, DiceMax),
            SkillResolverType.Coin =>
                new CoinResolver(CoinCount),
            SkillResolverType.Slot =>
                new SlotResolver(),
            _ => CreateFallbackResolver()
        };
    }

    private SkillResolver CreateFallbackResolver()
    {
        Debug.LogError(
            $"지원하지 않는 ResolverType입니다 : {ResolverType}");
        return new DiceResolver(0, 0);
    }
}

public enum SkillResolverType
{
    Dice,
    Coin,
    Slot
}
