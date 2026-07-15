using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BattleWorldFloatingTextManager : MonoBehaviour
{
    private const string TieText = "동률";

    [Header("Pool")]
    [SerializeField, Min(0)] private int maxPoolSize = 4;

    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private BattleWorldFloatingTextUI floatingTextPrefab;

    [Header("Clash Text Anchor")]
    [Tooltip("HEAD Anchor 위에 추가되는 월드 좌표 오프셋입니다.")]
    [SerializeField]
    private Vector3 clashTextOffset =
        new Vector3(0f, 0.35f, 0f);

    [Header("Screen Separation")]
    [SerializeField]
    private bool useScreenSpaceSeparation = true;

    [Tooltip("각 캐릭터 머리 위치에서 화면 바깥 방향으로 벌리는 기본 간격입니다.")]
    [SerializeField, Min(0f)]
    private float horizontalOffsetPixels = 110f;

    [Tooltip("두 결과 UI 중심 사이에 보장할 최소 화면 간격입니다.")]
    [SerializeField, Min(0f)]
    private float minimumPairSeparationPixels = 360f;

    [Tooltip("머리 Anchor에서 화면 위쪽으로 추가 이동하는 픽셀입니다.")]
    [SerializeField, Min(0f)]
    private float verticalOffsetPixels = 24f;

    [SerializeField]
    private bool keepInsideViewport = true;

    [SerializeField, Min(0f)]
    private float viewportPaddingPixels = 80f;

    [FormerlySerializedAs("sideOffset")]
    [SerializeField, HideInInspector]
    private float legacyWorldSideOffset = 0.65f;

    [SerializeField]
    private int sortingOrder = 100;

    [Header("Rolling Readability")]
    [SerializeField, Min(0f)]
    private float baseRollDuration = 0.8f;

    [SerializeField, Min(0.01f)]
    private float baseTickInterval = 0.07f;

    [SerializeField, Min(0f)]
    private float resultHoldDuration = 1.5f;

    [SerializeField, Min(0f)]
    private float tieHoldDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float fadeDuration = 0.35f;

    [SerializeField, HideInInspector]
    private int readabilitySettingsVersion;

    private const int CurrentReadabilitySettingsVersion = 1;

    [Header("Tie Speed Up")]
    [SerializeField] private float speedMultiplierPerTie = 0.75f;
    [SerializeField] private float minRollDuration = 0.18f;
    [SerializeField] private float minTickInterval = 0.02f;
    [SerializeField] private int maxTieRerollCount = 10;

    [Header("Random Range")]
    [SerializeField] private int randomMin = 1;
    [SerializeField] private int randomMax = 20;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Stack<BattleWorldFloatingTextUI> textPool =
        new Stack<BattleWorldFloatingTextUI>();

    private readonly HashSet<BattleWorldFloatingTextUI> activeTexts =
        new HashSet<BattleWorldFloatingTextUI>();

    private bool isShuttingDown;
    private int lifecycleVersion;

    private struct ClashTextPair
    {
        public BattleWorldFloatingTextUI Attacker;
        public BattleWorldFloatingTextUI Target;
        public int LifecycleVersion;
    }

    private void Awake()
    {
        MigrateAndClampReadabilitySettings();
        ResolveWorldCamera();
    }

    private void OnValidate()
    {
        MigrateAndClampReadabilitySettings();
    }

    private void OnDisable()
    {
        CancelAllActiveTexts();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        DestroyAllTrackedTexts();
    }

    // Legacy API kept for compatibility with existing callers.
    public IEnumerator ShowClashPowerUntilResolved(
        Transform attackerAnchor,
        Transform targetAnchor,
        int firstAttackerValue,
        int firstTargetValue,
        Color normalColor,
        Color tieColor)
    {
        ClashTextPair pair;

        if (!TryCreateTextPair(
                attackerAnchor,
                targetAnchor,
                normalColor,
                out pair))
        {
            yield break;
        }

        try
        {
            int attackerValue = firstAttackerValue;
            int targetValue = firstTargetValue;
            int tieCount = 0;

            while (true)
            {
                float rollDuration;
                float tickInterval;
                GetRollTiming(tieCount, out rollDuration, out tickInterval);

                SetPairColor(pair, normalColor);

                yield return RunPairInParallel(
                    pair.Attacker,
                    pair.Attacker.RollToValue(
                        attackerValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax),
                    pair.Target,
                    pair.Target.RollToValue(
                        targetValue,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

                if (!IsPairActive(pair))
                    yield break;

                if (logDebug)
                {
                    Debug.Log(
                        $"[BattleWorldFloatingTextManager] 합 결과 Round {tieCount + 1} : " +
                        $"{attackerValue} vs {targetValue}");
                }

                if (attackerValue != targetValue)
                    break;

                tieCount++;
                yield return ShowTie(pair, tieColor);

                if (!IsPairActive(pair))
                    yield break;

                if (tieCount >= maxTieRerollCount)
                {
                    Debug.LogWarning(
                        "[BattleWorldFloatingTextManager] 동률 반복 횟수 초과. 강제로 결과를 분리합니다.");

                    if (!TryCreateDifferentValue(attackerValue, out targetValue))
                        break;

                    continue;
                }

                attackerValue =
                    Random.Range(randomMin, randomMax + 1);

                targetValue =
                    Random.Range(randomMin, randomMax + 1);
            }

            yield return WaitForDuration(resultHoldDuration);

            if (!IsPairActive(pair))
                yield break;

            yield return FadePair(pair);
        }
        finally
        {
            ReleasePair(pair);
        }
    }

    public IEnumerator ShowClashPowerSequence(
        Transform attackerAnchor,
        Transform targetAnchor,
        IReadOnlyList<ClashRollVisualStep> steps,
        Color normalColor,
        Color tieColor)
    {
        yield return ShowClashPowerSequence(
            attackerAnchor,
            targetAnchor,
            steps,
            normalColor,
            tieColor,
            null,
            null);
    }

    public IEnumerator ShowClashPowerSequence(
        Transform attackerAnchor,
        Transform targetAnchor,
        IReadOnlyList<ClashRollVisualStep> steps,
        Color normalColor,
        Color tieColor,
        string attackerLabel,
        string targetLabel)
    {
        if (steps == null ||
            steps.Count == 0)
        {
            yield break;
        }

        ClashTextPair pair;

        if (!TryCreateTextPair(
                attackerAnchor,
                targetAnchor,
                normalColor,
                out pair,
                attackerLabel,
                targetLabel))
        {
            yield break;
        }

        try
        {
            for (int i = 0; i < steps.Count; i++)
            {
                ClashRollVisualStep step = steps[i];

                float rollDuration;
                float tickInterval;
                GetRollTiming(i, out rollDuration, out tickInterval);

                SetPairColor(pair, normalColor);

                if (logDebug)
                {
                    Debug.Log(
                        $"[BattleWorldFloatingTextManager] 합 표시 / " +
                        $"Round={step.RoundIndex}, " +
                        $"AttackerFinal={step.AttackerFinalPower}, " +
                        $"AttackerSpeed={step.AttackerSpeedModifier}, " +
                        $"AttackerMomentum={step.AttackerMomentumModifier}, " +
                        $"AttackerClash={step.AttackerValue}, " +
                        $"TargetFinal={step.TargetFinalPower}, " +
                        $"TargetSpeed={step.TargetSpeedModifier}, " +
                        $"TargetMomentum={step.TargetMomentumModifier}, " +
                        $"TargetClash={step.TargetValue}");
                }

                yield return RunPairInParallel(
                    pair.Attacker,
                    pair.Attacker.RollToClashResult(
                        step.AttackerRollResult,
                        step.AttackerValue,
                        step.AttackerSpeedModifier,
                        step.AttackerMomentumModifier,
                        step.AttackerCritical,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax),
                    pair.Target,
                    pair.Target.RollToClashResult(
                        step.TargetRollResult,
                        step.TargetValue,
                        step.TargetSpeedModifier,
                        step.TargetMomentumModifier,
                        step.TargetCritical,
                        rollDuration,
                        tickInterval,
                        randomMin,
                        randomMax));

                if (!IsPairActive(pair))
                    yield break;

                if (!step.IsTie)
                    break;

                yield return ShowTie(pair, tieColor);

                if (!IsPairActive(pair))
                    yield break;
            }

            yield return WaitForDuration(resultHoldDuration);

            if (!IsPairActive(pair))
                yield break;

            yield return FadePair(pair);
        }
        finally
        {
            ReleasePair(pair);
        }
    }

    private bool TryCreateTextPair(
        Transform attackerAnchor,
        Transform targetAnchor,
        Color normalColor,
        out ClashTextPair pair,
        string attackerLabel = null,
        string targetLabel = null)
    {
        pair = default(ClashTextPair);

        if (!isActiveAndEnabled || isShuttingDown)
            return false;

        if (floatingTextPrefab == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] FloatingTextPrefab 없음");
            return false;
        }

        if (attackerAnchor == null || targetAnchor == null)
        {
            Debug.LogWarning("[BattleWorldFloatingTextManager] 합 표시 Anchor 없음");
            return false;
        }

        ResolveWorldCamera();
        pair.LifecycleVersion = lifecycleVersion;

        pair.Attacker =
            CreateText(
                attackerAnchor,
                targetAnchor,
                fallbackSideSign: -1f,
                attackerLabel,
                "?",
                normalColor);

        pair.Target =
            CreateText(
                targetAnchor,
                attackerAnchor,
                fallbackSideSign: 1f,
                targetLabel,
                "?",
                normalColor);

        if (pair.Attacker != null && pair.Target != null)
            return true;

        ReleasePair(pair);
        pair = default(ClashTextPair);
        return false;
    }

    private BattleWorldFloatingTextUI CreateText(
        Transform anchor,
        Transform opposingAnchor,
        float fallbackSideSign,
        string ownerLabel,
        string initialText,
        Color color)
    {
        BattleWorldFloatingTextUI ui =
            GetTextFromPool();

        if (ui == null)
        {
            ui = Instantiate(
                floatingTextPrefab,
                anchor.position,
                Quaternion.identity);
        }
        else
        {
            ui.transform.SetParent(
                null,
                false);

            ui.transform.SetPositionAndRotation(
                anchor.position,
                Quaternion.identity);
        }

        ui.gameObject.SetActive(true);
        activeTexts.Add(ui);

        ui.InitializeForClash(
            anchor,
            opposingAnchor,
            worldCamera,
            clashTextOffset,
            useScreenSpaceSeparation,
            fallbackSideSign,
            horizontalOffsetPixels,
            minimumPairSeparationPixels,
            verticalOffsetPixels,
            keepInsideViewport,
            viewportPaddingPixels,
            legacyWorldSideOffset,
            ownerLabel,
            initialText,
            color,
            sortingOrder);

        return ui;
    }

    private BattleWorldFloatingTextUI GetTextFromPool()
    {
        while (textPool.Count > 0)
        {
            BattleWorldFloatingTextUI ui = textPool.Pop();

            if (ui != null)
                return ui;
        }

        return null;
    }

    private void GetRollTiming(
        int tieIndex,
        out float rollDuration,
        out float tickInterval)
    {
        float speedFactor =
            Mathf.Pow(speedMultiplierPerTie, Mathf.Max(0, tieIndex));

        rollDuration =
            Mathf.Max(
                minRollDuration,
                baseRollDuration * speedFactor);

        tickInterval =
            Mathf.Max(
                minTickInterval,
                baseTickInterval * speedFactor);
    }

    private static void SetPairColor(
        ClashTextPair pair,
        Color color)
    {
        if (pair.Attacker != null)
            pair.Attacker.SetColor(color);

        if (pair.Target != null)
            pair.Target.SetColor(color);
    }

    private IEnumerator ShowTie(
        ClashTextPair pair,
        Color tieColor)
    {
        SetPairColor(pair, tieColor);

        if (pair.Attacker != null)
            pair.Attacker.SetText(TieText);

        if (pair.Target != null)
            pair.Target.SetText(TieText);

        yield return WaitForDuration(tieHoldDuration);
    }

    private IEnumerator FadePair(ClashTextPair pair)
    {
        if (pair.Attacker == null || pair.Target == null)
            yield break;

        yield return RunPairInParallel(
            pair.Attacker,
            pair.Attacker.FadeOut(fadeDuration),
            pair.Target,
            pair.Target.FadeOut(fadeDuration));
    }

    private IEnumerator RunPairInParallel(
        BattleWorldFloatingTextUI attacker,
        IEnumerator attackerRoutine,
        BattleWorldFloatingTextUI target,
        IEnumerator targetRoutine)
    {
        Coroutine attackerCoroutine = null;
        Coroutine targetCoroutine = null;

        try
        {
            attackerCoroutine = attacker.StartCoroutine(attackerRoutine);
            targetCoroutine = target.StartCoroutine(targetRoutine);

            yield return attackerCoroutine;
            attackerCoroutine = null;

            yield return targetCoroutine;
            targetCoroutine = null;
        }
        finally
        {
            if (attacker != null && attackerCoroutine != null)
                attacker.StopCoroutine(attackerCoroutine);

            if (target != null && targetCoroutine != null)
                target.StopCoroutine(targetCoroutine);
        }
    }

    private static IEnumerator WaitForDuration(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);
    }

    private bool TryCreateDifferentValue(
        int attackerValue,
        out int targetValue)
    {
        if (randomMax <= randomMin)
        {
            targetValue = attackerValue;
            return false;
        }

        targetValue =
            attackerValue > randomMin
                ? attackerValue - 1
                : attackerValue + 1;

        targetValue = Mathf.Clamp(targetValue, randomMin, randomMax);
        return targetValue != attackerValue;
    }

    private void MigrateAndClampReadabilitySettings()
    {
        if (readabilitySettingsVersion <
            CurrentReadabilitySettingsVersion)
        {
            if (Mathf.Approximately(
                    baseRollDuration,
                    0.55f))
            {
                baseRollDuration = 0.8f;
            }

            if (Mathf.Approximately(
                    baseTickInterval,
                    0.06f))
            {
                baseTickInterval = 0.07f;
            }

            if (Mathf.Approximately(
                    resultHoldDuration,
                    0.45f))
            {
                resultHoldDuration = 1.5f;
            }

            if (Mathf.Approximately(
                    tieHoldDuration,
                    0.25f))
            {
                tieHoldDuration = 0.5f;
            }

            if (Mathf.Approximately(
                    fadeDuration,
                    0.2f))
            {
                fadeDuration = 0.35f;
            }

            if (horizontalOffsetPixels <= 0f)
                horizontalOffsetPixels = 110f;

            if (minimumPairSeparationPixels <= 0f)
                minimumPairSeparationPixels = 360f;

            if (verticalOffsetPixels < 0f)
                verticalOffsetPixels = 24f;

            if (viewportPaddingPixels <= 0f)
                viewportPaddingPixels = 80f;

            readabilitySettingsVersion =
                CurrentReadabilitySettingsVersion;
        }

        maxPoolSize =
            Mathf.Max(
                0,
                maxPoolSize);

        horizontalOffsetPixels =
            Mathf.Max(
                0f,
                horizontalOffsetPixels);

        minimumPairSeparationPixels =
            Mathf.Max(
                0f,
                minimumPairSeparationPixels);

        verticalOffsetPixels =
            Mathf.Max(
                0f,
                verticalOffsetPixels);

        viewportPaddingPixels =
            Mathf.Max(
                0f,
                viewportPaddingPixels);

        baseRollDuration =
            Mathf.Max(
                0f,
                baseRollDuration);

        baseTickInterval =
            Mathf.Max(
                0.01f,
                baseTickInterval);

        resultHoldDuration =
            Mathf.Max(
                0f,
                resultHoldDuration);

        tieHoldDuration =
            Mathf.Max(
                0f,
                tieHoldDuration);

        fadeDuration =
            Mathf.Max(
                0f,
                fadeDuration);

        speedMultiplierPerTie =
            Mathf.Clamp(
                speedMultiplierPerTie,
                0.05f,
                1f);

        minRollDuration =
            Mathf.Max(
                0f,
                minRollDuration);

        minTickInterval =
            Mathf.Max(
                0.01f,
                minTickInterval);

        maxTieRerollCount =
            Mathf.Max(
                1,
                maxTieRerollCount);

        if (randomMax < randomMin)
            randomMax = randomMin;
    }

    private void ResolveWorldCamera()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void ReleasePair(ClashTextPair pair)
    {
        if (pair.LifecycleVersion != lifecycleVersion)
            return;

        ReleaseText(pair.Attacker);
        ReleaseText(pair.Target);
    }

    private bool IsPairActive(ClashTextPair pair)
    {
        return
            pair.LifecycleVersion == lifecycleVersion &&
            isActiveAndEnabled &&
            !isShuttingDown &&
            pair.Attacker != null &&
            pair.Target != null &&
            activeTexts.Contains(pair.Attacker) &&
            activeTexts.Contains(pair.Target);
    }

    private void ReleaseText(BattleWorldFloatingTextUI ui)
    {
        if (!activeTexts.Remove(ui))
            return;

        if (ui == null)
            return;

        ui.ResetForPool();

        if (isShuttingDown || textPool.Count >= maxPoolSize)
        {
            Destroy(ui.gameObject);
            return;
        }

        ui.transform.SetParent(transform, false);
        ui.gameObject.SetActive(false);
        textPool.Push(ui);
    }

    private void ReleaseAllActiveTexts()
    {
        while (activeTexts.Count > 0)
        {
            BattleWorldFloatingTextUI ui = GetFirstActiveText();
            ReleaseText(ui);
        }
    }

    public void CancelAllActiveTexts()
    {
        lifecycleVersion++;
        StopAllCoroutines();
        ReleaseAllActiveTexts();
    }

    private BattleWorldFloatingTextUI GetFirstActiveText()
    {
        foreach (BattleWorldFloatingTextUI ui in activeTexts)
            return ui;

        return null;
    }

    private void DestroyAllTrackedTexts()
    {
        while (activeTexts.Count > 0)
        {
            BattleWorldFloatingTextUI ui = GetFirstActiveText();
            activeTexts.Remove(ui);
            DestroyText(ui);
        }

        while (textPool.Count > 0)
            DestroyText(textPool.Pop());
    }

    private static void DestroyText(BattleWorldFloatingTextUI ui)
    {
        if (ui == null)
            return;

        ui.ResetForPool();
        Destroy(ui.gameObject);
    }
}