using UnityEngine;

/// <summary>
/// 캐릭터가 자기 몸으로 표현해야 하는 합 모션과 피격 반응의 단일 원본입니다.
///
/// 스킬 Timeline은 공격자 연출만 소유하고, 합 시스템과 피격 시스템은
/// 의미 키만 요청합니다. 따라서 올라프 스킬이 특정 적의 AnimationClip을
/// 직접 참조하지 않습니다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Visual/Character Presentation Profile",
    fileName = "NewCharacterPresentationProfile")]
public sealed class CharacterPresentationProfile : ScriptableObject
{
    [Header("Clash Motions")]
    [Tooltip("전체 합이 시작되어 합 위치로 이동할 때 사용하는 모션입니다.")]
    public AnimationClip ClashEnter;

    [Tooltip("각 교환의 판정 전, 서로 맞대고 대치하는 중립 모션입니다.")]
    public AnimationClip ClashContest;

    [Tooltip("교환에서 이겨 실제 스킬 공격권을 얻는 짧은 전환 모션입니다.")]
    public AnimationClip ClashAdvantage;

    [Tooltip("교환에서 져 자세가 무너지는 짧은 전환 모션입니다.")]
    public AnimationClip ClashDisadvantage;

    [Tooltip("교환이 동률일 때 양쪽이 튕겨 나가는 중립 모션입니다.")]
    public AnimationClip ClashTie;

    [Tooltip("다음 교환을 위해 다시 합 위치와 자세를 잡는 모션입니다.")]
    public AnimationClip ClashReengage;

    [Tooltip("전체 합이 끝나 전투 위치로 복귀하기 직전의 마무리 모션입니다.")]
    public AnimationClip ClashExit;

    [Header("Target Reactions")]
    public AnimationClip LightHit;
    public AnimationClip HeavyHit;
    public AnimationClip Knockback;
    public AnimationClip Knockdown;
    public AnimationClip PartBreak;
    public AnimationClip Death;

    public AnimationClip ResolveClashMotion(
        ClashMotionKey key)
    {
        return key switch
        {
            ClashMotionKey.Enter =>
                ClashEnter,

            ClashMotionKey.Contest =>
                ClashContest,

            ClashMotionKey.Advantage =>
                ClashAdvantage ?? ClashContest,

            ClashMotionKey.Disadvantage =>
                ClashDisadvantage ?? HeavyHit ?? LightHit,

            ClashMotionKey.Tie =>
                ClashTie ?? ClashDisadvantage ?? HeavyHit ?? LightHit,

            ClashMotionKey.Reengage =>
                ClashReengage ?? ClashEnter,

            ClashMotionKey.Exit =>
                ClashExit ?? ClashReengage,

            _ =>
                null
        };
    }

    public AnimationClip ResolveReaction(
        HitReactionKey key)
    {
        return key switch
        {
            HitReactionKey.None =>
                null,

            HitReactionKey.LightHit =>
                LightHit ?? HeavyHit,

            HitReactionKey.HeavyHit =>
                HeavyHit ?? LightHit,

            HitReactionKey.Knockback =>
                Knockback ?? HeavyHit ?? LightHit,

            HitReactionKey.Knockdown =>
                Knockdown ?? Knockback ?? HeavyHit ?? LightHit,

            HitReactionKey.PartBreak =>
                PartBreak ?? Knockdown ?? HeavyHit ?? LightHit,

            HitReactionKey.Death =>
                Death ?? Knockdown ?? HeavyHit ?? LightHit,

            _ =>
                HeavyHit ?? LightHit
        };
    }
}
