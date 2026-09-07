using System.Text;

public enum BattleStatusDisposition
{
    Neutral = 0,
    Beneficial = 1,
    Harmful = 2
}

/// <summary>
/// Character Detail의 상태 탭에서 런타임 StatusEffect를
/// "무엇이 / 누가 / 어디에 / 얼마나" 걸었는지 읽기 쉬운 TMP RichText로 변환한다.
/// 전투 규칙 자체에는 관여하지 않는다.
/// </summary>
public static class BattleStatusUiText
{
    public static string BuildListLabel(
        StatusEffect effect,
        Character viewedCharacter)
    {
        if (effect == null)
            return "상태 효과";

        string displayName =
            GetDisplayName(effect);

        string scope =
            GetScopeLabel(effect);

        string source =
            GetSourceCompactLabel(
                effect,
                viewedCharacter);

        string category =
            GetCategoryRichText(effect);

        string stack =
            effect.Stack > 0
                ? $" ×{effect.Stack}"
                : string.Empty;

        return
            $"{category} {ColorizeStatusName(effect, displayName)}{stack}\n" +
            $"<size=78%>{source}  ·  {scope}  ·  {FormatDuration(effect)}</size>";
    }

    public static string BuildDescription(
        StatusEffect effect,
        Character viewedCharacter)
    {
        if (effect == null)
            return "설명이 없습니다.";

        string displayName =
            GetDisplayName(effect);

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            $"{GetCategoryRichText(effect)}  " +
            $"{ColorizeStatusName(effect, displayName)}");

        builder.AppendLine();
        builder.AppendLine(
            $"<color=#AEB8C8>적용 대상</color>  {GetScopeLabel(effect)}");

        builder.AppendLine(
            $"<color=#AEB8C8>부여 출처</color>  {GetSourceLongLabel(effect, viewedCharacter)}");

        if (effect.SourcePart != null)
        {
            builder.AppendLine(
                $"<color=#AEB8C8>발생 부위</color>  {GetPartName(effect.SourcePart.Type)}");
        }

        builder.AppendLine(
            $"<color=#AEB8C8>현재 스택</color>  {effect.Stack}");

        builder.AppendLine(
            $"<color=#AEB8C8>남은 지속</color>  {FormatDuration(effect)}");

        builder.AppendLine();
        builder.AppendLine("<b>현재 효과</b>");
        builder.AppendLine(
            BattleKeywordGlossary.ColorizeText(
                GetStatusDescription(
                    effect,
                    displayName)));

        builder.AppendLine();
        builder.AppendLine(
            $"<size=82%><color=#7F8998>중첩 {effect.StackPolicy}  ·  지속 {effect.DurationPolicy}</color></size>");

