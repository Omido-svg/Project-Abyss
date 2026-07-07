using System.Collections;
using UnityEngine;

public class CharacterActionMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;

    [Header("Move")]
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

    public IEnumerator MoveNearTarget(
        Character target,
        BodyPart targetPart = null)
    {
        if (visualRoot == null)
            yield break;

        if (target == null)
            yield break;

        Vector3 targetPoint =
            GetTargetPoint(target, targetPart);

        Vector3 directionFromTargetToSelf =
            visualRoot.position - targetPoint;

        directionFromTargetToSelf.y = 0f;

        if (directionFromTargetToSelf.sqrMagnitude <= 0.0001f)
            directionFromTargetToSelf = -target.transform.forward;

        Vector3 destination =
            targetPoint +
            directionFromTargetToSelf.normalized * approachDistance;

        destination.y = visualRoot.position.y;

        yield return MoveWorldPosition(destination);
    }

    public IEnumerator ReturnToDefaultPosition()
    {
        if (visualRoot == null)
            yield break;

        while (Vector3.Distance(
                   visualRoot.localPosition,
                   defaultLocalPosition) > arriveDistance)
        {
            visualRoot.localPosition =
                Vector3.MoveTowards(
                    visualRoot.localPosition,
                    defaultLocalPosition,
                    moveSpeed * Time.deltaTime);

            yield return null;
        }

        visualRoot.localPosition = defaultLocalPosition;
    }

    public void ReturnToDefaultPositionInstant()
    {
        if (visualRoot == null)
            return;

        visualRoot.localPosition = defaultLocalPosition;
    }

    private IEnumerator MoveWorldPosition(Vector3 destination)
    {
        while (Vector3.Distance(
                   visualRoot.position,
                   destination) > arriveDistance)
        {
            visualRoot.position =
                Vector3.MoveTowards(
                    visualRoot.position,
                    destination,
                    moveSpeed * Time.deltaTime);

            yield return null;
        }

        visualRoot.position = destination;
    }

    private Vector3 GetTargetPoint(
        Character target,
        BodyPart targetPart)
    {
        CharacterView targetView =
            target.GetComponent<CharacterView>();

        if (targetView != null && targetPart != null)
            return targetView.GetDamageNumberPosition(targetPart);

        if (targetView != null)
            return targetView.LookAtPoint.position;

        return target.transform.position + Vector3.up * 1.5f;
    }
}