using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleClashRollSquareUI : MonoBehaviour
{
    [SerializeField] private RectTransform visualRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private CanvasGroup visualCanvasGroup;

    [Header("Roll Result Text")]
    [SerializeField] private TMP_Text resolverText;
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private BattleResolverRollVisualUI resolverVisual;

    [Header("Size")]
    [SerializeField, Min(16f)]
    private float visualSize = 62f;

    private readonly List<GameObject> transientObjects = new();

    private Color baseColor = Color.white;
    private Vector2 baseAnchoredPosition;
    private Vector3 baseScale = Vector3.one;
    private bool isBroken;
    private Coroutine oneSidedPulseRoutine;
    private Color oneSidedPulseColor;

    public CombatRollType RollType { get; private set; }
    public bool IsBroken => isBroken;

    public void Configure(
        CombatRollType type,
        Color attackColor,
        Color defenseColor,
        TMP_FontAsset font = null,
        float requestedVisualSize = 62f)
    {
        if (font != null)
            fontAsset = font;

        visualSize =
            Mathf.Max(
                16f,
                requestedVisualSize);

        EnsureReferences();

        if (resolverVisual != null)
        {
            resolverVisual.Configure(
                fontAsset,
                visualSize);
        }

        RollType = type;
        baseColor =
            type == CombatRollType.Defense
                ? defenseColor
                : attackColor;

        fillImage.color = baseColor;
        glowImage.color = new Color(1f, 1f, 1f, 0f);
        glowImage.enabled = false;

        baseAnchoredPosition = visualRect.anchoredPosition;
        baseScale = visualRect.localScale;
        isBroken = false;

        visualCanvasGroup.alpha = 1f;
        visualRect.anchoredPosition = baseAnchoredPosition;
        visualRect.localScale = baseScale;
        fillImage.enabled = true;

        ClearRollText();
        ClearTransientObjects();
    }

    /// <summary>
    /// 실제 전투 계산에서 이미 생성된 RollResult만 시각화한다.
    /// 연출용 변화는 System.Random을 사용하므로 UnityEngine.Random 상태를 변경하지 않는다.
    /// </summary>
    public IEnumerator PlayRollReveal(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        int seed)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        SetRollTextVisible(false);

        if (resolverVisual != null)
        {
            yield return resolverVisual.PlayReveal(
                result,
                clashPower,
                duration,
                updateInterval,
                seed);

            yield break;
        }

        // 전문 Resolver Visual이 없는 경우의 안전한 기존 텍스트 Fallback.
        SetRollTextVisible(true);

        float safeDuration = Mathf.Max(0.05f, duration);
        float safeInterval = Mathf.Max(0.025f, updateInterval);
        float elapsed = 0f;
        float nextFrameAt = 0f;

        System.Random visualRandom =
            new System.Random(
                unchecked(
                    seed * 397 ^
                    (result?.RawValue ?? clashPower) * 31 ^
                    (int)(result?.ResolverType ?? SkillResolverType.Dice)));

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= nextFrameAt)
            {
                nextFrameAt += safeInterval;
                ShowRollingFrame(result, visualRandom);
            }

            yield return null;
        }

        ShowFinalRoll(result, clashPower);
    }

    public void SetVisualSize(
        float requestedVisualSize)
    {
        visualSize =
            Mathf.Max(
                16f,
                requestedVisualSize);

        EnsureReferences();

        if (resolverVisual != null)
        {
            resolverVisual.Configure(
                fontAsset,
                visualSize);
        }

        baseAnchoredPosition =
            visualRect.anchoredPosition;

        baseScale =
            visualRect.localScale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        visualSize =
            Mathf.Max(
                16f,
                visualSize);

        if (!Application.isPlaying)
            EnsureReferences();
    }
