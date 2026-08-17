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
public sealed class BattleResolverRollVisualUI :
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
        public Image Background;
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

    private IEnumerator PlayCoin(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        System.Random random)
    {
        int count =
            Mathf.Clamp(
                result?.CoinFaces?.Count ?? 0,
                1,
                8);

        List<CoinView> coins =
            CreateCoinViews(
                count);

        float elapsed = 0f;
        float nextUpdate = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            bool updateValues =
                elapsed >= nextUpdate;

            if (updateValues)
            {
                nextUpdate +=
                    updateInterval;
            }

            float revealProgress =
                Mathf.InverseLerp(
                    0.48f,
                    0.92f,
                    t);

            int lockedCount =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        revealProgress *
                        (count + 0.999f)),
                    0,
                    count);

            float tokenSize =
                GetCoinTokenSize(
                    count);

            for (int index = 0;
                 index < coins.Count;
                 index++)
            {
                CoinView coin =
                    coins[index];

                bool locked =
                    index < lockedCount;

                if (locked)
                {
                    bool face =
                        GetCoinFace(
                            result,
                            index);

                    SetCoinFace(
                        coin,
                        face);

                    coin.Rect.anchoredPosition =
                        GetRowPosition(
                            index,
                            count,
                            tokenSize,
                            tokenSize * 0.20f);

                    coin.Rect.localRotation =
                        Quaternion.identity;

                    coin.Rect.localScale =
                        Vector3.one;
                }
                else
                {
                    float angle =
                        elapsed * 500f +
                        index *
                        360f /
                        Mathf.Max(
                            1,
                            count);

                    float radians =
                        angle *
                        Mathf.Deg2Rad;

                    float radius =
                        visualSize *
                        (count <= 3
                            ? 0.30f
                            : 0.36f);

                    coin.Rect.anchoredPosition =
                        new Vector2(
                            Mathf.Cos(radians),
                            Mathf.Sin(radians)) *
                        radius;

                    float flip =
                        Mathf.Abs(
                            Mathf.Cos(
                                elapsed * 15f +
                                index));

                    coin.Rect.localScale =
                        new Vector3(
                            Mathf.Max(
                                0.12f,
                                flip),
                            1f,
                            1f);

                    coin.Rect.localRotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            -angle);

                    coin.Image.color =
                        coinSpinColor;

                    if (updateValues)
                        coin.Label.text = string.Empty;
                }
            }

            equationText.text =
                BuildRollSummaryText(
                    result,
                    lockedCount <= 0
                        ? (int?)null
                        : SumFirstValues(
                            result?.CoinValues,
                            lockedCount),
                    false,
                    result?.BasePower ?? 0,
                    null,
                    lockedCount <= 0
                        ? "코인 굴림 중"
                        : null);

            yield return null;
        }

        BuildFinalCoinState(
            coins,
            result,
            clashPower);
    }

    private IEnumerator PlayDice(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        System.Random random)
    {
        int count =
            Mathf.Clamp(
                result?.DiceValues?.Count ?? 0,
                1,
                8);

        List<DieView> dice =
            CreateDiceViews(
                count);

        float elapsed = 0f;
        float nextUpdate = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            bool updateValues =
                elapsed >= nextUpdate;

            if (updateValues)
                nextUpdate += updateInterval;

            float revealProgress =
                Mathf.InverseLerp(
                    0.52f,
                    0.93f,
                    t);

            int lockedCount =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        revealProgress *
                        (count + 0.999f)),
                    0,
                    count);

            float dieSize =
                GetDieSize(
                    count);

            for (int index = 0;
                 index < dice.Count;
                 index++)
            {
                DieView die =
                    dice[index];

                bool locked =
                    index < lockedCount;

                int value =
                    locked
                        ? GetDiceValue(
                            result,
                            index)
                        : random.Next(
                            GetDiceMinimum(result),
                            GetDiceMaximum(result) + 1);

                if (updateValues ||
                    locked)
                {
                    SetDieFace(
                        die,
                        NormalizeDieFace(value));
                }

                if (locked)
                {
                    die.Rect.anchoredPosition =
                        GetRowPosition(
                            index,
                            count,
                            dieSize,
                            dieSize * 0.18f);

                    die.Rect.localRotation =
                        Quaternion.identity;

                    die.Rect.localScale =
                        Vector3.one;
                }
                else
                {
                    float angle =
                        elapsed * 700f +
                        index * 71f;

                    float radians =
                        angle *
                        Mathf.Deg2Rad;

                    die.Rect.anchoredPosition =
                        new Vector2(
                            Mathf.Cos(radians) *
                            visualSize *
                            0.26f,
                            Mathf.Sin(radians * 1.3f) *
                            visualSize *
                            0.18f);

                    die.Rect.localRotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            angle);

                    float pulse =
                        0.85f +
                        Mathf.Abs(
                            Mathf.Sin(
                                elapsed * 11f +
                                index)) *
                        0.25f;

                    die.Rect.localScale =
                        Vector3.one *
                        pulse;
                }
            }

            equationText.text =
                BuildRollSummaryText(
                    result,
                    lockedCount <= 0
                        ? (int?)null
                        : SumFirstValues(
                            result?.DiceValues,
                            lockedCount),
                    false,
                    result?.BasePower ?? 0,
                    null,
                    lockedCount <= 0
                        ? "주사위 굴림 중"
                        : null);

            yield return null;
        }

        BuildFinalDiceState(
            dice,
            result,
            clashPower);
    }

    private IEnumerator PlaySlot(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        System.Random random)
    {
        ReelView left =
            CreateReel(
                "LeftReel",
                new Vector2(
                    -visualSize * 0.23f,
                    0f));

        ReelView right =
            CreateReel(
                "RightReel",
                new Vector2(
                    visualSize * 0.23f,
                    0f));

        TMP_Text multiply =
            CreateGeneratedText(
                "Multiply",
                "×",
                visualSize * 0.22f,
                FontStyles.Bold);

        multiply.rectTransform.anchoredPosition =
            Vector2.zero;

        int finalA =
            result?.SlotA > 0
                ? result.SlotA
                : Mathf.Max(
                    1,
                    result != null &&
                    result.DiceValues != null &&
                    result.DiceValues.Count > 0
                        ? result.DiceValues[0]
                        : 1);

        int finalB =
            result?.SlotB > 0
                ? result.SlotB
                : Mathf.Max(
                    1,
                    result != null &&
                    result.DiceValues != null &&
                    result.DiceValues.Count > 1
                        ? result.DiceValues[1]
                        : finalA);

        float elapsed = 0f;
        float nextUpdate = 0f;
        int valueA = 1;
        int valueB = 1;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            bool lockLeft =
                t >= 0.64f;

            bool lockRight =
                t >= 0.82f;

            if (elapsed >= nextUpdate)
            {
                nextUpdate +=
                    updateInterval;

                if (!lockLeft)
                    valueA = random.Next(1, 10);

                if (!lockRight)
                    valueB = random.Next(1, 10);
            }

            if (lockLeft)
                valueA = finalA;

            if (lockRight)
                valueB = finalB;

            left.ValueText.text =
                valueA.ToString();

            right.ValueText.text =
                valueB.ToString();

            float leftOffset =
                lockLeft
                    ? 0f
                    : Mathf.Repeat(
                        elapsed * 260f,
                        visualSize * 0.55f) -
                      visualSize * 0.275f;

            float rightOffset =
                lockRight
                    ? 0f
                    : Mathf.Repeat(
                        elapsed * 330f + 13f,
                        visualSize * 0.55f) -
                      visualSize * 0.275f;

            left.ValueText.rectTransform.anchoredPosition =
                new Vector2(
                    0f,
                    leftOffset);

            right.ValueText.rectTransform.anchoredPosition =
                new Vector2(
                    0f,
                    rightOffset);

            float leftScale =
                lockLeft
                    ? 1f +
                      Mathf.Max(
                          0f,
                          Mathf.Sin(
                              (t - 0.64f) *
                              18f)) *
                      0.18f
                    : 1f;

            float rightScale =
                lockRight
                    ? 1f +
                      Mathf.Max(
                          0f,
                          Mathf.Sin(
                              (t - 0.82f) *
                              18f)) *
                      0.18f
                    : 1f;

            left.Rect.localScale =
                Vector3.one *
                leftScale;

            right.Rect.localScale =
                Vector3.one *
                rightScale;

            equationText.text =
                BuildRollSummaryText(
                    result,
                    lockRight
                        ? ResolveDisplayedRandomValue(result)
                        : (int?)null,
                    lockRight,
                    result?.BasePower ?? 0,
                    lockRight
                        ? result?.FinalPower
                        : null,
                    lockRight
                        ? null
                        : "슬롯 정렬 중");

            yield return null;
        }

        SetReelFinal(
            left,
            finalA);

        SetReelFinal(
            right,
            finalB);

        bool jackpot =
            finalA == finalB ||
            (result?.IsMax == true);

        if (jackpot)
        {
            left.Background.color =
                coinFrontColor;

            right.Background.color =
                coinFrontColor;

            left.ValueText.color =
                Color.black;

            right.ValueText.color =
                Color.black;

            combinationText.text =
                result?.IsMax == true
                    ? "JACKPOT"
                    : "DOUBLE";

            combinationText.color =
                coinFrontColor;
        }

        equationText.text =
            BuildRollSummaryText(
                result,
                ResolveDisplayedRandomValue(result),
                true,
                result?.BasePower ?? 0,
                result?.FinalPower ?? clashPower,
                result?.IsMax == true
                    ? "JACKPOT"
                    : (finalA == finalB
                        ? "DOUBLE"
                        : null),
                clashPower);
    }

    private IEnumerator PlayChinchiro(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        System.Random random)
    {
        List<DieView> dice =
            CreateDiceViews(
                3);

        float elapsed = 0f;
        float nextUpdate = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            bool updateValues =
                elapsed >= nextUpdate;

            if (updateValues)
                nextUpdate += updateInterval;

            float revealProgress =
                Mathf.InverseLerp(
                    0.48f,
                    0.80f,
                    t);

            int lockedCount =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        revealProgress *
                        3.999f),
                    0,
                    3);

            float dieSize =
                GetDieSize(
                    3);

            for (int index = 0;
                 index < dice.Count;
                 index++)
            {
                DieView die =
                    dice[index];

                bool locked =
                    index < lockedCount;

                int value =
                    locked
                        ? GetDiceValue(
                            result,
                            index)
                        : random.Next(1, 7);

                if (updateValues ||
                    locked)
                {
                    SetDieFace(
                        die,
                        value);
                }

                Vector2 finalPosition =
                    GetRowPosition(
                        index,
                        3,
                        dieSize,
                        dieSize * 0.22f);

                if (locked)
                {
                    die.Rect.anchoredPosition =
                        finalPosition;

                    die.Rect.localRotation =
                        Quaternion.identity;

                    die.Rect.localScale =
                        Vector3.one;
                }
                else
                {
                    float angle =
                        elapsed * 780f +
                        index * 120f;

                    float radius =
                        visualSize *
                        0.32f;

                    die.Rect.anchoredPosition =
                        new Vector2(
                            Mathf.Cos(
                                angle *
                                Mathf.Deg2Rad),
                            Mathf.Sin(
                                angle *
                                1.37f *
                                Mathf.Deg2Rad)) *
                        radius;

                    die.Rect.localRotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            angle);

                    die.Rect.localScale =
                        Vector3.one *
                        (0.82f +
                         Mathf.Abs(
                             Mathf.Sin(
                                 elapsed * 13f +
                                 index)) *
                         0.28f);
                }
            }

            if (t >= 0.80f)
            {
                float effectT =
                    Mathf.InverseLerp(
                        0.80f,
                        1f,
                        t);

                ApplyChinchiroEffect(
                    dice,
                    result,
                    effectT);
            }
            else
            {
                combinationText.text =
                    "조합 판정 중";
            }

            equationText.text =
                BuildRollSummaryText(
                    result,
                    t >= 0.82f
                        ? ResolveDisplayedRandomValue(result)
                        : (int?)null,
                    t >= 0.82f,
                    result?.BasePower ?? 0,
                    t >= 0.82f
                        ? result?.FinalPower
                        : null,
                    t >= 0.82f
                        ? GetChinchiroName(
                            result?.ChinchiroCombination ?? ChinchiroCombination.None)
                        : "조합 판정 중");

            yield return null;
        }

        BuildFinalChinchiroState(
            dice,
            result,
            clashPower);
    }

    private void BuildFinalCoin(
        RollResult result,
        int clashPower)
    {
        int count =
            Mathf.Clamp(
                result?.CoinFaces?.Count ?? 0,
                1,
                8);

        List<CoinView> coins =
            CreateCoinViews(
                count);

        BuildFinalCoinState(
            coins,
            result,
            clashPower);
    }

    private void BuildFinalCoinState(
        IReadOnlyList<CoinView> coins,
        RollResult result,
        int clashPower)
    {
        int count =
            coins?.Count ?? 0;

        float tokenSize =
            GetCoinTokenSize(
                Mathf.Max(
                    1,
                    count));

        for (int index = 0;
             index < count;
             index++)
        {
            CoinView coin =
                coins[index];

            SetCoinFace(
                coin,
                GetCoinFace(
                    result,
                    index));

            coin.Rect.anchoredPosition =
                GetRowPosition(
                    index,
                    count,
                    tokenSize,
                    tokenSize * 0.20f);

            coin.Rect.localRotation =
                Quaternion.identity;

            coin.Rect.localScale =
                Vector3.one;
        }

        equationText.text =
            BuildRollSummaryText(
                result,
                ResolveDisplayedRandomValue(result),
                true,
                result?.BasePower ?? 0,
                result?.FinalPower ?? clashPower,
                null,
                clashPower);
    }

    private void BuildFinalDice(
        RollResult result,
        int clashPower)
    {
        int count =
            Mathf.Clamp(
                result?.DiceValues?.Count ?? 0,
                1,
                8);

        List<DieView> dice =
            CreateDiceViews(
                count);

        BuildFinalDiceState(
            dice,
            result,
            clashPower);
    }

    private void BuildFinalDiceState(
        IReadOnlyList<DieView> dice,
        RollResult result,
        int clashPower)
    {
        int count =
            dice?.Count ?? 0;

        float dieSize =
            GetDieSize(
                Mathf.Max(
                    1,
                    count));

        for (int index = 0;
             index < count;
             index++)
        {
            DieView die =
                dice[index];

            SetDieFace(
                die,
                NormalizeDieFace(
                    GetDiceValue(
                        result,
                        index)));

            die.Rect.anchoredPosition =
                GetRowPosition(
                    index,
                    count,
                    dieSize,
                    dieSize * 0.18f);

            die.Rect.localRotation =
                Quaternion.identity;

            die.Rect.localScale =
                Vector3.one;
        }

        equationText.text =
            BuildRollSummaryText(
                result,
                ResolveDisplayedRandomValue(result),
                true,
                result?.BasePower ?? 0,
                result?.FinalPower ?? clashPower,
                null,
                clashPower);
    }

    private void BuildFinalSlot(
        RollResult result,
        int clashPower)
    {
        ReelView left =
            CreateReel(
                "LeftReel",
                new Vector2(
                    -visualSize * 0.23f,
                    0f));

        ReelView right =
            CreateReel(
                "RightReel",
                new Vector2(
                    visualSize * 0.23f,
                    0f));

        TMP_Text multiply =
            CreateGeneratedText(
                "Multiply",
                "×",
                visualSize * 0.22f,
                FontStyles.Bold);

        multiply.rectTransform.anchoredPosition =
            Vector2.zero;

        int a =
            result?.SlotA > 0
                ? result.SlotA
                : 1;

        int b =
            result?.SlotB > 0
                ? result.SlotB
                : 1;

        SetReelFinal(
            left,
            a);

        SetReelFinal(
            right,
            b);

        if (a == b ||
            result?.IsMax == true)
        {
            left.Background.color =
                coinFrontColor;

            right.Background.color =
                coinFrontColor;

            left.ValueText.color =
                Color.black;

            right.ValueText.color =
                Color.black;

            combinationText.text =
                result?.IsMax == true
                    ? "JACKPOT"
                    : "DOUBLE";

            combinationText.color =
                coinFrontColor;
        }

        equationText.text =
            BuildRollSummaryText(
                result,
                ResolveDisplayedRandomValue(result),
                true,
                result?.BasePower ?? 0,
                result?.FinalPower ?? clashPower,
                result?.IsMax == true
                    ? "JACKPOT"
                    : (a == b
                        ? "DOUBLE"
                        : null),
                clashPower);
    }

    private void BuildFinalChinchiro(
        RollResult result,
        int clashPower)
    {
        List<DieView> dice =
            CreateDiceViews(
                3);

        BuildFinalChinchiroState(
            dice,
            result,
            clashPower);
    }

    private void BuildFinalChinchiroState(
        IReadOnlyList<DieView> dice,
        RollResult result,
        int clashPower)
    {
        float dieSize =
            GetDieSize(
                3);

        for (int index = 0;
             index < dice.Count;
             index++)
        {
            DieView die =
                dice[index];

            SetDieFace(
                die,
                GetDiceValue(
                    result,
                    index));

            die.Rect.anchoredPosition =
                GetRowPosition(
                    index,
                    3,
                    dieSize,
                    dieSize * 0.22f);

            die.Rect.localRotation =
                Quaternion.identity;

            die.Rect.localScale =
                Vector3.one;
        }

        ApplyChinchiroEffect(
            dice,
            result,
            1f);

        equationText.text =
            BuildRollSummaryText(
                result,
                ResolveDisplayedRandomValue(result),
                true,
                result?.BasePower ?? 0,
                result?.FinalPower ?? clashPower,
                GetChinchiroName(
                    result?.ChinchiroCombination ?? ChinchiroCombination.None),
                clashPower);
    }

    private List<CoinView> CreateCoinViews(
        int count)
    {
        List<CoinView> result =
            new List<CoinView>();

        float size =
            GetCoinTokenSize(
                count);

        for (int index = 0;
             index < count;
             index++)
        {
            GameObject coinObject =
                CreateGeneratedObject(
                    $"Coin_{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            RectTransform rect =
                coinObject.GetComponent<
                    RectTransform>();

            rect.sizeDelta =
                Vector2.one *
                size;

            Image image =
                coinObject.GetComponent<Image>();

            image.sprite =
                GetCircleSprite();

            image.color =
                coinSpinColor;

            image.raycastTarget = false;

            TMP_Text label =
                CreateTextChild(
                    coinObject.transform,
                    "Face",
                    string.Empty,
                    size * 0.34f,
                    FontStyles.Bold);

            label.color =
                new Color(
                    0.12f,
                    0.09f,
                    0.02f,
                    1f);

            result.Add(
                new CoinView
                {
                    Rect = rect,
                    Image = image,
                    Label = label
                });
        }

        return result;
    }

    private List<DieView> CreateDiceViews(
        int count)
    {
        List<DieView> result =
            new List<DieView>();

        float size =
            GetDieSize(
                count);

        for (int index = 0;
             index < count;
             index++)
        {
            result.Add(
                CreateDie(
                    $"Die_{index}",
                    size));
        }

        return result;
    }

    private DieView CreateDie(
        string objectName,
        float size)
    {
        GameObject dieObject =
            CreateGeneratedObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            dieObject.GetComponent<
                RectTransform>();

        rect.sizeDelta =
            Vector2.one *
            size;

        Image background =
            dieObject.GetComponent<Image>();

        background.color =
            diceColor;

        background.raycastTarget = false;

        DieView result =
            new DieView
            {
                Rect = rect,
                Background = background
            };

        Vector2[] positions =
        {
            new Vector2(-0.25f, 0.25f),
            new Vector2(0.25f, 0.25f),
            new Vector2(-0.25f, 0f),
            new Vector2(0.25f, 0f),
            new Vector2(-0.25f, -0.25f),
            new Vector2(0.25f, -0.25f),
            Vector2.zero
        };

        float pipSize =
            size * 0.14f;

        for (int index = 0;
             index < positions.Length;
             index++)
        {
            GameObject pipObject =
                new GameObject(
                    $"Pip_{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            pipObject.transform.SetParent(
                rect,
                false);

            RectTransform pipRect =
                pipObject.GetComponent<
                    RectTransform>();

            pipRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            pipRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            pipRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            pipRect.sizeDelta =
                Vector2.one *
                pipSize;

            pipRect.anchoredPosition =
                positions[index] *
                size;

            Image pip =
                pipObject.GetComponent<Image>();

            pip.sprite =
                GetCircleSprite();

            pip.color =
                new Color(
                    0.08f,
                    0.09f,
                    0.12f,
                    1f);

            pip.raycastTarget = false;
            result.Pips.Add(pip);
        }

        SetDieFace(
            result,
            1);

        return result;
    }

    private ReelView CreateReel(
        string objectName,
        Vector2 position)
    {
        GameObject reelObject =
            CreateGeneratedObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));

        RectTransform rect =
            reelObject.GetComponent<
                RectTransform>();

        rect.sizeDelta =
            new Vector2(
                visualSize * 0.36f,
                visualSize * 0.50f);

        rect.anchoredPosition =
            position;

        Image background =
            reelObject.GetComponent<Image>();

        background.color =
            slotColor;

        background.raycastTarget = false;

        Mask mask =
            reelObject.GetComponent<Mask>();

        mask.showMaskGraphic = true;

        TMP_Text valueText =
            CreateTextChild(
                reelObject.transform,
                "Value",
                "1",
                visualSize * 0.33f,
                FontStyles.Bold);

        valueText.color =
            Color.white;

        return
            new ReelView
            {
                Rect = rect,
                Background = background,
                ValueText = valueText
            };
    }

    private void SetCoinFace(
        CoinView coin,
        bool front)
    {
        if (coin == null)
            return;

        coin.Image.color =
            front
                ? coinFrontColor
                : coinBackColor;

        coin.Label.text =
            front
                ? "앞"
                : "뒤";

        coin.Label.color =
            front
                ? new Color(
                    0.12f,
                    0.09f,
                    0.01f,
                    1f)
                : new Color(
                    1f,
                    0.90f,
                    0.48f,
                    1f);
    }

    private static void SetDieFace(
        DieView die,
        int face)
    {
        if (die == null ||
            die.Pips.Count < 7)
        {
            return;
        }

        int value =
            Mathf.Clamp(
                face,
                1,
                6);

        bool[] visible =
            new bool[7];

        switch (value)
        {
            case 1:
                visible[6] = true;
                break;

            case 2:
                visible[0] = true;
                visible[5] = true;
                break;

            case 3:
                visible[0] = true;
                visible[6] = true;
                visible[5] = true;
                break;

            case 4:
                visible[0] = true;
                visible[1] = true;
                visible[4] = true;
                visible[5] = true;
                break;

            case 5:
                visible[0] = true;
                visible[1] = true;
                visible[4] = true;
                visible[5] = true;
                visible[6] = true;
                break;

            default:
                visible[0] = true;
                visible[1] = true;
                visible[2] = true;
                visible[3] = true;
                visible[4] = true;
                visible[5] = true;
                break;
        }

        for (int index = 0;
             index < die.Pips.Count;
             index++)
        {
            die.Pips[index]
                .gameObject
                .SetActive(
                    visible[index]);
        }
    }

    private void ApplyChinchiroEffect(
        IReadOnlyList<DieView> dice,
        RollResult result,
        float amount)
    {
        if (dice == null ||
            dice.Count == 0)
        {
            return;
        }

        float safe =
            Mathf.Clamp01(
                amount);

        ChinchiroCombination combination =
            result?.ChinchiroCombination ??
            ChinchiroCombination.None;

        selfRect.anchoredPosition =
            baseAnchoredPosition;

        switch (combination)
        {
            case ChinchiroCombination.Arashi:
            {
                combinationText.text =
                    "아라시";

                combinationText.color =
                    coinFrontColor;

                float pulse =
                    1f +
                    Mathf.Sin(
                        safe *
                        Mathf.PI *
                        5f) *
                    0.16f *
                    safe;

                foreach (DieView die in dice)
                {
                    die.Background.color =
                        Color.Lerp(
                            diceColor,
                            coinFrontColor,
                            0.82f);

                    die.Rect.localScale =
                        Vector3.one *
                        pulse;
                }

                break;
            }

            case ChinchiroCombination.Shigoro:
            {
                combinationText.text =
                    "시고로  4·5·6";

                combinationText.color =
                    new Color(
                        0.30f,
                        1f,
                        0.67f,
                        1f);

                for (int index = 0;
                     index < dice.Count;
                     index++)
                {
                    float local =
                        Mathf.Clamp01(
                            safe * 3f -
                            index * 0.55f);

                    dice[index].Background.color =
                        Color.Lerp(
                            diceColor,
                            new Color(
                                0.22f,
                                0.88f,
                                0.61f,
                                1f),
                            local);

                    dice[index].Rect.localScale =
                        Vector3.one *
                        Mathf.Lerp(
                            1f,
                            1.18f,
                            Mathf.Sin(
                                local *
                                Mathf.PI));
                }

                break;
            }

            case ChinchiroCombination.Moku:
            {
                combinationText.text =
                    "목";

                combinationText.color =
                    new Color(
                        0.40f,
                        0.72f,
                        1f,
                        1f);

                ResolveMokuPair(
                    result,
                    out int first,
                    out int second);

                for (int index = 0;
                     index < dice.Count;
                     index++)
                {
                    bool pair =
                        index == first ||
                        index == second;

                    dice[index].Background.color =
                        pair
                            ? new Color(
                                0.36f,
                                0.67f,
                                1f,
                                1f)
                            : new Color(
                                0.42f,
                                0.45f,
                                0.52f,
                                1f);

                    float pulse =
                        pair
                            ? 1f +
                              Mathf.Sin(
                                  safe *
                                  Mathf.PI *
                                  4f) *
                              0.12f
                            : 0.90f;

                    dice[index].Rect.localScale =
                        Vector3.one *
                        pulse;
                }

                break;
            }

            case ChinchiroCombination.Blank:
            {
                combinationText.text =
                    "무역";

                combinationText.color =
                    new Color(
                        0.72f,
                        0.74f,
                        0.80f,
                        1f);

                foreach (DieView die in dice)
                {
                    die.Background.color =
                        new Color(
                            0.62f,
                            0.64f,
                            0.69f,
                            1f);

                    die.Rect.localScale =
                        Vector3.one *
                        Mathf.Lerp(
                            1f,
                            0.93f,
                            safe);
                }

                break;
            }

            case ChinchiroCombination.Hifumi:
            {
                combinationText.text =
                    "히후미";

                combinationText.color =
                    new Color(
                        1f,
                        0.24f,
                        0.24f,
                        1f);

                float shake =
                    Mathf.Sin(
                        safe *
                        Mathf.PI *
                        22f) *
                    visualSize *
                    0.045f *
                    (1f - safe * 0.25f);

                selfRect.anchoredPosition =
                    baseAnchoredPosition +
                    new Vector2(
                        shake,
                        0f);

                foreach (DieView die in dice)
                {
                    die.Background.color =
                        new Color(
                            0.92f,
                            0.22f,
                            0.20f,
                            1f);

                    die.Rect.localScale =
                        Vector3.one *
                        (1f +
                         Mathf.Max(
                             0f,
                             Mathf.Sin(
                                 safe *
                                 Mathf.PI *
                                 6f)) *
                         0.10f);
                }

                break;
            }

            default:
            {
                combinationText.text =
                    "NONE";

                combinationText.color =
                    new Color(
                        0.62f,
                        0.64f,
                        0.70f,
                        1f);

                foreach (DieView die in dice)
                {
                    die.Background.color =
                        new Color(
                            0.48f,
                            0.50f,
                            0.56f,
                            1f);

                    die.Rect.localScale =
                        Vector3.one;
                }

                break;
            }
        }
    }

    private static void ResolveMokuPair(
        RollResult result,
        out int first,
        out int second)
    {
        first = 0;
        second = 1;

        if (result?.DiceValues == null ||
            result.DiceValues.Count < 3)
        {
            return;
        }

        int a = result.DiceValues[0];
        int b = result.DiceValues[1];
        int c = result.DiceValues[2];

        if (a == b)
        {
            first = 0;
            second = 1;
        }
        else if (a == c)
        {
            first = 0;
            second = 2;
        }
        else
        {
            first = 1;
            second = 2;
        }
    }

    private void SetHeader(
        RollResult result)
    {
        SkillResolverType resolver =
            result?.ResolverType ??
            SkillResolverType.Dice;

        resolverText.text =
            GetResolverLabel(
                resolver) +
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
        SkillResolverType resolver)
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
                "DICE"
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

    private static string BuildCoinPartialEquation(
        RollResult result,
        int lockedCount)
    {
        if (result == null)
            return "...";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower}");

        int count =
            Mathf.Min(
                lockedCount,
                result.CoinValues?.Count ?? 0);

        for (int index = 0;
             index < count;
             index++)
        {
            AppendSignedValue(
                builder,
                result.CoinValues[index]);
        }

        if (count <
            (result.CoinValues?.Count ?? 0))
        {
            builder.Append(" + ...");
        }

        return builder.ToString();
    }

    private static string BuildCoinEquation(
        RollResult result,
        int clashPower)
    {
        if (result == null)
            return $"= 최종위력 {clashPower}";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower}");

        if (result.CoinValues != null)
        {
            foreach (int value
                     in result.CoinValues)
            {
                AppendSignedValue(
                    builder,
                    value);
            }
        }
        else
        {
            AppendSignedValue(
                builder,
                result.RawValue);
        }

        AppendFinalPower(
            builder,
            result,
            clashPower);

        return builder.ToString();
    }

    private static string BuildDicePartialEquation(
        RollResult result,
        int lockedCount)
    {
        if (result == null)
            return "...";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower}");

        int count =
            Mathf.Min(
                lockedCount,
                result.DiceValues?.Count ?? 0);

        for (int index = 0;
             index < count;
             index++)
        {
            AppendSignedValue(
                builder,
                result.DiceValues[index]);
        }

        if (count <
            (result.DiceValues?.Count ?? 0))
        {
            builder.Append(" + ...");
        }

        return builder.ToString();
    }

    private static string BuildDiceEquation(
        RollResult result,
        int clashPower)
    {
        if (result == null)
            return $"= 최종위력 {clashPower}";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower}");

        if (result.DiceValues != null &&
            result.DiceValues.Count > 0)
        {
            foreach (int value
                     in result.DiceValues)
            {
                AppendSignedValue(
                    builder,
                    value);
            }
        }
        else
        {
            AppendSignedValue(
                builder,
                result.RawValue);
        }

        AppendFinalPower(
            builder,
            result,
            clashPower);

        return builder.ToString();
    }

    private static string BuildSlotEquation(
        RollResult result,
        int clashPower)
    {
        if (result == null)
            return $"= 최종위력 {clashPower}";

        int a =
            result.SlotA > 0
                ? result.SlotA
                : 1;

        int b =
            result.SlotB > 0
                ? result.SlotB
                : 1;

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower} + " +
            $"({a} × {b})");

        AppendFinalPower(
            builder,
            result,
            clashPower);

        return builder.ToString();
    }

    private static string BuildChinchiroEquation(
        RollResult result,
        int clashPower)
    {
        if (result == null)
            return $"= 최종위력 {clashPower}";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"기본위력 {result.BasePower}");

        AppendSignedValue(
            builder,
            result.ChinchiroBonus);

        builder.Append(
            $"  [{GetChinchiroName(result.ChinchiroCombination)}]");

        AppendFinalPower(
            builder,
            result,
            clashPower);

        return builder.ToString();
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

    private static string GetChinchiroName(
        ChinchiroCombination combination)
    {
        return combination switch
        {
            ChinchiroCombination.Arashi =>
                "아라시",

            ChinchiroCombination.Shigoro =>
                "시고로",

            ChinchiroCombination.Moku =>
                "목",

            ChinchiroCombination.Blank =>
                "무역",

            ChinchiroCombination.Hifumi =>
                "히후미",

            _ =>
                "NONE"
        };
    }

    private static int GetDiceValue(
        RollResult result,
        int index)
    {
        if (result?.DiceValues != null &&
            index >= 0 &&
            index < result.DiceValues.Count)
        {
            return result.DiceValues[index];
        }

        return result?.RawValue ?? 1;
    }

    private static bool GetCoinFace(
        RollResult result,
        int index)
    {
        if (result?.CoinFaces != null &&
            index >= 0 &&
            index < result.CoinFaces.Count)
        {
            return result.CoinFaces[index];
        }

        return false;
    }

    private static int GetDiceMinimum(
        RollResult result)
    {
        int minimum =
            result?.DiceMin ?? 1;

        int maximum =
            result?.DiceMax ?? 6;

        if (minimum == 0 &&
            maximum == 0)
        {
            return 1;
        }

        return
            Mathf.Min(
                minimum,
                maximum);
    }

    private static int GetDiceMaximum(
        RollResult result)
    {
        int minimum =
            result?.DiceMin ?? 1;

        int maximum =
            result?.DiceMax ?? 6;

        if (minimum == 0 &&
            maximum == 0)
        {
            return 6;
        }

        return
            Mathf.Max(
                minimum,
                maximum);
    }

    private static int NormalizeDieFace(
        int value)
    {
        if (value >= 1 &&
            value <= 6)
        {
            return value;
        }

        return
            Mathf.Abs(value) %
            6 +
            1;
    }

    private float GetCoinTokenSize(
        int count)
    {
        return
            Mathf.Clamp(
                visualSize *
                (count <= 3
                    ? 0.30f
                    : count <= 5
                        ? 0.23f
                        : 0.18f),
                10f,
                visualSize * 0.34f);
    }

    private float GetDieSize(
        int count)
    {
        return
            Mathf.Clamp(
                visualSize *
                (count <= 3
                    ? 0.34f
                    : count <= 5
                        ? 0.25f
                        : 0.19f),
                10f,
                visualSize * 0.38f);
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

    private void SetReelFinal(
        ReelView reel,
        int value)
    {
        if (reel == null)
            return;

        reel.ValueText.text =
            value.ToString();

        reel.ValueText.rectTransform.anchoredPosition =
            Vector2.zero;

        reel.Rect.localScale =
            Vector3.one;
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
                    HideFlags.HideAndDontSave
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
            HideFlags.HideAndDontSave;

        return circleSprite;
    }
}