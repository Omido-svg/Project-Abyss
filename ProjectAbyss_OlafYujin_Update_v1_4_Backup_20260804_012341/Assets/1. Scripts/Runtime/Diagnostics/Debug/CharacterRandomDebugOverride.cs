using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum DebugRandomResolverMode
{
    Original = 0,
    Dice = 1,
    Coin = 2,
    Slot = 3,
    Chinchiro = 4
}

public enum DebugCoinPattern
{
    Random = 0,
    AllFront = 1,
    AllBack = 2,
    AlternatingFrontFirst = 3,
    AlternatingBackFirst = 4,
    Custom = 5
}

public enum DebugChinchiroPattern
{
    Random = 0,
    None = 1,
    Arashi = 2,
    Shigoro = 3,
    Moku = 4,
    Blank = 5,
    Hifumi = 6
}

/// <summary>
/// 개발 중 특정 캐릭터의 실제 전투 굴림만 교체하는 런타임 디버그 컴포넌트다.
///
/// - 비활성 또는 Original이면 기존 Skill / SkillRollData를 전혀 건드리지 않는다.
/// - 활성 상태에서는 캐릭터가 사용하는 모든 스킬 굴림을 선택한 Resolver로 대체한다.
/// - System.Random을 사용하므로 UnityEngine.Random의 전투 외 RNG 흐름을 오염시키지 않는다.
/// - Scene/Prefab 에셋의 SkillDefinition 값은 수정하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterRandomDebugOverride :
    MonoBehaviour
{
    [Header("Master")]
    public bool OverrideEnabled;

    public DebugRandomResolverMode Mode =
        DebugRandomResolverMode.Original;

    public bool UseSkillBasePower = true;
    public int OverrideBasePower;

    public bool DeterministicSequence = true;
    public int Seed = 1207;
    public bool LogActualRolls = true;

    [Header("Dice")]
    [Range(1, 8)]
    public int DiceCount = 1;

    public int DiceMin = 1;
    public int DiceMax = 6;

    public bool ForceDiceValue;
    public int ForcedDiceValue = 6;

    [Header("Coin")]
    [Range(1, 8)]
    public int CoinCount = 3;

    [Range(0f, 1f)]
    public float CoinFrontChance = 0.5f;

    public int CoinFrontValue = 2;
    public int CoinBackValue;

    public bool CoinFrontIsCritical;

    public DebugCoinPattern CoinPattern =
        DebugCoinPattern.Random;

    [Tooltip("Custom 패턴: F/B, 앞/뒤, 1/0을 사용할 수 있습니다. 예: FBF, 101")]
    public string CustomCoinPattern = "FBF";

    [Header("Slot")]
    [Range(1, 9)]
    public int SlotMinimum = 1;

    [Range(1, 9)]
    public int SlotMaximum = 9;

    public bool ForceSlotResult;
    [Range(1, 9)] public int ForcedSlotA = 7;
    [Range(1, 9)] public int ForcedSlotB = 7;

    [Header("Chinchiro")]
    public DebugChinchiroPattern ChinchiroPattern =
        DebugChinchiroPattern.Random;

    [Range(1, 6)]
    public int ChinchiroTripleFace = 6;

    [Range(1, 6)]
    public int ChinchiroMokuPairFace = 4;

    [Range(1, 6)]
    public int ChinchiroMokuSingleFace = 1;

    public int ChinchiroArashiPower = 24;
    public int ChinchiroShigoroPower = 20;
    public int ChinchiroMokuPower = 7;
    public int ChinchiroBlankPower = 3;
    public int ChinchiroHifumiPower = -4;
    [Min(0)] public int ChinchiroHifumiSelfDamage = 1;

    [Tooltip("켜면 목 조합의 위력은 ChinchiroMokuPower 대신 실제 짝 눈을 사용합니다.")]
    public bool UsePairFaceAsMokuPower;

    private int runtimeRollCounter;

    public int RuntimeRollCounter =>
        runtimeRollCounter;

    private void OnValidate()
    {
        Sanitize();
    }

    public void Sanitize()
    {
        DiceCount =
            Mathf.Clamp(
                DiceCount,
                1,
                8);

        int diceMinimum =
            Mathf.Min(
                DiceMin,
                DiceMax);

        int diceMaximum =
            Mathf.Max(
                DiceMin,
                DiceMax);

        DiceMin = diceMinimum;
        DiceMax = diceMaximum;

        ForcedDiceValue =
            Mathf.Clamp(
                ForcedDiceValue,
                DiceMin,
                DiceMax);

        CoinCount =
            Mathf.Clamp(
                CoinCount,
                1,
                8);

        CoinFrontChance =
            Mathf.Clamp01(
                CoinFrontChance);

        SlotMinimum =
            Mathf.Clamp(
                SlotMinimum,
                1,
                9);

        SlotMaximum =
            Mathf.Clamp(
                SlotMaximum,
                SlotMinimum,
                9);

        ForcedSlotA =
            Mathf.Clamp(
                ForcedSlotA,
                SlotMinimum,
                SlotMaximum);

        ForcedSlotB =
            Mathf.Clamp(
                ForcedSlotB,
                SlotMinimum,
                SlotMaximum);

        ChinchiroTripleFace =
            Mathf.Clamp(
                ChinchiroTripleFace,
                1,
                6);

        ChinchiroMokuPairFace =
            Mathf.Clamp(
                ChinchiroMokuPairFace,
                1,
                6);

        ChinchiroMokuSingleFace =
            Mathf.Clamp(
                ChinchiroMokuSingleFace,
                1,
                6);

        if (ChinchiroMokuSingleFace ==
            ChinchiroMokuPairFace)
        {
            ChinchiroMokuSingleFace =
                ChinchiroMokuPairFace == 6
                    ? 1
                    : ChinchiroMokuPairFace + 1;
        }

        ChinchiroHifumiSelfDamage =
            Mathf.Max(
                0,
                ChinchiroHifumiSelfDamage);

        CustomCoinPattern ??=
            string.Empty;
    }

    public void ResetSequence()
    {
        runtimeRollCounter = 0;
    }

    public static SkillResolverType InferResolverType(
        SkillResolver resolver)
    {
        if (resolver is CoinResolver)
            return SkillResolverType.Coin;

        if (resolver is SlotResolver)
            return SkillResolverType.Slot;

        if (resolver is ChinchiroResolver)
            return SkillResolverType.Chinchiro;

        return SkillResolverType.Dice;
    }

    public static bool TryCreateRoll(
        Character owner,
        Skill skill,
        SkillRollData rollData,
        SkillResolverType fallbackType,
        int exchangeIndex,
        out RollResult result)
    {
        result = null;

        if (owner == null)
            return false;

        CharacterRandomDebugOverride debug =
            owner.GetComponent<
                CharacterRandomDebugOverride>();

        if (debug == null ||
            !debug.OverrideEnabled ||
            debug.Mode ==
            DebugRandomResolverMode.Original)
        {
            return false;
        }

        debug.Sanitize();

        result =
            debug.BuildRoll(
                skill,
                rollData,
                fallbackType,
                exchangeIndex,
                previewSampleIndex: null);

        if (result == null)
            return false;

        if (debug.LogActualRolls)
        {
            Debug.Log(
                "[RandomDebug][ROLL] " +
                $"Owner={GetCharacterName(owner)}, " +
                $"Skill={skill?.SkillName ?? "NULL"}, " +
                $"Exchange={exchangeIndex}, " +
                $"Mode={debug.Mode}, " +
                $"Result={result.GetClashDetailDisplayText()}",
                owner);
        }

        return true;
    }

    public RollResult CreatePreview(
        Skill skill,
        int exchangeIndex,
        int sampleIndex = 0)
    {
        Sanitize();

        return BuildRoll(
            skill,
            skill?.GetRollData(exchangeIndex),
            skill?.Definition?.ResolverType ??
            InferResolverType(skill?.Resolver),
            exchangeIndex,
            Mathf.Max(0, sampleIndex));
    }

    public string GetSummary()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            OverrideEnabled
                ? "ON"
                : "OFF");

        builder.Append(" / ");
        builder.Append(Mode);

        builder.Append(" / Base=");
        builder.Append(
            UseSkillBasePower
                ? "Skill"
                : OverrideBasePower.ToString());

        switch (Mode)
        {
            case DebugRandomResolverMode.Dice:
                builder.Append(
                    $" / {DiceCount}D [{DiceMin}~{DiceMax}]");
                break;

            case DebugRandomResolverMode.Coin:
                builder.Append(
                    $" / Coin×{CoinCount} " +
                    $"F={CoinFrontValue} B={CoinBackValue} " +
                    $"P={CoinFrontChance:0.00}");
                break;

            case DebugRandomResolverMode.Slot:
                builder.Append(
                    $" / Slot [{SlotMinimum}~{SlotMaximum}]×2");
                break;

            case DebugRandomResolverMode.Chinchiro:
                builder.Append(
                    $" / {ChinchiroPattern}");
                break;
        }

        return builder.ToString();
    }

    private RollResult BuildRoll(
        Skill skill,
        SkillRollData rollData,
        SkillResolverType fallbackType,
        int exchangeIndex,
        int? previewSampleIndex)
    {
        if (Mode ==
            DebugRandomResolverMode.Original)
        {
            return null;
        }

        int basePower =
            ResolveDebugBasePower(
                skill,
                rollData);

        CombatRollType rollType =
            rollData?.Type ??
            skill?.GetRollType(exchangeIndex) ??
            CombatRollType.Attack;

        int judgmentModifier =
            rollData?.JudgmentModifier ?? 0;

        System.Random random =
            CreateRandom(
                exchangeIndex,
                previewSampleIndex);

        RollResult result =
            Mode switch
            {
                DebugRandomResolverMode.Coin =>
                    BuildCoinRoll(
                        random,
                        basePower),

                DebugRandomResolverMode.Slot =>
                    BuildSlotRoll(
                        random,
                        basePower),

                DebugRandomResolverMode.Chinchiro =>
                    BuildChinchiroRoll(
                        random,
                        basePower),

                _ =>
                    BuildDiceRoll(
                        random,
                        basePower)
            };

        if (result == null)
            return null;

        result.RollIndex =
            Mathf.Max(
                0,
                exchangeIndex);

        result.RollType =
            rollType;

        result.JudgmentModifier =
            judgmentModifier;

        result.DebugOverrideApplied = true;
        result.DebugSource =
            $"{GetCharacterName(GetComponent<Character>())}:{Mode}";

        result.RecalculateClashPower();

        return result;
    }

    private int ResolveDebugBasePower(
        Skill skill,
        SkillRollData rollData)
    {
        if (!UseSkillBasePower)
            return OverrideBasePower;

        // 공용 Dice의 AbsoluteRange는 Min~Max 자체가 최종 위력이다.
        // 디버그 Dice 강제 굴림에서도 스킬 BasePower를 중복 가산하지 않는다.
        if (Mode == DebugRandomResolverMode.Dice &&
            rollData != null &&
            rollData.DiceMode == DicePowerMode.AbsoluteRange)
        {
            return 0;
        }

        return skill?.BasePower ?? 0;
    }

    private System.Random CreateRandom(
        int exchangeIndex,
        int? previewSampleIndex)
    {
        int sequence =
            previewSampleIndex ??
            runtimeRollCounter++;

        int seed;

        if (DeterministicSequence)
        {
            seed =
                unchecked(
                    Seed * 486187739 +
                    sequence * 16777619 +
                    exchangeIndex * 397 +
                    GetInstanceID());
        }
        else
        {
            seed =
                unchecked(
                    Environment.TickCount *
                    31 +
                    sequence *
                    486187739 +
                    GetInstanceID());
        }

        return new System.Random(seed);
    }

    private RollResult BuildDiceRoll(
        System.Random random,
        int basePower)
    {
        int count =
            Mathf.Clamp(
                DiceCount,
                1,
                8);

        int sum = 0;

        RollResult result =
            CreateBaseResult(
                SkillResolverType.Dice,
                basePower);

        result.DiceMin = DiceMin;
        result.DiceMax = DiceMax;

        for (int index = 0;
             index < count;
             index++)
        {
            int value =
                ForceDiceValue
                    ? ForcedDiceValue
                    : random.Next(
                        DiceMin,
                        DiceMax + 1);

            result.DiceValues.Add(value);
            sum += value;
        }

        result.RawValue = sum;
        result.ModifiedValue = sum;
        result.IsMax =
            sum >=
            DiceMax * count;

        result.IsCritical = false;
        result.RecalculateFinalPower();

        return result;
    }

    private RollResult BuildCoinRoll(
        System.Random random,
        int basePower)
    {
        int count =
            Mathf.Clamp(
                CoinCount,
                1,
                8);

        int sum = 0;
        int frontCount = 0;

        RollResult result =
            CreateBaseResult(
                SkillResolverType.Coin,
                basePower);

        for (int index = 0;
             index < count;
             index++)
        {
            bool front =
                ResolveCoinFace(
                    random,
                    index);

            int value =
                front
                    ? CoinFrontValue
                    : CoinBackValue;

            result.CoinFaces.Add(front);
            result.CoinValues.Add(value);

            sum += value;

            if (front)
                frontCount++;
        }

        result.RawValue = sum;
        result.ModifiedValue = sum;

        int maximumContribution =
            count *
            Mathf.Max(
                CoinFrontValue,
                CoinBackValue);

        result.IsMax =
            sum >=
            maximumContribution;

        result.IsCritical =
            CoinFrontIsCritical &&
            frontCount > 0;

        result.RecalculateFinalPower();

        return result;
    }

    private bool ResolveCoinFace(
        System.Random random,
        int index)
    {
        switch (CoinPattern)
        {
            case DebugCoinPattern.AllFront:
                return true;

            case DebugCoinPattern.AllBack:
                return false;

            case DebugCoinPattern.AlternatingFrontFirst:
                return index % 2 == 0;

            case DebugCoinPattern.AlternatingBackFirst:
                return index % 2 != 0;

            case DebugCoinPattern.Custom:
            {
                List<bool> custom =
                    ParseCoinPattern(
                        CustomCoinPattern);

                if (custom.Count > 0)
                {
                    return
                        custom[
                            index %
                            custom.Count];
                }

                break;
            }
        }

        return
            random.NextDouble() <
            CoinFrontChance;
    }

    private RollResult BuildSlotRoll(
        System.Random random,
        int basePower)
    {
        int minimum =
            Mathf.Clamp(
                SlotMinimum,
                1,
                9);

        int maximum =
            Mathf.Clamp(
                SlotMaximum,
                minimum,
                9);

        int a =
            ForceSlotResult
                ? Mathf.Clamp(
                    ForcedSlotA,
                    minimum,
                    maximum)
                : random.Next(
                    minimum,
                    maximum + 1);

        int b =
            ForceSlotResult
                ? Mathf.Clamp(
                    ForcedSlotB,
                    minimum,
                    maximum)
                : random.Next(
                    minimum,
                    maximum + 1);

        int value = a * b;

        RollResult result =
            CreateBaseResult(
                SkillResolverType.Slot,
                basePower);

        result.SlotA = a;
        result.SlotB = b;
        result.SlotValue = value;

        result.RawValue = value;
        result.ModifiedValue = value;
        result.IsMax =
            value >=
            maximum * maximum;

        result.IsCritical =
            a == maximum &&
            b == maximum;

        result.RecalculateFinalPower();

        return result;
    }

    private RollResult BuildChinchiroRoll(
        System.Random random,
        int basePower)
    {
        int a;
        int b;
        int c;

        ChinchiroCombination combination =
            ResolveChinchiroDice(
                random,
                out a,
                out b,
                out c);

        int value =
            ResolveChinchiroPower(
                combination,
                a,
                b,
                c);

        RollResult result =
            CreateBaseResult(
                SkillResolverType.Chinchiro,
                basePower);

        result.DiceMin = 1;
        result.DiceMax = 6;
        result.DiceValues.Add(a);
        result.DiceValues.Add(b);
        result.DiceValues.Add(c);

        result.ChinchiroCombination =
            combination;

        result.ChinchiroBonus =
            value;

        result.ChinchiroSelfDamage =
            combination ==
            ChinchiroCombination.Hifumi
                ? ChinchiroHifumiSelfDamage
                : 0;

        result.RawValue = value;
        result.ModifiedValue = value;

        result.IsMax =
            combination ==
            ChinchiroCombination.Arashi;

        result.IsCritical = false;
        result.RecalculateFinalPower();

        return result;
    }

    private ChinchiroCombination ResolveChinchiroDice(
        System.Random random,
        out int a,
        out int b,
        out int c)
    {
        DebugChinchiroPattern pattern =
            ChinchiroPattern;

        if (pattern ==
            DebugChinchiroPattern.Random)
        {
            a = random.Next(1, 7);
            b = random.Next(1, 7);
            c = random.Next(1, 7);

            return EvaluateChinchiro(
                a,
                b,
                c);
        }

        switch (pattern)
        {
            case DebugChinchiroPattern.Arashi:
                a = ChinchiroTripleFace;
                b = ChinchiroTripleFace;
                c = ChinchiroTripleFace;
                return ChinchiroCombination.Arashi;

            case DebugChinchiroPattern.Shigoro:
                a = 4;
                b = 5;
                c = 6;
                return ChinchiroCombination.Shigoro;

            case DebugChinchiroPattern.Moku:
                a = ChinchiroMokuPairFace;
                b = ChinchiroMokuPairFace;
                c = ChinchiroMokuSingleFace;
                return ChinchiroCombination.Moku;

            case DebugChinchiroPattern.Hifumi:
                a = 1;
                b = 2;
                c = 3;
                return ChinchiroCombination.Hifumi;

            case DebugChinchiroPattern.None:
                a = 2;
                b = 4;
                c = 6;
                return ChinchiroCombination.None;

            default:
                // 1·3·5는 짝도 없고 1·2·3 / 4·5·6도 아니다.
                a = 1;
                b = 3;
                c = 5;
                return ChinchiroCombination.Blank;
        }
    }

    private int ResolveChinchiroPower(
        ChinchiroCombination combination,
        int a,
        int b,
        int c)
    {
        switch (combination)
        {
            case ChinchiroCombination.Arashi:
                return ChinchiroArashiPower;

            case ChinchiroCombination.Shigoro:
                return ChinchiroShigoroPower;

            case ChinchiroCombination.Moku:
            {
                if (!UsePairFaceAsMokuPower)
                    return ChinchiroMokuPower;

                if (a == b || a == c)
                    return a;

                return b;
            }

            case ChinchiroCombination.Hifumi:
                return ChinchiroHifumiPower;

            case ChinchiroCombination.None:
                return 0;

            default:
                return ChinchiroBlankPower;
        }
    }

    private static ChinchiroCombination EvaluateChinchiro(
        int a,
        int b,
        int c)
    {
        int[] sorted =
        {
            a,
            b,
            c
        };

        Array.Sort(sorted);

        if (a == b &&
            b == c)
        {
            return ChinchiroCombination.Arashi;
        }

        if (sorted[0] == 4 &&
            sorted[1] == 5 &&
            sorted[2] == 6)
        {
            return ChinchiroCombination.Shigoro;
        }

        if (sorted[0] == 1 &&
            sorted[1] == 2 &&
            sorted[2] == 3)
        {
            return ChinchiroCombination.Hifumi;
        }

        if (a == b ||
            a == c ||
            b == c)
        {
            return ChinchiroCombination.Moku;
        }

        return ChinchiroCombination.Blank;
    }

    private static RollResult CreateBaseResult(
        SkillResolverType resolverType,
        int basePower)
    {
        RollResult result =
            new RollResult
            {
                ResolverType =
                    resolverType,

                BasePower =
                    basePower,

                RawValue = 0,
                ModifiedValue = 0,
                ExternalModifier = 0,

                SpeedModifier = 0,
                MomentumModifier = 0,
                PreparationModifier = 0
            };

        result.RecalculateFinalPower();
        return result;
    }

    private static List<bool> ParseCoinPattern(
        string pattern)
    {
        List<bool> result =
            new List<bool>();

        if (string.IsNullOrWhiteSpace(pattern))
            return result;

        foreach (char character in pattern)
        {
            switch (char.ToUpperInvariant(character))
            {
                case 'F':
                case '1':
                case '앞':
                    result.Add(true);
                    break;

                case 'B':
                case '0':
                case '뒤':
                    result.Add(false);
                    break;
            }
        }

        return result;
    }

    private static string GetCharacterName(
        Character character)
    {
        return
            character?.Data?.CharacterName ??
            character?.name ??
            "NULL";
    }
}