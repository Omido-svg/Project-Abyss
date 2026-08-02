using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Skill/Skill Definition", fileName = "NewSkillDefinition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("저장 데이터와 상점 카탈로그에서 사용하는 안정적인 식별자입니다.")]
    private string skillId;

    public string SkillId =>
        string.IsNullOrWhiteSpace(skillId)
            ? name
            : skillId;

    [Header("Basic")]
    public string SkillName;
    public ActionType ActionType;
    public int BasePower;
    public bool CanBreakPart;
    public bool GainPrestige = true;

    [Header("Attack Weight — total character targets including main target")]
    public AttackWeightSettings AttackWeight =
        new AttackWeightSettings();

    [TextArea(2, 8)]
    [Tooltip("상세 화면에서 표시할 스킬 설명입니다. 비어 있으면 수치로 자동 생성합니다.")]
    public string Description;

    [Tooltip("클릭 가능한 고유 키워드입니다. 비어 있으면 행동 타입과 효과에서 자동 추론합니다.")]
    public List<SkillKeywordEntry> Keywords = new();

    [Header("Independent rolls — 1 to 8")]
    public List<SkillRollData> Rolls = new();

    [Header("Legacy roll fallback")]
    [Range(1, 8)] public int ExchangeRollCount = 3;
    public SkillRollReusePolicy RollReusePolicy = SkillRollReusePolicy.RollEachExchange;

    [Header("High-roll-count risk")]
    public MultiRollPenaltyData MultiRollPenalty = new();

    [Tooltip("김삿갓처럼 위세지만 속도순 COMBAT에서 합을 붙일 때 사용합니다.")]
    public bool ResolvePrestigeInCombat;
    public bool OverrideCanClash;
    public bool CanClashValue;

    [Header("Preparation")]
    public PreparationTier PreparationTier = PreparationTier.Weak;

    [Header("Legacy/default RNG source")]
    public SkillResolverType ResolverType;
    public int DiceMin;
    public int DiceMax;
    [Min(1)] public int CoinCount = 1;
    [Range(0f, 1f)] public float CoinFrontChance = 0.5f;
    public int CoinFrontValue = 1;
    public int CoinBackValue;
    public bool CoinFrontIsCritical;

    [Header("Chinchiro")]
    [Min(0)] public int ChinchiroArashiBonus = 6;
    [Min(0)] public int ChinchiroShigoroBonus = 6;
    [Min(0)] public int ChinchiroHifumiPenalty = 1;
    [Min(0)] public int ChinchiroHifumiSelfDamage = 1;

    [Header("Effects — reusable template + per-skill parameters")]
    public List<SkillEffectEntry> EffectEntries = new();

    [HideInInspector]
    [Tooltip("기존 에셋 호환용입니다. 새 스킬은 EffectEntries를 사용하세요.")]
    public List<SkillEffectDefinition> Effects = new();

    [Header("Energy Cost — every skill owns its cost")]
    public bool OverrideEnergyCost;
    [Min(0)] public int EnergyCost;

    [Header("Other Resource Rules")]
    public bool OverrideResourceRules;
    public bool RequireFullPrestige;
    [Min(0)] public int PrestigeCost;
    public bool ConsumeAllPrestige;
    public string CustomResourceKey;
    [Min(0)] public int CustomResourceCost;
    public bool ConsumeAllCustomResource;

    [Header("Prestige Use Policy")]
    public bool OverridePrestigeUsePolicy;
    public PrestigeUsePolicy PrestigeUsePolicy = PrestigeUsePolicy.OncePerTurn;

    [Header("Visual")]
    public SkillVisualDefinition VisualDefinition;


    public bool HasEffectEntries =>
        EffectEntries != null &&
        EffectEntries.Exists(entry => entry?.Definition != null);

    public IEnumerable<SkillEffectEntry> EnumerateEffectEntries()
    {
        if (HasEffectEntries)
        {
            foreach (SkillEffectEntry entry in EffectEntries)
            {
                if (entry?.Definition != null)
                    yield return entry;
            }

            yield break;
        }

        if (Effects == null)
            yield break;

        foreach (SkillEffectDefinition effect in Effects)
        {
            if (effect != null)
                yield return SkillEffectEntry.FromLegacy(effect);
        }
    }

    public int EffectiveRollCount =>
        Rolls != null && Rolls.Count > 0
            ? Mathf.Clamp(Rolls.Count, 1, 8)
            : Mathf.Clamp(ExchangeRollCount, 1, 8);

    public SkillRollData GetRollData(int exchangeIndex)
    {
        if (Rolls == null || Rolls.Count == 0)
            return null;
        int index = Mathf.Clamp(exchangeIndex, 0, Rolls.Count - 1);
        return Rolls[index];
    }

    public Skill CreateRuntimeSkill() => ActionType switch
    {
        ActionType.NormalAttack => new DataNormalSkill(this),
        ActionType.Duel => new DataDuelSkill(this),
        ActionType.Preparation => new DataPreparationSkill(this),
        ActionType.Prestige => new DataPrestigeSkill(this),
        _ => CreateUnsupportedSkill()
    };

    private Skill CreateUnsupportedSkill()
    {
        Debug.LogError($"지원하지 않는 ActionType입니다 : {ActionType}");
        return null;
    }

    public SkillResolver CreateResolver() => ResolverType switch
    {
        SkillResolverType.Dice => new DiceResolver(DiceMin, DiceMax),
        SkillResolverType.Coin => new CoinResolver(CoinCount, CoinFrontChance, CoinFrontValue, CoinBackValue, CoinFrontIsCritical),
        SkillResolverType.Slot => new SlotResolver(),
        SkillResolverType.Chinchiro => new ChinchiroResolver(ChinchiroArashiBonus, ChinchiroShigoroBonus, ChinchiroHifumiPenalty, ChinchiroHifumiSelfDamage),
        _ => CreateFallbackResolver()
    };

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureSkillId();
        ExchangeRollCount = Mathf.Clamp(ExchangeRollCount, 1, 8);
        EnergyCost = Mathf.Max(0, EnergyCost);
        CoinCount = Mathf.Max(1, CoinCount);
        CoinFrontChance = Mathf.Clamp01(CoinFrontChance);
        Rolls ??= new List<SkillRollData>();
        Keywords ??= new List<SkillKeywordEntry>();
        EffectEntries ??= new List<SkillEffectEntry>();
        Effects ??= new List<SkillEffectDefinition>();
        MultiRollPenalty ??= new MultiRollPenaltyData();
        MultiRollPenalty.Sanitize();
        AttackWeight ??= new AttackWeightSettings();
        AttackWeight.Sanitize();
        while (Rolls.Count > 8) Rolls.RemoveAt(Rolls.Count - 1);
        for (int i = 0; i < Rolls.Count; i++) Rolls[i]?.Sanitize(i);
        if (ResolvePrestigeInCombat && ActionType != ActionType.Prestige) ResolvePrestigeInCombat = false;
        if (ActionType != ActionType.Preparation) PreparationTier = PreparationTier.Weak;
    }

    public bool EnsureSkillId()
    {
        if (!string.IsNullOrWhiteSpace(skillId))
            return false;

        skillId = System.Guid.NewGuid().ToString("N");
        return true;
    }
#endif

    private SkillResolver CreateFallbackResolver()
    {
        Debug.LogError($"지원하지 않는 ResolverType입니다 : {ResolverType}");
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