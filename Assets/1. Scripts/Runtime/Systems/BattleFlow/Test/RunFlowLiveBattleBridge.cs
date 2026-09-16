using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Battle Test Scene에만 설치되는 Run Flow live handoff bridge.
/// BattleTestScenarioSwitcher(-20000)가 roster source를 정한 뒤,
/// BattleManager(default order)가 Initialize하기 전에 RunProgression/Emotion/Inventory를 주입한다.
/// </summary>
[DefaultExecutionOrder(-15000)]
[DisallowMultipleComponent]
public sealed class RunFlowLiveBattleBridge : MonoBehaviour
{
    private BattleManager battleManager;
    private BattleTestScenarioSwitcher scenarioSwitcher;
    private RunFlowLiveContentRegistry registry;

    private Character mutatedSourcePrefab;
    private List<CharacterItem> originalItems;
    private List<CharacterAugment> originalAugments;
    private bool sourceRestored;
    private bool activeHandoff;
    private bool returning;
    private BattleEvent boundBattleEvent;

    private void Awake()
    {
        if (!RunFlowLiveHandoff.BattleInFlight ||
            RunFlowLiveHandoff.RetainedSession == null)
        {
            enabled = false;
            return;
        }

        activeHandoff = true;
        registry = RunFlowLiveContentRegistry.Load();
        battleManager = FindFirstObjectByType<BattleManager>();
        scenarioSwitcher = FindFirstObjectByType<BattleTestScenarioSwitcher>();

        if (battleManager == null)
        {
            FailAndReturn("BattleManager를 찾지 못했습니다.");
            return;
        }

        RunFlowTestSession session =
            RunFlowLiveHandoff.RetainedSession;

        bool runConfigured =
            battleManager.ConfigureRunProgression(
                session.Progression);

        bool emotionConfigured =
            battleManager.ConfigureEmotionProgression(
                session.SelectedEmotion,
                registry?.EmotionAugmentCatalog);

        Character sourcePrefab =
            ResolveSelectedPlayerPrefab(
                session.SelectedCharacterKey);

        if (sourcePrefab == null)
        {
            FailAndReturn(
                $"Player prefab을 찾지 못했습니다: {session.SelectedCharacterKey}");
            return;
        }

        int injected =
            InjectRunInventoryIntoSourcePrefab(
                sourcePrefab,
                session.Progression?.Inventory);

        battleManager.BattlePrepared +=
            HandleBattlePrepared;

        Debug.Log(
            "[RunFlowLive] Pre-Battle injection / " +
            $"RunConfigured={runConfigured}, " +
            $"EmotionConfigured={emotionConfigured}, " +
            $"Character={session.SelectedCharacterKey}, " +
            $"Inventory={session.Progression?.Inventory?.Owned?.Count ?? 0}, " +
            $"CombatItemsInjected={injected}",
            this);
    }

    private IEnumerator Start()
    {
        if (!activeHandoff)
            yield break;

        // BattleManager.Start도 한 프레임을 기다리므로 같은 시점에 초기화 실패를 안전하게 감지한다.
        yield return null;

        if (returning)
            yield break;

        if (battleManager == null || !battleManager.IsInitialized)
        {
            RestoreSourcePrefab();
            FailAndReturn("BattleManager 초기화가 완료되지 않았습니다.");
        }
    }

