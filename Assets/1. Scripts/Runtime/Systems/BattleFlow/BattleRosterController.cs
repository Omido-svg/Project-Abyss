using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleParticipantSourceMode
{
    Auto,
    UseSceneInstance,
    InstantiateCopy
}

[Serializable]
public sealed class BattleParticipantSource
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private Character source;
    [SerializeField] private BattleParticipantSourceMode mode =
        BattleParticipantSourceMode.Auto;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool snapToSpawnPoint = true;
    [SerializeField] private bool parentSpawnedCopyToRuntimeRoot = true;
    [SerializeField] private string runtimeNameOverride;

    public bool Enabled => enabled;
    public Character Source => source;
    public Transform SpawnPoint => spawnPoint;
    public bool IsConfigured => enabled && source != null;

    public void Configure(
        Character character,
        BattleParticipantSourceMode sourceMode,
        Transform targetSpawnPoint = null)
    {
        source = character;
        mode = sourceMode;
        spawnPoint = targetSpawnPoint;
    }

    public Character Resolve(
        Transform runtimeRoot,
        List<Character> spawnedInstances)
    {
        if (!IsConfigured)
            return null;

        bool sourceIsSceneInstance =
            source.gameObject.scene.IsValid();

        bool instantiateCopy =
            mode switch
            {
                BattleParticipantSourceMode.UseSceneInstance => false,
                BattleParticipantSourceMode.InstantiateCopy => true,
                _ => !sourceIsSceneInstance
            };

        if (!instantiateCopy && !sourceIsSceneInstance)
        {
            Debug.LogWarning(
                "[BattleRosterController] Prefab Asset은 Scene Instance로 " +
                "직접 사용할 수 없어 InstantiateCopy로 전환합니다. " +
                $"Source={source.name}");

            instantiateCopy = true;
        }

        Character result =
            instantiateCopy
                ? CreateCopy(runtimeRoot)
                : source;

        if (result == null)
            return null;

        if (!instantiateCopy &&
            snapToSpawnPoint &&
            spawnPoint != null)
        {
            result.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation);
        }

        if (!string.IsNullOrWhiteSpace(runtimeNameOverride))
            result.name = runtimeNameOverride.Trim();

        result.gameObject.SetActive(true);

        if (instantiateCopy)
            spawnedInstances?.Add(result);

        return result;
    }

    private Character CreateCopy(Transform runtimeRoot)
    {
        Transform parent =
            parentSpawnedCopyToRuntimeRoot
                ? runtimeRoot
                : null;

        Vector3 position =
            spawnPoint != null
                ? spawnPoint.position
                : source.transform.position;

        Quaternion rotation =
            spawnPoint != null
                ? spawnPoint.rotation
                : source.transform.rotation;

        Character copy =
            UnityEngine.Object.Instantiate(
                source,
                position,
                rotation,
                parent);

        if (copy != null)
            copy.transform.localScale = source.transform.localScale;

        return copy;
    }
}

public sealed class BattleRosterSnapshot
{
    public Character Player { get; }
    public List<Character> Enemies { get; }

    public BattleRosterSnapshot(
        Character player,
        List<Character> enemies)
    {
        Player = player;
        Enemies = enemies ?? new List<Character>();
    }
}

