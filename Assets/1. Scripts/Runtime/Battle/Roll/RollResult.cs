using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public class RollResult
{
    public SkillResolverType ResolverType;

    public int BasePower;
    public int RawValue;
    public int ModifiedValue;
    public int ExternalModifier;
    public int FinalPower;

    public int SpeedModifier;
    public int MomentumModifier;
    public int PreparationModifier;
    public int ClashPower;

    public bool IsMax;
    public bool IsCritical;
    public bool WasRerolled;
    public bool WasReused;

    public bool Critical => IsCritical;

    public int DiceMin;
    public int DiceMax;
    public List<int> DiceValues = new();

    public List<bool> CoinFaces = new();
    public List<int> CoinValues = new();

    public int SlotA;
    public int SlotB;
    public int SlotValue;

    public ChinchiroCombination ChinchiroCombination;
    public int ChinchiroBonus;
    public int ChinchiroSelfDamage;

    public void SetModifiedValue(int modifiedValue)
    {
        ModifiedValue = modifiedValue;
        RecalculateFinalPower();
    }

    public void ApplyExternalModifier(int modifier)
    {
        ExternalModifier += modifier;
        RecalculateFinalPower();
    }

    public void ApplyExternalFinalPower(int newFinalPower)
    {
        ExternalModifier =
            newFinalPower -
            (BasePower + ModifiedValue);

        RecalculateFinalPower();
    }

    public void RecalculateFinalPower()
    {
        FinalPower =
            BasePower +
            ModifiedValue +
            ExternalModifier;

        RecalculateClashPower();
    }

    public void SetClashModifiers(
        int speedModifier,
        int momentumModifier,
        int preparationModifier = 0)
    {
        SpeedModifier = speedModifier;
        MomentumModifier = momentumModifier;
        PreparationModifier = preparationModifier;
        RecalculateClashPower();
    }

    public void ClearClashModifiers()
    {
        SetClashModifiers(0, 0);
    }

    public void RecalculateClashPower()
    {
        ClashPower =
            FinalPower +
            SpeedModifier +
            MomentumModifier +
            PreparationModifier;
    }

    public RollResult Clone()
    {
        RollResult clone = (RollResult)MemberwiseClone();
        clone.DiceValues = DiceValues == null
            ? new List<int>()
            : new List<int>(DiceValues);
        clone.CoinFaces = CoinFaces == null
            ? new List<bool>()
            : new List<bool>(CoinFaces);
        clone.CoinValues = CoinValues == null
            ? new List<int>()
            : new List<int>(CoinValues);
        return clone;
    }

    public string GetShortDisplayText()
    {
        switch (ResolverType)
        {
            case SkillResolverType.Dice:
                return GetDiceDisplayText();

            case SkillResolverType.Coin:
                return GetCoinDisplayText();

            case SkillResolverType.Slot:
                return GetSlotDisplayText();

            case SkillResolverType.Chinchiro:
                return GetChinchiroDisplayText();
        }

        return FinalPower.ToString();
    }

    public string GetDetailDisplayText()
    {
        string criticalText = IsCritical
            ? " / [CRITICAL]"
            : string.Empty;

        string reuseText = WasReused
            ? " / [REUSE]"
            : string.Empty;

        return
            $"{GetShortDisplayText()}\n" +
            $"<size=70%>{GetPurePowerBreakdown()}" +
            $"{criticalText}{reuseText}</size>";
    }

    public string GetClashDetailDisplayText()
    {
        StringBuilder builder = new();

        builder.Append(GetShortDisplayText());
        builder.Append("\n<size=70%>");
        builder.Append(GetPurePowerBreakdown());

        AppendSignedModifier(
            builder,
            "속도",
            SpeedModifier);

        // 새 설계에서 기세는 합 수치 보정이 아니다.
        if (MomentumModifier != 0)
        {
            AppendSignedModifier(
                builder,
                "기세(구식)",
                MomentumModifier);
        }

        AppendSignedModifier(
            builder,
            "도사림",
            PreparationModifier);

        builder.Append($" = 합 {ClashPower}");

        if (IsCritical)
            builder.Append(" / [CRITICAL]");

        if (WasReused)
            builder.Append(" / [REUSE]");

        builder.Append("</size>");
        return builder.ToString();
    }

    public string GetPurePowerBreakdown()
    {
        StringBuilder builder = new();

        builder.Append(
            $"기본 {BasePower} + 굴림 {ModifiedValue}");

        AppendSignedModifier(
            builder,
            "보정",
            ExternalModifier);

        builder.Append($" = 순수 {FinalPower}");
        return builder.ToString();
    }

    private static void AppendSignedModifier(
        StringBuilder builder,
        string label,
        int value)
    {
        if (builder == null || value == 0)
            return;

        if (value > 0)
            builder.Append($" + {label} {value}");
        else
            builder.Append($" - {label} {Math.Abs(value)}");
    }

    private string GetDiceDisplayText()
    {
        if (DiceValues == null || DiceValues.Count == 0)
            return $"🎲 {RawValue}";

        if (DiceValues.Count == 1)
            return $"🎲 {DiceValues[0]}";

        return $"🎲 {string.Join(" + ", DiceValues)}";
    }

    private string GetCoinDisplayText()
    {
        if (CoinFaces == null || CoinFaces.Count == 0)
            return $"🪙 {RawValue}";

        StringBuilder builder = new();
        builder.Append("🪙 ");

        for (int i = 0; i < CoinFaces.Count; i++)
        {
            builder.Append(CoinFaces[i] ? "앞" : "뒤");

            if (CoinValues != null &&
                i < CoinValues.Count)
            {
                builder.Append($"({CoinValues[i]})");
            }

            if (i < CoinFaces.Count - 1)
                builder.Append(" ");
        }

        return builder.ToString();
    }

    private string GetSlotDisplayText()
    {
        return $"🎰 {SlotA} × {SlotB} = {SlotValue}";
    }

    private string GetChinchiroDisplayText()
    {
        string diceText = DiceValues == null
            ? string.Empty
            : string.Join("·", DiceValues);

        return
            $"🎲 {diceText} / " +
            $"{ChinchiroCombination} " +
            $"({ChinchiroBonus:+#;-#;0})";
    }
}
