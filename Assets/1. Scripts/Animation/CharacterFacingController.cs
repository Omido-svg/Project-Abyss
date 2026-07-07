using System.Collections;
using UnityEngine;

public class CharacterFacingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform rotateRoot;

    [Header("Turn")]
    [SerializeField] private float turnSpeed = 720f;

    [Tooltip("모델이 반대로 바라보면 180을 넣으세요.")]
    [SerializeField] private float yawOffset = 0f;

    [Header("Default")]
    [SerializeField] private bool returnToDefaultAfterAction = true;

    private Quaternion defaultLocalRotation;

    private void Awake()
    {
        if (rotateRoot == null)
            rotateRoot = transform;

        defaultLocalRotation = rotateRoot.localRotation;
    }

    public void FaceTargetInstant(Character target)
    {
        if (target == null)
            return;

        FacePositionInstant(target.transform.position);
    }

    public void FacePositionInstant(Vector3 worldPosition)
    {
        if (rotateRoot == null)
            return;

        Quaternion targetRotation =
            CalculateLookRotation(worldPosition);

        rotateRoot.rotation = targetRotation;
    }

    public IEnumerator FaceTargetSmooth(Character target)
    {
        if (target == null)
            yield break;

        yield return FacePositionSmooth(target.transform.position);
    }

    public IEnumerator FacePositionSmooth(Vector3 worldPosition)
    {
        if (rotateRoot == null)
            yield break;

        Quaternion targetRotation =
            CalculateLookRotation(worldPosition);

        yield return RotateToWorldRotation(targetRotation);
    }

    public IEnumerator ReturnToDefaultSmooth()
    {
        if (!returnToDefaultAfterAction)
            yield break;

        if (rotateRoot == null)
            yield break;

        yield return RotateToLocalRotation(defaultLocalRotation);
    }

    public void ReturnToDefaultInstant()
    {
        if (rotateRoot == null)
            return;

        rotateRoot.localRotation = defaultLocalRotation;
    }

    private Quaternion CalculateLookRotation(Vector3 worldPosition)
    {
        Vector3 direction =
            worldPosition - rotateRoot.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return rotateRoot.rotation;

        Quaternion lookRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        return lookRotation * Quaternion.Euler(0f, yawOffset, 0f);
    }

    private IEnumerator RotateToWorldRotation(Quaternion targetRotation)
    {
        while (Quaternion.Angle(rotateRoot.rotation, targetRotation) > 0.5f)
        {
            rotateRoot.rotation =
                Quaternion.RotateTowards(
                    rotateRoot.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);

            yield return null;
        }

        rotateRoot.rotation = targetRotation;
    }

    private IEnumerator RotateToLocalRotation(Quaternion targetLocalRotation)
    {
        while (Quaternion.Angle(rotateRoot.localRotation, targetLocalRotation) > 0.5f)
        {
            rotateRoot.localRotation =
                Quaternion.RotateTowards(
                    rotateRoot.localRotation,
                    targetLocalRotation,
                    turnSpeed * Time.deltaTime);

            yield return null;
        }

        rotateRoot.localRotation = targetLocalRotation;
    }
}