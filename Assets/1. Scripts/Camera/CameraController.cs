using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Focus")]
    [SerializeField] private float focusDistance = 5f;

    private Vector3 defaultPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        defaultPosition = transform.position;
        targetPosition = defaultPosition;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);
    }

    //------------------------------------------

    /// <summary>
    /// 월드 좌표를 화면 중앙으로 보도록 카메라 이동
    /// </summary>
    public void Focus(Vector3 worldPosition)
    {
        targetPosition =
            worldPosition -
            transform.forward * focusDistance;
    }

    //------------------------------------------

    /// <summary>
    /// 기본 위치로 복귀
    /// </summary>
    public void Return()
    {
        targetPosition = defaultPosition;
    }

    //------------------------------------------

    /// <summary>
    /// 기본 카메라 위치 변경
    /// </summary>
    public void SetDefaultPosition(Vector3 position)
    {
        defaultPosition = position;
        targetPosition = position;
    }

    //------------------------------------------

    public bool IsMoving =>
        Vector3.Distance(transform.position, targetPosition) > 0.01f;
}