using UnityEngine;

/// <summary>
/// 런타임 Follow/CombatFrame 위치를 담당하는 카메라 바인딩 루트입니다.
/// 자식 CinemachineCamera의 로컬 애니메이션과 부모 좌표계 이동을 분리합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillCutsceneCameraBindingRoot :
    MonoBehaviour
{
}
