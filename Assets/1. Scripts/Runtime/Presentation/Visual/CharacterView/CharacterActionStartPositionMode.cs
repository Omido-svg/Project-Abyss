public enum CharacterActionStartPositionMode
{
    None,

    // 현재 위치에서 바로 애니메이션 재생
    CurrentPosition,

    // 기본 위치 + Local Offset
    DefaultLocalOffset,

    // 타겟 기준으로 접근
    NearTarget,

    // 타겟 Transform 기준 Offset
    TargetRelativeOffset
}