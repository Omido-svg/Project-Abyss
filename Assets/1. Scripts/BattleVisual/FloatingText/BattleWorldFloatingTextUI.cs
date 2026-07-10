using System.Collections;
using TMPro;
using UnityEngine;

public class BattleWorldFloatingTextUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private Camera worldCamera;

    private Transform followTarget;
    private Vector3 worldOffset;
    private float cameraRightOffset;

    private void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (text != null)
        {
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
        }
    }

    private void LateUpdate()
    {
        FollowTarget();
        FaceCamera();
    }

    public void Initialize(
        Transform target,
        Camera camera,
        Vector3 offset,
        float rightOffset,
        string initialText,
        Color color,
        int sortingOrder)
    {
        followTarget = target;
        worldCamera = camera;
        worldOffset = offset;
        cameraRightOffset = rightOffset;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        SetText(initialText);
        SetColor(color);

        FollowTarget();
        FaceCamera();
    }

    public void SetText(string value)
    {
        if (text == null)
            return;

        text.text = value;
    }

    public void SetColor(Color color)
    {
        if (text == null)
            return;

        text.color = color;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
    }

    public IEnumerator RollToValue(
        int finalValue,
        float rollDuration,
        float tickInterval,
        int randomMin,
        int randomMax)
    {
        SetAlpha(1f);

        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;

                int randomValue =
                    Random.Range(randomMin, randomMax + 1);

                SetText(randomValue.ToString());
            }

            yield return null;
        }

        SetText(finalValue.ToString());
    }
    
    public IEnumerator RollToClashResult(
        RollResult rollResult,
        int finalClashValue,
        int speedModifier,
        float rollDuration,
        float tickInterval,
        int randomMin,
        int randomMax)
    {
        Debug.Log(
            $"[FloatingTextUI] RollToClashResult / " +
            $"Type={rollResult?.ResolverType}, " +
            $"Display={rollResult?.GetShortDisplayText()}, " +
            $"FinalPower={rollResult?.FinalPower}, " +
            $"FinalClash={finalClashValue}, " +
            $"SpeedModifier={speedModifier}");
            
        if (rollResult == null)
        {
            yield return RollToValue(
                finalClashValue,
                rollDuration,
                tickInterval,
                randomMin,
                randomMax);

            yield break;
        }

        SetAlpha(1f);

        float elapsed =
            0f;

        float tickTimer =
            0f;

        while (elapsed < rollDuration)
        {
            elapsed +=
                Time.deltaTime;

            tickTimer +=
                Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer =
                    0f;

                SetText(
                    CreateRollingPreviewText(
                        rollResult,
                        randomMin,
                        randomMax));
            }

            yield return null;
        }

        SetText(
            CreateFinalRollText(
                rollResult,
                finalClashValue,
                speedModifier));
    }
    
    private string CreateRollingPreviewText(
        RollResult rollResult,
        int randomMin,
        int randomMax)
    {
        if (rollResult == null)
            return "?";

        switch (rollResult.ResolverType)
        {
            case SkillResolverType.Dice:
            {
                int min =
                    rollResult.DiceMin;

                int max =
                    rollResult.DiceMax;

                if (max < min)
                {
                    min = randomMin;
                    max = randomMax;
                }

                int value =
                    Random.Range(
                        min,
                        max + 1);

                return $"🎲 {value}";
            }

            case SkillResolverType.Coin:
            {
                int count =
                    rollResult.CoinFaces != null &&
                    rollResult.CoinFaces.Count > 0
                        ? rollResult.CoinFaces.Count
                        : 1;

                string text =
                    "🪙 ";

                for (int i = 0; i < count; i++)
                {
                    text +=
                        Random.value >= 0.5f
                            ? "앞"
                            : "뒤";

                    if (i < count - 1)
                        text += " ";
                }

                return text;
            }

            case SkillResolverType.Slot:
            {
                int a =
                    Random.Range(
                        1,
                        10);

                int b =
                    Random.Range(
                        1,
                        10);

                return $"🎰 {a} × {b}";
            }
        }

        return Random.Range(
                randomMin,
                randomMax + 1)
            .ToString();
    }
    
    private string CreateFinalRollText(
        RollResult rollResult,
        int finalClashValue,
        int speedModifier)
    {
        if (rollResult == null)
            return finalClashValue.ToString();

        string baseText =
            rollResult.GetShortDisplayText();

        string powerText =
            $"기본 {rollResult.BasePower} + 굴림 {rollResult.ModifiedValue}";

        if (rollResult.ExternalModifier != 0)
        {
            powerText +=
                rollResult.ExternalModifier > 0
                    ? $" + 보정 {rollResult.ExternalModifier}"
                    : $" - 보정 {Mathf.Abs(rollResult.ExternalModifier)}";
        }

        powerText +=
            $" = {rollResult.FinalPower}";

        if (speedModifier != 0)
        {
            powerText +=
                speedModifier > 0
                    ? $" / 속도 +{speedModifier}"
                    : $" / 속도 {speedModifier}";
        }

        if (finalClashValue != rollResult.FinalPower)
        {
            powerText +=
                $" / 합 {finalClashValue}";
        }

        return $"{baseText}\n<size=70%>{powerText}</size>";
    }

    public IEnumerator FadeAndDestroy(float fadeDuration)
    {
        float fadeElapsed = 0f;

        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(fadeElapsed / fadeDuration);

            SetAlpha(1f - t);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void FollowTarget()
    {
        if (followTarget == null)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        Vector3 cameraRight =
            worldCamera != null
                ? worldCamera.transform.right
                : Vector3.right;

        transform.position =
            followTarget.position +
            worldOffset +
            cameraRight * cameraRightOffset;
    }

    private void FaceCamera()
    {
        if (!faceCamera)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return;

        transform.rotation =
            worldCamera.transform.rotation *
            Quaternion.Euler(rotationOffset);
    }
}