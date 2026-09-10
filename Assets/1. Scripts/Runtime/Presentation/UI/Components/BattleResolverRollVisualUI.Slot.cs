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
                BattlePlaybackSpeedController.BattleUnscaledDeltaTime;

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

}
