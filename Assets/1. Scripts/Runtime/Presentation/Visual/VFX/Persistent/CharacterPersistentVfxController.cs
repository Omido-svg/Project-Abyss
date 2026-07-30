using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class CharacterPersistentVfxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterView characterView;
    [SerializeField] private BattleVfxPool pool;

    [Header("Initial VFX")]
    [SerializeField] private List<PersistentBattleVfxDefinition> initialVfx = new();

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Dictionary<string, ActivePersistentVfx> activeVfx = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        PlayInitialVfx();
    }

    private void OnDisable()
    {
        StopAll();
    }

    public void Play(PersistentBattleVfxDefinition definition)
    {
        if (definition == null || definition.Prefab == null)
            return;

        ResolveReferences();

        string key = ResolveKey(definition);

        if (activeVfx.ContainsKey(key))
            return;

        Transform anchor = ResolveAnchor(definition);

        if (anchor == null)
        {
            Debug.LogWarning(
                $"[CharacterPersistentVfxController] Anchor 없음 / Definition={definition.name}",
                this);
            return;
        }

        Quaternion rotation =
            anchor.rotation * Quaternion.Euler(definition.LocalEulerOffset);

        Vector3 position =
            anchor.position + anchor.rotation * definition.LocalPositionOffset;

        BattleVfxInstance pooledInstance;

        if (definition.UsePooling)
        {
            pooledInstance = pool.Acquire(
                definition.Prefab,
                position,
                rotation,
                definition.ParentToAnchor ? anchor : null,
                definition.MaxPoolSize);
        }
        else
        {
            GameObject created = Instantiate(
                definition.Prefab,
                position,
                rotation,
                definition.ParentToAnchor ? anchor : null);

            pooledInstance = created.GetComponent<BattleVfxInstance>();

            if (pooledInstance == null)
                pooledInstance = created.AddComponent<BattleVfxInstance>();

            pooledInstance.Configure(null, new BattleVfxPoolKey(definition.Prefab), 0);
            pooledInstance.PrepareForUse(position, rotation, definition.ParentToAnchor ? anchor : null);
        }

        if (pooledInstance == null)
            return;

        if (definition.ParentToAnchor)
        {
            pooledInstance.transform.localPosition = definition.LocalPositionOffset;
            pooledInstance.transform.localRotation = Quaternion.Euler(definition.LocalEulerOffset);
        }

        pooledInstance.transform.localScale = definition.LocalScale;

        ActivePersistentVfx active = new ActivePersistentVfx(pooledInstance);
        activeVfx.Add(key, active);

        if (definition.PlayOnSpawn)
            PlayAllEffects(active);

        if (logDebug)
        {
            Debug.Log(
                $"[CharacterPersistentVfxController] Play / " +
                $"Character={character?.Data?.CharacterName}, Key={key}, Pooled={definition.UsePooling}");
        }
    }

    public void Stop(PersistentBattleVfxDefinition definition)
    {
        if (definition == null)
            return;

        Stop(
            ResolveKey(definition),
            definition.StopVisualEffectsOnRemove,
            definition.DestroyDelay);
    }

    public void Stop(
        string key,
        bool stopEffects = true,
        float destroyDelay = 0.5f)
    {
        if (string.IsNullOrEmpty(key) ||
            !activeVfx.TryGetValue(key, out ActivePersistentVfx active))
        {
            return;
        }

        activeVfx.Remove(key);

        if (stopEffects)
            StopAllEffects(active);

        active.Instance?.ReleaseAfter(Mathf.Max(0f, destroyDelay));

        if (logDebug)
        {
            Debug.Log(
                $"[CharacterPersistentVfxController] Stop / " +
                $"Character={character?.Data?.CharacterName}, Key={key}");
        }
    }

    public void StopAll()
    {
        if (activeVfx.Count == 0)
            return;

        ActivePersistentVfx[] snapshot = new ActivePersistentVfx[activeVfx.Count];
        activeVfx.Values.CopyTo(snapshot, 0);
        activeVfx.Clear();

        foreach (ActivePersistentVfx active in snapshot)
        {
            StopAllEffects(active);
            active.Instance?.Release();
        }
    }

    private void PlayInitialVfx()
    {
        if (initialVfx == null)
            return;

        foreach (PersistentBattleVfxDefinition definition in initialVfx)
            Play(definition);
    }

    private void ResolveReferences()
    {
        if (character == null)
            character = GetComponentInParent<Character>();

        if (characterView == null)
        {
            characterView = character != null
                ? BattleCameraTargetResolver.GetView(character)
                : GetComponentInChildren<CharacterView>(true);
        }

        if (pool == null)
            pool = BattleVfxPool.GetOrCreate();
    }

    private Transform ResolveAnchor(PersistentBattleVfxDefinition definition)
    {
        if (definition == null)
            return transform;

        if (characterView == null)
            ResolveReferences();

        return definition.AnchorType switch
        {
            CharacterPersistentVfxAnchorType.CharacterRoot =>
                character != null ? character.transform : transform,
            CharacterPersistentVfxAnchorType.ViewRoot =>
                characterView != null ? characterView.transform : transform,
            CharacterPersistentVfxAnchorType.LookAtPoint =>
                characterView != null && characterView.LookAtPoint != null
                    ? characterView.LookAtPoint
                    : transform,
            CharacterPersistentVfxAnchorType.BodyPartAnchor =>
                characterView != null
                    ? characterView.GetBodyPartAnchor(definition.BodyPartType)
                    : transform,
            _ => transform
        };
    }

    private static string ResolveKey(PersistentBattleVfxDefinition definition)
    {
        return string.IsNullOrWhiteSpace(definition.Key)
            ? definition.name
            : definition.Key.Trim();
    }

    private static void PlayAllEffects(ActivePersistentVfx active)
    {
        active?.RefreshEffects();

        if (active == null)
            return;

        foreach (VisualEffect visualEffect in active.VisualEffects)
            visualEffect?.Play();
    }

    private static void StopAllEffects(ActivePersistentVfx active)
    {
        active?.RefreshEffects();

        if (active == null)
            return;

        foreach (VisualEffect visualEffect in active.VisualEffects)
            visualEffect?.Stop();
    }

    private sealed class ActivePersistentVfx
    {
        public ActivePersistentVfx(BattleVfxInstance instance)
        {
            Instance = instance;
        }

        public BattleVfxInstance Instance { get; }
        public List<VisualEffect> VisualEffects { get; } = new();

        public void RefreshEffects()
        {
            VisualEffects.Clear();

            if (Instance == null)
                return;

            Instance.GetComponentsInChildren(true, VisualEffects);
        }
    }
}