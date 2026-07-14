using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public class RollResult
{
    public SkillResolverType ResolverType;

    //--------------------------------
    // 순수 굴림 결과
    //--------------------------------

    public int BasePower;

    public int RawValue;
    public int ModifiedValue;

    // 장비/패시브처럼 순수 위력 자체를 변경하는 보정만 저장한다.
    // 속도와 기세 보정은 여기에 포함하지 않는다.
    public int ExternalModifier;

    // 피해 계산이 읽는 순수 위력.
    // BasePower + ModifiedValue + ExternalModifier
    public int FinalPower;

    //--------------------------------
    // 합 전용 결과
    //--------------------------------

    public int SpeedModifier;
    public int MomentumModifier;

    // FinalPower + SpeedModifier + MomentumModifier
    public int ClashPower;

    //--------------------------------
    // 판정 메타데이터
    //--------------------------------

    public bool IsMax;
    public bool IsCritical;
    public bool WasRerolled;

    // 이름이 더 명확한 읽기 전용 별칭.
    public bool Critical => IsCritical;

    public int DiceMin;
    public int DiceMax;
    public List<int> DiceValues = new();

    public List<bool> CoinFaces = new();

    public int SlotA;
    public int SlotB;
    public int SlotValue;

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

    // 기존 호출부 호환.
    // 이 메서드는 순수 FinalPower를 바꾸는 용도로만 사용해야 한다.
    // 합 전용 기세 보정은 SetClashModifiers를 사용한다.
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
        int momentumModifier)
    {
        SpeedModifier = speedModifier;
        MomentumModifier = momentumModifier;
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
            MomentumModifier;
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
        }

        return FinalPower.ToString();
    }

    public string GetDetailDisplayText()
    {
        string rollText =
            GetShortDisplayText();

        string criticalText =
            IsCritical
                ? " / [CRITICAL]"
                : string.Empty;

        return
            $"{rollText}\n" +
            $"<size=70%>{GetPurePowerBreakdown()}{criticalText}</size>";
    }

    public string GetClashDetailDisplayText()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.Append(GetShortDisplayText());
        builder.Append("\n<size=70%>");
        builder.Append(GetPurePowerBreakdown());

        AppendSignedModifier(
            builder,
            "속도",
            SpeedModifier);

        AppendSignedModifier(
            builder,
            "기세",
            MomentumModifier);

        builder.Append($" = 합 {ClashPower}");

        if (IsCritical)
            builder.Append(" / [CRITICAL]");

        builder.Append("</size>");

        return builder.ToString();
    }

    public string GetPurePowerBreakdown()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.Append($"기본 {BasePower} + 굴림 {ModifiedValue}");

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

        StringBuilder builder =
            new StringBuilder();

        builder.Append("🪙 ");

        for (int i = 0; i < CoinFaces.Count; i++)
        {
            builder.Append(
                CoinFaces[i]
                    ? "앞"
                    : "뒤");

            if (i < CoinFaces.Count - 1)
                builder.Append(" ");
        }

        return builder.ToString();
    }

    private string GetSlotDisplayText()
    {
        return $"🎰 {SlotA} × {SlotB} = {SlotValue}";
    }
}
