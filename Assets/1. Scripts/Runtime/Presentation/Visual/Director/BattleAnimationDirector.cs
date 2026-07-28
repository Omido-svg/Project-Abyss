using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAnimationDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private SkillCutsceneDirector skillCutsceneDirector;
    [SerializeField] private BattleWorldFloatingTextManager floatingTextManager;
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField, Tooltip(
        "이전 Scene 직렬화 호환용입니다. Timeline-only 런타임은 SkillDefinition의 전용 Visual만 사용합니다.")]
    private SkillVisualProfile defaultVisualProfile;
    [SerializeField] private TargetArrowUI targetArrowUI;
    [SerializeField] private MomentumScrollbarUI momentumScrollbarUI;
    [SerializeField] private BattleVfxManager vfxManager;

    [Header("Fallback")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    [Header("Continuous Clash Sequence")]
    [SerializeField, Min(0.01f)]
    private float clashInitialAnimationSpeed = 1f;

    [SerializeField, Min(0f)]
    private float clashAnimationSpeedStep = 0.12f;

    [SerializeField, Min(0.01f)]
    private float clashMaximumAnimationSpeed = 1.5f;

    [SerializeField, Min(0f)]
    private float clashBaseExchangeGap = 0.16f;

    [SerializeField, Range(0.05f, 1f)]
    private float clashExchangeGapMultiplier = 0.78f;

    [SerializeField, Min(0f)]
    private float clashMinimumExchangeGap = 0.04f;

    [SerializeField, Min(0f)]
    private float clashBaseRollHold = 0.28f;

    [SerializeField, Min(0f)]
    private float clashMinimumRollHold = 0.06f;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private BattleActionAnnounceUI actionAnnounceUI;
    [SerializeField] private BattleClashRollPresentationUI clashRollPresentationUI;

    private bool isPlaying;
    private BattleVisualRequestBuilder requestBuilder;
    private BattleVisualDamagePresenter damagePresenter;
    private BattleVisualPlaybackState activePlayback;

    public bool IsPlaying =>
        isPlaying ||
        activePlayback != null;

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

        if (skillCutsceneDirector == null)
        {
            skillCutsceneDirector =
                GetComponent<SkillCutsceneDirector>();

            if (skillCutsceneDirector == null)
            {
                skillCutsceneDirector =
                    FindFirstObjectByType<
                        SkillCutsceneDirector>(
                            FindObjectsInactive.Include);
            }

            if (skillCutsceneDirector == null &&
                Application.isPlaying)
            {
                skillCutsceneDirector =
                    gameObject.AddComponent<
                        SkillCutsceneDirector>();
            }
        }

        if (floatingTextManager == null)
            floatingTextManager = FindFirstObjectByType<BattleWorldFloatingTextManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();

        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();

        if (actionAnnounceUI == null)
            actionAnnounceUI = FindFirstObjectByType<BattleActionAnnounceUI>();

        if (clashRollPresentationUI == null)
        {
            clashRollPresentationUI =
                FindFirstObjectByType<BattleClashRollPresentationUI>(
                    FindObjectsInactive.Include);
        }

        if (targetArrowUI == null)
            targetArrowUI = FindFirstObjectByType<TargetArrowUI>();
            
        if (momentumScrollbarUI == null)
            momentumScrollbarUI = FindFirstObjectByType<MomentumScrollbarUI>();
            
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();
    }

    public void AssignClashRollPresentationUI(
        BattleClashRollPresentationUI value)
    {
        clashRollPresentationUI = value;
    }

    public IEnumerator Play(BattleVisualRequest request)
    {
        if (request == null)
            yield break;

        // Scene 재로드나 UI 재구성 뒤 파괴된 Unity 참조를 새 인스턴스로 다시 연결한다.
        ResolveReferences();

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

        skillCutsceneDirector?.Cancel();

        StopAllCoroutines();

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
        if (playback?.Request?.HasClashSequence == true)
        {
            yield return PlayClashSequenceInternal(playback);
            yield break;
        }

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

        BattleVisualValidator.ValidateDefinition(
            visual,
            attacker,
            logWarnings: true);

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

        if (!HasRequiredTimelineCutscene(
                visual,
                SkillCutsceneSegment.Action,
                request))
        {
            yield break;
        }

        yield return PlayTimelineCutsceneInternal(
            playback,
            request,
            visual,
            views,
            isClashAttack: false,
            exchangeIndex: -1,
            isOneSided: false,
            playbackSpeed: 1f,
            manageActionLifecycle: true);
    }
    
    private bool HasRequiredTimelineCutscene(
        SkillVisualDefinition visual,
        SkillCutsceneSegment segment,
        BattleVisualRequest request)
    {
        SkillVisualDefinition definition =
            visual;

        bool valid =
            skillCutsceneDirector != null &&
            definition != null &&
            definition.HasCompleteTimelineSet &&
            definition.HasTimeline(segment);

        if (valid)
            return true;

        string missing = definition == null
            ? "SkillVisualDefinition"
            : string.Join(", ", definition.GetMissingRequirements());

        Debug.LogError(
            "[BattleAnimationDirector] Timeline-only 스킬 연출을 재생할 수 없습니다. " +
            $"Skill={request?.SourceAction?.Skill?.SkillName ?? request?.VisualDefinition?.name ?? "UNKNOWN"}, " +
            $"Segment={segment}, Missing={missing}. " +
            "Tools/Project Abyss/Migration/Convert All Combat Skills To Timeline을 실행하세요.",
            visual);

        return false;
    }

    private IEnumerator
        PlayTimelineCutsceneInternal(
            BattleVisualPlaybackState playback,
            BattleVisualRequest request,
            SkillVisualDefinition visual,
            CharacterViewSet views,
            bool isClashAttack,
            int exchangeIndex,
            bool isOneSided,
            float playbackSpeed,
            bool manageActionLifecycle,
            bool includeReturnTimeline = true,
            bool restoreStaging = true,
            bool trackSequenceStaging = false)
    {
        SkillVisualDefinition definition =
            visual;

        if (playback == null ||
            request == null ||
            visual == null ||
            definition == null ||
            skillCutsceneDirector == null)
        {
            yield break;
        }

        int hitFrameCount =
            0;

        if (manageActionLifecycle)
        {
            yield return ShowActionAnnouncement(
                playback,
                visual);
        }

        if (definition.PrepareFacing &&
            request.Target != null &&
            !request.IsSelfTarget)
        {
            if (trackSequenceStaging)
            {
                playback.ShouldRestoreFacing =
                    playback.ShouldRestoreFacing ||
                    definition.RestoreFacing;
            }
            else
            {
                playback.ShouldRestoreFacing =
                    definition.RestoreFacing;
            }

            yield return FaceEachOther(
                views,
                request.Attacker,
                request.Target);
        }

        if (definition.PrepareMovement &&
            views.AttackerMover != null &&
            request.Target != null &&
            !request.IsSelfTarget)
        {
            if (trackSequenceStaging)
            {
                if (definition.RestoreMovement)
                {
                    playback.TrackStagedMover(
                        views.AttackerMover,
                        visual.MoveSettings);
                }
            }
            else
            {
                playback
                    .ShouldRestoreAttackerPosition =
                        definition
                            .RestoreMovement;
            }

            if (visual.MoveSettings != null)
            {
                yield return views.AttackerMover
                    .MoveToActionStartPosition(
                        request.Attacker,
                        request.Target,
                        request.TargetPart,
                        visual.MoveSettings);
            }
            else
            {
                yield return views.AttackerMover
                    .MoveNearTarget(
                        request.Target,
                        request.TargetPart);
            }
        }

        yield return skillCutsceneDirector
            .PlaySequence(
                request,
                definition,
                isClashAttack,
                playbackSpeed,
                eventClip =>
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

                            ApplyHitFrame(
                                playback,
                                views,
                                visual,
                                hitIndex,
                                exchangeIndex:
                                    exchangeIndex,
                                isClash:
                                    isClashAttack,
                                isOneSided:
                                    isOneSided);

                            break;
                        }

                        case SkillCutsceneEventType.Vfx:
                            if (!definition.UseExplicitVisualFxTracks)
                            {
                                PlaySkillVfx(
                                    playback,
                                    request,
                                    visual,
                                    eventClip.VfxTiming,
                                    eventClip.HitIndex);
                            }
                            break;

                        case SkillCutsceneEventType
                            .CameraShake:
                            PlayHitCameraShake(
                                visual);
                            break;

                        case SkillCutsceneEventType
                            .CameraImpactPulse:
                        {
                            int pulseHitIndex =
                                eventClip.HitIndex >= 0
                                    ? eventClip.HitIndex
                                    : Mathf.Max(0, hitFrameCount - 1);

                            int pulseDamage =
                                damagePresenter != null
                                    ? damagePresenter.GetDamageForHitIndex(
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
                                request.WasCritical && pulseHitIndex == 0,
                                request.BrokePart,
                                request.WasKilled,
                                isClashAttack,
                                isOneSided);
                            break;
                        }

                        case SkillCutsceneEventType
                            .TargetHitReaction:
                            views.TargetView?
                                .PlayHitRestart();
                            break;

                        case SkillCutsceneEventType
                            .Custom:
                            if (logDebug)
                            {
                                Debug.Log(
                                    "[BattleAnimationDirector] " +
                                    "Timeline Custom Event / " +
                                    $"Key={eventClip.CustomEventKey}",
                                    this);
                            }

                            break;
                    }
                },
                includeReturnTimeline);

        views.AttackerView?
            .RefreshVisualState();

        views.TargetView?
            .RefreshVisualState();

        if (restoreStaging)
        {
            if (views.AttackerMover != null &&
                playback
                    .ShouldRestoreAttackerPosition)
            {
                if (visual.MoveSettings != null)
                {
                    yield return views.AttackerMover
                        .ReturnToDefaultPosition(
                            visual.MoveSettings);
                }
                else
                {
                    yield return views.AttackerMover
                        .ReturnToDefaultPosition();
                }
            }

            playback
                .ShouldRestoreAttackerPosition =
                    false;

            if (definition.RestoreFacing &&
                playback.ShouldRestoreFacing)
            {
                yield return ReturnFacing(
                    views);
            }

            playback.ShouldRestoreFacing =
                false;
        }

        if (!manageActionLifecycle)
            yield break;

        yield return HideActionAnnouncement(
            playback,
            visual);

        yield return
            PlayMomentumRefreshAtVisualEnd(
                playback);
    }

    private IEnumerator PlayClashSequenceInternal(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest request =
            playback.RootRequest;

        if (request == null ||
            request.Attacker == null ||
            request.Target == null)
        {
            yield break;
        }

        SkillVisualDefinition visual =
            request.VisualDefinition;

        if (visual == null)
        {
            Debug.LogWarning(
                "[BattleAnimationDirector] 연속 합 VisualDefinition 없음");
            yield break;
        }

        BattleVisualValidator.ValidateDefinition(
            visual,
            request.Attacker,
            logWarnings: true);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleAnimationDirector] 연속 합 시작 / " +
                $"First={GetCharacterDisplayName(request.Attacker, "NULL")}, " +
                $"Second={GetCharacterDisplayName(request.Target, "NULL")}, " +
                $"Exchanges={request.ClashExchanges.Count}");
        }

        playback.ResetToRootRequest();
        BeginVisualRequest(
            playback,
            visual,
            prepareDamage: false);

        CharacterViewSet rootViews =
            GetViews(
                request.Attacker,
                request.Target);

        playback.AttackerView = rootViews.AttackerView;
        playback.TargetView = rootViews.TargetView;
        playback.AttackerMover = rootViews.AttackerMover;
        playback.AttackerFacing = rootViews.AttackerFacing;
        playback.TargetFacing = rootViews.TargetFacing;

        playback.BeginCueScope();

        if (clashRollPresentationUI != null)
        {
            yield return clashRollPresentationUI
                .ShowSequence(request);
        }
        else
        {
            yield return ShowActionAnnouncement(
                playback,
                visual);
        }

        // 합 수치/UI는 전투 해석 결과를 보여주는 별도 Presentation이다.
        // 실제 스킬 Animation / Camera / VFX / Hit 타이밍은 각 교환의
        // ClashAttack Timeline만 담당한다.
        // 이동/Facing/Return은 합 전체가 끝날 때 한 번만 복구한다.

        BattleVisualRequest lastAttackRequest =
            null;

        SkillVisualDefinition lastAttackVisual =
            visual;

        for (int i = 0;
             i < request.ClashExchanges.Count;
             i++)
        {
            if (playback.IsCancellationRequested)
                break;

            BattleClashVisualExchange exchange =
                request.ClashExchanges[i];

            if (exchange == null)
                continue;

            float animationSpeed =
                GetClashAnimationSpeed(i);

            if (logDebug)
            {
                Debug.Log(
                    $"[BattleAnimationDirector] 연속 합 교환 / " +
                    $"Index={exchange.ExchangeIndex}, " +
                    $"OneSided={exchange.IsOneSided}, " +
                    $"Tie={exchange.IsTie}, " +
                    $"Cancelled={exchange.WasCancelled}, " +
                    $"HasAttack={exchange.HasAttack}, " +
                    $"AnimationSpeed={animationSpeed:0.00}");
            }

            BattleVisualRequest exchangeRequest =
                exchange.AttackRequest ?? request;

            SkillVisualDefinition exchangeVisual =
                exchangeRequest.VisualDefinition ?? visual;

            playback.BeginCueScope();

            bool rollPresentationPlayed = false;

            if (!exchange.IsOneSided &&
                exchangeVisual.ShowsClashPower)
            {
                if (clashRollPresentationUI != null)
                {
                    yield return clashRollPresentationUI
                        .PlayPairedExchange(
                            exchange,
                            i);

                    rollPresentationPlayed = true;
                }
                else
                {
                    yield return ShowClashPowerStep(
                        playback,
                        request,
                        rootViews,
                        exchangeVisual,
                        exchange.DisplayStep,
                        i);
                }
            }

            if (!exchange.IsOneSided &&
                !rollPresentationPlayed &&
                clashRollPresentationUI != null)
            {
                yield return clashRollPresentationUI
                    .PlayPairedExchange(
                        exchange,
                        i);
            }

            if (exchange.IsOneSided &&
                clashRollPresentationUI != null)
            {
                clashRollPresentationUI
                    .BeginOneSidedExchange(exchange);
            }

            if (exchange.HasAttack)
            {
                lastAttackRequest =
                    exchange.AttackRequest;

                lastAttackVisual =
                    exchange.AttackRequest
                        .VisualDefinition ??
                    visual;

                yield return PlayClashExchangeAttack(
                    playback,
                    exchange.AttackRequest,
                    exchange,
                    animationSpeed);
            }

            if (exchange.IsOneSided &&
                clashRollPresentationUI != null)
            {
                clashRollPresentationUI
                    .EndOneSidedExchange();
            }

            // 전투 계산은 이미 끝났지만 기세 UI는 교환별 스냅샷을 따라간다.
            // 따라서 각 굴림의 승패와 공격 연출이 끝난 직후 해당 이동을 재생한다.
            yield return PlayMomentumExchangeStep(
                playback,
                exchange);

            float exchangeGap =
                GetClashExchangeGap(i);

            if (exchangeGap > 0f &&
                i < request.ClashExchanges.Count - 1)
            {
                yield return new WaitForSeconds(
                    exchangeGap);
            }
        }

        yield return PlayMomentumSequenceFinalStep(
            playback,
            request);

        if (!playback.IsCancellationRequested &&
            skillCutsceneDirector != null &&
            lastAttackRequest != null &&
            lastAttackVisual != null &&
            !lastAttackRequest.WasKilled &&
            lastAttackVisual.ReturnTimeline != null)
        {
            playback.SetCurrentRequest(
                lastAttackRequest);

            yield return skillCutsceneDirector
                .PlayReturnSegment(
                    lastAttackRequest,
                    lastAttackVisual,
                    playbackSpeed: 1f);
        }

        yield return RestoreClashSequenceStaging(
            playback,
            rootViews);

        playback.ResetToRootRequest();

        if (logDebug)
        {
            Debug.Log(
                "[BattleAnimationDirector] 연속 합 종료 / 전체 굴림 소모 후 원위치 복귀 완료");
        }

        rootViews.AttackerView?.RefreshVisualState();
        rootViews.TargetView?.RefreshVisualState();

        if (clashRollPresentationUI != null)
        {
            yield return clashRollPresentationUI.Hide();
        }
        else
        {
            yield return HideActionAnnouncement(
                playback,
                visual);
        }

        yield return PlayMomentumRefreshAtVisualEnd(
            playback);
    }

    private IEnumerator PlayClashExchangeAttack(
        BattleVisualPlaybackState playback,
        BattleVisualRequest attackRequest,
        BattleClashVisualExchange exchange,
        float animationSpeed)
    {
        if (playback == null ||
            attackRequest == null ||
            attackRequest.Attacker == null)
        {
            yield break;
        }

        playback.SetCurrentRequest(attackRequest);

        SkillVisualDefinition visual =
            attackRequest.VisualDefinition ??
            playback.RootRequest?.VisualDefinition;

        if (visual == null)
            yield break;

        CharacterViewSet views =
            GetViews(
                attackRequest.Attacker,
                attackRequest.Target);

        playback.ActiveActionView = views.AttackerView;
        playback.ActiveTargetView = views.TargetView;

        damagePresenter ??=
            new BattleVisualDamagePresenter(
                battleUIManager,
                logDebug);

        damagePresenter.Prepare(
            playback,
            visual);

        if (!HasRequiredTimelineCutscene(
                visual,
                SkillCutsceneSegment.ClashAttack,
                attackRequest))
        {
            playback.ActiveActionView = null;
            playback.ActiveTargetView = null;
            yield break;
        }

        yield return PlayTimelineCutsceneInternal(
            playback,
            attackRequest,
            visual,
            views,
            isClashAttack: true,
            exchangeIndex: exchange?.ExchangeIndex ?? -1,
            isOneSided: exchange?.IsOneSided == true,
            playbackSpeed: animationSpeed,
            manageActionLifecycle: false,
            includeReturnTimeline: false,
            restoreStaging: false,
            trackSequenceStaging: true);

        playback.ActiveActionView = null;
        playback.ActiveTargetView = null;
    }

    private IEnumerator RestoreClashSequenceStaging(
        BattleVisualPlaybackState playback,
        CharacterViewSet rootViews)
    {
        if (playback == null)
            yield break;

        List<Coroutine> returnRoutines =
            new List<Coroutine>();

        foreach (KeyValuePair<
                     CharacterActionMover,
                     CharacterActionMoveSettings> pair
                 in playback.StagedMoverSettings)
        {
            CharacterActionMover mover =
                pair.Key;

            if (mover == null)
                continue;

            IEnumerator routine =
                pair.Value != null
                    ? mover.ReturnToDefaultPosition(
                        pair.Value)
                    : mover.ReturnToDefaultPosition();

            returnRoutines.Add(
                StartCoroutine(
                    routine));
        }

        foreach (Coroutine routine
                 in returnRoutines)
        {
            if (routine != null)
                yield return routine;
        }

        playback.ClearStagedMovers();

        if (playback.ShouldRestoreFacing)
        {
            yield return ReturnFacing(
                rootViews);

            playback.ShouldRestoreFacing =
                false;
        }
    }

    private IEnumerator ShowClashPowerStep(
        BattleVisualPlaybackState playback,
        BattleVisualRequest sequenceRequest,
        CharacterViewSet rootViews,
        SkillVisualDefinition visual,
        ClashRollVisualStep step,
        int timingIndex)
    {
        if (floatingTextManager == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning(
                    "[BattleAnimationDirector] FloatingTextManager 없음");
            }

            yield break;
        }

        Transform attackerAnchor =
            GetClashTextAnchor(
                sequenceRequest.Attacker,
                rootViews.AttackerView);

        Transform targetAnchor =
            GetClashTextAnchor(
                sequenceRequest.Target,
                rootViews.TargetView);

        playback.HasFloatingTextActivity = true;

        yield return floatingTextManager
            .ShowClashPowerStep(
                attackerAnchor,
                targetAnchor,
                step,
                visual.ClashColor,
                visual.TieColor,
                GetCharacterDisplayName(
                    sequenceRequest.Attacker,
                    "공격자"),
                GetCharacterDisplayName(
                    sequenceRequest.Target,
                    "대상"),
                timingIndex,
                GetClashRollHold(timingIndex));

        playback.HasFloatingTextActivity = false;
    }

    private float GetClashAnimationSpeed(int exchangeIndex)
    {
        float initial =
            Mathf.Max(0.01f, clashInitialAnimationSpeed);

        float maximum =
            Mathf.Max(initial, clashMaximumAnimationSpeed);

        return Mathf.Min(
            maximum,
            initial +
            Mathf.Max(0, exchangeIndex) *
            Mathf.Max(0f, clashAnimationSpeedStep));
    }

    private float GetClashExchangeGap(int exchangeIndex)
    {
        float multiplier =
            Mathf.Clamp(
                clashExchangeGapMultiplier,
                0.05f,
                1f);

        return Mathf.Max(
            clashMinimumExchangeGap,
            clashBaseExchangeGap *
            Mathf.Pow(
                multiplier,
                Mathf.Max(0, exchangeIndex)));
    }

    private float GetClashRollHold(int exchangeIndex)
    {
        float multiplier =
            Mathf.Clamp(
                clashExchangeGapMultiplier,
                0.05f,
                1f);

        return Mathf.Max(
            clashMinimumRollHold,
            clashBaseRollHold *
            Mathf.Pow(
                multiplier,
                Mathf.Max(0, exchangeIndex)));
    }

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

    private void PlaySkillVfx(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        BattleVfxTiming timing,
        int hitIndex = -1,
        int damage = 0)
    {
        if (request == null ||
            visual == null ||
            vfxManager == null ||
            visual.VfxCues == null)
        {
            return;
        }

        BattleVfxContext context =
            BattleVfxContext.FromRequest(
                request,
                hitIndex,
                damage);

        context.AttackerView =
            playback?.ActiveActionView ??
            BattleCameraTargetResolver.GetView(
                request.Attacker);

        context.TargetView =
            playback?.ActiveTargetView ??
            BattleCameraTargetResolver.GetView(
                request.Target);
        context.BindPlayback(playback);

        for (int i = 0; i < visual.VfxCues.Count; i++)
        {
            BattleVfxCue cue = visual.VfxCues[i];

            if (cue == null || cue.Timing != timing)
                continue;

            if (!ShouldPlayAutoDistributedHitCue(
                    visual,
                    cue,
                    i,
                    hitIndex))
            {
                continue;
            }

            vfxManager.PlayCue(
                cue,
                context,
                i);
        }
    }

    private static bool ShouldPlayAutoDistributedHitCue(
        SkillVisualDefinition visual,
        BattleVfxCue cue,
        int cueIndex,
        int hitIndex)
    {
        if (visual?.VfxCues == null ||
            cue == null ||
            hitIndex < 0 ||
            cue.Timing != BattleVfxTiming.OnHitFrame ||
            cue.UseHitIndexFilter)
        {
            return true;
        }

        int matchingCueCount = 0;
        int matchingCueOrdinal = -1;

        for (int i = 0; i < visual.VfxCues.Count; i++)
        {
            BattleVfxCue candidate = visual.VfxCues[i];

            if (!cue.CanAutoDistributeByHitIndexWith(candidate))
                continue;

            if (i == cueIndex)
                matchingCueOrdinal = matchingCueCount;

            matchingCueCount++;
        }

        if (matchingCueCount <= 1 || matchingCueOrdinal < 0)
            return true;

        // HitIndex 0에는 첫 Cue, HitIndex 1에는 두 번째 Cue를 배정한다.
        // 따라서 Duel 2히트의 복제된 BloodSplash가 첫 타격에 겹쳐 나오거나
        // 두 번째 타격에서 재생 기록 충돌로 사라지는 문제를 동시에 막는다.
        return matchingCueOrdinal == hitIndex;
    }

    private IEnumerator PlayMomentumExchangeStep(
        BattleVisualPlaybackState playback,
        BattleClashVisualExchange exchange)
    {
        if (playback == null ||
            exchange == null ||
            momentumScrollbarUI == null ||
            !playback.IsMomentumDisplayLocked)
        {
            yield break;
        }

        if (exchange.MomentumBefore ==
            exchange.MomentumAfter)
        {
            yield break;
        }

        yield return momentumScrollbarUI
            .AnimateLockedDisplayToRoutine(
                exchange.MomentumAfter);
    }

    private IEnumerator PlayMomentumSequenceFinalStep(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request)
    {
        if (playback == null ||
            request == null ||
            momentumScrollbarUI == null ||
            !playback.IsMomentumDisplayLocked ||
            !request.HasMomentumTimeline)
        {
            yield break;
        }

        if (Mathf.Approximately(
                momentumScrollbarUI.DisplayedMomentum,
                request.MomentumAfterSequence))
        {
            yield break;
        }

        // 결투 대 결투의 최종 다수결 보너스처럼 특정 교환이 아니라
        // 합 전체 종료 시 적용되는 이동을 마지막에 별도로 보여준다.
        yield return momentumScrollbarUI
            .AnimateLockedDisplayToRoutine(
                request.MomentumAfterSequence);
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
        SkillVisualDefinition visual,
        bool prepareDamage = true)
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

        if (prepareDamage)
        {
            damagePresenter.Prepare(
                playback,
                visual);
        }

        if (targetArrowUI != null)
        {
            playback.IsTargetArrowBound = true;
            targetArrowUI.SetCurrentVisualRequest(request);
        }

        if (momentumScrollbarUI != null)
        {
            playback.IsMomentumDisplayLocked = true;

            if (request?.HasClashSequence == true &&
                request.HasMomentumTimeline)
            {
                momentumScrollbarUI.LockDisplayAt(
                    request.MomentumAtSequenceStart);
            }
            else
            {
                momentumScrollbarUI.LockCurrentDisplay();
            }
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
                    cameraDirector?.CancelImpactPulse(
                        restoreLens: true);
                    break;

                case BattleVisualCleanupPhase.AttackerAnimation:
                    CleanupActionView(
                        playback.ActiveActionView,
                        playback.IsCancellationRequested);

                    if (!ReferenceEquals(
                            playback.AttackerView,
                            playback.ActiveActionView))
                    {
                        CleanupActionView(
                            playback.AttackerView,
                            playback.IsCancellationRequested);
                    }

                    if (!ReferenceEquals(
                            playback.TargetView,
                            playback.ActiveActionView) &&
                        !ReferenceEquals(
                            playback.TargetView,
                            playback.AttackerView))
                    {
                        CleanupActionView(
                            playback.TargetView,
                            playback.IsCancellationRequested);
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

    // Camera 위치·전환은 Skill Camera Timeline Track만 담당한다.
    // BattleCameraDirector는 Timeline Event가 명시적으로 요청한 Impact Pulse/Shake에만 사용한다.

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
        if (visual == null)
            return;

        if (!visual.UseHitCameraShake)
            return;

        if (cameraDirector == null)
            return;

        cameraDirector.PlayShake(
            visual.HitShake);
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
        int? damageOverride = null,
        int exchangeIndex = -1,
        bool isClash = false,
        bool isOneSided = false)
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
            
        bool useExplicitVisualFx =
            visual != null &&
            visual.UseExplicitVisualFxTracks;

        if (!useExplicitVisualFx)
        {
            PlaySkillVfx(
                playback,
                request,
                visual,
                BattleVfxTiming.OnHitFrame,
                hitIndex,
                damage);

            if (request.WasCritical && hitIndex == 0)
            {
                PlaySkillVfx(
                    playback,
                    request,
                    visual,
                    BattleVfxTiming.OnCritical,
                    hitIndex,
                    damage);
            }
        }

        targetView.PlayHitRestart();

        if (damage > 0)
        {
            ShowDamageNumber(
                targetView,
                request.TargetPart,
                damage);
        }

        damagePresenter.ApplyHit(
            playback,
            hitIndex,
            damage);

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
            visual.TieColor,
            GetCharacterDisplayName(
                request.Attacker,
                "공격자"),
            GetCharacterDisplayName(
                request.Target,
                "대상"));

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

    private static string GetCharacterDisplayName(
        Character character,
        string fallback)
    {
        if (character == null)
            return fallback;

        string dataName =
            character.Data?.CharacterName;

        if (!string.IsNullOrWhiteSpace(dataName))
            return dataName;

        if (!string.IsNullOrWhiteSpace(character.name))
            return character.name;

        return fallback;
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