        return builder
            .ToString()
            .TrimEnd();
    }

    public static BattleStatusDisposition GetDisposition(
        StatusEffect effect)
    {
        if (effect == null)
            return BattleStatusDisposition.Neutral;

        if (effect is StrengthStatus ||
            effect is SturdyStatus ||
            effect is ProtectionStatus ||
            effect is HeatStatus ||
            effect is RegenerationStatus)
        {
            return BattleStatusDisposition.Beneficial;
        }

        if (effect is WeaknessStatus ||
            effect is DisarmStatus ||
            effect is FractureStatus ||
            effect is RuptureStatus ||
            effect is PainStatus ||
            effect is SealedPartStatus ||
            effect is Burn ||
            effect is Bleeding ||
            effect is CrowdControlStatus ||
            effect is PartDisabledStatus ||
            effect is BrokenPartStatus ||
            effect is DamageStatus)
        {
            return BattleStatusDisposition.Harmful;
        }

        return BattleStatusDisposition.Neutral;
    }

    private static string ColorizeStatusName(
        StatusEffect effect,
        string displayName)
    {
        BattleKeywordTone tone =
            effect is IUniqueKeywordStatus
                ? BattleKeywordTone.Unique
                : GetDisposition(effect) switch
                {
                    BattleStatusDisposition.Beneficial =>
                        BattleKeywordTone.Beneficial,
                    BattleStatusDisposition.Harmful =>
                        BattleKeywordTone.Harmful,
                    _ =>
                        BattleKeywordTone.Neutral
                };

        return
            $"<color={BattleKeywordGlossary.GetColorHex(tone)}><b>{displayName}</b></color>";
    }

    public static string GetCategoryRichText(
        StatusEffect effect)
    {
        BattleStatusDisposition disposition =
            GetDisposition(effect);

        bool unique =
            effect is IUniqueKeywordStatus;

        string label =
            disposition switch
            {
                BattleStatusDisposition.Beneficial => "이로운",
                BattleStatusDisposition.Harmful => "해로운",
                _ => "상태"
            };

        if (unique)
            label = $"고유 · {label}";

        BattleKeywordTone tone =
            unique
                ? BattleKeywordTone.Unique
                : disposition switch
                {
                    BattleStatusDisposition.Beneficial =>
                        BattleKeywordTone.Beneficial,
                    BattleStatusDisposition.Harmful =>
                        BattleKeywordTone.Harmful,
                    _ =>
                        BattleKeywordTone.Neutral
                };

        return
            $"<color={BattleKeywordGlossary.GetColorHex(tone)}><b>[{label}]</b></color>";
    }

    public static string GetDisplayName(
        StatusEffect effect)
    {
        if (effect == null)
            return "상태 효과";

        if (effect is Bleeding)
            return "혈상";

        if (effect is Burn)
            return "화상";

        if (effect is Stun)
            return "기절";

        return
            string.IsNullOrWhiteSpace(effect.Name)
                ? effect.EffectName
                : effect.Name;
    }

    private static string GetStatusDescription(
        StatusEffect effect,
        string displayName)
    {
        if (effect is PartDisabledStatus)
        {
            return
                "해당 부위가 약화 상태입니다. 약화된 부위에서 발생하는 행동 제한/굴림 패널티가 적용됩니다.";
        }

        if (effect is BrokenPartStatus)
        {
            return
                "해당 부위가 파괴 상태입니다. 부위 파괴 규칙에 따라 행동/피해 처리에 제약이 적용됩니다.";
        }

        if (effect is SealedPartStatus)
        {
            return
                "해당 부위의 행동이 봉인되어 지속시간 동안 스킬을 사용할 수 없습니다.";
        }

        return
            BattleKeywordGlossary.GetDescription(
                displayName);
    }

    public static string FormatDuration(
        StatusEffect effect)
    {
        if (effect == null)
            return "-";

        if (effect.IsPermanent)
            return "영구";

        int duration =
            UnityEngine.Mathf.Max(
                0,
                effect.Duration);

        return $"{duration}턴";
    }

    private static string GetScopeLabel(
        StatusEffect effect)
    {
        if (effect?.OwnerPart != null)
            return $"부위 · {GetPartName(effect.OwnerPart.Type)}";

        if (effect?.SourcePart != null &&
            effect is PartDisabledStatus)
        {
            return $"부위 약화 · {GetPartName(effect.SourcePart.Type)}";
        }

        return "전신";
    }

    private static string GetSourceCompactLabel(
        StatusEffect effect,
        Character viewedCharacter)
    {
        Character source =
            effect?.Source;

        if (source == null)
            return "시스템";

        if (source == viewedCharacter)
            return "자기 부여";

        return
            $"{GetRelationship(viewedCharacter, source)} · {GetCharacterName(source)}";
    }

    private static string GetSourceLongLabel(
        StatusEffect effect,
        Character viewedCharacter)
    {
        Character source =
            effect?.Source;

        if (source == null)
            return "환경 / 시스템";

        if (source == viewedCharacter)
            return $"{GetCharacterName(source)} · 자기 부여";

        return
            $"{GetRelationship(viewedCharacter, source)} · {GetCharacterName(source)}";
    }

    private static string GetRelationship(
        Character viewedCharacter,
        Character source)
    {
        if (viewedCharacter == null ||
            source == null ||
            viewedCharacter.BattleContext == null)
        {
            return "외부";
        }

        BattleContext context =
            viewedCharacter.BattleContext;

        bool viewedPlayer =
            context.Player == viewedCharacter;

        bool sourcePlayer =
            context.Player == source;

        bool viewedEnemy =
            context.Enemies != null &&
            context.Enemies.Contains(viewedCharacter);

        bool sourceEnemy =
            context.Enemies != null &&
            context.Enemies.Contains(source);

        bool sameSide =
            viewedPlayer && sourcePlayer ||
            viewedEnemy && sourceEnemy;

        return sameSide
            ? "아군"
            : "적";
    }

    private static string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "알 수 없음";

        return
            character.Data?.CharacterName ??
            character.name ??
            "알 수 없음";
    }

    private static string GetPartName(
        PartType type)
    {
        return type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼팔",
            PartType.RIGHT_HAND => "오른팔",
            PartType.LEGS => "다리",
            _ => type.ToString()
        };
    }
}
