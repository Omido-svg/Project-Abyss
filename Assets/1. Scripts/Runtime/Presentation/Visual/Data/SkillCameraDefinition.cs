using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SkillCameraDefinition",
    menuName = "Battle/Visual/Skill Camera Definition")]
public class SkillCameraDefinition : ScriptableObject
{
    [Header("Shots")]
    public List<SkillCameraShot> Shots = new();

    [Header("Return")]
    public bool ReturnToOverviewAfterAction = true;

    [Header("Return Blend")]
    public bool OverrideReturnBrainBlend = true;

    public CinemachineBlendDefinition.Styles ReturnBlendStyle =
        CinemachineBlendDefinition.Styles.EaseOut;

    public float ReturnBlendTime = 0.25f;
}