    private void HandleBattlePrepared(BattleContext context)
    {
        RestoreSourcePrefab();

        if (context == null || context.Player == null)
        {
            FailAndReturn("BattlePrepared context/player가 null입니다.");
            return;
        }

        int appliedPartSnapshots =
            ApplyRunAvatarStateToPlayer(
                context.Player,
                RunFlowLiveHandoff.RetainedSession);

        RunFlowTestSession session =
            RunFlowLiveHandoff.RetainedSession;

        int expectedPartSnapshots =
            CountExactPartSnapshots(session?.Avatar);

        int expectedItems =
            CountExpectedCombatItems(
                session?.Progression?.Inventory);

        int equippedItems =
            CountEquippedRunCombatItems(
                context.Player,
                session?.Progression?.Inventory);

        int expectedTempMechanics =
            CountExpectedTempBalanceRunItems(
                session?.Progression?.Inventory);

        int tempMechanics =
            CountTempBalanceRunItemMechanics(
                context.Player);

        bool sameRun =
            session != null &&
            ReferenceEquals(
                context.RunProgression,
                session.Progression);

        bool sameUpgrades =
            session != null &&
            ReferenceEquals(
                context.SkillUpgrades,
                session.Progression.SkillUpgrades);

        string details =
            $"PlayerHP={context.Player.CurrentHP}/{context.Player.MaxCombatHP}, " +
            $"ExpectedItems={expectedItems}, Equipped={equippedItems}, " +
            $"TempItemMechanics={tempMechanics}/{expectedTempMechanics}, " +
            $"ExactPartHP={appliedPartSnapshots}/{expectedPartSnapshots}";

        RunFlowLiveHandoff.MarkTransferAudit(
            sameRun,
            sameUpgrades,
            expectedItems,
            equippedItems,
            expectedTempMechanics,
            tempMechanics,
            expectedPartSnapshots,
            appliedPartSnapshots,
            details);

        Debug.Log(
            "[RunFlowLive] Transfer Audit / " +
            $"SameRun={sameRun}, SameUpgrades={sameUpgrades}, " +
            details,
            this);

        boundBattleEvent = context._battleEvent;
        if (boundBattleEvent != null)
            boundBattleEvent.OnBattleEnded += HandleBattleEnded;
    }

    private void HandleBattleEnded()
    {
        if (returning)
            return;

        BattleContext context =
            battleManager?.BattleContext;

        Character player =
            context?.Player;

        bool playerAlive =
            player != null && !player.IsDead;

        bool allEnemiesDead =
            context?.Enemies != null &&
            context.Enemies.Count > 0;

        if (allEnemiesDead)
        {
            for (int i = 0; i < context.Enemies.Count; i++)
            {
                Character enemy = context.Enemies[i];
                if (enemy != null && !enemy.IsDead)
                {
                    allEnemiesDead = false;
                    break;
                }
            }
        }

        RunFlowLiveBattleResult result =
            new RunFlowLiveBattleResult
            {
                Victory = playerAlive && allEnemiesDead,
                PlayerCurrentHp = player?.CurrentHP ?? 0,
                PlayerMaximumHp = player?.MaxCombatHP ?? 0,
                Message = playerAlive && allEnemiesDead
                    ? "Battle completed: victory"
                    : "Battle completed: defeat"
            };

        if (player?.BodyParts != null)
        {
            for (int i = 0; i < player.BodyParts.Count; i++)
            {
                BodyPart part = player.BodyParts[i];
                if (part == null)
                    continue;

                result.Parts.Add(
                    new RunFlowLivePartSnapshot
                    {
                        Type = part.Type,
                        State = part.State,
                        CurrentHp = part.PartHP,
                        MaximumHp = part.MaxPartHP
                    });
            }
        }

        RunFlowLiveHandoff.RecordBattleResult(result);
        returning = true;

        Debug.Log(
            "[RunFlowLive] Battle result captured / " +
            $"Victory={result.Victory}, " +
            $"HP={result.PlayerCurrentHp}/{result.PlayerMaximumHp}, " +
            $"Parts={result.Parts.Count}",
            this);

        StartCoroutine(ReturnToRunSceneNextFrame());
    }

    private IEnumerator ReturnToRunSceneNextFrame()
    {
        // BattleManager.EndBattleInternal cleanup이 끝난 뒤 Scene을 전환한다.
        yield return null;

        SceneManager.LoadScene(
            RunFlowLiveHandoff.RunSceneName,
            LoadSceneMode.Single);
    }

