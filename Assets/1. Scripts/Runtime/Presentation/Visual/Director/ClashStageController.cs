using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Structured clash 참가자의 staging 위치, facing, clash motion과 복귀를 담당한다.
/// BattleAnimationDirector는 언제 Enter/Reengage/Exit할지만 결정하고,
/// 실제 위치/방향/모션 구현은 이 객체에 위임한다.
/// </summary>
internal sealed class ClashStageController
{
    internal sealed class Session
    {
        public Character FirstCharacter;
        public Character SecondCharacter;
        public CharacterView FirstView;
        public CharacterView SecondView;
        public CharacterActionMover FirstMover;
        public CharacterActionMover SecondMover;
        public CharacterFacingController FirstFacing;
        public CharacterFacingController SecondFacing;
        public Vector3 FirstAnchor;
        public Vector3 SecondAnchor;
    }

    private readonly MonoBehaviour coroutineHost;
    private readonly float participantSpacing;
    private readonly float stagingMoveSpeed;
    private readonly float stagingArriveDistance;
    private readonly float motionPlaybackSpeed;
    private readonly float enterMaximumDuration;
    private readonly float reengageMaximumDuration;
    private readonly float exitMaximumDuration;
    private readonly float missingMotionFallbackDuration;

    public ClashStageController(
        MonoBehaviour coroutineHost,
        float participantSpacing,
        float stagingMoveSpeed,
        float stagingArriveDistance,
        float motionPlaybackSpeed,
        float enterMaximumDuration,
        float reengageMaximumDuration,
        float exitMaximumDuration,
        float missingMotionFallbackDuration)
    {
        this.coroutineHost = coroutineHost;
        this.participantSpacing = participantSpacing;
        this.stagingMoveSpeed = stagingMoveSpeed;
        this.stagingArriveDistance = stagingArriveDistance;
        this.motionPlaybackSpeed = motionPlaybackSpeed;
        this.enterMaximumDuration = enterMaximumDuration;
        this.reengageMaximumDuration = reengageMaximumDuration;
        this.exitMaximumDuration = exitMaximumDuration;
        this.missingMotionFallbackDuration = missingMotionFallbackDuration;
    }

    public Session CreateSession(
        BattleVisualRequest request,
        CharacterView firstView,
        CharacterView secondView,
        CharacterActionMover firstMover,
        CharacterActionMover secondMover,
        CharacterFacingController firstFacing,
        CharacterFacingController secondFacing)
    {
        if (request?.Attacker == null ||
            request.Target == null)
        {
            return null;
        }

        Vector3 firstPosition =
            firstMover != null
                ? firstMover.CurrentWorldPosition
                : request.Attacker.transform.position;

        Vector3 secondPosition =
            secondMover != null
                ? secondMover.CurrentWorldPosition
                : request.Target.transform.position;

        Vector3 direction =
            secondPosition - firstPosition;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = request.Attacker.transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        Vector3 midpoint =
            (firstPosition + secondPosition) * 0.5f;

        float halfSpacing =
            Mathf.Max(
                0.25f,
                participantSpacing * 0.5f);

        Vector3 firstAnchor =
            midpoint - direction * halfSpacing;

        Vector3 secondAnchor =
            midpoint + direction * halfSpacing;

        firstAnchor.y = firstPosition.y;
        secondAnchor.y = secondPosition.y;

        return new Session
        {
            FirstCharacter = request.Attacker,
            SecondCharacter = request.Target,
            FirstView = firstView,
            SecondView = secondView,
            FirstMover = firstMover,
            SecondMover = secondMover,
            FirstFacing = firstFacing,
            SecondFacing = secondFacing,
            FirstAnchor = firstAnchor,
            SecondAnchor = secondAnchor
        };
    }

    public IEnumerator Enter(
        BattleVisualPlaybackState playback,
        Session session)
    {
        if (playback == null || session == null)
            yield break;

        playback.TrackStagedMover(
            session.FirstMover,
            null);

        playback.TrackStagedMover(
            session.SecondMover,
            null);

        playback.ShouldRestoreFacing =
            session.FirstFacing != null ||
            session.SecondFacing != null;

        yield return MoveParticipants(
            session,
            ClashMotionKey.Enter,
            enterMaximumDuration,
            motionPlaybackSpeed);
    }

    public IEnumerator Reengage(
        Session session,
        float exchangeSpeed)
    {
        if (session == null)
            yield break;

        yield return MoveParticipants(
            session,
            ClashMotionKey.Reengage,
            reengageMaximumDuration,
            motionPlaybackSpeed *
            Mathf.Max(0.01f, exchangeSpeed));
    }

    public IEnumerator Exit(Session session)
    {
        if (session == null)
            yield break;

        yield return PlayPairedMotion(
            session.FirstView,
            ClashMotionKey.Exit,
            session.SecondView,
            ClashMotionKey.Exit,
            1f,
            exitMaximumDuration);
    }

    public IEnumerator PlayPairedMotion(
        CharacterView firstView,
        ClashMotionKey firstKey,
        CharacterView secondView,
        ClashMotionKey secondKey,
        float playbackSpeed,
        float maximumDuration)
    {
        List<Coroutine> routines =
            new List<Coroutine>();

        float safeSpeed =
            motionPlaybackSpeed *
            Mathf.Max(0.01f, playbackSpeed);

        StartAndTrack(
            routines,
            firstView?.PlayClashMotion(
                firstKey,
                safeSpeed,
                maximumDuration,
                missingMotionFallbackDuration));

        if (secondView != null &&
            !ReferenceEquals(firstView, secondView))
        {
            StartAndTrack(
                routines,
                secondView.PlayClashMotion(
                    secondKey,
                    safeSpeed,
                    maximumDuration,
                    missingMotionFallbackDuration));
        }

        if (routines.Count == 0 &&
            missingMotionFallbackDuration > 0f)
        {
            yield return new WaitForSeconds(
                missingMotionFallbackDuration);
            yield break;
        }

        foreach (Coroutine routine in routines)
        {
            if (routine != null)
                yield return routine;
        }
    }

