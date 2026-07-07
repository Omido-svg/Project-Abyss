using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleWorldFloatingTextManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private BattleWorldFloatingTextUI floatingTextPrefab;

    [Header("Clash Text")]
    [SerializeField] private Vector3 clashTextOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] private float sideOffset = 0.65f;
    [SerializeField] private int sortingOrder = 100;

    [Header("Rolling")]
    [SerializeField] private float baseRollDuration = 0.55f;
    [SerializeField] private float baseTickInterval = 0.06f;
    [SerializeField] private float resultHoldDuration = 0.45f;
    [SerializeField] private float tieHoldDuration = 0.25f;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Tie Speed Up")]
    [SerializeField] private float speedMultiplierPerTie = 0.75f;
    [SerializeField] private float minRollDuration = 0.18f;
    [SerializeField] private float minTickInterval = 0.02f;
    [SerializeField] private int maxTieRerollCount = 10;

    [Header("Random Range")]
    [SerializeField] private int randomMin = 1;
    [SerializeField] private int randomMax = 20;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    public IEnumerator ShowClashPowerUntilResolved(
        Transform attackerAnchor,
        Transform targetAnchor,
        int firstAttackerValue,
        int firstTargetValue,
        Color normalColor,
        Color tieColor)
    {
        if (floatingTextPrefab == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] FloatingTextPrefab 없음");
            yield break;
        }

        if (attackerAnchor == null || targetAnchor == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] 합 표시 Anchor 없음");
            yield break;
        }

        if (worldCamera == null)
            worldCamera = Camera.main;

        BattleWorldFloatingTextUI attackerText =
            CreateText(
                attackerAnchor,
                leftSide: true,
                "?",
                normalColor);

        BattleWorldFloatingTextUI targetText =
            CreateText(
                targetAnchor,
                leftSide: false,
                "?",
                normalColor);

        int attackerValue = firstAttackerValue;
        int targetValue = firstTargetValue;

        int tieCount = 0;

        while (true)
        {
            float speedFactor =
                Mathf.Pow(speedMultiplierPerTie, tieCount);

            float rollDuration =
                Mathf.Max(
                    minRollDuration,
                    baseRollDuration * speedFactor);

            float tickInterval =
                Mathf.Max(
                    minTickInterval,
                    baseTickInterval * speedFactor);

            attackerText.SetColor(normalColor);
            targetText.SetColor(normalColor);

            Coroutine attackerRoll =
                StartCoroutine(
                    attackerText.RollToValue(
                        attackerValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

            Coroutine targetRoll =
                StartCoroutine(
                    targetText.RollToValue(
                        targetValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

            yield return attackerRoll;
            yield return targetRoll;

            Debug.Log(
                $"[BattleWorldFloatingTextManager] 합 결과 Round {tieCount + 1} : " +
                $"{attackerValue} vs {targetValue}");

            if (attackerValue != targetValue)
                break;

            tieCount++;

            attackerText.SetColor(tieColor);
            targetText.SetColor(tieColor);

            attackerText.SetText("동률");
            targetText.SetText("동률");

            yield return new WaitForSeconds(tieHoldDuration);

            if (tieCount >= maxTieRerollCount)
            {
                Debug.LogWarning(
                    "[BattleWorldFloatingTextManager] 동률 반복 횟수 초과. 강제로 결과를 분리합니다.");

                targetValue =
                    Mathf.Clamp(
                        targetValue - 1,
                        randomMin,
                        randomMax);

                if (targetValue == attackerValue)
                {
                    targetValue =
                        Mathf.Clamp(
                            targetValue + 2,
                            randomMin,
                            randomMax);
                }

                continue;
            }

            attackerValue =
                Random.Range(randomMin, randomMax + 1);

            targetValue =
                Random.Range(randomMin, randomMax + 1);
        }

        yield return new WaitForSeconds(resultHoldDuration);

        Coroutine attackerFade =
            StartCoroutine(attackerText.FadeAndDestroy(fadeDuration));

        Coroutine targetFade =
            StartCoroutine(targetText.FadeAndDestroy(fadeDuration));

        yield return attackerFade;
        yield return targetFade;
    }

    private BattleWorldFloatingTextUI CreateText(
        Transform anchor,
        bool leftSide,
        string initialText,
        Color color)
    {
        float rightOffset =
            leftSide ? -sideOffset : sideOffset;

        BattleWorldFloatingTextUI ui =
            Instantiate(
                floatingTextPrefab,
                anchor.position,
                Quaternion.identity);

        ui.Initialize(
            anchor,
            worldCamera,
            clashTextOffset,
            rightOffset,
            initialText,
            color,
            sortingOrder);

        return ui;
    }
    
    public IEnumerator ShowClashPowerSequence(
        Transform attackerAnchor,
        Transform targetAnchor,
        IReadOnlyList<ClashRollVisualStep> steps,
        Color normalColor,
        Color tieColor)
    {
        if (steps == null || steps.Count == 0)
            yield break;

        if (floatingTextPrefab == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] FloatingTextPrefab 없음");
            yield break;
        }

        if (attackerAnchor == null || targetAnchor == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] 합 표시 Anchor 없음");
            yield break;
        }

        if (worldCamera == null)
            worldCamera = Camera.main;

        BattleWorldFloatingTextUI attackerText =
            CreateText(
                attackerAnchor,
                leftSide: true,
                "?",
                normalColor);

        BattleWorldFloatingTextUI targetText =
            CreateText(
                targetAnchor,
                leftSide: false,
                "?",
                normalColor);

        for (int i = 0; i < steps.Count; i++)
        {
            ClashRollVisualStep step =
                steps[i];

            float speedFactor =
                Mathf.Pow(speedMultiplierPerTie, i);

            float rollDuration =
                Mathf.Max(
                    minRollDuration,
                    baseRollDuration * speedFactor);

            float tickInterval =
                Mathf.Max(
                    minTickInterval,
                    baseTickInterval * speedFactor);

            attackerText.SetColor(normalColor);
            targetText.SetColor(normalColor);

            Coroutine attackerRoll =
                StartCoroutine(
                    attackerText.RollToValue(
                        step.AttackerValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

            Coroutine targetRoll =
                StartCoroutine(
                    targetText.RollToValue(
                        step.TargetValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

            yield return attackerRoll;
            yield return targetRoll;

            if (!step.IsTie)
                break;

            attackerText.SetColor(tieColor);
            targetText.SetColor(tieColor);

            attackerText.SetText("동률");
            targetText.SetText("동률");

            yield return new WaitForSeconds(tieHoldDuration);
        }

        yield return new WaitForSeconds(resultHoldDuration);

        Coroutine attackerFade =
            StartCoroutine(
                attackerText.FadeAndDestroy(fadeDuration));

        Coroutine targetFade =
            StartCoroutine(
                targetText.FadeAndDestroy(fadeDuration));

        yield return attackerFade;
        yield return targetFade;
    }
}