using System;
using System.Collections;
using UnityEngine;

public partial class BattleAnimationDirector : MonoBehaviour
{
    private static void CleanupActionView(
        CharacterView view,
        bool abort)
    {
        if (view == null)
            return;

        if (abort)
            view.AbortActionPlayback();
        else
            view.CancelActionPlayback();
    }

    private static void CleanupActionViewUnlessPreserved(
        CharacterView view,
        CharacterView preservedReactionView,
        bool abort)
    {
        if (!abort &&
            ReferenceEquals(
                view,
                preservedReactionView))
        {
            return;
        }

        CleanupActionView(
            view,
            abort);
    }

    private void EndVisualRequest(
        BattleVisualPlaybackState playback)
    {
        if (playback == null ||
            playback.IsCleanedUp)
        {
            return;
        }

        playback.IsCleanedUp = true;

        for (BattleVisualCleanupPhase phase = BattleVisualCleanupPhase.CameraRoutine;
             phase <= BattleVisualCleanupPhase.Momentum;
             phase++)
        {
            RunCleanupPhase(
                playback,
                phase);
        }
    }

    private void RunCleanupPhase(
        BattleVisualPlaybackState playback,
        BattleVisualCleanupPhase phase)
    {
        try
        {
            switch (phase)
            {
                case BattleVisualCleanupPhase.CameraRoutine:
                    cameraDirector?.CancelImpactPulse(
                        restoreLens: true);
                    break;

                case BattleVisualCleanupPhase.AttackerAnimation:
                {
                    CharacterView preservedReactionView =
                        playback.IsCancellationRequested
                            ? null
                            : playback.ActiveReactionView;

                    CleanupActionViewUnlessPreserved(
                        playback.ActiveActionView,
                        preservedReactionView,
                        playback.IsCancellationRequested);

                    if (!ReferenceEquals(
                            playback.AttackerView,
                            playback.ActiveActionView))
                    {
                        CleanupActionViewUnlessPreserved(
                            playback.AttackerView,
                            preservedReactionView,
                            playback.IsCancellationRequested);
                    }

                    if (!ReferenceEquals(
                            playback.TargetView,
                            playback.ActiveActionView) &&
                        !ReferenceEquals(
                            playback.TargetView,
                            playback.AttackerView))
                    {
                        CleanupActionViewUnlessPreserved(
                            playback.TargetView,
                            preservedReactionView,
                            playback.IsCancellationRequested);
                    }

                    // Attack Weight로 여러 대상이 동시에 반응할 수 있다.
                    // 정상 종료에서는 마지막 피격 반응을 자르지 않고 각 View가 스스로 마치게 두며,
                    // 취소 경로에서만 모든 secondary reaction을 즉시 중단한다.
                    if (playback.IsCancellationRequested)
                    {
                        foreach (CharacterView reactionView
                                 in playback.ReactionViews)
                        {
                            if (reactionView != null)
                                reactionView.AbortActionPlayback();
                        }
                    }

                    playback.ReactionViews.Clear();
                    break;
                }

                case BattleVisualCleanupPhase.TargetVisualState:
                    if (playback.TargetView != null)
                        playback.TargetView.RefreshVisualState();
                    break;

                case BattleVisualCleanupPhase.Vfx:
                    CleanupTrackedVfx(playback);
                    break;

                case BattleVisualCleanupPhase.Position:
                {
                    bool shouldRestore =
                        playback.ShouldRestoreAttackerPosition;

                    playback.ShouldRestoreAttackerPosition = false;

                    if (shouldRestore &&
                        playback.AttackerMover != null)
                    {
                        playback.AttackerMover
                            .ReturnToDefaultPositionInstant();
                    }

                    foreach (CharacterActionMover mover
                             in playback
                                 .StagedMoverSettings
                                 .Keys)
                    {
                        if (mover != null)
                        {
                            mover
                                .ReturnToDefaultPositionInstant();
                        }
                    }

                    playback.ClearStagedMovers();
                    break;
                }

                case BattleVisualCleanupPhase.Facing:
                {
                    bool shouldRestore =
                        playback.ShouldRestoreFacing;

                    playback.ShouldRestoreFacing = false;

                    if (!shouldRestore)
                        break;

                    if (playback.AttackerFacing != null)
                        playback.AttackerFacing.ReturnToDefaultInstant();

                    if (playback.TargetFacing != null)
                        playback.TargetFacing.ReturnToDefaultInstant();
                    break;
                }

                case BattleVisualCleanupPhase.Camera:
                {
                    bool hadCameraActivity =
                        playback.HasCameraActivity;

                    playback.HasCameraActivity = false;

                    if (hadCameraActivity && cameraDirector != null)
                        cameraDirector.Return();
                    break;
                }

                case BattleVisualCleanupPhase.Announcement:
                {
                    bool wasVisible =
                        playback.IsAnnouncementVisible;

                    playback.IsAnnouncementVisible = false;

                    if (wasVisible && actionAnnounceUI != null)
                        actionAnnounceUI.HideImmediate();

                    // UnityEngine.Object에 ?.를 사용하면 파괴된 객체도 CLR null이 아니어서
                    // MissingReferenceException이 발생할 수 있다. Unity의 오버로드된 null 검사를 사용한다.
                    if (clashRollPresentationUI != null)
                    {
                        clashRollPresentationUI.HideImmediate();
                    }
                    else
                    {
                        clashRollPresentationUI = null;
                    }

                    break;
                }

                case BattleVisualCleanupPhase.FloatingText:
                {
                    bool hadFloatingText =
                        playback.HasFloatingTextActivity;

                    playback.HasFloatingTextActivity = false;

                    if (hadFloatingText && floatingTextManager != null)
                        floatingTextManager.CancelAllActiveTexts();
                    break;
                }

                case BattleVisualCleanupPhase.DamagePresentation:
                    if (playback.HasBegun)
                        damagePresenter?.Clear(playback);
                    break;

                case BattleVisualCleanupPhase.TargetArrow:
                {
                    bool wasBound =
                        playback.IsTargetArrowBound;

                    playback.IsTargetArrowBound = false;

                    if (wasBound && targetArrowUI != null)
                    {
                        targetArrowUI.ClearCurrentVisualRequest(
                            playback.RootRequest);
                    }
                    break;
                }

                case BattleVisualCleanupPhase.Momentum:
                {
                    bool wasLocked =
                        playback.IsMomentumDisplayLocked;

                    playback.IsMomentumDisplayLocked = false;

                    if (wasLocked && momentumScrollbarUI != null)
                        momentumScrollbarUI.ForceRefresh();
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[BattleAnimationDirector] Cleanup 실패 / Phase={phase}",
                this);

            Debug.LogException(
                exception,
                this);
        }
    }

    private static void CleanupTrackedVfx(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        try
        {
            if (!playback.IsCancellationRequested)
                return;

            foreach (BattleVfxInstance instance
                     in playback.SpawnedVfxInstances)
            {
                instance?.Release();
            }
        }
        finally
        {
            playback.SpawnedVfxInstances.Clear();
            playback.PlayedVfxCueKeys.Clear();
        }
    }

    private enum BattleVisualCleanupPhase
    {
        CameraRoutine,
        AttackerAnimation,
        TargetVisualState,
        Vfx,
        Position,
        Facing,
        Camera,
        Announcement,
        FloatingText,
        DamagePresentation,
        TargetArrow,
        Momentum
    }

}