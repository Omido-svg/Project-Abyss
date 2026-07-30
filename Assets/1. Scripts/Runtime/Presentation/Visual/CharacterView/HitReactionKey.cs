/// <summary>
/// 공격자가 타깃에게 요청하는 피격 반응의 의미입니다.
/// 실제 AnimationClip은 타깃 자신의 CharacterPresentationProfile이 결정합니다.
/// </summary>
public enum HitReactionKey
{
    None = 0,
    LightHit = 1,
    HeavyHit = 2,
    Knockback = 3,
    Knockdown = 4,
    PartBreak = 5,
    Death = 6
}
