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

    public bool IsMax;
    public bool IsCritical;
    public bool WasRerolled;

    public int DiceMin;
    public int DiceMax;
    public List<int> DiceValues = new();

    public List<bool> CoinFaces = new();

    public int SlotA;
    public int SlotB;
    public int SlotValue;

    public void ApplyExternalFinalPower(int newFinalPower)
    {
        int diff =
            newFinalPower - FinalPower;

        ExternalModifier += diff;
        FinalPower = newFinalPower;
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

        string modifierText =
            "";

        if (ExternalModifier != 0)
        {
            modifierText =
                ExternalModifier > 0
                    ? $" + {ExternalModifier}"
                    : $" - {Math.Abs(ExternalModifier)}";
        }

        return $"{rollText}\n<size=70%>기본 {BasePower} + 굴림 {ModifiedValue}{modifierText} = {FinalPower}</size>";
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