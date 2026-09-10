using UnityEngine;

/// <summary>
/// Planning Overview 카메라가 이동할 수 있는 로컬 Box 영역.
/// Transform을 회전시키면 회전된 Bounding Box도 사용할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlanningCameraBounds : MonoBehaviour
{
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new(15f, 7f, 10f);

    public Vector3 Center
    {
        get => center;
        set => center = value;
    }

    public Vector3 Size
    {
        get => size;
        set => size = SanitizeSize(value);
    }

    public Vector3 ClampWorldPoint(Vector3 worldPoint)
    {
        Vector3 localPoint =
            transform.InverseTransformPoint(worldPoint);

        Vector3 safeSize = SanitizeSize(size);
        Vector3 half = safeSize * 0.5f;
        Vector3 relative = localPoint - center;

        relative.x = Mathf.Clamp(relative.x, -half.x, half.x);
        relative.y = Mathf.Clamp(relative.y, -half.y, half.y);
        relative.z = Mathf.Clamp(relative.z, -half.z, half.z);

        return transform.TransformPoint(center + relative);
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        Vector3 localPoint =
            transform.InverseTransformPoint(worldPoint) - center;

        Vector3 half = SanitizeSize(size) * 0.5f;

        return Mathf.Abs(localPoint.x) <= half.x &&
               Mathf.Abs(localPoint.y) <= half.y &&
               Mathf.Abs(localPoint.z) <= half.z;
    }

    private void OnValidate()
    {
        size = SanitizeSize(size);
    }

    private static Vector3 SanitizeSize(Vector3 value)
    {
        return new Vector3(
            Mathf.Max(0.1f, Mathf.Abs(value.x)),
            Mathf.Max(0.1f, Mathf.Abs(value.y)),
            Mathf.Max(0.1f, Mathf.Abs(value.z)));
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(center, SanitizeSize(size));
        Gizmos.matrix = oldMatrix;
    }
}
