using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleActionAnnounceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Placement")]
    [SerializeField] private Vector2 anchorMin = new(0.025f, 0.39f);
    [SerializeField] private Vector2 anchorMax = new(0.365f, 0.605f);
    [SerializeField, Min(10f)] private float fontSize = 22f;

    [Header("Fade")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

    private void Awake()
    {
        ResolveReferences();
        ApplyPresentationSettings();
        HideImmediate();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyPresentationSettings();
    }

    public IEnumerator ShowPersistent(
        BattleVisualRequest request)
    {
        if (request == null)
            yield break;

        if (request.HasClashSequence)
        {
            HideImmediate();
            yield break;
        }

        SetText(BuildActionText(request));
        yield return FadeTo(1f);
    }

    public IEnumerator Hide()
    {
        yield return FadeTo(0f);
    }

    private void ResolveReferences()
    {
        if (actionText == null)
            actionText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    private void ApplyPresentationSettings()
    {
        RectTransform rect = transform as RectTransform;

        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
        }

        if (actionText == null)
            return;

        actionText.richText = true;
        actionText.enableAutoSizing = false;
        actionText.fontSize = fontSize;
        actionText.alignment = TextAlignmentOptions.Left;
        actionText.textWrappingMode =
            TextWrappingModes.Normal;
        actionText.overflowMode = TextOverflowModes.Overflow;
        actionText.raycastTarget = false;
        actionText.margin = new Vector4(14f, 8f, 14f, 8f);
    }

    private void SetText(string text)
    {
        if (actionText == null)
            return;

        actionText.text = text;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(elapsed / fadeDuration);

            canvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    t);

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    public void HideImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private string BuildActionText(
        BattleVisualRequest request)
    {
        BattleAction action = request.SourceAction;
        BattleAction opponent = request.OpponentAction;

        if (action == null)
            return "<color=#FCA5A5>행동 정보를 불러오지 못했습니다.</color>";

        string ownerName = GetCharacterName(action.Owner, "행동자");
        string targetName = GetCharacterName(action.Target, "대상");
        string ownerPart = GetPartName(action.OwnerPart);
        string targetPart = GetPartName(action.TargetPart);
        string skillName = GetSkillName(action.Skill);
        string skillColor = GetActionColor(action.Skill);
        string speedText = $"<color=#93C5FD>SPD {action.Speed}</color>";

        if (opponent == null)
        {
            return
                $"<color=#60A5FA><b>{ownerName}</b></color> · {ownerPart}\n" +
                $"<color={skillColor}><b>{skillName}</b></color>  {speedText}\n" +
                $"<color=#CBD5E1>→</color> <color=#FCA5A5>{targetName}</color> · {targetPart}  " +
                $"<color=#A7F3D0>일방 행동</color>";
        }

        string opponentName = GetCharacterName(opponent.Owner, "상대");
        string opponentPart = GetPartName(opponent.OwnerPart);
        string opponentSkill = GetSkillName(opponent.Skill);
        string opponentColor = GetActionColor(opponent.Skill);

        return
            "<color=#FBBF24><b>합 진행</b></color>\n" +
            $"<color=#60A5FA><b>{ownerName}</b></color> · {ownerPart}  " +
            $"<color={skillColor}>{skillName}</color>  {speedText}\n" +
            $"<color=#F87171><b>{opponentName}</b></color> · {opponentPart}  " +
            $"<color={opponentColor}>{opponentSkill}</color>  " +
            $"<color=#FCA5A5>SPD {opponent.Speed}</color>";
    }

    private static string GetCharacterName(
        Character character,
        string fallback)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               fallback;
    }

    private static string GetPartName(BodyPart part)
    {
        if (part == null)
            return "전신";

        return part.Type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼손",
            PartType.RIGHT_HAND => "오른손",
            PartType.LEGS => "다리",
            _ => part.Type.ToString()
        };
    }

    private static string GetSkillName(Skill skill)
    {
        return string.IsNullOrWhiteSpace(skill?.SkillName)
            ? "행동"
            : skill.SkillName;
    }

    private static string GetActionColor(Skill skill)
    {
        if (skill == null)
            return "#E2E8F0";

        return skill.ActionType switch
        {
            ActionType.NormalAttack => "#7DD3FC",
            ActionType.Duel => "#FB7185",
            ActionType.Preparation => "#C4B5FD",
            ActionType.Prestige => "#FBBF24",
            _ => "#E2E8F0"
        };
    }
}