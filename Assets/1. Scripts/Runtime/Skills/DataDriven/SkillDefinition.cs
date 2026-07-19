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

    [Header("Exchange Roll")]
    [Min(1)]
    public int ExchangeRollCount = 3;

    public SkillRollReusePolicy RollReusePolicy =
        SkillRollReusePolicy.RollEachExchange;

    [Tooltip(
        "김삿갓처럼 위세지만 속도순 COMBAT에서 합을 붙일 때 사용합니다.")]
    public bool ResolvePrestigeInCombat;

    public bool OverrideCanClash;
    public bool CanClashValue;

    [Header("Resolver")]
    public SkillResolverType ResolverType;
    public int DiceMin;
    public int DiceMax;

    [Min(1)]
    public int CoinCount = 1;

    [Range(0f, 1f)]
    public float CoinFrontChance = 0.5f;
    public int CoinFrontValue = 1;
    public int CoinBackValue;
    public bool CoinFrontIsCritical;

    [Header("Chinchiro")]
    [Min(0)] public int ChinchiroArashiBonus = 6;
    [Min(0)] public int ChinchiroShigoroBonus = 6;
    [Min(0)] public int ChinchiroHifumiPenalty = 1;
    [Min(0)] public int ChinchiroHifumiSelfDamage = 1;

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
                new CoinResolver(
                    CoinCount,
                    CoinFrontChance,
                    CoinFrontValue,
                    CoinBackValue,
                    CoinFrontIsCritical),

            SkillResolverType.Slot =>
                new SlotResolver(),

            SkillResolverType.Chinchiro =>
                new ChinchiroResolver(
                    ChinchiroArashiBonus,
                    ChinchiroShigoroBonus,
                    ChinchiroHifumiPenalty,
                    ChinchiroHifumiSelfDamage),

            _ => CreateFallbackResolver()
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ExchangeRollCount = Mathf.Max(1, ExchangeRollCount);
        CoinCount = Mathf.Max(1, CoinCount);
        CoinFrontChance = Mathf.Clamp01(CoinFrontChance);

        ChinchiroArashiBonus = Mathf.Max(0, ChinchiroArashiBonus);
        ChinchiroShigoroBonus = Mathf.Max(0, ChinchiroShigoroBonus);
        ChinchiroHifumiPenalty = Mathf.Max(0, ChinchiroHifumiPenalty);
        ChinchiroHifumiSelfDamage = Mathf.Max(0, ChinchiroHifumiSelfDamage);

        if (ResolvePrestigeInCombat &&
            ActionType != ActionType.Prestige)
        {
            ResolvePrestigeInCombat = false;
        }
    }
#endif

    private SkillResolver CreateFallbackResolver()
    {
        Debug.LogError(
            $"지원하지 않는 ResolverType입니다 : {ResolverType}");
        return new DiceResolver(0, 0);
    }
}

public enum SkillResolverType
{
    Dice = 0,
    Coin = 1,
    Slot = 2,
    Chinchiro = 3
}
