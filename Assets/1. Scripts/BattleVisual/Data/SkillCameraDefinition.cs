using System.Collections.Generic;
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
}