using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChinchiroCombination
{
    None,
    Arashi,
    Shigoro,
    Moku,
    Blank,
    Hifumi
}

/// <summary>
/// 주사위 세 개를 한 번에 굴려 조합 하나를 만드는 친치로 Resolver.
/// 같은 결과를 모든 교환에서 재사용하려면 SkillDefinition의
/// RollReusePolicy를 OncePerAction으로 설정한다.
/// </summary>
public sealed class ChinchiroResolver : SkillResolver
{
    private readonly int arashiBonus;
    private readonly int shigoroBonus;
    private readonly int hifumiPenalty;
    private readonly int hifumiSelfDamage;

    public override int MinValue =>
        Mathf.Min(-hifumiPenalty, 1);

    public override int MaxValue =>
        Mathf.Max(
            6,
            Mathf.Max(arashiBonus, shigoroBonus));

    public ChinchiroResolver(
        int arashiBonus,
        int shigoroBonus,
        int hifumiPenalty,
        int hifumiSelfDamage)
    {
        this.arashiBonus = Mathf.Max(0, arashiBonus);
        this.shigoroBonus = Mathf.Max(0, shigoroBonus);
        this.hifumiPenalty = Mathf.Max(0, hifumiPenalty);
        this.hifumiSelfDamage = Mathf.Max(0, hifumiSelfDamage);
    }

    public override int Roll()
    {
        return RollResult(null).RawValue;
    }

    public override RollResult RollResult(
        Skill skill)
    {
        int a = UnityEngine.Random.Range(1, 7);
        int b = UnityEngine.Random.Range(1, 7);
        int c = UnityEngine.Random.Range(1, 7);

        List<int> sorted = new() { a, b, c };
        sorted.Sort();

        ChinchiroCombination combination;
        int value;
        if (a == b && b == c)
        {
            combination = ChinchiroCombination.Arashi;
            value = arashiBonus;
        }
        else if (sorted[0] == 4 &&
                 sorted[1] == 5 &&
                 sorted[2] == 6)
        {
            combination = ChinchiroCombination.Shigoro;
            value = shigoroBonus;
        }
        else if (sorted[0] == 1 &&
                 sorted[1] == 2 &&
                 sorted[2] == 3)
        {
            combination = ChinchiroCombination.Hifumi;
            value = -hifumiPenalty;
        }
        else if (a == b || a == c || b == c)
        {
            combination = ChinchiroCombination.Moku;

            value = a == b || a == c
                ? a
                : b;
        }
        else
        {
            combination = ChinchiroCombination.Blank;
            value = sorted[0];
        }

        RollResult result = CreateResult(
            skill,
            SkillResolverType.Chinchiro,
            value,
            value >= MaxValue);

        result.DiceMin = 1;
        result.DiceMax = 6;
        result.DiceValues.Add(a);
        result.DiceValues.Add(b);
        result.DiceValues.Add(c);
        result.ChinchiroCombination = combination;
        result.ChinchiroBonus = value;
        result.ChinchiroSelfDamage =
            combination == ChinchiroCombination.Hifumi
                ? hifumiSelfDamage
                : 0;
        // 아라시와 시고로는 위력 조합이며 크리티컬이 아니다.
        result.IsCritical = false;

        return result;
    }
}