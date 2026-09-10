using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class BattleAnimationDirector : MonoBehaviour
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

    [Header("Structured Clash Presentation")]
    [SerializeField]
    private bool useStructuredClashPresentation = true;

    [SerializeField, Min(0.5f)]
    private float clashParticipantSpacing = 2.4f;

    [SerializeField, Min(0.01f)]
    private float clashStagingMoveSpeed = 8f;

    [SerializeField, Min(0.001f)]
    private float clashStagingArriveDistance = 0.03f;

    [SerializeField, Min(0.01f)]
    private float clashMotionPlaybackSpeed = 1f;

    [SerializeField, Min(0f)]
    private float clashEnterMaximumDuration = 0.55f;

    [SerializeField, Min(0f)]
    private float clashContestMaximumDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float clashResultMaximumDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float clashReengageMaximumDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float clashExitMaximumDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float clashMissingMotionFallbackDuration = 0.08f;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private BattleActionAnnounceUI actionAnnounceUI;
    [SerializeField] private BattleClashRollPresentationUI clashRollPresentationUI;

    private bool isPlaying;

    /// <summary>
    /// 하나의 BattleVisualRequest가 실제 화면 재생까지 끝난 뒤 발행됩니다.
    /// 전투 로직 완료(OnActionEnd)와 프레젠테이션 완료 시점을 분리해서
    /// 행동 순서 UI처럼 시각적 완료 타이밍이 필요한 시스템이 사용합니다.
    /// </summary>
    public event Action<BattleVisualRequest> VisualRequestCompleted;

    /// <summary>
    /// 논리 BattleAction의 특정 교환이 실제 Timeline hit frame에 도달했을 때 발행한다.
    /// 상태 VFX처럼 논리 결과를 해당 시각 시점에 합류시키는 Presenter가 사용한다.
    /// exchangeIndex는 일방/비합 요청에서 -1일 수 있다.
    /// </summary>
    public event Action<BattleAction, int, int> HitFramePresented;

    private BattleVisualRequestBuilder requestBuilder;
    private BattleVisualDamagePresenter damagePresenter;
    private BattleHitPresenter hitPresenter;
    private SkillTimelineEventRouter timelineEventRouter;
    private ClashStageController clashStageController;
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

        RebuildPresentationPresenters();
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
            
        if (momentumScrollbarUI == null ||
            !momentumScrollbarUI.isActiveAndEnabled ||
            !momentumScrollbarUI.gameObject.activeInHierarchy)
        {
            MomentumScrollbarUI[] momentumBars =
                FindObjectsByType<MomentumScrollbarUI>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            MomentumScrollbarUI fallbackBar = null;

            for (int i = 0;
                 i < momentumBars.Length;
                 i++)
            {
                MomentumScrollbarUI candidate =
                    momentumBars[i];

                if (candidate == null)
                    continue;

                fallbackBar ??= candidate;

                if (candidate.isActiveAndEnabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    fallbackBar = candidate;
                    break;
                }
            }

            momentumScrollbarUI = fallbackBar;
        }
            
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();
    }

    private void EnsurePresentationPresenters()
    {
        if (damagePresenter != null &&
            hitPresenter != null &&
            timelineEventRouter != null &&
            clashStageController != null)
        {
            return;
        }

        RebuildPresentationPresenters();
    }

    private void RebuildPresentationPresenters()
    {
        damagePresenter =
            new BattleVisualDamagePresenter(
                battleUIManager,
                logDebug);

        hitPresenter =
            new BattleHitPresenter(
                damagePresenter,
                damageNumberManager,
                vfxManager,
                IsPlaybackActive,
                NotifyHitFramePresented,
                logMissingReferences,
                logDebug,
                this);

        timelineEventRouter =
            new SkillTimelineEventRouter(
                hitPresenter,
                cameraDirector,
                logDebug,
                this);

        clashStageController =
            new ClashStageController(
                this,
                clashParticipantSpacing,
                clashStagingMoveSpeed,
                clashStagingArriveDistance,
                clashMotionPlaybackSpeed,
                clashEnterMaximumDuration,
                clashReengageMaximumDuration,
                clashExitMaximumDuration,
                clashMissingMotionFallbackDuration);
    }

    private bool IsPlaybackActive(
        BattleVisualPlaybackState playback)
    {
        return playback != null &&
               ReferenceEquals(
                   activePlayback,
                   playback);
    }

    private void NotifyHitFramePresented(
        BattleAction sourceAction,
        int exchangeIndex,
        int hitIndex)
    {
        if (sourceAction == null)
            return;

        try
        {
            HitFramePresented?.Invoke(
                sourceAction,
                exchangeIndex,
                hitIndex);
        }
        catch (Exception exception)
        {
            // 프레젠테이션 관찰자 실패가 Timeline 재생 자체를 중단시키지 않게 격리한다.
            Debug.LogException(
                exception,
                this);
        }
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

        // ResolveReferences에서 갱신된 Scene/UI/VFX 참조를 새 playback presenter에 반영한다.
        // 재생 중인 presenter를 교체하지 않도록 isPlaying 검사 뒤에 수행한다.
        RebuildPresentationPresenters();

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

            if (!playback.IsCancellationRequested)
            {
                BattleVisualRequest completedRequest =
                    playback.RootRequest ??
                    playback.Request;

                if (completedRequest != null)
                {
                    try
                    {
                        VisualRequestCompleted?.Invoke(
                            completedRequest);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(
                            exception,
                            this);
                    }
                }
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
        playback.TargetMover = views.TargetMover;
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
            "SkillVisualDefinition의 필수 Timeline Segment와 CameraRig 참조를 확인하세요.",
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

        bool allowDefinitionStaging =
            !isClashAttack ||
            isOneSided ||
            !useStructuredClashPresentation;

        if (manageActionLifecycle)
        {
            yield return ShowActionAnnouncement(
                playback,
                visual);
        }

        if (allowDefinitionStaging &&
            definition.PrepareFacing &&
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

        if (allowDefinitionStaging &&
            definition.PrepareMovement &&
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

        EnsurePresentationPresenters();

        Action<SkillCutsceneEventClip> timelineEventHandler =
            timelineEventRouter?.CreateHandler(
                playback,
                request,
                visual,
                views.TargetView,
                exchangeIndex,
                isClashAttack,
                isOneSided);

        yield return skillCutsceneDirector
            .PlaySequence(
                request,
                definition,
                isClashAttack,
                playbackSpeed,
                timelineEventHandler);

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
        playback.TargetMover = rootViews.TargetMover;
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

        bool hasActualClash =
            useStructuredClashPresentation &&
            HasActualClashExchange(request);

        ClashStageController.Session clashSession =
            hasActualClash
                ? clashStageController.CreateSession(
                    request,
                    rootViews.AttackerView,
                    rootViews.TargetView,
                    rootViews.AttackerMover,
                    rootViews.TargetMover,
                    rootViews.AttackerFacing,
                    rootViews.TargetFacing)
                : null;

        if (clashSession != null)
        {
            yield return clashStageController.Enter(
                playback,
                clashSession);
        }

        bool sequenceEndedByKill = false;
        Character killedCharacter = null;

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

            if (clashSession != null &&
                !exchange.IsOneSided)
            {
                yield return clashStageController.PlayPairedMotion(
                    clashSession.FirstView,
                    ClashMotionKey.Contest,
                    clashSession.SecondView,
                    ClashMotionKey.Contest,
                    animationSpeed,
                    clashContestMaximumDuration);
            }

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

            // 양측 굴림이 있는 합은 굴림 결과가 화면에 확정되는 즉시 기세를 이동시킨다.
            // 이전에는 실제 타격 연출까지 끝난 뒤에 움직여 사용자가 "굴림 -> 기세" 연결을
            // 바로 읽기 어려웠다.
            if (!exchange.IsOneSided)
            {
                yield return PlayMomentumExchangeStep(
                    playback,
                    exchange);
            }

            if (clashSession != null &&
                !exchange.IsOneSided)
            {
                if (!exchange.HasResolvedWinner)
                {
                    yield return clashStageController.PlayPairedMotion(
                        clashSession.FirstView,
                        ClashMotionKey.Tie,
                        clashSession.SecondView,
                        ClashMotionKey.Tie,
                        animationSpeed,
                        clashResultMaximumDuration);
                }
                else
                {
                    CharacterViewSet resultViews =
                        GetViews(
                            exchange.WinnerAction.Owner,
                            exchange.LoserAction.Owner);

                    yield return clashStageController.PlayPairedMotion(
                        resultViews.AttackerView,
                        ClashMotionKey.Advantage,
                        resultViews.TargetView,
                        ClashMotionKey.Disadvantage,
                        animationSpeed,
                        clashResultMaximumDuration);
                }
            }

            if (exchange.HasAttack)
            {
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

            // 일방 공격은 상대 굴림이 없으므로 실제 적중 연출 직후 기세를 이동시킨다.
            if (exchange.IsOneSided)
            {
                yield return PlayMomentumExchangeStep(
                    playback,
                    exchange);
            }

            bool killedTarget =
                exchange.AttackRequest?.WasKilled == true;

            if (killedTarget)
            {
                sequenceEndedByKill = true;
                killedCharacter =
                    exchange.AttackRequest?.Target;
                break;
            }

            bool hasNextExchange =
                HasNextExchange(
                    request,
                    i);

            bool hasNextPairedExchange =
                HasNextPairedExchange(
                    request,
                    i);

            if (clashSession != null &&
                !exchange.IsOneSided &&
                hasNextPairedExchange)
            {
                yield return clashStageController.Reengage(
                    clashSession,
                    animationSpeed);
            }
            else
            {
                float exchangeGap =
                    GetClashExchangeGap(i);

                if (exchangeGap > 0f &&
                    hasNextExchange)
                {
                    yield return new WaitForSeconds(
                        exchangeGap);
                }
            }
        }

        yield return PlayMomentumSequenceFinalStep(
            playback,
            request);

        if (clashSession != null &&
            !sequenceEndedByKill &&
            !playback.IsCancellationRequested)
        {
            yield return clashStageController.Exit(
                clashSession);
        }

        yield return clashStageController.Restore(
            playback,
            clashSession,
            killedCharacter);

        playback.ResetToRootRequest();

        if (logDebug)
        {
            Debug.Log(
                "[BattleAnimationDirector] 연속 합 종료 / 공통 Clash Exit 후 원위치 복귀 완료");
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

    private static bool HasActualClashExchange(
        BattleVisualRequest request)
    {
        if (request?.ClashExchanges == null)
            return false;

        foreach (BattleClashVisualExchange exchange
                 in request.ClashExchanges)
        {
            if (exchange != null &&
                !exchange.IsOneSided)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNextExchange(
        BattleVisualRequest request,
        int currentIndex)
    {
        if (request?.ClashExchanges == null)
            return false;

        for (int index = currentIndex + 1;
             index < request.ClashExchanges.Count;
             index++)
        {
            if (request.ClashExchanges[index] != null)
                return true;
        }

        return false;
    }

    private static bool HasNextPairedExchange(
        BattleVisualRequest request,
        int currentIndex)
    {
        if (request?.ClashExchanges == null)
            return false;

        for (int index = currentIndex + 1;
             index < request.ClashExchanges.Count;
             index++)
        {
            BattleClashVisualExchange next =
                request.ClashExchanges[index];

            if (next == null)
                continue;

            return !next.IsOneSided;
        }

        return false;
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

        EnsurePresentationPresenters();

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
            restoreStaging: false,
            trackSequenceStaging: true);

        playback.ActiveActionView = null;
        playback.ActiveTargetView = null;
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

        EnsurePresentationPresenters();

        if (prepareDamage)
        {
            damagePresenter.Prepare(
                playback,
                visual);
        }
        else if (request?.HasClashSequence == true)
        {
            damagePresenter.PrepareSequence(
                playback);
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
    



    // Camera 위치·전환은 Skill Camera Timeline Track만 담당한다.
    // BattleCameraDirector는 Timeline Event가 명시적으로 요청한 Impact Pulse/Shake에만 사용한다.





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
        CharacterViewSet views,
        CharacterView preserveView = null)
    {
        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (views.AttackerFacing != null &&
            !ReferenceEquals(
                views.AttackerView,
                preserveView))
        {
            attackerRoutine =
                StartCoroutine(
                    views.AttackerFacing.ReturnToDefaultSmooth());
        }

        if (views.TargetFacing != null &&
            !ReferenceEquals(
                views.TargetView,
                preserveView))
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

            result.TargetMover =
                target.GetComponentInChildren<CharacterActionMover>(true);
        }

        return result;
    }
    

    private struct CharacterViewSet
    {
        public CharacterView AttackerView;
        public CharacterView TargetView;

        public CharacterFacingController AttackerFacing;
        public CharacterFacingController TargetFacing;

        public CharacterActionMover AttackerMover;
        public CharacterActionMover TargetMover;
    }

}