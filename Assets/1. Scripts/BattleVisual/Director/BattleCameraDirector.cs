using System.Collections;
using UnityEngine;

public class BattleCameraDirector : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;

    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = GetComponent<CameraController>();

        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    public void FocusBetween(
        Character a,
        Character b)
    {
        if (cameraController == null)
            return;

        cameraController.FocusBetween(
            a,
            b);
    }

    public void Return()
    {
        if (cameraController == null)
            return;

        cameraController.Return();
    }

    public IEnumerator WaitUntilArrived(
        float timeout)
    {
        if (cameraController == null)
            yield break;

        yield return cameraController.WaitUntilArrived(
            timeout);
    }

    public void PlayShake(BattleCameraShakeSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[BattleCameraDirector] Shake 실패 : settings가 null입니다.");
            return;
        }

        Debug.Log(
            $"[BattleCameraDirector] Shake 실행 / " +
            $"Duration={settings.Duration}, " +
            $"Frequency={settings.Frequency}, " +
            $"Position={settings.PositionStrength}, " +
            $"Rotation={settings.RotationStrength}");

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine =
            StartCoroutine(
                ShakeRoutine(settings));
    }

    public IEnumerator PlayShotsByTiming(
        BattleVisualRequest request,
        SkillCameraDefinition definition,
        SkillCameraShotTiming timing)
    {
        if (request == null)
            yield break;

        if (definition == null ||
            definition.Shots == null)
            yield break;

        foreach (SkillCameraShot shot in definition.Shots)
        {
            if (shot == null)
                continue;

            if (shot.Timing != timing)
                continue;

            yield return PlayShot(
                request,
                shot);
        }
    }

    public IEnumerator PlayShot(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (request == null || shot == null)
            yield break;

        if (cameraController == null)
            yield break;

        if (shot.UseSceneCameraPoint)
        {
            bool success =
                TryPlaySceneCameraPoint(
                    request,
                    shot);

            if (success)
            {
                if (shot.BlendWaitTime > 0f)
                {
                    yield return cameraController.WaitUntilArrived(
                        shot.BlendWaitTime);
                }

                if (shot.UseShake)
                    PlayShake(shot.Shake);

                if (shot.Duration > 0f)
                {
                    yield return new WaitForSeconds(
                        shot.Duration);
                }

                yield break;
            }
        }

        switch (shot.ShotType)
        {
            case SkillCameraShotType.FocusBetween:
            case SkillCameraShotType.ClashWide:
                cameraController.FocusBetween(
                    request.Attacker,
                    request.Target);
                break;

            case SkillCameraShotType.AttackerClose:
            case SkillCameraShotType.AttackerOverShoulder:
                cameraController.FocusCharacter(
                    request.Attacker,
                    shot.PositionOffset,
                    shot.LookAtOffset,
                    shot.FocusDistance);
                break;

            case SkillCameraShotType.TargetClose:
            case SkillCameraShotType.TargetOverShoulder:
            case SkillCameraShotType.HitImpact:
                cameraController.FocusCharacter(
                    request.Target,
                    shot.PositionOffset,
                    shot.LookAtOffset,
                    shot.FocusDistance);
                break;

            case SkillCameraShotType.ReturnOverview:
                cameraController.Return();
                break;
        }

        if (shot.BlendWaitTime > 0f)
        {
            yield return cameraController.WaitUntilArrived(
                shot.BlendWaitTime);
        }

        if (shot.UseShake)
        {
            PlayShake(
                shot.Shake);
        }

        if (shot.Duration > 0f)
        {
            yield return new WaitForSeconds(
                shot.Duration);
        }
    }
    
    private bool TryPlaySceneCameraPoint(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (request == null || shot == null)
            return false;

        if (cameraController == null)
            return false;

        Transform cameraPoint =
            ResolveCameraPoint(
                request,
                shot.CameraPoint);

        Transform lookAtPoint =
            ResolveCameraPoint(
                request,
                shot.LookAtPoint);

        if (cameraPoint == null)
        {
            Debug.LogWarning(
                $"[BattleCameraDirector] CameraPoint 찾기 실패 / " +
                $"Owner={shot.CameraPoint.Owner}, Key={shot.CameraPoint.Key}");

            return false;
        }

        bool useCameraPointRotation =
            shot.RotationMode == SkillCameraRotationMode.CameraPointRotation;

        cameraController.FocusFromTransform(
            cameraPoint,
            lookAtPoint,
            useCameraPointRotation);

        return true;
    }

    private Transform ResolveCameraPoint(
        BattleVisualRequest request,
        SkillCameraPointReference pointReference)
    {
        if (request == null || pointReference == null)
            return null;

        Character owner =
            GetCameraPointOwner(
                request,
                pointReference.Owner);

        if (owner == null)
            return null;

        CharacterCameraPointSet pointSet =
            owner.GetComponentInChildren<CharacterCameraPointSet>(true);

        if (pointSet == null)
            return null;

        return pointSet.GetPoint(
            pointReference.Key);
    }

    private Character GetCameraPointOwner(
        BattleVisualRequest request,
        SkillCameraPointOwner ownerType)
    {
        if (request == null)
            return null;

        switch (ownerType)
        {
            case SkillCameraPointOwner.Attacker:
                return request.Attacker;

            case SkillCameraPointOwner.Target:
                return request.Target;
        }

        return null;
    }

    private IEnumerator ShakeRoutine(
        BattleCameraShakeSettings settings)
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
                settings.StrengthCurve != null
                    ? settings.StrengthCurve.Evaluate(t)
                    : 1f - t;

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