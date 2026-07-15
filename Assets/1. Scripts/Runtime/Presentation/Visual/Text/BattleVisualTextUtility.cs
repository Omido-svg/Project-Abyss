using TMPro;

public enum BattleVisualIconMode
{
    PlainText,
    UnicodeEmoji,
    TmpSpriteAsset
}

public static class BattleVisualTextUtility
{
    public static string FormatRollText(
        string source,
        BattleVisualIconMode mode,
        string diceSpriteName,
        string coinSpriteName,
        string slotSpriteName)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        return mode switch
        {
            BattleVisualIconMode.UnicodeEmoji => source,
            BattleVisualIconMode.TmpSpriteAsset => source
                .Replace("🎲", BuildSpriteTag(diceSpriteName, "[DICE]"))
                .Replace("🪙", BuildSpriteTag(coinSpriteName, "[COIN]"))
                .Replace("🎰", BuildSpriteTag(slotSpriteName, "[SLOT]")),
            _ => source
                .Replace("🎲", "[DICE]")
                .Replace("🪙", "[COIN]")
                .Replace("🎰", "[SLOT]")
        };
    }

    public static void ConfigureTmp(
        TMP_Text text,
        TMP_SpriteAsset spriteAsset,
        BattleVisualIconMode mode)
    {
        if (text == null)
            return;

        text.richText = true;

        if (mode == BattleVisualIconMode.TmpSpriteAsset && spriteAsset != null)
            text.spriteAsset = spriteAsset;
    }

    private static string BuildSpriteTag(string spriteName, string fallback)
    {
        return string.IsNullOrWhiteSpace(spriteName)
            ? fallback
            : $"<sprite name=\"{spriteName}\">";
    }
}