    private int InjectRunInventoryIntoSourcePrefab(
        Character sourcePrefab,
        RunInventory inventory)
    {
        if (sourcePrefab == null)
            return 0;

        mutatedSourcePrefab = sourcePrefab;
        originalItems = new List<CharacterItem>();
        originalAugments = new List<CharacterAugment>();

        IReadOnlyList<CharacterItem> existingItems =
            sourcePrefab.EquippedItems;

        if (existingItems != null)
        {
            for (int i = 0; i < existingItems.Count; i++)
            {
                CharacterItem item = existingItems[i];
                if (item != null && !originalItems.Contains(item))
                    originalItems.Add(item);
            }
        }

        IReadOnlyList<CharacterAugment> existingAugments =
            sourcePrefab.EquippedAugments;

        if (existingAugments != null)
        {
            for (int i = 0; i < existingAugments.Count; i++)
            {
                CharacterAugment augment = existingAugments[i];
                if (augment != null && !originalAugments.Contains(augment))
                    originalAugments.Add(augment);
            }
        }

        List<CharacterItem> merged =
            new List<CharacterItem>(originalItems);

        int injected = 0;
        IReadOnlyList<RunItemDefinition> owned =
            inventory?.Owned;

        if (owned != null)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                CharacterItem combatItem =
                    owned[i]?.CombatItem;

                if (combatItem == null || merged.Contains(combatItem))
                    continue;

                merged.Add(combatItem);
                injected++;
            }
        }

        sourcePrefab.ConfigureAuthoringCore(
            sourcePrefab.Data,
            sourcePrefab.CombatLoadoutSource,
            merged,
            originalAugments);

        sourceRestored = false;
        return injected;
    }

    private void RestoreSourcePrefab()
    {
        if (sourceRestored || mutatedSourcePrefab == null)
            return;

        mutatedSourcePrefab.ConfigureAuthoringCore(
            mutatedSourcePrefab.Data,
            mutatedSourcePrefab.CombatLoadoutSource,
            originalItems,
            originalAugments);

        sourceRestored = true;
    }

    private Character ResolveSelectedPlayerPrefab(string characterKey)
    {
        Character fromRegistry =
            registry?.ResolvePlayerPrefab(characterKey);

        if (fromRegistry != null)
            return fromRegistry;

        if (scenarioSwitcher == null)
            return null;

        if (string.Equals(characterKey, "Olaf", StringComparison.OrdinalIgnoreCase))
            return scenarioSwitcher.OlafPrefab;

        if (string.Equals(characterKey, "Hifumi", StringComparison.OrdinalIgnoreCase))
            return scenarioSwitcher.HifumiPrefab;

        return scenarioSwitcher.YujinPrefab;
    }

    private static int ApplyRunAvatarStateToPlayer(
        Character player,
        RunFlowTestSession session)
    {
        if (player == null || session?.Avatar == null)
            return 0;

        int exactApplied = 0;
        IReadOnlyList<RunFlowTestPart> sourceParts =
            session.Avatar.Parts;

        if (sourceParts != null)
        {
            if (ApplyPartState(player, PartType.HEAD, sourceParts)) exactApplied++;
            if (ApplyPartState(player, PartType.LEFT_HAND, sourceParts)) exactApplied++;
            if (ApplyPartState(player, PartType.RIGHT_HAND, sourceParts)) exactApplied++;
            if (ApplyPartState(player, PartType.LEGS, sourceParts)) exactApplied++;
        }

        if (player.RuntimeStatus != null)
        {
            player.RuntimeStatus.currentHP =
                Mathf.Clamp(
                    session.Avatar.CurrentHp,
                    1,
                    Mathf.Max(1, player.MaxCombatHP));
        }

        return exactApplied;
    }

    private static bool ApplyPartState(
        Character player,
        PartType type,
        IReadOnlyList<RunFlowTestPart> sourceParts)
    {
        BodyPart part = player.GetBodyPart(type);
        if (part == null)
            return false;

        RunFlowTestPart source = null;
        for (int i = 0; i < sourceParts.Count; i++)
        {
            if (sourceParts[i] != null && sourceParts[i].Type == type)
            {
                source = sourceParts[i];
                break;
            }
        }
        if (source == null)
            return false;
        RunFlowTestPartState state = source.State;

        BodyPartState battleState =
            state switch
            {
                RunFlowTestPartState.Broken => BodyPartState.Broken,
                RunFlowTestPartState.Weakened => BodyPartState.Weakened,
                _ => BodyPartState.Normal
            };

        bool useExact =
            source.HasExactHp &&
            source.MaximumHp > 0f;

        float maximumHp = useExact
            ? Mathf.Max(1f, source.MaximumHp)
            : Mathf.Max(1f, part.MaxPartHP);

        float hp = useExact
            ? Mathf.Clamp(source.CurrentHp, 0f, maximumHp)
            : battleState switch
            {
                BodyPartState.Broken => 0f,
                BodyPartState.Weakened => 1f,
                _ => maximumHp
            };

        if (battleState == BodyPartState.Broken)
            hp = 0f;

        player.SetBodyPartStateForDebug(
            part,
            hp,
            maximumHp,
            battleState,
            clearNonStructuralStatuses: true);

        return useExact;
    }

    private static int CountExactPartSnapshots(RunFlowTestAvatarState avatar)
    {
        IReadOnlyList<RunFlowTestPart> parts = avatar?.Parts;
        if (parts == null)
            return 0;

        int count = 0;
        for (int i = 0; i < parts.Count; i++)
            if (parts[i]?.HasExactHp == true) count++;
        return count;
    }

    private static int CountExpectedCombatItems(RunInventory inventory)
    {
        IReadOnlyList<RunItemDefinition> owned =
            inventory?.Owned;

        if (owned == null)
            return 0;

        HashSet<CharacterItem> unique = new();
        for (int i = 0; i < owned.Count; i++)
        {
            CharacterItem item = owned[i]?.CombatItem;
            if (item != null)
                unique.Add(item);
        }

        return unique.Count;
    }

    private static int CountEquippedRunCombatItems(
        Character player,
        RunInventory inventory)
    {
        if (player == null)
            return 0;

        IReadOnlyList<CharacterItem> equipped =
            player.EquippedItems;

        IReadOnlyList<RunItemDefinition> owned =
            inventory?.Owned;

        if (equipped == null || owned == null)
            return 0;

        HashSet<CharacterItem> expected = new();
        for (int i = 0; i < owned.Count; i++)
        {
            CharacterItem item = owned[i]?.CombatItem;
            if (item != null)
                expected.Add(item);
        }

        int count = 0;
        foreach (CharacterItem expectedItem in expected)
        {
            for (int i = 0; i < equipped.Count; i++)
            {
                if (ReferenceEquals(equipped[i], expectedItem))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }


    private static int CountExpectedTempBalanceRunItems(RunInventory inventory)
    {
        IReadOnlyList<RunItemDefinition> owned =
            inventory?.Owned;

        if (owned == null)
            return 0;

        HashSet<CharacterItem> unique = new();
        for (int i = 0; i < owned.Count; i++)
        {
            CharacterItem item = owned[i]?.CombatItem;
            if (item is TempBalanceRunItem)
                unique.Add(item);
        }

        return unique.Count;
    }

    private static int CountTempBalanceRunItemMechanics(Character player)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            player?.Mechanics;

        if (mechanics == null)
            return 0;

        int count = 0;
        for (int i = 0; i < mechanics.Count; i++)
        {
            CombatMechanic mechanic = mechanics[i];
            if (mechanic != null &&
                string.Equals(
                    mechanic.MechanicName,
                    "TEMP_BALANCE_V1 Run Item",
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private void FailAndReturn(string reason)
    {
        if (returning)
            return;

        RestoreSourcePrefab();
        returning = true;

        Debug.LogError(
            "[RunFlowLive] Live Battle bridge failure / " + reason,
            this);

        RunFlowLiveHandoff.RecordBattleResult(
            new RunFlowLiveBattleResult
            {
                Victory = false,
                PlayerCurrentHp =
                    RunFlowLiveHandoff.RetainedSession?.Avatar?.CurrentHp ?? 1,
                PlayerMaximumHp =
                    RunFlowLiveHandoff.RetainedSession?.Avatar?.MaximumHp ?? 1,
                Message = "Integration failure: " + reason
            });

        StartCoroutine(ReturnToRunSceneNextFrame());
    }

    private void OnDestroy()
    {
        RestoreSourcePrefab();

        if (battleManager != null)
            battleManager.BattlePrepared -= HandleBattlePrepared;

        if (boundBattleEvent != null && !boundBattleEvent.IsDisposed)
            boundBattleEvent.OnBattleEnded -= HandleBattleEnded;
    }
}
