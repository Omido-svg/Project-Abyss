using UnityEngine;

public class CharacterCameraPointSet : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Transform lookAtPoint;

    [Header("Camera Positions")]
    [SerializeField] private Transform closeCameraPoint;
    [SerializeField] private Transform overShoulderCameraPoint;
    [SerializeField] private Transform hitImpactCameraPoint;
    [SerializeField] private Transform sideCameraPoint;

    public Transform GetPoint(
        SkillCameraPointType type)
    {
        switch (type)
        {
            case SkillCameraPointType.LookAt:
                return lookAtPoint;

            case SkillCameraPointType.Close:
                return closeCameraPoint;

            case SkillCameraPointType.OverShoulder:
                return overShoulderCameraPoint;

            case SkillCameraPointType.HitImpact:
                return hitImpactCameraPoint;

            case SkillCameraPointType.Side:
                return sideCameraPoint;
        }

        return null;
    }
}