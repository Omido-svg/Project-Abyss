using UnityEngine;
using UnityEngine.VFX;

public class BattleVfxManager : MonoBehaviour
{
    public void PlayCue(
        BattleVfxCue cue,
        BattleVfxContext context)
    {
        if (cue == null)
            return;

        if (cue.Vfx == null)
            return;

        if (cue.UseHitIndexFilter &&
            cue.HitIndex != context.HitIndex)
        {
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
        
        Debug.Log(
            $"[BattleVfxManager] VFX 위치 계산 / " +
            $"Definition={definition.name}, " +
            $"AnchorType={anchorType}, " +
            $"Anchor={(anchor != null ? anchor.name : "NULL")}, " +
            $"Position={position}, " +
            $"Target={context?.Target?.Data.CharacterName}, " +
            $"TargetPart={context?.TargetPart?.Type}");

        GameObject instance =
            Instantiate(
                definition.EffectPrefab,
                position,
                rotation);

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

        MonoBehaviour[] behaviours =
            instance.GetComponentsInChildren<MonoBehaviour>(true);

        bool played = false;

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IBattleVfxPlayable playable)
            {
                playable.Play(playData);
                played = true;
            }
        }

        return played;
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
                return GetBodyPartAnchor(
                    context.Attacker,
                    context.AttackerPart);

            case BattleVfxAnchorType.TargetBodyPart:
                return GetBodyPartAnchor(
                    context.Target,
                    context.TargetPart);

            case BattleVfxAnchorType.AttackerLookAt:
                return GetLookAtPoint(
                    context.Attacker);

            case BattleVfxAnchorType.TargetLookAt:
                return GetLookAtPoint(
                    context.Target);

            case BattleVfxAnchorType.WorldPosition:
                return null;
        }

        return null;
    }

    private Transform GetBodyPartAnchor(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return null;

        CharacterView view =
            character.GetComponentInChildren<CharacterView>(true);

        if (view == null)
        {
            Debug.LogWarning(
                $"[BattleVfxManager] CharacterView 없음 / Character={character.Data.CharacterName}");

            return character.transform;
        }

        if (part != null)
        {
            Transform partAnchor =
                view.GetBodyPartAnchor(
                    part.Type);

            if (partAnchor != null)
            {
                Debug.Log(
                    $"[BattleVfxManager] BodyPartAnchor 사용 / " +
                    $"Character={character.Data.CharacterName}, Part={part.Type}, Anchor={partAnchor.name}");

                return partAnchor;
            }

            Debug.LogWarning(
                $"[BattleVfxManager] BodyPartAnchor 못 찾음 / " +
                $"Character={character.Data.CharacterName}, Part={part.Type}");
        }
        else
        {
            Debug.LogWarning(
                $"[BattleVfxManager] TargetPart null / Character={character.Data.CharacterName}");
        }

        if (view.LookAtPoint != null)
        {
            Debug.LogWarning(
                $"[BattleVfxManager] BodyPartAnchor 대신 LookAtPoint 사용 / Character={character.Data.CharacterName}");

            return view.LookAtPoint;
        }

        Debug.LogWarning(
            $"[BattleVfxManager] BodyPartAnchor/LookAtPoint 모두 없음. CharacterRoot 사용 / Character={character.Data.CharacterName}");

        return character.transform;
    }

    private Transform GetLookAtPoint(
        Character character)
    {
        if (character == null)
            return null;

        CharacterView view =
            character.GetComponentInChildren<CharacterView>(true);

        if (view == null)
            return character.transform;

        if (view.LookAtPoint != null)
            return view.LookAtPoint;

        return character.transform;
    }
}