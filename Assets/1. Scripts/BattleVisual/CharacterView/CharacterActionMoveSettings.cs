using System;
using UnityEngine;

[Serializable]
public class CharacterActionMoveSettings
{
    [Header("Use")]
    public bool UseMove = true;

    [Header("Start Position")]
    public CharacterActionStartPositionMode StartPositionMode =
        CharacterActionStartPositionMode.NearTarget;

    public CharacterActionTargetPointType TargetPointType =
        CharacterActionTargetPointType.TargetBodyPart;

    [Header("Near Target")]
    public float ApproachDistance = 1.8f;

    [Tooltip("NearTarget 결과 위치에 더해지는 월드 오프셋")]
    public Vector3 WorldOffset;

    [Header("Default Local Offset")]
    [Tooltip("기본 위치 기준 로컬 오프셋")]
    public Vector3 DefaultLocalOffset;

    [Header("Target Relative Offset")]
    [Tooltip("타겟 기준 로컬 오프셋. x=좌우, y=높이, z=앞뒤")]
    public Vector3 TargetRelativeOffset;

    [Header("Speed")]
    public bool OverrideMoveSpeed;
    public float MoveSpeed = 8f;

    public bool OverrideArriveDistance;
    public float ArriveDistance = 0.03f;

    [Header("Return")]
    public bool ReturnAfterAction = true;
    public bool InstantReturn;
}