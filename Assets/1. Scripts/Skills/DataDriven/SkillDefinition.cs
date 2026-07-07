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
    
    [Header("Visual")]
    public SkillVisualDefinition VisualDefinition;

    public Skill CreateRuntimeSkill()
    {
        switch (ActionType)
        {
            case ActionType.NormalAttack:
                return new DataNormalSkill(this);

            case ActionType.Duel:
                return new DataDuelSkill(this);

            case ActionType.Preparation:
                return new DataPreparationSkill(this);

            case ActionType.Prestige:
                return new DataPrestigeSkill(this);

            default:
                Debug.LogError($"지원하지 않는 ActionType입니다 : {ActionType}");
                return null;
        }
    }

    public SkillResolver CreateResolver()
    {
        switch (ResolverType)
        {
            case SkillResolverType.Dice:
                return new DiceResolver(DiceMin, DiceMax);

            case SkillResolverType.Coin:
                return new CoinResolver(CoinCount);

            default:
                Debug.LogError($"지원하지 않는 ResolverType입니다 : {ResolverType}");
                return new DiceResolver(0, 0);
        }
    }
}

public enum SkillResolverType
{
    Dice,
    Coin
}