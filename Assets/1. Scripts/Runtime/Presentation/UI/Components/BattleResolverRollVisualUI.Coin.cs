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

}
