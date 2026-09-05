using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 합 굴림 네모 내부에서 Resolver별 시각 연출을 담당한다.
///
/// 외부 이미지 에셋 없이 UI Image / TMP만으로 생성한다.
/// 시각 연출용 랜덤은 System.Random을 사용해 실제 전투 RNG를 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class BattleResolverRollVisualUI :
    MonoBehaviour
{
    private sealed class CoinView
    {
        public RectTransform Rect;
        public Image Image;
        public TMP_Text Label;
    }

    private sealed class DieView
    {
        public RectTransform Rect;
        public DicePolygonGraphic Background;
        public TMP_Text ValueText;
        public bool UseClassicPips;
        public readonly List<Image> Pips =
            new List<Image>();
    }

    private sealed class ReelView
    {
        public RectTransform Rect;
        public Image Background;
        public TMP_Text ValueText;
    }

    private static Sprite circleSprite;

    [SerializeField]
    private RectTransform selfRect;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private TMP_Text resolverText;

    [SerializeField]
    private TMP_Text basePowerText;

    [SerializeField]
    private TMP_Text combinationText;

    [SerializeField]
    private TMP_Text equationText;

    [SerializeField]
    private TMP_FontAsset fontAsset;

    [SerializeField, Min(16f)]
    private float visualSize = 62f;

    [Header("Colors")]
    [SerializeField]
    private Color coinSpinColor =
        new Color(
            1f,
            0.76f,
            0.08f,
            1f);

    [SerializeField]
    private Color coinFrontColor =
        new Color(
            1f,
            0.91f,
            0.30f,
            1f);

    [SerializeField]
    private Color coinBackColor =
        new Color(
            0.48f,
            0.34f,
            0.06f,
            1f);

    [SerializeField]
    private Color diceColor =
        new Color(
            0.95f,
            0.95f,
            0.90f,
            1f);

    [SerializeField]
    private Color slotColor =
        new Color(
            0.16f,
            0.20f,
            0.30f,
            1f);

    [Header("Readable Number Colors")]
    [SerializeField]
    private Color basePowerValueColor =
        new Color(
            0.66f,
            0.86f,
            1f,
            1f);

    [SerializeField]
    private Color rngPositiveValueColor =
        new Color(
            1f,
            0.83f,
            0.28f,
            1f);

    [SerializeField]
    private Color rngNegativeValueColor =
        new Color(
            1f,
            0.42f,
            0.42f,
            1f);

    [SerializeField]
    private Color finalPowerValueColor =
        new Color(
            0.50f,
            1f,
            0.72f,
            1f);

    [SerializeField]
    private Color summaryNoteColor =
        new Color(
            0.84f,
            0.88f,
            0.94f,
            1f);

    private readonly List<GameObject> generatedObjects =
        new List<GameObject>();

    private Vector2 baseAnchoredPosition;

    public void Configure(
        TMP_FontAsset font,
        float requestedVisualSize)
    {
        if (font != null)
            fontAsset = font;

        visualSize =
            Mathf.Max(
                16f,
                requestedVisualSize);

        EnsureHierarchy();
        ApplyLayout();
    }

    public IEnumerator PlayReveal(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        int seed)
    {
        EnsureHierarchy();
        ApplyLayout();
        ClearGenerated();

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        SetHeader(
            result);

        float safeDuration =
            Mathf.Max(
                0.12f,
                duration);

        float safeInterval =
            Mathf.Max(
                0.025f,
                updateInterval);

        System.Random random =
            new System.Random(
                unchecked(
                    seed * 486187739 +
                    (result?.RawValue ?? clashPower) *
                    397 +
                    (int)(result?.ResolverType ??
                          SkillResolverType.Dice)));

        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        switch (resolver)
        {
            case SkillResolverType.Coin:
                yield return PlayCoin(
                    result,
                    clashPower,
                    safeDuration,
                    safeInterval,
                    random);
                break;

            case SkillResolverType.Slot:
                yield return PlaySlot(
                    result,
                    clashPower,
                    safeDuration,
                    safeInterval,
                    random);
                break;

            case SkillResolverType.Chinchiro:
                yield return PlayChinchiro(
                    result,
                    clashPower,
                    safeDuration,
                    safeInterval,
                    random);
                break;

            default:
                yield return PlayDice(
                    result,
                    clashPower,
                    safeDuration,
                    safeInterval,
                    random);
                break;
        }
    }

    public void ShowFinal(
        RollResult result,
        int clashPower)
    {
        EnsureHierarchy();
        ApplyLayout();
        ClearGenerated();

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        SetHeader(
            result);

        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        switch (resolver)
        {
            case SkillResolverType.Coin:
                BuildFinalCoin(
                    result,
                    clashPower);
                break;

            case SkillResolverType.Slot:
                BuildFinalSlot(
                    result,
                    clashPower);
                break;

            case SkillResolverType.Chinchiro:
                BuildFinalChinchiro(
                    result,
                    clashPower);
                break;

            default:
                BuildFinalDice(
                    result,
                    clashPower);
                break;
        }
    }

    public void HideImmediate()
    {
        if (this == null)
            return;

        if (selfRect != null)
        {
            selfRect.anchoredPosition =
                baseAnchoredPosition;
        }

        ClearGenerated();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }






















    private void SetHeader(
        RollResult result)
    {
        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        resolverText.text =
            GetResolverLabel(
                resolver,
                result) +
            (result?.DebugOverrideApplied == true
                ? "  DEBUG"
                : string.Empty);

        resolverText.color =
            result?.DebugOverrideApplied == true
                ? new Color(
                    1f,
                    0.56f,
                    0.14f,
                    1f)
                : Color.white;

        basePowerText.text =
            BuildBasePowerText(
                result?.BasePower ?? 0);

        basePowerText.color =
            Color.white;

        combinationText.text =
            string.Empty;

        combinationText.color =
            Color.white;

        equationText.text =
            BuildRollSummaryText(
                result,
                null,
                false,
                result?.BasePower ?? 0,
                null,
                null);

        equationText.color =
            Color.white;
    }

    private static string GetResolverLabel(
        SkillResolverType resolver,
        RollResult result)
    {
        return resolver switch
        {
            SkillResolverType.Coin =>
                "COIN",

            SkillResolverType.Slot =>
                "SLOT",

            SkillResolverType.Chinchiro =>
                "CHINCHIRO",

            _ =>
                $"D{GetDiceVisualSideCount(result)}"
        };
    }


    private string BuildBasePowerText(
        int basePower)
    {
        return
            $"<size=62%>기본위력</size>\n" +
            $"<size=180%><b><color=#{ColorToHex(basePowerValueColor)}>{basePower}</color></b></size>";
    }

    private string BuildRollSummaryText(
        RollResult result,
        int? displayedRandomValue,
        bool resolved,
        int basePower,
        int? displayedFinalPower,
        string primaryNote,
        int? clashPowerOverride = null)
    {
        string rngLabel =
            GetRandomValueLabel(
                result?.ResolverType ?? SkillResolverType.Dice);

        Color rngColor =
            displayedRandomValue.HasValue &&
            displayedRandomValue.Value < 0
                ? rngNegativeValueColor
                : rngPositiveValueColor;

        string rngValueText =
            displayedRandomValue.HasValue
                ? displayedRandomValue.Value.ToString()
                : "...";

        int totalValue =
            resolved
                ? (displayedFinalPower ??
                   result?.FinalPower ??
                   basePower)
                : basePower +
                  (displayedRandomValue ?? 0);

        string totalLabel =
            resolved
                ? "최종위력"
                : "현재위력";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"<size=64%>{rngLabel}</size> " +
            $"<size=150%><b><color=#{ColorToHex(rngColor)}>{rngValueText}</color></b></size>\n");

        builder.Append(
            $"<size=64%>{totalLabel}</size> " +
            $"<size=155%><b><color=#{ColorToHex(finalPowerValueColor)}>{totalValue}</color></b></size>");

        string noteText =
            BuildSummaryNoteText(
                result,
                primaryNote,
                clashPowerOverride,
                resolved);

        if (!string.IsNullOrEmpty(noteText))
        {
            builder.Append(
                $"\n<size=56%><color=#{ColorToHex(summaryNoteColor)}>{noteText}</color></size>");
        }

        return builder.ToString();
    }

    private static int? SumFirstValues(
        IReadOnlyList<int> values,
        int count)
    {
        if (values == null ||
            count <= 0)
        {
            return null;
        }

        int safeCount =
            Mathf.Min(
                count,
                values.Count);

        int sum = 0;

        for (int index = 0;
             index < safeCount;
             index++)
        {
            sum += values[index];
        }

        return sum;
    }

    private static int ResolveDisplayedRandomValue(
        RollResult result)
    {
        if (result == null)
            return 0;

        return result.ResolverType switch
        {
            SkillResolverType.Coin =>
                result.CoinValues != null &&
                result.CoinValues.Count > 0
                    ? SumAllValues(result.CoinValues)
                    : result.ModifiedValue,

            SkillResolverType.Dice =>
                result.DiceValues != null &&
                result.DiceValues.Count > 0
                    ? SumAllValues(result.DiceValues)
                    : result.ModifiedValue,

            SkillResolverType.Slot =>
                result.SlotValue != 0
                    ? result.SlotValue
                    : result.ModifiedValue,

            SkillResolverType.Chinchiro =>
                result.ChinchiroBonus != 0 ||
                result.ChinchiroCombination != ChinchiroCombination.None
                    ? result.ChinchiroBonus
                    : result.ModifiedValue,

            _ => result.ModifiedValue
        };
    }

    private static int SumAllValues(
        IReadOnlyList<int> values)
    {
        if (values == null)
            return 0;

        int sum = 0;

        for (int index = 0;
             index < values.Count;
             index++)
        {
            sum += values[index];
        }

        return sum;
    }

    private string BuildSummaryNoteText(
        RollResult result,
        string primaryNote,
        int? clashPowerOverride,
        bool resolved)
    {
        List<string> notes =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(primaryNote) &&
            !string.Equals(primaryNote, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add(primaryNote);
        }

        if (resolved &&
            result != null &&
            result.ExternalModifier != 0)
        {
            notes.Add(
                $"보정 {result.ExternalModifier}");
        }

        int? clashPower =
            clashPowerOverride;

        if (!clashPower.HasValue &&
            result != null)
        {
            clashPower = result.ClashPower;
        }

        if (resolved &&
            result != null &&
            clashPower.HasValue &&
            clashPower.Value != result.FinalPower)
        {
            notes.Add(
                $"합 위력 {clashPower.Value}");
        }

        return string.Join("  •  ", notes);
    }

    private static string GetRandomValueLabel(
        SkillResolverType resolver)
    {
        return resolver switch
        {
            SkillResolverType.Coin => "코인합",
            SkillResolverType.Slot => "슬롯값",
            SkillResolverType.Chinchiro => "조합값",
            _ => "주사위합"
        };
    }

    private static string ColorToHex(
        Color color)
    {
        return ColorUtility.ToHtmlStringRGB(color);
    }







    private static void AppendFinalPower(
        StringBuilder builder,
        RollResult result,
        int clashPower)
    {
        builder.Append(
            $" = 최종위력 {result.FinalPower}");

        if (clashPower !=
            result.FinalPower)
        {
            builder.Append(
                $"\n<size=70%>합 위력 {clashPower}</size>");
        }
    }

    private static void AppendSignedValue(
        StringBuilder builder,
        int value)
    {
        if (value >= 0)
        {
            builder.Append(
                $" + {value}");
        }
        else
        {
            builder.Append(
                $" - {Mathf.Abs(value)}");
        }
    }










    private static Vector2 GetRowPosition(
        int index,
        int count,
        float itemSize,
        float spacing)
    {
        float totalWidth =
            count * itemSize +
            Mathf.Max(
                0,
                count - 1) *
            spacing;

        float start =
            -totalWidth * 0.5f +
            itemSize * 0.5f;

        return
            new Vector2(
                start +
                index *
                (itemSize + spacing),
                0f);
    }


    private void EnsureHierarchy()
    {
        selfRect ??=
            GetComponent<RectTransform>();

        if (selfRect == null)
            selfRect = gameObject.AddComponent<RectTransform>();

        canvasGroup ??=
            GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        resolverText =
            EnsurePersistentText(
                resolverText,
                "Resolver",
                new Vector2(0f, 0.80f),
                new Vector2(1f, 1.05f),
                visualSize * 0.13f,
                FontStyles.Bold);

        basePowerText =
            EnsurePersistentText(
                basePowerText,
                "BasePower",
                new Vector2(-0.15f, 0.60f),
                new Vector2(1.15f, 0.84f),
                visualSize * 0.12f,
                FontStyles.Normal);

        combinationText =
            EnsurePersistentText(
                combinationText,
                "Combination",
                new Vector2(-0.25f, 0.48f),
                new Vector2(1.25f, 0.72f),
                visualSize * 0.14f,
                FontStyles.Bold);

        equationText =
            EnsurePersistentText(
                equationText,
                "Equation",
                new Vector2(-0.55f, -0.35f),
                new Vector2(1.55f, 0.12f),
                visualSize * 0.14f,
                FontStyles.Bold);

        baseAnchoredPosition =
            selfRect.anchoredPosition;
    }

    private void ApplyLayout()
    {
        if (selfRect == null)
            return;

        selfRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        selfRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        selfRect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        selfRect.sizeDelta =
            new Vector2(
                visualSize * 2.25f,
                visualSize * 1.45f);

        selfRect.localScale =
            Vector3.one;

        UpdatePersistentText(
            resolverText,
            visualSize * 0.13f);

        UpdatePersistentText(
            basePowerText,
            visualSize * 0.12f);

        UpdatePersistentText(
            combinationText,
            visualSize * 0.14f);

        UpdatePersistentText(
            equationText,
            visualSize * 0.14f);
    }

    private TMP_Text EnsurePersistentText(
        TMP_Text current,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles style)
    {
        if (current == null)
        {
            Transform existing =
                transform.Find(
                    objectName);

            if (existing != null)
            {
                current =
                    existing.GetComponent<
                        TMP_Text>();
            }
        }

        if (current == null)
        {
            GameObject textObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                transform,
                false);

            current =
                textObject.GetComponent<
                    TMP_Text>();
        }

        RectTransform rect =
            current.rectTransform;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        ConfigureText(
            current,
            fontSize,
            style);

        return current;
    }

    private TMP_Text CreateGeneratedText(
        string objectName,
        string value,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject =
            CreateGeneratedObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        TMP_Text text =
            textObject.GetComponent<
                TMP_Text>();

        RectTransform rect =
            text.rectTransform;

        rect.sizeDelta =
            new Vector2(
                visualSize * 0.42f,
                visualSize * 0.42f);

        ConfigureText(
            text,
            fontSize,
            style);

        text.text = value;
        return text;
    }

    private TMP_Text CreateTextChild(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        textObject.transform.SetParent(
            parent,
            false);

        TMP_Text text =
            textObject.GetComponent<
                TMP_Text>();

        RectTransform rect =
            text.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ConfigureText(
            text,
            fontSize,
            style);

        text.text = value;
        return text;
    }

    private void ConfigureText(
        TMP_Text text,
        float fontSize,
        FontStyles style)
    {
        if (text == null)
            return;

        if (fontAsset != null)
            text.font = fontAsset;

        text.fontSize =
            Mathf.Max(
                6f,
                fontSize);

        text.enableAutoSizing = true;
        text.fontSizeMin =
            Mathf.Max(
                5f,
                fontSize * 0.55f);

        text.fontSizeMax =
            Mathf.Max(
                6f,
                fontSize);

        text.fontStyle = style;
        text.alignment =
            TextAlignmentOptions.Center;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        text.overflowMode =
            TextOverflowModes.Ellipsis;

        text.raycastTarget = false;
        text.color = Color.white;
        text.outlineWidth = 0.14f;
        text.outlineColor =
            new Color(
                0f,
                0f,
                0f,
                0.92f);
    }

    private void UpdatePersistentText(
        TMP_Text text,
        float fontSize)
    {
        if (text == null)
            return;

        if (fontAsset != null)
            text.font = fontAsset;

        text.fontSize =
            Mathf.Max(
                6f,
                fontSize);

        text.fontSizeMin =
            Mathf.Max(
                5f,
                fontSize * 0.55f);

        text.fontSizeMax =
            Mathf.Max(
                6f,
                fontSize);
    }

    private GameObject CreateGeneratedObject(
        string objectName,
        params Type[] componentTypes)
    {
        GameObject target =
            new GameObject(
                objectName,
                componentTypes);

        target.transform.SetParent(
            transform,
            false);

        generatedObjects.Add(
            target);

        return target;
    }

    private void ClearGenerated()
    {
        for (int index =
                 generatedObjects.Count - 1;
             index >= 0;
             index--)
        {
            GameObject target =
                generatedObjects[index];

            if (target == null)
                continue;

            target.SetActive(false);

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        generatedObjects.Clear();

        if (combinationText != null)
            combinationText.text = string.Empty;

        if (equationText != null)
            equationText.text = string.Empty;

        if (selfRect != null)
            selfRect.anchoredPosition =
                baseAnchoredPosition;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 32;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name =
                    "ProjectAbyss_RuntimeCircle",

                filterMode =
                    FilterMode.Bilinear,

                wrapMode =
                    TextureWrapMode.Clamp,

                hideFlags =
                    HideFlags.DontSaveInBuild
            };

        Color[] pixels =
            new Color[
                size *
                size];

        Vector2 center =
            new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f);

        float radius =
            size * 0.48f;

        for (int y = 0;
             y < size;
             y++)
        {
            for (int x = 0;
                 x < size;
                 x++)
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(
                            x,
                            y),
                        center);

                float alpha =
                    Mathf.Clamp01(
                        radius -
                        distance +
                        1f);

                pixels[
                    y *
                    size +
                    x] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha);
            }
        }

        texture.SetPixels(
            pixels);

        texture.Apply(
            false,
            true);

        circleSprite =
            Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    size,
                    size),
                new Vector2(
                    0.5f,
                    0.5f),
                100f);

        circleSprite.name =
            "ProjectAbyss_RuntimeCircleSprite";

        circleSprite.hideFlags =
            HideFlags.DontSaveInBuild;

        return circleSprite;
    }
}

