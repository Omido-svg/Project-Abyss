using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BattleResolverRollVisualUI :
    MonoBehaviour
{
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
                count,
                result);

        float elapsed = 0f;
        float nextUpdate = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                BattlePlaybackSpeedController.BattleUnscaledDeltaTime;

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
                        value);
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
                count,
                result);

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
                GetDiceValue(
                    result,
                    index));

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

    private List<DieView> CreateDiceViews(
        int count,
        RollResult result)
    {
        return CreateDiceViews(
            count,
            GetDiceVisualSideCount(result),
            UsesClassicSixSidedPips(result));
    }

    private List<DieView> CreateClassicDiceViews(
        int count)
    {
        return CreateDiceViews(
            count,
            6,
            true);
    }

    private List<DieView> CreateDiceViews(
        int count,
        int sideCount,
        bool useClassicPips)
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
                    size,
                    sideCount,
                    useClassicPips));
        }

        return result;
    }

    private DieView CreateDie(
        string objectName,
        float size,
        int sideCount,
        bool useClassicPips)
    {
        GameObject dieObject =
            CreateGeneratedObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DicePolygonGraphic));

        RectTransform rect =
            dieObject.GetComponent<
                RectTransform>();

        rect.sizeDelta =
            Vector2.one *
            size;

        DicePolygonGraphic background =
            dieObject.GetComponent<
                DicePolygonGraphic>();

        background.SideCount =
            Mathf.Clamp(
                sideCount,
                3,
                20);

        background.color =
            diceColor;

        background.raycastTarget = false;

        Outline outline =
            dieObject.AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.06f,
                0.07f,
                0.10f,
                0.92f);

        float outlineSize =
            Mathf.Max(
                1f,
                size * 0.035f);

        outline.effectDistance =
            new Vector2(
                outlineSize,
                -outlineSize);

        outline.useGraphicAlpha = true;

        DieView result =
            new DieView
            {
                Rect = rect,
                Background = background,
                UseClassicPips = useClassicPips
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

        result.ValueText =
            CreateTextChild(
                rect,
                "FaceValue",
                "1",
                size * 0.42f,
                FontStyles.Bold);

        result.ValueText.color =
            new Color(
                0.07f,
                0.08f,
                0.11f,
                1f);

        result.ValueText.outlineWidth = 0f;
        result.ValueText.gameObject.SetActive(
            !useClassicPips);

        SetDieFace(
            result,
            1);

        return result;
    }

    private static void SetDieFace(
        DieView die,
        int face)
    {
        if (die == null)
            return;

        if (!die.UseClassicPips)
        {
            for (int index = 0;
                 index < die.Pips.Count;
                 index++)
            {
                die.Pips[index]
                    .gameObject
                    .SetActive(false);
            }

            if (die.ValueText != null)
            {
                die.ValueText.gameObject.SetActive(true);
                die.ValueText.text = face.ToString();
            }

            return;
        }

        if (die.ValueText != null)
            die.ValueText.gameObject.SetActive(false);

        if (die.Pips.Count < 7)
            return;

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

    private static int GetDiceVisualSideCount(
        RollResult result)
    {
        int minimum =
            GetDiceMinimum(result);

        int maximum =
            GetDiceMaximum(result);

        int outcomeCount =
            Mathf.Max(
                1,
                maximum - minimum + 1);

        return
            Mathf.Clamp(
                outcomeCount,
                3,
                20);
    }

    private static bool UsesClassicSixSidedPips(
        RollResult result)
    {
        return
            GetDiceMinimum(result) == 1 &&
            GetDiceMaximum(result) == 6;
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

}