#endif

    public void ShowFinalRoll(
        RollResult result,
        int clashPower)
    {
        EnsureReferences();

        if (isBroken)
            return;

        if (resolverVisual != null)
        {
            SetRollTextVisible(false);

            resolverVisual.ShowFinal(
                result,
                clashPower);

            return;
        }

        SetRollTextVisible(true);

        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        resolverText.text =
            GetResolverLabel(resolver);

        powerText.text =
            clashPower.ToString();

        powerText.color =
            result?.IsCritical == true
                ? new Color(1f, 0.87f, 0.24f, 1f)
                : Color.white;

        detailText.text =
            BuildFinalDetail(
                result,
                clashPower);
    }

    public IEnumerator PlayNoise(
        float duration,
        float intensity,
        float frequency,
        int seed)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        float safeDuration = Mathf.Max(0.01f, duration);
        float safeFrequency = Mathf.Max(1f, frequency);
        float elapsed = 0f;
        Vector2 origin = baseAnchoredPosition;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float time = elapsed * safeFrequency;

            float x =
                (Mathf.PerlinNoise(seed + time, 0.17f) - 0.5f) *
                2f * intensity;

            float y =
                (Mathf.PerlinNoise(0.73f, seed + time) - 0.5f) *
                2f * intensity;

            visualRect.anchoredPosition =
                origin + new Vector2(x, y);

            yield return null;
        }

        visualRect.anchoredPosition = origin;
    }

    public IEnumerator PlayWinnerSparkle(
        float duration)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        glowImage.enabled = true;
        float safeDuration = Mathf.Max(0.1f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float wave = Mathf.Sin(t * Mathf.PI * 5f);
            float pulse = Mathf.Max(0f, wave);

            visualRect.localScale =
                baseScale * Mathf.Lerp(1.05f, 1.28f, pulse);

            glowImage.color =
                new Color(1f, 1f, 0.82f, Mathf.Lerp(0.25f, 0.9f, pulse));

            fillImage.color =
                Color.Lerp(baseColor, Color.white, pulse * 0.42f);

            SetTextGlow(pulse);
            yield return null;
        }

        visualRect.localScale = baseScale * 1.06f;
        fillImage.color = Color.Lerp(baseColor, Color.white, 0.18f);
        glowImage.color = new Color(1f, 0.95f, 0.55f, 0.34f);
        SetTextGlow(0.25f);
    }

    public IEnumerator PlayTieFlash(
        float duration)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        float safeDuration = Mathf.Max(0.08f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float pulse = Mathf.Sin(t * Mathf.PI);

            fillImage.color =
                Color.Lerp(baseColor, Color.white, pulse * 0.65f);

            visualRect.localScale =
                baseScale * Mathf.Lerp(1f, 1.16f, pulse);

            SetTextGlow(pulse * 0.5f);
            yield return null;
        }

        fillImage.color = baseColor;
        visualRect.localScale = baseScale;
        SetTextGlow(0f);
    }

    public IEnumerator PlayBreak(
        float duration,
        float distance)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        isBroken = true;
        fillImage.enabled = false;
        glowImage.enabled = false;
        SetRollTextVisible(false);
        if (resolverVisual != null)
            resolverVisual.HideImmediate();

        RectTransform[] shards =
            CreateShards();

        float safeDuration = Mathf.Max(0.12f, duration);
        float elapsed = 0f;

        float visualScale =
            GetVisualScale();

        Vector2[] starts =
        {
            new Vector2(-9f, 9f) * visualScale,
            new Vector2(9f, 9f) * visualScale,
            new Vector2(-9f, -9f) * visualScale,
            new Vector2(9f, -9f) * visualScale
        };

        Vector2[] directions =
        {
            new(-0.9f, 1f),
            new(0.9f, 1f),
            new(-1f, -0.85f),
            new(1f, -0.85f)
        };

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            for (int index = 0;
                 index < shards.Length;
                 index++)
            {
                RectTransform shard = shards[index];

                if (shard == null)
                    continue;

                shard.anchoredPosition =
                    starts[index] +
                    directions[index] *
                    distance *
                    visualScale *
                    eased +
                    Vector2.down *
                    18f *
                    visualScale *
                    t *
                    t;

                shard.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        (index % 2 == 0 ? -1f : 1f) *
                        160f * eased);

                Image shardImage =
                    shard.GetComponent<Image>();

                if (shardImage != null)
                {
                    Color color = shardImage.color;
                    color.a = 1f - t;
                    shardImage.color = color;
                }
            }

            yield return null;
        }

        ClearTransientObjects();
        visualCanvasGroup.alpha = 0.12f;
    }

    public void BeginOneSidedHighlight(
        Color oneSidedColor)
    {
        EnsureReferences();

        if (isBroken)
            return;

        EndOneSidedHighlight(dimAfter: false);
        oneSidedPulseColor = oneSidedColor;
        oneSidedPulseRoutine =
            StartCoroutine(
                OneSidedPulseLoop());
    }

    public void EndOneSidedHighlight(
        bool dimAfter = true)
    {
        if (oneSidedPulseRoutine != null)
        {
            StopCoroutine(oneSidedPulseRoutine);
            oneSidedPulseRoutine = null;
        }

        EnsureReferences();

        if (isBroken)
            return;

        fillImage.color = baseColor;
        visualRect.localScale = baseScale;
        glowImage.enabled = false;
        visualCanvasGroup.alpha = dimAfter ? 0.38f : 1f;
    }

    private IEnumerator OneSidedPulseLoop()
    {
        glowImage.enabled = true;
        visualCanvasGroup.alpha = 1f;
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse =
                0.5f +
                Mathf.Sin(elapsed * 12f) * 0.5f;

            fillImage.color =
                Color.Lerp(
                    baseColor,
                    oneSidedPulseColor,
                    0.68f + pulse * 0.32f);

            glowImage.color =
                new Color(
                    oneSidedPulseColor.r,
                    oneSidedPulseColor.g,
                    oneSidedPulseColor.b,
                    0.3f + pulse * 0.62f);

            visualRect.localScale =
                baseScale * Mathf.Lerp(1.06f, 1.25f, pulse);

            yield return null;
        }
    }

    public IEnumerator PlayUnusedDefense(
        float duration)
    {
        EnsureReferences();

        if (isBroken)
            yield break;

        float safeDuration = Mathf.Max(0.08f, duration);
        float startAlpha = visualCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            visualCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0.22f, t);
            yield return null;
        }

        visualCanvasGroup.alpha = 0.22f;
    }

    public void HideImmediate()
    {
        EnsureReferences();
        StopAllCoroutines();
        oneSidedPulseRoutine = null;
        ClearTransientObjects();
        if (resolverVisual != null)
            resolverVisual.HideImmediate();
        visualCanvasGroup.alpha = 0f;
    }

    private void ShowRollingFrame(
        RollResult result,
        System.Random random)
    {
        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        resolverText.text =
            GetResolverLabel(resolver);

        powerText.color = Color.white;

        switch (resolver)
        {
            case SkillResolverType.Coin:
            {
                bool front = random.Next(0, 2) == 1;
                powerText.text = front ? "앞" : "뒤";
                detailText.text = "코인 회전 중";
                break;
            }

            case SkillResolverType.Slot:
            {
                int a = random.Next(1, 10);
                int b = random.Next(1, 10);
                powerText.text = (a * b).ToString();
                detailText.text = $"{a} × {b}";
                break;
            }

            case SkillResolverType.Chinchiro:
            {
                int a = random.Next(1, 7);
                int b = random.Next(1, 7);
                int c = random.Next(1, 7);
                powerText.text = "?";
                detailText.text = $"{a} · {b} · {c}";
                break;
            }

            default:
            {
                int min = result != null
                    ? Mathf.Min(result.DiceMin, result.DiceMax)
                    : 1;

                int max = result != null
                    ? Mathf.Max(result.DiceMin, result.DiceMax)
                    : 6;

                if (min == 0 && max == 0)
                {
                    min = 1;
                    max = 6;
                }

                int value = random.Next(min, max + 1);
                powerText.text = value.ToString();
                detailText.text = $"{min} ~ {max}";
                break;
            }
        }
    }

    private static string GetResolverLabel(
        SkillResolverType resolver)
    {
        return resolver switch
        {
            SkillResolverType.Coin => "COIN",
            SkillResolverType.Slot => "SLOT",
            SkillResolverType.Chinchiro => "CHINCHIRO",
            _ => "DICE"
        };
    }

    private static string BuildFinalDetail(
        RollResult result,
        int clashPower)
    {
        if (result == null)
            return $"합 {clashPower}";

        string outcome =
            result.ResolverType switch
            {
                SkillResolverType.Coin =>
                    BuildCoinOutcome(result),

                SkillResolverType.Slot =>
                    BuildSlotOutcome(result),

                SkillResolverType.Chinchiro =>
                    BuildChinchiroOutcome(result),

                _ =>
                    BuildDiceOutcome(result)
            };

        string modifiers =
            BuildModifierText(result);

        if (string.IsNullOrWhiteSpace(modifiers))
            return outcome;

        return outcome + "\n" + modifiers;
    }

    private static string BuildDiceOutcome(
        RollResult result)
    {
        if (result.DiceValues != null &&
            result.DiceValues.Count > 0)
        {
            return string.Join(
                " + ",
                result.DiceValues);
        }

        return result.RawValue.ToString();
    }

    private static string BuildCoinOutcome(
        RollResult result)
    {
        if (result.CoinFaces == null ||
            result.CoinFaces.Count == 0)
        {
            return $"코인 {result.RawValue}";
        }

        StringBuilder builder = new();

        for (int index = 0;
             index < result.CoinFaces.Count;
             index++)
        {
            if (index > 0)
                builder.Append(' ');

            builder.Append(
                result.CoinFaces[index]
                    ? "앞"
                    : "뒤");

            if (result.CoinValues != null &&
                index < result.CoinValues.Count)
            {
                builder.Append('(');
                builder.Append(result.CoinValues[index]);
                builder.Append(')');
            }
        }

        return builder.ToString();
    }

    private static string BuildSlotOutcome(
        RollResult result)
    {
        if (result.SlotA > 0 &&
            result.SlotB > 0)
        {
            return
                $"{result.SlotA} × {result.SlotB} = " +
                $"{result.SlotValue}";
        }

        // 개별 SkillRollData에서 Slot이 아직 Dice형 값으로 계산되는 경우의 표시.
        if (result.DiceValues != null &&
            result.DiceValues.Count > 0)
        {
            return
                "슬롯 결과 " +
                string.Join(" · ", result.DiceValues);
        }

        return $"슬롯 결과 {result.RawValue}";
    }

    private static string BuildChinchiroOutcome(
        RollResult result)
    {
        string dice =
            result.DiceValues != null &&
            result.DiceValues.Count > 0
                ? string.Join(" · ", result.DiceValues)
                : "-";

        return
            $"{dice}  {GetChinchiroName(result.ChinchiroCombination)}";
    }

    private static string GetChinchiroName(
        ChinchiroCombination combination)
    {
        return combination switch
        {
            ChinchiroCombination.Arashi => "아라시",
            ChinchiroCombination.Shigoro => "시고로",
            ChinchiroCombination.Moku => "목",
            ChinchiroCombination.Hifumi => "히후미",
            ChinchiroCombination.Blank => "무역",
            _ => string.Empty
        };
    }

    private static string BuildModifierText(
        RollResult result)
    {
        if (result == null)
            return string.Empty;

        StringBuilder builder = new();

        AppendModifier(builder, "판", result.JudgmentModifier);
        AppendModifier(builder, "속", result.SpeedModifier);
        AppendModifier(builder, "기", result.MomentumModifier);
        AppendModifier(builder, "도", result.PreparationModifier);

        if (builder.Length == 0 &&
            result.FinalPower != result.ClashPower)
        {
            builder.Append($"순수 {result.FinalPower}");
        }

        if (result.WasReused)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append("재사용");
        }

        return builder.ToString();
    }

    private static void AppendModifier(
        StringBuilder builder,
        string label,
        int value)
    {
        if (builder == null ||
            value == 0)
        {
            return;
        }

        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append(label);
        builder.Append(value > 0 ? "+" : string.Empty);
        builder.Append(value);
    }

    private void SetTextGlow(
        float amount)
    {
        float safe = Mathf.Clamp01(amount);

        if (powerText != null)
        {
            powerText.outlineWidth =
                Mathf.Lerp(0.12f, 0.30f, safe);

            powerText.outlineColor =
                Color.Lerp(
                    new Color(0f, 0f, 0f, 0.9f),
                    new Color(1f, 0.82f, 0.18f, 1f),
                    safe);
        }
    }

    private void ClearRollText()
    {
        EnsureReferences();

        resolverText.text = string.Empty;
        powerText.text = string.Empty;
        detailText.text = string.Empty;
        powerText.color = Color.white;
        if (resolverVisual != null)
            resolverVisual.HideImmediate();
        SetRollTextVisible(false);
    }

    private void SetRollTextVisible(
        bool visible)
    {
        if (resolverText != null)
            resolverText.gameObject.SetActive(visible);

        if (powerText != null)
            powerText.gameObject.SetActive(visible);

        if (detailText != null)
            detailText.gameObject.SetActive(visible);
    }

    private void EnsureReferences()
    {
        if (visualRect == null)
        {
            Transform existing =
                transform.Find("Visual");

            if (existing != null)
                visualRect = existing as RectTransform;
        }

        if (visualRect == null)
        {
            GameObject visual =
                new(
                    "Visual",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));

            visual.transform.SetParent(transform, false);
            visualRect = visual.GetComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.sizeDelta =
                Vector2.one *
                visualSize;

            visualRect.anchoredPosition =
                Vector2.zero;
        }
        else
        {
            visualRect.sizeDelta =
                Vector2.one *
                visualSize;
        }

        visualCanvasGroup ??=
            visualRect.GetComponent<CanvasGroup>();

        if (visualCanvasGroup == null)
            visualCanvasGroup = visualRect.gameObject.AddComponent<CanvasGroup>();

        fillImage ??=
            visualRect.GetComponent<Image>();

        if (fillImage == null)
            fillImage = visualRect.gameObject.AddComponent<Image>();

        fillImage.raycastTarget = false;

        if (glowImage == null)
        {
            Transform existingGlow =
                visualRect.Find("Glow");

            if (existingGlow != null)
                glowImage = existingGlow.GetComponent<Image>();
        }

        if (glowImage == null)
        {
            GameObject glow =
                new(
                    "Glow",
                    typeof(RectTransform),
                    typeof(Image));

            glow.transform.SetParent(visualRect, false);

            glowImage =
                glow.GetComponent<Image>();

            glowImage.raycastTarget = false;
            glow.transform.SetAsFirstSibling();
        }

        float visualScale =
            GetVisualScale();

        RectTransform glowRect =
            glowImage.rectTransform;

        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin =
            Vector2.one *
            -8f *
            visualScale;

        glowRect.offsetMax =
            Vector2.one *
            8f *
            visualScale;

        resolverText =
            EnsureText(
                resolverText,
                "ResolverText",
                new Vector2(0.03f, 0.72f),
                new Vector2(0.97f, 0.98f),
                8f * visualScale,
                FontStyles.Bold);

        powerText =
            EnsureText(
                powerText,
                "PowerText",
                new Vector2(0.03f, 0.24f),
                new Vector2(0.97f, 0.78f),
                25f * visualScale,
                FontStyles.Bold);

        detailText =
            EnsureText(
                detailText,
                "DetailText",
                new Vector2(-0.15f, -0.18f),
                new Vector2(1.15f, 0.31f),
                8f * visualScale,
                FontStyles.Normal);

        if (resolverVisual == null)
        {
            Transform existingResolverVisual =
                visualRect.Find(
                    "ResolverVisual");

            if (existingResolverVisual != null)
            {
                resolverVisual =
                    existingResolverVisual.GetComponent<
                        BattleResolverRollVisualUI>();
            }
        }

        if (resolverVisual == null)
        {
            GameObject resolverObject =
                new GameObject(
                    "ResolverVisual",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(BattleResolverRollVisualUI));

            resolverObject.transform.SetParent(
                visualRect,
                false);

            resolverVisual =
                resolverObject.GetComponent<
                    BattleResolverRollVisualUI>();
        }

        resolverVisual.Configure(
            fontAsset,
            visualSize);
    }

    private float GetVisualScale()
    {
        return
            Mathf.Max(
                0.25f,
                visualSize / 62f);
    }

    private TMP_Text EnsureText(
        TMP_Text current,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle)
    {
        if (current == null)
        {
            Transform existing =
                visualRect.Find(objectName);

            if (existing != null)
                current = existing.GetComponent<TMP_Text>();
        }

        if (current == null)
        {
            GameObject textObject =
                new(
                    objectName,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(visualRect, false);
            current = textObject.GetComponent<TMP_Text>();
        }

        RectTransform rect =
            current.rectTransform;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        if (fontAsset != null)
            current.font = fontAsset;

        current.fontSize = fontSize;
        current.enableAutoSizing = true;
        current.fontSizeMin = Mathf.Max(5f, fontSize * 0.55f);
        current.fontSizeMax = fontSize;
        current.fontStyle = fontStyle;
        current.alignment = TextAlignmentOptions.Center;
        current.textWrappingMode =
            TextWrappingModes.NoWrap;
        current.overflowMode = TextOverflowModes.Ellipsis;
        current.raycastTarget = false;
        current.color = Color.white;
        current.outlineWidth = 0.15f;
        current.outlineColor = new Color(0f, 0f, 0f, 0.9f);

        return current;
    }

    private RectTransform[] CreateShards()
    {
        RectTransform[] shards =
            new RectTransform[4];

        for (int index = 0;
             index < shards.Length;
             index++)
        {
            GameObject shardObject =
                new(
                    $"Shard_{index + 1}",
                    typeof(RectTransform),
                    typeof(Image));

            shardObject.transform.SetParent(visualRect, false);

            RectTransform shard =
                shardObject.GetComponent<RectTransform>();

            shard.anchorMin = new Vector2(0.5f, 0.5f);
            shard.anchorMax = new Vector2(0.5f, 0.5f);
            shard.pivot = new Vector2(0.5f, 0.5f);
            shard.sizeDelta =
                Vector2.one *
                27f *
                GetVisualScale();

            Image image =
                shardObject.GetComponent<Image>();

            image.color = baseColor;
            image.raycastTarget = false;

            transientObjects.Add(shardObject);
            shards[index] = shard;
        }

        return shards;
    }

    private void ClearTransientObjects()
    {
        for (int index = transientObjects.Count - 1;
             index >= 0;
             index--)
        {
            GameObject target = transientObjects[index];

            if (target == null)
                continue;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        transientObjects.Clear();
    }
}