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
    [SerializeField]
    private bool faceCamera = true;

    [SerializeField]
    private Vector3 rotationOffset =
        Vector3.zero;

    [Header("Final Result Readability")]
    [SerializeField]
    private bool showOwnerLabel = true;

    [SerializeField, Range(40, 100)]
    private int ownerLabelSizePercent = 55;

    [SerializeField, Range(100, 220)]
    private int finalPowerSizePercent = 160;

    [SerializeField, Range(40, 100)]
    private int detailSizePercent = 62;

    [SerializeField, Min(1)]
    private int maximumOwnerLabelLength = 18;

    [Header("TMP Icon Compatibility")]
    [SerializeField] private BattleVisualIconMode iconMode = BattleVisualIconMode.PlainText;
    [SerializeField] private TMP_SpriteAsset iconSpriteAsset;
    [SerializeField] private string diceSpriteName = "dice";
    [SerializeField] private string coinSpriteName = "coin";
    [SerializeField] private string slotSpriteName = "slot";

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private Camera worldCamera;

    private Transform followTarget;
    private Transform opposingTarget;

    private Vector3 worldOffset;

    private bool useScreenSpaceLayout;
    private float fallbackSideSign;
    private float horizontalOffsetPixels;
    private float minimumPairSeparationPixels;
    private float verticalOffsetPixels;
    private bool keepInsideViewport;
    private float viewportPaddingPixels;

    private float legacyWorldRightOffset;

    private string ownerLabel;

    // Presentation 전용 난수. 전투 굴림의 UnityEngine.Random 상태를 소비하지 않는다.
    private System.Random visualRandom;

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

            BattleVisualTextUtility.ConfigureTmp(
                text,
                iconSpriteAsset,
                iconMode);
        }
    }

    private void LateUpdate()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        FollowTarget();
        FaceCamera();
    }

    // Legacy API.
    public void Initialize(
        Transform target,
        Camera camera,
        Vector3 offset,
        float rightOffset,
        string initialText,
        Color color,
        int sortingOrder)
    {
        InitializeForClash(
            target,
            null,
            camera,
            offset,
            useScreenLayout: false,
            fallbackSide:
                Mathf.Approximately(rightOffset, 0f)
                    ? 1f
                    : Mathf.Sign(rightOffset),
            horizontalPixels: 0f,
            minimumSeparationPixels: 0f,
            verticalPixels: 0f,
            clampToViewport: false,
            viewportPadding: 0f,
            fallbackWorldOffset:
                Mathf.Abs(rightOffset),
            label: null,
            initialText,
            color,
            sortingOrder);
    }

    public void InitializeForClash(
        Transform target,
        Transform otherTarget,
        Camera camera,
        Vector3 offset,
        bool useScreenLayout,
        float fallbackSide,
        float horizontalPixels,
        float minimumSeparationPixels,
        float verticalPixels,
        bool clampToViewport,
        float viewportPadding,
        float fallbackWorldOffset,
        string label,
        string initialText,
        Color color,
        int sortingOrder)
    {
        StopAllCoroutines();

        followTarget = target;
        opposingTarget = otherTarget;

        worldCamera = camera;
        worldOffset = offset;

        useScreenSpaceLayout =
            useScreenLayout;

        fallbackSideSign =
            Mathf.Approximately(
                fallbackSide,
                0f)
                ? 1f
                : Mathf.Sign(
                    fallbackSide);

        horizontalOffsetPixels =
            Mathf.Max(
                0f,
                horizontalPixels);

        minimumPairSeparationPixels =
            Mathf.Max(
                0f,
                minimumSeparationPixels);

        verticalOffsetPixels =
            Mathf.Max(
                0f,
                verticalPixels);

        keepInsideViewport =
            clampToViewport;

        viewportPaddingPixels =
            Mathf.Max(
                0f,
                viewportPadding);

        legacyWorldRightOffset =
            Mathf.Max(
                0f,
                fallbackWorldOffset) *
            fallbackSideSign;

        ownerLabel =
            NormalizeOwnerLabel(
                label);

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvas != null)
        {
            canvas.renderMode =
                RenderMode.WorldSpace;

            canvas.worldCamera =
                worldCamera;

            canvas.overrideSorting =
                true;

            canvas.sortingOrder =
                sortingOrder;
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

    public void SetText(
        string value)
    {
        if (text == null)
            return;

        string formatted =
            BattleVisualTextUtility.FormatRollText(
                value,
                iconMode,
                diceSpriteName,
                coinSpriteName,
                slotSpriteName);

        if (showOwnerLabel &&
            !string.IsNullOrEmpty(ownerLabel))
        {
            formatted =
                $"<size={ownerLabelSizePercent}%>" +
                $"<b>{ownerLabel}</b></size>\n" +
                formatted;
        }

        text.text = formatted;
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
                    NextVisualInt(randomMin, randomMax + 1);

                SetText(randomValue.ToString());
            }

            yield return null;
        }

        SetText(finalValue.ToString());
    }

    // 기존 호출부 호환.
    public IEnumerator RollToClashResult(
        RollResult rollResult,
        int finalClashValue,
        int speedModifier,
        float rollDuration,
        float tickInterval,
        int randomMin,
        int randomMax)
    {
        yield return RollToClashResult(
            rollResult,
            finalClashValue,
            speedModifier,
            rollResult?.MomentumModifier ?? 0,
            rollResult?.IsCritical ?? false,
            rollDuration,
            tickInterval,
            randomMin,
            randomMax);
    }

    public IEnumerator RollToClashResult(
        RollResult rollResult,
        int finalClashValue,
        int speedModifier,
        int momentumModifier,
        bool critical,
        float rollDuration,
        float tickInterval,
        int randomMin,
        int randomMax)
    {
        if (logDebug)
        {
            Debug.Log(
                $"[FloatingTextUI] RollToClashResult / " +
                $"Type={rollResult?.ResolverType}, " +
                $"Display={rollResult?.GetShortDisplayText()}, " +
                $"FinalPower={rollResult?.FinalPower}, " +
                $"SpeedModifier={speedModifier}, " +
                $"MomentumModifier={momentumModifier}, " +
                $"ClashPower={finalClashValue}, " +
                $"Critical={critical}");
        }

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

        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;

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
                speedModifier,
                momentumModifier,
                critical));
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
                    NextVisualInt(
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
                        NextVisualBool()
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
                    NextVisualInt(
                        1,
                        10);

                int b =
                    NextVisualInt(
                        1,
                        10);

                return $"🎰 {a} × {b}";
            }

            case SkillResolverType.Chinchiro:
            {
                int a = NextVisualInt(1, 7);
                int b = NextVisualInt(1, 7);
                int c = NextVisualInt(1, 7);
                return $"🎲 {a}·{b}·{c}";
            }
        }

        return NextVisualInt(
                randomMin,
                randomMax + 1)
            .ToString();
    }

    private int NextVisualInt(
        int minimumInclusive,
        int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
            return minimumInclusive;

        visualRandom ??=
            new System.Random(
                unchecked(
                    System.Environment.TickCount ^
                    GetInstanceID() * 397));

        return visualRandom.Next(
            minimumInclusive,
            maximumExclusive);
    }

    private bool NextVisualBool()
    {
        return NextVisualInt(0, 2) == 1;
    }

    private string CreateFinalRollText(
        RollResult rollResult,
        int finalClashValue,
        int speedModifier,
        int momentumModifier,
        bool critical)
    {
        if (rollResult == null)
            return finalClashValue.ToString();

        string baseText =
            rollResult.GetShortDisplayText();

        string powerText =
            rollResult.GetPurePowerBreakdown();

        AppendModifier(
            ref powerText,
            "속도",
            speedModifier);

        AppendModifier(
            ref powerText,
            "기세",
            momentumModifier);

        powerText +=
            $" / 합 {finalClashValue}";

        if (critical)
        {
            powerText +=
                " / <color=#FFD166><b>[CRITICAL]</b></color>";
        }

        return
            $"<size={finalPowerSizePercent}%>" +
            $"<b>{finalClashValue}</b></size>\n" +
            $"<size={detailSizePercent}%>" +
            $"{baseText}\n{powerText}</size>";
    }

    private static void AppendModifier(
        ref string textValue,
        string label,
        int modifier)
    {
        if (modifier == 0)
            return;

        textValue +=
            modifier > 0
                ? $" / {label} +{modifier}"
                : $" / {label} {modifier}";
    }

    public IEnumerator FadeAndDestroy(float fadeDuration)
    {
        yield return FadeOut(fadeDuration);
        Destroy(gameObject);
    }

    internal IEnumerator FadeOut(float fadeDuration)
    {
        if (fadeDuration <= 0f)
        {
            SetAlpha(0f);
            yield break;
        }

        float fadeElapsed = 0f;

        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(fadeElapsed / fadeDuration);

            SetAlpha(1f - t);

            yield return null;
        }

        SetAlpha(0f);
    }

    internal void ResetForPool()
    {
        StopAllCoroutines();

        followTarget = null;
        opposingTarget = null;

        worldCamera = null;
        worldOffset = Vector3.zero;

        useScreenSpaceLayout = false;
        fallbackSideSign = 1f;
        horizontalOffsetPixels = 0f;
        minimumPairSeparationPixels = 0f;
        verticalOffsetPixels = 0f;
        keepInsideViewport = false;
        viewportPaddingPixels = 0f;

        legacyWorldRightOffset = 0f;
        ownerLabel = string.Empty;

        SetText(string.Empty);
        SetAlpha(0f);

        if (canvas != null)
            canvas.worldCamera = null;
    }

    private void FollowTarget()
    {
        if (followTarget == null)
            return;

        Vector3 anchorPosition =
            followTarget.position +
            worldOffset;

        if (!useScreenSpaceLayout ||
            worldCamera == null)
        {
            Vector3 cameraRight =
                worldCamera != null
                    ? worldCamera.transform.right
                    : Vector3.right;

            transform.position =
                anchorPosition +
                cameraRight *
                legacyWorldRightOffset;

            return;
        }

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(
                anchorPosition);

        if (screenPosition.z <= 0.001f)
        {
            transform.position =
                anchorPosition;

            return;
        }

        float outwardSign =
            ResolveOutwardScreenSign(
                screenPosition);

        float horizontalPixels =
            ResolveHorizontalOffsetPixels(
                screenPosition);

        screenPosition.x +=
            outwardSign *
            horizontalPixels;

        screenPosition.y +=
            verticalOffsetPixels;

        if (keepInsideViewport)
        {
            float safeHorizontalPadding =
                Mathf.Min(
                    viewportPaddingPixels,
                    Screen.width * 0.45f);

            float safeVerticalPadding =
                Mathf.Min(
                    viewportPaddingPixels,
                    Screen.height * 0.45f);

            screenPosition.x =
                Mathf.Clamp(
                    screenPosition.x,
                    safeHorizontalPadding,
                    Screen.width -
                    safeHorizontalPadding);

            screenPosition.y =
                Mathf.Clamp(
                    screenPosition.y,
                    safeVerticalPadding,
                    Screen.height -
                    safeVerticalPadding);
        }

        transform.position =
            worldCamera.ScreenToWorldPoint(
                screenPosition);
    }

    private float ResolveOutwardScreenSign(
        Vector3 ownScreenPosition)
    {
        if (opposingTarget == null ||
            worldCamera == null)
        {
            return fallbackSideSign;
        }

        Vector3 opposingPosition =
            opposingTarget.position +
            worldOffset;

        Vector3 opposingScreenPosition =
            worldCamera.WorldToScreenPoint(
                opposingPosition);

        if (opposingScreenPosition.z <= 0.001f)
            return fallbackSideSign;

        float horizontalDelta =
            ownScreenPosition.x -
            opposingScreenPosition.x;

        if (Mathf.Abs(horizontalDelta) < 1f)
            return fallbackSideSign;

        // 화면상 왼쪽 캐릭터는 더 왼쪽으로,
        // 오른쪽 캐릭터는 더 오른쪽으로 배치한다.
        return Mathf.Sign(horizontalDelta);
    }

    private float ResolveHorizontalOffsetPixels(
        Vector3 ownScreenPosition)
    {
        float result =
            horizontalOffsetPixels;

        if (opposingTarget == null ||
            worldCamera == null ||
            minimumPairSeparationPixels <= 0f)
        {
            return result;
        }

        Vector3 opposingScreenPosition =
            worldCamera.WorldToScreenPoint(
                opposingTarget.position +
                worldOffset);

        if (opposingScreenPosition.z <= 0.001f)
            return result;

        float currentAnchorSeparation =
            Mathf.Abs(
                ownScreenPosition.x -
                opposingScreenPosition.x);

        float additionalPerSide =
            (
                minimumPairSeparationPixels -
                currentAnchorSeparation
            ) * 0.5f -
            horizontalOffsetPixels;

        if (additionalPerSide > 0f)
            result += additionalPerSide;

        return Mathf.Max(
            0f,
            result);
    }

    private string NormalizeOwnerLabel(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string result =
            value.Trim()
                .Replace("<", "‹")
                .Replace(">", "›");

        int safeMaximumLength =
            Mathf.Max(
                1,
                maximumOwnerLabelLength);

        if (result.Length <=
            safeMaximumLength)
        {
            return result;
        }

        return
            result.Substring(
                0,
                safeMaximumLength - 1) +
            "…";
    }

    private void FaceCamera()
    {
        if (!faceCamera)
            return;

        if (worldCamera == null)
            return;

        transform.rotation =
            worldCamera.transform.rotation *
            Quaternion.Euler(rotationOffset);
    }
}