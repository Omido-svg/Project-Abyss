using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class BattleCameraDirector : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private BattleCinemachineRig rig;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BattleCinemachineTargetGroupBinder groupBinder;

    private void Awake()
    {
        ResolveReferences();

        if (rig != null &&
            rig.TargetGroup != null)
        {
            groupBinder =
                new BattleCinemachineTargetGroupBinder(
                    rig.TargetGroup);
        }
    }

    private void ResolveReferences()
    {
        if (rig == null)
            rig = FindFirstObjectByType<BattleCinemachineRig>();
    }

    public void FocusBetween(
        Character a,
        Character b)
    {
        if (rig == null ||
            rig.GroupCamera == null ||
            groupBinder == null)
        {
            return;
        }

        Transform aTarget =
            BattleCameraTargetResolver.GetLookAtTarget(a);

        Transform bTarget =
            BattleCameraTargetResolver.GetLookAtTarget(b);

        groupBinder.BindTwoTargets(
            aTarget,
            bTarget,
            1f,
            1f,
            1f,
            1f);

        PlaceGroupCameraSideView(
            aTarget,
            bTarget,
            7f,
            2.4f,
            1.4f,
            false);

        rig.SetLive(
            rig.GroupCamera);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] FocusBetween / " +
                $"A={a?.Data.CharacterName}, B={b?.Data.CharacterName}");
        }
    }

    public void Return()
    {
        ReturnInternal();
    }

    public void Return(
        SkillCameraDefinition definition)
    {
        ApplyReturnBrainBlend(
            definition);

        ReturnInternal();
    }

    public void Return(
        SkillCameraShot shot)
    {
        ApplyBrainBlend(
            shot);

        ReturnInternal();
    }

    public void Return(
        CinemachineBlendDefinition.Styles blendStyle,
        float blendTime)
    {
        ApplyBrainBlend(
            blendStyle,
            blendTime);

        ReturnInternal();
    }

    private void ReturnInternal()
    {
        if (rig == null)
            return;

        rig.SetLive(
            rig.OverviewCamera);

        if (logDebug)
        {
            Debug.Log(
                "[BattleCameraDirector] Return Overview");
        }
    }

    public IEnumerator WaitUntilArrived(
        float timeout)
    {
        if (rig == null ||
            rig.Brain == null)
        {
            if (timeout > 0f)
                yield return new WaitForSeconds(timeout);

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            if (rig.Brain.ActiveBlend == null)
                yield break;

            yield return null;
        }
    }

    public void PlayShake(
        BattleCameraShakeSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[BattleCameraDirector] Shake 실패 : settings가 null입니다.");
            return;
        }

        if (rig == null ||
            rig.ImpulseSource == null ||
            !settings.UseImpulse)
        {
            return;
        }

        rig.ImpulseSource.GenerateImpulseWithForce(
            settings.ImpulseForce);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] Cinemachine Impulse / " +
                $"Force={settings.ImpulseForce}");
        }
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
        if (request == null ||
            shot == null)
        {
            yield break;
        }

        if (rig == null)
            yield break;
            
        ApplyBrainBlend(shot);

        if (shot.UseSceneCameraPoint)
        {
            bool success =
                PlaySceneCameraPoint(
                    request,
                    shot);

            if (success)
            {
                yield return WaitAndShake(
                    shot);

                yield break;
            }
        }

        switch (shot.ShotType)
        {
            case SkillCameraShotType.FocusBetween:
            case SkillCameraShotType.ClashWide:
                PlayGroupShot(
                    request,
                    shot);
                break;

            case SkillCameraShotType.AttackerClose:
            case SkillCameraShotType.AttackerOverShoulder:
                PlayCharacterPoseShot(
                    request.Attacker,
                    request.Target,
                    shot);
                break;

            case SkillCameraShotType.TargetClose:
            case SkillCameraShotType.TargetOverShoulder:
            case SkillCameraShotType.HitImpact:
                PlayCharacterPoseShot(
                    request.Target,
                    request.Attacker,
                    shot);
                break;
            case SkillCameraShotType.ReturnOverview:
                Return(shot);
                break;
        }

        yield return WaitAndShake(
            shot);
    }

    private IEnumerator WaitAndShake(
        SkillCameraShot shot)
    {
        if (shot == null)
            yield break;

        if (shot.BlendWaitTime > 0f)
        {
            yield return WaitUntilArrived(
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

    private void PlayGroupShot(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (rig.GroupCamera == null ||
            groupBinder == null)
        {
            return;
        }

        Transform attackerTarget =
            BattleCameraTargetResolver.GetLookAtTarget(
                request.Attacker);

        Transform targetTarget =
            BattleCameraTargetResolver.GetLookAtTarget(
                request.Target);

        groupBinder.BindTwoTargets(
            attackerTarget,
            targetTarget,
            shot.AttackerWeight,
            shot.TargetWeight,
            shot.AttackerRadius,
            shot.TargetRadius);

        if (shot.UseSideViewPlacement)
        {
            PlaceGroupCameraSideView(
                attackerTarget,
                targetTarget,
                shot.SideDistance,
                shot.SideHeight,
                shot.LookAtHeight,
                shot.FlipSide);
        }

        rig.SetLive(
            rig.GroupCamera);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] Group Shot / " +
                $"Timing={shot.Timing}, " +
                $"Type={shot.ShotType}, " +
                $"Attacker={request.Attacker?.Data.CharacterName}, " +
                $"Target={request.Target?.Data.CharacterName}");
        }
    }

    private void PlayCharacterPoseShot(
        Character focusCharacter,
        Character lookCharacter,
        SkillCameraShot shot)
    {
        if (rig.PoseCamera == null ||
            focusCharacter == null)
        {
            return;
        }

        Transform focusTarget =
            BattleCameraTargetResolver.GetLookAtTarget(
                focusCharacter);

        Transform lookTarget =
            BattleCameraTargetResolver.GetLookAtTarget(
                lookCharacter);

        if (focusTarget == null)
            return;

        Vector3 lookAtPosition =
            focusTarget.position +
            focusTarget.TransformDirection(
                shot.LookAtOffset);

        Vector3 positionOffset =
            focusTarget.TransformDirection(
                shot.PositionOffset);

        if (positionOffset.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallbackDirection =
                lookTarget != null
                    ? (focusTarget.position - lookTarget.position).normalized
                    : -focusTarget.forward;

            positionOffset =
                fallbackDirection *
                Mathf.Max(1f, shot.FocusDistance);
        }

        Vector3 cameraPosition =
            lookAtPosition + positionOffset;

        Quaternion cameraRotation =
            Quaternion.LookRotation(
                lookAtPosition - cameraPosition,
                Vector3.up);

        rig.PoseCamera.transform.SetPositionAndRotation(
            cameraPosition,
            cameraRotation);

        rig.SetLive(
            rig.PoseCamera);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] Character Pose Shot / " +
                $"Character={focusCharacter.Data.CharacterName}, " +
                $"ShotType={shot.ShotType}");
        }
    }

    private bool PlaySceneCameraPoint(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (rig.PoseCamera == null)
            return false;

        Transform cameraPoint =
            BattleCameraTargetResolver.ResolveCameraPoint(
                request,
                shot.CameraPoint);

        Transform lookAtPoint =
            BattleCameraTargetResolver.ResolveCameraPoint(
                request,
                shot.LookAtPoint);

        if (cameraPoint == null)
        {
            Debug.LogWarning(
                $"[BattleCameraDirector] CameraPoint 찾기 실패 / " +
                $"Owner={shot.CameraPoint.Owner}, Key={shot.CameraPoint.Key}");

            return false;
        }

        Quaternion rotation;

        if (shot.RotationMode == SkillCameraRotationMode.CameraPointRotation ||
            lookAtPoint == null)
        {
            rotation =
                cameraPoint.rotation;
        }
        else
        {
            Vector3 lookDirection =
                lookAtPoint.position - cameraPoint.position;

            if (lookDirection.sqrMagnitude <= 0.0001f)
                lookDirection = cameraPoint.forward;

            rotation =
                Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
        }

        rig.PoseCamera.transform.SetPositionAndRotation(
            cameraPoint.position,
            rotation);

        rig.SetLive(
            rig.PoseCamera);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] Scene CameraPoint Shot / " +
                $"CameraPoint={cameraPoint.name}");
        }

        return true;
    }

    private void PlaceGroupCameraSideView(
        Transform attackerTarget,
        Transform targetTarget,
        float sideDistance,
        float height,
        float lookAtHeight,
        bool flipSide)
    {
        if (rig.GroupCamera == null)
            return;

        if (attackerTarget == null ||
            targetTarget == null)
        {
            return;
        }

        Vector3 attackerPosition =
            attackerTarget.position;

        Vector3 targetPosition =
            targetTarget.position;

        Vector3 center =
            (attackerPosition + targetPosition) * 0.5f;

        Vector3 line =
            targetPosition - attackerPosition;

        line.y = 0f;

        if (line.sqrMagnitude <= 0.0001f)
            line = Vector3.right;

        line.Normalize();

        Vector3 side =
            Vector3.Cross(
                Vector3.up,
                line).normalized;

        if (flipSide)
            side = -side;

        Vector3 cameraPosition =
            center +
            side * Mathf.Max(0.1f, sideDistance) +
            Vector3.up * height;

        Vector3 lookAtPosition =
            center +
            Vector3.up * lookAtHeight;

        Quaternion cameraRotation =
            Quaternion.LookRotation(
                lookAtPosition - cameraPosition,
                Vector3.up);

        rig.GroupCamera.transform.SetPositionAndRotation(
            cameraPosition,
            cameraRotation);
    }
    
    private void ApplyBrainBlend(
        SkillCameraShot shot)
    {
        if (shot == null)
            return;

        if (!shot.OverrideBrainBlend)
            return;

        ApplyBrainBlend(
            shot.BlendStyle,
            shot.BlendTime);
    }

    private void ApplyReturnBrainBlend(
        SkillCameraDefinition definition)
    {
        if (definition == null)
            return;

        if (!definition.OverrideReturnBrainBlend)
            return;

        ApplyBrainBlend(
            definition.ReturnBlendStyle,
            definition.ReturnBlendTime);
    }

    private void ApplyBrainBlend(
        CinemachineBlendDefinition.Styles blendStyle,
        float blendTime)
    {
        if (rig == null ||
            rig.Brain == null)
            return;

        rig.Brain.DefaultBlend =
            new CinemachineBlendDefinition(
                blendStyle,
                Mathf.Max(0f, blendTime));

        if (logDebug)
        {
            Debug.Log(
                $"[BattleCameraDirector] Brain Blend 변경 / " +
                $"Style={blendStyle}, Time={Mathf.Max(0f, blendTime)}");
        }
    }
}