    public IEnumerator Restore(
        BattleVisualPlaybackState playback,
        Session session,
        Character preserveAtCurrentPosition = null)
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
            CharacterActionMover mover = pair.Key;

            if (mover == null)
                continue;

            bool isPreservedMover =
                preserveAtCurrentPosition != null &&
                session != null &&
                ((ReferenceEquals(
                      preserveAtCurrentPosition,
                      playback.RootRequest?.Attacker) &&
                  ReferenceEquals(mover, session.FirstMover)) ||
                 (ReferenceEquals(
                      preserveAtCurrentPosition,
                      playback.RootRequest?.Target) &&
                  ReferenceEquals(mover, session.SecondMover)));

            if (isPreservedMover)
                continue;

            IEnumerator routine =
                pair.Value != null
                    ? mover.ReturnToDefaultPosition(pair.Value)
                    : mover.ReturnToDefaultPosition();

            StartAndTrack(returnRoutines, routine);
        }

        foreach (Coroutine routine in returnRoutines)
        {
            if (routine != null)
                yield return routine;
        }

        playback.ClearStagedMovers();

        if (playback.ShouldRestoreFacing)
        {
            CharacterView preservedView =
                session != null &&
                ReferenceEquals(
                    preserveAtCurrentPosition,
                    playback.RootRequest?.Attacker)
                    ? session.FirstView
                    : session != null &&
                      ReferenceEquals(
                          preserveAtCurrentPosition,
                          playback.RootRequest?.Target)
                        ? session.SecondView
                        : null;

            yield return ReturnFacing(
                session,
                preservedView);

            playback.ShouldRestoreFacing = false;
        }
    }

    private IEnumerator MoveParticipants(
        Session session,
        ClashMotionKey motionKey,
        float maximumMotionDuration,
        float playbackSpeed)
    {
        if (session == null)
            yield break;

        session.FirstFacing?.FacePositionInstant(
            session.SecondAnchor);

        session.SecondFacing?.FacePositionInstant(
            session.FirstAnchor);

        List<Coroutine> routines =
            new List<Coroutine>();

        StartAndTrack(
            routines,
            session.FirstMover?.MoveToWorldPosition(
                session.FirstAnchor,
                stagingMoveSpeed,
                stagingArriveDistance));

        StartAndTrack(
            routines,
            session.SecondMover?.MoveToWorldPosition(
                session.SecondAnchor,
                stagingMoveSpeed,
                stagingArriveDistance));

        StartAndTrack(
            routines,
            session.FirstView?.PlayClashMotion(
                motionKey,
                playbackSpeed,
                maximumMotionDuration,
                missingMotionFallbackDuration));

        if (session.SecondView != null &&
            !ReferenceEquals(
                session.SecondView,
                session.FirstView))
        {
            StartAndTrack(
                routines,
                session.SecondView.PlayClashMotion(
                    motionKey,
                    playbackSpeed,
                    maximumMotionDuration,
                    missingMotionFallbackDuration));
        }

        foreach (Coroutine routine in routines)
        {
            if (routine != null)
                yield return routine;
        }

        yield return FaceParticipants(session);
    }

    private IEnumerator FaceParticipants(Session session)
    {
        if (session == null)
            yield break;

        List<Coroutine> routines =
            new List<Coroutine>();

        Vector3 firstPosition =
            session.FirstMover != null
                ? session.FirstMover.CurrentWorldPosition
                : session.FirstCharacter.transform.position;

        Vector3 secondPosition =
            session.SecondMover != null
                ? session.SecondMover.CurrentWorldPosition
                : session.SecondCharacter.transform.position;

        StartAndTrack(
            routines,
            session.FirstFacing?.FacePositionSmooth(
                secondPosition));

        StartAndTrack(
            routines,
            session.SecondFacing?.FacePositionSmooth(
                firstPosition));

        foreach (Coroutine routine in routines)
        {
            if (routine != null)
                yield return routine;
        }
    }

    private IEnumerator ReturnFacing(
        Session session,
        CharacterView preserveView)
    {
        if (session == null)
            yield break;

        List<Coroutine> routines =
            new List<Coroutine>();

        if (session.FirstFacing != null &&
            !ReferenceEquals(
                session.FirstView,
                preserveView))
        {
            StartAndTrack(
                routines,
                session.FirstFacing.ReturnToDefaultSmooth());
        }

        if (session.SecondFacing != null &&
            !ReferenceEquals(
                session.SecondView,
                preserveView))
        {
            StartAndTrack(
                routines,
                session.SecondFacing.ReturnToDefaultSmooth());
        }

        foreach (Coroutine routine in routines)
        {
            if (routine != null)
                yield return routine;
        }
    }

    private void StartAndTrack(
        ICollection<Coroutine> routines,
        IEnumerator routine)
    {
        if (routine == null ||
            routines == null ||
            coroutineHost == null)
        {
            return;
        }

        Coroutine coroutine =
            coroutineHost.StartCoroutine(routine);

        if (coroutine != null)
            routines.Add(coroutine);
    }
}
