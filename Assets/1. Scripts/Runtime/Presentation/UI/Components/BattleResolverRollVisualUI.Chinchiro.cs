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
    private IEnumerator PlayChinchiro(
        RollResult result,
        int clashPower,
        float duration,
        float updateInterval,
        System.Random random)
    {
        List<DieView> dice =
            CreateClassicDiceViews(
                3);

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

    private void BuildFinalChinchiro(
        RollResult result,
        int clashPower)
    {
        List<DieView> dice =
            CreateClassicDiceViews(
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

}
