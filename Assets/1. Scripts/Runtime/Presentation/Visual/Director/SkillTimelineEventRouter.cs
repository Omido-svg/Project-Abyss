using System;
using UnityEngine;

/// <summary>
/// Skill Timeline Event Track을 presentation 동작으로 라우팅한다.
///
/// SkillCutsceneDirector는 Timeline 자체를 재생하고, 이 객체는 Hit/VFX/Camera/Reaction
/// 이벤트를 어떤 presenter에 전달할지만 결정한다. 전투 판정은 수행하지 않는다.
/// </summary>
internal sealed class SkillTimelineEventRouter
{
    private readonly BattleHitPresenter hitPresenter;
    private readonly BattleCameraDirector cameraDirector;
    private readonly bool logDebug;
    private readonly UnityEngine.Object logContext;

    public SkillTimelineEventRouter(
        BattleHitPresenter hitPresenter,
        BattleCameraDirector cameraDirector,
        bool logDebug,
        UnityEngine.Object logContext)
    {
        this.hitPresenter = hitPresenter;
        this.cameraDirector = cameraDirector;
        this.logDebug = logDebug;
        this.logContext = logContext;
    }

    /// <summary>
    /// 한 Timeline 재생 세션 전용 callback을 만든다.
    /// 자동 HitIndex 카운터는 callback closure 안에만 존재해서 다음 Timeline으로 새지 않는다.
    /// </summary>
    public Action<SkillCutsceneEventClip> CreateHandler(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        CharacterView primaryTargetView,
        int exchangeIndex,
        bool isClashAttack,
        bool isOneSided)
    {
        int hitFrameCount = 0;

        return eventClip =>
        {
            if (eventClip == null)
                return;

            switch (eventClip.EventType)
            {
                case SkillCutsceneEventType.Hit:
                {
                    int hitIndex =
                        eventClip.HitIndex >= 0
                            ? eventClip.HitIndex
                            : hitFrameCount;

                    hitFrameCount =
                        Mathf.Max(
                            hitFrameCount,
                            hitIndex + 1);

                    hitPresenter?.ApplyHitFrame(
                        playback,
                        visual,
                        hitIndex,
                        exchangeIndex: exchangeIndex,
                        isClash: isClashAttack,
                        isOneSided: isOneSided);

                    break;
                }

                case SkillCutsceneEventType.Vfx:
                    if (visual != null &&
                        !visual.UseExplicitVisualFxTracks)
                    {
                        hitPresenter?.PlaySkillVfx(
                            playback,
                            request,
                            visual,
                            eventClip.VfxTiming,
                            eventClip.HitIndex);
                    }
                    break;

                case SkillCutsceneEventType.CameraShake:
                    PlayHitCameraShake(
                        visual);
                    break;

                case SkillCutsceneEventType.CameraImpactPulse:
                {
                    int pulseHitIndex =
                        eventClip.HitIndex >= 0
                            ? eventClip.HitIndex
                            : Mathf.Max(
                                0,
                                hitFrameCount - 1);

                    int pulseDamage =
                        hitPresenter != null
                            ? hitPresenter.GetDamageForHitIndex(
                                playback,
                                pulseHitIndex)
                            : 0;

                    TriggerCameraImpactPulse(
                        playback,
                        request,
                        visual,
                        eventClip.CameraImpactTiming,
                        pulseHitIndex,
                        exchangeIndex,
                        pulseDamage,
                        request?.WasCritical == true &&
                        pulseHitIndex == 0,
                        request?.BrokePart == true,
                        request?.WasKilled == true,
                        isClashAttack,
                        isOneSided);

                    break;
                }

                case SkillCutsceneEventType.TargetHitReaction:
                    if (primaryTargetView != null &&
                        visual != null)
                    {
                        if (playback != null)
                        {
                            playback.ActiveReactionView =
                                primaryTargetView;
                        }

                        primaryTargetView.PlayReaction(
                            visual.TargetReaction);
                    }
                    break;

                case SkillCutsceneEventType.Custom:
                    if (logDebug)
                    {
                        Debug.Log(
                            "[SkillTimelineEventRouter] " +
                            "Timeline Custom Event / " +
                            $"Key={eventClip.CustomEventKey}",
                            logContext);
                    }
                    break;
            }
        };
    }

    private void TriggerCameraImpactPulse(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraImpactTiming timing,
        int hitIndex = -1,
        int exchangeIndex = -1,
        int damage = 0,
        bool isCritical = false,
        bool brokePart = false,
        bool wasKilled = false,
        bool isClash = false,
        bool isOneSided = false)
    {
        if (request == null ||
            visual == null ||
            cameraDirector == null)
        {
            return;
        }

        SkillCameraImpactPulse pulse =
            visual.FindImpactPulse(
                timing,
                hitIndex,
                exchangeIndex,
                damage,
                isCritical,
                brokePart,
                wasKilled,
                isClash,
                isOneSided);

        if (pulse == null)
            return;

        Coroutine routine =
            cameraDirector.StartImpactPulse(
                pulse);

        if (routine != null &&
            playback != null)
        {
            playback.HasCameraActivity = true;
        }
    }

    private void PlayHitCameraShake(
        SkillVisualDefinition visual)
    {
        if (visual == null ||
            !visual.UseHitCameraShake ||
            cameraDirector == null)
        {
            return;
        }

        cameraDirector.PlayShake(
            visual.HitShake);
    }
}
