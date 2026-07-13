using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class BattleVfxManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Stack<List<MonoBehaviour>> playableSearchBufferPool = new();

    public void PlayCue(
        BattleVfxCue cue,
        BattleVfxContext context)
    {
        if (cue == null)
            return;

        if (cue.Vfx == null)
            return;

        if (context == null)
        {
            Debug.LogWarning("[BattleVfxManager] Context null");
            return;
        }

        if (cue.UseHitIndexFilter &&
            cue.HitIndex != context.HitIndex)
        {
            return;
        }

        if (!context.CanPlayVfx())
            return;

        if (cue.Delay <= 0f)
        {
            PlayVfx(
                cue.Vfx,
                cue.AnchorType,
                context);

            return;
        }

        StartCoroutine(
            PlayCueRoutine(
                cue,
                context));
    }

    private System.Collections.IEnumerator PlayCueRoutine(
        BattleVfxCue cue,
        BattleVfxContext context)
    {
        if (cue.Delay > 0f)
            yield return new WaitForSeconds(cue.Delay);

        if (!context.CanPlayVfx())
            yield break;

        PlayVfx(
            cue.Vfx,
            cue.AnchorType,
            context);
    }

    public void PlayVfx(
        BattleVfxDefinition definition,
        BattleVfxAnchorType anchorType,
        BattleVfxContext context)
    {
        if (definition == null)
        {
            Debug.LogWarning("[BattleVfxManager] Definition null");
            return;
        }

        if (definition.EffectPrefab == null)
        {
            Debug.LogWarning(
                $"[BattleVfxManager] EffectPrefab null / Definition={definition.name}");
            return;
        }

        if (context == null)
        {
            Debug.LogWarning("[BattleVfxManager] Context null");
            return;
        }

        if (!context.CanPlayVfx())
            return;

        Transform anchor =
            ResolveAnchor(
                anchorType,
                context);

        Vector3 position =
            anchor != null
                ? anchor.position
                : context.WorldPosition;

        Quaternion rotation =
            anchor != null
                ? anchor.rotation
                : Quaternion.identity;

        position += rotation * definition.PositionOffset;
        rotation *= Quaternion.Euler(definition.RotationOffset);
        
        if (logDebug)
        {
            Debug.Log(
                $"[BattleVfxManager] VFX 위치 계산 / " +
                $"Definition={definition.name}, " +
                $"AnchorType={anchorType}, " +
                $"Anchor={(anchor != null ? anchor.name : "NULL")}, " +
                $"Position={position}, " +
                $"Target={context?.Target?.Data.CharacterName}, " +
                $"TargetPart={context?.TargetPart?.Type}");
        }

        GameObject instance =
            Instantiate(
                definition.EffectPrefab,
                position,
                rotation);

        if (instance == null)
            return;

        if (!context.CanPlayVfx())
        {
            Destroy(instance);
            return;
        }

        context.TrackSpawnedVfx(instance);

        instance.transform.localScale =
            definition.Scale;

        if (definition.FollowMode == BattleVfxFollowMode.FollowAnchor &&
            anchor != null)
        {
            instance.transform.SetParent(
                anchor,
                true);
        }

        BattleVfxPlayData playData =
            new BattleVfxPlayData
            {
                Definition = definition,
                Context = context,
                Instance = instance,
                Anchor = anchor,
                Position = position,
                Rotation = rotation,
                Lifetime = definition.Lifetime,
                Color = definition.Color,
                Intensity = definition.Intensity,
                Radius = definition.Radius
            };

        bool playedByCustomPlayer =
            TryPlayWithCustomPlayer(
                instance,
                playData);

        if (!context.CanPlayVfx())
            return;

        if (!playedByCustomPlayer)
        {
            TryPlayAsVfxGraphFallback(
                instance,
                definition);
        }

        if (definition.LogSpawn)
        {
            Debug.Log(
                $"[BattleVfxManager] Effect Spawn / " +
                $"Definition={definition.name}, " +
                $"Prefab={definition.EffectPrefab.name}, " +
                $"Anchor={anchorType}, " +
                $"CustomPlayer={playedByCustomPlayer}");
        }

        if (definition.DestroyAfterLifetime)
        {
            Destroy(
                instance,
                definition.Lifetime);
        }
    }

    private bool TryPlayWithCustomPlayer(
        GameObject instance,
        BattleVfxPlayData playData)
    {
        if (instance == null)
            return false;

        List<MonoBehaviour> searchBuffer =
            GetPlayableSearchBuffer();

        bool played = false;

        try
        {
            instance.GetComponentsInChildren(
                true,
                searchBuffer);

            foreach (MonoBehaviour behaviour in searchBuffer)
            {
                if (playData.Context != null &&
                    !playData.Context.CanPlayVfx())
                {
                    break;
                }

                if (behaviour is not IBattleVfxPlayable playable)
                    continue;

                playable.Play(playData);
                played = true;

                if (playData.Context != null &&
                    !playData.Context.CanPlayVfx())
                {
                    break;
                }
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

    private void ReleasePlayableSearchBuffer(
        List<MonoBehaviour> searchBuffer)
    {
        if (searchBuffer == null)
            return;

        searchBuffer.Clear();
        playableSearchBufferPool.Push(searchBuffer);
    }

    private void TryPlayAsVfxGraphFallback(
        GameObject instance,
        BattleVfxDefinition definition)
    {
        if (instance == null ||
            definition == null)
            return;

        VisualEffect visualEffect =
            instance.GetComponentInChildren<VisualEffect>(true);

        if (visualEffect == null)
            return;

        if (!string.IsNullOrEmpty(definition.PlayEventName))
        {
            visualEffect.SendEvent(
                definition.PlayEventName);
        }
        else
        {
            visualEffect.Play();
        }
    }

    private Transform ResolveAnchor(
        BattleVfxAnchorType anchorType,
        BattleVfxContext context)
    {
        if (context == null)
            return null;

        switch (anchorType)
        {
            case BattleVfxAnchorType.AttackerRoot:
                return context.Attacker != null
                    ? context.Attacker.transform
                    : null;

            case BattleVfxAnchorType.TargetRoot:
                return context.Target != null
                    ? context.Target.transform
                    : null;

            case BattleVfxAnchorType.AttackerBodyPart:
                return BattleCameraTargetResolver.GetTargetPartAnchor(
                    context.Attacker,
                    context.AttackerPart,
                    context.AttackerView);

            case BattleVfxAnchorType.TargetBodyPart:
                return BattleCameraTargetResolver.GetTargetPartAnchor(
                    context.Target,
                    context.TargetPart,
                    context.TargetView);

            case BattleVfxAnchorType.AttackerLookAt:
                return BattleCameraTargetResolver.GetLookAtTarget(
                    context.Attacker,
                    context.AttackerView);

            case BattleVfxAnchorType.TargetLookAt:
                return BattleCameraTargetResolver.GetLookAtTarget(
                    context.Target,
                    context.TargetView);

            case BattleVfxAnchorType.WorldPosition:
                return null;
        }

        return null;
    }

}
