using UnityEngine;

public readonly struct GameplayV5EmotionTheme
{
    public GameplayV5EmotionTheme(
        string displayName,
        Color accent,
        Color softAccent)
    {
        DisplayName = displayName;
        Accent = accent;
        SoftAccent = softAccent;
    }

    public string DisplayName { get; }
    public Color Accent { get; }
    public Color SoftAccent { get; }
}

public static class GameplayV5UiPresentation
{
    public static GameplayV5EmotionTheme GetEmotionTheme(
        EmotionType emotion)
    {
        return emotion switch
        {
            EmotionType.Awe => new GameplayV5EmotionTheme(
                "경외",
                new Color(0.68f, 0.45f, 1f, 1f),
                new Color(0.30f, 0.17f, 0.48f, 1f)),

            EmotionType.Faith => new GameplayV5EmotionTheme(
                "신의",
                new Color(1f, 0.78f, 0.28f, 1f),
                new Color(0.48f, 0.34f, 0.08f, 1f)),

            EmotionType.Admiration => new GameplayV5EmotionTheme(
                "동경",
                new Color(1f, 0.42f, 0.70f, 1f),
                new Color(0.48f, 0.16f, 0.30f, 1f)),

            EmotionType.Impression => new GameplayV5EmotionTheme(
                "감복",
                new Color(0.32f, 0.84f, 1f, 1f),
                new Color(0.10f, 0.36f, 0.47f, 1f)),

            EmotionType.Compassion => new GameplayV5EmotionTheme(
                "연민",
                new Color(0.42f, 0.90f, 0.52f, 1f),
                new Color(0.14f, 0.40f, 0.20f, 1f)),

            EmotionType.Longing => new GameplayV5EmotionTheme(
                "그리움",
                new Color(0.42f, 0.62f, 1f, 1f),
                new Color(0.14f, 0.24f, 0.48f, 1f)),

            EmotionType.Detachment => new GameplayV5EmotionTheme(
                "초연",
                new Color(0.78f, 0.84f, 0.92f, 1f),
                new Color(0.30f, 0.34f, 0.42f, 1f)),

            _ => new GameplayV5EmotionTheme(
                emotion.ToString(),
                Color.white,
                new Color(0.24f, 0.24f, 0.28f, 1f))
        };
    }

    public static string GetMomentumStateName(
        MomentumState state)
    {
        return state switch
        {
            MomentumState.LastStand => "발악 / 짓눌림",
            MomentumState.Disadvantage => "열세",
            MomentumState.Balance => "균형",
            MomentumState.Advantage => "우세",
            MomentumState.Overwhelm => "짓누름",
            _ => state.ToString()
        };
    }

    public static string BuildFervorPips(
        int level,
        int maximumLevel = 3)
    {
        int max = Mathf.Max(0, maximumLevel);
        int current = Mathf.Clamp(level, 0, max);

        System.Text.StringBuilder builder = new();

        for (int i = 0; i < max; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(i < current ? '◆' : '◇');
        }

        return builder.ToString();
    }
}
