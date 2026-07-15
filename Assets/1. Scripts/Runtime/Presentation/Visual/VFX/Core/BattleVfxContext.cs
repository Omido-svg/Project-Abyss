using UnityEngine;

public class BattleVfxContext
{
    public BattleAction SourceAction;
    public DamageContext DamageContext;
    public DamageResult DamageResult;

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
        new TargetPoint(Target, TargetPart);

    public bool IsCharacterLevelTarget =>
        Target != null && TargetPart == null;

    public int HitIndex = -1;
    public int Damage = 0;

    public bool HasWorldPosition;
    public Vector3 WorldPosition;

    public string StatusKey;
    public StatusEffectVisualPhase? StatusPhase;

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
                attackerView = BattleCameraTargetResolver.GetView(Attacker);
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
                targetView = BattleCameraTargetResolver.GetView(Target);
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
            DamageContext = request.DamageContext,
            DamageResult = request.DamageResult,
            Attacker = request.Attacker,
            Target = request.Target,
            AttackerPart = request.AttackerPart,
            TargetPart = request.TargetPart,
            HitIndex = hitIndex,
            Damage = damage,
            HasWorldPosition = request.HasWorldPosition,
            WorldPosition = request.WorldPosition
        };
    }

    public static BattleVfxContext ForStatus(
        Character target,
        BodyPart targetPart,
        string statusKey,
        StatusEffectVisualPhase phase,
        int damage = 0,
        DamageContext damageContext = null)
    {
        return new BattleVfxContext
        {
            Attacker = damageContext?.Attacker,
            AttackerPart = damageContext?.Action?.OwnerPart,
            Target = target,
            TargetPart = targetPart,
            SourceAction = damageContext?.Action,
            DamageContext = damageContext,
            DamageResult = damageContext?.Result,
            StatusKey = statusKey,
            StatusPhase = phase,
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

    internal void BindPlayback(BattleVisualPlaybackState playbackState)
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

    internal void TrackSpawnedVfx(BattleVfxInstance instance)
    {
        if (playback == null || instance == null || !CanPlayVfx())
            return;

        playback.TrackVfx(instance);
    }

    internal bool TryMarkCuePlayed(string runtimeKey)
    {
        if (string.IsNullOrEmpty(runtimeKey))
            return true;

        return playback == null || playback.TryMarkVfxCue(runtimeKey);
    }

    private static bool ShouldResolveView(
        Character character,
        Character cachedOwner,
        CharacterView cachedView,
        bool wasResolved)
    {
        if (!wasResolved || !ReferenceEquals(character, cachedOwner))
            return true;

        return !ReferenceEquals(cachedView, null) && cachedView == null;
    }
}
