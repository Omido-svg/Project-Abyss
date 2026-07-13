using System;
using System.Collections;
using UnityEngine;

public class BattleAnimationDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private BattleWorldFloatingTextManager floatingTextManager;
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField] private SkillVisualProfile defaultVisualProfile;
    [SerializeField] private TargetArrowUI targetArrowUI;
    [SerializeField] private MomentumScrollbarUI momentumScrollbarUI;
    [SerializeField] private BattleVfxManager vfxManager;

    [Header("Fallback")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private BattleActionAnnounceUI actionAnnounceUI;

    private bool isPlaying;
    private Coroutine cameraShotRoutine;
    private BattleVisualRequestBuilder requestBuilder;
    private BattleVisualDamagePresenter damagePresenter;
    private BattleVisualPlaybackState activePlayback;

    private void Awake()
    {
        ResolveReferences();
        requestBuilder =
            new BattleVisualRequestBuilder(
                defaultVisualProfile);

        damagePresenter =
            new BattleVisualDamagePresenter(
                battleUIManager,
                logDebug);
    }

    private void OnDisable()
    {
        CancelActivePlayback();
    }

    private void OnDestroy()
    {
        CancelActivePlayback();
    }

    private void ResolveReferences()
    {
        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<BattleCameraDirector>();

        if (floatingTextManager == null)
            floatingTextManager = FindFirstObjectByType<BattleWorldFloatingTextManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();

        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();

        if (actionAnnounceUI == null)
            actionAnnounceUI = FindFirstObjectByType<BattleActionAnnounceUI>();

        if (targetArrowUI == null)
            targetArrowUI = FindFirstObjectByType<TargetArrowUI>();
            
        if (momentumScrollbarUI == null)
            momentumScrollbarUI = FindFirstObjectByType<MomentumScrollbarUI>();
            
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();
    }

    public IEnumerator Play(BattleVisualRequest request)
    {
        if (request == null)
            yield break;

        if (isPlaying)
        {
            Debug.LogWarning("[BattleAnimationDirector] 이미 전투 연출 중입니다.");
            yield break;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                "[BattleAnimationDirector] 비활성 상태에서는 전투 연출을 시작할 수 없습니다.");
            yield break;
        }

        isPlaying = true;
        BattleVisualPlaybackState playback =
            new BattleVisualPlaybackState(request);

        activePlayback = playback;

        Coroutine playbackRoutine =
            StartCoroutine(
                RunPlayback(playback));

        if (playbackRoutine == null)
        {
            playback.IsCancellationRequested = true;
            CompletePlayback(playback);
            yield break;
        }

        try
        {
            while (!playback.IsCompleted)
                yield return null;
        }
        finally
        {
            if (!playback.IsCompleted &&
                ReferenceEquals(activePlayback, playback))
            {
                CancelActivePlayback();
            }
        }
    }

    public void CancelActivePlayback()
    {
        BattleVisualPlaybackState playback =
            activePlayback;

        if (playback == null)
            return;

        playback.IsCancellationRequested = true;

        StopAllCoroutines();
        cameraShotRoutine = null;

        CompletePlayback(playback);
    }

    private IEnumerator RunPlayback(
        BattleVisualPlaybackState playback)
    {
        bool completedNormally = false;

        try
        {
            yield return PlayInternal(playback);
            completedNormally = true;
        }
        finally
        {
            if (!completedNormally)
                playback.IsCancellationRequested = true;

            CompletePlayback(playback);
        }
    }

    private void CompletePlayback(
        BattleVisualPlaybackState playback)
    {
        if (playback == null ||
            playback.IsCompleted)
        {
            return;
        }

        try
        {
            EndVisualRequest(playback);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception,
                this);
        }
        finally
        {
            playback.IsCompleted = true;

            if (ReferenceEquals(activePlayback, playback))
            {
                activePlayback = null;
                isPlaying = false;
            }
        }
    }

    public IEnumerator PlayAction(BattleAction action)
    {
        if (action == null)
            yield break;

        requestBuilder ??=
            new BattleVisualRequestBuilder(
                defaultVisualProfile);

        BattleVisualRequest request =
            requestBuilder.Build(
                action,
                clashSteps: null,
                hitDamages: null);

        yield return Play(request);
    }

    private IEnumerator PlayInternal(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest request =
            playback.Request;

        Character attacker =
            request.Attacker;

        Character target =
            request.Target;

        if (attacker == null)
            yield break;

        SkillVisualDefinition visual =
            request.VisualDefinition;

        if (visual == null)
        {
            Debug.LogWarning("[BattleAnimationDirector] VisualDefinition 없음");
            yield break;
        }

        BeginVisualRequest(
            playback,
            visual);

        CharacterViewSet views =
            GetViews(
                attacker,
                target);

        playback.AttackerView = views.AttackerView;
        playback.TargetView = views.TargetView;
        playback.AttackerMover = views.AttackerMover;
        playback.AttackerFacing = views.AttackerFacing;
        playback.TargetFacing = views.TargetFacing;
        
        PlaySkillVfx(
            playback,
            request,
            visual,
            BattleVfxTiming.OnActionStart);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleAnimationDirector] PlayInternal 시작 / " +
                $"Attacker={attacker?.Data.CharacterName}, " +
                $"Target={target?.Data.CharacterName}, " +
                $"ActionType={request.ActionType}, " +
                $"HasHitFrameDamage={visual.HasHitFrameDamage}, " +
                $"ApplyDamageIfNoHitFrame={visual.ApplyDamageIfNoHitFrame}, " +
                $"ShowsClashPower={visual.ShowsClashPower}, " +
                $"ClashSteps={request.ClashSteps?.Count ?? 0}, " +
                $"HitDamages={request.HitDamages?.Count ?? 0}");
        }

        yield return ShowActionAnnouncement(
            playback,
            visual);
            
        StartCameraShots(
            playback,
            request,
            visual,
            SkillCameraShotTiming.OnActionStart);

        BeginSkillCamera(
            playback,
            request,
            visual);

        if (visual.FaceEachOther &&
            target != null)
        {
            playback.ShouldRestoreFacing = true;

            yield return FaceEachOther(
                views,
                attacker,
                target);
        }

        if (views.AttackerMover != null &&
            target != null)
        {
            if (visual.MoveSettings != null)
            {
                CharacterActionStartPositionMode startPositionMode =
                    visual.MoveSettings.StartPositionMode;

                playback.ShouldRestoreAttackerPosition =
                    visual.MoveSettings.UseMove &&
                    startPositionMode != CharacterActionStartPositionMode.None &&
                    startPositionMode != CharacterActionStartPositionMode.CurrentPosition;

                yield return views.AttackerMover.MoveToActionStartPosition(
                    attacker,
                    target,
                    request.TargetPart,
                    visual.MoveSettings);
            }
            else if (visual.MovesToTarget)
            {
                playback.ShouldRestoreAttackerPosition = true;

                yield return views.AttackerMover.MoveNearTarget(
                    target,
                    request.TargetPart);
            }
        }

        yield return WaitSkillCameraArrive(
            visual);

        if (visual.ShowsClashPower)
        {
            StartCameraShots(
                playback,
                request,
                visual,
                SkillCameraShotTiming.OnClashRoll);

            if (visual.ClashRollCameraLeadTime > 0f)
            {
                yield return new WaitForSeconds(
                    visual.ClashRollCameraLeadTime);
            }

            yield return ShowClashPower(
                playback,
                request,
                views,
                visual);
        }
        
        PlaySkillVfx(
            playback,
            request,
            visual,
            BattleVfxTiming.BeforeAttackAnimation);
        
        StartCameraShots(
            playback,
            request,
            visual,
            SkillCameraShotTiming.BeforeAttackAnimation);

        if (visual.BeforeActionDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.BeforeActionDelay);
        }

        int hitFrameCount = 0;

        if (views.AttackerView != null)
        {
            yield return views.AttackerView.PlayAction(
                request.ActionType,
                onHitFrame: () =>
                {
                    int hitIndex =
                        hitFrameCount;

                    hitFrameCount++;

                    ApplyHitFrame(
                        playback,
                        views,
                        visual,
                        hitIndex);
                },
                onEffectFrame: null);
        }

        ApplyMissingHitFrameFallback(
            playback,
            views,
            visual,
            hitFrameCount);

        views.AttackerView?.RefreshVisualState();
        views.TargetView?.RefreshVisualState();
        
        PlaySkillVfx(
            playback,
            request,
            visual,
            BattleVfxTiming.AfterAction);

        if (visual.AfterActionDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.AfterActionDelay);
        }

        if (views.AttackerMover != null)
        {
            if (visual.MoveSettings != null)
            {
                yield return views.AttackerMover.ReturnToDefaultPosition(
                    visual.MoveSettings);
            }
            else if (visual.ReturnPositionAfterAction &&
                    visual.MovesToTarget)
            {
                yield return views.AttackerMover.ReturnToDefaultPosition();
            }
        }

        playback.ShouldRestoreAttackerPosition = false;

        if (visual.ReturnFacingAfterAction)
        {
            yield return ReturnFacing(
                views);
        }

        playback.ShouldRestoreFacing = false;

        StopCameraShotRoutine();

        Coroutine afterActionCameraRoutine =
            StartCameraShots(
                playback,
                request,
                visual,
                SkillCameraShotTiming.AfterAction);

        if (afterActionCameraRoutine != null)
            yield return afterActionCameraRoutine;

        EndSkillCamera(
            playback,
            visual);

        if (visual.AfterReturnDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.AfterReturnDelay);
        }

        yield return HideActionAnnouncement(
            playback,
            visual);

        yield return PlayMomentumRefreshAtVisualEnd(
            playback);
    }
    
    private void PlaySkillVfx(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        BattleVfxTiming timing,
        int hitIndex = -1,
        int damage = 0)
    {
        if (request == null || visual == null)
            return;

        if (vfxManager == null)
            return;

        if (visual.VfxCues == null)
            return;

        BattleVfxContext context =
            new BattleVfxContext
            {
                Attacker = request.Attacker,
                Target = request.Target,
                AttackerView = playback?.AttackerView,
                TargetView = playback?.TargetView,
                TargetPart = request.TargetPart,
                HitIndex = hitIndex,
                Damage = damage
            };

        context.BindPlayback(playback);

        foreach (BattleVfxCue cue in visual.VfxCues)
        {
            if (cue == null)
                continue;

            if (cue.Timing != timing)
                continue;

            vfxManager.PlayCue(
                cue,
                context);
        }
    }
    
    private IEnumerator PlayMomentumRefreshAtVisualEnd(
        BattleVisualPlaybackState playback)
    {
        if (momentumScrollbarUI == null)
            yield break;

        yield return momentumScrollbarUI.ReleaseAndAnimateToRealMomentumRoutine();

        if (playback != null)
            playback.IsMomentumDisplayLocked = false;
    }

    private void BeginVisualRequest(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (playback == null)
            return;

        BattleVisualRequest request =
            playback.Request;

        playback.HasBegun = true;

        damagePresenter ??=
            new BattleVisualDamagePresenter(
                battleUIManager,
                logDebug);

        damagePresenter.Prepare(
            playback,
            visual);

        if (targetArrowUI != null)
        {
            playback.IsTargetArrowBound = true;
            targetArrowUI.SetCurrentVisualRequest(request);
        }

        if (momentumScrollbarUI != null)
        {
            playback.IsMomentumDisplayLocked = true;
            momentumScrollbarUI.LockCurrentDisplay();
        }

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
                    StopCameraShotRoutine();
                    break;

                case BattleVisualCleanupPhase.AttackerAnimation:
                    if (playback.AttackerView != null)
                    {
                        if (playback.IsCancellationRequested)
                            playback.AttackerView.AbortActionPlayback();
                        else
                            playback.AttackerView.CancelActionPlayback();
                    }
                    break;

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

                    if (shouldRestore && playback.AttackerMover != null)
                        playback.AttackerMover.ReturnToDefaultPositionInstant();
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
                            playback.Request);
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
            if (playback.IsCancellationRequested)
            {
                foreach (GameObject instance in playback.SpawnedVfxInstances)
                {
                    if (instance != null)
                        Destroy(instance);
                }
            }
        }
        finally
        {
            playback.SpawnedVfxInstances.Clear();
        }
    }

    private void BeginSkillCamera(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual)
    {
        if (request == null ||
            visual == null)
            return;

        if (!visual.UsesTargetCamera)
            return;

        if (HasAnyCameraShots(visual.CameraDefinition))
            return;

        if (cameraDirector == null)
            return;

        if (request.Attacker == null ||
            request.Target == null)
            return;

        if (playback != null)
            playback.HasCameraActivity = true;

        cameraDirector.FocusBetween(
            request.Attacker,
            request.Target);
    }

    private Coroutine StartCameraShots(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraShotTiming timing)
    {
        if (request == null || visual == null)
            return null;

        if (cameraDirector == null)
            return null;

        if (visual.CameraDefinition == null)
            return null;

        if (!HasCameraShots(
                visual.CameraDefinition,
                timing))
        {
            return null;
        }

        StopCameraShotRoutine();

        cameraShotRoutine =
            StartCoroutine(
                PlayCameraShots(
                    request,
                    visual,
                    timing));

        if (playback != null)
            playback.HasCameraActivity = true;

        return cameraShotRoutine;
    }

    private void StopCameraShotRoutine()
    {
        if (cameraShotRoutine == null)
            return;

        StopCoroutine(cameraShotRoutine);
        cameraShotRoutine = null;
    }

    private static bool HasCameraShots(
        SkillCameraDefinition definition,
        SkillCameraShotTiming timing)
    {
        if (definition == null ||
            definition.Shots == null)
        {
            return false;
        }

        foreach (SkillCameraShot shot in definition.Shots)
        {
            if (shot != null &&
                shot.Timing == timing)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyCameraShots(
        SkillCameraDefinition definition)
    {
        if (definition == null ||
            definition.Shots == null)
        {
            return false;
        }

        foreach (SkillCameraShot shot in definition.Shots)
        {
            if (shot != null)
                return true;
        }

        return false;
    }

    private IEnumerator PlayCameraShots(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraShotTiming timing)
    {
        if (request == null || visual == null)
            yield break;

        if (cameraDirector == null)
            yield break;

        if (visual.CameraDefinition == null)
            yield break;

        yield return cameraDirector.PlayShotsByTiming(
            request,
            visual.CameraDefinition,
            timing);

        cameraShotRoutine = null;
    }

    private IEnumerator WaitSkillCameraArrive(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (!visual.UsesTargetCamera)
            yield break;

        if (HasAnyCameraShots(visual.CameraDefinition))
            yield break;

        if (cameraDirector == null)
            yield break;

        yield return cameraDirector.WaitUntilArrived(
            visual.CameraArriveTimeout);
    }

    private void EndSkillCamera(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (visual == null)
        {
            if (playback != null)
                playback.HasCameraActivity = false;

            return;
        }

        if (cameraDirector == null)
        {
            if (playback != null)
                playback.HasCameraActivity = false;

            return;
        }

        if (HasAnyCameraShots(visual.CameraDefinition))
        {
            if (visual.CameraDefinition.ReturnToOverviewAfterAction)
            {
                cameraDirector.Return(
                    visual.CameraDefinition);
            }

            if (playback != null)
                playback.HasCameraActivity = false;

            return;
        }

        if (!visual.ReturnCameraAfterAction)
        {
            if (playback != null)
                playback.HasCameraActivity = false;

            return;
        }

        cameraDirector.Return();

        if (playback != null)
            playback.HasCameraActivity = false;
    }

    private void PlayHitCameraShake(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            return;

        if (!visual.UseHitCameraShake)
            return;

        if (cameraDirector == null)
            return;

        cameraDirector.PlayShake(
            visual.HitShake);
    }

    private void ApplyMissingHitFrameFallback(
        BattleVisualPlaybackState playback,
        CharacterViewSet views,
        SkillVisualDefinition visual,
        int hitFrameCount)
    {
        if (playback == null ||
            visual == null)
        {
            return;
        }

        if (!visual.HasHitFrameDamage)
            return;

        int plannedHitCount =
            playback.HitDamages.Count;

        if (plannedHitCount <= hitFrameCount)
        {
            if (hitFrameCount > 0 ||
                !visual.ApplyDamageIfNoHitFrame)
            {
                return;
            }
        }

        int remainingDamage = 0;

        for (int i = Mathf.Max(0, hitFrameCount);
             i < plannedHitCount;
             i++)
        {
            remainingDamage +=
                Mathf.Max(
                    0,
                    playback.HitDamages[i]);
        }

        BattleVisualRequest request =
            playback.Request;

        if (plannedHitCount == 0)
        {
            remainingDamage =
                Mathf.Max(
                    0,
                    request.GetDamageForHitIndex(0));
        }

        if (remainingDamage <= 0 &&
            !visual.ApplyDamageIfNoHitFrame)
        {
            return;
        }

        Debug.LogWarning(
            $"[BattleAnimationDirector] 계획된 HitFrame 일부가 없어 fallback 피격/데미지 연출을 적용합니다. " +
            $"Attacker={request.Attacker?.Data.CharacterName}, " +
            $"Target={request.Target?.Data.CharacterName}, " +
            $"ActualHits={hitFrameCount}, PlannedHits={plannedHitCount}, " +
            $"RemainingDamage={remainingDamage}");

        ApplyHitFrame(
            playback,
            views,
            visual,
            Mathf.Max(0, hitFrameCount),
            remainingDamage);
    }

    private IEnumerator ShowActionAnnouncement(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        BattleVisualRequest request =
            playback != null
                ? playback.Request
                : null;

        if (request == null)
            yield break;

        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        playback.IsAnnouncementVisible = true;

        yield return actionAnnounceUI.ShowPersistent(
            request);
    }

    private IEnumerator HideActionAnnouncement(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        yield return actionAnnounceUI.Hide();

        if (playback != null)
            playback.IsAnnouncementVisible = false;
    }

    private void ApplyHitFrame(
        BattleVisualPlaybackState playback,
        CharacterViewSet views,
        SkillVisualDefinition visual,
        int hitIndex,
        int? damageOverride = null)
    {
        if (playback == null ||
            playback.IsCancellationRequested ||
            playback.IsCompleted ||
            playback.IsCleanedUp ||
            !ReferenceEquals(activePlayback, playback))
        {
            return;
        }

        BattleVisualRequest request =
            playback.Request;

        if (request == null ||
            visual == null)
        {
            return;
        }

        if (!visual.HasHitFrameDamage)
            return;

        CharacterView targetView =
            views.TargetView;

        if (targetView == null &&
            request.Target != null)
        {
            targetView =
                BattleCameraTargetResolver.GetView(
                    request.Target);
        }

        if (targetView == null)
        {
            Debug.LogWarning(
                $"[BattleAnimationDirector] TargetView 없음. 피격 연출 불가 / Target={request.Target?.name}");
            return;
        }

        int damage =
            damageOverride ??
            damagePresenter.GetDamageForHitIndex(
                playback,
                hitIndex);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleAnimationDirector] HitFrame 적용 : " +
                $"{request.Attacker?.Data.CharacterName} -> {request.Target?.Data.CharacterName} / " +
                $"HitIndex={hitIndex} / Damage={damage}");
        }
            
        PlaySkillVfx(
            playback,
            request,
            visual,
            BattleVfxTiming.OnHitFrame,
            hitIndex,
            damage);

        targetView.PlayHitRestart();

        if (damage > 0)
        {
            ShowDamageNumber(
                targetView,
                request.TargetPart,
                damage);
        }

        StartCameraShots(
            playback,
            request,
            visual,
            SkillCameraShotTiming.OnHitFrame);

        damagePresenter.ApplyHit(
            playback,
            hitIndex,
            damage);

        PlayHitCameraShake(
            visual);
    }

    private IEnumerator ShowClashPower(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        CharacterViewSet views,
        SkillVisualDefinition visual)
    {
        if (floatingTextManager == null)
        {
            if (logMissingReferences)
                Debug.LogWarning("[BattleAnimationDirector] FloatingTextManager 없음");

            yield break;
        }

        if (request.ClashSteps == null ||
            request.ClashSteps.Count == 0)
        {
            yield break;
        }

        Transform attackerAnchor =
            GetClashTextAnchor(
                request.Attacker,
                views.AttackerView);

        Transform targetAnchor =
            GetClashTextAnchor(
                request.Target,
                views.TargetView);

        if (playback != null)
            playback.HasFloatingTextActivity = true;

        yield return floatingTextManager.ShowClashPowerSequence(
            attackerAnchor,
            targetAnchor,
            request.ClashSteps,
            visual.ClashColor,
            visual.TieColor);

        if (playback != null)
            playback.HasFloatingTextActivity = false;
    }

    private void ShowDamageNumber(
        CharacterView targetView,
        BodyPart targetPart,
        int damage)
    {
        if (damageNumberManager == null)
        {
            if (logMissingReferences)
                Debug.LogWarning("[BattleAnimationDirector] DamageNumberManager 없음");

            return;
        }

        if (targetView == null)
            return;

        Vector3 position =
            targetView.GetDamageNumberPosition(
                targetPart);

        damageNumberManager.ShowDamage(
            position,
            damage);
    }

    private IEnumerator FaceEachOther(
        CharacterViewSet views,
        Character attacker,
        Character target)
    {
        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (views.AttackerFacing != null)
        {
            attackerRoutine =
                StartCoroutine(
                    views.AttackerFacing.FaceTargetSmooth(
                        target));
        }

        if (views.TargetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    views.TargetFacing.FaceTargetSmooth(
                        attacker));
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }

    private IEnumerator ReturnFacing(
        CharacterViewSet views)
    {
        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (views.AttackerFacing != null)
        {
            attackerRoutine =
                StartCoroutine(
                    views.AttackerFacing.ReturnToDefaultSmooth());
        }

        if (views.TargetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    views.TargetFacing.ReturnToDefaultSmooth());
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }

    private Transform GetClashTextAnchor(
        Character character,
        CharacterView view)
    {
        if (view != null)
        {
            Transform headAnchor =
                view.GetBodyPartAnchor(
                    PartType.HEAD);

            if (headAnchor != null &&
                headAnchor != view.transform)
            {
                return headAnchor;
            }

            if (view.LookAtPoint != null)
                return view.LookAtPoint;
        }

        if (character != null)
            return character.transform;

        return null;
    }

    private CharacterViewSet GetViews(
        Character attacker,
        Character target)
    {
        CharacterViewSet result =
            new CharacterViewSet();

        if (attacker != null)
        {
            result.AttackerView =
                BattleCameraTargetResolver.GetView(
                    attacker);

            result.AttackerFacing =
                attacker.GetComponentInChildren<CharacterFacingController>(true);

            result.AttackerMover =
                attacker.GetComponentInChildren<CharacterActionMover>(true);
        }

        if (target != null)
        {
            result.TargetView =
                BattleCameraTargetResolver.GetView(
                    target);

            result.TargetFacing =
                target.GetComponentInChildren<CharacterFacingController>(true);
        }

        return result;
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

    private struct CharacterViewSet
    {
        public CharacterView AttackerView;
        public CharacterView TargetView;

        public CharacterFacingController AttackerFacing;
        public CharacterFacingController TargetFacing;

        public CharacterActionMover AttackerMover;
    }
}
