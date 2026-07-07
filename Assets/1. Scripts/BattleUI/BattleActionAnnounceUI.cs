using System.Collections;
using TMPro;
using UnityEngine;

public class BattleActionAnnounceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.15f;

    private void Awake()
    {
        if (actionText == null)
            actionText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        HideImmediate();
    }

    public IEnumerator ShowPersistent(
        BattleVisualRequest request)
    {
        if (request == null)
            yield break;

        SetText(
            BuildActionText(request));

        yield return FadeTo(1f);
    }

    public IEnumerator Hide()
    {
        yield return FadeTo(0f);
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

        float startAlpha =
            canvasGroup.alpha;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

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

    private void HideImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private string BuildActionText(
        BattleVisualRequest request)
    {
        BattleAction action =
            request.SourceAction;

        BattleAction opponent =
            request.OpponentAction;

        if (action == null)
            return "NULL ACTION";

        string ownerName =
            action.Owner != null
                ? action.Owner.Data.CharacterName
                : "NULL_OWNER";

        string targetName =
            action.Target != null
                ? action.Target.Data.CharacterName
                : "NULL_TARGET";

        string ownerPart =
            action.OwnerPart != null
                ? action.OwnerPart.Type.ToString()
                : "NULL_PART";

        string targetPart =
            action.TargetPart != null
                ? action.TargetPart.Type.ToString()
                : "NULL_TARGET_PART";

        string skillName =
            action.Skill != null
                ? action.Skill.SkillName
                : "NULL_SKILL";

        int speed =
            action.Speed;

        string actionType =
            action.Skill != null
                ? action.Skill.ActionType.ToString()
                : "NULL_TYPE";

        if (opponent == null)
        {
            return
                $"{ownerName}의 {skillName}\n" +
                $"속도 {speed} / {ownerPart} → {targetName} {targetPart}\n" +
                $"대응 행동 없음\n" +
                $"[{actionType}]";
        }

        string opponentName =
            opponent.Owner != null
                ? opponent.Owner.Data.CharacterName
                : "NULL_OPPONENT";

        string opponentPart =
            opponent.OwnerPart != null
                ? opponent.OwnerPart.Type.ToString()
                : "NULL_OPPONENT_PART";

        string opponentSkill =
            opponent.Skill != null
                ? opponent.Skill.SkillName
                : "NULL_OPPONENT_SKILL";

        int opponentSpeed =
            opponent.Speed;

        return
            $"합 진행\n" +
            $"{ownerName} / {ownerPart}\n" +
            $"{skillName}  속도 {speed}\n" +
            $"VS\n" +
            $"{opponentName} / {opponentPart}\n" +
            $"{opponentSkill}  속도 {opponentSpeed}\n" +
            $"대상 : {targetName} {targetPart}";
    }
}