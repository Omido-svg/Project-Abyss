using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class BattleVfxManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleVfxPool pool;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Stack<List<MonoBehaviour>> playableSearchBufferPool = new();

    private void Awake()
    {
        if (pool == null)
            pool = BattleVfxPool.GetOrCreate();
    }

    public void PlayCue(BattleVfxCue cue, BattleVfxContext context)
    {
        if (cue == null || cue.Vfx == null || context == null)
            return;

        if (!cue.Matches(context) || !context.CanPlayVfx())
            return;

        string runtimeKey = cue.BuildRuntimeKey(-1, context);

        if (!context.TryMarkCuePlayed(runtimeKey))
            return;

        if (cue.Delay <= 0f)
        {
            PlayVfx(cue.Vfx, cue.AnchorType, context);
            return;
        }

        StartCoroutine(PlayCueRoutine(cue, context));
    }

    public void PlayCue(
        BattleVfxCue cue,
        BattleVfxContext context,
        int cueIndex)
    {
        if (cue == null || cue.Vfx == null || context == null)
            return;

        if (!cue.Matches(context) || !context.CanPlayVfx())
            return;

        string runtimeKey = cue.BuildRuntimeKey(cueIndex, context);

        if (!context.TryMarkCuePlayed(runtimeKey))
            return;

        if (cue.Delay <= 0f)
        {
            PlayVfx(cue.Vfx, cue.AnchorType, context);
            return;
        }

        StartCoroutine(PlayCueRoutine(cue, context));
    }

    private IEnumerator PlayCueRoutine(
        BattleVfxCue cue,
        BattleVfxContext context)
    {
        if (cue.Delay > 0f)
            yield return new WaitForSeconds(cue.Delay);

        if (!context.CanPlayVfx())
            yield break;

        PlayVfx(cue.Vfx, cue.AnchorType, context);
    }

    public BattleVfxInstance PlayVfx(
        BattleVfxDefinition definition,
        BattleVfxAnchorType anchorType,
        BattleVfxContext context)
    {
        if (!VfxDefinitionValidator.Validate(definition, this))
            return null;

        if (context == null || !context.CanPlayVfx())
            return null;

        if (pool == null)
            pool = BattleVfxPool.GetOrCreate();

        Transform anchor = ResolveAnchor(anchorType, context);

        if (!TryResolveSpawnPose(
                definition,
                anchor,
                anchorType,
                context,
                out Vector3 position,
                out Quaternion rotation))
        {
            return null;
        }

        Transform parent =
            definition.FollowMode == BattleVfxFollowMode.FollowAnchor
                ? anchor
                : null;

        BattleVfxInstance instance;

        if (definition.UsePooling)
        {
            pool.Prewarm(
                definition.EffectPrefab,
                definition.PrewarmCount,
                definition.MaxPoolSize);

            instance = pool.Acquire(
                definition.EffectPrefab,
                position,
                rotation,
                parent,
                definition.MaxPoolSize);
        }
        else
        {
            GameObject created = Instantiate(
                definition.EffectPrefab,
                position,
                rotation,
                parent);

            instance = created.GetComponent<BattleVfxInstance>();

            if (instance == null)
                instance = created.AddComponent<BattleVfxInstance>();

            instance.Configure(null, new BattleVfxPoolKey(definition.EffectPrefab), 0);
            instance.PrepareForUse(position, rotation, parent);
        }

        if (instance == null)
            return null;

        if (!context.CanPlayVfx())
        {
            instance.Release();
            return null;
        }

        context.TrackSpawnedVfx(instance);
        instance.transform.localScale = definition.Scale;

        BattleVfxPlayData playData = new BattleVfxPlayData
        {
            Definition = definition,
            Context = context,
            Instance = instance.gameObject,
            PooledInstance = instance,
            Anchor = anchor,
            Position = position,
            Rotation = rotation,
            Lifetime = definition.Lifetime,
            Color = definition.Color,
            Intensity = definition.Intensity,
            Radius = definition.Radius
        };

        bool playedByCustomPlayer = TryPlayWithCustomPlayer(instance.gameObject, playData);

        if (!playedByCustomPlayer)
            TryPlayAsVfxGraphFallback(instance.gameObject, definition);

        if (definition.LogSpawn || logDebug)
        {
            Debug.Log(
                $"[BattleVfxManager] Effect Spawn / " +
                $"Definition={definition.name}, " +
                $"Prefab={definition.EffectPrefab.name}, " +
                $"Anchor={anchorType}, " +
                $"Pooled={definition.UsePooling}, " +
                $"Request={context.SourceAction?.ActionId ?? 0}, " +
                $"Hit={context.HitIndex}, " +
                $"CustomPlayer={playedByCustomPlayer}");
        }

        if (definition.DestroyAfterLifetime)
        {
            instance.ReleaseAfter(
                definition.Lifetime,
                definition.UseUnscaledLifetime);
        }

        return instance;
    }

    private bool TryResolveSpawnPose(
        BattleVfxDefinition definition,
        Transform anchor,
        BattleVfxAnchorType anchorType,
        BattleVfxContext context,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (anchor != null)
        {
            position = anchor.position;
            rotation = anchor.rotation;
        }
        else if (context.HasWorldPosition)
        {
            position = context.WorldPosition;
            rotation = Quaternion.identity;
        }
        else if (context.Target != null)
        {
            position = context.Target.transform.position;
            rotation = context.Target.transform.rotation;
        }
        else if (context.Attacker != null)
        {
            position = context.Attacker.transform.position;
            rotation = context.Attacker.transform.rotation;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Debug.LogWarning(
                $"[BattleVfxManager] VFX 위치를 결정할 수 없습니다. " +
                $"Definition={definition.name}, Anchor={anchorType}",
                this);

            return false;
        }

        position += rotation * definition.PositionOffset;
        rotation *= Quaternion.Euler(definition.RotationOffset);
        return true;
    }

    private bool TryPlayWithCustomPlayer(
        GameObject instance,
        BattleVfxPlayData playData)
    {
        if (instance == null)
            return false;

        List<MonoBehaviour> searchBuffer = GetPlayableSearchBuffer();
        bool played = false;

        try
        {
            instance.GetComponentsInChildren(true, searchBuffer);

            foreach (MonoBehaviour behaviour in searchBuffer)
            {
                if (playData.Context != null && !playData.Context.CanPlayVfx())
                    break;

                if (behaviour is not IBattleVfxPlayable playable)
                    continue;

                playable.Play(playData);
                played = true;
            }
        }
        finally
        {
            ReleasePlayableSearchBuffer(searchBuffer);
        }

        return played;
    }

    private List<MonoBehaviour> GetPlayableSearchBuffer()
    {
        return playableSearchBufferPool.Count > 0
            ? playableSearchBufferPool.Pop()
            : new List<MonoBehaviour>();
    }

    private void ReleasePlayableSearchBuffer(List<MonoBehaviour> searchBuffer)
    {
        if (searchBuffer == null)
            return;

        searchBuffer.Clear();
        playableSearchBufferPool.Push(searchBuffer);
    }

    private static void TryPlayAsVfxGraphFallback(
        GameObject instance,
        BattleVfxDefinition definition)
    {
        if (instance == null || definition == null)
            return;

        VisualEffect visualEffect = instance.GetComponentInChildren<VisualEffect>(true);

        if (visualEffect == null)
            return;

        visualEffect.Reinit();

        if (!string.IsNullOrEmpty(definition.PlayEventName))
            visualEffect.SendEvent(definition.PlayEventName);
        else
            visualEffect.Play();
    }

    private static Transform ResolveAnchor(
        BattleVfxAnchorType anchorType,
        BattleVfxContext context)
    {
        if (context == null)
            return null;

        return anchorType switch
        {
            BattleVfxAnchorType.AttackerRoot =>
                context.Attacker != null ? context.Attacker.transform : null,
            BattleVfxAnchorType.TargetRoot =>
                context.Target != null ? context.Target.transform : null,
            BattleVfxAnchorType.AttackerBodyPart =>
                BattleCameraTargetResolver.GetTargetPartAnchor(
                    context.Attacker,
                    context.AttackerPart,
                    context.AttackerView),
            BattleVfxAnchorType.TargetBodyPart =>
                BattleCameraTargetResolver.GetTargetPartAnchor(
                    context.Target,
                    context.TargetPart,
                    context.TargetView),
            BattleVfxAnchorType.AttackerLookAt =>
                BattleCameraTargetResolver.GetLookAtTarget(
                    context.Attacker,
                    context.AttackerView),
            BattleVfxAnchorType.TargetLookAt =>
                BattleCameraTargetResolver.GetLookAtTarget(
                    context.Target,
                    context.TargetView),
            _ => null
        };
    }
}
