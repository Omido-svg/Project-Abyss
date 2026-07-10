using System.Collections;
using UnityEngine;

public class CharacterActionMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;

    [Header("Default Move")]
    [SerializeField] private float approachDistance = 1.8f;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float arriveDistance = 0.03f;

    private Vector3 defaultLocalPosition;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        defaultLocalPosition = visualRoot.localPosition;
    }

    public IEnumerator MoveToActionStartPosition(
        Character attacker,
        Character target,
        BodyPart targetPart,
        CharacterActionMoveSettings settings)
    {
        if (visualRoot == null)
            yield break;

        if (settings == null)
        {
            yield return MoveNearTarget(
                target,
                targetPart);

            yield break;
        }

        if (!settings.UseMove)
            yield break;

        switch (settings.StartPositionMode)
        {
            case CharacterActionStartPositionMode.None:
                yield break;

            case CharacterActionStartPositionMode.CurrentPosition:
                yield break;

            case CharacterActionStartPositionMode.DefaultLocalOffset:
                yield return MoveToDefaultLocalOffset(settings);
                yield break;

            case CharacterActionStartPositionMode.NearTarget:
                yield return MoveNearTarget(
                    target,
                    targetPart,
                    settings);
                yield break;

            case CharacterActionStartPositionMode.TargetRelativeOffset:
                yield return MoveToTargetRelativeOffset(
                    target,
                    targetPart,
                    settings);
                yield break;
        }
    }

    public IEnumerator MoveNearTarget(
        Character target,
        BodyPart targetPart = null)
    {
        CharacterActionMoveSettings tempSettings =
            new CharacterActionMoveSettings
            {
                UseMove = true,
                StartPositionMode = CharacterActionStartPositionMode.NearTarget,
                TargetPointType = CharacterActionTargetPointType.TargetBodyPart,
                ApproachDistance = approachDistance,
                OverrideMoveSpeed = false,
                OverrideArriveDistance = false,
                ReturnAfterAction = true
            };

        yield return MoveNearTarget(
            target,
            targetPart,
            tempSettings);
    }

    private IEnumerator MoveNearTarget(
        Character target,
        BodyPart targetPart,
        CharacterActionMoveSettings settings)
    {
        if (target == null)
            yield break;

        Vector3 targetPoint =
            GetTargetPoint(
                target,
                targetPart,
                settings.TargetPointType);

        Vector3 directionFromTargetToSelf =
            visualRoot.position - targetPoint;

        directionFromTargetToSelf.y = 0f;

        if (directionFromTargetToSelf.sqrMagnitude <= 0.0001f)
        {
            directionFromTargetToSelf =
                transform.position - target.transform.position;

            directionFromTargetToSelf.y = 0f;
        }

        if (directionFromTargetToSelf.sqrMagnitude <= 0.0001f)
            directionFromTargetToSelf = -target.transform.forward;

        float distance =
            settings.ApproachDistance > 0f
                ? settings.ApproachDistance
                : approachDistance;

        Vector3 destination =
            targetPoint +
            directionFromTargetToSelf.normalized * distance +
            settings.WorldOffset;

        destination.y =
            visualRoot.position.y + settings.WorldOffset.y;

        yield return MoveWorldPosition(
            destination,
            GetMoveSpeed(settings),
            GetArriveDistance(settings));
    }

    private IEnumerator MoveToDefaultLocalOffset(
        CharacterActionMoveSettings settings)
    {
        Vector3 destinationLocal =
            defaultLocalPosition +
            settings.DefaultLocalOffset;

        yield return MoveLocalPosition(
            destinationLocal,
            GetMoveSpeed(settings),
            GetArriveDistance(settings));
    }

    private IEnumerator MoveToTargetRelativeOffset(
        Character target,
        BodyPart targetPart,
        CharacterActionMoveSettings settings)
    {
        if (target == null)
            yield break;

        Vector3 targetPoint =
            GetTargetPoint(
                target,
                targetPart,
                settings.TargetPointType);

        Transform targetTransform =
            target.transform;

        CharacterView targetView =
            target.GetComponentInChildren<CharacterView>(true);

        if (targetView != null && targetView.LookAtPoint != null)
            targetTransform = targetView.LookAtPoint;

        Vector3 offset =
            targetTransform.right * settings.TargetRelativeOffset.x +
            Vector3.up * settings.TargetRelativeOffset.y +
            targetTransform.forward * settings.TargetRelativeOffset.z;

        Vector3 destination =
            targetPoint + offset;

        yield return MoveWorldPosition(
            destination,
            GetMoveSpeed(settings),
            GetArriveDistance(settings));
    }

    public IEnumerator ReturnToDefaultPosition()
    {
        if (visualRoot == null)
            yield break;

        yield return MoveLocalPosition(
            defaultLocalPosition,
            moveSpeed,
            arriveDistance);
    }

    public IEnumerator ReturnToDefaultPosition(
        CharacterActionMoveSettings settings)
    {
        if (visualRoot == null)
            yield break;

        if (settings == null)
        {
            yield return ReturnToDefaultPosition();
            yield break;
        }

        if (!settings.ReturnAfterAction)
            yield break;

        if (settings.InstantReturn)
        {
            ReturnToDefaultPositionInstant();
            yield break;
        }

        yield return MoveLocalPosition(
            defaultLocalPosition,
            GetMoveSpeed(settings),
            GetArriveDistance(settings));
    }

    public void ReturnToDefaultPositionInstant()
    {
        if (visualRoot == null)
            return;

        visualRoot.localPosition =
            defaultLocalPosition;
    }

    private IEnumerator MoveWorldPosition(
        Vector3 destination,
        float speed,
        float arrive)
    {
        while (Vector3.Distance(
                   visualRoot.position,
                   destination) > arrive)
        {
            visualRoot.position =
                Vector3.MoveTowards(
                    visualRoot.position,
                    destination,
                    speed * Time.deltaTime);

            yield return null;
        }

        visualRoot.position =
            destination;
    }

    private IEnumerator MoveLocalPosition(
        Vector3 destinationLocal,
        float speed,
        float arrive)
    {
        while (Vector3.Distance(
                   visualRoot.localPosition,
                   destinationLocal) > arrive)
        {
            visualRoot.localPosition =
                Vector3.MoveTowards(
                    visualRoot.localPosition,
                    destinationLocal,
                    speed * Time.deltaTime);

            yield return null;
        }

        visualRoot.localPosition =
            destinationLocal;
    }

    private Vector3 GetTargetPoint(
        Character target,
        BodyPart targetPart,
        CharacterActionTargetPointType targetPointType)
    {
        if (target == null)
            return Vector3.zero;

        CharacterView targetView =
            target.GetComponentInChildren<CharacterView>(true);

        switch (targetPointType)
        {
            case CharacterActionTargetPointType.TargetBodyPart:
                if (targetView != null && targetPart != null)
                    return targetView.GetDamageNumberPosition(targetPart);

                if (targetView != null && targetView.LookAtPoint != null)
                    return targetView.LookAtPoint.position;

                return target.transform.position + Vector3.up * 1.5f;

            case CharacterActionTargetPointType.TargetLookAtPoint:
                if (targetView != null && targetView.LookAtPoint != null)
                    return targetView.LookAtPoint.position;

                return target.transform.position + Vector3.up * 1.5f;

            case CharacterActionTargetPointType.TargetRoot:
                return target.transform.position;
        }

        return target.transform.position;
    }

    private float GetMoveSpeed(
        CharacterActionMoveSettings settings)
    {
        if (settings != null &&
            settings.OverrideMoveSpeed &&
            settings.MoveSpeed > 0f)
        {
            return settings.MoveSpeed;
        }

        return moveSpeed;
    }

    private float GetArriveDistance(
        CharacterActionMoveSettings settings)
    {
        if (settings != null &&
            settings.OverrideArriveDistance &&
            settings.ArriveDistance > 0f)
        {
            return settings.ArriveDistance;
        }

        return arriveDistance;
    }
}