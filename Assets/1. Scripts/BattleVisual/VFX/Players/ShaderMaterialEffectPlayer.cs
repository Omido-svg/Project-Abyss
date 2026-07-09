using System.Collections;
using UnityEngine;

public class ShaderMaterialEffectPlayer : MonoBehaviour, IBattleVfxPlayable
{
    [SerializeField] private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [SerializeField] private string colorProperty = "_Color";
    [SerializeField] private string progressProperty = "_Progress";
    [SerializeField] private string intensityProperty = "_Intensity";
    [SerializeField] private string radiusProperty = "_Radius";

    [Header("Animation")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private AnimationCurve progressCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private Coroutine routine;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>(true);

        propertyBlock = new MaterialPropertyBlock();
    }

    public void Play(
        BattleVfxPlayData playData)
    {
        if (targetRenderer == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine =
            StartCoroutine(
                PlayRoutine(playData));
    }

    private IEnumerator PlayRoutine(
        BattleVfxPlayData playData)
    {
        float lifetime =
            Mathf.Max(
                0.01f,
                playData.Lifetime);

        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / lifetime);

            float progress =
                progressCurve.Evaluate(t);

            ApplyProperties(
                playData,
                progress);

            if (faceCamera &&
                Camera.main != null)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        transform.position - Camera.main.transform.position);
            }

            yield return null;
        }

        ApplyProperties(
            playData,
            1f);
    }

    private void ApplyProperties(
        BattleVfxPlayData playData,
        float progress)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.GetPropertyBlock(
            propertyBlock);

        propertyBlock.SetColor(
            colorProperty,
            playData.Color);

        propertyBlock.SetFloat(
            progressProperty,
            progress);

        propertyBlock.SetFloat(
            intensityProperty,
            playData.Intensity);

        propertyBlock.SetFloat(
            radiusProperty,
            playData.Radius);

        targetRenderer.SetPropertyBlock(
            propertyBlock);
    }
}