public sealed class BattleRosterController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BattleParticipantSource player = new();

    [Header("Enemies")]
    [SerializeField]
    private List<BattleParticipantSource> enemies = new();

    [Header("Runtime Spawn")]
    [SerializeField] private Transform runtimeRoot;
    [SerializeField] private bool destroySpawnedCopiesOnRelease = true;

    [Header("Managed Scene Characters")]
    [Tooltip(
        "현재 Roster에서 선택되지 않은 Scene Character를 " +
        "자동으로 비활성화할 때 사용하는 후보 목록입니다.")]
    [SerializeField]
    private List<Character> managedSceneCharacters = new();

    [SerializeField]
    private bool deactivateUnselectedManagedCharacters = true;

    [Header("Validation")]
    [SerializeField]
    private bool rejectDuplicateCharacterInstances = true;

    private readonly List<Character> spawnedInstances = new();
    private readonly List<Character> resolvedCharacters = new();

    private BattleRosterSnapshot currentSnapshot;

    public BattleParticipantSource PlayerSource => player;
    public IReadOnlyList<BattleParticipantSource> EnemySources => enemies;
    public BattleRosterSnapshot CurrentSnapshot => currentSnapshot;

    public bool HasConfiguredPlayer =>
        player != null &&
        player.IsConfigured;

    public bool TryBuildRoster(out BattleRosterSnapshot snapshot)
    {
        ReleaseSpawnedRoster();
        resolvedCharacters.Clear();

        Character resolvedPlayer =
            player?.Resolve(
                runtimeRoot,
                spawnedInstances);

        if (resolvedPlayer == null)
        {
            snapshot = null;

            Debug.LogError(
                "[BattleRosterController] 플레이어를 생성하거나 찾지 못했습니다.");

            return false;
        }

        resolvedCharacters.Add(resolvedPlayer);

        List<Character> resolvedEnemies = new();

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                BattleParticipantSource source = enemies[i];

                if (source == null || !source.Enabled)
                    continue;

                Character enemy =
                    source.Resolve(
                        runtimeRoot,
                        spawnedInstances);

                if (enemy == null)
                {
                    Debug.LogWarning(
                        "[BattleRosterController] " +
                        $"Enemy Slot {i}를 해석하지 못했습니다.");

                    continue;
                }

                if (rejectDuplicateCharacterInstances &&
                    resolvedCharacters.Contains(enemy))
                {
                    Debug.LogError(
                        "[BattleRosterController] 같은 Character Instance가 " +
                        "두 슬롯에 등록되었습니다. " +
                        $"Character={enemy.name}");

                    ReleaseSpawnedRoster();
                    snapshot = null;
                    return false;
                }

                resolvedCharacters.Add(enemy);
                resolvedEnemies.Add(enemy);
            }
        }

        if (resolvedEnemies.Count == 0)
        {
            Debug.LogError(
                "[BattleRosterController] 활성화된 적이 하나도 없습니다.");

            ReleaseSpawnedRoster();
            snapshot = null;
            return false;
        }

        ApplyManagedCharacterActivation(resolvedCharacters);

        currentSnapshot =
            new BattleRosterSnapshot(
                resolvedPlayer,
                resolvedEnemies);

        snapshot = currentSnapshot;

        BattleDebugLog.Event(
            "[BattleRosterController] Roster Built / " +
            $"Player={GetCharacterName(resolvedPlayer)}, " +
            $"Enemies={resolvedEnemies.Count}",
            BattleLogLevel.Info,
            this);

        return true;
    }

    public void ConfigureSources(
        Character playerSource,
        IReadOnlyList<Character> enemySources,
        bool instantiateCopies)
    {
        BattleParticipantSourceMode sourceMode =
            instantiateCopies
                ? BattleParticipantSourceMode.InstantiateCopy
                : BattleParticipantSourceMode.Auto;

        player ??= new BattleParticipantSource();

        player.Configure(
            playerSource,
            sourceMode);

        enemies ??= new List<BattleParticipantSource>();
        enemies.Clear();

        if (enemySources == null)
            return;

        foreach (Character source in enemySources)
        {
            if (source == null)
                continue;

            BattleParticipantSource slot = new();

            slot.Configure(
                source,
                sourceMode);

            enemies.Add(slot);
        }
    }

    public void ConfigureSceneRoster(
        Character playerInstance,
        IReadOnlyList<Character> enemyInstances,
        Transform playerSpawnPoint,
        IReadOnlyList<Transform> enemySpawnPoints,
        Transform spawnedRuntimeRoot)
    {
        runtimeRoot = spawnedRuntimeRoot;

        player ??= new BattleParticipantSource();

        player.Configure(
            playerInstance,
            BattleParticipantSourceMode.Auto,
            playerSpawnPoint);

        enemies ??= new List<BattleParticipantSource>();
        enemies.Clear();

        managedSceneCharacters ??= new List<Character>();
        managedSceneCharacters.Clear();

        if (playerInstance != null)
            managedSceneCharacters.Add(playerInstance);

        if (enemyInstances == null)
            return;

        for (int i = 0; i < enemyInstances.Count; i++)
        {
            Character enemy = enemyInstances[i];

            if (enemy == null)
                continue;

            Transform spawnPoint =
                enemySpawnPoints != null &&
                i < enemySpawnPoints.Count
                    ? enemySpawnPoints[i]
                    : null;

            BattleParticipantSource slot = new();

            slot.Configure(
                enemy,
                BattleParticipantSourceMode.Auto,
                spawnPoint);

            enemies.Add(slot);

            if (!managedSceneCharacters.Contains(enemy))
                managedSceneCharacters.Add(enemy);
        }
    }

    public void ReleaseSpawnedRoster()
    {
        if (destroySpawnedCopiesOnRelease)
        {
            foreach (Character character in spawnedInstances)
            {
                if (character == null)
                    continue;

                character.gameObject.SetActive(false);

                if (Application.isPlaying)
                    Destroy(character.gameObject);
                else
                    DestroyImmediate(character.gameObject);
            }
        }

        spawnedInstances.Clear();
        resolvedCharacters.Clear();
        currentSnapshot = null;
    }

    private void ApplyManagedCharacterActivation(
        IReadOnlyList<Character> activeCharacters)
    {
        if (!deactivateUnselectedManagedCharacters ||
            managedSceneCharacters == null)
        {
            return;
        }

        foreach (Character character in managedSceneCharacters)
        {
            if (character == null ||
                !character.gameObject.scene.IsValid())
            {
                continue;
            }

            character.gameObject.SetActive(
                ContainsReference(
                    activeCharacters,
                    character));
        }
    }

    private static bool ContainsReference(
        IReadOnlyList<Character> characters,
        Character target)
    {
        if (characters == null || target == null)
            return false;

        for (int i = 0; i < characters.Count; i++)
        {
            if (ReferenceEquals(characters[i], target))
                return true;
        }

        return false;
    }

    private static string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data?.CharacterName ??
               character.name;
    }

    private void OnDestroy()
    {
        ReleaseSpawnedRoster();
    }
}
