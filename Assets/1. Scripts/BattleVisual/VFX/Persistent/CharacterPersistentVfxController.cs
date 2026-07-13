using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class CharacterPersistentVfxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterView characterView;

    [Header("Initial VFX")]
    [SerializeField] private List<PersistentBattleVfxDefinition> initialVfx = new();

    private readonly Dictionary<string, GameObject> activeVfx =
        new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (character == null)
            character = GetComponentInParent<Character>();

        if (characterView == null)
            characterView = GetComponent<CharacterView>();

        if (characterView == null)
            characterView = GetComponentInChildren<CharacterView>(true);
    }

    private void OnEnable()
    {
        PlayInitialVfx();
    }

    private void OnDisable()
    {
        StopAll();
    }

    private void PlayInitialVfx()
    {
        foreach (PersistentBattleVfxDefinition definition in initialVfx)
        {
            if (definition == null)
                continue;

            Play(definition);
        }
    }

    public void Play(
        PersistentBattleVfxDefinition definition)
    {
        if (definition == null)
            return;

        if (definition.Prefab == null)
        {
            Debug.LogWarning(
                $"[CharacterPersistentVfxController] Prefab 없음 / Definition={definition.name}");

            return;
        }

        string key =
            string.IsNullOrEmpty(definition.Key)
                ? definition.name
                : definition.Key;

        if (activeVfx.ContainsKey(key))
            return;

        Transform anchor =
            ResolveAnchor(definition);

        if (anchor == null)
        {
            Debug.LogWarning(
                $"[CharacterPersistentVfxController] Anchor 없음 / Definition={definition.name}");

            return;
        }

        GameObject instance =
            Instantiate(
                definition.Prefab,
                anchor.position,
                anchor.rotation);

        if (definition.ParentToAnchor)
        {
            instance.transform.SetParent(
                anchor,
                worldPositionStays: false);

            instance.transform.localPosition =
                definition.LocalPositionOffset;

            instance.transform.localRotation =
                Quaternion.Euler(
                    definition.LocalEulerOffset);
        }
        else
        {
            instance.transform.position =
                anchor.position +
                definition.LocalPositionOffset;

            instance.transform.rotation =
                anchor.rotation *
                Quaternion.Euler(
                    definition.LocalEulerOffset);
        }

        instance.transform.localScale =
            definition.LocalScale;

        activeVfx[key] =
            instance;

        if (definition.PlayOnSpawn)
        {
            PlayAllEffects(instance);
        }

        Debug.Log(
            $"[CharacterPersistentVfxController] Persistent VFX Play / " +
            $"Character={character?.Data.CharacterName}, Key={key}, Prefab={definition.Prefab.name}");
    }

    public void Stop(
        PersistentBattleVfxDefinition definition)
    {
        if (definition == null)
            return;

        string key =
            string.IsNullOrEmpty(definition.Key)
                ? definition.name
                : definition.Key;

        Stop(key, definition.StopParticleSystemsOnRemove, definition.DestroyDelay);
    }

    public void Stop(
        string key,
        bool stopParticles = true,
        float destroyDelay = 0.5f)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (!activeVfx.TryGetValue(key, out GameObject instance))
            return;

        activeVfx.Remove(key);

        if (instance == null)
            return;

        if (stopParticles)
        {
            StopAllEffects(instance);
        }

        Destroy(
            instance,
            Mathf.Max(0f, destroyDelay));

        Debug.Log(
            $"[CharacterPersistentVfxController] Persistent VFX Stop / " +
            $"Character={character?.Data.CharacterName}, Key={key}");
    }

    public void StopAll()
    {
        List<string> keys =
            new List<string>(activeVfx.Keys);

        foreach (string key in keys)
        {
            Stop(
                key,
                stopParticles: true,
                destroyDelay: 0f);
        }

        activeVfx.Clear();
    }

    private Transform ResolveAnchor(
        PersistentBattleVfxDefinition definition)
    {
        if (definition == null)
            return transform;

        if (characterView == null)
            characterView = GetComponentInChildren<CharacterView>(true);

        switch (definition.AnchorType)
        {
            case CharacterPersistentVfxAnchorType.CharacterRoot:
                if (character != null)
                    return character.transform;

                return transform;

            case CharacterPersistentVfxAnchorType.ViewRoot:
                if (characterView != null)
                    return characterView.transform;

                return transform;

            case CharacterPersistentVfxAnchorType.LookAtPoint:
                if (characterView != null &&
                    characterView.LookAtPoint != null)
                {
                    return characterView.LookAtPoint;
                }

                return transform;

            case CharacterPersistentVfxAnchorType.BodyPartAnchor:
                if (characterView != null)
                {
                    Transform anchor =
                        characterView.GetBodyPartAnchor(
                            definition.BodyPartType);

                    if (anchor != null)
                        return anchor;
                }

                return transform;
        }

        return transform;
    }

    private void PlayAllEffects(
        GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particles =
            instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particles)
        {
            particle.Play(true);
        }

        VisualEffect[] visualEffects =
            instance.GetComponentsInChildren<VisualEffect>(true);

        foreach (VisualEffect visualEffect in visualEffects)
        {
            visualEffect.Play();
        }
    }

    private void StopAllEffects(
        GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particles =
            instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        VisualEffect[] visualEffects =
            instance.GetComponentsInChildren<VisualEffect>(true);

        foreach (VisualEffect visualEffect in visualEffects)
        {
            visualEffect.Stop();
        }
    }
}