/// <summary>
/// 주사위 결과 범위의 경우의 수에 맞춰 3~20각형 실루엣을 생성한다.
/// 외부 Sprite를 사용하지 않으므로 D6, D8과 이후의 공용 Dice가 같은 경로를 사용한다.
/// </summary>
internal sealed class DicePolygonGraphic : MaskableGraphic
{
    [SerializeField, Range(3, 20)]
    private int sideCount = 6;

    public int SideCount
    {
        get => sideCount;
        set
        {
            int safe =
                Mathf.Clamp(
                    value,
                    3,
                    20);

            if (sideCount == safe)
                return;

            sideCount = safe;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(
        VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect =
            GetPixelAdjustedRect();

        Vector2 center =
            rect.center;

        float radius =
            Mathf.Max(
                0f,
                Mathf.Min(
                    rect.width,
                    rect.height) *
                0.5f);

        UIVertex vertex =
            UIVertex.simpleVert;

        vertex.color = color;
        vertex.position = center;
        vertexHelper.AddVert(vertex);

        // 짝수 면체는 윗변이 수평에 가까워지도록 반 면 회전한다.
        float angleOffset =
            Mathf.PI * 0.5f +
            Mathf.PI / sideCount;

        for (int index = 0;
             index < sideCount;
             index++)
        {
            float angle =
                angleOffset -
                Mathf.PI * 2f *
                index /
                sideCount;

            vertex.position =
                center +
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) *
                radius;

            vertexHelper.AddVert(vertex);
        }

        for (int index = 0;
             index < sideCount;
             index++)
        {
            int current = index + 1;
            int next =
                index + 1 < sideCount
                    ? current + 1
                    : 1;

            vertexHelper.AddTriangle(
                0,
                current,
                next);
        }
    }
}