using System;
using UnityEngine;

public class BattleVfxContext
{
    public BattleAction SourceAction;

    public Character Attacker;
    public Character Target;

    private Character attackerViewOwner;
    private Character targetViewOwner;
    private CharacterView attackerView;
    private CharacterView targetView;
    private bool attackerViewResolved;
    private bool targetViewResolved;
    private BattleVisualPlaybackState playback;

    public BodyPart AttackerPart;
    public BodyPart TargetPart;

    public TargetPoint ResolvedTargetPoint =>
        new TargetPoint(
            Target,
            TargetPart);

    public bool IsCharacterLevelTarget =>
        Target != null &&
        TargetPart == null;

    public int HitIndex = -1;
    public int Damage = 0;

    public Vector3 WorldPosition;

    public CharacterView AttackerView
    {
        get
        {
            if (ShouldResolveView(
                    Attacker,
                    attackerViewOwner,
                    attackerView,
                    attackerViewResolved))
            {
                attackerView =
                    BattleCameraTargetResolver.GetView(Attacker);

                attackerViewOwner = Attacker;
                attackerViewResolved = true;
            }

            return attackerView;
        }
        set
        {
            attackerView = value;
            attackerViewOwner = Attacker;
            attackerViewResolved = value != null;
        }
    }

    public CharacterView TargetView
    {
        get
        {
            if (ShouldResolveView(
                    Target,
                    targetViewOwner,
                    targetView,
                    targetViewResolved))
            {
                targetView =
                    BattleCameraTargetResolver.GetView(Target);

                targetViewOwner = Target;
                targetViewResolved = true;
            }

            return targetView;
        }
        set
        {
            targetView = value;
            targetViewOwner = Target;
            targetViewResolved = value != null;
        }
    }

    public static BattleVfxContext FromRequest(
        BattleVisualRequest request,
        int hitIndex = -1,
        int damage = 0)
    {
        if (request == null)
            return new BattleVfxContext();

        return new BattleVfxContext
        {
            SourceAction = request.SourceAction,
            Attacker = request.Attacker,
            Target = request.Target,
            AttackerPart =
                request.SourceAction?.OwnerPart,
            TargetPart = request.TargetPart,
            HitIndex = hitIndex,
            Damage = damage
        };
    }

    public void InvalidateResolvedViews()
    {
        attackerView = null;
        targetView = null;
        attackerViewOwner = null;
        targetViewOwner = null;
        attackerViewResolved = false;
        targetViewResolved = false;
    }

    internal void BindPlayback(
        BattleVisualPlaybackState playbackState)
    {
        playback = playbackState;
    }

    internal bool CanPlayVfx()
    {
        return playback == null ||
               (!playback.IsCancellationRequested &&
                !playback.IsCompleted &&
                !playback.IsCleanedUp);
    }

    internal void TrackSpawnedVfx(
        GameObject instance)
    {
        if (playback == null ||
            instance == null ||
            !CanPlayVfx())
        {
            return;
        }

        playback.SpawnedVfxInstances.Add(instance);
    }

    private static bool ShouldResolveView(
        Character character,
        Character cachedOwner,
        CharacterView cachedView,
        bool wasResolved)
    {
        if (!wasResolved ||
            !ReferenceEquals(character, cachedOwner))
        {
            return true;
        }

        // Unity's destroyed-object null differs from a real cached miss. A real
        // null is kept for this short-lived context; a destroyed view is retried.
        return !ReferenceEquals(cachedView, null) &&
               cachedView == null;
    }
}
