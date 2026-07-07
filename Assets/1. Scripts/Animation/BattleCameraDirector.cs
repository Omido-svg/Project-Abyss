using System.Collections;
using UnityEngine;

public class BattleCameraDirector : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;

    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    public void FocusBetween(
        Character a,
        Character b)
    {
        if (cameraController == null)
            return;

        cameraController.FocusBetween(a, b);
    }

    public void Return()
    {
        if (cameraController == null)
            return;

        cameraController.Return();
    }

    public IEnumerator WaitUntilArrived(float timeout)
    {
        if (cameraController == null)
            yield break;

        yield return cameraController.WaitUntilArrived(timeout);
    }

    public void PlayShake(BattleCameraShakeSettings settings)
    {
        if (settings == null)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine =
            StartCoroutine(
                ShakeRoutine(settings));
    }

    private IEnumerator ShakeRoutine(BattleCameraShakeSettings settings)
    {
        if (cameraController == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < settings.Duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(elapsed / settings.Duration);

            float power =
                1f - t;

            float noiseTime =
                Time.time * settings.Frequency;

            Vector3 positionOffset =
                new Vector3(
                    Mathf.PerlinNoise(noiseTime, 0.1f) - 0.5f,
                    Mathf.PerlinNoise(0.2f, noiseTime) - 0.5f,
                    0f) *
                settings.PositionStrength *
                power;

            Vector3 rotationOffset =
                new Vector3(
                    0f,
                    0f,
                    (Mathf.PerlinNoise(noiseTime, 0.3f) - 0.5f) *
                    settings.RotationStrength *
                    power);

            cameraController.SetNoiseOffset(
                positionOffset,
                rotationOffset);

            yield return null;
        }

        cameraController.SetNoiseOffset(
            Vector3.zero,
            Vector3.zero);

        shakeRoutine = null;
    }
}