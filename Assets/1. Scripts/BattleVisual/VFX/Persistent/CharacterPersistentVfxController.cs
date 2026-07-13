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

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Dictionary<string, ActivePersistentVfx> activeVfx =
        new Dictionary<string, ActivePersistentVfx>();

    private void Awake()
    {
        if (character == null)
            character = GetComponentInParent<Character>();

        if (characterView == null)
        {
            characterView =
                character != null
                    ? BattleCameraTargetResolver.GetView(character)
                    : GetComponentInChildren<CharacterView>(true);
        }
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
        if (initialVfx == null)
            return;

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

        ActivePersistentVfx activeInstance =
            new ActivePersistentVfx(instance);

        activeVfx[key] = activeInstance;

        if (definition.PlayOnSpawn)
        {
            PlayAllEffects(activeInstance);
        }

        if (logDebug)
        {
            Debug.Log(
                $"[CharacterPersistentVfxController] Persistent VFX Play / " +
                $"Character={character?.Data.CharacterName}, Key={key}, Prefab={definition.Prefab.name}");
        }
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

        if (!activeVfx.TryGetValue(
                key,
                out ActivePersistentVfx activeInstance))
            return;

        activeVfx.Remove(key);

        StopInstance(
            activeInstance,
            stopParticles,
            destroyDelay);

        if (logDebug)
        {
            Debug.Log(
                $"[CharacterPersistentVfxController] Persistent VFX Stop / " +
                $"Character={character?.Data.CharacterName}, Key={key}");
        }
    }

    public void StopAll()
    {
        foreach (ActivePersistentVfx activeInstance in activeVfx.Values)
        {
            StopInstance(
                activeInstance,
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
        {
            characterView =
                character != null
                    ? BattleCameraTargetResolver.GetView(character)
                    : GetComponentInChildren<CharacterView>(true);
        }

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
        ActivePersistentVfx activeInstance)
    {
        if (activeInstance == null ||
            activeInstance.Instance == null)
            return;

        activeInstance.RefreshEffects();

        foreach (ParticleSystem particle in activeInstance.Particles)
        {
            if (particle != null)
                particle.Play(true);
        }

        foreach (VisualEffect visualEffect in activeInstance.VisualEffects)
        {
            if (visualEffect != null)
                visualEffect.Play();
        }
    }

    private void StopAllEffects(
        ActivePersistentVfx activeInstance)
    {
        if (activeInstance == null ||
            activeInstance.Instance == null)
            return;

        activeInstance.RefreshEffects();

        foreach (ParticleSystem particle in activeInstance.Particles)
        {
            if (particle == null)
                continue;

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (VisualEffect visualEffect in activeInstance.VisualEffects)
        {
            if (visualEffect != null)
                visualEffect.Stop();
        }
    }

    private void StopInstance(
        ActivePersistentVfx activeInstance,
        bool stopParticles,
        float destroyDelay)
    {
        if (activeInstance == null ||
            activeInstance.Instance == null)
        {
            return;
        }

        if (stopParticles)
            StopAllEffects(activeInstance);

        Destroy(
            activeInstance.Instance,
            Mathf.Max(0f, destroyDelay));
    }

    private sealed class ActivePersistentVfx
    {
        public ActivePersistentVfx(
            GameObject instance)
        {
            Instance = instance;

        }

        public GameObject Instance { get; }

        public List<ParticleSystem> Particles { get; } = new();
        public List<VisualEffect> VisualEffects { get; } = new();

        public void RefreshEffects()
        {
            Particles.Clear();
            VisualEffects.Clear();

            if (Instance == null)
                return;

            Instance.GetComponentsInChildren(
                true,
                Particles);

            Instance.GetComponentsInChildren(
                true,
                VisualEffects);
        }
    }
}
